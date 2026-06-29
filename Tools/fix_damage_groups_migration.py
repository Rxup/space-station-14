#!/usr/bin/env python3
"""Re-migrate DamageSpecifier groups->types with even split (matches C# constructor)."""
from __future__ import annotations

import re
import subprocess
import sys
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / "Resources" / "Prototypes"

DAMAGE_GROUPS: dict[str, list[str]] = {
    "Brute": ["Blunt", "Slash", "Piercing", "ArmorPiercing"],
    "Burn": ["Heat", "Shock", "Cold", "Caustic"],
    "Airloss": ["Asphyxiation", "Bloodloss"],
    "Toxin": ["Poison", "Radiation"],
    "Genetic": ["Cellular"],
    "Metaphysical": ["Holy"],
}

DAMAGE_PARENT_KEYS = ("damage", "healing", "damagePerIntensity", "leech", "healAmount")


def split_group_value(value: str, count: int) -> str:
    if count <= 0:
        return value
    d = Decimal(value) / Decimal(count)
    q = d.quantize(Decimal("0.0001"), rounding=ROUND_HALF_UP)
    text = format(q.normalize(), "f")
    if "." in text:
        text = text.rstrip("0").rstrip(".")
    return text


def expand_groups(types_entries: dict[str, str], groups_entries: list[tuple[str, str]]) -> dict[str, str]:
    result = dict(types_entries)
    for group_id, value in groups_entries:
        dtypes = DAMAGE_GROUPS.get(group_id, [group_id])
        per_type = split_group_value(value, len(dtypes))
        for dtype in dtypes:
            if dtype not in result:
                result[dtype] = per_type
    return result


def parse_damage_parent_block(lines: list[str], start: int) -> tuple[int, str, dict[str, str], list[tuple[str, str]]] | None:
    line = lines[start]
    parent_key = None
    parent_indent = None
    for key in DAMAGE_PARENT_KEYS:
        m = re.match(rf"^(\s+){key}:\s*$", line)
        if m:
            parent_key = key
            parent_indent = m.group(1)
            break
    if parent_key is None:
        return None

    types_indent = parent_indent + "  "
    entry_indent = types_indent + "  "
    j = start + 1
    has_types = False
    types_entries: dict[str, str] = {}
    groups_entries: list[tuple[str, str]] = []
    while j < len(lines):
        cur = lines[j]
        if cur.strip() == "":
            j += 1
            continue
        cur_indent = len(cur) - len(cur.lstrip())
        if cur_indent <= len(parent_indent) and not cur.startswith(types_indent):
            break
        if re.match(rf"^{re.escape(types_indent)}types:\s*(\{{.*\}})?\s*$", cur):
            has_types = True
            j += 1
            # empty types: {}
            if cur.strip().endswith("{}"):
                continue
            while j < len(lines):
                tm = re.match(rf"^{re.escape(entry_indent)}(\w+):\s*(-?[\d.]+)", lines[j])
                if tm:
                    types_entries[tm.group(1)] = tm.group(2)
                    j += 1
                    continue
                if lines[j].strip() == "":
                    j += 1
                    continue
                nxt = lines[j]
                if re.match(rf"^{re.escape(types_indent)}types:", nxt):
                    break
                if re.match(rf"^{re.escape(types_indent)}groups:\s*$", nxt):
                    break
                nxt_indent = len(nxt) - len(nxt.lstrip())
                if nxt_indent <= len(parent_indent):
                    break
                if nxt_indent <= len(types_indent) and not nxt.startswith(entry_indent):
                    break
                break
            continue
        if re.match(rf"^{re.escape(types_indent)}groups:\s*$", cur):
            j += 1
            while j < len(lines):
                gm = re.match(rf"^{re.escape(entry_indent)}(\w+):\s*(-?[\d.]+)", lines[j])
                if gm:
                    groups_entries.append((gm.group(1), gm.group(2)))
                    j += 1
                    continue
                if lines[j].strip() == "":
                    j += 1
                    continue
                break
            continue
        tm = re.match(rf"^{re.escape(entry_indent)}(\w+):\s*(-?[\d.]+)", cur)
        if has_types and tm:
            types_entries[tm.group(1)] = tm.group(2)
            j += 1
            continue
        if re.match(rf"^{re.escape(types_indent)}types:", cur):
            continue
        break
    return j, parent_key, types_entries, groups_entries


def render_damage_parent_block(parent_indent: str, parent_key: str, types_entries: dict[str, str]) -> list[str]:
    types_indent = parent_indent + "  "
    entry_indent = types_indent + "  "
    out = [f"{parent_indent}{parent_key}:\n"]
    if not types_entries:
        out.append(f"{types_indent}types: {{}}\n")
        return out
    out.append(f"{types_indent}types:\n")
    for dtype, value in types_entries.items():
        out.append(f"{entry_indent}{dtype}: {value}\n")
    return out


