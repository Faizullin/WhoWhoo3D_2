namespace WhoWhoo3D.Truth.Topology;

public readonly record struct Vec3(float X, float Y, float Z)
{
    public static Vec3 Zero => new(0f, 0f, 0f);

    public override string ToString() => $"({X}, {Y}, {Z})";
}
