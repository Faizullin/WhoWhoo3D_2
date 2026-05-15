namespace WhoWhoo3D.Domain.Board;

public sealed class Side
{
    public CellId ParentCell { get; }
    public int LocalIndex { get; }
    public SideId Id => new(ParentCell, LocalIndex);
    public SideLink? Link { get; internal set; }
    public TileStack Stack { get; internal set; } = TileStack.Empty;

    public Side(CellId parentCell, int localIndex)
    {
        ParentCell = parentCell;
        LocalIndex = localIndex;
    }
}
