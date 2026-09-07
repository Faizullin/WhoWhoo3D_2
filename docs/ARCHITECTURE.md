# WhoWhoo3D — Architecture

> Design reference for the project at `WhoWhoo3D_2/`. Companion docs:
> - [BUILD_GUIDE.md](BUILD_GUIDE.md) — concrete stage-by-stage build manual
> - [TODO.md](TODO.md) — deferred work (AI, networking, abilities)
> - [WAVE_SPEC.md](WAVE_SPEC.md) — gameplay rules
> - [MAP_SCHEMA.md](MAP_SCHEMA.md) — data contract

---

## Design Principles

1. **Feature folders, not type folders** — group code by `Board`, `Player`, `Ability`, not by `Managers`, `Scripts`.
2. **Assembly Definitions** — fast compile + enforced module boundaries.
3. **ScriptableObjects for content data** — abilities, tile types, configs as assets so designers can author without code.
4. **Event Bus is notification glue** — not the spine. Core gameplay holds explicit references.
5. **One controller per concern** — one `GameController`, one `BoardController`. No parallel near-duplicates.
6. **No commented-out code** — git is the history.
7. **Pure C# first, Unity second** — anything that *can* be tested without a scene running, *should* be.
8. **Topology, not geometry** — the model speaks in `Cell` / `Side` / `Slot` graph terms and
   integer indices. Face directions and slot positions live only in `CubeGeometry`.
   *Known limit:* `Side.MaxSlots = 4` is hard-coded, so a face may have at most four edges.
   Cube clusters of any shape work; a triangular or pentagonal face does not. Lift this
   before attempting non-cube cells.

---

## Folder Structure

```
Assets/
├── StreamingAssets/                  ← bundled default maps
│   └── Maps/
│       └── sample.json
└── _Project/
    ├── Runtime/
    │   ├── Domain/                   ← pure C#, no Unity refs
    │   │   ├── Board/                ← BoardModel (with .Clone()), Cell, Side, TileStack
    │   │   ├── Wave/                 ← WaveSimulator, WaveConfig
    │   │   └── Maps/                 ← MapSerializer, MapDtos
    │   ├── Core/
    │   │   ├── Events/               ← EventBus
    │   │   ├── Services/             ← service locator / DI seam
    │   │   └── SceneFlow/            ← scene loader + fade
    │   ├── Gameplay/
    │   │   ├── Board/                ← BoardActions, CubeGeometry; View/ → BoardView, CellView, SlotView
    │   │   ├── Wave/                 ← WavePlaybackPlan (pure), WaveAnimator
    │   │   ├── Player/               ← PlayerState, PlayerRoster
    │   │   ├── Ai/                   ← GreedyAiPlayer (deferred, see TODO.md)
    │   │   ├── Ability/              ← AbilitySystem
    │   │   ├── Turn/                 ← IGamePhase + the four phases
    │   │   └── Game/                 ← GameController
    │   ├── Data/                     ← ScriptableObject definitions
    │   │   ├── Abilities/
    │   │   ├── Maps/
    │   │   └── GameConfig/
    │   ├── Input/                    ← Input System actions + handlers
    │   ├── UI/
    │   │   ├── HUD/
    │   │   ├── Menu/
    │   │   ├── Inventory/
    │   │   └── Common/
    │   ├── Networking/               ← Stage 13 only (deferred)
    │   ├── MapEditor/
    │   └── Utils/
    ├── Editor/
    ├── Tests/
    │   ├── EditMode/
    │   └── PlayMode/
    ├── Art/
    ├── Audio/
    ├── Prefabs/
    ├── Scenes/
    └── Settings/
```

`_Project/` is optional — flat `Assets/Scripts/...` is also fine. See BUILD_GUIDE for the discussion.

---

## Assembly Definitions

Target Unity version: **Unity 6 LTS** (Universal 3D / URP). Project at `WhoWhoo3D_2/` is on `6000.4.5f1`.

