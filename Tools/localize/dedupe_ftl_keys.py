#!/usr/bin/env python3
"""Remove duplicate Fluent message IDs within a locale tree.

Keeps the entry in the canonical file (prefer matching en-US path), removes
from other files. Multiline message blocks are removed as a whole.
"""

from __future__ import annotations

import argparse
import re
from collections import defaultdict
from pathlib import Path

KEY_RE = re.compile(r"^([a-zA-Z0-9_-]+)(?:\.([a-zA-Z0-9_-]+))?\s*=")
ATTR_RE = re.compile(r"^\s*\.([a-zA-Z0-9_-]+)\s*=")


def parse_entries(text: str) -> list[tuple[str | None, int, int, str]]:
    """Return list of (key_or_None, start, end, block) for top-level entries.

    Comments/blank runs between entries are attached to the following entry
    when possible; orphan preamble is keyed as None.
    """
    lines = text.splitlines(keepends=True)
    n = len(lines)
    entries: list[tuple[str | None, int, int, str]] = []
    i = 0
    while i < n:
        start = i
        # leading blanks/comments
        while i < n and (not lines[i].strip() or lines[i].lstrip().startswith("#")):
            i += 1
        if i >= n:
            if start < n:
                entries.append((None, start, n, "".join(lines[start:n])))
            break
        m = KEY_RE.match(lines[i])
        if not m:
            # non-entry junk line
            i += 1
            while i < n and not KEY_RE.match(lines[i]):
                if lines[i].strip() and not lines[i].lstrip().startswith("#"):
                    i += 1
                    continue
                break
            entries.append((None, start, i, "".join(lines[start:i])))
            continue
        key = m.group(1)
        if m.group(2):
            key = f"{key}.{m.group(2)}"
        i += 1
        # consume continuation: indented lines, blank lines inside, attributes
        while i < n:
            raw = lines[i]
            stripped = raw.strip()
            if not stripped:
                # peek ahead: blank belonging to this entry vs next
                j = i + 1
                while j < n and not lines[j].strip():
                    j += 1
                if j < n and (KEY_RE.match(lines[j]) or lines[j].lstrip().startswith("#")):
                    break
                i += 1
                continue
            if KEY_RE.match(raw):
                break
            if raw.startswith(" ") or raw.startswith("\t") or ATTR_RE.match(raw):
                i += 1
                continue
            if stripped.startswith("#"):
                break
            # unexpected — stop
            break
        entries.append((key, start, i, "".join(lines[start:i])))
    return entries


def collect(locale_dir: Path) -> dict[str, list[tuple[Path, str]]]:
    """key -> [(abs_path, rel_posix), ...]"""
    locs: dict[str, list[tuple[Path, str]]] = defaultdict(list)
    for ftl in locale_dir.rglob("*.ftl"):
        rel = ftl.relative_to(locale_dir).as_posix()
        text = ftl.read_text(encoding="utf-8", errors="replace")
        for key, _s, _e, _b in parse_entries(text):
            if key:
                locs[key].append((ftl, rel))
    return locs


def prefer_score(rel: str) -> tuple:
    """Higher is better when choosing which duplicate to keep."""
    parts = rel.lower().split("/")
    score = 0
    # Prefer ss14-ru prototype tree over loose backmen/ trees
    if rel.startswith("ss14-ru/"):
        score += 1000
    if "/_backmen/" in rel or rel.startswith("ss14-ru/prototypes/_backmen"):
        score += 200
    if "/backmen/" in rel and "/_backmen/" not in rel:
        score -= 50
    if rel.startswith("backmen/"):
        score -= 100
    # Prefer newer nested paths over legacy flat ones
    if "/wallmountmachines/" in rel or "/switches/" in rel or "/storage/" in rel:
        score += 30
    if rel.endswith("drinks_metamorphic.ftl") or "drinks_bottles_glass" in rel or "drinks_bottles_plastic" in rel:
        score += 40
    if rel.endswith("/drinks.ftl") and "metamorphic" not in rel:
        score -= 20
    if rel.endswith("plushies.ftl"):
        score += 20
    if "/chemistry/" in rel:
        score += 10
    # Deeper paths slightly preferred (more specific)
    score += len(parts)
    # Stable tie-break: shorter path string, then lexicographic
    return (score, -len(rel), rel)


def choose_keeper(
    key: str,
    ru_files: list[str],
    en_map: dict[str, list[str]],
) -> str:
    en_files = list(dict.fromkeys(en_map.get(key, [])))
    if len(en_files) == 1 and en_files[0] in ru_files:
        return en_files[0]
    if len(en_files) == 1:
        # en owner not among ru dups — still prefer closest match
        target = en_files[0]
        if target in ru_files:
            return target
    ranked = sorted(ru_files, key=prefer_score, reverse=True)
    return ranked[0]


def remove_keys_from_file(path: Path, keys_to_remove: set[str]) -> int:
    text = path.read_text(encoding="utf-8", errors="replace")
    entries = parse_entries(text)
    kept: list[str] = []
    removed = 0
    for key, _s, _e, block in entries:
        if key and key in keys_to_remove:
            removed += 1
            continue
        kept.append(block)
    new_text = "".join(kept)
    # normalize trailing whitespace
    new_text = new_text.rstrip() + ("\n" if new_text.strip() else "")
    if removed:
        path.write_text(new_text, encoding="utf-8")
    return removed


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--locale", default="Resources/Locale/ru-RU")
    ap.add_argument("--en-locale", default="Resources/Locale/en-US")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--also-en", action="store_true", help="Also dedupe en-US")
    args = ap.parse_args()

    root = Path(".")
    ru_dir = root / args.locale
    en_dir = root / args.en_locale

    en_map = collect(en_dir)
    # flatten to key -> [rel]
    en_rels = {k: [rel for _p, rel in v] for k, v in en_map.items()}

    def dedupe_locale(locale_dir: Path, use_en: bool) -> None:
        locs = collect(locale_dir)
        dups = {k: v for k, v in locs.items() if len({rel for _p, rel in v}) > 1}
        print(f"{locale_dir}: {len(dups)} duplicate keys")

        # file_rel -> keys to remove
        removals: dict[str, set[str]] = defaultdict(set)
        keepers: dict[str, str] = {}
        for key, hits in dups.items():
            files = list(dict.fromkeys(rel for _p, rel in hits))
            keeper = choose_keeper(key, files, en_rels if use_en else {})
            keepers[key] = keeper
            for rel in files:
                if rel != keeper:
                    removals[rel].add(key)

        total_rm = 0
        for rel, keys in sorted(removals.items()):
            path = locale_dir / rel
            if not path.exists():
                print(f"  missing {rel}")
                continue
            if args.dry_run:
                print(f"  would remove {len(keys)} keys from {rel}")
                total_rm += len(keys)
                continue
            n = remove_keys_from_file(path, keys)
            total_rm += n
            print(f"  removed {n} from {rel}")
            # delete empty files
            if path.exists() and not path.read_text(encoding="utf-8").strip():
                path.unlink()
                print(f"  deleted empty {rel}")

        print(f"  total removals: {total_rm}")

        # verify
        locs2 = collect(locale_dir)
        dups2 = {k: v for k, v in locs2.items() if len({rel for _p, rel in v}) > 1}
        print(f"  remaining dups: {len(dups2)}")

    dedupe_locale(ru_dir, use_en=True)
    if args.also_en:
        dedupe_locale(en_dir, use_en=False)


if __name__ == "__main__":
    main()
