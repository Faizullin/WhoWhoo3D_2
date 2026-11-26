# WhoWhoo3D — Architecture

> Design reference for the project at `WhoWhoo3D_2/`. Companion docs:
> - [BUILD_GUIDE.md](BUILD_GUIDE.md) — concrete stage-by-stage build manual
> - [TODO.md](TODO.md) — deferred work (networking and abilities)
> - [WAVE_SPEC.md](WAVE_SPEC.md) — gameplay rules
> - [MAP_SCHEMA.md](MAP_SCHEMA.md) — data contract

---

## Design Principles

1. **Feature folders, not type folders** — group code by `Board`, `Player`, `Ability`, not by `Managers`, `Scripts`.
2. **Assembly Definitions** — fast compile + enforced module boundaries.
3. **ScriptableObjects for content data** — abilities, tile types, configs as assets so designers can author without code.
4. **Event Bus is notification glue** — not the spine. Core gameplay holds explicit references.
5. **One controller per concern** — `GameController` owns the match, and `BoardView` renders it.
6. **No commented-out code** — git is the history.
7. **Pure C# first, Unity second** — anything that *can* be tested without a scene running, *should* be.
8. **Topology, not geometry** — the model speaks in `Cell` / `Side` / `Slot` graph terms and
   integer indices. Face directions and slot positions live only in `CubeGeometry`.
   *Known limit:* `Side.MaxSlots = 4` is hard-coded, so a face may have at most four edges.
   Cube clusters of any shape work; a triangular or pentagonal face does not. Lift this
   before attempting non-cube cells.

---

## Runtime boundaries

Unity 6000.4.5f1 uses feature folders under `Assets/_Project/Runtime`. Each top-level module below has its own assembly definition.

| Module | Current responsibility |
| --- | --- |
| Domain | Board topology (`Cell`, `Side`, `Slot`), map serialization, deterministic wave simulation, and the `Match/ClaimSlotCommand` value type. The assembly sets `noEngineReferences`. |
| Gameplay | Match setup and orchestration in `Game/`, roster in `Player/` and phases in `Turn/`. Board views, wave playback, map loading and history also live here. |
| Input | Resolves a pointer to a slot and submits a local action to the controller. |
| UI | Match setup, scoreboard, results and debug timeline. |
| MapEditor | Map authoring and its HUD. The editor builds boards using the same `BoardBuilder` as the game tests. |

`Core`, `Data` and `Utils` are assembly scaffolds. `Networking/` is reserved for the future transport. Unity scene builders live under `_Project/Editor`. Rule tests and the saved-scene match check live under `_Project/Tests/EditMode` and `PlayMode`.

## Code conventions

- Group files by feature and match their namespaces to the module and folder. Use the existing `CellId`, `SideId`, `SlotId` and `PlayerId` types at boundaries.
- Use explicit imports and one public type per file. Pure C# types use file-scoped namespaces. Use block-scoped namespaces for scene-attached MonoBehaviours so Unity resolves their script assets consistently.
- Keep a script's `.meta` file when moving it. Apply scene wiring changes to the saved scene and `GameSceneBuilder` together.
- Keep domain logic free of Unity types. Use direct calls for ordered gameplay transitions and deterministic iteration for wave resolution.

## Match setup and claims

`MatchSetup` stores the map and an immutable ordered list of seat controllers. Seat one belongs to P1. `StartMatch(setup)` creates the board and roster. Restart reloads the map using the same setup. Serialized scene settings supply defaults for scenes and the local setup screen.

Pointer input checks the active seat before calling `GameController.TrySubmitClaim` with a `ClaimSlotCommand` containing the actor, expected turn number and slot. The controller checks the phase, pause and review state, actor, turn and legality before changing ownership. It records the accepted claim and advances into wave resolution. A replayed command from a completed turn leaves the board and history intact.

Commands currently belong to a single running match on the Unity main thread. A future network adapter will bind each connection to a seat and match instance before forwarding a command.

## Key systems

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

### 2. Ability system (planned)

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
(`WavePlaybackPlan.InitialState`, `SlotView.ShowOwner`) and walks them forward one chain
step at a time, then hands display back to the model. Skipping or interrupting an
animation can therefore never desync the board. The timeline itself lives in
`WavePlaybackPlan` — pure C#, no `UnityEngine` — so it is unit-tested rather than watched.

### 4. Event bus (planned)

The proposed bus would handle **notifications** that cross subsystem boundaries: "turn ended", "tile captured", "player eliminated". Camera, UI, audio, analytics subscribe.

```csharp
EventBus.Publish(new TurnEndedEvent(playerId));
EventBus.Subscribe<TurnEndedEvent>(handler);
```

**Do not** route core gameplay through the bus. `TurnController`, `BoardController`, and `WaveSimulator` should hold explicit references (or be DI-injected). A pub/sub bus for the hot path makes flow invisible and ordering bugs likely.

Rule of thumb: if subscriber order or completion matters, use a direct call. If it's "fire and forget" listeners reacting to a fact, use the bus.

### 5. Single GameController

`GameController` owns the phase machine and accepts match commands for all seats.

### 6. Next milestones

The Edit Mode replay test runs the demo on two independent match instances. It compares ordered changes, ownership, roster and winner after each accepted claim and retries every delivery.

1. Bind each connection to a seat, deliver ordered host results and allow presentation to lag behind the authoritative turn.
2. Add transport-backed host/join, seat assignment and reconnect from a host snapshot. The host validates commands using the same claim rules.

The latest recorded hot-seat run and its verification results are in the [gameplay report](reports/README.md).
