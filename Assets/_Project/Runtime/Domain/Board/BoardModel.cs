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

    public Slot GetSlot(SlotId slotId) => GetSide(slotId.Side).GetSlot(slotId.Edge);

    /// <summary>
    /// Marks two faces as touching. Both become interior — buried between cells, so they
    /// are neither visible nor playable and may hold no slots.
    /// </summary>
    public void JoinSides(SideId a, SideId b)
    {
        if (a.Cell.Equals(b.Cell))
            throw new InvalidOperationException($"Cannot join a cell to itself: {a.Cell}.");

        var sideA = GetSide(a);
        var sideB = GetSide(b);

        if (sideA.Link.HasValue)
            throw new InvalidOperationException($"Side {a} is already joined to {sideA.Link.Value.Other}.");
        if (sideB.Link.HasValue)
            throw new InvalidOperationException($"Side {b} is already joined to {sideB.Link.Value.Other}.");
        if (sideA.SlotCount > 0 || sideB.SlotCount > 0)
            throw new InvalidOperationException($"Cannot join {a} to {b}: an interior face may not hold slots.");

        sideA.Link = new SideLink(b);
        sideB.Link = new SideLink(a);
    }

    /// <summary>
    /// Creates a linked pair of tile slots across a shared edge. Slots only ever exist in
    /// pairs, so this is the sole way to add one.
    /// </summary>
    public void AddSlotPair(SlotId a, SlotId b)
    {
        if (a.Equals(b))
            throw new InvalidOperationException($"Cannot pair slot {a} with itself.");

        var sideA = GetSide(a.Side);
        var sideB = GetSide(b.Side);

        if (sideA.IsInterior) throw new InvalidOperationException($"Side {a.Side} is interior and cannot hold slots.");
        if (sideB.IsInterior) throw new InvalidOperationException($"Side {b.Side} is interior and cannot hold slots.");

        RequireEdgeInRange(a);
        RequireEdgeInRange(b);

        if (sideA.GetSlot(a.Edge) != null) throw new InvalidOperationException($"Slot {a} already exists.");
        if (sideB.GetSlot(b.Edge) != null) throw new InvalidOperationException($"Slot {b} already exists.");

        sideA.SetSlot(a.Edge, new Slot(a, b));
        sideB.SetSlot(b.Edge, new Slot(b, a));
    }

    private static void RequireEdgeInRange(SlotId id)
    {
        if (id.Edge < 0 || id.Edge >= Side.MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(id), $"Edge {id.Edge} out of range 0..{Side.MaxSlots - 1}.");
    }

    /// <summary>Claims a slot for a player, or releases it back to white with PlayerId.None.</summary>
    public void SetOwner(SlotId slotId, PlayerId owner)
    {
        var slot = GetSlot(slotId) ?? throw new KeyNotFoundException($"Slot {slotId} does not exist.");
        slot.Owner = owner;
    }

    /// <summary>Resets every slot on a face to white. Used when a face expands.</summary>
    public void ClearSide(SideId sideId)
    {
        foreach (var slot in GetSide(sideId).Slots)
            slot.Owner = PlayerId.None;
    }

    public ValidationResult Validate()
    {
        var errors = new List<string>();

        foreach (var cell in _cells.Values)
        {
            foreach (var side in cell.Sides)
            {
                ValidateJoin(side, errors);
                ValidateSlots(side, errors);
                ValidateSingleOwner(side, errors);
            }
        }

        return new ValidationResult(errors);
    }

    private void ValidateJoin(Side side, List<string> errors)
    {
        if (!side.Link.HasValue) return;

        var targetId = side.Link.Value.Other;

        if (!_cells.TryGetValue(targetId.Cell, out var targetCell))
        {
            errors.Add($"Side {side.Id} joins non-existent cell {targetId.Cell}.");
            return;
        }
        if (targetId.LocalIndex < 0 || targetId.LocalIndex >= targetCell.Sides.Count)
        {
            errors.Add($"Side {side.Id} joins non-existent side index {targetId.LocalIndex} on {targetId.Cell}.");
            return;
        }

        var targetSide = targetCell.GetSide(targetId.LocalIndex);
        if (!targetSide.Link.HasValue || !targetSide.Link.Value.Other.Equals(side.Id))
            errors.Add($"Side {side.Id} → {targetId} join is not reciprocal.");

        if (side.SlotCount > 0)
            errors.Add($"Interior side {side.Id} holds {side.SlotCount} slot(s); interior faces must be bare.");
    }

    private void ValidateSlots(Side side, List<string> errors)
    {
        foreach (var slot in side.Slots)
        {
            var partnerSide = FindSide(slot.Partner.Side);
            if (partnerSide == null)
            {
                errors.Add($"Slot {slot.Id} pairs with a non-existent side {slot.Partner.Side}.");
                continue;
            }

            var partner = partnerSide.GetSlot(slot.Partner.Edge);
            if (partner == null)
            {
                // The rule that makes the network a network: no tile stands alone.
                errors.Add($"Slot {slot.Id} has no partner at {slot.Partner}.");
                continue;
            }

            if (!partner.Partner.Equals(slot.Id))
                errors.Add($"Slot {slot.Id} ↔ {slot.Partner} pairing is not reciprocal.");
        }
    }

    private static void ValidateSingleOwner(Side side, List<string> errors)
    {
        var owner = PlayerId.None;

        foreach (var slot in side.Slots)
        {
            if (slot.IsNeutral) continue;

            if (owner.IsNone) owner = slot.Owner;
            else if (!owner.Equals(slot.Owner))
            {
                errors.Add($"Side {side.Id} is contested ({owner} and {slot.Owner}); a face may hold only one player's tiles.");
                return;
            }
        }
    }

    private Side FindSide(SideId id) =>
        _cells.TryGetValue(id.Cell, out var cell) && id.LocalIndex >= 0 && id.LocalIndex < cell.Sides.Count
            ? cell.GetSide(id.LocalIndex)
            : null;

    /// <summary>
    /// Returns an independent copy of the board and its ownership state.
    /// </summary>
    public BoardModel Clone()
    {
        var clone = new BoardModel();

        foreach (var (id, cell) in _cells)
            clone.AddCell(id, cell.Position, cell.Sides.Count);

        // Joins first: they mark faces interior, and slots may not be added to those.
        var joined = new HashSet<(SideId, SideId)>();
        foreach (var (_, cell) in _cells)
        {
            foreach (var side in cell.Sides)
            {
                if (!side.Link.HasValue) continue;
                if (joined.Add(Ordered(side.Id, side.Link.Value.Other)))
                    clone.JoinSides(side.Id, side.Link.Value.Other);
            }
        }

        var paired = new HashSet<(SlotId, SlotId)>();
        foreach (var (_, cell) in _cells)
        {
            foreach (var side in cell.Sides)
            {
                foreach (var slot in side.Slots)
                {
                    var key = OrderedSlots(slot.Id, slot.Partner);
                    if (!paired.Add(key)) continue;

                    clone.AddSlotPair(slot.Id, slot.Partner);
                    clone.GetSlot(slot.Id).Owner = slot.Owner;
                    clone.GetSlot(slot.Partner).Owner = GetSlot(slot.Partner).Owner;
                }
            }
        }

        return clone;
    }

    private static (SideId, SideId) Ordered(SideId a, SideId b) =>
        CompareSides(a, b) <= 0 ? (a, b) : (b, a);

    private static (SlotId, SlotId) OrderedSlots(SlotId a, SlotId b) =>
        CompareSlots(a, b) <= 0 ? (a, b) : (b, a);

    private static int CompareSides(SideId a, SideId b)
    {
        int c = string.CompareOrdinal(a.Cell.Value, b.Cell.Value);
        return c != 0 ? c : a.LocalIndex.CompareTo(b.LocalIndex);
    }

    private static int CompareSlots(SlotId a, SlotId b)
    {
        int c = CompareSides(a.Side, b.Side);
        return c != 0 ? c : a.Edge.CompareTo(b.Edge);
    }
}

public sealed record ValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public override string ToString() =>
        IsValid ? "Valid" : $"Invalid: {string.Join("; ", Errors)}";
}
