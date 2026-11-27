# WhoWhoo3D gameplay report

[Watch the flat match and vertical demo (63.4 s)](demo4x4.mp4)

Unity 6000.4.5f1 · Demo4x4 · three local players · **P1 won in 25 clicks**.

**Tests passed** — Edit Mode (126), Play Mode (8). The match check compares every displayed legal move with the board rules, exercises map and player selection, checks restart, climbs a riser and clicks a rear face after orbiting.

The silent video records the game window at 12 fps. A seeded driver sends mouse events through the scene camera and tile colliders for the flat match. The terrace sequence stages a climb with legal alternating claims, rotates the camera by dragging, clicks the far wall, uses Relay and Freeze, takes supply and resolves a Beacon. The driver clicks Start match and Play again. The terrace sequence confirms Relay through the controller. Wave playback is accelerated for review. This run covers the local game on macOS. The LAN run below uses two copies of the same player.

## Local match setup

0.0 s

![Local match setup](screens/setup.png)

## Opening board and legal moves

1.6 s

![Opening board and legal moves](screens/opening.png)

## First claim and turn handoff

2.8 s

![First claim and turn handoff](screens/claim.png)

## Expansion across neighboring faces

12.1 s

![Expansion across neighboring faces](screens/wave.png)

## Match result

41.0 s

![Match result](screens/victory.png)

## Opening board after Play again

42.8 s

![Opening board after Play again](screens/restart.png)

## Terrace 6×6 opening

44.2 s

![Terrace 6×6 opening](screens/terrace.png)

## Inventory charges and ability guide

45.7 s

![Inventory charges and ability guide](screens/inventory-guide.png)

## Expansion from tread to riser

47.8 s

![Expansion from tread to riser](screens/terrace-wave.png)

## The terrace after orbiting to the far side

52.4 s

![The terrace after orbiting to the far side](screens/terrace-orbit.png)

## Relay target preview before confirmation

54.0 s

![Relay target preview before confirmation](screens/relay.png)

## A free supply choice after four ordinary claims

57.5 s

![A free supply choice after four ordinary claims](screens/supply.png)

## Frozen cube during the opposing turn

58.7 s

![Frozen cube during the opposing turn](screens/freeze.png)

## Public Beacon marks before the opponent replies

60.0 s

![Public Beacon marks before the opponent replies](screens/beacon.png)

## Beacon starting a wave before its owner acts

61.3 s

![Beacon starting a wave before its owner acts](screens/beacon-wave.png)

## Match setup and four-seat Freeze targeting at 800×480

![Match setup in a compact window](screens/compact-setup.png)

Real pointer input selected Freeze, picked a cube and orbited the camera with the preview still active. The capture driver exercised Cancel, Escape and Confirm and checked that only Confirm spent a turn.

![Four-seat terrace with Freeze selected](screens/compact-freeze.png)

## Inventory and adaptive layouts

![Four-seat inventory guide](screens/layouts/1280x720-1x-guide.png)

These offscreen renders use the game’s camera and controls. The four-seat waiting room, Freeze preview and result are staged for layout inspection. The portrait safe area has simulated side, top and bottom insets. The large-text setting requests 2× scaling and fits to the available space. Each capture checks control bounds, overlap and text overflow.

| Viewport | UI scale | Setup | LAN room | Target | Inventory | Result |
| --- | --- | --- | --- | --- | --- | --- |
| 800x480 | 1× | [setup](screens/layouts/800x480-1x-setup.png) | [room](screens/layouts/800x480-1x-room.png) | [target](screens/layouts/800x480-1x-target.png) | [guide](screens/layouts/800x480-1x-guide.png) | [result](screens/layouts/800x480-1x-result.png) |
| 800x480 | 2× | [setup](screens/layouts/800x480-2x-setup.png) | [room](screens/layouts/800x480-2x-room.png) | [target](screens/layouts/800x480-2x-target.png) | [guide](screens/layouts/800x480-2x-guide.png) | [result](screens/layouts/800x480-2x-result.png) |
| 1280x720 | 1× | [setup](screens/layouts/1280x720-1x-setup.png) | [room](screens/layouts/1280x720-1x-room.png) | [target](screens/layouts/1280x720-1x-target.png) | [guide](screens/layouts/1280x720-1x-guide.png) | [result](screens/layouts/1280x720-1x-result.png) |
| 1280x720 | 2× | [setup](screens/layouts/1280x720-2x-setup.png) | [room](screens/layouts/1280x720-2x-room.png) | [target](screens/layouts/1280x720-2x-target.png) | [guide](screens/layouts/1280x720-2x-guide.png) | [result](screens/layouts/1280x720-2x-result.png) |
| 1920x1080 | 1× | [setup](screens/layouts/1920x1080-1x-setup.png) | [room](screens/layouts/1920x1080-1x-room.png) | [target](screens/layouts/1920x1080-1x-target.png) | [guide](screens/layouts/1920x1080-1x-guide.png) | [result](screens/layouts/1920x1080-1x-result.png) |
| 1920x1080 | 2× | [setup](screens/layouts/1920x1080-2x-setup.png) | [room](screens/layouts/1920x1080-2x-room.png) | [target](screens/layouts/1920x1080-2x-target.png) | [guide](screens/layouts/1920x1080-2x-guide.png) | [result](screens/layouts/1920x1080-2x-result.png) |
| 3440x1440 | 1× | [setup](screens/layouts/3440x1440-1x-setup.png) | [room](screens/layouts/3440x1440-1x-room.png) | [target](screens/layouts/3440x1440-1x-target.png) | [guide](screens/layouts/3440x1440-1x-guide.png) | [result](screens/layouts/3440x1440-1x-result.png) |
| 3440x1440 | 2× | [setup](screens/layouts/3440x1440-2x-setup.png) | [room](screens/layouts/3440x1440-2x-room.png) | [target](screens/layouts/3440x1440-2x-target.png) | [guide](screens/layouts/3440x1440-2x-guide.png) | [result](screens/layouts/3440x1440-2x-result.png) |
| 360x640 | 1× | [setup](screens/layouts/360x640-1x-setup.png) | [room](screens/layouts/360x640-1x-room.png) | [target](screens/layouts/360x640-1x-target.png) | [guide](screens/layouts/360x640-1x-guide.png) | [result](screens/layouts/360x640-1x-result.png) |
| 360x640 | 2× | [setup](screens/layouts/360x640-2x-setup.png) | [room](screens/layouts/360x640-2x-room.png) | [target](screens/layouts/360x640-2x-target.png) | [guide](screens/layouts/360x640-2x-guide.png) | [result](screens/layouts/360x640-2x-result.png) |

## LAN match

Two macOS player processes ran on one computer. The guest discovered the named room over UDP and joined its advertised TCP port. The host played P1 and the guest played P2. Both finished on turn 23 with the same board, roster and result (P1 won).

![Host waiting for players](screens/lan-lobby.png)

![Room found on the Join LAN screen](screens/lan-room-browser.png)

![Guest taking a turn](screens/lan-guest-turn.png)

![Result on the host](screens/lan-host-result.png)

[Separate-interface and blocked-discovery checks](LAN.md)

## Refresh

On a graphics-capable macOS desktop with the pinned Unity editor, Mac build support, Python 3, FFmpeg and ffprobe installed, run the following from the repo root.

```sh
python3 scripts/report.py
```

Use `--unity /path/to/Unity` to select the editor executable. The command tests a disposable project copy, builds a player, then replaces the report, screenshots and MP4 after the match and video checks pass. Raw frames and build products are temporary.
