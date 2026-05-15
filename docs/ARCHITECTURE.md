# WhoWhoo3D — Architecture

> Design reference for the project at `WhoWhoo3D_2/`. Companion docs:
> - [BUILD_GUIDE.md](BUILD_GUIDE.md) — concrete stage-by-stage build manual
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
8. **Topology, not geometry** — model speaks in `Cell` / `Side` / `SideLink` graph terms. Direction names live only in the cube renderer.

---

## Folder Structure

```
Assets/
├── _Project/
│   ├── Runtime/
│   │   ├── Domain/                   ← pure C#, no Unity refs
│   │   │   ├── Board/                ← BoardModel, Cell, Side, TileStack
│   │   │   └── Wave/                 ← WaveSimulator, WaveConfig
│   │   ├── Core/
│   │   │   ├── Events/               ← EventBus
│   │   │   ├── Services/             ← service locator / DI seam
│   │   │   └── SceneFlow/            ← scene loader + fade
│   │   ├── Gameplay/
│   │   │   ├── Board/                ← BoardView, CellView, SideView
│   │   │   ├── Wave/                 ← WaveAnimator
│   │   │   ├── Player/               ← PlayerController, PlayerState
│   │   │   ├── Ability/              ← AbilitySystem
│   │   │   ├── Turn/                 ← TurnController, IGamePhase
│   │   │   └── Game/                 ← GameController
│   │   ├── Data/                     ← ScriptableObject definitions
│   │   │   ├── Abilities/
│   │   │   ├── Maps/
│   │   │   └── GameConfig/
│   │   ├── Input/                    ← Input System actions + handlers
│   │   ├── UI/
│   │   │   ├── HUD/
│   │   │   ├── Menu/
│   │   │   ├── Inventory/
│   │   │   └── Common/
│   │   ├── Networking/               ← Phase 12 only
│   │   ├── MapEditor/
│   │   └── Utils/
│   ├── Editor/
│   ├── Tests/
│   │   ├── EditMode/
│   │   └── PlayMode/
│   ├── Art/
│   ├── Audio/
│   ├── Prefabs/
│   ├── Scenes/
│   └── Settings/
└── StreamingAssets/                  ← bundled default maps
```

`_Project/` is optional — flat `Assets/Scripts/...` is also fine. See BUILD_GUIDE for the discussion.

---

## Assembly Definitions

Target Unity version: **Unity 6 LTS** (Universal 3D / URP). Project at `WhoWhoo3D_2/` is on `6000.4.5f1`.

| Asmdef | Deps | Notes |
|---|---|---|
| `WhoWhoo3D.Domain` | (none) | Pure C#, no `UnityEngine`. `BoardModel`, `WaveSimulator`, `MapSerializer` live here. |
| `WhoWhoo3D.Core` | (none) | EventBus, services, scene flow. |
| `WhoWhoo3D.Data` | Core | ScriptableObjects, configs. |
| `WhoWhoo3D.Gameplay` | Domain, Core, Data | Views, controllers, MonoBehaviours. |
| `WhoWhoo3D.Input` | Core, Gameplay, Unity.InputSystem | |
| `WhoWhoo3D.UI` | Core, Gameplay, Data, Unity.TextMeshPro | |
| `WhoWhoo3D.MapEditor` | Domain, Core, Data | |
| `WhoWhoo3D.Utils` | (none) | Math, helpers. |
| `WhoWhoo3D.Tests.EditMode` | all runtime | |
| `WhoWhoo3D.Tests.PlayMode` | all runtime | |

**Why `Domain` is separate:** `BoardModel`, `WaveSimulator`, and `MapSerializer` all live there. They return `BoardModel` objects, so they must all be in the same or upstream assembly. If serializers were in `Data` and models in `Gameplay`, `Data` would need to reference `Gameplay` while `Gameplay` already references `Data` — a compile cycle. `Domain` breaks the cycle: zero dependencies, everything else depends on it.

---

## Key Systems

### 1. Phase / Turn State Machine

Each phase is a class implementing `IGamePhase { Enter(); Tick(); Exit(); NextPhase; }`.

```
RoundStartPhase → BoardWavePhase → PlayerTurnPhase → VictoryCheckPhase
```

Clean transitions, testable, no giant switch.

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
BoardModel       — pure data: cells, sides, tile stacks, links (Domain)
BoardView        — Unity GameObjects, visualization      (Gameplay)
BoardController  — orchestrates Model ↔ View              (Gameplay)
WaveSimulator    — pure logic, returns WaveResult         (Domain)
WaveAnimator     — replays WaveResult visually            (Gameplay)
```

Model is testable without a Unity scene running. The simulator is graph-based and shape-agnostic — see WAVE_SPEC.

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

### 6. Multiplayer — command-based, not state-sync

When multiplayer is added (Phase 12, deferred):

- Client sends **commands** (intents): `MoveCommand`, `UseAbilityCommand`, `EndTurnCommand`.
- Host runs the authoritative `GameController`, validates commands, mutates the model, and publishes **result events** (`TileCapturedEvent`, `WaveResolvedEvent`).
- Clients re-apply the same deterministic transitions on result events, OR receive deltas only.

Dramatically less bandwidth than syncing every cell as a `NetworkVariable`, and no risk of desync if the simulator is deterministic (which it is — no randomness).

---

## Estimated Effort

- **Singleplayer + map editor MVP:** ~6-8 weeks solo focused.
- **Multiplayer addition:** +2-3 weeks.
