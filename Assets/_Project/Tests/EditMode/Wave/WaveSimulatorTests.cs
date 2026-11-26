using System.Linq;
using NUnit.Framework;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Wave;
using WhoWhoo3D.Tests.EditMode.Support;

namespace WhoWhoo3D.Tests.EditMode.Wave;

public class WaveSimulatorTests
{
    private static readonly PlayerId P1 = TestBoards.P1;
    private static readonly PlayerId P2 = TestBoards.P2;
    private static readonly CellId A = TestBoards.At(0, 0, 0);

    private static WaveConfig Config => WaveConfig.Default;

    [Test]
    public void QuietBoard_ProducesNoSteps()
    {
        var board = TestBoards.SingleCube();

        var result = WaveSimulator.Simulate(board, Config);

        Assert.AreEqual(0, result.Steps.Count);
        Assert.IsTrue(result.Quiescent);
    }

    [Test]
    public void FullFace_ClaimsEveryPartnerAndReleasesItself()
    {
        var board = TestBoards.SingleCube();
        var sideId = new SideId(A, 0);
        var partners = board.GetSide(sideId).Slots.Select(s => s.Partner).ToList();
        TestBoards.FillFace(board, sideId, P1);

        var result = WaveSimulator.Simulate(board, Config);

        Assert.Greater(result.Steps.Count, 0);
        Assert.IsTrue(board.GetSide(sideId).Slots.All(s => s.IsNeutral),
            "The expanding face spends its tiles and goes back to white.");
        foreach (var partner in partners)
            Assert.AreEqual(P1, board.GetSlot(partner).Owner,
                $"Partner {partner} should have been claimed.");
    }

    [Test]
    public void Expansion_ConquersAnEnemySlot()
    {
        var board = TestBoards.SingleCube();
        var sideId = new SideId(A, 0);
        var target = board.GetSide(sideId).Slots.First().Partner;

        board.SetOwner(target, P2);
        TestBoards.FillFace(board, sideId, P1);

        WaveSimulator.Simulate(board, Config);

        Assert.AreEqual(P1, board.GetSlot(target).Owner, "P2's tile is taken by expansion.");
    }

    [Test]
    public void Conquest_TakesTheDefendersWholeFace()
    {
        var board = TestBoards.SingleCube();
        var sideId = new SideId(A, 0);
        var target = board.GetSide(sideId).Slots.First().Partner;

        // The defender holds two tiles on the face about to be invaded.
        var invaded = board.GetSide(target.Side);
        var alsoHeld = invaded.Slots.First(s => !s.Id.Equals(target));
        board.SetOwner(target, P2);
        board.SetOwner(alsoHeld.Id, P2);

        TestBoards.FillFace(board, sideId, P1);
        WaveSimulator.Simulate(board, Config);

        Assert.AreEqual(P1, board.GetSlot(target).Owner, "The targeted slot is taken.");
        Assert.AreEqual(P1, board.GetSlot(alsoHeld.Id).Owner,
            "The defender's other tiles on that face are taken too, not just released.");
        Assert.AreEqual(0, invaded.OwnedCount(P2));
        Assert.IsTrue(board.Validate().IsValid, board.Validate().ToString());
    }

    [Test]
    public void ArrivalConsumesExactlyOneFreeSlotWhileConquestTakesTheOwnedOnes()
    {
        // The two effects are separate: conquest repaints the defender's tiles, and the
        // arriving tile still needs a home of its own. Without that the attacker's count
        // would not grow at all when expanding onto enemy territory.
        var board = TestBoards.SingleCube();
        var sideId = new SideId(A, 0);
        var arrival = board.GetSide(sideId).Slots.First().Partner;
        var face = board.GetSide(arrival.Side);

        board.SetOwner(arrival, P2);
        var alsoEnemy = face.Slots.First(s => !s.Id.Equals(arrival));
        board.SetOwner(alsoEnemy.Id, P2);

        int freeBefore = face.Slots.Count(s => s.IsNeutral);
        int enemyBefore = face.OwnedCount(P2);

        TestBoards.FillFace(board, sideId, P1);
        WaveSimulator.Simulate(board, Config);

        Assert.AreEqual(freeBefore - 1, face.Slots.Count(s => s.IsNeutral),
            "Exactly one free slot is taken by the arriving tile.");
        Assert.AreEqual(enemyBefore + 1, face.OwnedCount(P1),
            "The attacker gains the conquered tiles plus the new one.");
        Assert.AreEqual(0, face.OwnedCount(P2));
    }

