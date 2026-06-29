#!/usr/bin/env python3
"""Restore {...} and {select} from en-US using complex expressions only."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RU_LOCALE = ROOT / "Resources" / "Locale" / "ru-RU"
EN_LOCALE = ROOT / "Resources" / "Locale" / "en-US"

COMPLEX_MARKERS = (
    "(", "->", "TOSTRING", "CAPITALIZE", "LOC(", "POWER", "PRESSURE", "NATURAL",
    "INDEFINITE", "GENDER", "CONJUGATE", "POSS-ADJ", "SUBJECT", "REFLEXIVE", "THE(",
)


def extract_expressions(text: str) -> list[str]:
    expressions: list[str] = []
    i = 0
    while i < len(text):
        if text[i] == "{":
            depth = 0
            start = i
            while i < len(text):
                ch = text[i]
                if ch == "{":
                    depth += 1
                elif ch == "}":
                    depth -= 1
                    if depth == 0:
                        expressions.append(text[start : i + 1])
                        break
                i += 1
        i += 1
    return expressions


def complex_expressions(en_value: str) -> list[str]:
    return [e for e in extract_expressions(en_value) if any(m in e for m in COMPLEX_MARKERS)]


def fix_value(ru_value: str, en_value: str) -> tuple[str, bool]:
    if "{...}" not in ru_value and "{select}" not in ru_value:
        return ru_value, False

    en_exprs = complex_expressions(en_value)
    expr_iter = iter(en_exprs)
    out: list[str] = []
    i = 0
    changed = False

    while i < len(ru_value):
        if ru_value.startswith("{...}", i):
            try:
                out.append(next(expr_iter))
            except StopIteration:
                out.append("{...}")
            else:
                changed = True
            i += 5
            continue
        if ru_value.startswith("{select}", i):
            try:
                out.append(next(expr_iter))
            except StopIteration:
                out.append("{select}")
            else:
                changed = True
            i += 8
            continue
        if ru_value[i] == "{":
            depth = 0
            start = i
            while i < len(ru_value):
                ch = ru_value[i]
                if ch == "{":
                    depth += 1
                elif ch == "}":
                    depth -= 1
                    if depth == 0:
                        out.append(ru_value[start : i + 1])
                        break
                i += 1
            i += 1
            continue
        out.append(ru_value[i])
        i += 1

    return "".join(out), changed


def parse_ftl_entries(content: str) -> list[tuple[str, str]]:
    entries: list[tuple[str, str]] = []
    lines = content.splitlines(keepends=True)
    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.lstrip()
        if not stripped or stripped.startswith("#"):
            i += 1
            continue
        match = re.match(r"^([A-Za-z0-9_.-]+)\s*=\s*(.*)$", stripped)
        if not match:
            i += 1
            continue
        key = match.group(1)
        value_parts = [match.group(2)]
        i += 1
        while i < len(lines):
            nxt = lines[i]
            if re.match(r"^[A-Za-z0-9_.-]+\s*=", nxt):
                break
            if nxt.strip() == "" and i + 1 < len(lines) and re.match(r"^[A-Za-z0-9_.-]+\s*=", lines[i + 1]):
                break
            value_parts.append(nxt)
            i += 1
        entries.append((key, "".join(value_parts).rstrip("\n")))
    return entries


def replace_first_entry(content: str, key: str, new_value: str) -> str:
    pattern = re.compile(
        rf"^([ \t]*{re.escape(key)}\s*=\s*)(.*?)(?=^[A-Za-z0-9_.-]+\s*=|\Z)",
        re.MULTILINE | re.DOTALL,
    )
    return pattern.sub(lambda m: f"{m.group(1)}{new_value}\n", content, count=1)


def process_file(ru_path: Path) -> int:
    rel = ru_path.relative_to(RU_LOCALE)
    en_path = EN_LOCALE / rel
    if not en_path.exists():
        return 0

    ru_content = ru_path.read_text(encoding="utf-8")
    if "{...}" not in ru_content and "{select}" not in ru_content:
        return 0

    en_map = dict(parse_ftl_entries(en_path.read_text(encoding="utf-8")))
    fixes: dict[str, str] = {}
    count = 0
    for key, ru_value in parse_ftl_entries(ru_content):
        if key not in en_map:
            continue
        new_value, changed = fix_value(ru_value, en_map[key])
        if changed:
            fixes[key] = new_value
            count += 1

    if not fixes:
        return 0

    new_content = ru_content
    for key, new_value in fixes.items():
        new_content = replace_first_entry(new_content, key, new_value)

    ru_path.write_text(new_content, encoding="utf-8")
    print(f"Fixed {count} entries in {rel}")
    return count


def main() -> int:
    targets = sys.argv[1:] or [str(p) for p in RU_LOCALE.rglob("*.ftl")]
    total = 0
    for target in targets:
        path = Path(target)
        if not path.is_absolute():
            path = ROOT / path
        if path.is_file():
            total += process_file(path)
    print(f"Total fixed entries: {total}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
