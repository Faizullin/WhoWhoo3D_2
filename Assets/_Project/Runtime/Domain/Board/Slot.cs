namespace WhoWhoo3D.Domain.Board;

/// <summary>
/// A tile sitting on one edge of a face.
///
/// Slots always exist in pairs: the tile drawn on this face and the tile drawn on the
/// face across the shared edge. <see cref="Partner"/> is the other half and is never
/// absent on a valid board — <see cref="BoardModel.Validate"/> rejects a lone slot.
///
/// <see cref="Owner"/> being <see cref="PlayerId.None"/> means the tile exists but is
/// unclaimed — rendered white, and free for anyone to take.
/// </summary>
public sealed class Slot
{
    public SlotId Id { get; }
    public SlotId Partner { get; internal set; }
    public PlayerId Owner { get; internal set; } = PlayerId.None;

    public bool IsNeutral => Owner.IsNone;

    public Slot(SlotId id, SlotId partner)
    {
        Id = id;
        Partner = partner;
    }

    public override string ToString() =>
        IsNeutral ? $"{Id}:<white>" : $"{Id}:{Owner}";
}
