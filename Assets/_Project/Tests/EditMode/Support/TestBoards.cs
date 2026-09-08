using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Board;

namespace WhoWhoo3D.Tests.EditMode.Support;

/// <summary>
/// Board builders for tests. Mirrors the surface walk the map generator uses: every
/// edge between two exposed faces carries exactly one reciprocal slot pair.
/// </summary>
public static class TestBoards
{
    public static readonly PlayerId P1 = new(1);
    public static readonly PlayerId P2 = new(2);

    /// <summary>One cube, fully networked: 6 faces x 4 edges = 12 pairs, 24 slots.</summary>
    public static BoardModel SingleCube()
    {
        // Built through BoardBuilder so the tests exercise the same wiring rule the map
        // editor and the shipped maps use, rather than a second copy of it.
        var builder = new BoardBuilder();
        builder.AddCube(new BoardBuilder.Coord(0, 0, 0));
        return builder.Build();
    }

    /// <summary>Two cubes side by side along +X, joined and fully networked.</summary>
    public static BoardModel TwoCubes()
    {
        var builder = new BoardBuilder();
        builder.AddCube(new BoardBuilder.Coord(0, 0, 0));
        builder.AddCube(new BoardBuilder.Coord(1, 0, 0));
        return builder.Build();
    }

    /// <summary>Cell id of a cube at a grid position, for tests that need to name one.</summary>
    public static CellId At(int x, int y, int z) =>
        BoardBuilder.IdFor(new BoardBuilder.Coord(x, y, z));

    /// <summary>Claims every slot on a face for a player — the expansion trigger.</summary>
    public static void FillFace(BoardModel board, SideId sideId, PlayerId owner)
    {
        foreach (var slot in board.GetSide(sideId).Slots)
            board.SetOwner(slot.Id, owner);
    }
}

