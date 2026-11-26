# Gameplay backlog

The [architecture milestones](ARCHITECTURE.md) describe the order of implementation. The [gameplay report](reports/README.md) covers the local match.

## 1. Networking / Online Multiplayer

The local controller validates turn-addressed claims. An Edit Mode regression now replays a complete demo match on two independent boards and checks the outcome after every delivery. The next step is a host session that binds the sender to a seat and publishes each accepted turn in order.

### Work items

- [ ] `WhoWhoo3D.Networking` assembly (folder exists, no sources).
- [ ] Bind the sender to its assigned seat before forwarding a claim to the host controller.
- [ ] Result-event replication and client-side re-application.
- [ ] Lobby, matchmaking, reconnect.
- [x] Replay accepted claims on independent boards and compare ordered wave changes, ownership, elimination, turn and winner after each claim.

---

## 2. Abilities System

**Status: NOT STARTED.** Scoped out of "basic game logic".

- [ ] `AbilityDefinition` ScriptableObject: cost, cooldown, range, icon, executor type.
- [ ] `IAbilityExecutor` + `AbilitySystem.Execute(player, ability, target)`.
- [ ] `PlayerInventory` on `PlayerState`.
- [ ] Ability bar in the HUD.
- [ ] `WhoWhoo3D.Data` assembly gets its first real sources here.

---

## 3. Scene Architecture

**Status: PARTIAL.** `Demo4x4` is built (see below); the real scenes are not.

Scenes are wiring, and wiring done by hand drifts — a missing reference or an unticked box
costs a confusing play session to find. `Editor/GameSceneBuilder.cs` generates a scene from a
`SceneSpec`, so every scene is assembled identically and the setup is reviewable as a diff.

| Scene | State |
|---|---|
| `Game.unity` | The original hand-wired scene. **Leave it alone** — it is the working reference. |
| `Demo4x4.unity` | Built by `Tools ▸ WhoWhoo3D ▸ Build Demo4x4 Scene`. 16 cubes, 3 players, top faces only. |
| `DemoMapEditor.unity` | Built by `Tools ▸ WhoWhoo3D ▸ Build MapEditor Demo Scene`. Authoring demo, not a shipping scene. |
| `SingleplayerGame.unity` | **To do.** Same builder, plus map selection and player-count setup. |
| `MultiplayerGame.unity` | **To do.** Same builder again; only the session layer differs. |

- [ ] Extend `SceneSpec` with map selection and a start screen rather than fixed values.
- [ ] `SingleplayerGame` scene once Stage 9 (HUD, legibility, victory, restart) is done.
- [ ] `MultiplayerGame` scene — after the host/join milestone.
- [ ] Retire `Game.unity` once `SingleplayerGame` supersedes it.

---

## 4. UI and Mesh Rendering Design

**Status: Partial.**

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

Turn handoff and board readability also support online play and future target selection.

---

## 5. Deferred Infrastructure

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

## 6. Ideas Not Committed To

- Larger clusters and irregular shapes (the surface walk already supports any cube cluster).
- Sparse tile networks — maps where not every edge carries a tile pair. Supported by the
  model and validator today, but no map uses it and the gameplay effect is unexplored.
- Tuning `ConquestTakesFace = false` (only the targeted slot flips, so contested faces
  become reachable). Needs a majority-owner click rule implemented first, or the board can
  stall.
