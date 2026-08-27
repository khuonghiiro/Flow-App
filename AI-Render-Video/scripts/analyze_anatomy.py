"""
Analyse Master Base anatomy coordinates and generate precision 2D rigging components
with extended convex overlap pivot caps (Khớp xoay bo tròn chống hở khi cử động)
"""

import os
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageOps

ARTIFACT_DIR = r"C:\Users\Admin\.gemini\antigravity-ide\brain\f8ae0858-8e14-4772-9758-b7cfc35ba0ff"
MASTER_IMG = os.path.join(ARTIFACT_DIR, "test6_master_base_1787761895500.jpg")

img = Image.open(MASTER_IMG)
w, h = img.size
print(f"Master image size: {w}x{h}")

# Check green screen mask
arr = np.array(img.convert("RGBA"), dtype=np.float32)
r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
greenness = g - np.maximum(r, b)
is_character = greenness <= 8.0

y_indices, x_indices = np.where(is_character)
min_y, max_y = y_indices.min(), y_indices.max()
min_x, max_x = x_indices.min(), x_indices.max()

print(f"Character bounding box: Top={min_y}, Bottom={max_y}, Left={min_x}, Right={max_x}")
print(f"Character Height={max_y - min_y}, Width={max_x - min_x}")
