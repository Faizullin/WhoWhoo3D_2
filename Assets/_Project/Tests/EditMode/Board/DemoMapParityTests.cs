using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Maps;
using WhoWhoo3D.Gameplay.Board;
using Coord = WhoWhoo3D.Gameplay.Board.BoardBuilder.Coord;

namespace WhoWhoo3D.Tests.EditMode.Board;

/// <summary>
/// The shipped demo map must be exactly what <see cref="BoardBuilder"/> produces.
///
/// The map was originally written by a throwaway script, so the wiring rule existed in two
/// places at once. This pins them together: if the builder's topology ever drifts from the
/// file, this fails rather than the game quietly loading a board nothing can regenerate.
/// </summary>
public class DemoMapParityTests
{
    private const string MapName = "demo4x4";

    private static BoardModel LoadShippedMap()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Maps", MapName + ".json");
        if (!File.Exists(path)) Assert.Ignore($"{MapName}.json not present at {path}.");
        return MapSerializer.Deserialize(File.ReadAllText(path));
    }

    private static BoardModel BuildEquivalent()
    {
        var builder = new BoardBuilder();
        for (int x = 0; x < 4; x++)
            for (int z = 0; z < 4; z++)
                builder.AddCube(new Coord(x, 0, z));
        return builder.Build(BoardBuilder.TopFaceOnly);
    }

    [Test]
    public void BuilderReproducesTheShippedDemoTopology()
    {
        var shipped = LoadShippedMap();
        var built = BuildEquivalent();

        Assert.AreEqual(shipped.Cells.Count, built.Cells.Count, "cell count");
        Assert.AreEqual(
            shipped.Cells.Values.Sum(c => c.Sides.Sum(s => s.SlotCount)),
            built.Cells.Values.Sum(c => c.Sides.Sum(s => s.SlotCount)),
            "slot count");
        Assert.IsTrue(built.Validate().IsValid, built.Validate().ToString());
    }

    [Test]
    public void BuilderReproducesEveryPairing()
    {
        var shipped = LoadShippedMap();
        var built = BuildEquivalent();

        // Cell ids differ between the two (the file predates BoardBuilder's naming), so
        // compare by grid position, which is what actually defines the topology.
        string Key(BoardModel board, SlotId id)
        {
            var cell = board.GetCell(id.Side.Cell);
            return $"{(int)cell.Position.X},{(int)cell.Position.Y},{(int)cell.Position.Z}" +
                   $"#{id.Side.LocalIndex}@{id.Edge}";
        }

        var shippedPairs = shipped.Cells.Values
            .SelectMany(c => c.Sides).SelectMany(s => s.Slots)
            .Select(sl => $"{Key(shipped, sl.Id)} -> {Key(shipped, sl.Partner)}")
            .OrderBy(x => x).ToList();

        var builtPairs = built.Cells.Values
            .SelectMany(c => c.Sides).SelectMany(s => s.Slots)
            .Select(sl => $"{Key(built, sl.Id)} -> {Key(built, sl.Partner)}")
            .OrderBy(x => x).ToList();

        CollectionAssert.AreEqual(shippedPairs, builtPairs,
            "BoardBuilder must wire the slab exactly as the shipped map does.");
    }
}
