#!/usr/bin/env python3
"""Build a disposable macOS player and publish a verified gameplay recording."""

import argparse
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import subprocess
import tempfile
import time
import uuid
from xml.etree import ElementTree

ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / "scripts/report"
DEST = ROOT / "docs/reports"
SCENES = {
    "setup": "Local match setup",
    "opening": "Opening board and legal moves",
    "claim": "First claim and turn handoff",
    "wave": "Expansion across neighboring faces",
    "victory": "Match result",
    "restart": "Opening board after Play again",
}
FPS = 12


def run(args, log=None, **kwargs):
    try:
        subprocess.run(args, check=True, timeout=900, **kwargs)
    except (subprocess.SubprocessError, OSError) as error:
        tail = log.read_text(errors="replace")[-5000:] if log and log.exists() else ""
        raise RuntimeError(f"{args[0]} failed.\n{tail}") from error


def verify_suite(editor, project, work, platform, assembly):
    results = work / f"{platform}.xml"
    log = work / f"{platform}.log"
    try:
        run([editor, "-batchmode", "-nographics", "-projectPath", project, "-runTests",
             "-testPlatform", platform, "-assemblyNames", assembly, "-testResults", results, "-logFile", log], log)
    except RuntimeError:
        if results.exists():
            for case in ElementTree.parse(results).iter("test-case"):
                if case.get("result") == "Failed":
                    print(f"{case.get('fullname')}: {case.findtext('failure/message')}", flush=True)
        raise
    suite = ElementTree.parse(results).getroot()
    passed = int(suite.attrib["passed"])
    if suite.get("result") != "Passed" or not passed or passed != int(suite.attrib["total"]):
        raise RuntimeError(f"Incomplete {platform} suite: {suite.attrib}")
    return passed


