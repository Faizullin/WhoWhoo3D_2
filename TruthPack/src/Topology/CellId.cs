namespace WhoWhoo3D.Truth.Topology;

public readonly record struct CellId(string Value)
{
    public static CellId None => new(string.Empty);

    public bool IsNone => string.IsNullOrEmpty(Value);

    public override string ToString() => Value;
}
