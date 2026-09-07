# WhoWhoo3D_2 — Build From Scratch

> Step-by-step manual for building the project from an empty workspace.
>
> This guide is **opinionated and concrete** — exact paths, exact package names, exact file order. Deviate only when you understand why.
>
> Read order before starting:
> 1. [ARCHITECTURE.md](ARCHITECTURE.md) — the design
> 2. [WAVE_SPEC.md](WAVE_SPEC.md) — the gameplay rules
> 3. [MAP_SCHEMA.md](MAP_SCHEMA.md) — the data contract
> 4. This guide

---

## Default Decisions (overrideable)

These are the choices baked into this guide. If you want different, change them up front, not midway.

| Decision | Default | Why |
|---|---|---|
| Unity version | **Unity 6 LTS** (Universal 3D / URP template) | Current LTS (`6000.4.5f1`). |
| Render pipeline | **URP** | Mobile/console portable, decent visuals out of the box. |
| Input | **New Input System** | Legacy Input is deprecated; rebinding is built in. |
| DI framework | **None** — serialized inspector references | 5 scene refs total, all with auto-resolving fallbacks. A registry is machinery for a problem this project does not have. |
| Test framework | **Unity Test Framework + NUnit** | Built in. |
| Topology model | **Graph (Cell / Side / SideLink)** — not cube-locked | Future-proofs for any polyhedron. See ARCHITECTURE §"Topology vs Geometry". |
| Truth Pack location | **`/TruthPack/` standalone .NET 8 project** at the repo root, outside Assets/ | No Unity domain reload while testing core logic. Iterate fast. |
| Multiplayer | **Deferred** — see [TODO.md](TODO.md) §2 | Don't touch Netcode until the local game ships. |

> If you instead want **cube-only model from day 1**, replace every "Cell" with "Cube", "Side" with "Face", and use the cube-locked WAVE_SPEC examples directly. Generalizing later means a model rewrite — accept that tradeoff knowingly.

---

## Repo Layout (target)

```
WhoWhoo3D_2/                          ← Unity project
├── Assets/
│   ├── StreamingAssets/
│   │   └── Maps/
│   └── _Project/                     ← all game code here
│       ├── Runtime/
│       │   ├── Core/
│       │   ├── Domain/
│       │   ├── Gameplay/
│       │   ├── Data/
│       │   ├── Input/
│       │   ├── UI/
│       │   ├── Networking/           ← stays empty until Stage 13
│       │   ├── MapEditor/
│       │   └── Utils/
│       ├── Editor/
│       ├── Tests/
│       │   ├── EditMode/
│       │   └── PlayMode/
│       ├── Art/
│       ├── Audio/
│       ├── Prefabs/
│       ├── Scenes/
│       └── Settings/
├── Packages/
├── ProjectSettings/
├── TruthPack/                        ← pure .NET project (NOT under Assets)
│   ├── WhoWhoo3D.Truth.csproj
│   ├── WhoWhoo3D.Truth.Tests.csproj
│   └── src/
├── docs/                             ← these design docs
└── .gitignore
```

---

## Stage 0 — Bootstrap (½ day)

**Goal:** empty Unity project initialized + git + Truth Pack scaffold.

### 0.1 Verify prerequisites

- [ ] Unity Hub installed
- [ ] **Unity 6 LTS** installed via Hub (6000.0.x — pick the latest patch)
- [ ] .NET 8 SDK installed (`dotnet --version` ≥ 8.0)
- [ ] Git installed and configured
- [ ] Visual Studio 2022 OR JetBrains Rider with Unity support

### 0.2 Create the Unity project

