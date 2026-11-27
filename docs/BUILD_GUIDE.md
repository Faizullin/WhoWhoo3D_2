# Build and verify

## Run or build the game

1. Open this repository in Unity **6000.4.5f1** with desktop build support for the target platform.
2. Open `Assets/_Project/Scenes/Demo4x4.unity` and press Play. Both local modes and player-hosted LAN start here.
3. For a standalone player, choose **File → Build Profiles**, select the desktop target and build using the checked-in scene list. The first enabled scene is Demo4x4. On the development Mac the local build is `Builds/WhoWhoo3D.app`.

Use `DemoMapEditor.unity` to place or erase cubes, paint starting ownership and save maps to `Assets/StreamingAssets/Maps/`. Setup lists the JSON files in that folder. If scene wiring changes, update the saved scene and its builder together. The **Tools → WhoWhoo3D** menu can regenerate the demo and map editor scenes.

## Run tests and refresh captures

In **Window → General → Test Runner**, run the project’s Edit Mode and Play Mode suites. The former covers rules, topology and deterministic replay. The latter covers saved-scene controls, waves, inventory, LAN and rematches.

On a graphics-capable macOS desktop with the pinned Unity editor, Mac build support, Python 3, FFmpeg and ffprobe, run this command from the repository root.

```sh
python3 scripts/report.py
```

The script tests a disposable project copy and builds its own capture player. It records the local match, compares two LAN players, checks 50 layouts and profiles the longest wave in a seeded terrace match with screen recording disabled. It publishes the Markdown report, screenshots, full video and wave clip in `docs/reports/` after verification. Use `--unity /path/to/Unity` for another installation of the pinned editor. [Latest report](reports/README.md)

The separate Linux network check uses Docker and Linux build support. Its guide records the exact command, prerequisites and last verified result. [LAN verification](reports/LAN.md)
