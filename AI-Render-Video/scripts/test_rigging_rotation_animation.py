"""
Simulate 2D puppet rotation of arm and leg limbs to verify seamless overlap rotation
"""

import os
import sys
import shutil
import numpy as np
from PIL import Image

if sys.platform.startswith("win"):
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ARTIFACT_DIR = r"C:\Users\Admin\.gemini\antigravity-ide\brain\f8ae0858-8e14-4772-9758-b7cfc35ba0ff"
SRC_DIR = os.path.join(PROJECT_ROOT, "public", "test6", "02_linh_kien_tach_roi_transparent_png")
OUT_DIR = os.path.join(PROJECT_ROOT, "public", "test6", "03_kiem_chung_lap_rap_layers")

# Load pieces
dau = Image.open(os.path.join(SRC_DIR, "01_dau.png"))
than = Image.open(os.path.join(SRC_DIR, "02_than_co.png"))
bap_tay_l = Image.open(os.path.join(SRC_DIR, "03a_bap_tay_trai.png"))
cang_tay_l = Image.open(os.path.join(SRC_DIR, "04a_cang_tay_trai.png"))
ban_tay_l = Image.open(os.path.join(SRC_DIR, "05a_ban_tay_trai.png"))
bap_tay_r = Image.open(os.path.join(SRC_DIR, "03b_bap_tay_phai.png"))
cang_tay_r = Image.open(os.path.join(SRC_DIR, "04b_cang_tay_phai.png"))
ban_tay_r = Image.open(os.path.join(SRC_DIR, "05b_ban_tay_phai.png"))
dui_l = Image.open(os.path.join(SRC_DIR, "06a_dui_trai.png"))
cang_chan_l = Image.open(os.path.join(SRC_DIR, "07a_cang_chan_trai.png"))
ban_chan_l = Image.open(os.path.join(SRC_DIR, "08a_ban_chan_trai.png"))
dui_r = Image.open(os.path.join(SRC_DIR, "06b_dui_phai.png"))
cang_chan_r = Image.open(os.path.join(SRC_DIR, "07b_cang_chan_phai.png"))
ban_chan_r = Image.open(os.path.join(SRC_DIR, "08b_ban_chan_phai.png"))

def rotate_about_pivot(img, angle_deg, pivot_xy):
    """Rotate image around a specific pivot point without clipping"""
    px, py = pivot_xy
    w, h = img.size
    # Create an expanded canvas centered on pivot
    max_dim = int(np.hypot(w, h) * 2)
    big = Image.new("RGBA", (max_dim, max_dim), (0, 0, 0, 0))
    cx, cy = max_dim // 2, max_dim // 2
    big.paste(img, (cx - px, cy - py), img)
    rot = big.rotate(-angle_deg, resample=Image.Resampling.BICUBIC, center=(cx, cy))
    return rot, cx, cy

# Render pose with rotated limbs (e.g. arm wave, leg step)
canvas = Image.new("RGBA", (768, 1376), (0, 0, 0, 0))

# 1. Legs (Resting pose)
canvas.paste(dui_l, (297, 839), dui_l)
canvas.paste(cang_chan_l, (284, 1014), cang_chan_l)
canvas.paste(ban_chan_l, (274, 1209), ban_chan_l)

canvas.paste(dui_r, (378, 839), dui_r)
canvas.paste(cang_chan_r, (392, 1014), cang_chan_r)
canvas.paste(ban_chan_r, (397, 1209), ban_chan_r)

# 2. Right Arm (Character's left - resting)
canvas.paste(bap_tay_r, (447, 499), bap_tay_r)
canvas.paste(cang_tay_r, (537, 674), cang_tay_r)
canvas.paste(ban_tay_r, (602, 769), ban_tay_r)

# 3. Left Arm (Rotated up in Wave Pose! 35 degrees up)
# Rotate upper arm around shoulder (px=40, py=20)
rot_upper, ucx, ucy = rotate_about_pivot(bap_tay_l, 35, (40, 20))
canvas.paste(rot_upper, (280 - ucx, 515 - ucy), rot_upper)

# 4. Torso on top of limb roots
canvas.paste(than, (252, 454), than)

# 5. Head on top of neck
canvas.paste(dau, (142, 97), dau)

out_pose = os.path.join(OUT_DIR, "rigging_rotation_wave_test.png")
canvas.save(out_pose, "PNG")

# Also copy to artifacts directory for user viewing
shutil.copy2(out_pose, os.path.join(ARTIFACT_DIR, "rigging_rotation_wave_test.png"))

print(f"✅ Rotation wave test passed, saved to: {out_pose}")