- [ ] Unity Hub → New Project → **Universal 3D** template (URP) → Unity 6 LTS → name `WhoWhoo3D_2` → location `c:\Users\osman\Desktop\OSMANS_PROJECTS\` → Create
- [ ] Wait for first import to finish
- [ ] Close Unity

### 0.3 Initialize git

In `WhoWhoo3D_2/`:

- [ ] Use the Unity-tuned `.gitignore` from https://github.com/github/gitignore/blob/main/Unity.gitignore and the standard Unity `.gitattributes` (LFS for `.psd`, `.fbx`, `.blend`, etc.)
- [ ] `git init`
- [ ] `git add . && git commit -m "Initial Unity URP project"`

### 0.4 Copy docs

- [ ] Copy `WhoWhoo3D/docs/` → `WhoWhoo3D_2/docs/` so the new project carries its own design docs.
- [ ] Commit: `docs: import architecture, wave spec, map schema, build guide`

### 0.5 Create folder skeleton

In `WhoWhoo3D_2/Assets/`:

- [ ] Create `_Project/` and all subfolders from the layout above (empty for now)
- [ ] Create one placeholder `.gitkeep` file in each leaf folder so git tracks them
- [ ] Commit: `chore: create _Project skeleton`

### 0.6 Scaffold Truth Pack

In `WhoWhoo3D_2/TruthPack/` (sibling of Assets, **not under it**):

```
dotnet new classlib    -n WhoWhoo3D.Truth        -o src
dotnet new xunit       -n WhoWhoo3D.Truth.Tests  -o tests
dotnet new sln         -n WhoWhoo3D.Truth
dotnet sln add src/WhoWhoo3D.Truth.csproj tests/WhoWhoo3D.Truth.Tests.csproj
dotnet add tests/WhoWhoo3D.Truth.Tests.csproj reference src/WhoWhoo3D.Truth.csproj
```

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` succeeds (zero tests, but green)
- [ ] Commit: `truth: scaffold pure C# core + tests`

**Stage 0 exit:** project opens cleanly, `dotnet test` is green, git history has 4 commits.

---

## Stage 1 — Truth Pack: Topology (2-3 days)

**Goal:** the graph model is implemented and tested in pure C#. No Unity at all.

> If WAVE_SPEC.md still has TODOs in the rule sections, **stop and fill them in before this stage**. The tests below need concrete numbers.

### 1.1 Core types

Create `TruthPack/src/Topology/`:

- [ ] `Vec3.cs` — **your own position type**, not `UnityEngine.Vector3`:
  ```csharp
  public readonly record struct Vec3(float X, float Y, float Z);
  ```
  TruthPack has no Unity dependency. Unity converts to `UnityEngine.Vector3` in the view layer only.
- [ ] `CellId.cs` — `readonly struct CellId(string value)`
- [ ] `SideId.cs` — `readonly struct SideId(CellId cell, int sideIndex)`
- [ ] `PlayerId.cs` — `readonly struct PlayerId(int value)`, `PlayerId.None = new(0)`
- [ ] `Cell.cs` — id, position (`Vec3`), list of sides, list of tiles
- [ ] `Side.cs` — owning cell, local index, link (nullable)
- [ ] `SideLink.cs` — `readonly struct SideLink(SideId other)`
- [ ] `Tile.cs` — owner, side it sits on
- [ ] `BoardModel.cs` — `Dictionary<CellId, Cell>`, plus methods: `AddCell`, `LinkSides(a, b)`, `GetSide(SideId)`, `Validate()`

**Rule:** if a file in TruthPack ever needs `using UnityEngine;`, it belongs in the Unity project instead. TruthPack must stay runnable with plain `dotnet test`, no editor required.

Constraints to enforce in `BoardModel.LinkSides`:
- Both sides must exist
- Neither side can already have a link (links are exclusive)
- Self-links (cell.linkSelf) forbidden

### 1.2 Tests

Create `TruthPack/tests/Topology/`:

- [ ] `BoardModelTests.cs`
  - [ ] `AddCell_AssignsId`
  - [ ] `LinkSides_BothSidesShareLink`
  - [ ] `LinkSides_RejectsAlreadyLinked`
  - [ ] `LinkSides_RejectsSelfLink`
  - [ ] `Validate_PassesOnReciprocalLinks`
  - [ ] `Validate_FailsOnDanglingLink`

