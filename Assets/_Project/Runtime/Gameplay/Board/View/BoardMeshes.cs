using UnityEngine;

namespace WhoWhoo3D.Gameplay.Board.View;

/// <summary>Small shared meshes for the board's chamfered silhouettes and printed seat marks.</summary>
internal static class BoardMeshes
{
    private static Mesh _cube, _frame;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Tint = Shader.PropertyToID("_Color");

    // A flat, open square shared by face diamonds, frost rims and tile landing frames.
    public static Renderer Frame(Transform parent, string name, Vector3 position, Quaternion rotation, float size, Color color)
    {
        if (!_frame)
        {
            var vertices = new Vector3[8];
            var indices = new int[24];
            Vector3[] corners = { new(-.5f, 0, -.5f), new(-.5f, 0, .5f), new(.5f, 0, .5f), new(.5f, 0, -.5f) };
            for (int i = 0; i < corners.Length; i++)
            {
                int a = i * 2, b = ((i + 1) % 4) * 2, t = i * 6;
                vertices[a] = corners[i];
                vertices[a + 1] = corners[i] * .86f;
                indices[t] = a; indices[t + 1] = b; indices[t + 2] = b + 1;
                indices[t + 3] = a; indices[t + 4] = b + 1; indices[t + 5] = a + 1;
            }
            _frame = new Mesh { name = "Board outline", vertices = vertices, triangles = indices };
            _frame.RecalculateNormals();
        }
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(position, rotation);
        go.transform.localScale = Vector3.one * size;
        go.GetComponent<MeshFilter>().sharedMesh = _frame;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = SlotView.SharedMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColor, color);
        block.SetColor(Tint, color);
        renderer.SetPropertyBlock(block);
        return renderer;
    }

    private static readonly Mesh[] Stamps = new Mesh[4];
    public static Mesh Cube => _cube ? _cube : _cube = MakeCube();

    private static Mesh MakeCube()
    {
        const float flat = .455f, radius = .045f;
        float[] cuts = { -.5f, -flat, flat, .5f };
        var vertices = new Vector3[96];
        var normals = new Vector3[96];
        var triangles = new int[6 * 9 * 6];
        int index = 0;
        for (int face = 0; face < 6; face++)
        {
            var n = CubeGeometry.FaceNormal(face);
            var u = Mathf.Abs(n.y) > .5f ? Vector3.right : Vector3.up;
            var v = Vector3.Cross(n, u);
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                int k = face * 16 + y * 4 + x;
                var p = n * .5f + u * cuts[x] + v * cuts[y];
                var core = new Vector3(Mathf.Clamp(p.x, -flat, flat), Mathf.Clamp(p.y, -flat, flat), Mathf.Clamp(p.z, -flat, flat));
                normals[k] = (p - core).normalized;
                vertices[k] = core + normals[k] * radius;
                if (x == 3 || y == 3) continue;
                triangles[index++] = k; triangles[index++] = k + 1; triangles[index++] = k + 5;
                triangles[index++] = k; triangles[index++] = k + 5; triangles[index++] = k + 4;
            }
        }
        return new Mesh { name = "Beveled board cube", vertices = vertices, normals = normals, triangles = triangles };
    }

    public static Mesh SeatStamp(int seat)
    {
        if (Stamps[seat - 1]) return Stamps[seat - 1];
        const int sides = 8;
        var vertices = new Vector3[seat * (sides + 1)];
        var triangles = new int[seat * sides * 3];
        for (int i = 0; i < seat; i++)
        {
            // Two and three use a diagonal. Four uses the four corners, as on a die.
            var center = Vector3.zero;
            if (seat == 4)
                center = new Vector3(i % 2 == 0 ? -.19f : .19f, 0, i < 2 ? -.19f : .19f);
            else if (seat > 1 && i < 2)
                center = new Vector3(1, 0, 1) * (i == 0 ? -.22f : .22f);
            int k = i * (sides + 1);
            vertices[k] = center;
            for (int j = 0; j < sides; j++)
            {
                float a = j * Mathf.PI * 2 / sides;
                vertices[k + j + 1] = center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * .097f;
                int t = (i * sides + j) * 3;
                triangles[t] = k; triangles[t + 1] = k + j + 1; triangles[t + 2] = k + (j + 1) % sides + 1;
            }
        }
        var mesh = new Mesh { name = $"Seat {seat} pips", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        return Stamps[seat - 1] = mesh;
    }
}
