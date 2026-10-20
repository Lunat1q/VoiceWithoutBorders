"""Renders the app icon: a microphone with sound waves going both ways.

Usage: python tools/make_icon.py   (writes VoiceWithoutBorders/Assets/app.ico and app.png)
Needs Pillow.
"""
from pathlib import Path

from PIL import Image, ImageDraw

S = 1024  # draw large, downsample for clean anti-aliasing
OUT = Path(__file__).resolve().parent.parent / "VoiceWithoutBorders" / "Assets"
TOP_LEFT, BOTTOM_RIGHT = (57, 73, 171), (0, 172, 193)  # indigo -> teal
WHITE = (255, 255, 255, 255)


def tile() -> Image.Image:
    gradient = Image.new("RGBA", (S, S))
    pixels = gradient.load()
    for y in range(S):
        for x in range(S):
            t = (x + y) / (2 * (S - 1))
            pixels[x, y] = tuple(round(a + (b - a) * t) for a, b in zip(TOP_LEFT, BOTTOM_RIGHT)) + (255,)
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle((32, 32, S - 32, S - 32), radius=220, fill=255)
    gradient.putalpha(mask)
    return gradient


def draw_symbol(image: Image.Image) -> None:
    d = ImageDraw.Draw(image)
    cx = S // 2
    d.rounded_rectangle((cx - 100, 220, cx + 100, 560), radius=100, fill=WHITE)    # capsule
    d.arc((cx - 172, 340, cx + 172, 690), start=0, end=180, fill=WHITE, width=44)  # holder
    d.rectangle((cx - 22, 680, cx + 22, 790), fill=WHITE)                           # stem
    d.rounded_rectangle((cx - 130, 780, cx + 130, 830), radius=25, fill=WHITE)      # base
    cy = 420
    for radius in (290, 390):                                                       # waves, both directions
        box = (cx - radius, cy - radius, cx + radius, cy + radius)
        d.arc(box, start=150, end=210, fill=WHITE, width=44)
        d.arc(box, start=-30, end=30, fill=WHITE, width=44)


def main() -> None:
    image = tile()
    draw_symbol(image)
    OUT.mkdir(parents=True, exist_ok=True)
    icon = image.resize((256, 256), Image.Resampling.LANCZOS)
    icon.save(OUT / "app.png")
    icon.save(OUT / "app.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])


if __name__ == "__main__":
    main()