All tests must pass before moving on. **Commit as a single "topology core" commit so it's easy to revert.**

---

## Stage 2 — Truth Pack: Wave Simulator (3-4 days)

**Goal:** the wave engine, fully tested, before any Unity.

### 2.1 Wave types

Create `TruthPack/src/Wave/`:

- [ ] `WaveStep.cs` — represents one chain step's events: `List<TileMoved>`, `List<TileCaptured>`, `List<ScoreDelta>`
- [ ] `WaveResult.cs` — `List<WaveStep>` + final board state
- [ ] `WaveSimulator.cs` — `static WaveResult Simulate(BoardModel board, WaveConfig cfg)`
- [ ] `WaveConfig.cs` — Capacity, EmitAmount, MaxChainSteps, ScorePerCapture (from WAVE_SPEC.md constants)

### 2.2 Algorithm (graph version)

```
Simulate(board, cfg):
  result = new WaveResult()
  for step in 0..MaxChainSteps:
    emissions = collect all sides where tileCount >= Capacity
    if emissions empty: break

    waveStep = new WaveStep()
    for each emission side:
      target = side.link?.other
      if target == null: continue   // edge of the world
      resolve(emission, target, cfg, waveStep)

    result.steps.Add(waveStep)

  result.finalBoard = board
  return result
```

`resolve` implements R1-R5 from WAVE_SPEC.md.

### 2.3 Scenario tests

Translate every worked example from WAVE_SPEC.md into an xunit `[Theory]` or `[Fact]`:

- [ ] `Example1_SingleEmission_NoCollision`
- [ ] `Example2_EnemyCollision_SubtractRule`
- [ ] `Example3_SameOwnerStack_AtCapacity`
- [ ] `Example4_ChainWave`
- [ ] `Example5_FullCellEmission` (once R2 is finalized)
- [ ] At least 5 more scenarios you invent

Each test:
1. Build a small `BoardModel` literal in code
2. Call `WaveSimulator.Simulate`
3. Assert exact final state of every relevant cell + score deltas

**No randomness. Deterministic always.**

### 2.4 Commit milestone

Tag the commit: `git tag truth-pack-v1`. This is your "the rules are real" line.

---

## Stage 3 — Truth Pack: Map I/O (1 day)

**Goal:** v1 schema round-trips and validates per [MAP_SCHEMA.md](MAP_SCHEMA.md).

### 3.1 Implement

In `TruthPack/src/Maps/`:

- [ ] `MapDtos.cs` — DTOs matching MAP_SCHEMA.md exactly (`MapDto`, `CellDto`, `SideDto`, `TileStackDto`, `SideLinkDto`, `Vec3Dto`)
- [ ] `MapSerializer.cs` — `static string Serialize(BoardModel, MapMetadata)`, `static BoardModel Deserialize(string json)`
- [ ] Use `System.Text.Json` with sorted-key writing for stable diffs

### 3.2 Tests

Per the test list in MAP_SCHEMA.md "Test Cases":

- [ ] `RoundTrip_ProducesIdenticalJson`
- [ ] `Reject_MissingSchemaVersion`
- [ ] `Reject_WrongSchemaVersion`
- [ ] `Reject_DanglingLink`
- [ ] `Reject_NonReciprocalLink`
- [ ] `Reject_DuplicateCellId`
- [ ] `Reject_NonPositiveStackCount`
- [ ] `SparseSides_RoundTripPreservesOmissions`

---

## Stage 4 — Unity Bootstrap (1 day)

**Goal:** Unity project has packages installed, asmdefs in place, an empty Bootstrap scene.

### 4.1 Install packages

In Unity → Window → Package Manager → install:

