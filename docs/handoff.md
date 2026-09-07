# WhoWhoo3D_2 — Project Handoff

> Written: 2026-05-24. Updated: 2026-09-07. Describes what the project is, how it is
> structured, and what has been built so far.
>
> **Verification status:** everything below through Stage 7.5 compiles cleanly (Gameplay,
> Input and the new Edit Mode tests were compiled against the Unity 6000.4.5f1 reference
> assemblies). The test suite has **not** been executed in the Unity Test Runner since
> Stage 6 — open the project in Unity and run Edit Mode before treating the counts below
> as green.

---

## What the game is

**WhoWhoo3D** is a 3D turn-based strategy game played on a cluster of cubes.

- Cubes are pressed together. Faces that touch become **interior** — buried and unplayable.
- The remaining **exposed** faces form one continuous surface, and that surface is the board.
- Each exposed face carries up to 4 **tile slots**, one at the midpoint of each edge. Every
  slot is paired with the slot across that edge, so tiles always exist in twos.
- A tile is **white** (unclaimed) or owned. On your turn you claim one white tile — but only
  on a face where no other player already holds a tile.
- Fill every slot on a face and it **expands**: each of its tiles claims its partner across
  the edge, and the face itself is released back to white.
- Last player holding tiles wins.

**Current goal: a simple local player-versus-player game.** The AI opponent and online
multiplayer are built or designed but deliberately deferred — see [TODO.md](TODO.md).

---

## Repository layout

```
WhoWhoo3D_2/
├── Assets/
│   ├── StreamingAssets/Maps/sample.json   ← 2-cube v2 map, 40 slots, 20 pairs
│   └── _Project/
│       ├── Runtime/
│       │   ├── Domain/          ← pure C#, no Unity API
│       │   │   ├── Board/       ← BoardModel, Cell, Side, Slot, SlotId
│       │   │   ├── Wave/        ← WaveSimulator, WaveConfig, SlotStateChange
│       │   │   └── Maps/        ← MapSerializer (schema v2)
│       │   ├── Gameplay/
│       │   │   ├── Board/       ← BoardActions, CubeGeometry
│       │   │   │   └── View/    ← BoardView, CellView, SlotView
│       │   │   ├── Player/      ← PlayerState, PlayerRoster
│       │   │   ├── Turn/        ← IGamePhase + 4 phases
│       │   │   ├── Wave/        ← WavePlaybackPlan (pure), WaveAnimator
│       │   │   ├── Ai/          ← GreedyAiPlayer (deferred)
│       │   │   ├── History/     ← GameHistory, HistoryEntry (pure)
│       │   │   └── Game/        ← GameController
│       │   ├── Input/           ← PlayerInputHandler
│       │   └── UI/              ← HUD/ScoreboardView, Debug/TimelineView (IMGUI)
│       ├── Tests/EditMode/      ← NUnit: Board, Wave, Maps, Turn, Ai, Support
│       ├── Prefabs/Board/Cube.prefab
│       └── Scenes/              ← Bootstrap.unity (empty shell), Game.unity (playable)
├── TruthPack/                   ← DOES NOT COMPILE — see TODO.md §4
└── docs/
```

---

## Domain model

| Type | Kind | Description |
|---|---|---|
| `CellId` / `PlayerId` / `SideId` | `readonly record struct` | Identifiers. `PlayerId.None` = unclaimed/white. |
| `SlotId` | `readonly record struct` | `(SideId Side, int Edge)` — a tile position on one edge of a face. |
| `Slot` | `class` | `Id`, `Partner`, `Owner`. `IsNeutral` when unclaimed. |
| `Side` | `class` | Up to 4 slots. `IsInterior` when joined to another cell. `IsFull(out owner)` is the expansion trigger. |
| `Cell` | `class` | Sides + position. `ExposedSides` filters out buried faces. |
| `BoardModel` | `class` | `AddCell`, `JoinSides`, `AddSlotPair`, `SetOwner`, `ClearSide`, `Validate`, `Clone`. |

`Validate()` enforces two rules that carry gameplay meaning: **no lone slot** (a tile with no
partner has nothing to expand across) and **no contested face** (a face holding two players'
tiles is clickable by nobody).

