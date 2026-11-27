using System.IO;
using UnityEditor;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Maps;
using WhoWhoo3D.Gameplay.Board;
using Coord = WhoWhoo3D.Gameplay.Board.BoardBuilder.Coord;

public static class TerraceMapBuilder
{
    [MenuItem("Tools/WhoWhoo3D/Rebuild Terrace Map")]
    public static void Build()
    {
        string[] heights = { "111111", "112211", "123321", "123321", "112211", "111111" };
        var builder = new BoardBuilder();
        for (int z = 0; z < heights.Length; z++)
            for (int x = 0; x < heights[z].Length; x++)
                for (int y = 0; y < heights[z][x] - '0'; y++)
                    builder.AddCube(new Coord(x, y, z));

        // The base ends at the perimeter. Only the steps above it carry vertical routes.
        var board = builder.BuildSurface((coord, face) => face == 0 || (coord.Y > 0 && face > 1));
        var starts = new[] { new Coord(0, 0, 0), new Coord(5, 0, 5), new Coord(5, 0, 0), new Coord(0, 0, 5) };
        for (int i = 0; i < starts.Length; i++)
        {
            var start = starts[i];
            int edge = CubeGeometry.EdgeIndexOf(0, start.X == 0 ? 2 : 3);
            board.SetOwner(new SlotId(new SideId(BoardBuilder.IdFor(start), 0), edge), new PlayerId(i + 1));
        }
        if (!board.Validate().IsValid) throw new System.InvalidOperationException("Invalid terrace topology.");
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "Maps/terrace6x6.json"),
            MapSerializer.Serialize(board, new MapMetadata("Terrace 6×6", "2026-11-26T00:00:00Z")));
        AssetDatabase.Refresh();
    }
}