- [ ] **Input System** (com.unity.inputsystem) — accept the prompt to switch input handling to "Both"
- [ ] **Test Framework** (com.unity.test-framework) — usually pre-installed
- [ ] **TextMeshPro** (com.unity.textmeshpro / com.unity.ugui) — import TMP Essentials when prompted
- [ ] **Addressables** (com.unity.addressables) — optional, install if you plan to load maps from disk
- [ ] **Netcode for GameObjects** — DO NOT install yet. Deferred to Stage 13.

### 4.2 Create asmdefs

In each `_Project/Runtime/<Module>/` create an `.asmdef`:

| Asmdef name | Folder | References |
|---|---|---|
| `WhoWhoo3D.Domain` | `Runtime/Domain/` | (none — no Unity refs, no MonoBehaviour) |
| `WhoWhoo3D.Core` | `Runtime/Core/` | (none) |
| `WhoWhoo3D.Data` | `Runtime/Data/` | Core |
| `WhoWhoo3D.Gameplay` | `Runtime/Gameplay/` | Domain, Core, Data |
| `WhoWhoo3D.Input` | `Runtime/Input/` | Core, Gameplay, Unity.InputSystem |
| `WhoWhoo3D.UI` | `Runtime/UI/` | Core, Gameplay, Data, Unity.TextMeshPro |
| `WhoWhoo3D.MapEditor` | `Runtime/MapEditor/` | Domain, Core, Data |
| `WhoWhoo3D.Utils` | `Runtime/Utils/` | (none) |

**Why `Domain` is separate:** `BoardModel`, `WaveSimulator`, and `MapSerializer` all live here. They return `BoardModel` objects, so they must all be in the same or downstream assemblies. If you put serializers in `Data` and models in `Gameplay`, `Data` would need to reference `Gameplay` while `Gameplay` already references `Data` — a compile cycle. `Domain` breaks the cycle: it has zero dependencies and everything else depends on it.

Add `Domain` to the folder skeleton from Stage 0.5 before proceeding.

In `_Project/Editor/` create `WhoWhoo3D.Editor.asmdef` with `editor: true` and refs to all runtime asmdefs.

In `_Project/Tests/EditMode/` create `WhoWhoo3D.Tests.EditMode.asmdef` (Editor-only, refs Test Framework + all runtime asmdefs).
In `_Project/Tests/PlayMode/` create `WhoWhoo3D.Tests.PlayMode.asmdef` (refs Test Framework + all runtime asmdefs).

- [ ] Verify Unity compiles cleanly (Console: 0 errors, 0 warnings ideally).

### 4.3 Bootstrap scene

- [ ] New scene `_Project/Scenes/Bootstrap.unity`
- [ ] Empty GameObject "ServiceContainer"
- [ ] Add to Build Settings as scene 0
- [ ] Commit: `feat: unity bootstrap, asmdefs, packages`

---

## Stage 5 — Port Truth Pack into Unity (1 day)

**Goal:** the C# model + simulator now live inside the Unity project, still pure (no MonoBehaviour).

### 5.1 Mechanics

- [ ] Copy `TruthPack/src/Topology/*.cs` → `Assets/_Project/Runtime/Domain/Board/`
- [ ] Copy `TruthPack/src/Wave/*.cs` → `Assets/_Project/Runtime/Domain/Wave/`
- [ ] Copy `TruthPack/src/Maps/*.cs` → `Assets/_Project/Runtime/Domain/Maps/`
- [ ] Set namespace to `WhoWhoo3D.Domain.*` throughout — all in the `WhoWhoo3D.Domain` asmdef, zero Unity refs
- [ ] `Vec3` stays as-is. In `BoardView.cs` (Unity side) convert: `new Vector3(pos.X, pos.Y, pos.Z)`
- [ ] **DO NOT** delete TruthPack — it stays as the canonical, non-Unity reference. New rule changes happen there first, then port.

### 5.2 Mirror the tests

