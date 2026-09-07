using WhoWhoo3D.Truth.Topology;
using Xunit;

namespace WhoWhoo3D.Truth.Tests.Topology;

public class BoardModelTests
{
    private static readonly CellId A = new("A");
    private static readonly CellId B = new("B");
    private static readonly PlayerId P1 = new(1);
    private static readonly PlayerId P2 = new(2);

    [Fact]
    public void AddCell_AssignsIdAndExposesViaCellsDictionary()
    {
        var board = new BoardModel();
        var cell = board.AddCell(A, Vec3.Zero, sideCount: 6);

        Assert.Equal(A, cell.Id);
        Assert.Equal(6, cell.Sides.Count);
        Assert.Same(cell, board.GetCell(A));
        Assert.True(board.Cells.ContainsKey(A));
    }

    [Fact]
    public void AddCell_RejectsNoneId()
    {
        var board = new BoardModel();
        Assert.Throws<ArgumentException>(() => board.AddCell(CellId.None, Vec3.Zero, 6));
    }

    [Fact]
    public void AddCell_RejectsDuplicateId()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        Assert.Throws<InvalidOperationException>(() => board.AddCell(A, Vec3.Zero, 6));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void AddCell_RejectsInvalidSideCount(int sideCount)
    {
        var board = new BoardModel();
        Assert.Throws<ArgumentOutOfRangeException>(() => board.AddCell(A, Vec3.Zero, sideCount));
    }

