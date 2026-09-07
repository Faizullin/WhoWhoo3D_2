using System.Linq;
using NUnit.Framework;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Wave;
using WhoWhoo3D.Gameplay.Wave;
using WhoWhoo3D.Tests.EditMode.Support;

namespace WhoWhoo3D.Tests.EditMode.Wave;

public class WavePlaybackPlanTests
{
    private static readonly PlayerId P1 = TestBoards.P1;
    private static readonly PlayerId P2 = TestBoards.P2;
    private static readonly CellId Cube = new("cube");

    private static WavePlaybackPlan PlanFor(BoardModel board) =>
        WavePlaybackPlan.Build(WaveSimulator.Simulate(board, WaveConfig.Default));

    [Test]
    public void Build_ProducesOneFramePerChainStep()
    {
        var board = TestBoards.SingleCube("cube");
        TestBoards.FillFace(board, new SideId(Cube, 0), P1);

        var result = WaveSimulator.Simulate(board, WaveConfig.Default);
        var plan = WavePlaybackPlan.Build(result);

        Assert.AreEqual(result.Steps.Count, plan.Frames.Count);
        Assert.IsFalse(plan.IsEmpty);
    }

    [Test]
    public void InitialState_CapturesPreWaveOwnersNotPostWaveOwners()
    {
        var board = TestBoards.SingleCube("cube");
        var face = new SideId(Cube, 0);
        var slots = board.GetSide(face).Slots.Select(s => s.Id).ToList();
        TestBoards.FillFace(board, face, P1);

        var plan = PlanFor(board);

        // The simulator has already released the face to white; the plan must still
        // remember it as P1 or playback would start at the end.
        foreach (var slot in slots)
            Assert.AreEqual(P1, plan.InitialState[slot]);
        Assert.IsTrue(board.GetSide(face).Slots.All(s => s.IsNeutral));
    }

    [Test]
    public void Build_EmitsAFlightIntoEverySlotThatWasClaimed()
    {
        var board = TestBoards.SingleCube("cube");
        var face = new SideId(Cube, 0);
        var partners = board.GetSide(face).Slots.Select(s => s.Partner).ToList();
        TestBoards.FillFace(board, face, P1);

        var plan = PlanFor(board);
        var flights = plan.Frames.SelectMany(f => f.Flights).ToList();

        foreach (var partner in partners)
            Assert.IsTrue(flights.Any(f => f.To.Equals(partner)),
                $"Expected a flight landing on {partner}.");
        Assert.IsTrue(flights.All(f => f.Owner.Equals(P1)));
    }

    [Test]
    public void Build_FlightsOriginateFromThePartnerSlot()
    {
        var board = TestBoards.SingleCube("cube");
        var face = new SideId(Cube, 0);
        TestBoards.FillFace(board, face, P1);

        var plan = PlanFor(board);

        foreach (var flight in plan.Frames.SelectMany(f => f.Flights))
            Assert.AreEqual(flight.From, board.GetSlot(flight.To).Partner,
                "A claimed tile always comes from across its own edge.");
    }

    [Test]
    public void Build_ReleasedTilesProduceNoFlight()
    {
        var board = TestBoards.SingleCube("cube");
        var face = new SideId(Cube, 0);
        TestBoards.FillFace(board, face, P1);

        var plan = PlanFor(board);
        var flights = plan.Frames.SelectMany(f => f.Flights).ToList();

        // Slots going back to white are the source spending its tiles, not an arrival.
        foreach (var slot in board.GetSide(face).Slots)
            Assert.IsFalse(flights.Any(f => f.To.Equals(slot.Id)));
    }

    [Test]
    public void Build_ReturnsAnEmptyPlanWhenNothingHappened()
    {
        var board = TestBoards.SingleCube("cube");

        var plan = PlanFor(board);

        Assert.IsTrue(plan.IsEmpty);
        Assert.AreEqual(0, plan.InitialState.Count);
    }

    [Test]
    public void Build_DoesNotFlyATileInForASlotAbsorbedByConquest()
    {
        // Regression: conquest takes the defender's whole face, so those slots change
        // hands without crossing an edge. Inferring their origin from their own partner
        // spawned spheres on unrelated parts of the board and flew them across.
        var board = TestBoards.SingleCube("cube");
        var face = new SideId(Cube, 0);
        var arrival = board.GetSide(face).Slots.First().Partner;
        var invaded = board.GetSide(arrival.Side);

        board.SetOwner(arrival, P2);
        var absorbed = invaded.Slots.First(s => !s.Id.Equals(arrival));
        board.SetOwner(absorbed.Id, P2);

        TestBoards.FillFace(board, face, P1);
        var plan = PlanFor(board);
        var flights = plan.Frames.SelectMany(f => f.Flights).ToList();

        Assert.IsFalse(flights.Any(f => f.To.Equals(arrival)),
            "A slot taken by conquest must not have a tile flown into it.");
        Assert.IsFalse(flights.Any(f => f.To.Equals(absorbed.Id)),
            "Nor the defender's other slots on that face.");
        Assert.IsTrue(flights.Any(f => f.To.Side.Equals(invaded.Id)),
            "The arriving tile itself still flies -- onto a free slot.");
    }

    [Test]
    public void EveryFlightStartsOnTheFaceThatExpanded()
    {
        var board = TestBoards.TwoCubes();
        var face = new SideId(new CellId("cube_a"), 0);
        var sourceSlots = board.GetSide(face).Slots.Select(s => s.Id).ToHashSet();
        TestBoards.FillFace(board, face, P1);

        var plan = PlanFor(board);
        var flights = plan.Frames.SelectMany(f => f.Flights).ToList();

        Assert.IsNotEmpty(flights);
        foreach (var flight in flights.Where(f => sourceSlots.Contains(f.From)))
            Assert.AreNotEqual(flight.From, flight.To, "A tile cannot fly to itself.");

        // The first step's flights must all leave the face that expanded.
        foreach (var flight in plan.Frames[0].Flights)
            Assert.IsTrue(sourceSlots.Contains(flight.From),
                $"Flight {flight} does not start on the expanding face.");
    }

    [Test]
    public void Build_CoversConquestOfAnEnemyTile()
    {
        var board = TestBoards.SingleCube("cube");
        var face = new SideId(Cube, 0);
        var arrival = board.GetSide(face).Slots.First().Partner;
        board.SetOwner(arrival, P2);

        TestBoards.FillFace(board, face, P1);
        var plan = PlanFor(board);

        Assert.AreEqual(P2, plan.InitialState[arrival],
            "Playback starts from the defender's tile.");
        Assert.IsTrue(plan.Frames.SelectMany(f => f.Flights).Any(),
            "The expansion still produces flights.");
    }

}

