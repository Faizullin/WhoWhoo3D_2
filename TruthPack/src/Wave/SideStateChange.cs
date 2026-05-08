using WhoWhoo3D.Truth.Topology;

namespace WhoWhoo3D.Truth.Wave;

public readonly record struct SideStateChange(SideId Side, TileStack Before, TileStack After)
{
    public override string ToString() => $"{Side}: {Before} → {After}";
}
