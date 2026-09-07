# TODO — Deferred Work

> Everything here is **out of scope for the current goal**. The primary goal is a simple
> local, player-versus-player cube game: click a tile, faces expand, last player standing
> wins. Nothing in this file is needed for that.
>
> Items are here because they are *deferred*, not because they are unwanted. Some are
> already built — see the status column. Do not start any of these while the roadmap in
> [BUILD_GUIDE.md](BUILD_GUIDE.md) still has open stages.

---

## 1. AI Opponent

**Status: BUILT, DISABLED BY DEFAULT.** `GameController.enableAi` ships `false`, so every
player is human. The code is tested and works; it is simply not the current focus.

| Piece | Where | State |
|---|---|---|
| `GreedyAiPlayer` | `Runtime/Gameplay/Ai/` | One-ply search over `board.Clone()`, scores `(own gain) − (opponents' gain)`, ties to lowest slot id. Deterministic. |
| `PlayerState.IsAi` | `Runtime/Gameplay/Player/` | Flag the roster reads. |
| `GameController.ConfigureAi(...)` | `Runtime/Gameplay/Game/` | Runtime switch; used by tests. |
| `PlayerTurnPhase` AI branch | `Runtime/Gameplay/Turn/` | Plays itself after `aiThinkDelay` seconds. |
| `GreedyAiPlayerTests` | `Tests/EditMode/Ai/` | 8 tests. |

**To re-enable:** tick `Enable Ai` on the `Game` object and set `Human Player Id`.

### Future AI work (none of it started)

- [ ] Look ahead more than one ply — the current AI cannot see a chain reaction coming.
- [ ] Difficulty levels. One-ply greedy is the "easy" tier.
- [ ] Weight *reach* as well as tile count. Expansion trades tiles for footholds, so raw
      score is a poor proxy for position.
- [ ] Personality / opening preferences so repeat games differ.
- [ ] Move-explanation debug overlay (`GreedyAiPlayer.EvaluateAll` already returns the
      scored list needed for it).

---

## 2. Networking / Online Multiplayer

**Status: NOT STARTED. Deferred until the local game ships.**

The design is already settled and should not be revisited before then:

- **Command-based, not state-sync.** Clients send intents (`ClaimSlotCommand`,
  `EndTurnCommand`); the host runs the authoritative `GameController`, validates, mutates,
  and publishes result events.
- **The simulator is deterministic** — `WaveSimulator` orders faces by `(cellId, sideIndex)`
  and has no randomness — so clients can re-apply the same transitions without desync.
  Keep it that way; a single unordered iteration would break this silently.

### Work items

- [ ] `WhoWhoo3D.Networking` assembly (folder exists, no sources).
- [ ] Command types + validation on the host.
- [ ] Result-event replication and client-side re-application.
- [ ] Lobby, matchmaking, reconnect.
- [ ] Determinism regression test: run the same command sequence on two boards, assert
      identical `WaveResult` change lists.

**Do not touch Netcode until the local game is shipped.**

---

## 3. Abilities System

**Status: NOT STARTED.** Scoped out of "basic game logic".

- [ ] `AbilityDefinition` ScriptableObject: cost, cooldown, range, icon, executor type.
- [ ] `IAbilityExecutor` + `AbilitySystem.Execute(player, ability, target)`.
- [ ] `PlayerInventory` on `PlayerState`.
- [ ] Ability bar in the HUD.
- [ ] `WhoWhoo3D.Data` assembly gets its first real sources here.

---

## 4. Deferred Infrastructure

- [ ] **Retire or fix TruthPack.** It currently does **not compile** — all 8 source files
      still use `TileStack` / `SideStateChange`, deleted in the slot-model rewrite. Nothing
      on the dev machine builds it (`dotnet` is not installed), which is how it rotted twice.
      Recommendation: delete it; the Unity Edit Mode suite covers the same ground.
- [ ] **Delete the empty assemblies** `Data`, `Utils`, `MapEditor`, `Networking` until the
      stage that fills them arrives. Each is an asmdef with zero `.cs` files.
- [ ] **`GameConfig` ScriptableObject** for the 13 tuning values currently typed into the
      inspector across four components.
- [ ] **Editor auto-wire** (`Reset`/`OnValidate`) so scene references fill themselves.
- [ ] **PlayMode test suite** — the asmdef exists, no tests in it.
- [ ] Replace `ScoreboardView` (IMGUI stopgap) with proper uGUI/TextMeshPro in Stage 9.
- [ ] Audio, VFX, camera polish.
- [ ] Non-cube cell shapes. The Domain is already shape-agnostic; only `CubeGeometry` and
      the map generator assume six faces.

---

## 5. Ideas Not Committed To

- Larger clusters and irregular shapes (the surface walk already supports any cube cluster).
- Sparse tile networks — maps where not every edge carries a tile pair. Supported by the
  model and validator today, but no map uses it and the gameplay effect is unexplored.
- Tuning `ConquestTakesFace = false` (only the targeted slot flips, so contested faces
  become reachable). Needs a majority-owner click rule implemented first, or the board can
  stall.