| Asmdef | Deps | Notes |
|---|---|---|
| `WhoWhoo3D.Domain` | (none) | Pure C#, no `UnityEngine`. `BoardModel`, `WaveSimulator`, `MapSerializer` live here. |
| `WhoWhoo3D.Core` | (none) | EventBus, services, scene flow. **Scaffold only — no sources yet.** |
| `WhoWhoo3D.Data` | Core | ScriptableObjects, configs. **Scaffold only — no sources yet.** |
| `WhoWhoo3D.Gameplay` | Domain, Core, Data | Views, controllers, phases, MonoBehaviours. |
| `WhoWhoo3D.Input` | Core, Gameplay, Unity.InputSystem | |
| `WhoWhoo3D.UI` | Core, Gameplay, Data, Unity.TextMeshPro | |
| `WhoWhoo3D.MapEditor` | Domain, Core, Data | |
| `WhoWhoo3D.Utils` | (none) | Math, helpers. |
| `WhoWhoo3D.Tests.EditMode` | all runtime | |
| `WhoWhoo3D.Tests.PlayMode` | all runtime | |

`Core`, `Data`, `Utils`, `UI` and `MapEditor` currently have an `.asmdef` but no `.cs`
files. Unity builds them as empty assemblies. Keep them only as long as the stage that
fills them is near; an assembly with no sources is import cost with no boundary value.

**Why `Domain` is separate:** `BoardModel`, `WaveSimulator`, and `MapSerializer` all live there. They return `BoardModel` objects, so they must all be in the same or upstream assembly. If serializers were in `Data` and models in `Gameplay`, `Data` would need to reference `Gameplay` while `Gameplay` already references `Data` — a compile cycle. `Domain` breaks the cycle: zero dependencies, everything else depends on it.

---

## Code Conventions

Kept deliberately small, and enforced by review rather than tooling:

- **File-scoped namespaces** everywhere (`namespace X;`). C# 10, so **every assembly needs a
  `csc.rsp` containing `-langversion:latest`** — Unity 6 defaults to C# 9 and the failure is
  a confusing parse error, not a clear message. Every asmdef has a sibling `csc.rsp`.
- **Explicit `using` statements per file.** No `GlobalUsings.cs`.
- **One public type per file**, named after the file.
- **XML docs on anything non-obvious**, explaining *why* rather than restating the signature.
- **No commented-out code.** Git remembers.
- **`Domain` never references `UnityEngine`.** Enforced by `noEngineReferences: true`.
- **Pure C# where possible**, even inside Gameplay: `BoardActions`, `GreedyAiPlayer`,
  `WavePlaybackPlan`, `GameHistory` and `CubeGeometry` are all plain classes, which is why
  they are unit-tested rather than played through.

---

## Key Systems

### 1. Phase / Turn State Machine

Each phase is a class implementing
`IGamePhase { Enter(); Tick(float deltaTime); Exit(); bool IsComplete; IGamePhase NextPhase(); }`.

```
RoundStartPhase → PlayerTurnPhase → BoardWavePhase → VictoryCheckPhase
                        ↑                                    │
                        └───────── next living player ───────┘
                                                             │
                                          one player left ───┴──→ NextPhase() == null
```

The player acts *before* the wave resolves: reinforcing a side is what pushes it to
capacity and sets the chain off.

Each phase names its own successor, so `GameController` holds no `switch` over phase
types — it just drains whatever is already complete. Two constraints keep this testable:

- `Tick` receives `deltaTime` instead of reading `UnityEngine.Time`, so Edit Mode tests
  can drive time without a frame loop.
- Completion is **pushed**: an input event advances the machine in the same call rather
  than waiting for the next `Update()`. Polling-only transitions make every turn
  untestable outside Play Mode.

`AdvanceWhileComplete` is iterative and capped (`MaxPhaseChain`) so a pair of phases that
complete each other cannot freeze the editor.

### 2. Ability System

`AbilityDefinition` ScriptableObject + `IAbilityExecutor`.

