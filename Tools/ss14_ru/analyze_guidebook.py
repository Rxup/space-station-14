#!/usr/bin/env python3
"""Audit Guidebook XML translation status in Resources/ServerInfo/Guidebook."""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

CYRILLIC = re.compile(r"[а-яА-ЯёЁ]")
DEFAULT_BASE_COMMIT = "2d84571fcb2"
SERVER_RULES_PREFIX = "ServerRules/"

REPO_ROOT = Path(__file__).resolve().parents[2]
GUIDEBOOK_ROOT = REPO_ROOT / "Resources" / "ServerInfo" / "Guidebook"


def git_show(commit: str, path: str) -> str | None:
    rel = path.replace("\\", "/")
    try:
        return subprocess.check_output(
            ["git", "-C", str(REPO_ROOT), "show", f"{commit}:{rel}"],
            text=True,
            encoding="utf-8",
            stderr=subprocess.DEVNULL,
        )
    except subprocess.CalledProcessError:
        return None


def list_guidebook_files() -> list[str]:
    output = subprocess.check_output(
        [
            "git",
            "-C",
            str(REPO_ROOT),
            "ls-tree",
            "-r",
            "--name-only",
            "HEAD",
            "Resources/ServerInfo/Guidebook",
        ],
        text=True,
        encoding="utf-8",
    )
    return [
        line.replace("Resources/ServerInfo/Guidebook/", "")
        for line in output.strip().splitlines()
        if line.endswith(".xml")
    ]


def classify_file(rel: str, base_commit: str) -> str:
    path = GUIDEBOOK_ROOT / rel
    current = path.read_text(encoding="utf-8")
    has_cyrillic = bool(CYRILLIC.search(current))

    if has_cyrillic:
        return "translated"

    old = git_show(base_commit, f"Resources/ServerInfo/Guidebook/{rel}")
    if old is None:
        return "new_untranslated"

    if CYRILLIC.search(old):
        return "lost_translation"

    return "never_translated"


def is_in_scope(rel: str, include_server_rules: bool) -> bool:
    if include_server_rules:
        return True
    return not rel.replace("\\", "/").startswith(SERVER_RULES_PREFIX)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--base-commit",
        default=DEFAULT_BASE_COMMIT,
        help=f"Commit to compare against (default: {DEFAULT_BASE_COMMIT})",
    )
    parser.add_argument(
        "--include-server-rules",
        action="store_true",
        help="Include WizDen ServerRules in the report",
    )
    args = parser.parse_args()

    files = sorted(list_guidebook_files())
    buckets: dict[str, list[str]] = {
        "translated": [],
        "lost_translation": [],
        "never_translated": [],
        "new_untranslated": [],
    }

    for rel in files:
        if not is_in_scope(rel, args.include_server_rules):
            continue
        buckets[classify_file(rel, args.base_commit)].append(rel)

    in_scope = sum(len(v) for v in buckets.values())
    untranslated = (
        len(buckets["lost_translation"])
        + len(buckets["never_translated"])
        + len(buckets["new_untranslated"])
    )

    print(f"Guidebook audit (base commit: {args.base_commit})")
    print(f"In scope: {in_scope} files")
    print(f"  translated:         {len(buckets['translated'])}")
    print(f"  lost_translation:   {len(buckets['lost_translation'])}")
    print(f"  never_translated:   {len(buckets['never_translated'])}")
    print(f"  new_untranslated:   {len(buckets['new_untranslated'])}")
    print(f"  needs work:         {untranslated}")
    print()

    for label in ("lost_translation", "never_translated", "new_untranslated"):
        items = buckets[label]
        if not items:
            continue
        print(f"=== {label} ({len(items)}) ===")
        for rel in items:
            print(f"  {rel}")
        print()

    return 1 if untranslated else 0


if __name__ == "__main__":
    sys.exit(main())
