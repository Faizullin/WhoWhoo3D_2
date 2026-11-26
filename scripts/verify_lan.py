#!/usr/bin/env python3
"""Verify discovery and direct joining between two Linux Unity players on an isolated LAN."""
import argparse
from contextlib import ExitStack
import json
import os
import platform
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]
IMAGE = "whowhoo-lan-test:bookworm"


def docker(*args):
    return subprocess.check_output(["docker", *args], text=True, timeout=120).strip()


def build(editor, work):
    project = work / "project"
    for name in ("Assets", "Packages", "ProjectSettings"):
        shutil.copytree(ROOT / name, project / name)
    apple_silicon = platform.system() == "Darwin" and platform.machine() == "arm64"
    if apple_silicon:
        # Install the cross compiler only in the disposable verification project.
        manifest = project / "Packages/manifest.json"
        packages = json.loads(manifest.read_text())
        packages["dependencies"]["com.unity.sdk.linux-x86_64"] = "1.1.1"
        packages["dependencies"]["com.unity.toolchain.macos-arm64-linux"] = "1.1.1"
        manifest.write_text(json.dumps(packages))
    capture = project / "Assets/LanVerification"
    (capture / "Editor").mkdir(parents=True)
    shutil.copy(ROOT / "scripts/report/LanMatchRecorder.cs", capture)
    shutil.copy(ROOT / "scripts/report/BuildReportPlayer.cs", capture / "Editor")
    (capture / "LanVerification.asmdef").write_text(json.dumps({
        "name": "LanVerification", "references": ["WhoWhoo3D.Domain", "WhoWhoo3D.Gameplay", "WhoWhoo3D.Networking"]}))
    (capture / "Editor/LanVerificationEditor.asmdef").write_text(json.dumps({
        "name": "LanVerificationEditor", "includePlatforms": ["Editor"]}))
    player = work / "build/Game.x86_64"
    player.parent.mkdir()
    log = work / "build.log"
    result = subprocess.run([editor, "-batchmode", "-quit", "-buildTarget", "Linux64", "-projectPath", project,
                             "-executeMethod", "BuildReportPlayer.Build", "-logFile", log],
                            env=dict(os.environ, WHOWHOO_PLAYER=str(player),
                                     WHOWHOO_LAN_BACKEND="il2cpp" if apple_silicon else "mono"), timeout=1800)
    if result.returncode or not player.is_file():
        raise RuntimeError("Linux player build failed.\n" + log.read_text(errors="replace")[-3000:])
    return player


def scenario(player, directory, blocked):
    directory.mkdir()
    name = "whowhoo-" + uuid.uuid4().hex[:12]
    with ExitStack() as cleanup:
        docker("network", "create", "--internal", name)
        cleanup.callback(docker, "network", "rm", name)
        peers = {}
        addresses = {}
        for role in ("host", "guest"):
            args = ["run", "--detach", "--platform", "linux/amd64", "--network", name,
                    "-v", f"{player.parent}:/game:ro", "-v", f"{directory}:/results",
                    "-e", "SDL_VIDEODRIVER=dummy", "-e", "SDL_AUDIODRIVER=dummy",
                    "-e", "WHOWHOO_LAN_PROBE=1", "-e", "WHOWHOO_FRAMES=/results",
                    "-e", "WHOWHOO_ROOM_NAME=" + name, "-e", "WHOWHOO_LAN_ROLE=" + role]
            cmd = ["/game/" + player.name, "-batchmode", "-nographics", "-job-worker-count", "2",
                   "-logFile", "/dev/stdout"]
            startup = "mkdir -p /dev/input /run/udev/data"
            if blocked and role == "guest":
                args += ["--cap-add", "NET_ADMIN", "-e", "WHOWHOO_DIRECT_HOST=" + addresses["host"]]
                # This INPUT chain belongs only to this disposable guest namespace.
                startup += "; iptables -I INPUT -p udp --dport 47772 -j DROP"
            cmd = ["sh", "-ec", startup + '; exec "$@"', "sh", *cmd]
            peer = docker(*args, IMAGE, *cmd)
            peers[role] = peer
            cleanup.callback(docker, "rm", "-f", peer)
            addresses[role] = docker("inspect", "-f",
                '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}', peer)
        if not all(addresses.values()) or addresses["host"] == addresses["guest"]:
            raise RuntimeError("The players need distinct interface addresses.")

        deadline = time.monotonic() + 115
        while not all((directory / f"{role}.json").is_file() for role in peers):
            for role, peer in peers.items():
                if time.monotonic() > deadline or docker("inspect", "-f", "{{.State.Running}}", peer) != "true":
                    raise RuntimeError(f"{role} failed.\n{docker('logs', '--tail', '50', peer)}")
            time.sleep(0.2)
        if blocked:
            rules = docker("exec", peers["guest"], "iptables", "-nvxL", "INPUT")
            hits = [int(line.split()[0]) for line in rules.splitlines() if "DROP" in line and "dpt:47772" in line]
            if not hits or sum(hits) == 0:
                raise RuntimeError("No discovery datagram reached the guest's drop rule.")
        host, guest = (json.loads((directory / f"{role}.json").read_text()) for role in peers)
        if host != guest or not host["finished"] or host["turn"] < 2:
            raise RuntimeError("Players did not reach identical final board and roster states.")
        (directory / "complete").touch()
        for peer in peers.values():
            if docker("wait", peer) != "0":
                raise RuntimeError(docker("logs", "--tail", "50", peer))
        print(f"{directory.name} passed on turn {host['turn']}, winner {host['winner']}", flush=True)
        return host


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--unity", help="Pinned Unity editor with Linux Build Support")
    source.add_argument("--probe-player", type=Path, help="Reuse a Linux test player built by this script")
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix="whowhoo-lan-") as temp:
        work = Path(temp)
        player = args.probe_player.resolve() if args.probe_player else build(args.unity, work)
        if not player.is_file():
            parser.error("Linux test player is missing")
        subprocess.run(["docker", "build", "--platform", "linux/amd64", "-t", IMAGE, ROOT / "scripts/lan"], check=True)
        discovered = scenario(player, work / "discovery", False)
        direct = scenario(player, work / "direct-with-udp-blocked", True)
        if discovered != direct:
            raise RuntimeError("The two seeded matches finished differently.")


if __name__ == "__main__":
    main()
