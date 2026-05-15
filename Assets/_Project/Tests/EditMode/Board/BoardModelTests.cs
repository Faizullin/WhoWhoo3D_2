using NUnit.Framework;
using System;
using System.Collections.Generic;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Tests.EditMode.Board;

public class BoardModelTests
{
    private static readonly CellId A = new("A");
    private static readonly CellId B = new("B");
    private static readonly PlayerId P1 = new(1);
    private static readonly PlayerId P2 = new(2);

    [Test]
    public void AddCell_AssignsIdAndExposesViaCellsDictionary()
    {
        var board = new BoardModel();
        var cell = board.AddCell(A, Vec3.Zero, sideCount: 6);

        Assert.AreEqual(A, cell.Id);
        Assert.AreEqual(6, cell.Sides.Count);
        Assert.AreSame(cell, board.GetCell(A));
        Assert.IsTrue(board.Cells.ContainsKey(A));
    }

    [Test]
    public void AddCell_RejectsNoneId()
    {
        var board = new BoardModel();
        Assert.Throws<ArgumentException>(() => board.AddCell(CellId.None, Vec3.Zero, 6));
    }

    [Test]
    public void AddCell_RejectsDuplicateId()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        Assert.Throws<InvalidOperationException>(() => board.AddCell(A, Vec3.Zero, 6));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-100)]
    public void AddCell_RejectsInvalidSideCount(int sideCount)
    {
        var board = new BoardModel();
        Assert.Throws<ArgumentOutOfRangeException>(() => board.AddCell(A, Vec3.Zero, sideCount));
    }

    [Test]
    public void LinkSides_BothSidesShareLink()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, new Vec3(1, 0, 0), 6);

        var sideA = new SideId(A, 2);
        var sideB = new SideId(B, 4);
        board.LinkSides(sideA, sideB);

        Assert.AreEqual(new SideLink(sideB), board.GetSide(sideA).Link);
        Assert.AreEqual(new SideLink(sideA), board.GetSide(sideB).Link);
    }

    [Test]
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

    [Test]
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

    [Test]
    public void LinkSides_RejectsSelfLink()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);

        Assert.Throws<InvalidOperationException>(() =>
            board.LinkSides(new SideId(A, 0), new SideId(A, 1)));
    }

    [Test]
    public void LinkSides_RejectsMissingCell()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);

        Assert.Throws<KeyNotFoundException>(() =>
            board.LinkSides(new SideId(A, 0), new SideId(B, 0)));
    }

    [Test]
    public void LinkSides_RejectsOutOfRangeSideIndex()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            board.LinkSides(new SideId(A, 99), new SideId(B, 0)));
    }

    [Test]
    public void Validate_PassesOnReciprocalLinks()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);
        board.LinkSides(new SideId(A, 1), new SideId(B, 3));

        var result = board.Validate();
        Assert.IsTrue(result.IsValid, result.ToString());
    }

    [Test]
    public void Validate_PassesOnEmptyBoard()
    {
        var board = new BoardModel();
        Assert.IsTrue(board.Validate().IsValid);
    }

    [Test]
    public void Validate_FailsOnDanglingLink()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        var sideA = board.GetSide(new SideId(A, 0));
        sideA.Link = new SideLink(new SideId(B, 0));

        var result = board.Validate();
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(1, result.Errors.Count);
        StringAssert.Contains("non-existent cell", result.Errors[0]);
    }

    [Test]
    public void Validate_FailsOnNonReciprocalLink()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);

        var sideA = board.GetSide(new SideId(A, 0));
        sideA.Link = new SideLink(new SideId(B, 0));

        var result = board.Validate();
        Assert.IsFalse(result.IsValid);
        StringAssert.Contains("not reciprocal", result.Errors[0]);
    }

    [Test]
    public void PlayerId_None_IsZero()
    {
        Assert.IsTrue(PlayerId.None.IsNone);
        Assert.IsFalse(P1.IsNone);
    }

    [Test]
    public void Side_StartsWithEmptyStack()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        Assert.IsTrue(board.GetSide(new SideId(A, 0)).Stack.IsEmpty);
    }

    [Test]
    public void AddTiles_OnEmptySide_CreatesStack()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);

        board.AddTiles(sideId, P1, 3);

        Assert.AreEqual(new TileStack(P1, 3), board.GetSide(sideId).Stack);
    }

    [Test]
    public void AddTiles_SameOwner_StacksUp()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);

        board.AddTiles(sideId, P1, 2);
        board.AddTiles(sideId, P1, 3);

        Assert.AreEqual(new TileStack(P1, 5), board.GetSide(sideId).Stack);
    }

    [Test]
    public void AddTiles_DifferentOwner_Throws()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);
        board.AddTiles(sideId, P1, 2);

        Assert.Throws<InvalidOperationException>(() => board.AddTiles(sideId, P2, 1));
    }

    [Test]
    public void RemoveTiles_PartialRemoval_KeepsOwner()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);
        board.AddTiles(sideId, P1, 5);

        board.RemoveTiles(sideId, 2);

        Assert.AreEqual(new TileStack(P1, 3), board.GetSide(sideId).Stack);
    }

    [Test]
    public void RemoveTiles_FullRemoval_ResetsToEmpty()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);
        board.AddTiles(sideId, P1, 3);

        board.RemoveTiles(sideId, 3);

        Assert.IsTrue(board.GetSide(sideId).Stack.IsEmpty);
        Assert.AreEqual(PlayerId.None, board.GetSide(sideId).Stack.Owner);
    }

    [Test]
    public void RemoveTiles_MoreThanPresent_Throws()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);
        board.AddTiles(sideId, P1, 2);

        Assert.Throws<InvalidOperationException>(() => board.RemoveTiles(sideId, 3));
    }

    [Test]
    public void SetStack_ReplacesOwnership_ForCapture()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        var sideId = new SideId(A, 0);
        board.AddTiles(sideId, P1, 5);

        board.SetStack(sideId, new TileStack(P2, 2));

        Assert.AreEqual(new TileStack(P2, 2), board.GetSide(sideId).Stack);
    }

    [Test]
    public void SetStack_RejectsNegativeCount()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            board.SetStack(new SideId(A, 0), new TileStack(P1, -1)));
    }

    [Test]
    public void SetStack_RejectsNoneOwnerWithCount()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        Assert.Throws<ArgumentException>(() =>
            board.SetStack(new SideId(A, 0), new TileStack(PlayerId.None, 5)));
    }

    [Test]
    public void SetStack_RejectsCountZeroWithOwner()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);
        Assert.Throws<ArgumentException>(() =>
            board.SetStack(new SideId(A, 0), new TileStack(P1, 0)));
    }

    [Test]
    public void Cell_TotalTileCount_SumsAllSides()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 3);

        board.AddTiles(new SideId(A, 0), P1, 2);
        board.AddTiles(new SideId(A, 2), P1, 4);

        Assert.AreEqual(6, board.GetCell(A).TotalTileCount);
    }
}
