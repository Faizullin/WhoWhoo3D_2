using WhoWhoo3D.Truth.Maps;
using WhoWhoo3D.Truth.Topology;
using Xunit;

namespace WhoWhoo3D.Truth.Tests.Maps;

public class MapSerializerTests
{
    private static readonly CellId A = new("cell_0001");
    private static readonly CellId B = new("cell_0002");
    private static readonly CellId C = new("cell_0003");
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

    [Fact]
    public void RoundTrip_ProducesIdenticalJson()
    {
        var board = TwoLinkedCells();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 2));

        var json1 = MapSerializer.Serialize(board, Meta);
        var board2 = MapSerializer.Deserialize(json1);
        var json2 = MapSerializer.Serialize(board2, Meta);

        Assert.Equal(json1, json2);
    }

    [Fact]
    public void Serialize_ContainsSchemaVersion1()
    {
        var json = MapSerializer.Serialize(TwoLinkedCells(), Meta);
        Assert.Contains("\"schemaVersion\": 1", json);
    }

    [Fact]
    public void Serialize_SparseSides_EmptySidesOmitted()
    {
        // Cell with only side 0 populated — other 5 sides should not appear in JSON.
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, new Vec3(1, 0, 0), 6);
        board.LinkSides(new SideId(A, 0), new SideId(B, 0));

        var json = MapSerializer.Serialize(board, Meta);

        // Each cell should have exactly 1 side entry.
        var doc = System.Text.Json.JsonDocument.Parse(json);
        foreach (var cell in doc.RootElement.GetProperty("cells").EnumerateArray())
        {
            var sides = cell.GetProperty("sides").EnumerateArray().Count();
            Assert.Equal(1, sides);
        }
    }

    [Fact]
    public void Deserialize_RestoresBoardModel_Correctly()
    {
        var board = TwoLinkedCells();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 3));

        var restored = MapSerializer.Deserialize(MapSerializer.Serialize(board, Meta));

        Assert.Equal(new TileStack(P1, 3), restored.GetSide(new SideId(A, 0)).Stack);
        Assert.Equal(new SideLink(new SideId(B, 3)), restored.GetSide(new SideId(A, 0)).Link);
        Assert.Equal(new SideLink(new SideId(A, 0)), restored.GetSide(new SideId(B, 3)).Link);
        Assert.True(restored.Validate().IsValid);
    }

    [Fact]
    public void Reject_MissingSchemaVersion()
    {
        var json = """{"name":"x","createdAt":"2026-01-01T00:00:00+00:00","cells":[]}""";
        var ex = Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        Assert.Contains("schemaVersion", ex.Message);
    }

    [Fact]
    public void Reject_WrongSchemaVersion()
    {
        var json = """{"schemaVersion":99,"name":"x","createdAt":"2026-01-01T00:00:00+00:00","cells":[]}""";
        var ex = Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void Reject_DuplicateCellId()
    {
        var json = """
        {
          "schemaVersion": 1,
          "name": "x",
          "createdAt": "2026-01-01T00:00:00+00:00",
          "cells": [
            { "id": "cell_0001", "position": {"x":0,"y":0,"z":0}, "sideCount": 6, "sides": [] },
            { "id": "cell_0001", "position": {"x":1,"y":0,"z":0}, "sideCount": 6, "sides": [] }
          ]
        }
        """;
        var ex = Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        Assert.Contains("Duplicate", ex.Message);
    }

    [Fact]
    public void Reject_DanglingLink()
    {
        var json = """
        {
          "schemaVersion": 1,
          "name": "x",
          "createdAt": "2026-01-01T00:00:00+00:00",
          "cells": [
            {
              "id": "cell_0001",
              "position": {"x":0,"y":0,"z":0},
              "sideCount": 6,
              "sides": [
                { "index": 0, "link": { "cellId": "cell_XXXX", "sideIndex": 0 } }
              ]
            }
          ]
        }
        """;
        Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
    }

    [Fact]
    public void Reject_NonReciprocalLink()
    {
        // A links to B, but B does not link back to A.
        var json = """
        {
          "schemaVersion": 1,
          "name": "x",
          "createdAt": "2026-01-01T00:00:00+00:00",
          "cells": [
            {
              "id": "cell_0001",
              "position": {"x":0,"y":0,"z":0},
              "sideCount": 6,
              "sides": [
                { "index": 0, "link": { "cellId": "cell_0002", "sideIndex": 0 } }
              ]
            },
            {
              "id": "cell_0002",
              "position": {"x":1,"y":0,"z":0},
              "sideCount": 6,
              "sides": []
            }
          ]
        }
        """;
        var ex = Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        Assert.Contains("reciprocal", ex.Message);
    }

    [Fact]
    public void Reject_NonPositiveStackCount()
    {
        var json = """
        {
          "schemaVersion": 1,
          "name": "x",
          "createdAt": "2026-01-01T00:00:00+00:00",
          "cells": [
            {
              "id": "cell_0001",
              "position": {"x":0,"y":0,"z":0},
              "sideCount": 6,
              "sides": [
                { "index": 0, "stack": { "owner": 1, "count": 0 } }
              ]
            }
          ]
        }
        """;
        var ex = Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        Assert.Contains("count", ex.Message);
    }
}