def migrate_text(text: str) -> tuple[str, int]:
    lines = text.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    fixes = 0
    while i < len(lines):
        parsed = parse_damage_parent_block(lines, i)
        if parsed is None:
            out.append(lines[i])
            i += 1
            continue
        end, parent_key, types_entries, groups_entries = parsed
        if groups_entries:
            fixes += 1
            merged = expand_groups(types_entries, groups_entries)
            parent_indent = re.match(r"^(\s+)", lines[i]).group(1)
            out.extend(render_damage_parent_block(parent_indent, parent_key, merged))
            i = end
        else:
            out.append(lines[i])
            i += 1
    return "".join(out), fixes


def extract_damage_blocks(text: str) -> list[tuple[str, dict[str, str]]]:
    """Return ordered list of (parent_key, types_dict) for blocks that have types."""
    lines = text.splitlines(keepends=True)
    blocks: list[tuple[str, dict[str, str]]] = []
    i = 0
    while i < len(lines):
        parsed = parse_damage_parent_block(lines, i)
        if parsed is None:
            i += 1
            continue
        end, parent_key, types_entries, groups_entries = parsed
        if groups_entries:
            types_entries = expand_groups(types_entries, groups_entries)
        elif types_entries:
            pass
        else:
            i = end
            continue
        if types_entries:
            blocks.append((parent_key, types_entries))
        i = end
    return blocks


def patch_current_from_master(current: str, master: str) -> tuple[str, int]:
    master_blocks = extract_damage_blocks(migrate_text(master)[0])
    if not master_blocks:
        return current, 0

    lines = current.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    block_idx = 0
    patched = 0
    while i < len(lines):
        parsed = parse_damage_parent_block(lines, i)
        if parsed is None:
            out.append(lines[i])
            i += 1
            continue
        end, parent_key, _, _ = parsed
        parent_indent = re.match(r"^(\s+)", lines[i]).group(1)
        if block_idx < len(master_blocks) and master_blocks[block_idx][0] == parent_key:
            out.extend(render_damage_parent_block(parent_indent, parent_key, master_blocks[block_idx][1]))
            block_idx += 1
            patched += 1
        else:
            out.extend(lines[i:end])
        i = end
    return "".join(out), patched


def git_show_master(rel: str) -> str | None:
    try:
        return subprocess.check_output(
            ["git", "show", f"master:{rel}"],
            cwd=REPO,
            stderr=subprocess.DEVNULL,
        ).decode("utf-8")
    except subprocess.CalledProcessError:
        return None


def dedupe_damage_blocks(text: str) -> tuple[str, int]:
    """Merge duplicate types: sections within damage parent blocks."""
    lines = text.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    fixes = 0
    while i < len(lines):
        parsed = parse_damage_parent_block(lines, i)
        if parsed is None:
            out.append(lines[i])
            i += 1
            continue
        end, parent_key, types_entries, groups_entries = parsed
        raw_slice = "".join(lines[i:end])
        if groups_entries or raw_slice.count("types:") > 1:
            fixes += 1
            merged = expand_groups(types_entries, groups_entries) if groups_entries else types_entries
            parent_indent = re.match(r"^(\s+)", lines[i]).group(1)
            out.extend(render_damage_parent_block(parent_indent, parent_key, merged))
            i = end
        else:
            out.append(lines[i])
            i += 1
    return "".join(out), fixes


def main() -> int:
    diff_files = subprocess.check_output(
        ["git", "diff", "master", "--name-only", "-G", "groups:", "--", "Resources/Prototypes"],
        cwd=REPO,
        text=True,
    ).strip().splitlines()

    # Also fix files with duplicate types keys under damage blocks
    for path in ROOT.rglob("*.yml"):
        rel = path.relative_to(REPO).as_posix()
        text = path.read_text(encoding="utf-8")
        if "damage:" in text and text.count("types:") > 1:
            if rel not in diff_files:
                diff_files.append(rel)

    total_patched = 0
    changed = 0

    for rel in diff_files:
        if not rel.endswith(".yml"):
            continue
        path = REPO / rel
        if not path.exists():
            continue

        current = path.read_text(encoding="utf-8")
        master = git_show_master(rel)

        if master and any(
            f"{key}:" in master and "groups:" in master for key in DAMAGE_PARENT_KEYS
        ):
            new_text, patched = patch_current_from_master(current, master)
        else:
            new_text, patched = dedupe_damage_blocks(current)

        if patched and new_text != current:
            path.write_text(new_text, encoding="utf-8")
            print(f"{rel}: {patched} block(s)")
            changed += 1
            total_patched += patched

    print(f"Patched {total_patched} blocks in {changed} files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
