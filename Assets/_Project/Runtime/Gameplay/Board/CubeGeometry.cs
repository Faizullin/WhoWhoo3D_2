using UnityEngine;

namespace WhoWhoo3D.Gameplay.Board;

/// <summary>
/// Where a cube's faces and edge slots sit in local space. This is the only place
/// that knows a cell is drawn as a cube — the Domain stays shape-agnostic and speaks
/// only in side and edge indices.
///
/// Face indices: 0 top(+Y) 1 bottom(-Y) 2 right(+X) 3 left(-X) 4 front(+Z) 5 back(-Z).
/// Opposite faces pair as 0-1, 2-3, 4-5.
///
/// A face's four edges are its four perpendicular faces, taken in ascending face
/// index. So on the top face, edge 0 runs toward +X, edge 1 toward -X, edge 2 toward
/// +Z, edge 3 toward -Z. The map and the renderer must agree on exactly this.
/// </summary>
public static class CubeGeometry
{
    public const int FaceCount = 6;
    public const int EdgesPerFace = 4;

    private static readonly Vector3[] Normals =
    {
        Vector3.up,      // 0 top
        Vector3.down,    // 1 bottom
        Vector3.right,   // 2 right
        Vector3.left,    // 3 left
        Vector3.forward, // 4 front
        Vector3.back,    // 5 back
    };

    /// <summary>How far from the cube centre the face plane sits (unit cube).</summary>
    private const float FaceOffset = 0.5f;

    /// <summary>How far along the face toward its edge a tile sits.</summary>
    private const float EdgeOffset = 0.32f;

    /// <summary>Lifts the tile clear of the face so it does not z-fight with the mesh.</summary>
    private const float SurfaceLift = 0.03f;

    public static Vector3 FaceNormal(int face) => Normals[face];

    public static int OppositeFace(int face) => face % 2 == 0 ? face + 1 : face - 1;

    /// <summary>The four face indices bordering this face, in ascending order.</summary>
    public static int[] EdgeNeighbours(int face)
    {
        var result = new int[EdgesPerFace];
        int opposite = OppositeFace(face);
        int n = 0;

        for (int i = 0; i < FaceCount; i++)
            if (i != face && i != opposite)
                result[n++] = i;

        return result;
    }

    /// <summary>Which edge of <paramref name="face"/> borders <paramref name="neighbour"/>, or -1.</summary>
    public static int EdgeIndexOf(int face, int neighbour)
    {
        var neighbours = EdgeNeighbours(face);
        for (int i = 0; i < neighbours.Length; i++)
            if (neighbours[i] == neighbour)
                return i;
        return -1;
    }

    /// <summary>Local position of a tile: out on its face, shifted toward its edge.</summary>
    public static Vector3 SlotLocalPosition(int face, int edge)
    {
        var faceNormal = Normals[face];
        var edgeNormal = Normals[EdgeNeighbours(face)[edge]];
        return faceNormal * (FaceOffset + SurfaceLift) + edgeNormal * EdgeOffset;
    }

    /// <summary>Lays the tile flat in its face's plane.</summary>
    public static Quaternion SlotLocalRotation(int face, int edge)
    {
        var faceNormal = Normals[face];
        var edgeNormal = Normals[EdgeNeighbours(face)[edge]];
        return Quaternion.LookRotation(edgeNormal, faceNormal);
    }
}

