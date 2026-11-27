# WhoWhoo3D

A turn-based strategy game for two to four seats. Play solo against computer opponents, share one screen, or host a match for players on your local network. Claim a mint tile on an open cube face. Filling the face sends its tiles across the adjoining edges and can start a chain reaction. The last player holding tiles wins.

[![Three-level terrace in a two-player match](docs/reports/screens/terrace.png)](docs/reports/README.md)

[Watch a complete match and the terrace demo](docs/reports/demo4x4.mp4) · [Browse the gameplay report and LAN screenshots](docs/reports/README.md)

## Play in Unity

Open the project in Unity **6000.4.5f1** and play `Assets/_Project/Scenes/Demo4x4.unity`. For shared-screen play, select **Hot-seat**, choose a map and player count, then select **Start match**. In **Solo**, you play P1 and the computer takes the remaining seats. Mint tiles accept a claim, a gold outline identifies the hovered legal tile, and gray tiles are locked by another player. Press **F3** in a local match to show the debug timeline. The header briefly says who to pass the controls to after each hot-seat turn. After victory, **Play again** restores the selected map and roster.

Select **terrace6x6** for a three-level board with playable walls. Right-drag to orbit, middle-drag to pan, scroll to zoom and press **Home** to fit the board. **A/D** rotate, **W/S** tilt and **Q/E** zoom. The gold cross marks the hovered tile’s paired edge.

Each seat starts with one **Relay** and one **Freeze**. For Relay, select the arrows icon, pick a gold-rimmed owned tile and preview the two marked destinations. Confirm to vacate your tile and claim both white tiles across its edge. A full destination face expands immediately. Cancel returns to normal claims and keeps the charge.

**Freeze** spends your turn to ice a cube. Pick a tile on it and confirm. The next opponent cannot claim there or land Relay there, and can still send a wave into it. The blue tint clears after that opponent’s wave. The preview only offers cubes where the opponent has a legal claim both on and off the cube.

Choose **Fixed charges** for the opening inventory, or **Supply after 4 claims** to earn one refill per seat per match. On a later turn, collect an empty Relay, Freeze, or **Beacon** card for free. A Beacon marks two white tiles you select on the same open face. Both must still be legal when your next turn begins. Its wave finishes before you choose a normal action. The header shows scores and public charges. On the board, one to four pips identify the seat that owns a tile.

Hover or focus an inventory icon to see its name. Select the **ⓘ** button to see the local seat’s charges, ability rules and refill conditions. Close the guide to return to a pending target. **Large text** enlarges the interface to fit the screen. **Reduce motion** gives buttons immediate feedback and plays waves as successive board states. Both preferences are saved on this device. Use **Tab** and **Shift+Tab** to move between buttons and text fields in setup, the room browser, play and results. Press **Enter** to activate a focused button or **Escape** to cancel an ability preview or supply selection. Camera controls work throughout playback.

## Host or join on the same network

1. Open the same desktop build on each computer and connect to the same Wi-Fi or Ethernet network. To create the build in Unity, open **File → Build Profiles**, select the desktop target and choose **Build**. The checked-in scene list opens the match setup first.
2. On one computer, select **Host LAN**, choose a room name, map and total number of players (including yourself), then select **Host room**. You play P1.
3. On each other computer, select **Join LAN** and click the room under **Nearby rooms**. Each listing shows the map and how many seats are open. If the room does not appear, enter the IPv4 address shown on the host and select **Join by address**. The match starts when all seats are filled. Each player can claim tiles on their own turn.

If a guest loses its connection, the room waits. Select **Reconnect** in the same running game to resume from the host’s board. The host can start a rematch after victory. Leaving an active room ends it for everyone. For connection problems, check that the computers can reach each other and that the host allows the game to receive local connections on TCP port `47771`. Automatic discovery also uses local IPv4 broadcasts on UDP port `47772`.

## Project status

| Area | Current state |
| --- | --- |
| Local match | Map and player setup, click-to-claim, animated waves, scores, elimination, result and restart |
| LAN | Discoverable player-hosted rooms for 2–4 seats, direct joining, turn validation, reconnect and rematch |
| Verification | Unity rule tests, a saved-scene pointer test, TCP Play Mode tests, a two-process macOS gameplay report, and [isolated Linux LAN tests](docs/reports/LAN.md) |
| Map editor | Separate authoring demo with place, erase, paint, save and load |
| Next implementation | Highlight faces one claim away from expansion, then add a destination cue when a wave lands. See the [gameplay roadmap](docs/GAMEPLAY_ROADMAP.md). |

The capture command and its requirements are in the [gameplay report](docs/reports/README.md). Board formats and expansion rules are in the [map schema](docs/MAP_SCHEMA.md) and [wave specification](docs/WAVE_SPEC.md).
