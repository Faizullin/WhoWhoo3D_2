using System;
using System.Collections.Generic;

namespace WhoWhoo3D.Gameplay.Game;

/// <summary>Seats are ordered by player ID, starting with P1.</summary>
public sealed class MatchSetup
{
    public const int MinPlayers = 2;
    public const int MaxPlayers = 4;

    public string MapName { get; }
    public IReadOnlyList<SeatControl> Seats { get; }

    public MatchSetup(string mapName, params SeatControl[] seats)
    {
        if (string.IsNullOrWhiteSpace(mapName)) throw new ArgumentException("A map name is required.", nameof(mapName));
        if (seats == null) throw new ArgumentNullException(nameof(seats));
        if (seats.Length < MinPlayers || seats.Length > MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(seats), "A match requires two to four seats.");
        foreach (var seat in seats)
            if (seat != SeatControl.LocalHuman && seat != SeatControl.Computer)
                throw new ArgumentOutOfRangeException(nameof(seats), "Unknown seat controller.");

        MapName = mapName;
        Seats = Array.AsReadOnly((SeatControl[])seats.Clone());
    }
}
