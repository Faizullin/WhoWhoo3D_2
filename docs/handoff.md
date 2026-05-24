# WhoWhoo3D_2 — Project Handoff

> Written: 2026-05-24. Describes what the project is, how it is structured, and what has been built so far.

---

## What the game is

**WhoWhoo3D** is a 3D turn-based strategy game in the style of Dice Wars / Risk.

- The board is made of **cells** (cubes by default, but the model is shape-agnostic).
- Each cell face is a **side**. Sides hold **tile stacks** belonging to a player.
- On a player's turn they trigger a **wave**: tile stacks that reach capacity (≥4) emit tiles to linked neighbour sides, potentially cascading in a chain reaction.
- A player wins by eliminating all opponents' tiles from the board.

The game is single-player (vs AI) first; networked multiplayer is deferred to a later phase.

---

## Repository layout

```
WhoWhoo3D_2/
├── Assets/
│   └── _Project/
│       ├── Runtime/
│       │   ├── Domain/          ← pure domain logic (no Unity API)
│       │   │   ├── Board/       ← graph topology types
│       │   │   ├── Wave/        ← deterministic wave simulator
│       │   │   └── Maps/        ← JSON serialiser / deserialiser
│       │   └── Gameplay/
│       │       └── Board/
│       │           └── View/    ← MonoBehaviours: BoardView, CellView, SideView, TileView
│       ├── Tests/
│       │   └── EditMode/        ← NUnit tests for Domain (Board, Wave, Maps)
│       └── Prefabs/
│           └── Board/
│               └── Cube.prefab  ← 1-unit cube mesh, no MonoBehaviours
├── StreamingAssets/
│   └── Maps/
│       └── sample.json          ← 6-cell 2×3 grid map used during development
├── TruthPack/                   ← pure .NET 8 project, NO Unity dependency
│   └── src/                     ← canonical C# source (ported into Domain assembly)
├── docs/                        ← design documents
│   ├── ARCHITECTURE.md
│   ├── BUILD_GUIDE.md           ← 12-stage implementation roadmap
│   ├── MAP_SCHEMA.md
│   ├── WAVE_SPEC.md
│   └── handoff.md               ← this file
└── Packages/                    ← Unity package manifest (managed by user via Package Manager)
```

---

## Assembly structure

| Assembly | Location | Purpose |
|---|---|---|
| `WhoWhoo3D.Domain` | `Runtime/Domain/` | Pure domain logic. `noEngineReferences: true`. References only `Newtonsoft.Json`. |
| `WhoWhoo3D.Gameplay` | `Runtime/Gameplay/` | Unity MonoBehaviours. References Domain. |
| `WhoWhoo3D.Tests.EditMode` | `Tests/EditMode/` | NUnit tests run in Unity Editor. `overrideReferences: true`, explicit `nunit.framework.dll` + `Newtonsoft.Json.dll`. |