```
AbilityDefinition (SO):  cost, cooldown, range, icon, executor type
PlayerInventory:         List<AbilityInstance>
AbilitySystem.Execute(player, ability, target)
```

Designers create abilities as assets without code changes.

### 3. Board / Wave System

```
BoardModel       — pure data: cells, sides, edge-paired tile slots (Domain)
BoardView        — Unity GameObjects, visualization      (Gameplay)
GameController   — orchestrates Model ↔ View, owns phases  (Gameplay)
WaveSimulator    — pure logic, returns WaveResult         (Domain)
WaveAnimator     — replays WaveResult visually            (Gameplay)
```

Model is testable without a Unity scene running. The simulator is graph-based and
shape-agnostic — see [WAVE_SPEC.md](WAVE_SPEC.md).

**The unit of ownership is a slot, not a face.** A slot is a tile at the midpoint of one
edge of an *exposed* face, and every slot is paired with the slot across that edge — around
a cube's own corner, or across the seam onto a neighbouring cube. Faces pressed together are
*interior*: buried, unplayable, and forbidden from holding slots. The exposed faces of a
cluster therefore form one continuous surface, and that surface is the entire playing field.

A face expands when every slot it has is claimed by one player — capacity is per-face, so a
two-slot face fires on two. Expanding claims each slot's partner and releases the source
face to white.

Only `CubeGeometry` and the map author know a cell is drawn as a cube. The Domain speaks in
side and edge indices, per principle 8.

`WaveAnimator` is presentation only. The simulator has already written the final state by
the time playback starts, so the animator rewinds the *views* to the pre-wave state
(`WavePlaybackPlan.InitialState`, `SideView.ShowStack`) and walks them forward one chain
step at a time, then hands display back to the model. Skipping or interrupting an
animation can therefore never desync the board. The timeline itself lives in
`WavePlaybackPlan` — pure C#, no `UnityEngine` — so it is unit-tested rather than watched.

### 4. Event Bus — notification glue, not the spine

Used for **notifications** that cross subsystem boundaries: "turn ended", "tile captured", "player eliminated". Camera, UI, audio, analytics subscribe.

```csharp
EventBus.Publish(new TurnEndedEvent(playerId));
EventBus.Subscribe<TurnEndedEvent>(handler);
```

**Do not** route core gameplay through the bus. `TurnController`, `BoardController`, and `WaveSimulator` should hold explicit references (or be DI-injected). A pub/sub bus for the hot path makes flow invisible and ordering bugs likely.

Rule of thumb: if subscriber order or completion matters, use a direct call. If it's "fire and forget" listeners reacting to a fact, use the bus.

### 5. Single GameController

One file. Owns the phase machine. The only top-level orchestrator.

### 6. AI Opponent — deferred

`GreedyAiPlayer` is built and tested but **off by default**: the current goal is a local
player-versus-player game. See [TODO.md](TODO.md) §1.

Two properties are worth preserving whenever it is picked back up:

- **It shares `BoardActions` with the human path**, so a move it simulates is exactly the
  move `GameController` executes.
- **It has no `UnityEngine` reference**, so move selection is unit-tested rather than
  watched.

### 7. Multiplayer### 7. Multiplayer — command-based, not state-sync

**Deferred until the local game ships — see [TODO.md](TODO.md) §2.** The design is settled;
recording it here so it is not re-litigated:

- Client sends **commands** (intents): `MoveCommand`, `UseAbilityCommand`, `EndTurnCommand`.
- Host runs the authoritative `GameController`, validates commands, mutates the model, and publishes **result events** (`TileCapturedEvent`, `WaveResolvedEvent`).
- Clients re-apply the same deterministic transitions on result events, OR receive deltas only.

Dramatically less bandwidth than syncing every cell as a `NetworkVariable`, and no risk of desync if the simulator is deterministic (which it is — no randomness).

---

## Estimated Effort

- **Singleplayer + map editor MVP:** ~6-8 weeks solo focused.
- **Multiplayer addition:** +2-3 weeks.
