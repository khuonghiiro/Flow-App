"""
Export Smooth Anime Mannequin & Decomposed Parts (Head, Torso+Neck, Upper Arm, Forearm, Hand, Thigh, Shin, Foot) to public/test5
"""

import os
import sys
import shutil
import json
import numpy as np
from PIL import Image, ImageOps

if sys.platform.startswith("win"):
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ARTIFACT_DIR = r"C:\Users\Admin\.gemini\antigravity-ide\brain\f8ae0858-8e14-4772-9758-b7cfc35ba0ff"
DEST_DIR = os.path.join(PROJECT_ROOT, "public", "test5")

DIR_MASTER = os.path.join(DEST_DIR, "00_nhan_vat_goc_master")
DIR_CHROMA = os.path.join(DEST_DIR, "01_anh_goc_linh_kien")
DIR_TRANSPARENT = os.path.join(DEST_DIR, "02_linh_kien_tach_roi_transparent_png")
DIR_VERIFY = os.path.join(DEST_DIR, "03_kiem_chung_lap_rap_layers")

for d in [DIR_MASTER, DIR_CHROMA, DIR_TRANSPARENT, DIR_VERIFY]:
    os.makedirs(d, exist_ok=True)

AI_ASSETS = {
    "master": "smooth_anime_base_master_1787759475064.jpg",
    "01_dau": "part_head_smooth_1787759497744.jpg",
    "02_than_co": "part_torso_with_neck_1787759536257.jpg",
    "03a_bap_tay_trai": "part_upper_arm_smooth_1787759556312.jpg",
    "04a_cang_tay_trai": "part_forearm_smooth_1787759576213.jpg",
    "05a_ban_tay_trai": "part_hand_smooth_1787759596671.jpg",
    "06a_dui_trai": "part_thigh_smooth_1787759615603.jpg",
    "07a_cang_chan_trai": "part_shin_smooth_1787759634119.jpg",
    "08a_ban_chan_trai": "part_foot_smooth_1787759670410.jpg",
    "09a_canh_tay_trai_full": "part_arm_full_smooth_1787759689823.jpg",
    "10a_chan_trai_full": "part_leg_full_smooth_1787759709698.jpg",
}

def remove_green_screen(img_pil, pad=8):
    img_rgba = img_pil.convert("RGBA")
    arr = np.array(img_rgba, dtype=np.float32)
    r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
    greenness = g - np.maximum(r, b)
    alpha = np.clip(1.0 - (greenness - 18.0) / 28.0, 0.0, 1.0) * 255.0
    despilled_g = np.minimum(g, np.maximum(r, b) * 1.05 + 8.0)
    arr[:, :, 1] = np.where(greenness > 8.0, despilled_g, g)
    arr[:, :, 3] = alpha
    res = Image.fromarray(np.uint8(arr), mode="RGBA")
    bbox = res.split()[3].getbbox()
    if bbox:
        w, h = res.size
        crop_box = (
            max(0, bbox[0] - pad),
            max(0, bbox[1] - pad),
            min(w, bbox[2] + pad),
            min(h, bbox[3] + pad)
        )
        return res.crop(crop_box)
    return res

# 1. Master Character
master_src = os.path.join(ARTIFACT_DIR, AI_ASSETS["master"])
shutil.copy2(master_src, os.path.join(DIR_MASTER, "master_character_turnaround.jpg"))
master_img = Image.open(master_src)
master_transparent = remove_green_screen(master_img, pad=10)
master_transparent.save(os.path.join(DIR_MASTER, "master_character_cutout.png"))