- [ ] Copy each test class from `TruthPack/tests/` → `Assets/_Project/Tests/EditMode/`
- [ ] Convert xunit `[Fact]`/`[Theory]` to NUnit `[Test]`/`[TestCase]`
- [ ] Run via Unity → Test Runner → Run All
- [ ] All green before continuing

### 5.3 Save current state

- [ ] Tag: `git tag unity-truth-ported`

---

## Stage 6 — Board View (3-4 days)

**Goal:** a `BoardModel` displays as 3D cubes on screen. Read-only — no gameplay yet.

### 6.1 Visual prefab

- [ ] Create `Assets/_Project/Prefabs/Board/Cube.prefab` — a unit cube mesh with MeshRenderer + MeshFilter, default material. View MonoBehaviours attach at runtime, not on the prefab.

### 6.2 View MonoBehaviours

In `_Project/Runtime/Gameplay/Board/View/`:

- [ ] `BoardView.cs` — given a `BoardModel`, instantiates one `CellView` per cell, positions them in 3D space
- [ ] `CellView.cs` — represents one cell visually; references its `CellId` for lookups
- [ ] `SideView.cs` — represents one side; can be highlighted by player color
- [ ] `TileView.cs` — represents one tile; child of a SideView

The layout strategy: take `Cell.position` from the map (a Vector3 in the v1 schema; add it as map-side metadata, not topology-side).

### 6.3 Test scene

- [ ] New scene `_Project/Scenes/Game.unity`
- [ ] One Camera, one Directional Light, one ground plane
- [ ] Empty GameObject with `BoardView` script
- [ ] On Start, load a small hand-built `BoardModel` (3 cells linked in a line)
- [ ] Run — see 3 cubes on screen

### 6.4 Then load a v1 map from disk

- [ ] Hand-write a sample `Assets/StreamingAssets/Maps/sample.json` per [MAP_SCHEMA.md](MAP_SCHEMA.md) — 4-8 cells in a small layout
- [ ] At Start, read it via `MapSerializer.Deserialize` → `BoardModel` → `BoardView`
- [ ] Run — see the cells laid out

**Stage 6 exit:** Game scene loads a real v1 map JSON and renders it.

---

## Stage 7 — Turn Loop + Player Input (3-4 days)

**Goal:** clickable tiles, player turns rotate, end-of-turn fires a wave.

### 7.0 Pre-Stage 7 Fixes & Hardening
- [x] Fix `MapDtos.cs` import to standard `Newtonsoft.Json`.
- [x] Implement `BoardModel.Clone()` deep copy + unit tests.
- [x] Fix `TileView` runtime material leak by using pre-allocated shared materials.
- [x] Attach `BoxCollider` to cell GameObjects in `CellView` for raycast hit detection.
- [x] Optimize `WaveSimulator` (remove redundant emission scan) and `WaveConfig.Default` (`static readonly`).

### 7.1 State machine

In `_Project/Runtime/Gameplay/Turn/`:

- [x] `IGamePhase.cs` — `Enter`, `Tick(deltaTime)`, `Exit`, `IsComplete`, `NextPhase()`
- [x] `RoundStartPhase.cs`
- [x] `PlayerTurnPhase.cs` — waits for input (human) or plays automatically (AI)
- [x] `BoardWavePhase.cs` — runs `WaveSimulator.Simulate`, hands off to animator, waits for completion
- [x] `VictoryCheckPhase.cs`
- [x] `GameController.cs` — owns the phase machine; one MonoBehaviour, scene singleton (NOT static)

Two rules the phase machine has to keep:

- **`Tick` takes `deltaTime` as a parameter** rather than reading `UnityEngine.Time`.
  Edit Mode has no frame loop, so a phase that reads `Time.deltaTime` directly cannot
  be tested without entering Play Mode.
- **Transitions are pushed, not polled.** `GameController.OnCellClicked` advances the
  machine itself. An earlier draft only advanced inside `Update()`, which made every
  turn silently untestable in Edit Mode.

