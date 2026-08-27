"""
Export Complete Blank Mannequin & V-Neck Head & Separated Limbs to public/test4
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
DEST_DIR = os.path.join(PROJECT_ROOT, "public", "test4")

DIR_MASTER = os.path.join(DEST_DIR, "00_nhan_vat_goc_master")
DIR_CHROMA = os.path.join(DEST_DIR, "01_anh_goc_linh_kien")
DIR_TRANSPARENT = os.path.join(DEST_DIR, "02_linh_kien_tach_roi_transparent_png")
DIR_VERIFY = os.path.join(DEST_DIR, "03_kiem_chung_lap_rap_layers")

for d in [DIR_MASTER, DIR_CHROMA, DIR_TRANSPARENT, DIR_VERIFY]:
    os.makedirs(d, exist_ok=True)

AI_ASSETS = {
    "master": "chibi_master_base_1787748161003.jpg",
    "01_dau_co_tam_giac": "chibi_head_neck_triangle_1787748174194.jpg",
    "02_than_mannequin": "chibi_torso_body_1787748188682.jpg",
    "03a_canh_tay_trai": "chibi_arm_left_1787748200629.jpg",
    "03b_canh_tay_phai": "chibi_arm_right_1787748212527.jpg",
    "04a_chan_trai": "chibi_leg_left_1787748225395.jpg",
    "04b_chan_phai": "chibi_leg_right_1787748238306.jpg",
    "05a_bap_tay_trai": "chibi_bap_tay_trai_1787748356274.jpg",
    "06a_cang_tay_trai": "chibi_cang_tay_trai_1787748371865.jpg",
    "07a_ban_tay_trai": "chibi_ban_tay_trai_1787748390639.jpg",
    "08a_dui_trai": "chibi_dui_trai_1787748405752.jpg",
    "09a_cang_chan_trai": "chibi_cang_chan_trai_1787748421033.jpg",
    "10a_ban_chan_trai": "chibi_ban_chan_trai_1787748437049.jpg",
}

def remove_green_screen(img_pil):
    img_rgba = img_pil.convert("RGBA")
    arr = np.array(img_rgba, dtype=np.float32)
    r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
    greenness = g - np.maximum(r, b)
    alpha = np.clip(1.0 - (greenness - 20.0) / 30.0, 0.0, 1.0) * 255.0
    despilled_g = np.minimum(g, np.maximum(r, b) * 1.05 + 10.0)
    arr[:, :, 1] = np.where(greenness > 10.0, despilled_g, g)
    arr[:, :, 3] = alpha
    res = Image.fromarray(np.uint8(arr), mode="RGBA")
    bbox = res.split()[3].getbbox()
    if bbox:
        pad = 8
        w, h = res.size
        crop_box = (
            max(0, bbox[0] - pad),
            max(0, bbox[1] - pad),
            min(w, bbox[2] + pad),
            min(h, bbox[3] + pad)
        )
        return res.crop(crop_box)
    return res

# 1. Master character
master_src = os.path.join(ARTIFACT_DIR, AI_ASSETS["master"])
shutil.copy2(master_src, os.path.join(DIR_MASTER, "master_character_turnaround.jpg"))
master_img = Image.open(master_src)
master_transparent = remove_green_screen(master_img)
master_transparent.save(os.path.join(DIR_MASTER, "master_character_cutout.png"))

ITEMS_TO_EXPORT = [
    {"id": "01_dau_co_tam_giac", "name": "Đầu + Cổ + Tam Giác Ngực Thay Áo", "file": "01_dau_co_tam_giac.png", "src": AI_ASSETS["01_dau_co_tam_giac"], "flip": False},
    {"id": "02_than_mannequin", "name": "Thân Mannequin", "file": "02_than_mannequin.png", "src": AI_ASSETS["02_than_mannequin"], "flip": False},
    {"id": "03a_canh_tay_trai", "name": "Cánh Tay Trái Toàn Bộ", "file": "03a_canh_tay_trai_toan_bo.png", "src": AI_ASSETS["03a_canh_tay_trai"], "flip": False},
    {"id": "03b_canh_tay_phai", "name": "Cánh Tay Phải Toàn Bộ", "file": "03b_canh_tay_phai_toan_bo.png", "src": AI_ASSETS["03b_canh_tay_phai"], "flip": False},
    {"id": "04a_chan_trai", "name": "Chân Trái Toàn Bộ", "file": "04a_chan_trai_toan_bo.png", "src": AI_ASSETS["04a_chan_trai"], "flip": False},
    {"id": "04b_chan_phai", "name": "Chân Phải Toàn Bộ", "file": "04b_chan_phai_toan_bo.png", "src": AI_ASSETS["04b_chan_phai"], "flip": False},
    {"id": "05a_bap_tay_trai", "name": "Bắp Tay Trái", "file": "05a_bap_tay_trai.png", "src": AI_ASSETS["05a_bap_tay_trai"], "flip": False},
    {"id": "05b_bap_tay_phai", "name": "Bắp Tay Phải", "file": "05b_bap_tay_phai.png", "src": AI_ASSETS["05a_bap_tay_trai"], "flip": True},
    {"id": "06a_cang_tay_trai", "name": "Cẳng Tay Trái", "file": "06a_cang_tay_trai.png", "src": AI_ASSETS["06a_cang_tay_trai"], "flip": False},
    {"id": "06b_cang_tay_phai", "name": "Cẳng Tay Phải", "file": "06b_cang_tay_phai.png", "src": AI_ASSETS["06a_cang_tay_trai"], "flip": True},
    {"id": "07a_ban_tay_trai", "name": "Bàn Tay Trái (5 Ngón)", "file": "07a_ban_tay_trai.png", "src": AI_ASSETS["07a_ban_tay_trai"], "flip": False},
    {"id": "07b_ban_tay_phai", "name": "Bàn Tay Phải (5 Ngón)", "file": "07b_ban_tay_phai.png", "src": AI_ASSETS["07a_ban_tay_trai"], "flip": True},
    {"id": "08a_dui_trai", "name": "Đùi Trái", "file": "08a_dui_trai.png", "src": AI_ASSETS["08a_dui_trai"], "flip": False},
    {"id": "08b_dui_phai", "name": "Đùi Phải", "file": "08b_dui_phai.png", "src": AI_ASSETS["08a_dui_trai"], "flip": True},
    {"id": "09a_cang_chan_trai", "name": "Cẳng Chân Trái", "file": "09a_cang_chan_trai.png", "src": AI_ASSETS["09a_cang_chan_trai"], "flip": False},
    {"id": "09b_cang_chan_phai", "name": "Cẳng Chân Phải", "file": "09b_cang_chan_phai.png", "src": AI_ASSETS["09a_cang_chan_trai"], "flip": True},
    {"id": "10a_ban_chan_trai", "name": "Bàn Chân Trái", "file": "10a_ban_chan_trai.png", "src": AI_ASSETS["10a_ban_chan_trai"], "flip": False},
    {"id": "10b_ban_chan_phai", "name": "Bàn Chân Phải", "file": "10b_ban_chan_phai.png", "src": AI_ASSETS["10a_ban_chan_trai"], "flip": True},
]

manifest_parts = []

for item in ITEMS_TO_EXPORT:
    src_file = os.path.join(ARTIFACT_DIR, item["src"])
    img = Image.open(src_file)
    if item["flip"]:
        img = ImageOps.mirror(img)

    chroma_file = item["file"].replace(".png", ".jpg")
    img.save(os.path.join(DIR_CHROMA, chroma_file))

    transparent_img = remove_green_screen(img)
    transparent_img.save(os.path.join(DIR_TRANSPARENT, item["file"]))

    manifest_parts.append({
        "id": item["id"],
        "name_vi": item["name"],
        "filename": item["file"],
        "width": transparent_img.width,
        "height": transparent_img.height,
        "chroma_path": f"/test4/01_anh_goc_linh_kien/{chroma_file}",
        "transparent_path": f"/test4/02_linh_kien_tach_roi_transparent_png/{item['file']}"
    })

# Write manifest.json
manifest = {
    "character_name": "Mannequin 2D Chibi Trắng Sứ V-Neck Bib & Tách Chi Tiết",
    "features": [
        "Không có quần áo (No clothes)",
        "Không có mặt (Blank face, no eyes/mouth)",
        "Đầu bao gồm Cổ + Tam Giác Ngực (V-Neck Bib) hỗ trợ thay đồ không mất chi tiết",
        "Tách rời hoàn chỉnh Cánh Tay và Đôi Chân (cả bản toàn bộ và bản chi tiết từng khớp)"
    ],
    "total_parts": len(manifest_parts),
    "parts": manifest_parts
}

with open(os.path.join(DEST_DIR, "manifest.json"), "w", encoding="utf-8") as f:
    json.dump(manifest, f, ensure_ascii=False, indent=2)

print(f"✓ Đã xuất toàn bộ {len(manifest_parts)} linh kiện vào {DEST_DIR}")
