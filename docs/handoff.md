# Project handoff

**27 November 2026. The current demo is ready for a human playtest.** Launch the macOS player in `Builds/WhoWhoo3D.app`, or open the saved `Demo4x4.unity` scene in Unity 6000.4.5f1. Select `terrace6x6` and Hot-seat for the quickest way to inspect the board cues.

## What changed

- A gold diamond marks an unfrozen, occupied face with exactly one empty slot. Both seats can see the warning between decisions.
- A pale frame marks the actual landing slot after each wave step. Reduced motion uses the same frame while omitting the flight. Frost rims and the inventory tray identify the frozen cube and its expiry turn.
- Solo, Hot-seat and LAN share the same map, turn rules, ability inventory and HUD. Match setup, reconnect, rematch, keyboard focus and display preferences are implemented.

The latest automated run passed 126 Edit Mode and 8 Play Mode tests. The report links the 50 layout captures, a two-process LAN match, and a short clip of the terrace cue sequence. [Verification results](reports/README.md)

## Where to resume

| Task | Start here |
| --- | --- |
| Run tests, build a desktop player or regenerate recorded evidence | [Build guide](BUILD_GUIDE.md) |
| Inspect the wall climb, arrival frames, frame timings or device layouts | [Gameplay report](reports/README.md) |
| Change rules, presentation or room behavior | [Runtime architecture](ARCHITECTURE.md), [wave rules](WAVE_SPEC.md) |
| Add or edit a map | [Map format](MAP_SCHEMA.md) |
| Pick the next piece of work | [Gameplay roadmap](GAMEPLAY_ROADMAP.md) |

## Still unverified by people

Can two players distinguish a near expansion from a legal move, follow the landing on a wall, and identify the thaw turn at their usual viewing distance? Does discovery and reconnection work on their physical LAN? Automated tests already cover controller behavior, two desktop processes, simulated portrait insets and separate virtual network interfaces. The next decision is whether observations justify changing the cues or controls. [Earlier separate-interface LAN run](reports/LAN.md)