### 7.2 Players

In `_Project/Runtime/Gameplay/Player/`:

- [x] `PlayerState.cs` — id, alive flag, `IsAi` flag, on-demand score
- [x] `PlayerRoster.cs` — list of PlayerStates, current turn index, `AdvanceToNextAlive()`
- [ ] Colour and inventory list (deferred to Stage 9 / 10)

### 7.3 Input

In `_Project/Runtime/Input/`:

- [ ] Ensure Input Actions asset `_Project/Settings/InputActions.inputactions` configured (Click, Pause, Ability1-4)
- [x] `PlayerInputHandler.cs` — reads actions, raycasts to find clicked `CellView`, publishes click event

### 7.4 AI opponent — DEFERRED

Built and tested, then deferred: the current goal is local player-versus-player.
`GameController.enableAi` ships `false`. See [TODO.md](TODO.md) §1.

### 7.5 Test it

- [x] Edit Mode: turn loop, click handling, AI move selection, AI-vs-AI game terminates
- [ ] Manually click tiles in the Editor, end turn, watch wave fire
- [ ] PlayMode test: scripted scenario — load map, simulate player clicks, assert end state

---

## Stage 8 — Wave Animation (2-3 days)

In `_Project/Runtime/Gameplay/Wave/`:

- [x] `WavePlaybackPlan.cs` — pure C#. Turns a `WaveResult` into a replayable timeline:
      the pre-wave value of every touched side, plus one frame per chain step carrying that
      step's changes and the tile flights to draw. No `UnityEngine` reference, so the
      timeline is covered by Edit Mode tests.
- [x] `WaveAnimator.cs` — replays the plan with coroutine lerps; flying tiles arc from the
      emitting side to its linked target in the owner's colour.
- [x] Signals `BoardWavePhase` when playback completes (`IsPlaying` goes false).
- [ ] Highlights/VFX for captures vs friendly stacking (currently both just restack).
- [ ] Camera nudge or screen shake on large captures.

Three rules this stage has to keep:

- **The model is already final before the first frame plays.** `WaveSimulator` mutates the
  board in `Enter()`. Animation rewinds the *views* (`SideView.ShowStack`) and walks them
  forward; it must never write back to `BoardModel`. A skipped or interrupted animation
  then cannot desync the game.
- **Instant path stays first-class.** `WaveAnimator.CanPlay` is false outside Play Mode, so
  Edit Mode tests and AI-vs-AI runs resolve waves with zero delay through the same code.
- **`BoardWavePhase` polls, it does not subscribe.** This is why `IGamePhase.IsComplete` is
  a property rather than a callback — Stage 8 is the case it was shaped for.

---

## Stage 9 — Make the Game Legible (4-5 days)

**Goal:** a person who has never seen the game can sit down and play it. Right now the only
way to know whose turn it is, or which tiles you may click, is the Console.

### 9.1 Legal-move highlighting — do this first

With ~40 white tiles on screen and the rule "one enemy tile locks the whole face", the
player has no way to see what is clickable. This is not polish; without it the game is
unplayable by anyone who has not read WAVE_SPEC.

- [ ] Tint or outline slots where `BoardActions.CanClaim(board, slot, activePlayer)` is true
- [ ] Dim faces locked by another player
- [ ] Hover feedback on the slot under the pointer
- [ ] Show which face is one tile from expanding — the key tactical read

### 9.2 HUD

In `_Project/Runtime/UI/HUD/`:

- [ ] Turn banner: whose turn, in their colour
- [ ] Per-player tile count (`BoardActions.ScoreFor`)
- [ ] Wave indicator while `BoardWavePhase` is animating

### 9.3 Victory and restart

- [ ] Victory screen — `GameController.IsGameOver` / `Winner` are already exposed
- [ ] Restart: `InitializeWithBoard` already resets game-over state, so this is a button
- [ ] Quit / back to menu

