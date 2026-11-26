using System;
using System.Collections.Generic;
using System.Linq;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Gameplay.Board;

/// <summary>
/// Builds a <see cref="BoardModel"/> from a set of cube positions.
///
/// This is the canonical implementation of the surface-wiring rule described in
/// MAP_SCHEMA.md — the map editor, the tests and any generator all go through here, so the
/// rule exists once.
///
/// <para><b>It rebuilds rather than edits.</b> Placing a cube buries its neighbours' facing
/// sides, which destroys every slot on them and changes what the surrounding slots pair
/// with. Incremental mutation of that is a bug farm, so the builder keeps only cube
/// positions and slot ownership, and recomputes the topology on every <see cref="Build"/>.
/// Ownership of a slot that no longer exists is simply dropped.</para>
///
/// No UnityEngine reference, so the whole thing is unit-testable.
/// </summary>
public sealed class BoardBuilder
{
    /// <summary>Integer grid position of a cube.</summary>
    public readonly record struct Coord(int X, int Y, int Z)
    {
        public Coord Step((int X, int Y, int Z) d) => new(X + d.X, Y + d.Y, Z + d.Z);
        public override string ToString() => $"{X},{Y},{Z}";
    }

    /// <summary>Decides which faces may carry tiles. A pair needs BOTH ends to pass.</summary>
    public static readonly Predicate<int> AllFaces = _ => true;

    /// <summary>Only the top face — a flat slab you play across, like the demo map.</summary>
    public static readonly Predicate<int> TopFaceOnly = face => face == 0;

    private const int FaceCount = CubeGeometry.FaceCount;

    private readonly HashSet<Coord> _cubes = new();
    private readonly Dictionary<SlotId, PlayerId> _owners = new();

    public IReadOnlyCollection<Coord> Cubes => _cubes;
    public int CubeCount => _cubes.Count;

    public bool Contains(Coord coord) => _cubes.Contains(coord);

    /// <summary>Adds a cube. Returns false if one is already there.</summary>
    public bool AddCube(Coord coord) => _cubes.Add(coord);

    /// <summary>Removes a cube. Returns false if there was none.</summary>
    public bool RemoveCube(Coord coord) => _cubes.Remove(coord);

    public void Clear()
    {
        _cubes.Clear();
        _owners.Clear();
    }

    /// <summary>
    /// Records who owns a slot. Kept separately from the topology, so it survives unrelated
    /// edits and is discarded only when that slot stops existing.
    /// </summary>
    public void SetOwner(SlotId slot, PlayerId owner)
    {
        if (owner.IsNone) _owners.Remove(slot);
        else _owners[slot] = owner;
    }

    public PlayerId OwnerOf(SlotId slot) =>
        _owners.TryGetValue(slot, out var owner) ? owner : PlayerId.None;

    public void ClearOwners() => _owners.Clear();

    /// <summary>Stable id for a cube, derived from its position so it survives a rebuild.</summary>
    public static CellId IdFor(Coord coord) => new($"cell_{coord.X}_{coord.Y}_{coord.Z}");

    /// <summary>
    /// Produces the board. Cubes are added in a deterministic order so two builds of the
    /// same layout are byte-identical when serialised.
    /// </summary>
    public BoardModel Build(Predicate<int> faceFilter = null)
    {
        faceFilter ??= AllFaces;

        var board = new BoardModel();
        var ordered = _cubes.OrderBy(c => c.X).ThenBy(c => c.Y).ThenBy(c => c.Z).ToList();

        foreach (var coord in ordered)
            board.AddCell(IdFor(coord), new Vec3(coord.X, coord.Y, coord.Z), FaceCount);

        JoinTouchingCubes(board, ordered);
        WireSurface(board, ordered, faceFilter);
        ApplyOwners(board);

        return board;
    }

    /// <summary>Builds and validates. Errors are reported rather than thrown, so an editor can show them.</summary>
    public bool TryBuild(out BoardModel board, out IReadOnlyList<string> errors, Predicate<int> faceFilter = null)
    {
        board = Build(faceFilter);
        var result = board.Validate();
        errors = result.Errors;
        return result.IsValid;
    }

    /// <summary>Two cubes side by side bury the faces between them.</summary>
    private void JoinTouchingCubes(BoardModel board, List<Coord> ordered)
    {
        foreach (var coord in ordered)
        {
            for (int face = 0; face < FaceCount; face++)
            {
                var neighbour = coord.Step(CubeGeometry.FaceStep(face));
                if (!_cubes.Contains(neighbour)) continue;

                // Each pair is reached twice; apply it from the lower side only.
                var a = new SideId(IdFor(coord), face);
                var b = new SideId(IdFor(neighbour), CubeGeometry.OppositeFace(face));
                if (board.GetSide(a).IsInterior) continue;

                board.JoinSides(a, b);
            }
        }
    }

