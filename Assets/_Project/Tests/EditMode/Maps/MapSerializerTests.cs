using Newtonsoft.Json.Linq;
using NUnit.Framework;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Maps;

namespace WhoWhoo3D.Tests.EditMode.Maps;

public class MapSerializerTests
{
    private static readonly CellId A = new("cell_0001");
    private static readonly CellId B = new("cell_0002");
    private static readonly PlayerId P1 = new(1);
    private static readonly MapMetadata Meta = new("TestMap", "2026-05-08T00:00:00+00:00");

    private static BoardModel TwoLinkedCells()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, new Vec3(1, 0, 0), 6);
        board.LinkSides(new SideId(A, 0), new SideId(B, 3));
        return board;
    }

    [Test]
    public void RoundTrip_ProducesIdenticalJson()
    {
        var board = TwoLinkedCells();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 2));

        var json1 = MapSerializer.Serialize(board, Meta);
        var board2 = MapSerializer.Deserialize(json1);
        var json2 = MapSerializer.Serialize(board2, Meta);

        Assert.AreEqual(json1, json2);
    }

    [Test]
    public void Serialize_ContainsSchemaVersion1()
    {
        var json = MapSerializer.Serialize(TwoLinkedCells(), Meta);
        StringAssert.Contains("\"schemaVersion\": 1", json);
    }

    [Test]
    public void Serialize_SparseSides_EmptySidesOmitted()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, new Vec3(1, 0, 0), 6);
        board.LinkSides(new SideId(A, 0), new SideId(B, 0));

        var json = MapSerializer.Serialize(board, Meta);

        var doc = JObject.Parse(json);
        foreach (var cell in (JArray)doc["cells"])
        {
            var sides = ((JArray)cell["sides"]).Count;
            Assert.AreEqual(1, sides);
        }
    }

    [Test]
    public void Deserialize_RestoresBoardModel_Correctly()
    {
        var board = TwoLinkedCells();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 3));

        var restored = MapSerializer.Deserialize(MapSerializer.Serialize(board, Meta));

        Assert.AreEqual(new TileStack(P1, 3), restored.GetSide(new SideId(A, 0)).Stack);
        Assert.AreEqual(new SideLink(new SideId(B, 3)), restored.GetSide(new SideId(A, 0)).Link);
        Assert.AreEqual(new SideLink(new SideId(A, 0)), restored.GetSide(new SideId(B, 3)).Link);
        Assert.IsTrue(restored.Validate().IsValid);
    }

    [Test]
    public void Reject_MissingSchemaVersion()
    {
        var json = "{\"name\":\"x\",\"createdAt\":\"2026-01-01T00:00:00+00:00\",\"cells\":[]}";
        var ex = Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        StringAssert.Contains("schemaVersion", ex.Message);
    }

    [Test]
    public void Reject_WrongSchemaVersion()
    {
        var json = "{\"schemaVersion\":99,\"name\":\"x\",\"createdAt\":\"2026-01-01T00:00:00+00:00\",\"cells\":[]}";
        var ex = Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        StringAssert.Contains("99", ex.Message);
    }

    [Test]
    public void Reject_DuplicateCellId()
    {
        var json = @"{
  ""schemaVersion"": 1,
  ""name"": ""x"",
  ""createdAt"": ""2026-01-01T00:00:00+00:00"",
  ""cells"": [
    { ""id"": ""cell_0001"", ""position"": {""x"":0,""y"":0,""z"":0}, ""sideCount"": 6, ""sides"": [] },
    { ""id"": ""cell_0001"", ""position"": {""x"":1,""y"":0,""z"":0}, ""sideCount"": 6, ""sides"": [] }
  ]
}";
        var ex = Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        StringAssert.Contains("Duplicate", ex.Message);
    }

    [Test]
    public void Reject_DanglingLink()
    {
        var json = @"{
  ""schemaVersion"": 1,
  ""name"": ""x"",
  ""createdAt"": ""2026-01-01T00:00:00+00:00"",
  ""cells"": [
    {
      ""id"": ""cell_0001"",
      ""position"": {""x"":0,""y"":0,""z"":0},
      ""sideCount"": 6,
      ""sides"": [
        { ""index"": 0, ""link"": { ""cellId"": ""cell_XXXX"", ""sideIndex"": 0 } }
      ]
    }
  ]
}";
        Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
    }

    [Test]
    public void Reject_NonReciprocalLink()
    {
        var json = @"{
  ""schemaVersion"": 1,
  ""name"": ""x"",
  ""createdAt"": ""2026-01-01T00:00:00+00:00"",
  ""cells"": [
    {
      ""id"": ""cell_0001"",
      ""position"": {""x"":0,""y"":0,""z"":0},
      ""sideCount"": 6,
      ""sides"": [
        { ""index"": 0, ""link"": { ""cellId"": ""cell_0002"", ""sideIndex"": 0 } }
      ]
    },
    {
      ""id"": ""cell_0002"",
      ""position"": {""x"":1,""y"":0,""z"":0},
      ""sideCount"": 6,
      ""sides"": []
    }
  ]
}";
        var ex = Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        StringAssert.Contains("reciprocal", ex.Message);
    }

    [Test]
    public void Reject_NonPositiveStackCount()
    {
        var json = @"{
  ""schemaVersion"": 1,
  ""name"": ""x"",
  ""createdAt"": ""2026-01-01T00:00:00+00:00"",
  ""cells"": [
    {
      ""id"": ""cell_0001"",
      ""position"": {""x"":0,""y"":0,""z"":0},
      ""sideCount"": 6,
      ""sides"": [
        { ""index"": 0, ""stack"": { ""owner"": 1, ""count"": 0 } }
      ]
    }
  ]
}";
        var ex = Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        StringAssert.Contains("count", ex.Message);
    }
}