### 9.4 Local player setup

Players are currently inferred from whichever tiles the map seeds. For a hot-seat game the
player count must be a choice, not a map artifact.

- [ ] Choose 2-4 players before starting
- [ ] Assign each a start position on the board
- [ ] Colour legend so players know which colour they are

### 9.5 EventBus

Stage 9 is the first real consumer, so `WhoWhoo3D.Core` finally gets sources.

- [ ] `EventBus` in `Core/Events/`
- [ ] Publish turn-changed / score-changed / player-eliminated / game-over
- [ ] Replace the `Debug.Log` calls in the phases with events
- [ ] Keep the phase machine **off** the bus — ordering matters there, so it stays direct calls

---

## Stage 9.6 — Debug Timeline (done)

Built alongside Stage 9 because debugging the expansion rules by eye was not working.

- [x] `GameHistory` / `HistoryEntry` (`Gameplay/History/`) — pure C#, no UnityEngine.
- [x] `TimelineView` (`UI/Debug/`) — ◀ ▶ arrows, pause, step-one-wave-chain, revert.
- [x] `WaveAnimator.Pause/Resume/StepOnce/StopAndSnapToModel`.
- [x] `BoardView.ShowOwner/ClearOverrides` for painting a past state.

Two rules this depends on:

- **`SlotStateChange` carries both `Before` and `After`,** so history is exactly reversible
  with no inverse-operation logic. Verified headlessly: 3342 changes over 60 turns reverse
  to the opening board and replay to the final board, both exact.
- **The arrows are a viewer, not an undo.** Scrubbing paints presentation overrides and
  never touches `BoardModel`, so the phase machine cannot desync. Only "Revert here"
  mutates, and it is a separate button on purpose.

---

## Stage 10 — Map Editor (4-5 days)

**Goal:** author maps without hand-writing JSON.

This has moved up the roadmap. Schema v2 slot pairings are not writable by hand — the only
existing map was produced by a throwaway Python script. Without an editor the game has
exactly one board.

### 10.1 Editor scene

- [ ] Place and delete cubes on a grid
- [ ] Auto-join touching faces (both become interior)
- [ ] Auto-wire the tile network by walking the exposed surface — the rule is in
      [MAP_SCHEMA.md](MAP_SCHEMA.md) §"Finding a slot's partner"
- [ ] Toggle individual slot pairs for sparse maps (always both halves, never one)
- [ ] Paint starting ownership

### 10.2 Save / load

- [ ] Write via `MapSerializer.Serialize`, read via `Deserialize`
- [ ] Run `BoardModel.Validate()` before saving and refuse to write an invalid map
- [ ] Save into `Assets/StreamingAssets/Maps/`

### 10.3 Pitfalls

- A slot with no partner is invalid. Create and delete pairs, never halves.
- An interior face may not hold slots. Joining two cubes must strip any slots first.
- Editor geometry must match `CubeGeometry.cs` exactly or tiles render on the wrong edges.

---

## Stage 11 — Polish & Ship (1 week)

- [ ] Two or three more maps built in the editor
- [ ] Audio: claim, expand, capture, victory
- [ ] VFX on capture and chain reactions
- [ ] Camera: orbit and zoom around the cluster
- [ ] Main menu → game → results flow through `SceneFlow`
- [ ] Standalone build, run it outside the editor
- [ ] Add URP Lit to Always Included Shaders (`SlotView.ResolveShader` warns if missing)

---

## Demo scenes

`Tools ▸ WhoWhoo3D ▸ Build Demo4x4 Scene` generates `Demo4x4.unity`: a 16-cube flat slab,
playable on the **top faces only**, for 3 hot-seat players on `demo4x4.json`.

