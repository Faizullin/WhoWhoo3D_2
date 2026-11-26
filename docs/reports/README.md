# WhoWhoo3D gameplay report

[Watch the full match and restart (43.5 s)](demo4x4.mp4)

Unity 6000.4.5f1 · Demo4x4 · three local players · **P1 won in 25 clicks**.

**Tests passed** — Edit Mode (109), Play Mode (3). The match check compares every displayed legal move with the board rules, exercises map and player selection, and checks the opening state after restart.

The silent video records the game window at 12 fps. A seeded driver sends mouse events through the scene camera and tile colliders. Setup and restart use the same controller actions as the HUD buttons. Wave playback is accelerated for review. This run covers the local game on macOS. The LAN run below uses two copies of the same player.

## Local match setup

0.0 s

![Local match setup](screens/setup.png)

## Opening board and legal moves

1.2 s

![Opening board and legal moves](screens/opening.png)

## First claim and turn handoff

2.5 s

![First claim and turn handoff](screens/claim.png)

## Expansion across neighboring faces

11.8 s

![Expansion across neighboring faces](screens/wave.png)

## Match result

40.7 s

![Match result](screens/victory.png)

## Opening board after Play again

42.2 s

![Opening board after Play again](screens/restart.png)

## LAN match

Two macOS player processes connected over loopback. The host played P1 and the guest played P2. Both finished on turn 23 with the same board, roster and result (P1 won).

![Guest taking a turn](screens/lan-guest-turn.png)

![Result on the host](screens/lan-host-result.png)

## Refresh

On a graphics-capable macOS desktop with the pinned Unity editor, Mac build support, Python 3, FFmpeg and ffprobe installed, run the following from the repo root.

```sh
python3 scripts/report.py
```

Use `--unity /path/to/Unity` to select the editor executable. The command tests a disposable project copy, builds a player, then replaces the report, screenshots and MP4 after the match and video checks pass. Raw frames and build products are temporary.
