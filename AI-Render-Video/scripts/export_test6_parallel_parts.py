"""
Export Clean Parallel Decomposed Parts (No Ball Joints / No Seams, No Cheek Blush) to public/test6
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
DEST_DIR = os.path.join(PROJECT_ROOT, "public", "test6")

DIR_MASTER = os.path.join(DEST_DIR, "00_nhan_vat_goc_master")
DIR_CHROMA = os.path.join(DEST_DIR, "01_anh_goc_linh_kien")
DIR_TRANSPARENT = os.path.join(DEST_DIR, "02_linh_kien_tach_roi_transparent_png")
DIR_VERIFY = os.path.join(DEST_DIR, "03_kiem_chung_lap_rap_layers")

for d in [DIR_MASTER, DIR_CHROMA, DIR_TRANSPARENT, DIR_VERIFY]:
    os.makedirs(d, exist_ok=True)

SHEET_IMG_NAME = "test6_parallel_decomposed_sheet_1787762045314.jpg"
MASTER_IMG_NAME = "test6_master_base_1787761895500.jpg"

def remove_green_screen(img_pil, pad=4):
    img_rgba = img_pil.convert("RGBA")
    arr = np.array(img_rgba, dtype=np.float32)
    r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
    greenness = g - np.maximum(r, b)
    alpha = np.clip(1.0 - (greenness - 16.0) / 24.0, 0.0, 1.0) * 255.0
    despilled_g = np.minimum(g, np.maximum(r, b) * 1.04 + 6.0)
    arr[:, :, 1] = np.where(greenness > 6.0, despilled_g, g)
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

# 1. Save Master Character
master_path = os.path.join(ARTIFACT_DIR, MASTER_IMG_NAME)
shutil.copy2(master_path, os.path.join(DIR_MASTER, "master_character_turnaround.jpg"))
master_img = Image.open(master_path)
master_transparent = remove_green_screen(master_img, pad=10)
master_transparent.save(os.path.join(DIR_MASTER, "master_character_cutout.png"))

# 2. Save Full Parallel Sheet
sheet_path = os.path.join(ARTIFACT_DIR, SHEET_IMG_NAME)
shutil.copy2(sheet_path, os.path.join(DIR_CHROMA, "parallel_decomposed_sheet_full.jpg"))
sheet_img = Image.open(sheet_path)
sheet_transparent = remove_green_screen(sheet_img, pad=4)
sheet_transparent.save(os.path.join(DIR_TRANSPARENT, "parallel_decomposed_sheet_transparent.png"))

# 3. Crop Bounding Boxes on Sheet
# Sheet dimensions: W=1792, H=1024 (or whatever actual size is)
sW, sH = sheet_img.size

# Coordinates in normalized or pixel percentages
REGIONS = [
    # Top Row
    {"id": "01_dau", "name": "Đầu (Không Cổ, Không Má Hồng)", "box": (0.02, 0.04, 0.23, 0.44), "file": "01_dau.png", "group": "head", "layer": 10},
    {"id": "02_than_co", "name": "Thân + Cổ (Giữ Cổ, Không Khớp Cầu)", "box": (0.24, 0.04, 0.42, 0.44), "file": "02_than_co.png", "group": "torso", "layer": 5},
    {"id": "03a_bap_tay_trai", "name": "Vai / Bắp Tay Trái (1: Từ Vai Đến Khuỷu)", "box": (0.43, 0.04, 0.58, 0.44), "file": "03a_bap_tay_trai.png", "group": "arms", "layer": 4},
    {"id": "03b_bap_tay_phai", "name": "Vai / Bắp Tay Phải (1: Đối Xứng)", "box": (0.43, 0.04, 0.58, 0.44), "file": "03b_bap_tay_phai.png", "flip": True, "group": "arms", "layer": 4},
    
    # Bottom Row
    {"id": "04a_cang_tay_trai", "name": "Cẳng Tay Trái (5: Từ Khuỷu Đến Cổ Tay)", "box": (0.05, 0.74, 0.13, 0.98), "file": "04a_cang_tay_trai.png", "group": "arms", "layer": 6},
    {"id": "04b_cang_tay_phai", "name": "Cẳng Tay Phải (5: Từ Khuỷu Đến Cổ Tay)", "box": (0.17, 0.74, 0.25, 0.98), "file": "04b_cang_tay_phai.png", "group": "arms", "layer": 6},
    {"id": "05a_ban_tay_trai", "name": "Bàn Tay Trái 5 Ngón (6: Từ Cổ Tay Đến Ngón)", "box": (0.03, 0.48, 0.15, 0.72), "file": "05a_ban_tay_trai.png", "group": "arms", "layer": 7},
    {"id": "05b_ban_tay_phai", "name": "Bàn Tay Phải 5 Ngón (6: Từ Cổ Tay Đến Ngón)", "box": (0.15, 0.48, 0.27, 0.72), "file": "05b_ban_tay_phai.png", "group": "arms", "layer": 7},
    
    {"id": "06a_dui_trai", "name": "Đùi Trái (2: Từ Háng Đến Gối)", "box": (0.30, 0.48, 0.41, 0.88), "file": "06a_dui_trai.png", "group": "legs", "layer": 2},
    {"id": "06b_dui_phai", "name": "Đùi Phải (2: Từ Háng Đến Gối)", "box": (0.44, 0.48, 0.56, 0.88), "file": "06b_dui_phai.png", "group": "legs", "layer": 2},
    
    {"id": "07a_cang_chan_trai", "name": "Cẳng Chân Trái (3: Từ Gối Đến Cổ Chân)", "box": (0.58, 0.48, 0.67, 0.98), "file": "07a_cang_chan_trai.png", "group": "legs", "layer": 3},
    {"id": "07b_cang_chan_phai", "name": "Cẳng Chân Phải (3: Từ Gối Đến Cổ Chân)", "box": (0.67, 0.48, 0.76, 0.98), "file": "07b_cang_chan_phai.png", "group": "legs", "layer": 3},
    
    {"id": "08a_ban_chan_trai", "name": "Bàn Chân Trái (4: Từ Cổ Chân Đến Ngón)", "box": (0.78, 0.60, 0.87, 0.95), "file": "08a_ban_chan_trai.png", "group": "legs", "layer": 4},
    {"id": "08b_ban_chan_phai", "name": "Bàn Chân Phải (4: Từ Cổ Chân Đến Ngón)", "box": (0.88, 0.60, 0.97, 0.95), "file": "08b_ban_chan_phai.png", "group": "legs", "layer": 4},
]

manifest_parts = []

for r in REGIONS:
    x1 = int(r["box"][0] * sW)
    y1 = int(r["box"][1] * sH)
    x2 = int(r["box"][2] * sW)
    y2 = int(r["box"][3] * sH)
    
    crop_chroma = sheet_img.crop((x1, y1, x2, y2))
    if r.get("flip"):
        crop_chroma = ImageOps.mirror(crop_chroma)
        
    chroma_dst = os.path.join(DIR_CHROMA, r["file"].replace(".png", "_chroma.jpg"))
    crop_chroma.save(chroma_dst, "JPEG", quality=98)
    
    trans_piece = remove_green_screen(crop_chroma, pad=6)
    trans_dst = os.path.join(DIR_TRANSPARENT, r["file"])
    trans_piece.save(trans_dst, "PNG")
    
    w, h = trans_piece.size
    manifest_parts.append({
        "id": r["id"],
        "name": r["name"],
        "group": r["group"],
        "layer_order": r["layer"],
        "filename": r["file"],
        "width": w,
        "height": h,
        "aspect_ratio": round(w / h, 4),
        "is_mirrored": bool(r.get("flip")),
    })
    print(f"  ✓ Processed: {r['file']} ({w}x{h}px)")

manifest_data = {
    "kit_id": "smooth_anime_mannequin_v6_parallel",
    "kit_name": "Smooth Anime Mannequin Base V6 (Parallel Seamless Slices)",
    "description": "Linh kiện nhân vật anime bóc tách song song từ cùng 1 tấm sheet, mịn da hoàn toàn, không có má hồng, không khớp cầu/lồi nhựa, phân chia chuẩn từ vai-khuỷu, khuỷu-cổ tay, cổ tay-bàn tay, háng-gối, gối-cổ chân, cổ chân-bàn chân.",
    "gender": "neutral_smooth",
    "skin_tone": "clean_fair_peach_no_blush",
    "structure": {
        "head": "Đầu không cổ, không má hồng (01_dau.png)",
        "torso": "Thân kèm cổ, không khớp cầu (02_than_co.png)",
        "arms": ["Vai/Bắp tay (03a, 03b)", "Cẳng tay (04a, 04b)", "Bàn tay 5 ngón (05a, 05b)"],
        "legs": ["Đùi (06a, 06b)", "Cẳng chân (07a, 07b)", "Bàn chân (08a, 08b)"]
    },
    "total_parts": len(manifest_parts),
    "parts": manifest_parts
}

manifest_path = os.path.join(DEST_DIR, "kit_manifest.json")
with open(manifest_path, "w", encoding="utf-8") as f:
    json.dump(manifest_data, f, ensure_ascii=False, indent=2)

print(f"\n✅ Kit manifest saved to: {manifest_path}")
