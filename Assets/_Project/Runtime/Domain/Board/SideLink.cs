namespace WhoWhoo3D.Domain.Board;

public readonly record struct SideLink(SideId Other)
{
    public override string ToString() => $"→{Other}";
}
