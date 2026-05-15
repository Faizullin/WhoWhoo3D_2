# WhoWhoo3D_2 — Build From Scratch

> Step-by-step manual for building the new project at `c:\Users\osman\Desktop\OSMANS_PROJECTS\WhoWhoo3D_2` from an empty workspace.
>
> This guide is **opinionated and concrete** — exact paths, exact package names, exact file order. Deviate only when you understand why.
>
> Read order before starting:
> 1. [ARCHITECTURE.md](ARCHITECTURE.md) — the design
> 2. [WAVE_SPEC.md](WAVE_SPEC.md) — fill in the TODOs first
> 3. [MAP_SCHEMA.md](MAP_SCHEMA.md) — the data contract
> 4. This guide

---

## Default Decisions (overrideable)

These are the choices baked into this guide. If you want different, change them up front, not midway.

| Decision | Default | Why |
|---|---|---|
| Unity version | **Unity 6 LTS** (Universal 3D / URP template) | Current LTS. |
| Render pipeline | **URP** | Mobile/console portable, decent visuals out of the box. |
| Input | **New Input System** | Legacy Input is deprecated; rebinding is built in. |
| DI framework | **None initially** (manual ctor injection + ServiceLocator) | Avoid VContainer/Zenject lock-in until you actually need it. |
| Test framework | **Unity Test Framework + NUnit** | Built in. |
| Topology model | **Graph (Cell / Side / SideLink)** — not cube-locked | Future-proofs for any polyhedron. See ARCHITECTURE §"Topology vs Geometry". |
| Truth Pack location | **`/TruthPack/` standalone .NET 8 project** at the repo root, outside Assets/ | No Unity domain reload while testing core logic. Iterate fast. |
| Multiplayer | **Deferred** until SP ships | Don't touch Netcode until Phase 9. |

> If you instead want **cube-only model from day 1**, replace every "Cell" with "Cube", "Side" with "Face", and use the cube-locked WAVE_SPEC examples directly. Generalizing later means a model rewrite — accept that tradeoff knowingly.

---

## Repo Layout (target)

