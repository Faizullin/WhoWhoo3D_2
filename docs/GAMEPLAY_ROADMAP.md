# Gameplay roadmap

## Ready for playtesting

The current demo includes the flat 4×4 and three-level terrace maps, Solo, Hot-seat, player-hosted LAN, inventory and rematches. Players can see a face one claim away from expanding, track an arrival across a wall and identify a frozen cube and its expiry turn. The report includes the short terrace clip, normal and reduced-motion verification, and timing for the longest wave in a seeded match. [Gameplay report](reports/README.md)

## Next decision

Use a short terrace playtest to choose the next change. Ask each player to point out a face about to expand, follow one landing, and say when a frozen cube becomes available again. Record the map, seat, turn and camera angle for anything they miss. Also try the same build on two computers on the intended Wi-Fi network and reconnect the guest.

| Finding | Follow-up |
| --- | --- |
| A warning is missed, or looks like a selectable tile | Revisit the diamond’s size, contrast or placement using the recorded view. |
| A wave is hard to follow | Adjust landing duration or add player-facing playback speed controls. Compare normal and reduced-motion snapshots again. |
| LAN discovery or reconnect fails on hardware | Record both players’ room status, check direct IPv4 joining and repeat the isolated network test. |

## Later work

Ability balancing is deferred. Before adding another power, compare Freeze and Beacon against ordinary claims on mirrored starts. Phase would suppress arrivals for one opposing turn. Bank would trade the current action for a stronger later claim. Both remain design candidates.

Audio, additional maps, camera smoothing and work on the legacy TruthPack harness can be scheduled separately. The active verification path is the Unity test suite and recorded player runs.
