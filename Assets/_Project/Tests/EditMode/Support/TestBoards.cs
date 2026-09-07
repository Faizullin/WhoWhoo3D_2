using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Board;

namespace WhoWhoo3D.Tests.EditMode.Support
{
    /// <summary>
    /// Board builders for tests. Mirrors the surface walk the map generator uses: every
    /// edge between two exposed faces carries exactly one reciprocal slot pair.
    /// </summary>
    public static class TestBoards
    {
        public static readonly PlayerId P1 = new(1);
        public static readonly PlayerId P2 = new(2);

        /// <summary>One cube, fully networked: 6 faces × 4 edges = 12 pairs, 24 slots.</summary>
        public static BoardModel SingleCube(string id = "cube")
        {
            var board = new BoardModel();
            var cell = new CellId(id);
            board.AddCell(cell, Vec3.Zero, 6);
            WireExposedFaces(board, cell);
            return board;
        }

        /// <summary>Two cubes side by side along +X, joined and fully networked.</summary>
        public static BoardModel TwoCubes(string a = "cube_a", string b = "cube_b")
        {
            var board = new BoardModel();
            var ca = new CellId(a);
            var cb = new CellId(b);

            board.AddCell(ca, Vec3.Zero, 6);
            board.AddCell(cb, new Vec3(1, 0, 0), 6);

            // Cube A's +X face meets cube B's -X face; both become interior.
            board.JoinSides(new SideId(ca, 2), new SideId(cb, 3));

            WireExposedFaces(board, ca, cb);
            return board;
        }

        private static void WireExposedFaces(BoardModel board, params CellId[] cells)
        {
            foreach (var cellId in cells)
            {
                var cell = board.GetCell(cellId);

                foreach (var side in cell.ExposedSides)
                {
                    var neighbours = CubeGeometry.EdgeNeighbours(side.LocalIndex);

                    for (int edge = 0; edge < neighbours.Length; edge++)
                    {
                        var a = new SlotId(side.Id, edge);
                        if (board.GetSlot(a) != null) continue;

                        int g = neighbours[edge];
                        var neighbourSide = cell.GetSide(g);

                        SlotId b;
                        if (!neighbourSide.IsInterior)
                        {
                            // Around this cube's own corner.
                            b = new SlotId(neighbourSide.Id, CubeGeometry.EdgeIndexOf(g, side.LocalIndex));
                        }
                        else
                        {
                            // Across the seam onto the neighbour cube's same-facing face.
                            var other = neighbourSide.Link.Value.Other.Cell;
                            int backEdge = CubeGeometry.EdgeIndexOf(
                                side.LocalIndex, CubeGeometry.OppositeFace(g));
                            b = new SlotId(new SideId(other, side.LocalIndex), backEdge);
                        }

                        if (board.GetSlot(b) != null) continue;
                        board.AddSlotPair(a, b);
                    }
                }
            }
        }

        /// <summary>Claims every slot on a face for a player — the expansion trigger.</summary>
        public static void FillFace(BoardModel board, SideId sideId, PlayerId owner)
        {
            foreach (var slot in board.GetSide(sideId).Slots)
                board.SetOwner(slot.Id, owner);
        }
    }
}
