# Map format v2

Maps live in `Assets/StreamingAssets/Maps/`. Use `DemoMapEditor.unity` to author one, or use the checked-in two-cube map as a complete JSON example. [Sample map](../Assets/StreamingAssets/Maps/sample.json)

| Field | Meaning |
| --- | --- |
| `schemaVersion` | Integer 2. Earlier stack-based maps require regeneration. |
| `name`, `createdAt` | Display name and serialized creation timestamp. |
| `cells[]` | Each cell has a unique string `id`, a position with `x`, `y`, `z`, a `sideCount`, and lists of joins and slots. Cube maps use six sides. |
| `joins[]` | A join records the local `side` and the other cell’s `cellId` and `sideIndex`. Joined faces are interior and cannot hold slots. |
| `slots[]` | A slot records the local `side`, `edge`, `owner` and `partner`. Owner zero or omitted means unowned. The opening setup clears owners above the selected seat count. |
| `partner` | A slot reference with `cellId`, `sideIndex` and `edge`. A slot has exactly one reciprocal partner. The serializer writes both halves of pairs and joins. |

## Edge numbering

Cube faces are 0 top (+Y), 1 bottom (−Y), 2 right (+X), 3 left (−X), 4 front (+Z), 5 back (−Z). Edges 0–3 correspond to the four perpendicular faces in ascending index order. On the top face the edges point +X, −X, +Z, −Z.

The board builder pairs exposed slots around an outside corner, across a coplanar seam, or up the exposed wall of an inside corner. For the following cases, F is the source face of cell C and G is its adjacent face at the edge.

- If G is exposed, the partner is on C.G at the edge facing F.
- If G joins cell D and D.F is exposed, the partner is on D.F at the edge facing the opposite of G.
- If D.F is buried against cell E, the partner is on E’s face opposite G at the edge facing the opposite of F.

Sparse maps may omit whole pairs. Keep IDs stable, joins reciprocal, edge indices in range, and starting owners nonnegative. A valid face may hold slots from at most one player. The loader constructs joins before slots and validates the resulting board. The simulation follows the graph. The renderer uses the grid geometry to place tiles and draw flights. [Wave rules](WAVE_SPEC.md)
