namespace WhoWhoo3D.Domain.Board;

public readonly record struct PlayerId(int Value)
{
    public static PlayerId None => new(0);

    public bool IsNone => Value == 0;

    public override string ToString() => IsNone ? "<none>" : $"P{Value}";
}
