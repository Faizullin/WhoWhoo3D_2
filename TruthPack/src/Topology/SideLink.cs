namespace WhoWhoo3D.Truth.Topology;

public readonly record struct SideLink(SideId Other)
{
    public override string ToString() => $"→{Other}";
}
