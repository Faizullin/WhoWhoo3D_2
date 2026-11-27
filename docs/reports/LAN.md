# LAN verification on separate interfaces

On **26 November 2026**, two Linux IL2CPP players on one Apple Silicon Mac completed the same seeded 4×4 match in separate containers on an internal Docker bridge. P1 won on turn 23. All four final snapshots agreed on board ownership, roster, active seat, turn and winner.

| Scenario | Verified behavior |
| --- | --- |
| Room discovery | The guest received the host’s UDP announcement on its own IPv4 interface and joined the advertised TCP endpoint. |
| Discovery blocked | The guest dropped UDP 47772, saw no matching room for five seconds, then joined the host’s IPv4 address on TCP 47771. The runner checked the packet-drop counter and both players’ exit codes. |

These runs exercised virtual interfaces. Discovery, firewall behavior and reconnect on two physical Wi-Fi devices remain to be checked. The current macOS report includes a separate two-process match. [Visual report](README.md)

## Reproduce

Use Python 3, Docker with Linux amd64 container support, and Unity 6000.4.5f1 with Linux Build Support. On Apple Silicon install the IL2CPP module. On an x86-64 host use Mono. Run from the repository root.

```sh
python3 scripts/verify_lan.py --unity "/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity"
```

The runner builds a temporary player, compares both matches and removes the containers and bridge. The blocked guest uses `NET_ADMIN` for its container firewall.
