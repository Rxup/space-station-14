#!/usr/bin/env python3
"""Fix common upstream/stable YAML API mismatches for YAMLLinter."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "Resources" / "Prototypes"

DAMAGE_GROUPS: dict[str, list[str]] = {
    "Brute": ["Blunt", "Slash", "Piercing", "ArmorPiercing"],
    "Burn": ["Heat", "Shock", "Cold", "Caustic"],
    "Airloss": ["Asphyxiation", "Bloodloss"],
    "Toxin": ["Poison", "Radiation"],
    "Genetic": ["Cellular"],
    "Metaphysical": ["Holy"],
}


def split_component_block(lines: list[str], start: int) -> tuple[int, list[str]]:
    base_indent = len(lines[start]) - len(lines[start].lstrip())
    end = start + 1
    while end < len(lines):
        line = lines[end]
        if line.strip() == "":
            end += 1
            continue
        indent = len(line) - len(line.lstrip())
        if indent <= base_indent and line.lstrip().startswith("- type:"):
            break
        end += 1
    return end, lines[start:end]


def parse_fields(block: list[str]) -> tuple[str, dict[str, list[str]]]:
    comp_type = ""
    fields: dict[str, list[str]] = {}
    current_key = None
    for line in block[1:]:
        m = re.match(r"^(\s+)(\w+):\s*(.*)$", line)
        if m:
            current_key = m.group(2)
            fields[current_key] = [line]
            continue
        if current_key is not None:
            fields[current_key].append(line)
    m = re.search(r"- type:\s*(\S+)", block[0])
    if m:
        comp_type = m.group(1)
    return comp_type, fields


def rebuild_block(header: str, fields: dict[str, list[str]]) -> list[str]:
    out = [header]
    for key in fields:
        out.extend(fields[key])
    return out


def expand_damage_groups_in_block(block: list[str], damage_key: str = "damage") -> list[str]:
    """Remove groups: under damage: and expand to types."""
    comp_type, fields = parse_fields(block)
    if damage_key not in fields:
        return block

    damage_lines = fields[damage_key]
    text = "".join(damage_lines)
    if "groups:" not in text:
        return block

    indent_match = re.search(r"^(\s+)damage:", damage_lines[0])
    if not indent_match:
        return block
    base_indent = indent_match.group(1)
    types_indent = base_indent + "  "
    entry_indent = types_indent + "  "

    types: dict[str, str] = {}
    in_types = False
    in_groups = False
    for line in damage_lines[1:]:
        if re.match(rf"^{re.escape(types_indent)}types:\s*$", line):
            in_types = True
            in_groups = False
            continue
        if re.match(rf"^{re.escape(types_indent)}groups:\s*$", line):
            in_groups = True
            in_types = False
            continue
        type_m = re.match(rf"^{re.escape(entry_indent)}(\w+):\s*(-?[\d.]+)", line)
        if in_types and type_m:
            types[type_m.group(1)] = type_m.group(2)
            continue
        group_m = re.match(rf"^{re.escape(entry_indent)}(\w+):\s*(-?[\d.]+)", line)
        if in_groups and group_m:
            group_id = group_m.group(1)
            value = group_m.group(2)
            for dtype in DAMAGE_GROUPS.get(group_id, [group_id]):
                types[dtype] = value

    new_damage = [f"{base_indent}damage:\n", f"{types_indent}types:\n"]
    for dtype, value in types.items():
        new_damage.append(f"{entry_indent}{dtype}: {value}\n")

    fields[damage_key] = new_damage
    return rebuild_block(block[0], fields)


def migrate_passive_damage(block: list[str]) -> list[str]:
    comp_type, fields = parse_fields(block)
    if comp_type != "PassiveDamage":
        return block
    fields.pop("damageCap", None)
    block = rebuild_block(block[0], fields)
    return expand_damage_groups_in_block(block)


def migrate_solution_regen_purge(block: list[str]) -> list[str]:
    comp_type, fields = parse_fields(block)
    if comp_type not in ("SolutionRegeneration", "SolutionPurge"):
        return block
    fields.pop("solution", None)
    return rebuild_block(block[0], fields)


def migrate_gas_tank(block: list[str]) -> list[str]:
    comp_type, fields = parse_fields(block)
    if comp_type != "GasTank":
        return block
    if "outputPressure" in fields:
        fields["releasePressure"] = [
            fields["outputPressure"][0].replace("outputPressure", "releasePressure")
        ]
        del fields["outputPressure"]
    return rebuild_block(block[0], fields)


def migrate_damage_specifier_groups(text: str) -> str:
    """Expand standalone groups: blocks under types: sibling (DamageSpecifier in YAML)."""
    lines = text.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    changed = False
    while i < len(lines):
        line = lines[i]
        # pattern: types: ... groups: Brute: X under same parent (4-space indent typical)
        if re.match(r"^(\s+)groups:\s*$", line):
            indent = re.match(r"^(\s+)", line).group(1)
            entry_indent = indent + "  "
            # look back for types: at same indent
            j = i + 1
            group_entries: list[tuple[str, str]] = []
            while j < len(lines):
                m = re.match(rf"^{re.escape(entry_indent)}(\w+):\s*(-?[\d.]+)", lines[j])
                if not m:
                    break
                group_entries.append((m.group(1), m.group(2)))
                j += 1
            if group_entries:
                # find types block above
                k = len(out) - 1
                types_indent = indent
                types_start = -1
                while k >= 0:
                    if re.match(rf"^{re.escape(types_indent)}types:\s*$", out[k]):
                        types_start = k
                        break
                    if out[k].strip() and not out[k].startswith(types_indent):
                        break
                    k -= 1
                if types_start >= 0:
                    changed = True
                    for group_id, value in group_entries:
                        for dtype in DAMAGE_GROUPS.get(group_id, [group_id]):
                            out.append(f"{entry_indent}{dtype}: {value}\n")
                    i = j
                    continue
        out.append(line)
        i += 1
    return "".join(out) if changed else text


def migrate_file(path: Path) -> bool:
    original = path.read_text(encoding="utf-8")
    text = original

    text = text.replace("outputPressure:", "releasePressure:")
    text = re.sub(r"(\s+)- Hypospray\b", r"\1- Injector", text)
    text = re.sub(r"(\s+)- type: Hypospray\b", r"\1- type: Injector", text)
    text = migrate_damage_specifier_groups(text)

    lines = text.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    changed = text != original
    while i < len(lines):
        m = re.match(r"^(\s*)- type: (\S+)\s*$", lines[i])
        if m:
            comp = m.group(2)
            end, block = split_component_block(lines, i)
            new_block = block
            if comp == "PassiveDamage":
                new_block = migrate_passive_damage(block)
            elif comp in ("SolutionRegeneration", "SolutionPurge"):
                new_block = migrate_solution_regen_purge(block)
            elif comp == "GasTank":
                new_block = migrate_gas_tank(block)
            if new_block != block:
                changed = True
            out.extend(new_block)
            i = end
            continue
        out.append(lines[i])
        i += 1

    new_text = "".join(out)
    if new_text != original:
        path.write_text(new_text, encoding="utf-8")
        return True
    return changed


def main() -> int:
    changed = 0
    for path in ROOT.rglob("*.yml"):
        if migrate_file(path):
            changed += 1
            print(path.relative_to(ROOT.parent.parent))
    print(f"Migrated {changed} files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
