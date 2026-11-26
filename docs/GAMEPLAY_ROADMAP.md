# From the 4×4 slab to a vertical board

The first playable update should establish whether a player can see where a claim on a lower terrace will travel, rotate the board, and follow it onto the upper level. Ship that slice before adding an ability that changes the answer. Keep the flat 4×4 as the quick-start map and regression fixture.

## 1. Build a stepped demo and its camera together

Start with a **6×6 footprint**. Use a broad lower terrace, two side routes around a raised middle terrace, and a short two-block-high ridge. Leave the camera a clear view into the inside corners. Make top faces and selected risers playable. Show paired edges with matching marks on both surfaces. Place the opening seats on opposite approaches, with viable openings for two, three and four players. A narrow bridge is a useful second variant after routes on the terrace read well.

Proposed footprint, viewed from above. Each digit is the column height in cubes. Four height-three columns form the summit. A player can approach them by either outer lane.

```text
1 1 1 1 1 1
1 1 2 2 1 1
1 2 3 3 2 1
1 2 3 3 2 1
1 1 2 2 1 1
1 1 1 1 1 1
```

Start the two-seat version at diagonally opposite corners. For four seats, use all corners. For three, choose starts by shortest *playable-edge* distance to the ridge and by opening move count. Check both metrics in the map fixture.

Use the existing map schema, board builder and match setup. Load the map through the same selector in Solo, Hot-seat and LAN. Generate a demonstration scene through `GameSceneBuilder` if it needs its own launch shortcut. Fit the camera to the selected board’s bounds on map change. The current builder positions a fixed camera from a nominal board size.

- Orbit the board pivot with a secondary-button drag. Pan along the board plane with a middle-button drag, zoom with the wheel, and reset to fit with a key or HUD button. Provide keyboard rotation and zoom. Limit pitch and pan so the board remains reachable.
- Keep camera state local to each player. Suppress claims during drags and over interface controls. Show the selected slot’s partner on hover or focus, including a direction marker when it is occluded. Orbiting must preserve the selection.
- **Acceptance.** Validate reciprocal pairs and opening moves for each seat. Drive a claim whose wave crosses a tread, rises up a wall and reaches an upper face. Capture the same route from two angles. Verify a rear-face click after rotation, pan limits, resizing, reset, and zero claims during camera drags. Replay the match on two boards and compare the settled states.

## 2. Give one ability a complete inventory interaction

Prototype **Freeze** first. Each seat starts with one charge. Using it spends that seat’s action. The player chooses a cube, previews the affected faces and duration, then confirms or cancels. Until the next opposing seat finishes its turn, direct claims on that cube are unavailable. Waves continue across its paired edges. Reject a target that would remove every legal claim from that next seat. This makes Freeze a choice between blocking an approach and advancing your own position.

Put a compact inventory beside the turn indicator. A card shows an icon, name, remaining charges and the rule in one sentence. Selecting it highlights legal cubes. The confirmation identifies the cube and who acts before the effect expires. Other players see the same status badge on the board and in turn history. Keep the tray visible in its empty, depleted, targeting and waiting states. Start with charges only.

| Later experiment | Decision it gives the player | First constraint to test |
| --- | --- | --- |
| **Phase** a cube | Temporarily sacrifice an incoming expansion to protect a route. The cube becomes translucent with a solid silhouette and a duration badge. | Prototype a one-turn effect. Existing ownership stays in place. Incoming waves release their source normally and skip arrivals on the phased cube. Preserve slot pairs and scores. Verify expiry, selection behind it and overlapping waves. |
| **Pass** your own turn | Spend a scarce charge when claiming would trigger an unfavorable chain. | One use per seat per match. Record a voluntary pass separately from a forced no-move skip. Check end conditions with two, three and four players. |
| **Skip an opponent** | Interrupt a leader immediately before an expansion. | Defer until Pass and Freeze are measured. In a duel this grants consecutive actions. Measure lost turns and lockouts before trying a telegraphed, once-per-match version with protection against consecutive skips. |

Use deterministic target IDs, charge counts and expiry turns in the match model. The host validates actor, turn, target and charges before publishing the accepted action. Bump the wire protocol when adding ability commands. Include inventory and active effects in snapshots, history and rematches. Test cancellation, duplicate delivery, wrong-seat input, elimination during an effect, reconnect after activation and restart. Compare seeded results with abilities enabled and disabled. Track how often players have a useful choice and how many turns a seat loses.

Subset Games describes showing enemy intentions in advance so players can choose a response. The proposed target preview and expiry badge apply that idea here. The specific effects and durations above are design hypotheses for this game. [Into the Breach design overview](https://www.subsetgames.com/itb.html)

## 3. Give the interface one layout and visual language

Migrate setup, room browser, scoreboard, inventory and results to shared uGUI/TextMeshPro components. Use one type scale, spacing scale, focus outline and set of button states. Use a symbol or pattern alongside each seat color. Put current turn and score at the top, inventory within reach at the bottom, and reserve the middle for the board. Compact screens can collapse the score list and expand one ability card at a time. Opening a tray should preserve the camera target.

The current IMGUI pass aligns the match panels and their pointer-blocking rectangles to the same safe area. The redesign should also handle larger text through reflow, keyboard focus, long room and ability names, and a visible explanation for each disabled action. Use anchored regions and a reference-resolution canvas, then choose compact layouts from available space. [Unity’s guide to multiple resolutions](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/HOWTO-UIMultiResolution.html)

**Acceptance.** Automatically capture setup, a four-seat room, ability targeting and victory at 800×480, 1280×720, 1920×1080 and 3440×1440, plus a 360×640 portrait layout probe with simulated safe insets. Repeat with large text. Check focused controls, wrapping, overlap, click-through and the ability to cancel using the keyboard. Portrait checks assess layout only.

## 4. Animate the route and the outcome

Bevel cube edges and give the board a quiet material so ownership and connections stand out. Animate a claim settling onto its face, then move each expansion along the actual paired edge, including wall corners. Finish each step with a brief destination mark and score update. For Freeze use a frost rim and countdown. For Phase fade the body while keeping the outline and occupant marks visible.

Drive these effects from the existing wave playback steps. The simulator determines ownership and scores. Skipping playback, reconnecting mid-wave, accelerating the replay or enabling reduced motion must land on the same result. Smooth user-driven camera motion and offer an immediate reset. Reduced motion should replace flights and pulses with static source/destination marks. [Xbox guidance on camera motion and visual distraction](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117)

**Acceptance.** Record the same seeded terrace match in normal and reduced-motion modes, compare final snapshots, and measure frame time during its longest wave. Add the before/after camera angles, inventory preview and a short wave clip to the gameplay report.
