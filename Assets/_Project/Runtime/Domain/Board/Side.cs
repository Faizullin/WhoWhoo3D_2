using System.Collections.Generic;

namespace WhoWhoo3D.Domain.Board;

/// <summary>
/// One face of a cell. Holds up to <see cref="MaxSlots"/> tile slots, one per edge.
///
/// A face pressed against a neighbouring cell is <see cref="IsInterior"/>: it is buried
/// inside the cluster, cannot be seen or clicked, and may hold no slots at all.
/// </summary>
public sealed class Side
{
    public const int MaxSlots = 4;

    private readonly Slot[] _slots = new Slot[MaxSlots];

    public CellId ParentCell { get; }
    public int LocalIndex { get; }
    public SideId Id => new(ParentCell, LocalIndex);

    /// <summary>Set when this face is linked to another cell's face — i.e. buried.</summary>
    public SideLink? Link { get; internal set; }
    public bool IsInterior => Link.HasValue;

    public Side(CellId parentCell, int localIndex)
    {
        ParentCell = parentCell;
        LocalIndex = localIndex;
    }

    /// <summary>The slot on the given edge, or null if this face has none there.</summary>
    public Slot GetSlot(int edge) =>
        edge >= 0 && edge < MaxSlots ? _slots[edge] : null;

    internal void SetSlot(int edge, Slot slot) => _slots[edge] = slot;

    /// <summary>Only the slots that actually exist, in ascending edge order.</summary>
    public IEnumerable<Slot> Slots
    {
        get
        {
            for (int i = 0; i < MaxSlots; i++)
                if (_slots[i] != null)
                    yield return _slots[i];
        }
    }

    public int SlotCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < MaxSlots; i++)
                if (_slots[i] != null) n++;
            return n;
        }
    }

    /// <summary>Tiles on this face claimed by the given player.</summary>
    public int OwnedCount(PlayerId player)
    {
        int n = 0;
        for (int i = 0; i < MaxSlots; i++)
            if (_slots[i] != null && _slots[i].Owner.Equals(player)) n++;
        return n;
    }

    /// <summary>
    /// The single player holding tiles here, or None when the face is empty, all-white,
    /// or contested. A valid board never produces a contested face — see
    /// <see cref="BoardModel.Validate"/>.
    /// </summary>
    public PlayerId SoleOwner()
    {
        var owner = PlayerId.None;

        for (int i = 0; i < MaxSlots; i++)
        {
            var slot = _slots[i];
            if (slot == null || slot.IsNeutral) continue;

            if (owner.IsNone) owner = slot.Owner;
            else if (!owner.Equals(slot.Owner)) return PlayerId.None;
        }

        return owner;
    }

    /// <summary>
    /// True when every slot on this face is claimed by one player — the expansion
    /// trigger. Capacity is per-face: a face with two slots fires on two.
    /// </summary>
    public bool IsFull(out PlayerId owner)
    {
        owner = PlayerId.None;
        if (SlotCount == 0) return false;

        for (int i = 0; i < MaxSlots; i++)
        {
            var slot = _slots[i];
            if (slot == null) continue;
            if (slot.IsNeutral) return false;

            if (owner.IsNone) owner = slot.Owner;
            else if (!owner.Equals(slot.Owner)) return false;
        }

        return !owner.IsNone;
    }
}
