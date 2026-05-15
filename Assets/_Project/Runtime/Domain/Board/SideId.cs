namespace WhoWhoo3D.Domain.Board;

public readonly record struct SideId(CellId Cell, int LocalIndex)
{
    public override string ToString() => $"{Cell}#{LocalIndex}";
}