def record_lan(command, env, frames, work):
    env = dict(env, WHOWHOO_ROOM_NAME="Demo " + uuid.uuid4().hex[:6])
    players = []
    deadline = time.monotonic() + 120

    def wait_for(names):
        while not all((frames / name).is_file() for name in names):
            for player, log in players:
                if player.poll() is not None:
                    raise RuntimeError(f"LAN player exited early.\n{log.read_text(errors='replace')[-3000:]}")
            if time.monotonic() >= deadline:
                raise RuntimeError(f"Timed out waiting for {names}")
            time.sleep(0.1)

    try:
        for role in ("host", "guest"):
            log = work / f"lan-{role}.log"
            player = subprocess.Popen([*command, "-logFile", log], env=dict(env, WHOWHOO_LAN_ROLE=role))
            players.append((player, log))
        wait_for(["lan-room-browser.png", "host.json", "guest.json", "lan-guest-turn.png", "lan-host-result.png", "lan-guest-result.png"])
        result = json.loads((frames / "host.json").read_text())
        if result != json.loads((frames / "guest.json").read_text()) or not result["finished"]:
            raise RuntimeError("Host and guest finished with different match states.")
        (frames / "complete").touch()
        for player, log in players:
            if player.wait(timeout=10):
                raise RuntimeError(f"LAN player failed.\n{log.read_text(errors='replace')[-3000:]}")
        return result
    finally:
        for player, _ in players:
            if player.poll() is None:
                player.kill()
            player.wait()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", help="Path to the pinned Unity editor executable")
    args = parser.parse_args()
    version = re.search(r"m_EditorVersion: (\S+)", (ROOT / "ProjectSettings/ProjectVersion.txt").read_text())[1]
    editor = args.unity or os.getenv("UNITY_EDITOR") or f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity"
    for command in (editor, "ffmpeg", "ffprobe"):
        if not shutil.which(command):
            parser.error(f"Missing executable: {command}")
    info = Path(shutil.which(editor)).resolve().parents[1] / "Info.plist"
    with info.open("rb") as stream:
        installed = plistlib.load(stream).get("CFBundleVersion")
    if installed != version:
        parser.error(f"This project uses Unity {version}, selected editor is {installed}")
    with tempfile.TemporaryDirectory(prefix="whowhoo-report-") as temp:
        work = Path(temp)
        project = work / "project"
        for folder in ("Assets", "Packages", "ProjectSettings"):
            shutil.copytree(ROOT / folder, project / folder)
        edit = verify_suite(editor, project, work, "EditMode", "WhoWhoo3D.Tests.EditMode")
        play = verify_suite(editor, project, work, "PlayMode", "WhoWhoo3D.Tests.PlayMode")
        print(f"Passed {edit} Edit Mode and {play} Play Mode tests. Building capture player.", flush=True)
        capture = project / "Assets/ReportCapture"
        capture.mkdir()
        (capture / "Editor").mkdir()
        shutil.copy(TOOLS / "BuildReportPlayer.cs", capture / "Editor")
        shutil.copy(TOOLS / "MatchRecorder.cs", capture)
        shutil.copy(TOOLS / "LanMatchRecorder.cs", capture)
        shutil.copy(project / "Assets/_Project/Tests/PlayMode/DemoMatchSequence.cs", capture)
        (capture / "ReportCapture.asmdef").write_text(json.dumps({
            "name": "ReportCapture", "references": ["WhoWhoo3D.Domain", "WhoWhoo3D.Gameplay", "WhoWhoo3D.Networking", "Unity.InputSystem"]}))
        (capture / "csc.rsp").write_text("-langversion:latest\n")
        (capture / "Editor/ReportBuild.asmdef").write_text(json.dumps({"name": "ReportBuild", "includePlatforms": ["Editor"]}))
        player = work / "Game.app"
        frames = work / "frames"
        frames.mkdir()
        env = dict(os.environ, WHOWHOO_PLAYER=str(player), WHOWHOO_FRAMES=str(frames))
        log = work / "build.log"
        run([editor, "-batchmode", "-quit", "-projectPath", project, "-executeMethod",
             "BuildReportPlayer.Build", "-logFile", log], log, env=env)
        log = work / "player.log"
        exe = next((player / "Contents/MacOS").iterdir())
        command = [exe, "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720"]
        run([*command, "-logFile", log], log, env=env)
        lan = record_lan(command, env, frames, work)
        lines = (frames / "timeline.tsv").read_text().splitlines()
        times = {label: int(frame) / FPS for frame, label in (line.split("\t") for line in lines)}
        result = re.search(r"Demo match completed in (\d+) clicks. Winner P(\d+)\.", log.read_text())
        if list(times) != list(SCENES) or not result:
            raise RuntimeError("The scene did not complete every lifecycle checkpoint.")
        output = work / "report"
        screens = output / "screens"
        screens.mkdir(parents=True)
        for name in [*SCENES, "lan-room-browser", "lan-guest-turn", "lan-host-result"]:
            shutil.copy(frames / f"{name}.png", screens)
        movie = output / "demo4x4.mp4"
        run(["ffmpeg", "-loglevel", "error", "-framerate", str(FPS), "-i", frames / "frame-%05d.png",
             "-vf", "scale=1280:720", "-c:v", "libx264", "-crf", "23", "-pix_fmt", "yuv420p", "-movflags", "+faststart", movie])
        metadata = json.loads(subprocess.check_output(["ffprobe", "-v", "error", "-show_streams", "-of", "json", movie]))
        stream = metadata["streams"][0]
        if stream["codec_name"] != "h264" or int(stream["nb_frames"]) != len(list(frames.glob("frame-*.png"))):
            raise RuntimeError("Encoded video failed validation.")
        report = [
            "# WhoWhoo3D gameplay report", "",
            f"[Watch the full match and restart ({float(stream['duration']):.1f} s)](demo4x4.mp4)", "",
            f"Unity {version} · Demo4x4 · three local players · **P{result[2]} won in {result[1]} clicks**.", "",
            f"**Tests passed** — Edit Mode ({edit}), Play Mode ({play}). "
            "The match check compares every displayed legal move with the board rules, exercises map and player selection, "
            "and checks the opening state after restart.", "",
            "The silent video records the game window at 12 fps. A seeded driver sends mouse events through the scene camera "
            "and tile colliders. Setup and restart use the same controller actions as the HUD buttons. "
            "Wave playback is accelerated for review. This run covers the local game on macOS. "
            "The LAN run below uses two copies of the same player.", "",
        ]
        for name, title in SCENES.items():
            report += [f"## {title}", "", f"{times[name]:.1f} s", "", f"![{title}](screens/{name}.png)", ""]
        outcome = f"P{lan['winner']} won" if lan['winner'] else "a draw"
        report += ["## LAN match", "",
                   f"Two macOS player processes ran on one computer. The guest discovered the named room over UDP and joined its advertised TCP port. The host played P1 and the guest played P2. "
                   f"Both finished on turn {lan['turn']} with the same board, roster and result ({outcome}).", "",
                   "![Room found on the Join LAN screen](screens/lan-room-browser.png)", "",
                   "![Guest taking a turn](screens/lan-guest-turn.png)", "",
                   "![Result on the host](screens/lan-host-result.png)", ""]
        report += ["[Separate-interface and blocked-discovery checks](LAN.md)", ""]
        report += ["## Refresh", "", "On a graphics-capable macOS desktop with the pinned Unity editor, Mac build support, "
                   "Python 3, FFmpeg and ffprobe installed, run the following from the repo root.", "",
                   "```sh", "python3 scripts/report.py", "```", "",
                   "Use `--unity /path/to/Unity` to select the editor executable. The command tests a disposable project copy, "
                   "builds a player, then replaces the report, screenshots and MP4 after the match and video checks pass. "
                   "Raw frames and build products are temporary.", ""]
        (output / "README.md").write_text("\n".join(report))
        DEST.mkdir(parents=True, exist_ok=True)
        (DEST / "screens").mkdir(exist_ok=True)
        for source in [*screens.iterdir(), movie, output / "README.md"]:
            target = DEST / source.relative_to(output)
            with tempfile.NamedTemporaryFile(dir=target.parent, delete=False) as staged, source.open("rb") as src:
                shutil.copyfileobj(src, staged)
            os.chmod(staged.name, 0o644)
            os.replace(staged.name, target)
        print(DEST / "README.md")


if __name__ == "__main__":
    main()
