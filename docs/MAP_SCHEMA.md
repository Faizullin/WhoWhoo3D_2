# Map JSON Schema — v2

> The data contract for saved maps. Matches `BoardModel` / `Cell` / `Side` / `Slot` in
> `WhoWhoo3D.Domain`.
>
> **v1 is not readable.** It described one tile stack per face and cell-to-cell face links;
> the model is now edge-paired tiles on an exposed surface. No migrator exists — v1 maps
> must be regenerated.

---

## Schema

```jsonc
{
  "schemaVersion": 2,
  "name": "Sample",
  "createdAt": "2026-09-07T00:00:00+00:00",
  "cells": [
    {
      "id": "cell_0001",
      "position": { "x": 0, "y": 0, "z": 0 },
      "sideCount": 6,
      "joins": [
        { "side": 2, "cellId": "cell_0002", "sideIndex": 3 }
      ],
      "slots": [
        {
          "side": 0, "edge": 0, "owner": 1,
          "partner": { "cellId": "cell_0002", "sideIndex": 0, "edge": 1 }
        }
      ]
    }
  ]
}
```

---

## Field Reference

### Top level

| Field | Type | Notes |
|---|---|---|
| `schemaVersion` | int (required) | Always `2`. Loaders read this first and reject anything else. |
| `name` | string | Human-readable label. |
| `createdAt` | ISO-8601 string | UTC. |
| `cells` | array of Cell | The board. |

### Cell

| Field | Type | Notes |
|---|---|---|
| `id` | string (required) | Stable identifier, not an array index. Convention `cell_NNNN`. |
| `position` | `{x,y,z}` floats | World position. **View-only** — the simulator never reads it. |
| `sideCount` | int (required) | 6 for cubes. Sides are 0-indexed. |
| `joins` | array of Join | Faces pressed against a neighbour. |
| `slots` | array of Slot | Tiles on this cell's exposed faces. |

### Join

Marks two faces as touching. Both become **interior**: invisible, unplayable, and forbidden
from holding slots.

| Field | Type | Notes |
|---|---|---|
| `side` | int (required) | This cell's face index. |
| `cellId` | string (required) | The neighbouring cell. |
| `sideIndex` | int (required) | The neighbour's face index. |

Both cells list the join. The loader applies each pair once and the validator checks
reciprocity.

### Slot

One tile position, at the midpoint of one edge of a face.

| Field | Type | Notes |
|---|---|---|
| `side` | int (required) | Face index, `0..sideCount-1`. Must be an **exposed** face. |
| `edge` | int (required) | `0..3`. See Edge Indexing below. |
| `owner` | int | `PlayerId.Value`. `0` or omitted means an unclaimed **white** tile. |
| `partner` | SlotRef (**required**) | The other half of the pair. |

### SlotRef

| Field | Type | Notes |
|---|---|---|
| `cellId` | string (required) | May be this cell (corner pairing) or a neighbour (seam pairing). |
| `sideIndex` | int (required) | |
| `edge` | int (required) | |

Both halves are listed, each naming the other. The serializer writes both; the validator
checks reciprocity.

---

## Edge Indexing

Face indices for a cube: `0` top (+Y), `1` bottom (−Y), `2` right (+X), `3` left (−X),
`4` front (+Z), `5` back (−Z). Opposites pair as 0-1, 2-3, 4-5.

**A face's four edges are its four perpendicular faces, in ascending face index.** So the
top face's edges run `0` → +X, `1` → −X, `2` → +Z, `3` → −Z.

This convention is shared by `CubeGeometry.cs` (which places the tile in the scene) and by
whatever writes the map. They must agree or tiles will render on the wrong edges.

### Finding a slot's partner

For an exposed face `F` of cell `C`, and edge `e` whose neighbouring face is `G`:

- **`G` is exposed on `C`** — pair around this cube's own corner:
  partner = `(C, G, edgeIndexOf(G, F))`
- **`G` is interior**, joined to cell `D` — pair across the seam onto `D`'s same-facing face:
  partner = `(D, F, edgeIndexOf(F, opposite(G)))`

Every edge of the exposed surface yields exactly one reciprocal pair, so a fully-wired
cluster gives each exposed face exactly 4 slots. Sparse maps are fine — just omit pairs,
never halves.

---

## Validation Rules

A loader MUST reject a map that:

- Has `schemaVersion != 2`, or no `schemaVersion`
- Has duplicate `cells[].id`
- Has a `joins[]` entry naming a cell or side index that does not exist
- Has a non-reciprocal join
- Declares a **slot on an interior face**
- Has a slot with **no `partner`**, or whose partner slot is never created
- Has a non-reciprocal slot pairing
- Has an `edge` outside `0..3`, or a `side` outside `0..sideCount-1`
- Has a negative `owner`
- Leaves any face **contested** — holding tiles of two different players

These are the same checks `BoardModel.Validate()` runs after deserialization.

The lone-slot and contested-face rules are the two that carry gameplay meaning:
a tile with no partner has nothing to expand across, and a contested face cannot be
clicked by anybody (see WAVE_SPEC R1/R4).

---

## Future Versions

Bump to 3 and add a migrator when the model changes again. The v1 → v2 jump deliberately
had none: the two models share no meaningful structure.