Every assembly has a `csc.rsp` file next to its `.asmdef` enabling `-langversion:latest -nullable:enable` (required because Unity 6 defaults to C# 9 and `record` types need C# 10+).

A `IsExternalInit.cs` polyfill lives in the Domain assembly to support `record` and `init` under .NET Standard 2.1 (Unity's compile target).

---

## Domain model

### Board (`WhoWhoo3D.Domain.Board`)

| Type | Kind | Description |
|---|---|---|
| `Vec3` | `readonly record struct` | 3D position (float X/Y/Z). No Unity dependency. |
| `CellId` | `readonly record struct` | String identifier for a cell. |
| `PlayerId` | `readonly record struct` | Integer player index. `PlayerId.None = new(0)`. |
| `SideId` | `readonly record struct` | `(CellId, int LocalIndex)` pair. |
| `SideLink` | `readonly record struct` | Points to the other `SideId` of a connection. |
| `TileStack` | `readonly record struct` | `(PlayerId Owner, int Count)`. `TileStack.Empty` for unoccupied. |
| `Side` | `class` | Owns a `Stack` and an optional `Link`. Mutable via internal setters. |
| `Cell` | `class` | List of `Side`s, `Vec3 Position`, `CellId Id`. |
| `BoardModel` | `class` | `Dictionary<CellId, Cell>`. Methods: `AddCell`, `LinkSides`, `SetStack`, `AddTiles`, `RemoveTiles`, `Validate()`. |

Rules enforced by `BoardModel`:
- Both sides must exist before linking.
- A side can only have one link (exclusive).
- No self-links.
- `Validate()` checks reciprocity of every link.

### Wave (`WhoWhoo3D.Domain.Wave`)

`WaveSimulator.Simulate(BoardModel, WaveConfig) → WaveResult`

- Pure static method, no mutation of the board passed in (works on a copy).
- `WaveConfig.Default`: Capacity = 4, Keep = 1, MaxChainSteps = 5.
- Returns a list of `WaveStep` records describing each chain step's side-state changes.

### Maps (`WhoWhoo3D.Domain.Maps`)

`MapSerializer.Serialize(BoardModel, MapMetadata) → string` / `MapSerializer.Deserialize(string) → BoardModel`

- JSON via `Newtonsoft.Json` (package `com.unity.nuget.newtonsoft-json` 3.2.2).
- Schema version field guards against stale files.
- Validates: no duplicate cell IDs, all links are reciprocal, stack counts > 0.
- Map files live in `Assets/StreamingAssets/Maps/` and are loaded at runtime with `File.ReadAllText`.

Map JSON schema: `{ schemaVersion, name, createdAt, cells[] → { id, position, sideCount, sides[] → { index, stack?, link? } } }`

---

## View layer (Stage 6)

| MonoBehaviour | Responsibility |
|---|---|
| `BoardView` | Reads map JSON in `Start()`, calls `MapSerializer.Deserialize`, instantiates one `Cube.prefab` + `CellView` per cell. |
| `CellView` | Positions itself at `cell.Position` (local). Creates one `SideView` child per side, placed at the matching face offset (FaceOffsets[0..5]). |
| `SideView` | Owns a `TileView` child. Spawns it when `Side.Stack` is non-empty, destroys it when empty. Calls `TileView.Refresh` on state change. |
| `TileView` | Spawns a `PrimitiveType.Sphere` child. Scale = `0.15 + Count × 0.04`. Color = Red/Blue/Green/Yellow for players 1-4. |

Face offset mapping (unit cube, local space):

| Index | Direction | Offset |
|---|---|---|
| 0 | Top (+Y) | (0, 0.5, 0) |
| 1 | Bottom (−Y) | (0, −0.5, 0) |
| 2 | Right (+X) | (0.5, 0, 0) |
| 3 | Left (−X) | (−0.5, 0, 0) |
| 4 | Front (+Z) | (0, 0, 0.5) |
| 5 | Back (−Z) | (0, 0, −0.5) |

The `Cube.prefab` holds only a `MeshFilter` + `MeshRenderer` (1-unit Unity default cube mesh). MonoBehaviours are added at runtime via `AddComponent`.

---

## Test coverage

All 56 tests pass in the Unity Test Runner (Edit Mode):

| Suite | Tests | Coverage |
|---|---|---|
| `Board/BoardModelTests.cs` | 29 | AddCell, LinkSides, Validate, SetStack, AddTiles, RemoveTiles |
| `Wave/WaveSimulatorTests.cs` | 17 | All WAVE_SPEC worked examples + edge cases |
| `Maps/MapSerializerTests.cs` | 10 | Round-trip, reject invalid JSON, reject bad schema version, link reciprocity, stack validation |

---

## Build progress (12-stage plan)

| Stage | Description | Status |
|---|---|---|
| 0 | Bootstrap — Unity project, git, TruthPack scaffold | Done |
| 1 | TruthPack: topology types + BoardModel tests | Done |
| 2 | TruthPack: WaveSimulator + tests | Done |
| 3 | TruthPack: MapSerializer + tests | Done |
| 4 | Port TruthPack → Unity Domain assembly | Done |
| 5 | Unity Edit-Mode tests (56 tests green) | Done |
| 6 | Board view — cubes + tiles rendered from JSON map | Done |
| **7** | **Turn loop + player input** | **Next** |
| 8 | Wave animation | Not started |
| 9 | AI player | Not started |
| 10 | UI / HUD | Not started |
| 11 | Audio + polish | Not started |
| 12 | Multiplayer (deferred) | Not started |

---

## Stage 7 — what comes next

**Goal:** a player can click a cell to trigger a wave, turns advance, game detects winner.

Key pieces to build:

1. **`PlayerState`** — player id, alive/eliminated flag.
2. **`PlayerRoster`** — list of players, active player index, `NextTurn()`.
3. **`IGamePhase`** — interface with `Enter()` / `Exit()` for state machine phases (WaitForInput, WaveResolve, CheckVictory, NextTurn).
4. **`GameController` (MonoBehaviour)** — owns the BoardModel, PlayerRoster, current phase. Drives the phase machine in `Update`.
5. **`PlayerInputHandler` (MonoBehaviour)** — listens to click/tap on a `CellView`, raises an event. `GameController` subscribes during WaitForInput phase.
6. **Cell click detection** — add a `MeshCollider` or `BoxCollider` to the Cube prefab so raycasts hit it; `CellView` exposes a `CellId` property so the handler knows which cell was clicked.
7. **`WaveAnimator`** — coroutine that plays through `WaveResult.Steps` one step per frame (or timed), then calls `Refresh` on affected `SideView`s.

No networking, no AI, no score UI yet — just the raw turn loop working end-to-end in the Editor Game view.

---

## Known constraints (carry forward)

- **Never edit `Packages/manifest.json`** — all package changes are done by the user via Unity Package Manager UI.
- **Explicit `using` statements in every file** — no `GlobalUsings.cs`, no `csc.rsp` workarounds for missing types.
- **No `git add` / `git commit`** — the user stages and commits themselves.
- **Unity 6 LTS** (6000.4.5f1), URP, New Input System.
- **`noEngineReferences: true`** on the Domain assembly — keep domain code free of Unity API.
