using System;
using System.Collections.Generic;

namespace WhoWhoo3D.Domain.Board;

public sealed class BoardModel
{
    private readonly Dictionary<CellId, Cell> _cells = new();

    public IReadOnlyDictionary<CellId, Cell> Cells => _cells;

    public Cell AddCell(CellId id, Vec3 position, int sideCount)
    {
        if (id.IsNone)
            throw new ArgumentException("CellId cannot be None.", nameof(id));
        if (_cells.ContainsKey(id))
            throw new InvalidOperationException($"Cell with id {id} already exists.");

        var cell = new Cell(id, position, sideCount);
        _cells[id] = cell;
        return cell;
    }

    public Cell GetCell(CellId id)
    {
        if (!_cells.TryGetValue(id, out var cell))
            throw new KeyNotFoundException($"Cell {id} not found.");
        return cell;
    }

    public Side GetSide(SideId sideId) => GetCell(sideId.Cell).GetSide(sideId.LocalIndex);

    public void LinkSides(SideId a, SideId b)
    {
        if (a.Cell.Equals(b.Cell))
            throw new InvalidOperationException($"Cannot link a cell to itself: {a.Cell}.");

        var sideA = GetSide(a);
        var sideB = GetSide(b);

        if (sideA.Link.HasValue)
            throw new InvalidOperationException($"Side {a} is already linked to {sideA.Link.Value.Other}.");
        if (sideB.Link.HasValue)
            throw new InvalidOperationException($"Side {b} is already linked to {sideB.Link.Value.Other}.");

        sideA.Link = new SideLink(b);
        sideB.Link = new SideLink(a);
    }

    public void SetStack(SideId sideId, TileStack stack)
    {
        if (stack.Count < 0)
            throw new ArgumentOutOfRangeException(nameof(stack), "Stack count cannot be negative.");
        if (stack.Count > 0 && stack.Owner.IsNone)
            throw new ArgumentException("Non-empty stack must have an owner.", nameof(stack));
        if (stack.Count == 0 && !stack.Owner.IsNone)
            throw new ArgumentException("Empty stack must have PlayerId.None.", nameof(stack));

        GetSide(sideId).Stack = stack;
    }

    public void AddTiles(SideId sideId, PlayerId owner, int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive.");
        if (owner.IsNone)
            throw new ArgumentException("Owner cannot be None.", nameof(owner));

        var side = GetSide(sideId);
        if (side.Stack.IsEmpty)
            side.Stack = new TileStack(owner, count);
        else if (side.Stack.Owner.Equals(owner))
            side.Stack = side.Stack with { Count = side.Stack.Count + count };
        else
            throw new InvalidOperationException(
                $"Side {sideId} is owned by {side.Stack.Owner}; cannot add tiles for {owner}. Use SetStack to capture.");
    }

    public void RemoveTiles(SideId sideId, int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive.");

        var side = GetSide(sideId);
        if (side.Stack.Count < count)
            throw new InvalidOperationException(
                $"Side {sideId} has {side.Stack.Count} tiles; cannot remove {count}.");

        var remaining = side.Stack.Count - count;
        side.Stack = remaining == 0 ? TileStack.Empty : side.Stack with { Count = remaining };
    }

    public ValidationResult Validate()
    {
        var errors = new List<string>();

        foreach (var cell in _cells.Values)
        {
            foreach (var side in cell.Sides)
            {
                if (!side.Link.HasValue) continue;

                var targetId = side.Link.Value.Other;

                if (!_cells.TryGetValue(targetId.Cell, out var targetCell))
                {
                    errors.Add($"Side {side.Id} links to non-existent cell {targetId.Cell}.");
                    continue;
                }
                if (targetId.LocalIndex < 0 || targetId.LocalIndex >= targetCell.Sides.Count)
                {
                    errors.Add($"Side {side.Id} links to non-existent side index {targetId.LocalIndex} on {targetId.Cell}.");
                    continue;
                }

                var targetSide = targetCell.GetSide(targetId.LocalIndex);
                if (!targetSide.Link.HasValue || !targetSide.Link.Value.Other.Equals(side.Id))
                {
                    errors.Add($"Side {side.Id} → {targetId} is not reciprocal.");
                }
            }
        }

        return new ValidationResult(errors);
    }
}

public sealed record ValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public override string ToString() =>
        IsValid ? "Valid" : $"Invalid: {string.Join("; ", Errors)}";
}
