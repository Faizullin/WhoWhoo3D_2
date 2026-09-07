# TODO — Deferred Work

> Everything here is **out of scope for the current goal**. The primary goal is a simple
> local, player-versus-player cube game: click a tile, faces expand, last player standing
> wins. Nothing in this file is needed for that.
>
> Items are here because they are *deferred*, not because they are unwanted. Some are
> already built — see the status column.
>
> ## Ordering rule
>
> **Nothing in this file starts until the singleplayer core is finished and tested.**
>
> AI, multiplayer and abilities are all *extensions* of the core loop. Building any of them
> against a core that is still changing means designing the extension around rules that will
> move — and the seams end up in the wrong places. The slot-model rewrite already proved the
> cost: it invalidated the AI's scoring, the map schema, every view, and roughly sixty tests
> in one go. That is cheap while the surface is small and expensive once three subsystems
> hang off it.
>
> Core first means: the game is playable start to finish, the rules are settled, the UI
> communicates what is happening, and the Edit Mode suite passes in Unity. Then extend.

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

## 4. Scene Architecture

**Status: PARTIAL.** `Demo4x4` is built (see below); the real scenes are not.

Scenes are wiring, and wiring done by hand drifts — a missing reference or an unticked box
costs a confusing play session to find. `Editor/GameSceneBuilder.cs` generates a scene from a
`SceneSpec`, so every scene is assembled identically and the setup is reviewable as a diff.

| Scene | State |
|---|---|
| `Game.unity` | The original hand-wired scene. **Leave it alone** — it is the working reference. |
| `Demo4x4.unity` | Built by `Tools ▸ WhoWhoo3D ▸ Build Demo4x4 Scene`. 16 cubes, 3 players, top faces only. |
| `SingleplayerGame.unity` | **To do.** Same builder, plus map selection and player-count setup. |
| `MultiplayerGame.unity` | **To do.** Same builder again; only the session layer differs. |

- [ ] Extend `SceneSpec` with map selection and a start screen rather than fixed values.
- [ ] `SingleplayerGame` scene once Stage 9 (HUD, legibility, victory, restart) is done.
- [ ] `MultiplayerGame` scene — after §2, not before.
- [ ] Retire `Game.unity` once `SingleplayerGame` supersedes it.

---

## 5. UI and Mesh Rendering Design

**Status: NOT STARTED. Blocks nothing, but should land before §1–§3.**

The current visuals are functional placeholders: primitive cubes, code-spawned slab tiles,
and two IMGUI panels. That is enough to debug rules and no more. Interaction is the weaker
half — you cannot tell what is clickable without reading WAVE_SPEC.

### Mesh / visual
- [ ] Replace `GameObject.CreatePrimitive` tiles with an authored mesh or prefab. Tiles are
      spawned per slot (288 of them on Demo4x4), so this is a batching question too.
- [ ] A real cube prefab with proper materials, edge bevels, and a readable top surface.
- [ ] Distinguish **face state** visually: free, held, one-tile-from-full, locked by an enemy.
      Right now a full face and a nearly-full face look identical.
- [ ] Buildable/placeable item visuals — the piece the future map editor and abilities need.
- [ ] Camera: orbit, zoom and framing that work for a 9×9 slab, not just two cubes.

### UI interaction
- [ ] Replace `ScoreboardView` and `TimelineView` IMGUI with uGUI/TextMeshPro.
- [ ] Hover and selection feedback on tiles — the single biggest legibility gap.
- [ ] Click affordances: highlight legal slots for the active player, dim locked faces.
- [ ] Turn hand-off screen for hot-seat play, so players know the turn changed.
- [ ] Wave playback controls surfaced properly rather than as a debug bar.

**Why before §1–§3:** an AI is only debuggable if you can see what it did, multiplayer needs
the turn hand-off and state readability that do not exist yet, and abilities need a target
selection UI. Each of the three would otherwise ship its own ad-hoc UI, and they would not
match.

---

## 6. Deferred Infrastructure

- [ ] **Retire or fix TruthPack.** It currently does **not compile** — all 8 source files
      still use `TileStack` / `SideStateChange`, deleted in the slot-model rewrite. Nothing
      on the dev machine builds it (`dotnet` is not installed), which is how it rotted twice.
      Recommendation: delete it; the Unity Edit Mode suite covers the same ground.
- [ ] **Delete the empty assemblies** `Data`, `Utils`, `MapEditor` and `Tests.PlayMode`
      until the stage that fills them arrives. Each is an asmdef with zero `.cs` files.
      (`Core` gets sources at Stage 9's EventBus, so leave that one.) They are referenced by
      GUID from `Gameplay`, `Input`, `UI` and `Editor`, so removing one means editing those
      reference lists too — do it in one pass, not piecemeal.
- [ ] **Split `GameController`** when it next grows. It is ~470 lines across five clearly
      marked sections (init, phase machine, player actions, presentation state, history).
      That is still navigable, but the history/timeline block is the cohesive piece to lift
      out first, and multiplayer will want a command seam in front of `TryPlayerAction`.
- [ ] **Lift `Side.MaxSlots = 4`** if non-cube cells are ever wanted. See ARCHITECTURE
      principle 8 — the Domain is index-based but assumes four edges per face.
- [ ] **`GameConfig` ScriptableObject** for the 13 tuning values currently typed into the
      inspector across four components.
- [ ] **Editor auto-wire** (`Reset`/`OnValidate`) so scene references fill themselves.
- [ ] **PlayMode test suite** — the asmdef exists, no tests in it.
- [ ] Audio, VFX polish.
- [ ] Non-cube cell shapes. The Domain is already shape-agnostic; only `CubeGeometry` and
      the map generator assume six faces.

---

## 7. Ideas Not Committed To

- Larger clusters and irregular shapes (the surface walk already supports any cube cluster).
- Sparse tile networks — maps where not every edge carries a tile pair. Supported by the
  model and validator today, but no map uses it and the gameplay effect is unexplored.
- Tuning `ConquestTakesFace = false` (only the targeted slot flips, so contested faces
  become reachable). Needs a majority-owner click rule implemented first, or the board can
  stall.
