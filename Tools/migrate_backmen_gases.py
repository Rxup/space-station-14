import re
from pathlib import Path

path = Path(__file__).resolve().parent.parent / "Resources/Prototypes/_Backmen/Atmospherics/gases.yml"
text = path.read_text(encoding="utf-8")
blocks = re.split(r"(?=- type: gas)", text)
out = []
for block in blocks:
    if not block.strip():
        continue
    if not block.startswith("- type: gas"):
        block = "- type: gas" + block
    m = re.search(r"\n  id: (\S+)", block)
    if not m:
        out.append(block)
        continue
    gas_id = m.group(1)
    slug = re.sub(r"([a-z])([A-Z])", r"\1-\2", gas_id).lower()
    if "abbreviation:" not in block:
        block = re.sub(
            r"(  name: .+\n)",
            rf"\1  abbreviation: gas-{slug}-abbreviation\n",
            block,
            count=1,
        )
    block = block.replace("specificHeat:", "molarHeatCapacity:")
    block = re.sub(
        r"  gasOverlaySprite: (/Textures/[^\n]+)\n  gasOverlayState: (\S+)",
        r"  gasOverlaySprite:\n    sprite: \1\n    state: \2",
        block,
    )
    block = re.sub(
        r"  color: ([^#'\"\n]+)\n",
        lambda m: f"  color: '#{m.group(1).lstrip('#')}'\n",
        block,
    )
    out.append(block if block.endswith("\n") else block + "\n")

path.write_text("".join(out), encoding="utf-8")
print("migrated", path)
