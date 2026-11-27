# WhoWhoo3D

A turn-based strategy game for two to four seats. Claim tiles on a cluster of cubes. Complete a face to send tiles across its edges and start a chain reaction. The last player holding tiles wins.

[![A match on the three-level terrace](docs/reports/screens/terrace.png)](docs/reports/README.md)

[Watch a complete match](docs/reports/demo4x4.mp4) · [Watch a wave climb the terrace](docs/reports/terrace-wave.mp4)

## Play

Open `Assets/_Project/Scenes/Demo4x4.unity` in Unity **6000.4.5f1** and press Play, or open the desktop build. Choose **Hot-seat** to share a screen, **Solo** to play P1 against computer opponents, or **Host LAN** to play P1 and host a room. Select `demo4x4` for a short, flat game or `terrace6x6` for three levels with playable walls.

- **Claim** a mint tile. A face containing an opponent’s tile is locked to direct claims. Seat colors and one to four pips show ownership. A gold diamond at the center of a face means its owner needs one more claim to expand. A pale frame marks each wave landing.
- **Move the camera** with right-drag to orbit, middle-drag to pan and scroll to zoom. A/D rotate, W/S tilt, Q/E zoom and Home resets the view. Hover over a legal tile to locate its paired edge.
- **Use an ability** by selecting an icon, picking a target and confirming. Cancel keeps the turn and charge. Relay trades one owned tile for two on the adjacent face. Freeze blocks direct claims and arrivals from abilities on a cube through the next opponent’s turn. Waves can still cross its frost rim. Beacon marks two tiles to claim together at the start of your next turn if both are still legal.
- **Choose inventory rules** in setup. Fixed charges gives each seat one Relay and one Freeze. Supply adds one free refill after four ordinary claims. Take it on a later turn to recharge an empty ability, including Beacon.
- **Open the ⓘ guide** to see charges, ability descriptions, Large text and Reduce motion. Tab and Shift+Tab navigate controls, Enter activates them and Escape closes the guide or cancels targeting. After victory, **Play again** restores your map and roster.

## Play on the same network

The host chooses a room name, map and total seats. Guests open the same build, select **Join LAN**, then choose the room or enter the host’s IPv4 address. The match starts when every seat is filled. A disconnected guest can select **Reconnect** in the same running game. The host can start a rematch. Room discovery uses UDP 47772 and play uses TCP 47771.

## Development

[Current handoff](docs/handoff.md) · [Build and verify](docs/BUILD_GUIDE.md) · [Architecture](docs/ARCHITECTURE.md) · [Gameplay roadmap](docs/GAMEPLAY_ROADMAP.md) · [Gameplay report and layout gallery](docs/reports/README.md)