    [Fact]
    public void LinkSides_BothSidesShareLink()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, new Vec3(1, 0, 0), 6);

        var sideA = new SideId(A, 2);
        var sideB = new SideId(B, 4);
        board.LinkSides(sideA, sideB);

        Assert.Equal(new SideLink(sideB), board.GetSide(sideA).Link);
        Assert.Equal(new SideLink(sideA), board.GetSide(sideB).Link);
    }

    [Fact]
    public void LinkSides_RejectsAlreadyLinkedSourceSide()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);
        var c = new CellId("C");
        board.AddCell(c, Vec3.Zero, 6);

        board.LinkSides(new SideId(A, 0), new SideId(B, 0));
        Assert.Throws<InvalidOperationException>(() =>
            board.LinkSides(new SideId(A, 0), new SideId(c, 0)));
    }

    [Fact]
    public void LinkSides_RejectsAlreadyLinkedTargetSide()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);
        var c = new CellId("C");
        board.AddCell(c, Vec3.Zero, 6);

        board.LinkSides(new SideId(A, 0), new SideId(B, 0));
        Assert.Throws<InvalidOperationException>(() =>
            board.LinkSides(new SideId(c, 0), new SideId(B, 0)));
    }

    [Fact]
    public void LinkSides_RejectsSelfLink()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);

        Assert.Throws<InvalidOperationException>(() =>
            board.LinkSides(new SideId(A, 0), new SideId(A, 1)));
    }

    [Fact]
    public void LinkSides_RejectsMissingCell()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);

        Assert.Throws<KeyNotFoundException>(() =>
            board.LinkSides(new SideId(A, 0), new SideId(B, 0)));
    }

    [Fact]
    public void LinkSides_RejectsOutOfRangeSideIndex()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            board.LinkSides(new SideId(A, 99), new SideId(B, 0)));
    }

    [Fact]
    public void Validate_PassesOnReciprocalLinks()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);
        board.LinkSides(new SideId(A, 1), new SideId(B, 3));

        var result = board.Validate();
        Assert.True(result.IsValid, result.ToString());
    }

    [Fact]
    public void Validate_PassesOnEmptyBoard()
    {
        var board = new BoardModel();
        Assert.True(board.Validate().IsValid);
    }

    [Fact]
    public void Validate_FailsOnDanglingLink()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        var sideA = board.GetSide(new SideId(A, 0));
        sideA.Link = new SideLink(new SideId(B, 0));

        var result = board.Validate();
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("non-existent cell", result.Errors[0]);
    }

    [Fact]
    public void Validate_FailsOnNonReciprocalLink()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);

        var sideA = board.GetSide(new SideId(A, 0));
        sideA.Link = new SideLink(new SideId(B, 0));

        var result = board.Validate();
        Assert.False(result.IsValid);
        Assert.Contains("not reciprocal", result.Errors[0]);
    }

    [Fact]
    public void PlayerId_None_IsZero()
    {
        Assert.True(PlayerId.None.IsNone);
        Assert.False(P1.IsNone);
    }

    [Fact]
    public void Side_StartsWithEmptyStack()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        Assert.True(board.GetSide(new SideId(A, 0)).Stack.IsEmpty);
    }

    [Fact]
    public void AddTiles_OnEmptySide_CreatesStack()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);

        board.AddTiles(sideId, P1, 3);

        Assert.Equal(new TileStack(P1, 3), board.GetSide(sideId).Stack);
    }

    [Fact]
    public void AddTiles_SameOwner_StacksUp()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);

        board.AddTiles(sideId, P1, 2);
        board.AddTiles(sideId, P1, 3);

        Assert.Equal(new TileStack(P1, 5), board.GetSide(sideId).Stack);
    }

    [Fact]
    public void AddTiles_DifferentOwner_Throws()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);
        board.AddTiles(sideId, P1, 2);

        Assert.Throws<InvalidOperationException>(() => board.AddTiles(sideId, P2, 1));
    }

    [Fact]
    public void RemoveTiles_PartialRemoval_KeepsOwner()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);
        board.AddTiles(sideId, P1, 5);

        board.RemoveTiles(sideId, 2);

        Assert.Equal(new TileStack(P1, 3), board.GetSide(sideId).Stack);
    }

    [Fact]
    public void RemoveTiles_FullRemoval_ResetsToEmpty()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);
        board.AddTiles(sideId, P1, 3);

        board.RemoveTiles(sideId, 3);

        Assert.True(board.GetSide(sideId).Stack.IsEmpty);
        Assert.Equal(PlayerId.None, board.GetSide(sideId).Stack.Owner);
    }

    [Fact]
    public void RemoveTiles_MoreThanPresent_Throws()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);
        board.AddTiles(sideId, P1, 2);

        Assert.Throws<InvalidOperationException>(() => board.RemoveTiles(sideId, 3));
    }

    [Fact]
    public void SetStack_ReplacesOwnership_ForCapture()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);
        board.AddTiles(sideId, P1, 5);

        board.SetStack(sideId, new TileStack(P2, 2));

        Assert.Equal(new TileStack(P2, 2), board.GetSide(sideId).Stack);
    }

    [Fact]
    public void SetStack_RejectsNegativeCount()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            board.SetStack(new SideId(A, 0), new TileStack(P1, -1)));
    }

    [Fact]
    public void SetStack_RejectsNoneOwnerWithCount()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        Assert.Throws<ArgumentException>(() =>
            board.SetStack(new SideId(A, 0), new TileStack(PlayerId.None, 5)));
    }

    [Fact]
    public void SetStack_RejectsCountZeroWithOwner()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        Assert.Throws<ArgumentException>(() =>
            board.SetStack(new SideId(A, 0), new TileStack(P1, 0)));
    }

    [Fact]
    public void Cell_TotalTileCount_SumsAllSides()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);

        board.AddTiles(new SideId(A, 0), P1, 2);
        board.AddTiles(new SideId(A, 2), P1, 4);

        Assert.Equal(6, board.GetCell(A).TotalTileCount);
    }

    // ── Clone tests ──────────────────────────────────────────────────

    [Fact]
    public void Clone_ProducesIndependentCopy_MutatingCloneDoesNotAffectOriginal()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, new Vec3(1, 0, 0), 6);
        board.LinkSides(new SideId(A, 0), new SideId(B, 0));
        board.SetStack(new SideId(A, 0), new TileStack(P1, 3));

        var clone = board.Clone();
        clone.SetStack(new SideId(A, 0), new TileStack(P2, 1));

        Assert.Equal(new TileStack(P1, 3), board.GetSide(new SideId(A, 0)).Stack);
        Assert.Equal(new TileStack(P2, 1), clone.GetSide(new SideId(A, 0)).Stack);
    }

    [Fact]
    public void Clone_PreservesLinks()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, new Vec3(1, 0, 0), 6);
        board.LinkSides(new SideId(A, 2), new SideId(B, 3));

        var clone = board.Clone();

        Assert.Equal(new SideLink(new SideId(B, 3)), clone.GetSide(new SideId(A, 2)).Link);
        Assert.Equal(new SideLink(new SideId(A, 2)), clone.GetSide(new SideId(B, 3)).Link);
    }

    [Fact]
    public void Clone_PassesValidation()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, new Vec3(1, 0, 0), 6);
        board.LinkSides(new SideId(A, 0), new SideId(B, 0));
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 2));

        var clone = board.Clone();
        var result = clone.Validate();

        Assert.True(result.IsValid, result.ToString());
    }

    [Fact]
    public void Clone_PreservesCellCount()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, new Vec3(1, 0, 0), 4);

        var clone = board.Clone();

        Assert.Equal(2, clone.Cells.Count);
        Assert.Equal(6, clone.GetCell(A).Sides.Count);
        Assert.Equal(4, clone.GetCell(B).Sides.Count);
    }
}
