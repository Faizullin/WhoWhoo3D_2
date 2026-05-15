namespace WhoWhoo3D.Domain.Board;

public readonly record struct TileStack(PlayerId Owner, int Count)
{
    public static TileStack Empty => new(PlayerId.None, 0);

    public bool IsEmpty => Count == 0;

    public override string ToString() =>
        IsEmpty ? "<empty>" : $"{Owner}×{Count}";
}