ITEMS_TO_EXPORT = [
    {"id": "01_dau", "name": "Đầu (Không Cổ)", "file": "01_dau.png", "src": AI_ASSETS["01_dau"], "flip": False, "group": "head", "layer": 10},
    {"id": "02_than_co", "name": "Thân + Cổ (Giữ Cổ)", "file": "02_than_co.png", "src": AI_ASSETS["02_than_co"], "flip": False, "group": "torso", "layer": 5},
    {"id": "03a_bap_tay_trai", "name": "Vai / Bắp Tay Trái (1)", "file": "03a_bap_tay_trai.png", "src": AI_ASSETS["03a_bap_tay_trai"], "flip": False, "group": "arms", "layer": 4},
    {"id": "03b_bap_tay_phai", "name": "Vai / Bắp Tay Phải (1)", "file": "03b_bap_tay_phai.png", "src": AI_ASSETS["03a_bap_tay_trai"], "flip": True, "group": "arms", "layer": 4},
    {"id": "04a_cang_tay_trai", "name": "Cẳng Tay Trái (5)", "file": "04a_cang_tay_trai.png", "src": AI_ASSETS["04a_cang_tay_trai"], "flip": False, "group": "arms", "layer": 6},
    {"id": "04b_cang_tay_phai", "name": "Cẳng Tay Phải (5)", "file": "04b_cang_tay_phai.png", "src": AI_ASSETS["04a_cang_tay_trai"], "flip": True, "group": "arms", "layer": 6},
    {"id": "05a_ban_tay_trai", "name": "Bàn Tay Trái 5 Ngón (6)", "file": "05a_ban_tay_trai.png", "src": AI_ASSETS["05a_ban_tay_trai"], "flip": False, "group": "arms", "layer": 7},
    {"id": "05b_ban_tay_phai", "name": "Bàn Tay Phải 5 Ngón (6)", "file": "05b_ban_tay_phai.png", "src": AI_ASSETS["05a_ban_tay_trai"], "flip": True, "group": "arms", "layer": 7},
    {"id": "06a_dui_trai", "name": "Đùi Trái (2)", "file": "06a_dui_trai.png", "src": AI_ASSETS["06a_dui_trai"], "flip": False, "group": "legs", "layer": 2},
    {"id": "06b_dui_phai", "name": "Đùi Phải (2)", "file": "06b_dui_phai.png", "src": AI_ASSETS["06a_dui_trai"], "flip": True, "group": "legs", "layer": 2},
    {"id": "07a_cang_chan_trai", "name": "Cẳng Chân Trái (3)", "file": "07a_cang_chan_trai.png", "src": AI_ASSETS["07a_cang_chan_trai"], "flip": False, "group": "legs", "layer": 3},
    {"id": "07b_cang_chan_phai", "name": "Cẳng Chân Phải (3)", "file": "07b_cang_chan_phai.png", "src": AI_ASSETS["07a_cang_chan_trai"], "flip": True, "group": "legs", "layer": 3},
    {"id": "08a_ban_chan_trai", "name": "Bàn Chân Trái (4)", "file": "08a_ban_chan_trai.png", "src": AI_ASSETS["08a_ban_chan_trai"], "flip": False, "group": "legs", "layer": 4},
    {"id": "08b_ban_chan_phai", "name": "Bàn Chân Phải (4)", "file": "08b_ban_chan_phai.png", "src": AI_ASSETS["08a_ban_chan_trai"], "flip": True, "group": "legs", "layer": 4},
    {"id": "09a_canh_tay_trai_full", "name": "Cánh Tay Trái Toàn Bộ", "file": "09a_canh_tay_trai_toan_bo.png", "src": AI_ASSETS["09a_canh_tay_trai_full"], "flip": False, "group": "arms_full", "layer": 5},
    {"id": "09b_canh_tay_phai_full", "name": "Cánh Tay Phải Toàn Bộ", "file": "09b_canh_tay_phai_toan_bo.png", "src": AI_ASSETS["09a_canh_tay_trai_full"], "flip": True, "group": "arms_full", "layer": 5},
    {"id": "10a_chan_trai_full", "name": "Chân Trái Toàn Bộ", "file": "10a_chan_trai_toan_bo.png", "src": AI_ASSETS["10a_chan_trai_full"], "flip": False, "group": "legs_full", "layer": 3},
    {"id": "10b_chan_phai_full", "name": "Chân Phải Toàn Bộ", "file": "10b_chan_phai_toan_bo.png", "src": AI_ASSETS["10a_chan_trai_full"], "flip": True, "group": "legs_full", "layer": 3},
]

manifest_parts = []

for item in ITEMS_TO_EXPORT:
    raw_path = os.path.join(ARTIFACT_DIR, item["src"])
    chroma_dst = os.path.join(DIR_CHROMA, item["file"].replace(".png", "_chroma.jpg"))
    shutil.copy2(raw_path, chroma_dst)

    img = Image.open(raw_path)
    if item["flip"]:
        img = ImageOps.mirror(img)

    trans_img = remove_green_screen(img, pad=6)
    trans_dst = os.path.join(DIR_TRANSPARENT, item["file"])
    trans_img.save(trans_dst, "PNG")

    w, h = trans_img.size
    manifest_parts.append({
        "id": item["id"],
        "name": item["name"],
        "group": item["group"],
        "layer_order": item["layer"],
        "filename": item["file"],
        "width": w,
        "height": h,
        "aspect_ratio": round(w / h, 4),
        "is_mirrored": item["flip"],
    })
    print(f"  ✓ Exported: {item['file']} ({w}x{h}px)")

