# Gameplay backlog

The [gameplay roadmap](GAMEPLAY_ROADMAP.md) gives the implementation order and acceptance checks for the next four updates. The [gameplay report](reports/README.md) covers the local match.

## 1. Networking / Online Multiplayer

One player hosts and plays P1. Guests select an advertised room or enter an IPv4 address. The host owns the match for the lifetime of the room.

- [x] Bind each TCP connection to a seat and validate its claims.
- [x] Deliver accepted turns in order to the host view and guests.
- [x] Wait for 2–4 seats, restore dropped guests from a snapshot and synchronize rematches.
- [x] Compare independent boards after every accepted claim and verify standalone host and guest results.
- [x] Discover rooms on the LAN. Advertise room name, protocol version and available seats. Remove expired listings and keep direct joining available.
- [x] Exercise discovery and direct joining across separate virtual network interfaces, including a guest that drops UDP discovery. [Recorded results](reports/LAN.md)

---

## 2. Abilities System

Begin with one charge of Freeze after the vertical board and camera slice. Phase and turn manipulation are later balance experiments.

- [ ] Define ability metadata and a charge-based inventory.
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
| Solo, hot-seat and LAN | Shared `Demo4x4.unity` setup and board. Seat controls and the session determine how moves arrive. |

- [ ] Decide which scene to ship as the default entry point.

---

## 4. UI and Mesh Rendering Design

**Status: Partial.**

The current board uses primitive cubes and code-spawned tiles. Mint highlights identify legal claims. The next readability task is making paired edges and wave paths legible across different heights.

### Mesh / visual
- [ ] Replace `GameObject.CreatePrimitive` tiles with an authored mesh or prefab. Tiles are
      spawned per slot (48 on Demo4x4), so this is a batching question too.
- [ ] A real cube prefab with proper materials, edge bevels, and a readable top surface.
- [ ] Distinguish **face state** visually: free, held, one-tile-from-full, locked by an enemy.
      Right now a full face and a nearly-full face look identical.
- [ ] Buildable/placeable item visuals — the piece the future map editor and abilities need.
- [ ] Fit, orbit, pan and zoom the vertical demo using the loaded board bounds.

### UI interaction
- [ ] Replace `ScoreboardView` and `TimelineView` IMGUI with uGUI/TextMeshPro.
- [x] Hover and selection feedback on tiles — the single biggest legibility gap.
- [x] Click affordances: highlight legal slots for the active player, dim locked faces.
- [ ] Turn hand-off screen for hot-seat play, so players know the turn changed.
- [ ] Wave playback controls surfaced properly rather than as a debug bar.

Turn handoff and board readability also support online play and future target selection.

---

## 5. Deferred Infrastructure

- [ ] **Retire or fix TruthPack.** It currently does **not compile** — all 8 source files
      still use `TileStack` / `SideStateChange`, deleted in the slot-model rewrite. Nothing
      on the dev machine builds it (`dotnet` is not installed), which is how it rotted twice.
      Recommendation: delete it; the Unity Edit Mode suite covers the same ground.
- [ ] **Delete the empty assemblies** `Data` and `Utils`
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
- [x] Play Mode coverage for the saved scene and LAN match lifecycle.
- [ ] Audio, VFX polish.
- [ ] Non-cube cell shapes. The Domain is already shape-agnostic; only `CubeGeometry` and
      the map generator assume six faces.

---

## 6. Ideas Not Committed To

- Larger or irregular clusters beyond the planned stepped demo.
- Sparse tile networks — maps where not every edge carries a tile pair. Supported by the
  model and validator today, but no map uses it and the gameplay effect is unexplored.
- Tuning `ConquestTakesFace = false` (only the targeted slot flips, so contested faces
  become reachable). Needs a majority-owner click rule implemented first, or the board can
  stall.
