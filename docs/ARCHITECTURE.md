# Runtime architecture

`Demo4x4.unity` wires one controller, board view, camera/input handler and HUD. Maps, seat controls and the LAN session determine how that scene plays. Runtime modules live under `Assets/_Project/Runtime/`.

| Module | Responsibility |
| --- | --- |
| Domain | Pure C# board topology, stable IDs, map serialization, match commands and deterministic wave simulation. |
| Gameplay | Match setup, players, legality, computer turns, history, snapshots and playback. `BoardView` renders cells and slots. |
| Input | Local orbit, pan, zoom, pointer picking and board input blocking. |
| UI | Shared uGUI/TextMeshPro controls, setup, LAN browser, results, live inventory and modal guide. F3 opens the local debug timeline. |
| Networking | Room discovery, seat assignment, host validation, ordered delivery, reconnect and rematch. |
| MapEditor | Map authoring through the shared board builder, serializer and renderer. |

## A turn

`MatchSetup` holds the map, supply setting and ordered seats. Inputs submit a `ClaimSlotCommand` with actor, turn, action and target. `GameController.TrySubmitClaim` checks the phase, turn and actor. `MatchAbilities` validates the targets and charges. An accepted action records history, resolves a wave and checks victory. Supply refills keep the turn open. A pending Beacon resolves on its owner’s turn boundary before ordinary input resumes.

The simulator writes final ownership and an ordered change log. `WavePlaybackPlan` groups the changes into visual steps. `WaveAnimator` uses each expanded change’s recorded origin and destination for flights, landing frames and displayed scores. `BoardView` shows one-away diamonds between decisions and frost rims from the active inventory state. All three effects are presentation. Snapshot restore rebuilds views from the map and match state. [Wave rules](WAVE_SPEC.md)

## Multiplayer

The host plays P1 and runs a separate, headless authority controller. Guests discover its advertised room or enter an IPv4 address. The TCP handshake checks the protocol and installed map digest. The host validates the connection’s seat, current round, turn and move. Both the host’s visible board and guests consume the same sequenced commands.

During a disconnect the host reserves the seat. A guest in the same process can reconnect with its seat token. Once all seats are present, a settled snapshot restores the board, roster, turn, inventory and pending effects. Rematch starts a new round with the same setup. Camera, guide focus and display preferences stay local.

## Conventions

Group code by feature. Use `CellId`, `SideId`, `SlotId` and `PlayerId` at boundaries. Keep Unity types out of Domain. Use stable ordering for rule evaluation, direct calls for ordered transitions and shared legality for local players, computer seats and the LAN authority. Preserve Unity `.meta` files when moving assets.

`ScoreboardView` owns screen selection. `MatchScreens` and `MatchHud` share controls and safe-area layout. `AbilityGuide` owns modal focus and blocks board input through `IBoardInputPanel`. Changes to scene wiring belong in both `GameSceneBuilder` and the saved scene. [Build guide](BUILD_GUIDE.md)
