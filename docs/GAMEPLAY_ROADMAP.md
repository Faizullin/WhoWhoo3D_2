# Gameplay roadmap

## Playable now

The shared match scene offers the quick 4×4 board and a 6×6 terrace with 52 cubes and three levels. Each corner starts six paired edges from the summit and has the same opening move count as its rivals. A wave can leave the lowest tread, fill a wall, then emerge on the upper tread. Right-drag to orbit, middle-drag to pan, scroll to zoom and press Home to fit. Hover a legal tile to mark its partner, including through a wall. [Gameplay recording and screenshots](reports/README.md)

**Relay** gives each seat an opening charge to give up a tile and reinforce the face across its paired edge. The landing slot and one other slot on that face must both be white and legal. The game picks the lowest-index other white edge and marks both destinations before confirmation. Cancel keeps the charge and turn. Confirm spends both and resolves full faces normally. An ordinary claim already reaches any unlocked face, so a simple one-tile move would give up territory for a destination the player could claim anyway. Two arrivals let Relay concentrate territory and sometimes trigger a wave immediately. Rivals can close a promising landing face by holding a tile there first.

**Fixed charges** gives each seat Relay ×1 and Freeze ×1. **Supply after 4 claims** adds one free refill of an empty card after a seat’s fourth ordinary claim. Beacon is available through that supply choice. Both rulesets use the same scene and apply to Solo, Hot-seat and player-hosted LAN. [How to play](../README.md)

## Ability design and next experiments

Three published games suggest different decisions worth trying here.

