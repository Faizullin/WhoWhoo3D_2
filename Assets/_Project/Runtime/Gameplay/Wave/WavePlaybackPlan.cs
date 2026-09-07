using System;
using System.Collections.Generic;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Wave;

namespace WhoWhoo3D.Gameplay.Wave;

/// <summary>A tile travelling from an expanding face to the slot it claims across the edge.</summary>
public readonly struct TileFlight
{
    public SlotId From { get; }
    public SlotId To { get; }
    public PlayerId Owner { get; }

    public TileFlight(SlotId from, SlotId to, PlayerId owner)
    {
        From = from;
        To = to;
        Owner = owner;
    }

    public override string ToString() => $"{Owner}: {From} → {To}";
}

/// <summary>One chain step's worth of playback: what flies, and what the board looks like after.</summary>
public sealed class WavePlaybackFrame
{
    public IReadOnlyList<SlotStateChange> Changes { get; }
    public IReadOnlyList<TileFlight> Flights { get; }

    public WavePlaybackFrame(IReadOnlyList<SlotStateChange> changes, IReadOnlyList<TileFlight> flights)
    {
        Changes = changes;
        Flights = flights;
    }
}

/// <summary>
/// Turns a <see cref="WaveResult"/> into something replayable.
///
/// The simulator has already mutated the board by the time we get here, so the views
/// show the *final* state. To animate we rewind the affected slots to how they looked
/// before the wave (<see cref="InitialState"/>), then walk forward one frame per chain
/// step. Nothing here touches the model — this is presentation only.
///
/// Pure C#: no UnityEngine reference, so the timeline is covered by Edit Mode tests.
/// </summary>
public sealed class WavePlaybackPlan
{
    public IReadOnlyDictionary<SlotId, PlayerId> InitialState { get; }
    public IReadOnlyList<WavePlaybackFrame> Frames { get; }

    public bool IsEmpty => Frames.Count == 0;

    private WavePlaybackPlan(
        IReadOnlyDictionary<SlotId, PlayerId> initialState,
        IReadOnlyList<WavePlaybackFrame> frames)
    {
        InitialState = initialState;
        Frames = frames;
    }

    public static WavePlaybackPlan Build(WaveResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var initial = new Dictionary<SlotId, PlayerId>();
        var frames = new List<WavePlaybackFrame>(result.Steps.Count);

        foreach (var step in result.Steps)
        {
            var flights = new List<TileFlight>();

            foreach (var change in step.Changes)
            {
                // The first time a slot is touched, its Before value is the pre-wave truth.
                if (!initial.ContainsKey(change.Slot))
                    initial[change.Slot] = change.Before;

                // ONLY an Expanded change actually crossed an edge. Absorbed slots
                // changed hands where they stand, and Released ones simply went white.
                // Drawing a flight for those sends a tile in from an unrelated part of
                // the board -- which is precisely the bug this enum exists to prevent.
                if (change.Cause != SlotChangeCause.Expanded)
                    continue;

                var from = change.Origin
                    ?? throw new InvalidOperationException(
                        $"Expanded change on {change.Slot} has no recorded origin. " +
                        "WaveSimulator must record where an arriving tile travelled from.");

                flights.Add(new TileFlight(from, change.Slot, change.After));
            }

            frames.Add(new WavePlaybackFrame(step.Changes, flights));
        }

        return new WavePlaybackPlan(initial, frames);
    }
}

