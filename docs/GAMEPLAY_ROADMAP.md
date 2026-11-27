# Gameplay roadmap

## Playable now

The shared match scene offers the quick 4×4 board and a 6×6 terrace with 52 cubes and three levels. Each corner starts six paired edges from the summit and has the same opening move count as its rivals. A wave can leave the lowest tread, fill a wall, then emerge on the upper tread. Right-drag to orbit, middle-drag to pan, scroll to zoom and press Home to fit. Hover a legal tile to mark its partner, including through a wall. [Gameplay recording and screenshots](reports/README.md)

**Relay** gives each seat one chance per match to give up a tile and reinforce the face across its paired edge. The landing slot and one other slot on that face must both be white and legal. The game picks the lowest-index other white edge and marks both destinations before confirmation. Cancel keeps the charge and turn. Confirm spends both and resolves full faces normally. An ordinary claim already reaches any unlocked face, so a simple one-tile move would give up territory for a destination the player could claim anyway. Two arrivals let Relay concentrate territory and sometimes trigger a wave immediately. Rivals can close a promising landing face by holding a tile there first.

## Which abilities to add

Three published games suggest different decisions worth trying here.

- *Mario + Rabbids Sparks of Hope* gives movement tactical uses through dashes, team jumps and cover. On this board the interesting movement decision is which foothold to give up to start a chain elsewhere. That motivated Relay. [Nintendo’s gameplay overview](https://play.nintendo.com/news-tips/game-releases/mario-rabbids-sparks-of-hope-available-now/)
- *Into the Breach* reveals enemy intentions before they resolve. A visible target and time to respond would give a delayed ability like Beacon a different role from Relay’s immediate expansion. [Subset Games’ design overview](https://www.subsetgames.com/itb.html)
- *Patchwork* lets a player spend time to acquire a patch or advance for currency. It suggests a way to make a quiet turn valuable here. Bank would let a player delay reinforcement while opponents develop the board. [Lookout Games’ rules](https://www.lookout-spiele.de/upload/en_patchwork.html_Rules_Patchwork_EN.pdf)

The opening maps give a reason to test Freeze on a prepared wall. In a two-seat match P1 has 45 ordinary claims on the flat board and 213 on the terrace. Even the best-placed frozen cube would remove only four and twelve of those choices respectively. Its value should come from denying one imminent expansion. Measure that directly before increasing its duration.

A 60-match probe checked how often Relay can finish a face. Each map ran with two, three and four seats using seeds 7, 19, 42, 73 and 101. In one run each seat chose uniformly from sorted legal claims. Its paired run used the same seed and spent Relay at the first chance to complete a face. Relay fired in all 15 enabled games on each map. On the flat board players spent 31 of 45 available charges. On the terrace they spent 44 of 45. Median match length was 25 actions on the flat board with either rule, and 125 on the terrace with ordinary claims versus 126 with Relay. This measures how often the opportunity arises under random play. Comparing its value against the best ordinary claim is the next balance test.

These are proposed rules for WhoWhoo3D. Start each prototype at one charge per seat and one whole action per use. Compare one new power at a time against Relay and ordinary claims.

| Priority | Power and decision | Opponent response and first check |
| --- | --- | --- |
| 1 | **Freeze** a cube through the next opposing seat’s turn. Block direct claims and Relay arrivals there. Waves still traverse it. | Rivals can fill a neighboring face and expand into it. Reject a target if it removes the next seat’s last legal claim. Show exactly whose turn ends the effect. |
| 2 | **Beacon** marks two white tiles on one open face. At the start of your next turn, claim both if they are still legal. Pick the target now and risk losing the entire action. | Everyone sees the marker and can occupy the face first. Resolve once before turn input. Compare cancellation rates in duels and four-seat games. |
| 3 | **Phase** makes a cube translucent through one opposing turn. Waves release their sources normally and skip arrivals on the phased cube. Ownership and physical edge pairs persist. | Rivals can build a chain around it or wait. Show a solid silhouette and a duration badge. Check wave order at inside corners and simultaneous arrivals. |
| 4 | **Bank** forgoes a claim now. Your next ordinary claim can take a second legal white edge on the same face, then the two resolve together. | Rivals have a turn to occupy the best face. Show the banked charge publicly. Compare the delayed burst with two ordinary claims. |
| 5 | **Switchback** marks one occupied face and one outgoing edge. On that face’s next expansion, send that edge’s arrival to a selected adjacent edge’s partner instead. Consume the mark once. | Rivals can conquer the marked face first to cancel it. Preview both paths and test repeated or simultaneous arrivals with stable edge ordering. |
| Later | **Pass** spends a charge to keep the position as it stands. A forced opponent skip would grant consecutive actions in a duel. | Try voluntary Pass first and keep its history distinct from a no-move skip. Revisit an opponent skip only if taking away a turn adds a decision for the affected seat. |

**Next implementation.** Add a typed action identifier and per-seat inventory when Freeze joins Relay. Store the targeted cube and expiry in the authoritative snapshot. Preview blocked faces, show the frost rim to every seat, and give the computer the same legal target set. Exercise cancel, duplicate delivery, expiry after a skipped or eliminated seat, reconnect and rematch.

**Balance check.** Run seeded matches with mirrored seats on both maps at two, three and four seats. Compare legal targets while a charge is available, uses per match, immediate expansions, lost turns and wins by starting seat. Count turns when a power is legal yet a normal claim scores better. Promote a power only after its intended choice occurs across multiple starts and a rival has a legal response during any announced delay.

## Adaptive interface

Migrate setup, room browser, scoreboard, inventory and results to shared uGUI/TextMeshPro components. Use one type scale, spacing scale, focus outline and set of button states. Use a symbol or pattern alongside each seat color. Put current turn and score at the top, inventory within reach at the bottom, and reserve the middle for the board. Compact screens can collapse the score list and expand one ability card at a time. Opening a tray should preserve the camera target.

The current IMGUI pass aligns the match panels and their pointer-blocking rectangles to the same safe area. The redesign should also handle larger text through reflow, keyboard focus, long room and ability names, and a visible explanation for each disabled action. Use anchored regions and a reference-resolution canvas, then choose compact layouts from available space. [Unity’s guide to multiple resolutions](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/HOWTO-UIMultiResolution.html)

**Acceptance.** Automatically capture setup, a four-seat room, ability targeting and victory at 800×480, 1280×720, 1920×1080 and 3440×1440, plus a 360×640 portrait layout probe with simulated safe insets. Repeat with large text. Check focused controls, wrapping, overlap, click-through and the ability to cancel using the keyboard. Portrait checks assess layout only.

## Animation and materials

Bevel cube edges and give the board a quiet material so ownership and connections stand out. Animate a claim settling onto its face, then move each expansion along the actual paired edge, including wall corners. Finish each step with a brief destination mark and score update. For Freeze use a frost rim and countdown. For Phase fade the body while keeping the outline and occupant marks visible.

Drive these effects from the existing wave playback steps. The simulator determines ownership and scores. Skipping playback, reconnecting mid-wave, accelerating the replay or enabling reduced motion must land on the same result. Smooth user-driven camera motion and offer an immediate reset. Reduced motion should replace flights and pulses with static source/destination marks. [Xbox guidance on camera motion and visual distraction](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117)

**Acceptance.** Record the same seeded terrace match in normal and reduced-motion modes, compare final snapshots, and measure frame time during its longest wave. Add the before/after camera angles, inventory preview and a short wave clip to the gameplay report.
