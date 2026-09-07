namespace WhoWhoo3D.Domain.Board;

/// <summary>
/// Identifies one tile slot: an edge position on a face.
/// A cube face has at most four, one per edge of that face.
/// </summary>
public readonly record struct SlotId(SideId Side, int Edge)
{
    public override string ToString() => $"{Side}@{Edge}";
}
