using System;
using System.Collections.Generic;

namespace WhoWhoo3D.Domain.Board;

public sealed class Cell
{
    public CellId Id { get; }
    public Vec3 Position { get; }
    public IReadOnlyList<Side> Sides { get; }

    public Cell(CellId id, Vec3 position, int sideCount)
    {
        if (sideCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(sideCount), "A cell must have at least one side.");

        Id = id;
        Position = position;
        var sides = new Side[sideCount];
        for (int i = 0; i < sideCount; i++)
            sides[i] = new Side(id, i);
        Sides = sides;
    }

    public Side GetSide(int localIndex)
    {
        if (localIndex < 0 || localIndex >= Sides.Count)
            throw new ArgumentOutOfRangeException(nameof(localIndex), $"Cell {Id} has no side at index {localIndex}.");
        return Sides[localIndex];
    }

    /// <summary>Faces that are visible and playable — everything not buried against a neighbour.</summary>
    public IEnumerable<Side> ExposedSides
    {
        get
        {
            foreach (var side in Sides)
                if (!side.IsInterior)
                    yield return side;
        }
    }

    /// <summary>Slots on this cell claimed by the given player.</summary>
    public int OwnedCount(PlayerId player)
    {
        int n = 0;
        foreach (var side in Sides)
            n += side.OwnedCount(player);
        return n;
    }
}