    /// <summary>
    /// Walks the exposed surface and pairs slots across every shared edge.
    ///
    /// For an exposed face F and one of its edges leading to face G:
    /// <list type="bullet">
    /// <item>G exposed on the same cube — the pair folds around that cube's own corner.</item>
    /// <item>G buried against cube D — cross the flat seam, or turn up the riser if D is covered.</item>
    /// </list>
    /// A pair is only created when both ends pass <paramref name="faceFilter"/>, which is
    /// what keeps a filtered board free of lone slots.
    /// </summary>
    private void WireSurface(BoardModel board, List<Coord> ordered, Predicate<int> faceFilter)
    {
        foreach (var coord in ordered)
        {
            var cell = board.GetCell(IdFor(coord));

            for (int face = 0; face < FaceCount; face++)
            {
                if (cell.GetSide(face).IsInterior || !faceFilter(face)) continue;

                var edges = CubeGeometry.EdgeNeighbours(face);
                for (int edge = 0; edge < edges.Length; edge++)
                {
                    var here = new SlotId(new SideId(cell.Id, face), edge);
                    if (board.GetSlot(here) != null) continue;

                    if (!TryResolvePartner(board, coord, face, edges[edge], faceFilter, out var there))
                        continue;
                    if (board.GetSlot(there) != null) continue;

                    board.AddSlotPair(here, there);
                }
            }
        }
    }

    private bool TryResolvePartner(
        BoardModel board, Coord coord, int face, int neighbourFace,
        Predicate<int> faceFilter, out SlotId partner)
    {
        partner = default;
        var cell = board.GetCell(IdFor(coord));

        if (!cell.GetSide(neighbourFace).IsInterior)
        {
            // Around this cube's own corner onto the adjoining face.
            if (!faceFilter(neighbourFace)) return false;

            partner = new SlotId(
                new SideId(cell.Id, neighbourFace),
                CubeGeometry.EdgeIndexOf(neighbourFace, face));
            return true;
        }

        // Across the seam onto the neighbour's same-facing face.
        var other = coord.Step(CubeGeometry.FaceStep(neighbourFace));
        if (!_cubes.Contains(other)) return false;

        var otherCell = board.GetCell(IdFor(other));
        if (otherCell.GetSide(face).IsInterior)
        {
            // Three cubes meet at a concave edge. Pair the tread with the exposed riser.
            var upper = other.Step(CubeGeometry.FaceStep(face));
            int riser = CubeGeometry.OppositeFace(neighbourFace);
            if (!faceFilter(riser)) return false;
            partner = new SlotId(new SideId(IdFor(upper), riser),
                CubeGeometry.EdgeIndexOf(riser, CubeGeometry.OppositeFace(face)));
            return true;
        }
        if (!faceFilter(face)) return false;

        partner = new SlotId(
            new SideId(otherCell.Id, face),
            CubeGeometry.EdgeIndexOf(face, CubeGeometry.OppositeFace(neighbourFace)));
        return true;
    }

    /// <summary>Re-applies ownership, skipping slots the current topology no longer has.</summary>
    private void ApplyOwners(BoardModel board)
    {
        foreach (var pair in _owners)
            if (board.GetSlot(pair.Key) != null)
                board.SetOwner(pair.Key, pair.Value);
    }

    /// <summary>
    /// Seeds a builder from an existing board, so a saved map can be reopened.
    ///
    /// Cell ids are re-derived from position (<see cref="IdFor"/>), so ownership has to be
    /// remapped onto the new ids — a map authored elsewhere may name its cells anything.
    /// </summary>
    public static BoardBuilder FromBoard(BoardModel board)
    {
        var builder = new BoardBuilder();
        var coordOf = new Dictionary<CellId, Coord>();

        foreach (var cell in board.Cells.Values)
        {
            var coord = new Coord(
                (int)Math.Round(cell.Position.X),
                (int)Math.Round(cell.Position.Y),
                (int)Math.Round(cell.Position.Z));

            if (!builder.AddCube(coord))
                throw new InvalidOperationException(
                    $"Two cells share grid position {coord}; the board is not on an integer grid.");

            coordOf[cell.Id] = coord;
        }

        foreach (var cell in board.Cells.Values)
        {
            foreach (var side in cell.Sides)
            {
                foreach (var slot in side.Slots)
                {
                    if (slot.IsNeutral) continue;

                    var rebuilt = new SlotId(
                        new SideId(IdFor(coordOf[slot.Id.Side.Cell]), slot.Id.Side.LocalIndex),
                        slot.Id.Edge);
                    builder.SetOwner(rebuilt, slot.Owner);
                }
            }
        }

        return builder;
    }
}
