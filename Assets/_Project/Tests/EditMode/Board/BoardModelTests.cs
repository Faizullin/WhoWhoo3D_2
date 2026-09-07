using System.Linq;
using NUnit.Framework;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Tests.EditMode.Support;

namespace WhoWhoo3D.Tests.EditMode.Board;

public class BoardModelTests
{
    private static readonly PlayerId P1 = TestBoards.P1;
    private static readonly PlayerId P2 = TestBoards.P2;
    private static readonly CellId A = new("cube_a");

    [Test]
    public void SingleCube_WiresTwelveReciprocalPairs()
    {
        var board = TestBoards.SingleCube("cube_a");

        int slots = board.GetCell(A).Sides.Sum(s => s.SlotCount);
        Assert.AreEqual(24, slots, "6 faces x 4 edges = 24 slot halves.");
        Assert.IsTrue(board.Validate().IsValid, board.Validate().ToString());
    }

    [Test]
    public void TwoCubes_JoinedFacesAreInteriorAndBare()
    {
        var board = TestBoards.TwoCubes();

        var joined = board.GetSide(new SideId(new CellId("cube_a"), 2));
        Assert.IsTrue(joined.IsInterior);
        Assert.AreEqual(0, joined.SlotCount, "An interior face must hold no slots.");
        Assert.IsTrue(board.Validate().IsValid, board.Validate().ToString());
    }

    [Test]
    public void TwoCubes_ExposedSurfaceHasFortySlots()
    {
        var board = TestBoards.TwoCubes();

        int slots = board.Cells.Values.Sum(c => c.Sides.Sum(s => s.SlotCount));
        Assert.AreEqual(40, slots, "10 exposed faces x 4 edges.");
    }

    [Test]
    public void JoinSides_RejectsAFaceThatAlreadyHoldsSlots()
    {
        var board = TestBoards.SingleCube("cube_a");
        var other = new CellId("cube_b");
        board.AddCell(other, new Vec3(1, 0, 0), 6);

        // Face 2 was wired with slots by SingleCube, so it cannot become interior.
        Assert.Throws<System.InvalidOperationException>(
            () => board.JoinSides(new SideId(A, 2), new SideId(other, 3)));
    }

    [Test]
    public void AddSlotPair_RejectsInteriorFaces()
    {
        var board = new BoardModel();
        var a = new CellId("a");
        var b = new CellId("b");
        board.AddCell(a, Vec3.Zero, 6);
        board.AddCell(b, new Vec3(1, 0, 0), 6);
        board.JoinSides(new SideId(a, 2), new SideId(b, 3));

        Assert.Throws<System.InvalidOperationException>(() => board.AddSlotPair(
            new SlotId(new SideId(a, 2), 0),
            new SlotId(new SideId(a, 0), 0)));
    }

    [Test]
    public void Validate_RejectsAContestedFace()
    {
        var board = TestBoards.SingleCube("cube_a");
        var side = board.GetSide(new SideId(A, 0));
        var slots = side.Slots.ToList();

        board.SetOwner(slots[0].Id, P1);
        board.SetOwner(slots[1].Id, P2);

        var result = board.Validate();
        Assert.IsFalse(result.IsValid, "A face holding two players' tiles is invalid.");
        StringAssert.Contains("contested", result.ToString());
    }

    [Test]
    public void SetOwner_AndRelease()
    {
        var board = TestBoards.SingleCube("cube_a");
        var slot = board.GetSide(new SideId(A, 0)).Slots.First();

        board.SetOwner(slot.Id, P1);
        Assert.AreEqual(P1, board.GetSlot(slot.Id).Owner);
        Assert.IsFalse(board.GetSlot(slot.Id).IsNeutral);

        board.SetOwner(slot.Id, PlayerId.None);
        Assert.IsTrue(board.GetSlot(slot.Id).IsNeutral);
    }

    [Test]
    public void IsFull_OnlyWhenEverySlotIsOneOwner()
    {
        var board = TestBoards.SingleCube("cube_a");
        var sideId = new SideId(A, 0);
        var side = board.GetSide(sideId);
        var slots = side.Slots.ToList();

        Assert.IsFalse(side.IsFull(out _), "All-white face is not full.");

        for (int i = 0; i < slots.Count - 1; i++)
            board.SetOwner(slots[i].Id, P1);
        Assert.IsFalse(side.IsFull(out _), "One white slot left means not full.");

        board.SetOwner(slots[^1].Id, P1);
        Assert.IsTrue(side.IsFull(out var owner));
        Assert.AreEqual(P1, owner);
    }

    [Test]
    public void IsFull_IsRelativeToTheFacesOwnSlotCount()
    {
        // A face with two slots fires on two, not four.
        var board = new BoardModel();
        var a = new CellId("a");
        board.AddCell(a, Vec3.Zero, 6);
        board.AddSlotPair(new SlotId(new SideId(a, 0), 0), new SlotId(new SideId(a, 2), 0));
        board.AddSlotPair(new SlotId(new SideId(a, 0), 1), new SlotId(new SideId(a, 3), 0));

        var side = board.GetSide(new SideId(a, 0));
        Assert.AreEqual(2, side.SlotCount);

        foreach (var slot in side.Slots.ToList())
            board.SetOwner(slot.Id, P1);

        Assert.IsTrue(side.IsFull(out var owner), "Two of two counts as full.");
        Assert.AreEqual(P1, owner);
    }

    [Test]
    public void ClearSide_ReleasesEverySlotToWhite()
    {
        var board = TestBoards.SingleCube("cube_a");
        var sideId = new SideId(A, 0);
        TestBoards.FillFace(board, sideId, P1);

        board.ClearSide(sideId);

        Assert.IsTrue(board.GetSide(sideId).Slots.All(s => s.IsNeutral));
    }

    [Test]
    public void Clone_IsIndependentOfTheOriginal()
    {
        var board = TestBoards.TwoCubes();
        var sideId = new SideId(new CellId("cube_a"), 0);
        TestBoards.FillFace(board, sideId, P1);

        var clone = board.Clone();
        Assert.IsTrue(clone.Validate().IsValid, clone.Validate().ToString());

        clone.ClearSide(sideId);

        Assert.IsTrue(board.GetSide(sideId).Slots.All(s => s.Owner.Equals(P1)),
            "Mutating the clone must not touch the original.");
        Assert.IsTrue(clone.GetSide(sideId).Slots.All(s => s.IsNeutral));
    }

    [Test]
    public void Clone_PreservesSlotPairingAndJoins()
    {
        var board = TestBoards.TwoCubes();
        var clone = board.Clone();

        int original = board.Cells.Values.Sum(c => c.Sides.Sum(s => s.SlotCount));
        int copied = clone.Cells.Values.Sum(c => c.Sides.Sum(s => s.SlotCount));

        Assert.AreEqual(original, copied);
        Assert.IsTrue(clone.GetSide(new SideId(new CellId("cube_a"), 2)).IsInterior);
    }
}

