"""Turn Poly Haven PBR textures into tintable ground textures for the game.

The albedo keeps its detail (blades, stones, cracks) but loses most of its own colour and is scaled so its average
brightness is about 0.85. The game then tints it with the surface colour from the brand palette (and the seasons),
so a photo texture still reads in the game's warm colours.

    python3 tools/textures/prep_ground.py SRC_DIR DST_DIR name:keep_colour[:size] ...

keep_colour 0..1 is how much of the photo's own colour stays. Normal maps (OpenGL convention) are copied resized.
"""
import sys, os
import numpy as np
from PIL import Image

src, dst, *specs = sys.argv[1:]
for spec in specs:
    parts = spec.split(':')
    name, keep = parts[0], float(parts[1])
    size = int(parts[2]) if len(parts) > 2 else 1024
    a = np.asarray(Image.open(os.path.join(src, f'{name}_Diffuse.jpg')).convert('RGB').resize((size, size), Image.LANCZOS), dtype=np.float32) / 255
    lum = (a * [0.299, 0.587, 0.114]).sum(axis=2, keepdims=True)
    mixed = lum + (a - lum) * keep
    mixed *= 0.85 / max(mixed.mean(), 1e-3)
    Image.fromarray((np.clip(mixed, 0, 1) * 255).astype(np.uint8)).save(os.path.join(dst, f'{name}_albedo.jpg'), quality=90)
    Image.open(os.path.join(src, f'{name}_nor_gl.jpg')).convert('RGB').resize((size, size), Image.LANCZOS).save(os.path.join(dst, f'{name}_normal.jpg'), quality=92)
    print(name, 'ok')
