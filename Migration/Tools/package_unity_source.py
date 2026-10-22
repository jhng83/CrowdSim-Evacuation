#!/usr/bin/env python3
"""Create a short-root Unity source ZIP from Git, never Library/cache/build files.

Default exports the committed HEAD. --worktree is a labeled cloud validation
candidate and must not be presented as a committed release. This does not build Unity.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import stat
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[2]


def selected(name: str) -> bool:
    return name.startswith(("UnityProject/Assets/", "UnityProject/Packages/", "UnityProject/ProjectSettings/",
                            "docs/unity-migration/")) or name in {"UnityProject/README.md", "UnityProject/AGENTS.md"}


def package(output: Path, worktree: bool = False) -> None:
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    files: dict[str, bytes] = {}
    if worktree:
        names = subprocess.check_output(["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard"], cwd=ROOT).decode().split("\0")
        for name in names:
            if name and selected(name):
                path = ROOT / name
                if path.is_symlink():
                    raise ValueError("Source package cannot contain links: " + name)
                files[name] = path.read_bytes()
    else:
        raw = subprocess.check_output(["git", "archive", "--format=zip", "HEAD", "UnityProject", "docs/unity-migration"], cwd=ROOT)
        with zipfile.ZipFile(io.BytesIO(raw)) as archive:
            for item in archive.infolist():
                if item.is_dir() or not selected(item.filename):
                    continue
                if stat.S_ISLNK(item.external_attr >> 16):
                    raise ValueError("Source package cannot contain links: " + item.filename)
                files[item.filename] = archive.read(item)
    for name in files:
        parts = PurePosixPath(name).parts
        if ".." in parts or PurePosixPath(name).is_absolute() or "\\" in name:
            raise ValueError("Invalid archive path: " + name)
        if any(p.lower() in {"library", "temp", "obj", "bin", "build", "builds", "logs", "usersettings", "packagecache"} for p in parts):
            raise ValueError("Generated/cache file in source package: " + name)
    for required in ["UnityProject/Assets/CrowdSim/Scenes/Bootstrap.unity", "UnityProject/Packages/manifest.json",
                     "UnityProject/ProjectSettings/ProjectVersion.txt", "UnityProject/Assets/CrowdSim/Editor/PlayerBuild.cs",
                     "UnityProject/Assets/StreamingAssets/Documentation/USER_GUIDE.txt", "docs/unity-migration/M11_LOCAL_TESTING.md"]:
        if required not in files:
            raise ValueError("Incomplete M11 source: " + required)
    receipt = {"kind": "Unity source, not an executable player", "commit": commit,
               "worktree_candidate": worktree, "files_sha256": {p: hashlib.sha256(data).hexdigest() for p, data in sorted(files.items())}}
    files["SOURCE_RECEIPT.json"] = (json.dumps(receipt, indent=2) + "\n").encode()
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("xb") as target, zipfile.ZipFile(target, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, data in sorted(files.items()):
            item = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
            item.compress_type = zipfile.ZIP_DEFLATED
            item.external_attr = 0o100644 << 16
            archive.writestr(item, data)
    with zipfile.ZipFile(output) as archive:
        assert archive.testzip() is None
        assert all(hashlib.sha256(archive.read(name)).hexdigest() == digest for name, digest in receipt["files_sha256"].items())
    print(f"PASS: {len(files)} files in {output}; source only; commit={commit}; worktree_candidate={worktree}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--worktree", action="store_true")
    args = parser.parse_args()
    package(args.output, args.worktree)