It exists to exercise the rules at a scale two cubes cannot. Because capacity is per-face,
the slab has three natural tiers — **4 corner faces expand at 2 tiles, 8 edge faces at 3,
4 interior faces at 4** — which is where the chain behaviour actually becomes visible.
48 slots across 24 paired edges.

The scene is generated rather than hand-wired; see [TODO.md](TODO.md) §4 for why, and for the
`SingleplayerGame` / `MultiplayerGame` scenes that should come through the same builder.
**Leave `Game.unity` alone** — it is the working two-cube reference.

---

## Deferred

AI opponent, online multiplayer and the abilities system are **out of scope**, and so is the
UI/mesh design pass ([TODO.md](TODO.md) §5) — though that one should land *before* the other
three, since each would otherwise invent its own ad-hoc UI.

**Nothing in TODO.md starts until the singleplayer core is finished and tested.** Extensions
built against rules that are still moving put their seams in the wrong places; the slot-model
rewrite already cost ~60 tests, the map schema, the AI's scoring and every view in one go.

---

## Default Assets to Create

Build these fresh in `WhoWhoo3D_2/Assets/_Project/`:

- [ ] **Cube prefab** — `Prefabs/Board/Cube.prefab`. MeshRenderer + MeshFilter + BoxCollider.
- [ ] **Tile prefab** — `Prefabs/Board/Tile.prefab`. Single quad/cube with material-swap for ownership.
- [ ] **Player color materials** — `Art/Materials/Player1.mat` ... `Player4.mat`. Distinct hues, high contrast.
- [ ] **Sample map** — `StreamingAssets/Maps/sample.json` per MAP_SCHEMA. 4-8 cells, hand-written.
- [ ] **Audio clips** — capture, wave whoosh, victory fanfare. Free CC0 sources are fine for v1.
- [ ] **Font assets** — TMP defaults are OK initially.

---

## Naming Conventions

| Concept | Name |
|---|---|
| Top-level game orchestrator | `GameController` (only one) |
| Board controller | `BoardController` |
| Cell-on-the-board | `Cell` in code (cube is just the visual) |
| Side of a cell | `Side` (not `Face`, not `CubeFace`) |
| Side identifier in the model | integer `sideIndex` (not `Direction` enum — direction names live in `CubeView` only) |
| Player's stash UI | `Inventory` |
| Online play class prefix | `Multiplayer` |
| Game mode | `GameMode` enum on `GameController`, NOT a duplicated class hierarchy |

## Common Pitfalls (Learned From Legacy)

1. **No commented-out code in commits.** If logic is being replaced, delete it. Git remembers.
2. **No singleton-finds-singleton chains.** If module A needs module B, inject B in the constructor or via a setter. `Singleton<T>.Instance` is allowed only at the scene root.
3. **Any lookup array of enum values must have a unit test verifying every value appears exactly once.** Trivial test, catches typos that nothing else catches.
4. **IDs are monotonic counters or GUIDs, never list indexes.** Deletion breaks index-based IDs.
5. **Always run `BoardModel.Validate()` after every mutation in Editor / debug builds.** Catches corruption early.
6. **Pure C# first, Unity second.** Anything that *can* be tested without a scene running, *should* be.
7. **Don't gate scene loading on network state.** Loader loads. Network is a separate concern.
8. **One model, two views.** Singleplayer and Multiplayer share the same `BoardModel` and `WaveSimulator`. Mode is a flag on `GameController`, not a duplicated class hierarchy.

---

## Commit Cadence

Aim for one commit per checkbox completed. Tag:

- `truth-pack-v1` — end of Stage 2
- `unity-truth-ported` — end of Stage 5
- `board-view-v1` — end of Stage 6
- `playable-loop-v1` — end of Stage 7
- `wave-animation-v1` — end of Stage 8
- `playable-v1` — end of Stage 9 (legible, HUD, victory)
- `editor-v1` — end of Stage 10
- `v1.0-local` — end of Stage 11

If you go more than 2 days without a green test run + commit, stop and investigate why.
