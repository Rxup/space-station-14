"""Generate themed HUD slot icons for Backmen underclothing slots."""
from __future__ import annotations

from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
INTERFACE = ROOT / "Resources/Textures/Interface"

UNDER_SLOTS = ("socks", "underpants", "undershirt")
THEMES = ("Plasmafire", "Slimecore", "Clockwork", "Retro", "Ashen", "Minimalist")
ICON_REFERENCE_SLOT = "mask"

# Background / frame colors used by the Default underclothing slot art.
UNDER_BG_COLORS = {(15, 18, 21), (22, 25, 28)}
UNDER_BORDER_COLORS = {(36, 34, 48)}


def luminance(rgb: tuple[int, int, int]) -> float:
    return 0.299 * rgb[0] + 0.587 * rgb[1] + 0.114 * rgb[2]


def extract_underclothing_icon_pixels(
    source: Image.Image,
) -> list[tuple[int, int, tuple[int, int, int, int]]]:
    pixels: list[tuple[int, int, tuple[int, int, int, int]]] = []

    for y in range(source.height):
        for x in range(source.width):
            pixel = source.getpixel((x, y))
            if pixel[3] < 50:
                continue

            rgb = pixel[:3]
            if rgb in UNDER_BG_COLORS or rgb in UNDER_BORDER_COLORS:
                continue

            pixels.append((x, y, pixel))

    return pixels


def extract_theme_icon_colors(
    theme_slot: Image.Image,
    theme_background: Image.Image,
) -> list[tuple[int, int, int]]:
    counts: dict[tuple[int, int, int], int] = {}

    for y in range(theme_slot.height):
        for x in range(theme_slot.width):
            slot_pixel = theme_slot.getpixel((x, y))
            background_pixel = theme_background.getpixel((x, y))

            if slot_pixel[3] < 50 or slot_pixel == background_pixel:
                continue

            rgb = slot_pixel[:3]
            counts[rgb] = counts.get(rgb, 0) + 1

    return [rgb for rgb, _ in sorted(counts.items(), key=lambda item: -item[1])]


def build_color_map(
    source_icon_colors: list[tuple[int, int, int]],
    theme_icon_colors: list[tuple[int, int, int]],
) -> dict[tuple[int, int, int], tuple[int, int, int]]:
    if not source_icon_colors:
        return {}

    if not theme_icon_colors:
        return {color: color for color in source_icon_colors}

    source_sorted = sorted(source_icon_colors, key=luminance)
    theme_sorted = sorted(theme_icon_colors, key=luminance)

    if len(theme_sorted) == 1:
        return {color: theme_sorted[0] for color in source_sorted}

    mapped: dict[tuple[int, int, int], tuple[int, int, int]] = {}
    last_index = len(theme_sorted) - 1

    for index, source_color in enumerate(source_sorted):
        if len(source_sorted) == 1:
            theme_index = 0
        else:
            theme_index = round(index / (len(source_sorted) - 1) * last_index)

        mapped[source_color] = theme_sorted[theme_index]

    return mapped


def recolor_slot(
    source: Image.Image,
    theme_background: Image.Image,
    theme_reference: Image.Image,
) -> Image.Image:
    source = source.convert("RGBA")
    theme_background = theme_background.convert("RGBA")
    theme_reference = theme_reference.convert("RGBA")

    output = theme_background.copy()
    icon_pixels = extract_underclothing_icon_pixels(source)
    source_icon_colors = list({pixel[:3] for _, _, pixel in icon_pixels})
    theme_icon_colors = extract_theme_icon_colors(theme_reference, theme_background)
    color_map = build_color_map(source_icon_colors, theme_icon_colors)

    for x, y, pixel in icon_pixels:
        mapped = color_map.get(pixel[:3])
        if mapped is None:
            mapped = theme_icon_colors[0] if theme_icon_colors else pixel[:3]

        output.putpixel((x, y), (*mapped, pixel[3]))

    return output


def generate_theme(theme: str) -> None:
    theme_background = Image.open(INTERFACE / theme / "SlotBackground.png")
    theme_reference = Image.open(INTERFACE / theme / "Slots" / f"{ICON_REFERENCE_SLOT}.png")
    out_dir = INTERFACE / theme / "Slots"
    out_dir.mkdir(parents=True, exist_ok=True)

    for slot in UNDER_SLOTS:
        source = Image.open(INTERFACE / "Default" / "Slots" / f"{slot}.png")
        recolor_slot(source, theme_background, theme_reference).save(out_dir / f"{slot}.png")
        print(f"Wrote {out_dir / f'{slot}.png'}")


def main() -> None:
    for theme in THEMES:
        generate_theme(theme)


if __name__ == "__main__":
    main()
