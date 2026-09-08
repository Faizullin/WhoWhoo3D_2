using System.Linq;
using NUnit.Framework;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Board;
using Coord = WhoWhoo3D.Gameplay.Board.BoardBuilder.Coord;

namespace WhoWhoo3D.Tests.EditMode.Board;

public class BoardBuilderTests
{
    private static readonly PlayerId P1 = new(1);
    private static readonly PlayerId P2 = new(2);

    private static BoardBuilder Slab(int n, int y = 0)
    {
        var builder = new BoardBuilder();
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
                builder.AddCube(new Coord(x, y, z));
        return builder;
    }

    private static int SlotCount(BoardModel board) =>
        board.Cells.Values.Sum(c => c.Sides.Sum(s => s.SlotCount));

    [Test]
    public void SingleCube_WiresTwelveEdgesAsTwentyFourSlots()
    {
        var builder = new BoardBuilder();
        builder.AddCube(new Coord(0, 0, 0));

        var board = builder.Build();

        Assert.AreEqual(24, SlotCount(board), "6 faces x 4 edges.");
        Assert.IsTrue(board.Validate().IsValid, board.Validate().ToString());
    }

    [Test]
    public void TouchingCubesBuryTheFacesBetweenThem()
    {
        var builder = new BoardBuilder();
        builder.AddCube(new Coord(0, 0, 0));
        builder.AddCube(new Coord(1, 0, 0));

        var board = builder.Build();
        var a = board.GetSide(new SideId(BoardBuilder.IdFor(new Coord(0, 0, 0)), 2));

        Assert.IsTrue(a.IsInterior, "The +X face meets the neighbour.");
        Assert.AreEqual(0, a.SlotCount, "A buried face carries nothing.");
        Assert.AreEqual(40, SlotCount(board), "10 exposed faces x 4 edges.");
        Assert.IsTrue(board.Validate().IsValid, board.Validate().ToString());
    }

    [Test]
    public void TopFaceOnlyFilterProducesTheDemoSlabTopology()
    {
        // This is the 4x4 demo board: 24 paired edges, and per-face capacity tiers of
        // 2 (corners), 3 (edges) and 4 (interior) falling out of the topology.
        var board = Slab(4).Build(BoardBuilder.TopFaceOnly);

        Assert.AreEqual(16, board.Cells.Count);
        Assert.AreEqual(48, SlotCount(board));
        Assert.IsTrue(board.Validate().IsValid, board.Validate().ToString());

        var perFace = board.Cells.Values
            .Select(c => c.GetSide(0).SlotCount)
            .GroupBy(n => n)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.AreEqual(4, perFace[2], "four corners");
        Assert.AreEqual(8, perFace[3], "eight edges");
        Assert.AreEqual(4, perFace[4], "four interior");

        int offTop = board.Cells.Values
            .Sum(c => c.Sides.Where(s => s.LocalIndex != 0).Sum(s => s.SlotCount));
        Assert.AreEqual(0, offTop, "The filter keeps every slot on the top face.");
    }

    [Test]
    public void FilteringNeverLeavesALoneSlot()
    {
        // A pair needs both ends to pass the filter, which is what stops a filtered board
        // producing half a tile -- the one thing Validate() rejects outright.
        var board = Slab(3).Build(BoardBuilder.TopFaceOnly);

        Assert.IsTrue(board.Validate().IsValid, board.Validate().ToString());
        foreach (var cell in board.Cells.Values)
            foreach (var side in cell.Sides)
                foreach (var slot in side.Slots)
                    Assert.IsNotNull(board.GetSlot(slot.Partner), $"{slot.Id} has no partner.");
    }

    [Test]
    public void RemovingACubeRewiresItsNeighbours()
    {
        var builder = Slab(2);
        int joined = SlotCount(builder.Build());

        builder.RemoveCube(new Coord(1, 0, 1));
        var board = builder.Build();

        Assert.AreEqual(3, board.Cells.Count);
        Assert.IsTrue(board.Validate().IsValid, board.Validate().ToString());
        Assert.AreNotEqual(joined, SlotCount(board),
            "Removing a cube exposes faces that were buried, changing the network.");
    }

    [Test]
    public void OwnershipSurvivesAnUnrelatedEdit()
    {
        var builder = Slab(3);
        var board = builder.Build(BoardBuilder.TopFaceOnly);
        var slot = board.GetSide(new SideId(BoardBuilder.IdFor(new Coord(0, 0, 0)), 0)).Slots.First().Id;

        builder.SetOwner(slot, P1);
        builder.AddCube(new Coord(0, 1, 0));            // stack a cube somewhere else

        var rebuilt = builder.Build(BoardBuilder.TopFaceOnly);

        Assert.AreEqual(P1, rebuilt.GetSlot(slot).Owner);
    }

    [Test]
    public void OwnershipOfASlotThatNoLongerExistsIsDropped()
    {
        var builder = Slab(2);
        var board = builder.Build();
        var cornerId = BoardBuilder.IdFor(new Coord(0, 0, 0));

        // A slot on the +X face, which burying under a neighbour will destroy.
        var doomed = board.GetSide(new SideId(cornerId, 1)).Slots.First().Id;
        builder.SetOwner(doomed, P1);

        builder.AddCube(new Coord(0, -1, 0));           // bury the -Y face
        var rebuilt = builder.Build();

        Assert.IsTrue(rebuilt.Validate().IsValid, rebuilt.Validate().ToString());
        Assert.IsNull(rebuilt.GetSlot(doomed), "That slot is gone, and so is its owner.");
    }

    [Test]
    public void BuildIsDeterministic()
    {
        var a = Slab(3).Build(BoardBuilder.TopFaceOnly);
        var b = Slab(3).Build(BoardBuilder.TopFaceOnly);

        CollectionAssert.AreEqual(
            a.Cells.Keys.Select(k => k.Value).ToList(),
            b.Cells.Keys.Select(k => k.Value).ToList());
        Assert.AreEqual(SlotCount(a), SlotCount(b));
    }

    [Test]
    public void TryBuildReportsAContestedFaceInsteadOfThrowing()
    {
        var builder = Slab(2);
        var board = builder.Build(BoardBuilder.TopFaceOnly);
        var slots = board.GetSide(new SideId(BoardBuilder.IdFor(new Coord(0, 0, 0)), 0)).Slots.ToList();

        builder.SetOwner(slots[0].Id, P1);
        builder.SetOwner(slots[1].Id, P2);

        bool ok = builder.TryBuild(out _, out var errors, BoardBuilder.TopFaceOnly);

        Assert.IsFalse(ok, "An editor needs to be told, not crashed.");
        Assert.IsTrue(errors.Any(e => e.Contains("contested")));
    }

    [Test]
    public void FromBoardRoundTripsAnExistingMap()
    {
        var original = Slab(3).Build(BoardBuilder.TopFaceOnly);
        var slot = original.GetSide(new SideId(BoardBuilder.IdFor(new Coord(1, 0, 1)), 0)).Slots.First().Id;
        original.SetOwner(slot, P2);

        var rebuilt = BoardBuilder.FromBoard(original).Build(BoardBuilder.TopFaceOnly);

        Assert.AreEqual(original.Cells.Count, rebuilt.Cells.Count);
        Assert.AreEqual(SlotCount(original), SlotCount(rebuilt));
        Assert.AreEqual(P2, rebuilt.GetSlot(slot).Owner, "Ownership is remapped onto the rebuilt ids.");
        Assert.IsTrue(rebuilt.Validate().IsValid);
    }
}