- *Mario + Rabbids Sparks of Hope* gives movement tactical uses through dashes, team jumps and cover. On this board the interesting movement decision is which foothold to give up to start a chain elsewhere. That motivated Relay. [Nintendo’s gameplay overview](https://play.nintendo.com/news-tips/game-releases/mario-rabbids-sparks-of-hope-available-now/)
- *Into the Breach* reveals enemy intentions before they resolve. A visible target and time to respond would give a delayed ability like Beacon a different role from Relay’s immediate expansion. [Subset Games’ design overview](https://www.subsetgames.com/itb.html)
- *Patchwork* lets a player spend time to acquire a patch or advance for currency. It suggests a way to make a quiet turn valuable here. Bank would let a player delay reinforcement while opponents develop the board. [Lookout Games’ rules](https://www.lookout-spiele.de/upload/en_patchwork.html_Rules_Patchwork_EN.pdf)

The opening maps give a reason to test Freeze on a prepared wall. In a two-seat match P1 has 45 ordinary claims on the flat board and 213 on the terrace. Even the best-placed frozen cube would remove only four and twelve of those choices respectively. Its value should come from denying one imminent expansion. Measure that directly before increasing its duration.

A 60-match probe checked how often Relay can finish a face. Each map ran with two, three and four seats using seeds 7, 19, 42, 73 and 101. In one run each seat chose uniformly from sorted legal claims. Its paired run used the same seed and spent Relay at the first chance to complete a face. Relay fired in all 15 enabled games on each map. On the flat board players spent 31 of 45 available charges. On the terrace they spent 44 of 45. Median match length was 25 actions on the flat board with either rule, and 125 on the terrace with ordinary claims versus 126 with Relay. This measures how often the opportunity arises under random play. Comparing its value against the best ordinary claim is the next balance test.

A second probe played 36 matches to compare Fixed charges and Supply. Each ruleset ran on each map with two, three and four seats and seeds 11, 37 and 73. Seats chose a random ordinary claim unless a Freeze could interrupt a one-away expansion threatening their tiles or a Beacon could complete a face. Each supply pickup went to Beacon. On the flat board, 15 of 26 placed Beacons landed. On the terrace, 21 of 27 landed. All matches finished. These opponents did not deliberately defend against marks, so the next comparison should give defenders that choice.

Freeze and Beacon are playable. Phase, Bank, Switchback and Pass are candidates for later experiments. Compare each candidate with the ordinary claim the player could make instead.

| Priority | Power and decision | Opponent response and first check |
| --- | --- | --- |
| Playable | **Freeze** a cube through the next opposing seat’s turn. Block direct claims and Relay arrivals there. Waves still traverse it. | Rivals can fill a neighboring face and expand into it. Reject a target if it removes the next seat’s last legal claim. Show exactly whose turn ends the effect. |
| Playable in Supply | **Beacon** marks two white tiles on one open face. At the start of your next turn, claim both if they are still legal. Pick the target now and risk losing the entire action. | Everyone sees the marker and can occupy the face first. Resolve once before turn input. Compare cancellation rates in duels and four-seat games. |
| 3 | **Phase** makes a cube translucent through one opposing turn. Waves release their sources normally and skip arrivals on the phased cube. Ownership and physical edge pairs persist. | Rivals can build a chain around it or wait. Show a solid silhouette and a duration badge. Check wave order at inside corners and simultaneous arrivals. |
| 4 | **Bank** forgoes a claim now. Your next ordinary claim can take a second legal white edge on the same face, then the two resolve together. | Rivals have a turn to occupy the best face. Show the banked charge publicly. Compare the delayed burst with two ordinary claims. |
| 5 | **Switchback** marks one occupied face and one outgoing edge. On that face’s next expansion, send that edge’s arrival to a selected adjacent edge’s partner instead. Consume the mark once. | Rivals can conquer the marked face first to cancel it. Preview both paths and test repeated or simultaneous arrivals with stable edge ordering. |
| Later | **Pass** spends a charge to keep the position as it stands. A forced opponent skip would grant consecutive actions in a duel. | Try voluntary Pass first and keep its history distinct from a no-move skip. Revisit an opponent skip only if taking away a turn adds a decision for the affected seat. |

**Next experiment.** Start Blue one tile away from expanding onto Red’s occupied wall. Compare Red’s best immediate claim with freezing Blue’s cube. Repeat with a neighboring wave route open. For Beacon, compare an empty two-slot face, a friendly face and a face an opponent can easily close. Record how often a threatened player changes their action, and whether the original threat survives the reply. Keep Phase and turn-skipping powers outside the demo until these comparisons are available.

**Balance check.** Run seeded matches with mirrored seats on both maps at two, three and four seats. Compare legal targets while a charge is available, uses per match, immediate expansions, lost turns and wins by starting seat. Count turns when a power is legal yet a normal claim scores better. Promote a power only after its intended choice occurs across multiple starts and a rival has a legal response during any announced delay.

## Adaptive interface

The shared scene now uses a safe-area uGUI canvas and TextMeshPro for live matches. The header shows the turn, scores and public charge counts. The lower tray holds the local seat's cards, supply choice and target preview. Freeze identifies the selected cube and the seat after which it thaws. Narrow layouts stack the controls beneath the instructions. The saved-scene pointer test covers targeting, orbiting, canceling and confirming a four-seat Freeze. The capture script repeats that interaction in an 800×480 standalone window. [Gameplay report](reports/README.md)

Setup, LAN discovery, the waiting room and results now use the same controls and typography as the live HUD. Tab and Shift+Tab reach enabled buttons and text fields. The result initially focuses Play again for local players and hosts, or Leave room for guests. A brief “Pass to P2” prompt identifies the next hot-seat player while the board stays playable. Saved-scene checks cover keyboard setup, ability cancellation, replay, returning to setup, and joining and leaving a discovered room.

The ability tray uses a pair of arrows for Relay, a snowflake for Freeze and a signal for Beacon. Hover or keyboard focus shows the ability name. The info button opens the current seat’s inventory and rules. Large text and reduced motion can be toggled in the guide. Closing it restores the pending target and keyboard focus.

The report renders setup, a four-seat waiting room, a selected Freeze target, the inventory guide and results at 800×480, 1280×720, 1920×1080, 3440×1440 and 360×640. Each is checked at default and large scale. Portrait uses simulated insets for layout inspection. A separate saved-scene test exercises pointer and keyboard input, focus restoration and cancellation. [Screenshots at each size](reports/README.md#inventory-and-adaptive-layouts)

## Animation and materials

The board uses beveled cube and tile meshes. Ownership has a distinct color and pip count per seat. Wave flights follow each slot’s physical edge, including the turn from a tread onto a riser. Reduced motion preserves the sequence of ownership and score updates while suppressing the moving tiles. The saved-scene test repeats a terrace climb with reduced motion and compares the resulting match snapshots.

**Next implementation.** Mark a face that is one claim away from expansion. Add a brief landing cue on the destination, including a static mark in reduced motion. Give frozen cubes a distinct rim and expiry indicator. Then profile the longest terrace wave.

Drive these effects from the existing wave playback steps. The simulator determines ownership and scores. Skipping playback, reconnecting mid-wave, accelerating the replay or enabling reduced motion must land on the same result. Smooth user-driven camera motion and offer an immediate reset. Reduced motion should replace flights and pulses with static source/destination marks. [Xbox guidance on camera motion and visual distraction](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117)

**Acceptance.** Record the same seeded terrace match in normal and reduced-motion modes, compare final snapshots, and measure frame time during its longest wave. Add the before/after camera angles, inventory preview and a short wave clip to the gameplay report.