    [Test]
    public void ConquestTakesFaceDisabled_LeavesTheDefenderInPlaceAndContestsTheFace()
    {
        var board = TestBoards.SingleCube();
        var sideId = new SideId(A, 0);
        var arrival = board.GetSide(sideId).Slots.First().Partner;
        var face = board.GetSide(arrival.Side);

        board.SetOwner(arrival, P2);
        var survivor = face.Slots.First(s => !s.Id.Equals(arrival));
        board.SetOwner(survivor.Id, P2);

        TestBoards.FillFace(board, sideId, P1);
        WaveSimulator.Simulate(board, new WaveConfig(Config.MaxChainSteps, ConquestTakesFace: false));

        Assert.AreEqual(P2, board.GetSlot(arrival).Owner, "The defender keeps their tiles.");
        Assert.AreEqual(P2, board.GetSlot(survivor.Id).Owner);
        Assert.Greater(face.OwnedCount(P1), 0, "The arrival still lands on a free slot.");

        // This is precisely why the flag defaults to true: the face is now contested,
        // which R1 makes unclickable by either player.
        Assert.IsFalse(board.Validate().IsValid,
            "With conquest disabled a face can hold two players — a stalling board.");
    }

    [Test]
    public void ArrivalOnASlotWeAlreadyHoldIsRedirectedToAFreeSlot()
    {
        // Regression: the arrival used to hit "already ours, do nothing" and the tile
        // simply evaporated, so expanding onto our own territory gained nothing.
        var board = TestBoards.SingleCube();
        var sideId = new SideId(A, 0);
        var target = board.GetSide(sideId).Slots.First().Partner;

        board.SetOwner(target, P1);
        var arrivalFace = board.GetSide(target.Side);
        int before = arrivalFace.OwnedCount(P1);

        TestBoards.FillFace(board, sideId, P1);
        WaveSimulator.Simulate(board, Config);

        Assert.AreEqual(before + 1, arrivalFace.OwnedCount(P1),
            "The tile must land on a free slot rather than vanish.");
        Assert.IsTrue(board.Validate().IsValid, board.Validate().ToString());
    }

    [Test]
    public void ArrivalIsSpentWhenTheWholeFaceIsAlreadyOurs()
    {
        var board = TestBoards.SingleCube();
        var sideId = new SideId(A, 0);
        var target = board.GetSide(sideId).Slots.First().Partner;

        // Fill the arrival face completely, so there is nowhere useful to redirect to.
        TestBoards.FillFace(board, target.Side, P1);
        int before = board.GetSide(target.Side).OwnedCount(P1);

        TestBoards.FillFace(board, sideId, P1);
        WaveSimulator.Simulate(board, Config);

        Assert.LessOrEqual(board.GetSide(target.Side).OwnedCount(P1), before,
            "With no free slot the tile is simply spent.");
        Assert.IsTrue(board.Validate().IsValid);
    }

    [Test]
    public void EveryExpandedChangeRecordsWhereItCameFrom()
    {
        var board = TestBoards.SingleCube();
        var sideId = new SideId(A, 0);
        var sourceSlots = board.GetSide(sideId).Slots.Select(s => s.Id).ToList();
        TestBoards.FillFace(board, sideId, P1);

        var result = WaveSimulator.Simulate(board, Config);
        var expanded = result.Steps.SelectMany(s => s.Changes)
                             .Where(c => c.Cause == SlotChangeCause.Expanded).ToList();

        Assert.IsNotEmpty(expanded);
        foreach (var change in expanded)
        {
            Assert.IsTrue(change.Origin.HasValue, $"{change} has no origin.");
            Assert.Contains(change.Origin.Value, sourceSlots,
                "An origin must be a slot on the face that expanded.");
        }
    }

    [Test]
    public void PartlyOwnedFace_DoesNotExpand()
    {
        var board = TestBoards.SingleCube();
        var side = board.GetSide(new SideId(A, 0));
        var slots = side.Slots.ToList();

        for (int i = 0; i < slots.Count - 1; i++)
            board.SetOwner(slots[i].Id, P1);

        var result = WaveSimulator.Simulate(board, Config);

        Assert.AreEqual(0, result.Steps.Count, "One white slot left means no trigger.");
    }

