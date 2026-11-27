# WhoWhoo3D — Architecture

> Design reference for the project at `WhoWhoo3D_2/`. Companion docs:
> - [BUILD_GUIDE.md](BUILD_GUIDE.md) — concrete stage-by-stage build manual
> - [TODO.md](TODO.md) — remaining work
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
| Input | Fits the local camera to the loaded board, handles orbit/pan/zoom and resolves a pointer to a slot. Both views of a LAN match can navigate independently. |
| UI | Match setup, LAN room controls, scoreboard, results and local debug timeline. |
| Networking | TCP connections, room membership, seat assignment and ordered delivery of accepted claims. Depends on Gameplay and Domain. |
| MapEditor | Map authoring and its HUD. The editor builds boards using the same `BoardBuilder` as the game tests. |

`Core`, `Data` and `Utils` are assembly scaffolds. Unity scene builders live under `_Project/Editor`. Rule tests and the saved-scene match check live under `_Project/Tests/EditMode` and `PlayMode`.

The scene's `ScoreboardView` binds the local session and owns the screen lifecycle. It constructs one `MatchHud` for all maps and seat modes. Live controls call the existing controller commands. The canvas checks its header and tray rectangles through `IBoardInputPanel`, leaving the space between them available for board selection and camera gestures. Setup, results and the debug timeline use IMGUI. Opening the timeline gives it the lower tray’s screen space.

## Code conventions

- Group files by feature and match their namespaces to the module and folder. Use the existing `CellId`, `SideId`, `SlotId` and `PlayerId` types at boundaries.
- Use explicit imports and one public type per file. Pure C# types use file-scoped namespaces. Use block-scoped namespaces for scene-attached MonoBehaviours so Unity resolves their script assets consistently.
- Keep a script's `.meta` file when moving it. Apply scene wiring changes to the saved scene and `GameSceneBuilder` together.
- Keep domain logic free of Unity types. Use direct calls for ordered gameplay transitions and deterministic iteration for wave resolution.

## Match setup and claims

`MatchSetup` stores the map and an immutable ordered list of seat controllers. Seat one belongs to P1. `StartMatch(setup)` creates the board and roster. Restart reloads the map using the same setup. Serialized scene settings supply defaults for scenes and the local setup screen.

Pointer input checks the active seat before calling `GameController.TrySubmitClaim` with a `ClaimSlotCommand` containing the actor, expected turn number, action kind and target. Beacon also carries the second edge. Supply carries the chosen card. The controller checks the phase, pause and review state, actor, turn and legality before changing ownership. It records the accepted claim and advances into wave resolution. A replayed command from a completed turn leaves the board and history intact.

### LAN host and guest

`LanMatchSession` owns the listening socket and room on the host. P1 plays on the same computer. Each guest connects over IPv4 and receives one seat. Before play starts, each guest checks the protocol version and the digest of its installed map. TCP frames carry length-prefixed JSON. Socket processing and rule changes run on the Unity main thread.

While the initial lobby has open seats, `LanRoomAdvertiser` sends one announcement per second on UDP port `47772`. Each announcement includes an ID for this room instance, the protocol version, the room name, map, free and total seats, and the actual TCP port. It goes to the local IPv4 broadcast addresses and loopback. Pending seat assignments count toward capacity. Rooms waiting for a disconnected player reserve that seat.

`LanRoomBrowser` listens while **Join LAN** is open. It checks the protocol, reads the host address from the datagram source, and lists each room ID once even when several interfaces hear it. A listing expires after 3.5 seconds without an announcement. Selecting a listing sends its room ID in the TCP handshake so a replacement host on the same port cannot take that join. Both joining paths use `LanMatchSession.Join` and its seat and map checks.

The host runs a separate `GameController` without a view or wave animator. A move must match its connection’s seat, the current round, the active player, the expected turn and a legal slot. The host assigns a sequence number to each accepted claim. Every visible board, including P1’s, processes the same claims in order and can finish its own animations between moves. Offline modes call the same rules directly.

When a guest drops during play, the room pauses. The running guest keeps a private seat token in memory. Once all seats reconnect, the host sends a settled snapshot with ownership, active seat, turn, seat inventories, pending effects, elimination state, skipped turns and result. Each view rebuilds from the agreed map and resumes at the host’s sequence number. Starting a rematch increments the round and resets the sequence. The room lasts for the lifetime of the host process. This transport is for a trusted local network.

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

### 2. Ability inventory

`Gameplay/Abilities/MatchAbilities` shares target legality across the pointer, inventory, computer seats and authoritative turn commands. `AbilityInventory` holds one seat’s charges, supply progress, frozen cube and Beacon marks. Each seat starts with one Relay and one Freeze. A Supply command fills one empty card and leaves the current turn phase open. The LAN host chooses the ruleset in setup and sends it to joining seats using protocol 3.

After a turn wave, the victory phase removes expired ice and clears eliminated seats’ Beacons. At the next living seat’s turn boundary, a pending Beacon can start an opening wave. Its victory check normally returns control to the same seat. The ordinary wave resolver always uses the underlying paired edges. A reconnect restores the settled board and inventories, and a rematch rebuilds them from the same setup.

The local timeline displays individual wave steps. Branching during automatic playback steps returns to the decision that produced them, restoring a snapshot of ownership, turn, roster and inventories. See the [turn and ability rules](WAVE_SPEC.md).

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

### 5. Match rules

`GameController` owns the phase machine and accepts match commands for all seats.

### 6. Next milestones

The Edit Mode replay test runs the demo on two independent match instances. It compares ordered changes, ownership, roster and winner after each accepted claim and retries every delivery.

The TCP Play Mode suite checks seat binding, duplicate claims, loss and recovery, a three-player match through victory, and a synchronized rematch. The capture tool also plays a match between two independent desktop processes and compares their final snapshots.

The discovery tests join a room by its advertised port, track changing seat counts, expire filled and closed rooms, and filter incompatible announcements. The standalone report captures the join screen before the guest selects the discovered room.

Separate-interface and blocked-UDP verification runs through the Linux test player. The shared scene now loads the terrace map with a local camera rig. The demo supports Fixed charges and Supply. Upcoming UI work covers setup, LAN rooms and results, followed by board materials and wave animation. See the [LAN verification procedure](reports/LAN.md) and [gameplay roadmap](GAMEPLAY_ROADMAP.md).

The recorded hot-seat run, LAN screenshots and verification results are in the [gameplay report](reports/README.md).
