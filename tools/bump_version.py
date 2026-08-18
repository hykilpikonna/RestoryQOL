#!/usr/bin/env python3
"""Bump the RestoryQOL version across all three places it lives.

Usage:
    python3 tools/bump_version.py [major|minor|patch]   # bump component (default: patch)
    python3 tools/bump_version.py -v 2.1.0              # set exact version
    python3 tools/bump_version.py                       # same as `patch`

Places updated:
    RestoryQOL.csproj            <Version>...</Version>   (assembly/file version)
    Bootstrap.cs                 MelonInfo(...)           (MelonLoader version)
    Bootstrap.cs                 BepInPlugin(...)         (BepInEx plugin version)
"""

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CSPROJ = ROOT / "RestoryQOL.csproj"
BOOTSTRAP = ROOT / "Bootstrap.cs"

VERSION_RE = re.compile(r"\d+\.\d+\.\d+")
CSPROJ_RE = re.compile(r"<Version>\d+\.\d+\.\d+</Version>")
MELONINFO_RE = re.compile(r'MelonInfo\([^)]*"(\d+\.\d+\.\d+)"')
BEPINPLUGIN_RE = re.compile(r'BepInPlugin\("[^"]+",\s*"[^"]+",\s*"(\d+\.\d+\.\d+)"\)')


def current_version() -> str:
    match = CSPROJ_RE.search(CSPROJ.read_text(encoding="utf-8"))
    if not match:
        sys.exit("error: could not find <Version> in " + str(CSPROJ))
    return VERSION_RE.search(match.group(0)).group(0)


def bump(current: str, part: str) -> str:
    major, minor, patch = (int(x) for x in current.split("."))
    if part == "major":
        return f"{major + 1}.0.0"
    if part == "minor":
        return f"{major}.{minor + 1}.0"
    return f"{major}.{minor}.{patch + 1}"


def apply(version: str) -> None:
    csproj = CSPROJ.read_text(encoding="utf-8")
    new_csproj = CSPROJ_RE.sub(f"<Version>{version}</Version>", csproj, count=1)
    if new_csproj == csproj:
        sys.exit(f"error: failed to update {CSPROJ}")
    CSPROJ.write_text(new_csproj, encoding="utf-8")

    bootstrap = BOOTSTRAP.read_text(encoding="utf-8")
    new_bootstrap = MELONINFO_RE.sub(lambda m: m.group(0).replace(m.group(1), version), bootstrap, count=1)
    new_bootstrap = BEPINPLUGIN_RE.sub(lambda m: m.group(0).replace(m.group(1), version), new_bootstrap, count=1)
    if new_bootstrap == bootstrap:
        sys.exit(f"error: failed to update {BOOTSTRAP}")
    BOOTSTRAP.write_text(new_bootstrap, encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description="Bump the RestoryQOL version (csproj + MelonInfo + BepInPlugin).")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("component", nargs="?", choices=("major", "minor", "patch"), default="patch")
    group.add_argument("-v", "--version", metavar="X.Y.Z", help="set exact version")
    args = parser.parse_args()

    current = current_version()
    new = args.version if args.version else bump(current, args.component)
    apply(new)
    print(f"Bumped {current} -> {new}")


if __name__ == "__main__":
    main()