Edge indexing, the partner-resolution rule, and the wave rules are specified in
[MAP_SCHEMA.md](MAP_SCHEMA.md) and [WAVE_SPEC.md](WAVE_SPEC.md).

---

## Test coverage

62 Edit Mode tests. **They have never been executed in the Unity Test Runner** — they
compile against the Unity reference assemblies, and the Domain was additionally run
headlessly against `sample.json`, but a real test run is still outstanding.

| Suite | Tests |
|---|---|
| `Ai/GreedyAiPlayerTests.cs` | 8 |
| `Board/BoardModelTests.cs` | 12 |
| `Maps/MapSerializerTests.cs` | 11 |
| `Turn/PlayerRosterTests.cs` | 5 |
| `Turn/TurnLoopTests.cs` | 8 |
| `Wave/WavePlaybackPlanTests.cs` | 7 |
| `Wave/WaveSimulatorTests.cs` | 11 |

---

## Build progress

**Primary goal: simple local player-vs-player game.**

| Stage | Description | Status |
|---|---|---|
| 0-6 | Bootstrap → topology → wave → maps → Unity port → board view | Done |
| 7 | Turn loop + player input (phases, click-to-claim) | Rewritten for slot model; compiles; **needs a Unity run** |
| 8 | Wave animation (`WavePlaybackPlan` + `WaveAnimator`) | Rewritten for slot model; compiles; **needs a Unity run** |
| 9.6 | Debug timeline — history, arrows, pause, revert | Built; compiles; reverse/replay proven headlessly |
| **9** | **Make the game legible — highlighting, HUD, victory, player setup** | **Next** |
| 10 | Map editor | Not started |
| 11 | Polish & ship local | Not started |
| — | AI opponent, online multiplayer, abilities | **Deferred → [TODO.md](TODO.md)** |

---

## Where the model came from

The board was originally one `TileStack (Owner, Count)` per face, with tiles flowing along
cell-to-cell face links. That model was replaced wholesale in Sept 2026 after the 3D mock-up
showed what was actually wanted: discrete square tiles on cube edges, forming a network over
the exposed surface.

Consequences still visible in the repo:

- Map schema jumped to **v2** with no migrator. v1 maps do not load.
- `TruthPack` was not ported and no longer compiles.
- `WaveConfig.Capacity` is gone — capacity is per-face, equal to that face's slot count.
- `WAVE_SPEC.md` is at v0.2; its v0.1 rules (subtract-based combat, `Keep = 1`) are dead.

---

## Known constraints (carry forward)

- **Never edit `Packages/manifest.json`** — all package changes are done by the user via Unity Package Manager UI.
- **Explicit `using` statements in every file** — no `GlobalUsings.cs`, no `csc.rsp` workarounds for missing types.
- **No `git add` / `git commit`** — the user stages and commits themselves.
- **Unity 6 LTS** (6000.4.5f1), URP, New Input System.
- **`noEngineReferences: true`** on the Domain assembly — keep domain code free of Unity API.
- **Current goal is the local player-vs-player game.** AI, networking and abilities are
  deferred in [TODO.md](TODO.md); don't start them while a roadmap stage is open.
- **Tiles always exist in pairs.** Create and delete slot pairs, never halves — a lone slot
  fails `Validate()`.
- **`CubeGeometry` is the only place that knows a cell is a cube.** The map author must use
  the same edge convention or tiles render on the wrong edges.
- **No nullable annotations** on reference types — see the Assembly structure section.
- **TruthPack and Domain are a deliberate fork, not a mirror.** TruthPack uses
  `System.Text.Json` + xUnit; the Unity Domain uses `Newtonsoft.Json` + NUnit. Everything
  else must stay byte-identical. Any change to topology or wave logic has to land in
  **both** — a half-applied edit already left TruthPack with a duplicate-local compile
  error that went unnoticed because nothing on this machine builds it (`dotnet` is not
  installed). Either install the .NET SDK and build TruthPack as part of the routine, or
  retire it now that the Unity Edit Mode suite covers the same ground.
- **Verify before marking a stage done.** Unity generates a `.meta` file per script on
  import; source files with no `.meta` have never been seen by the editor, let alone
  compiled or tested. Check that before writing "N tests pass" into this document.
