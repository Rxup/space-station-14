"""Fix corrupted nested Temperature blocks after bad migration."""
from __future__ import annotations

import re
import sys
from pathlib import Path


def fix_content(text: str) -> tuple[str, int]:
    pattern = re.compile(
        r"^([ \t]*)- type: Temperature\s*\n"
        r"\1    types:\s*\n"
        r"((?:\1      .+\n)+)"
        r"\1- type: TemperatureDamage\s*\n"
        r"\1  heatDamage:\s*\n"
        r"\1  coldDamage: \{\}\s*\n",
        re.MULTILINE,
    )

    def repl(m: re.Match[str]) -> str:
        indent = m.group(1)
        types_block = m.group(2)
        # types_block has Heat/Cold lines at 6 spaces; move under heatDamage.types
        type_lines = []
        for line in types_block.splitlines():
            stripped = line.strip()
            if stripped:
                type_lines.append(f"{indent}      {stripped}")
        return (
            f"{indent}- type: Temperature\n"
            f"{indent}- type: TemperatureDamage\n"
            f"{indent}  heatDamage:\n"
            f"{indent}    types:\n"
            + "\n".join(type_lines)
            + f"\n{indent}  coldDamage: {{}}\n"
        )

    new_text, count = pattern.subn(repl, text)
    return new_text, count


def main(paths: list[str]) -> int:
    total = 0
    for path_str in paths:
        path = Path(path_str)
        text = path.read_text(encoding="utf-8")
        fixed, count = fix_content(text)
        if count:
            path.write_text(fixed, encoding="utf-8", newline="\n")
            print(f"{path}: fixed {count}")
            total += count
    print(f"Total fixes: {total}")
    return 0


if __name__ == "__main__":
    targets = sys.argv[1:] or [
        "Resources/Prototypes/_Backmen/Entities/Mobs/NPCs/TGMC_xeno.yml",
        "Resources/Prototypes/_Backmen/Entities/Mobs/NPCs/blob/blob_tiles.yml",
        "Resources/Prototypes/_Backmen/Entities/Structures/Webbing/Webbing/webs.yml",
        "Resources/Prototypes/_Backmen/Entities/Objects/Weapons/Guns/guns64.yml",
    ]
    raise SystemExit(main(targets))
