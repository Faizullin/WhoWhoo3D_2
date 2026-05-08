namespace WhoWhoo3D.Truth.Topology;

public readonly record struct SideId(CellId Cell, int LocalIndex)
{
    public override string ToString() => $"{Cell}#{LocalIndex}";
}
