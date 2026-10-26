"""Render the simple vector thermometer as a multi-resolution Windows icon."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent.parent
(root / 'validation').mkdir(exist_ok=True)
scale = 8
image = Image.new('RGBA', (64 * scale, 64 * scale))
draw = ImageDraw.Draw(image)
teal = '#16747e'

def box(values):
    return tuple(round(value * scale) for value in values)

draw.rounded_rectangle(box((3, 3, 61, 61)), radius=14 * scale, fill=teal)
draw.rounded_rectangle(box((25, 10, 39, 44)), radius=7 * scale, fill='white')
draw.ellipse(box((20, 35, 44, 59)), fill='white')
draw.rounded_rectangle(box((29.5, 18.5, 34.5, 45.5)), radius=2.5 * scale, fill=teal)
draw.ellipse(box((26, 41, 38, 53)), fill=teal)

image.save(root / 'assets' / 'cooler-panel.ico', sizes=[(n, n) for n in (16, 20, 24, 32, 48, 64, 128, 256)])
image.resize((128, 128), Image.Resampling.LANCZOS).save(root / 'validation' / 'icon-preview.png')
