"""Generate TauCeti-styled pressure indicator overlays for gas canisters."""
from PIL import Image, ImageDraw

# Dark strip on TauCeti canister body (plasma / generic)
INDICATOR_BOX = (13, 12, 20, 15)  # left, top, right, bottom (inclusive)


def blank_frame(size=(32, 32)):
    return Image.new('RGBA', size, (0, 0, 0, 0))


def draw_glow_rect(frame: Image.Image, color, box=INDICATOR_BOX):
    draw = ImageDraw.Draw(frame)
    x0, y0, x1, y1 = box
    # soft outer glow
    glow = tuple(min(255, c + 40) for c in color[:3]) + (90,)
    draw.rectangle((x0 - 1, y0 - 1, x1 + 1, y1 + 1), fill=glow)
    draw.rectangle((x0, y0, x1, y1), fill=color)


def save_can_o0(path):
    # animated blink: off frame + lit frame (64x32)
    sheet = Image.new('RGBA', (64, 32), (0, 0, 0, 0))
    off = blank_frame()
    on = blank_frame()
    draw_glow_rect(on, (220, 55, 55, 255))
    # dim off-state hint
    draw = ImageDraw.Draw(off)
    x0, y0, x1, y1 = INDICATOR_BOX
    draw.rectangle((x0, y0, x1, y1), fill=(70, 35, 35, 180))
    sheet.paste(off, (0, 0))
    sheet.paste(on, (32, 0))
    sheet.save(path)


def save_can_o(path, color):
    frame = blank_frame()
    draw_glow_rect(frame, color)
    frame.save(path)


def main():
    rsi = 'Resources/Textures/Structures/Storage/canister.rsi'
    save_can_o0(f'{rsi}/can-o0.png')
    save_can_o(f'{rsi}/can-o1.png', (235, 60, 60, 255))
    save_can_o(f'{rsi}/can-o2.png', (240, 210, 60, 255))
    save_can_o(f'{rsi}/can-o3.png', (70, 230, 90, 255))
    print('wrote can-o0..can-o3')


if __name__ == '__main__':
    main()