    [Test]
    public void MixedOwnerFace_DoesNotExpand()
    {
        var board = TestBoards.SingleCube();
        var slots = board.GetSide(new SideId(A, 0)).Slots.ToList();

        foreach (var slot in slots) board.SetOwner(slot.Id, P1);
        board.SetOwner(slots[0].Id, P2);

        var result = WaveSimulator.Simulate(board, Config);

        Assert.AreEqual(0, result.Steps.Count, "A contested face is not full for anyone.");
    }

    [Test]
    public void Simulate_StopsAsSoonAsTheLastOpponentIsWipedOut()
    {
        // Regression: a saturated board used to run the full MaxChainSteps releasing and
        // refilling itself, long after the game had been decided on step 1.
        var board = TestBoards.SingleCube();
        foreach (var cell in board.Cells.Values)
            foreach (var side in cell.Sides)
                foreach (var slot in side.Slots.ToList())
                    board.SetOwner(slot.Id, P1);

        var victim = board.GetSide(new SideId(A, 0)).Slots.First().Id;
        board.SetOwner(victim, P2);

        var result = WaveSimulator.Simulate(board, Config);

        Assert.IsTrue(result.Decided, "The chain should report that it stopped early.");
        Assert.AreEqual(1, result.Steps.Count,
            "One step decided it; the remaining seven were pure churn.");
        Assert.IsTrue(board.Validate().IsValid);
    }

    [Test]
    public void Simulate_DoesNotReportDecidedWhileTwoPlayersRemain()
    {
        var board = TestBoards.SingleCube();
        var sideId = new SideId(A, 0);
        board.SetOwner(board.GetSide(new SideId(A, 1)).Slots.First().Id, P2);
        TestBoards.FillFace(board, sideId, P1);

        var result = WaveSimulator.Simulate(board, Config);

        Assert.IsFalse(result.Decided);
    }

    [Test]
    public void Simulate_IsBoundedByMaxChainSteps()
    {
        var board = TestBoards.SingleCube();

        // Everything owned: faces keep re-filling each other, so this would loop forever.
        foreach (var cell in board.Cells.Values)
            foreach (var side in cell.Sides)
                foreach (var slot in side.Slots.ToList())
                    board.SetOwner(slot.Id, P1);

        var result = WaveSimulator.Simulate(board, Config);

        Assert.LessOrEqual(result.Steps.Count, Config.MaxChainSteps);
        Assert.IsTrue(board.Validate().IsValid);
    }

    [Test]
    public void Simulate_IsDeterministic()
    {
        var first = TestBoards.TwoCubes();
        var second = TestBoards.TwoCubes();
        var sideId = new SideId(TestBoards.At(0, 0, 0), 0);

        TestBoards.FillFace(first, sideId, P1);
        TestBoards.FillFace(second, sideId, P1);

        var a = WaveSimulator.Simulate(first, Config);
        var b = WaveSimulator.Simulate(second, Config);

        Assert.AreEqual(a.Steps.Count, b.Steps.Count);
        CollectionAssert.AreEqual(
            a.Steps.SelectMany(s => s.Changes).ToList(),
            b.Steps.SelectMany(s => s.Changes).ToList());
    }

    [Test]
    public void Expansion_CrossesTheSeamBetweenCubes()
    {
        var board = TestBoards.TwoCubes();
        var topA = new SideId(TestBoards.At(0, 0, 0), 0);
        var topB = new SideId(TestBoards.At(1, 0, 0), 0);

        // Cube A's top face borders cube B's top face across the seam.
        var acrossSeam = board.GetSide(topA).Slots
            .Select(s => s.Partner)
            .Where(p => p.Side == topB)
            .ToList();
        Assert.AreEqual(1, acrossSeam.Count, "The two top faces share exactly one edge.");

        TestBoards.FillFace(board, topA, P1);
        WaveSimulator.Simulate(board, Config);

        foreach (var slot in acrossSeam)
            Assert.AreEqual(P1, board.GetSlot(slot).Owner, "Expansion should cross onto cube B.");
    }

    [Test]
    public void Result_ReportsChangesForEveryFlip()
    {
        var board = TestBoards.SingleCube();
        var sideId = new SideId(A, 0);
        TestBoards.FillFace(board, sideId, P1);

        var result = WaveSimulator.Simulate(board, Config);
        var changes = result.Steps.SelectMany(s => s.Changes).ToList();

        Assert.IsNotEmpty(changes);
        Assert.IsTrue(changes.All(c => !c.Before.Equals(c.After)),
            "A recorded change must actually change something.");
    }
}

