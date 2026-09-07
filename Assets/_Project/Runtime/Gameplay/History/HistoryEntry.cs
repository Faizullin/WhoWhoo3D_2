using System.Collections.Generic;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Wave;

namespace WhoWhoo3D.Gameplay.History;

public enum HistoryKind
{
    /// <summary>A player claimed a white tile.</summary>
    Claim,

    /// <summary>One chain step of an expansion.</summary>
    WaveStep,
}

/// <summary>
/// One atomic board mutation, stored so it can be replayed or reversed.
///
/// Every <see cref="SlotStateChange"/> carries both Before and After, so an entry is
/// exactly reversible without any inverse-operation logic — the class of bug that makes
/// hand-written undo unreliable simply cannot occur here.
/// </summary>
public sealed class HistoryEntry
{
    public HistoryKind Kind { get; }

    /// <summary>Turn number this belongs to, 1-based. Groups entries in the UI.</summary>
    public int TurnNumber { get; }

    public PlayerId Actor { get; }
    public string Label { get; }
    public IReadOnlyList<SlotStateChange> Changes { get; }

    public HistoryEntry(
        HistoryKind kind,
        int turnNumber,
        PlayerId actor,
        string label,
        IReadOnlyList<SlotStateChange> changes)
    {
        Kind = kind;
        TurnNumber = turnNumber;
        Actor = actor;
        Label = label;
        Changes = changes;
    }

    public override string ToString() => $"T{TurnNumber} {Label}";
}

