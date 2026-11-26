# LAN verification on separate interfaces

Two Linux x86-64 Unity players run the same seeded 4×4 match. Each has its own IPv4 interface on a temporary, internal Docker bridge. The only shared files are the final snapshots and the signal to exit. The match, discovery announcements and claims travel over sockets.

| Case | What the check requires |
| --- | --- |
| Discover a room | The guest receives the named UDP announcement on its own interface, joins the advertised TCP endpoint and reaches the same final snapshot as the host. |
| Join with discovery blocked | The guest container drops incoming UDP on port `47772`. Its browser sees no matching room for five seconds. It joins the host’s interface address on TCP `47771`. The runner checks the firewall packet counter, both snapshots and both exit codes. |

The runner also compares the two scenarios’ final snapshots. Docker removes the test interfaces, firewall rule and containers at the end. The macOS visual report exercises two standalone game windows on one computer. A physical Wi-Fi router can additionally isolate clients or apply per-app firewall rules. [Visual gameplay report](README.md)

## Recorded result

On November 26, 2026, two Linux IL2CPP players on separate virtual interfaces completed both scenarios on one Apple Silicon Mac. The direct-join run recorded dropped discovery packets on the guest. P1 won on turn 23 in both matches. All four final snapshots agreed on ownership, roster, active seat, turn and winner. Physical Wi-Fi devices remain unverified.

## Reproduce

Use Docker with Linux amd64 container support, Python 3, and the project’s Unity 6000.4.5f1 editor with Linux Build Support. On Apple Silicon use the IL2CPP module so the Linux players can run under amd64 emulation. The script adds Unity’s cross-compiler package to its temporary project. On an x86-64 host use the Mono module. From the repository root run

```sh
python3 scripts/verify_lan.py --unity "/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity"
```

The command builds a disposable test player, runs both matches, and prints the final turn and winner for each. The blocked guest receives `NET_ADMIN` for its own container firewall. The bridge exposes no game ports on the host.
