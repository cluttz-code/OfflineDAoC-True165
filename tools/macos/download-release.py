#!/usr/bin/env python3
"""Downloads the OfflineDAoC True 1.65 release (the Windows game) on macOS or Linux, for tools/macos/setup-macos.sh.

The release's own downloader (release/Get-OfflineDAoC-True165.ps1) is Windows-only. This does the same new install:
it reads download-manifest.json from the GitHub release, downloads each part with curl (resumable), checks its size and
SHA-256, and unpacks its <RootFolder>/ entries into a NEW folder. Finished parts are remembered, so running it again
continues where it stopped. The Mac needs the release's navigation meshes, the client and its content; the Windows
.exe files in it are simply not used.

Usage:  python3 tools/macos/download-release.py [destination folder]
        (default: ~/OfflineDAoC-True165-release)
Then:   tools/macos/setup-macos.sh --release <that folder>
"""
import hashlib
import json
import os
import shutil
import subprocess
import sys
import zipfile

REPO = "shadowofze/OfflineDAoC-True165"
VERSION = "0.2"
BASE = f"https://github.com/{REPO}/releases/download/v{VERSION}/"


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while chunk := f.read(1 << 22):
            h.update(chunk)
    return h.hexdigest().upper()


def fetch(name, out):
    """curl with retries, continuing a shorter unfinished file."""
    cmd = ["curl", "--location", "--fail", "--retry", "10", "--retry-delay", "5", "--progress-bar",
           "--continue-at", "-", "--output", out, BASE + name]
    if subprocess.call(cmd) != 0:
        sys.exit(f"The download of {name} stopped. Run this again to continue; finished parts are kept.")


def unpack(zip_path, prefix, target):
    """Unpacks <prefix><rel> entries into target, refusing any entry outside it (checked before writing)."""
    root = os.path.realpath(target) + os.sep
    with zipfile.ZipFile(zip_path) as z:
        infos = z.infolist()
        for info in infos:
            name = info.filename.replace("\\", "/")
            if not name.startswith(prefix) or ":" in name or "/../" in "/" + name:
                sys.exit(f"{os.path.basename(zip_path)} has an unexpected layout; nothing was unpacked from it.")
            rel = name[len(prefix):]
            if rel and not os.path.realpath(os.path.join(target, rel)).startswith(root):
                sys.exit(f"{os.path.basename(zip_path)} has an unsafe entry; nothing was unpacked from it.")
        for info in infos:
            rel = info.filename.replace("\\", "/")[len(prefix):]
            if not rel or rel.endswith("/"):
                continue
            path = os.path.join(target, rel)
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with z.open(info) as src, open(path, "wb") as dst:
                shutil.copyfileobj(src, dst, 1 << 20)


def main():
    dest = os.path.abspath(os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/OfflineDAoC-True165-release"))
    cache = dest + ".downloads"
    progress = os.path.join(dest, ".install")
    if os.path.exists(dest) and not os.path.isdir(progress):
        if os.path.isfile(os.path.join(dest, "new-client", "godot", "project.godot")):
            print(f"Already downloaded: {dest}")
            print(f"Next: tools/macos/setup-macos.sh --release \"{dest}\"")
            return
        sys.exit(f"{dest} already exists and is not an unfinished download. Pick another folder.")
    os.makedirs(cache, exist_ok=True)

    manifest_file = os.path.join(cache, "download-manifest.json")
    if os.path.exists(manifest_file):
        os.remove(manifest_file)
    print("Getting the list of files...")
    fetch("download-manifest.json", manifest_file)
    with open(manifest_file, encoding="utf-8") as f:
        manifest = json.load(f)
    if manifest.get("Version") != VERSION or not manifest.get("RootFolder") or not manifest.get("Parts"):
        sys.exit("The release's download list is not what this script expects.")
    parts = manifest["Parts"]
    total = sum(p["Bytes"] for p in parts)
    need = manifest["InstalledBytes"] + max(p["Bytes"] for p in parts) + (1 << 30)
    free = shutil.disk_usage(os.path.dirname(dest)).free
    print(f"OfflineDAoC True 1.65 v{VERSION} -> {dest}")
    print(f"Download: {total / 1e9:.1f} GB in {len(parts)} parts. Installed size: {manifest['InstalledBytes'] / 1e9:.1f} GB.")
    if not os.path.isdir(progress) and free < need:
        sys.exit(f"About {need / 1e9:.0f} GB of free space is needed while it installs ({free / 1e9:.0f} GB free).")
    os.makedirs(progress, exist_ok=True)

    prefix = manifest["RootFolder"] + "/"
    for index, part in enumerate(parts, 1):
        name, size, digest = part["Name"], part["Bytes"], part["SHA256"].upper()
        if not name.endswith(".zip") or "/" in name or len(digest) != 64:
            sys.exit("The download list has an invalid entry.")
        marker = os.path.join(progress, name + ".done")
        if os.path.exists(marker):
            continue
        file = os.path.join(cache, name)
        if not (os.path.exists(file) and os.path.getsize(file) == size and sha256(file) == digest):
            if os.path.exists(file) and os.path.getsize(file) >= size:
                os.remove(file)                     # only a shorter, unfinished file is continued
            print(f"\nDownloading part {index} of {len(parts)} ({size / 1e9:.2f} GB)...")
            fetch(name, file)
            print("Checking it...")
            if os.path.getsize(file) != size or sha256(file) != digest:
                os.remove(file)
                sys.exit("That part did not download correctly. Run this again to retry it; finished parts are kept.")
        print(f"Unpacking part {index} of {len(parts)}...")
        unpack(file, prefix, dest)
        with open(marker, "w") as f:
            f.write(digest)
        os.remove(file)

    for rel in manifest.get("Required", []):
        if not os.path.isfile(os.path.join(dest, rel)):
            sys.exit(f"The download is missing {rel}. Run this again.")
    shutil.rmtree(progress)
    shutil.rmtree(cache, ignore_errors=True)
    print(f"\nDone: {dest}")
    print(f"Next: tools/macos/setup-macos.sh --release \"{dest}\"")


if __name__ == "__main__":
    main()
