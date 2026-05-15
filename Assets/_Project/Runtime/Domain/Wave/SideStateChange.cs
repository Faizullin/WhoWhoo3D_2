using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Domain.Wave;

public readonly record struct SideStateChange(SideId Side, TileStack Before, TileStack After)
{
    public override string ToString() => $"{Side}: {Before} → {After}";
}