```
WhoWhoo3D_2/                          ← new Unity project (empty today)
├── Assets/
│   └── _Project/                     ← all game code here
│       ├── Runtime/
│       │   ├── Core/
│       │   ├── Gameplay/
│       │   ├── Data/
│       │   ├── Input/
│       │   ├── UI/
│       │   ├── Networking/           ← stays empty until Phase 9
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
- [ ] **TextMeshPro** (com.unity.textmeshpro) — import TMP Essentials when prompted
- [ ] **Addressables** (com.unity.addressables) — optional, install if you plan to load maps from disk
- [ ] **Netcode for GameObjects** — DO NOT install yet. Phase 9.

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

### 7.1 State machine

In `_Project/Runtime/Gameplay/Turn/`:

- [ ] `IGamePhase.cs` — `Enter`, `Tick`, `Exit`, `NextPhase`
- [ ] `RoundStartPhase.cs`
- [ ] `BoardWavePhase.cs` — runs `WaveSimulator.Simulate`, hands off to animator, waits for completion
- [ ] `PlayerTurnPhase.cs` — waits for input, applies tile selection
- [ ] `VictoryCheckPhase.cs`
- [ ] `GameController.cs` — owns the phase machine; one MonoBehaviour, scene singleton (NOT static)

### 7.2 Players

In `_Project/Runtime/Gameplay/Player/`:

- [ ] `PlayerState.cs` — id, color, score, alive, inventory list
- [ ] `PlayerRoster.cs` — list of PlayerStates, current turn index, NextAlive() helper

### 7.3 Input

In `_Project/Runtime/Input/`:

- [ ] Create Input Actions asset `_Project/Settings/InputActions.inputactions`
  - [ ] Action map "Game": Click, Pause, Ability1, Ability2, Ability3, Ability4
- [ ] `PlayerInputHandler.cs` — reads actions, raycasts to find clicked `TileView`, publishes `TileClickedEvent`

### 7.4 Wave animator

In `_Project/Runtime/Gameplay/Wave/`:

- [ ] `WaveAnimator.cs` — replays a `WaveResult` visually with tweens (DOTween or just `Mathf.Lerp` in coroutines)
- [ ] On finish, signals `BoardWavePhase` to advance

### 7.5 Test it

- [ ] Manually click tiles, end turn, watch wave fire
- [ ] PlayMode test: scripted scenario — load map, simulate 3 player clicks, assert end state

---

## Stage 8 — Abilities (2-3 days)

**Goal:** a player can press 1-4 to use an ability that affects the board.

### 8.1 Data

In `_Project/Runtime/Data/Abilities/`:

- [ ] `AbilityDefinition.cs` — `[CreateAssetMenu]` ScriptableObject base. Fields: name, icon, cost, cooldown, range, executor type
- [ ] Create concrete child SOs: `PushAbility.asset`, `FreezeAbility.asset`, `CaptureAbility.asset`

### 8.2 Execution

In `_Project/Runtime/Gameplay/Ability/`:

- [ ] `IAbilityExecutor.cs` — `WaveResult Execute(BoardModel board, AbilityContext ctx)`
- [ ] `PushAbilityExecutor.cs`, etc.
- [ ] `AbilitySystem.cs` — looks up executor by type, calls Execute, hands result to WaveAnimator

### 8.3 Tests

- [ ] EditMode: each ability against a known board → assert expected board state
- [ ] PlayMode: click ability key in scene, verify visible change

---

## Stage 9 — UI (3-4 days)

**Goal:** main menu, lobby, HUD, pause, victory.

### 9.1 Scenes

- [ ] `_Project/Scenes/MainMenu.unity` — Start, Load Map, Quit
- [ ] `_Project/Scenes/Lobby.unity` — player slots (1-4), color pick, map picker, start button. Mode flag: SinglePlayer | LocalHotseat | Online (Online disabled until Phase 11)
- [ ] HUD added to `Game.unity` — current player indicator, score per player, ability bar

### 9.2 UI scripts

In `_Project/Runtime/UI/`:

- [ ] One MonoBehaviour per panel
- [ ] Subscribe to `EventBus` events (`TurnEndedEvent`, `TileCapturedEvent`, etc.) — UI is the right place for the bus
- [ ] Pause overlay listens to `PauseAction` from Input System

### 9.3 SceneFlow

In `_Project/Runtime/Core/SceneFlow/`:

- [ ] `SceneLoader.cs` — fade out → load async → fade in. Always loads; do not gate on network state. Networking is a separate concern.

---

## Stage 10 — Map Editor (3-4 days)

**Goal:** in-engine tool that places cubes, links neighbors, saves v1 JSON.

### 10.1 Editor scene

- [ ] `_Project/Scenes/MapEditor.unity`
- [ ] Free camera (orbit + pan + zoom)
- [ ] UI: place cube, delete cube, link/unlink sides, save, load

### 10.2 Editor scripts

In `_Project/Runtime/MapEditor/`:

- [ ] `MapEditorController.cs` — main coordinator
- [ ] `CubePlacer.cs` — raycast-to-grid placement
- [ ] `LinkTool.cs` — click side A then side B → link them
- [ ] `MapSaver.cs` — uses `MapSerializer` with two distinct paths:
  - **Bundled / default maps:** `Application.streamingAssetsPath/Maps/` — read-only in built games. Ship here.
  - **User-created maps:** `Application.persistentDataPath/Maps/` — writable on all platforms. Save here.
  - In the editor, both paths are writable; in a built game, only `persistentDataPath` is. Never assume `StreamingAssets` is writable at runtime.

### 10.3 Pitfalls to avoid

- [ ] **Use a monotonic counter or GUID** for cell IDs — never list count, deletes break it.
- [ ] **Copy list before removing in a loop** — never mutate a list while iterating it.
- [ ] **Validate before save** — run `BoardModel.Validate()` and refuse save on failure.

---

## Stage 11 — Polish & Ship Singleplayer (1 week)

- [ ] Audio hooks via EventBus (capture sound, wave whoosh, victory fanfare)
- [ ] Particle FX for waves and abilities
- [ ] Tutorial / first-time UX overlay
- [ ] Build pipeline: PC standalone build that runs from a fresh machine
- [ ] Playtest with 2-4 humans, log feedback
- [ ] Tag: `git tag v1.0-singleplayer`

---

## Stage 12 — Multiplayer (2-3 weeks, optional)

Command-based, not state-sync. See ARCHITECTURE §6.

- [ ] Install Netcode for GameObjects
- [ ] Define `MoveCommand`, `UseAbilityCommand`, `EndTurnCommand`
- [ ] Define result events: `TileCapturedEvent`, `WaveResolvedEvent`, `TurnEndedEvent`
- [ ] Authoritative host: `GameController` runs only on host
- [ ] Validate commands on host, broadcast result events to clients
- [ ] Clients re-apply deterministic transitions
- [ ] ParrelSync test
- [ ] Reconnect / disconnect handling
- [ ] Lobby → matchmaking

---

## Default Assets to Create

Build these fresh in `WhoWhoo3D_2/Assets/_Project/`:

- [ ] **Cube prefab** — `Prefabs/Board/Cube.prefab`. MeshRenderer + MeshFilter only (no MonoBehaviours from your code; views attach at runtime).
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

---

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
- `abilities-v1` — end of Stage 8
- `ui-v1` — end of Stage 9
- `editor-v1` — end of Stage 10
- `v1.0-singleplayer` — end of Stage 11

If you go more than 2 days without a green test run + commit, stop and investigate why.
