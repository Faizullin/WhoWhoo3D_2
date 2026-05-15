# Map JSON Schema — v1

> The data contract for saved maps. Matches the `BoardModel` / `Cell` / `Side` types in `WhoWhoo3D.Domain`.

---

## Schema

```jsonc
{
  "schemaVersion": 1,
  "name": "Default",
  "createdAt": "2026-05-08T12:00:00Z",
  "cells": [
    {
      "id": "cell_0001",
      "position": { "x": 0.0, "y": -0.5, "z": 8.0 },
      "sideCount": 6,
      "sides": [
        {
          "index": 0,
          "stack": { "owner": 1, "count": 2 },
          "link": { "cellId": "cell_0002", "sideIndex": 4 }
        },
        {
          "index": 2,
          "stack": null,
          "link": { "cellId": "cell_0003", "sideIndex": 1 }
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
| `schemaVersion` | int (required) | Always `1` for this version. Loaders MUST read this first. |
| `name` | string | Human-readable label. |
| `createdAt` | ISO-8601 string | UTC. Sortable, locale-free. |
| `cells` | array of Cell | The board. |

### Cell

| Field | Type | Notes |
|---|---|---|
| `id` | string (required) | Stable identifier. Not an array index — survives deletes. Convention: `cell_NNNN` zero-padded. |
| `position` | `{x, y, z}` floats | World-space position. **View-only metadata.** The simulator never reads this. |
| `sideCount` | int (required) | Number of sides this cell has (6 for cubes). Sides are 0-indexed, `0..sideCount-1`. |
| `sides` | array of Side | **Sparse** — only sides that have a stack OR a link need to be present. Omitted sides are empty + unlinked. |

### Side

| Field | Type | Notes |
|---|---|---|
| `index` | int (required) | Local index, `0..sideCount-1`. |
| `stack` | TileStack or null | Tiles on this side. `null` or omitted means empty. |
| `link` | SideLink or null | Linked neighbor. `null` or omitted means no neighbor. |

### TileStack

| Field | Type | Notes |
|---|---|---|
| `owner` | int (required) | `PlayerId.Value`. Must be > 0 (0 is `PlayerId.None`, only valid for empty stacks which are written as `null`). |
| `count` | int (required) | Must be > 0. Empty stacks are written as `null`, never `{owner: 0, count: 0}`. |

### SideLink

| Field | Type | Notes |
|---|---|---|
| `cellId` | string (required) | Must match an existing `cells[].id`. |
| `sideIndex` | int (required) | Must be in range `0..sideCount-1` of the linked cell. |

---

## Design Choices

1. **`schemaVersion` is mandatory.** Loaders dispatch on it. v2 will be additive when needed.
2. **Stable string IDs**, not array indexes. Deletes don't break references. Saved files are diff-friendly.
3. **Sparse `sides` array.** Empty sides are omitted. A bare cube has `sides: []`.
4. **Topology, not direction names.** Sides are integer-indexed. The renderer maps `index → "top" / "right" / ...` for cubes; the schema doesn't care.
5. **`position` is view-only metadata.** The wave simulator works on the link graph alone.
6. **No reciprocal-link duplication in JSON.** If A side 0 ↔ B side 4, both sides MUST list the link. The serializer writes both; the validator checks both.
7. **ISO-8601 timestamps.** Sortable, locale-free.

---

## Validation Rules

A loader MUST reject a map that:

- Has no `schemaVersion` field, or `schemaVersion != 1`.
- Has duplicate `cells[].id` values.
- Has any `sides[].index` outside `0..sideCount-1`.
- Has a `link.cellId` that doesn't exist in `cells`.
- Has a `link.sideIndex` outside the target cell's range.
- Has a non-reciprocal link (A → B without B → A on the matching sides).
- Has a `stack` with `count <= 0` or `owner <= 0`.

These are exactly the same checks `BoardModel.Validate()` performs after deserialization.

---

## Test Cases (Stage 3 must pass)

- [ ] **Round-trip:** write → read → write produces identical JSON (after key sort).
- [ ] **Reject `schemaVersion` missing.**
- [ ] **Reject `schemaVersion` ≠ 1.**
- [ ] **Reject dangling link** (`cellId` doesn't exist).
- [ ] **Reject non-reciprocal link.**
- [ ] **Reject duplicate cell IDs.**
- [ ] **Reject `stack.count <= 0`.**
- [ ] **Sparse sides** — a cell with only sides 0 and 2 populated round-trips with `sides: [..., index:0, ..., index:2, ...]` and others omitted.

---

## Future Versions

When the schema must change, bump `schemaVersion` to 2 and add a `MapV2Migrator` that takes v1 → v2. Keep v1 readable indefinitely; never break old saved maps.
