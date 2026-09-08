using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Maps;
using WhoWhoo3D.Gameplay.Board;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Maps;
using Coord = WhoWhoo3D.Gameplay.Board.BoardBuilder.Coord;

namespace WhoWhoo3D.MapEditor;

/// <summary>
/// Authoring surface for maps. Owns a <see cref="BoardBuilder"/>, renders it through the
/// same <see cref="BoardView"/> the game uses, and writes through <see cref="MapSerializer"/>.
///
/// Deliberately the same shape as GameController: a MonoBehaviour that holds pure logic and
/// drives a view, with all the rules living in classes that need no scene. Nothing here
/// knows how slots pair up — that is BoardBuilder's job, and the game, the tests and this
/// editor all get it from the same place.
/// </summary>
public sealed class MapEditorController : MonoBehaviour
{
    public enum Tool
    {
        /// <summary>Click the grid to add a cube.</summary>
        Place,

        /// <summary>Click a cube to remove it.</summary>
        Erase,

        /// <summary>Click a tile to assign it to the selected player.</summary>
        Paint,
    }

    [Header("Scene")]
    [SerializeField] private BoardView boardView;
    [SerializeField] private Camera editorCamera;

    [Header("Map")]
    [SerializeField] private string mapFileName = "custom";
    [Tooltip("Restrict tiles to the top face — a flat slab, like the demo board.")]
    [SerializeField] private bool topFaceOnly = true;
    [Range(2, 12)]
    [SerializeField] private int startingSlab = 4;

    [Header("Editing")]
    [SerializeField] private Tool tool = Tool.Place;
    [Range(1, 4)]
    [SerializeField] private int paintPlayer = 1;

    /// <summary>Height of the plane clicks are projected onto. Cubes are placed on it.</summary>
    [SerializeField] private int editLayer;

    private readonly BoardBuilder _builder = new();

    public Tool ActiveTool { get => tool; set => tool = value; }
    public int PaintPlayer { get => paintPlayer; set => paintPlayer = Mathf.Clamp(value, 1, 4); }
    public bool TopFaceOnly { get => topFaceOnly; set { topFaceOnly = value; Rebuild(); } }
    public string MapFileName { get => mapFileName; set => mapFileName = value; }
    public int CubeCount => _builder.CubeCount;

    public BoardModel Board { get; private set; }
    public IReadOnlyList<string> Errors { get; private set; } = Array.Empty<string>();
    public bool IsValid => Errors.Count == 0;
    public string Status { get; private set; } = "";

    private Predicate<int> Filter => topFaceOnly ? BoardBuilder.TopFaceOnly : BoardBuilder.AllFaces;

    private void Awake()
    {
        if (boardView == null) boardView = FindAnyObjectByType<BoardView>();
        if (editorCamera == null) editorCamera = Camera.main;
    }

    private void Start()
    {
        NewSlab(startingSlab);
    }

    private void Update()
    {
        if (WasClicked(out var screenPosition))
            Apply(screenPosition);
    }

    // ── Map operations ───────────────────────────────────────────

    /// <summary>Replaces the map with an n x n slab — a usable starting point.</summary>
    public void NewSlab(int n)
    {
        _builder.Clear();
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
                _builder.AddCube(new Coord(x, editLayer, z));

        Rebuild();
        Status = $"New {n}x{n} slab.";
    }

    public void Load()
    {
        try
        {
            var loaded = MapLoader.Load(mapFileName);
            CopyFrom(loaded);
            Status = $"Loaded '{mapFileName}'.";
        }
        catch (Exception ex) when (ex is FileNotFoundException or MapDeserializationException)
        {
            Status = $"Load failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Writes the map — but only if it validates. A map that fails validation cannot be
    /// loaded back, so refusing to write it is kinder than saving a file that is already
    /// broken.
    /// </summary>
    public bool Save()
    {
        Rebuild();

        if (!IsValid)
        {
            Status = $"Not saved — {Errors.Count} problem(s). Fix them first.";
            return false;
        }

        string path = MapLoader.PathFor(mapFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, MapSerializer.Serialize(Board, MapMetadata.New(mapFileName)));

        Status = $"Saved to {path}";
        Debug.Log($"Map written: {path}");
        return true;
    }

    /// <summary>Seeds the editor from an existing board.</summary>
    public void CopyFrom(BoardModel board)
    {
        var seeded = BoardBuilder.FromBoard(board);

        _builder.Clear();
        foreach (var coord in seeded.Cubes) _builder.AddCube(coord);
        foreach (var cell in board.Cells.Values)
            foreach (var side in cell.Sides)
                foreach (var slot in side.Slots)
                    if (!slot.IsNeutral)
                        _builder.SetOwner(slot.Id, slot.Owner);

        Rebuild();
    }

    /// <summary>Recomputes the board and redraws. Every edit goes through here.</summary>
    public void Rebuild()
    {
        bool ok = _builder.TryBuild(out var board, out var errors, Filter);
        Board = board;
        Errors = errors;

        if (boardView != null)
            boardView.Render(Board);

        if (!ok)
            Status = $"{errors.Count} validation problem(s).";
    }

    // ── Input ────────────────────────────────────────────────────

    private static bool WasClicked(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return false;

        screenPosition = pointer.position.ReadValue();
        return true;
    }

    private void Apply(Vector2 screenPosition)
    {
        if (editorCamera == null) return;

        var ray = editorCamera.ScreenPointToRay(screenPosition);

        if (tool == Tool.Paint)
        {
            PaintSlotUnder(ray);
            return;
        }

        if (!TryGridCell(ray, out var coord)) return;

        bool changed = tool == Tool.Place ? _builder.AddCube(coord) : _builder.RemoveCube(coord);
        if (!changed) return;

        Rebuild();
        Status = $"{(tool == Tool.Place ? "Placed" : "Erased")} cube at {coord}.";
    }

    private void PaintSlotUnder(Ray ray)
    {
        if (!Physics.Raycast(ray, out var hit)) return;

        var slotView = hit.collider.GetComponent<SlotView>()
                    ?? hit.collider.GetComponentInParent<SlotView>();
        if (slotView == null) return;

        // Clicking a tile the selected player already owns clears it, so one tool both
        // assigns and unassigns.
        var current = _builder.OwnerOf(slotView.SlotId);
        var target = new PlayerId(paintPlayer);
        _builder.SetOwner(slotView.SlotId, current.Equals(target) ? PlayerId.None : target);

        Rebuild();
        Status = $"Painted {slotView.SlotId}.";
    }

    /// <summary>
    /// Projects a click onto the editing plane. A mathematical plane rather than a collider,
    /// so an empty grid square is clickable — there is nothing there to hit.
    /// </summary>
    private bool TryGridCell(Ray ray, out Coord coord)
    {
        coord = default;

        // Cube centres sit on integer coordinates, so the top surface is at y + 0.5.
        var plane = new Plane(Vector3.up, new Vector3(0f, editLayer + 0.5f, 0f));
        if (!plane.Raycast(ray, out float distance)) return false;

        var point = ray.GetPoint(distance);
        coord = new Coord(Mathf.RoundToInt(point.x), editLayer, Mathf.RoundToInt(point.z));
        return true;
    }
}
