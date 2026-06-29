import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent / "Resources" / "Prototypes"
pat = re.compile(r"^(\s*- type: Injurable)\n(damageContainer:)", re.M)
count = 0
for path in root.rglob("*.yml"):
    text = path.read_text(encoding="utf-8")

    def repl(match: re.Match[str]) -> str:
        indent = re.match(r"^(\s*)", match.group(1)).group(1)
        field_indent = indent + "  "
        return f"{match.group(1)}\n{field_indent}{match.group(2)}"

    new_text = pat.sub(repl, text)
    if new_text != text:
        path.write_text(new_text, encoding="utf-8")
        count += 1
print(f"fixed {count}")