# Save manifest
manifest_data = {
    "kit_id": "smooth_anime_mannequin_v5",
    "kit_name": "Smooth Anime Mannequin Base V5 (Soft Skin & Segmented Limbs)",
    "description": "Bộ phận nhân vật anime mềm mại, mịn da, không khớp nối cơ học, thân giữ cổ, phân tách chi tiết theo 8 cụm giải phẫu chuẩn.",
    "gender": "neutral_female_male",
    "skin_tone": "fair_peach_rosy",
    "structure": {
        "head": "Đầu không cổ (01_dau.png)",
        "torso": "Thân kèm cổ (02_than_co.png)",
        "arms": ["Bắp tay (1)", "Cẳng tay (5)", "Bàn tay (6)"],
        "legs": ["Đùi (2)", "Cẳng chân (3)", "Bàn chân (4)"]
    },
    "total_parts": len(manifest_parts),
    "parts": manifest_parts
}

manifest_path = os.path.join(DEST_DIR, "kit_manifest.json")
with open(manifest_path, "w", encoding="utf-8") as f:
    json.dump(manifest_data, f, ensure_ascii=False, indent=2)

print(f"\n✅ Kit manifest saved to: {manifest_path}")

# Verification Canvas Assembly
canvas_w, canvas_h = 1000, 1600
verify_canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))

# Assembly layout coordinates
torso_img = Image.open(os.path.join(DIR_TRANSPARENT, "02_than_co.png"))
head_img = Image.open(os.path.join(DIR_TRANSPARENT, "01_dau.png"))
upper_arm_l = Image.open(os.path.join(DIR_TRANSPARENT, "03a_bap_tay_trai.png"))
upper_arm_r = Image.open(os.path.join(DIR_TRANSPARENT, "03b_bap_tay_phai.png"))
forearm_l = Image.open(os.path.join(DIR_TRANSPARENT, "04a_cang_tay_trai.png"))
forearm_r = Image.open(os.path.join(DIR_TRANSPARENT, "04b_cang_tay_phai.png"))
thigh_l = Image.open(os.path.join(DIR_TRANSPARENT, "06a_dui_trai.png"))
thigh_r = Image.open(os.path.join(DIR_TRANSPARENT, "06b_dui_phai.png"))
shin_l = Image.open(os.path.join(DIR_TRANSPARENT, "07a_cang_chan_trai.png"))
shin_r = Image.open(os.path.join(DIR_TRANSPARENT, "07b_cang_chan_phai.png"))
foot_l = Image.open(os.path.join(DIR_TRANSPARENT, "08a_ban_chan_trai.png"))
foot_r = Image.open(os.path.join(DIR_TRANSPARENT, "08b_ban_chan_phai.png"))

# Composite layers
cx = canvas_w // 2

# Scale torso
t_w = int(torso_img.width * 0.55)
t_h = int(torso_img.height * 0.55)
torso_scaled = torso_img.resize((t_w, t_h), Image.Resampling.LANCZOS)
t_x = cx - t_w // 2
t_y = 440

# Scale head
h_w = int(head_img.width * 0.48)
h_h = int(head_img.height * 0.48)
head_scaled = head_img.resize((h_w, h_h), Image.Resampling.LANCZOS)
h_x = cx - h_w // 2
h_y = t_y - h_h + 90

# Paste layers
verify_canvas.paste(torso_scaled, (t_x, t_y), torso_scaled)
verify_canvas.paste(head_scaled, (h_x, h_y), head_scaled)

verify_out = os.path.join(DIR_VERIFY, "assembly_verification_preview.png")
verify_canvas.save(verify_out, "PNG")

# Also copy to artifacts directory for user viewing
artifact_verify = os.path.join(ARTIFACT_DIR, "smooth_mannequin_test5_preview.png")
verify_canvas.save(artifact_verify, "PNG")

print(f"✨ Assembled verification preview saved to: {verify_out}")
