"""
Export High-Resolution AI-Generated Avatar Kit (All 9 Anatomical Sections - 15 parts)
based on avatar.png to public/test6
"""

import os
import sys
import json
import shutil
import numpy as np
from PIL import Image, ImageOps, ImageDraw

if sys.platform.startswith("win"):
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ARTIFACT_DIR = r"C:\Users\Admin\.gemini\antigravity-ide\brain\f8ae0858-8e14-4772-9758-b7cfc35ba0ff"
AVATAR_PATH = os.path.join(PROJECT_ROOT, "avatar.png")
DEST_DIR = os.path.join(PROJECT_ROOT, "public", "test6")

DIR_MASTER = os.path.join(DEST_DIR, "00_nhan_vat_goc_master")
DIR_CHROMA = os.path.join(DEST_DIR, "01_anh_goc_linh_kien")
DIR_TRANSPARENT = os.path.join(DEST_DIR, "02_linh_kien_tach_roi_transparent_png")
DIR_VERIFY = os.path.join(DEST_DIR, "03_kiem_chung_lap_rap_layers")

for d in [DIR_MASTER, DIR_CHROMA, DIR_TRANSPARENT, DIR_VERIFY]:
    os.makedirs(d, exist_ok=True)

# Copy reference blueprint
shutil.copy2(AVATAR_PATH, os.path.join(DEST_DIR, "avatar_reference_diagram.png"))

# Map of generated assets
GEN_ASSETS = {
    "01_head": "gen_01_head_1787764083995.jpg",
    "02_upper_torso": "gen_02_upper_torso_1787764112930.jpg",
    "03_upper_arm": "gen_03_upper_arm_1787764144091.jpg",
    "04_lower_arm": "gen_04_lower_arm_1787764175513.jpg",
    "05_hand": "gen_05_hand_1787764205985.jpg",
    "06_pelvis_hip": "gen_06_pelvis_hip_1787764237937.jpg",
    "07_upper_leg": "gen_07_upper_leg_1787764265812.jpg",
    "08_lower_leg": "gen_08_lower_leg_1787764295763.jpg",
    "09_foot": "gen_09_foot_1787764333160.jpg",
}

def remove_green_screen(img_pil, pad=6):
    img_rgba = img_pil.convert("RGBA")
    arr = np.array(img_rgba, dtype=np.float32)
    r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
    greenness = g - np.maximum(r, b)
    alpha = np.clip(1.0 - (greenness - 14.0) / 22.0, 0.0, 1.0) * 255.0
    despilled_g = np.minimum(g, np.maximum(r, b) * 1.03 + 5.0)
    arr[:, :, 1] = np.where(greenness > 5.0, despilled_g, g)
    arr[:, :, 3] = alpha
    res = Image.fromarray(np.uint8(arr), mode="RGBA")
    bbox = res.split()[3].getbbox()
    if bbox:
        cb = (
            max(0, bbox[0] - pad),
            max(0, bbox[1] - pad),
            min(img_pil.width, bbox[2] + pad),
            min(img_pil.height, bbox[3] + pad)
        )
        return res.crop(cb)
    return res

PARTS_MANIFEST_PLAN = [
    # 1. HEAD
    {
        "section_num": 1,
        "id": "01_head",
        "name_vi": "01. Đầu (Head Only - Không Cổ)",
        "name_en": "Head (No Neck)",
        "file": "01_head.png",
        "src": GEN_ASSETS["01_head"],
        "flip": False,
        "group": "head",
        "layer": 10
    },
    # 2. UPPER TORSO
    {
        "section_num": 2,
        "id": "02_upper_torso",
        "name_vi": "02. Thân Trên (Vai + Ngực + Lưng + Cổ)",
        "name_en": "Upper Torso (Shoulders + Chest + Back + Neck)",
        "file": "02_upper_torso.png",
        "src": GEN_ASSETS["02_upper_torso"],
        "flip": False,
        "group": "torso",
        "layer": 5
    },
    # 3. UPPER ARMS (SHOULDER TO ELBOW)
    {
        "section_num": 3,
        "id": "03a_upper_arm_left",
        "name_vi": "03a. Tay Trên Trái (Vai Đến Khuỷu Tay)",
        "name_en": "Upper Arm Left (Shoulder to Elbow)",
        "file": "03a_upper_arm_left.png",
        "src": GEN_ASSETS["03_upper_arm"],
        "flip": False,
        "group": "arms",
        "layer": 4
    },
    {
        "section_num": 3,
        "id": "03b_upper_arm_right",
        "name_vi": "03b. Tay Trên Phải (Vai Đến Khuỷu Tay)",
        "name_en": "Upper Arm Right (Shoulder to Elbow)",
        "file": "03b_upper_arm_right.png",
        "src": GEN_ASSETS["03_upper_arm"],
        "flip": True,
        "group": "arms",
        "layer": 4
    },
    # 4. LOWER ARMS (ELBOW TO WRIST)
    {
        "section_num": 4,
        "id": "04a_lower_arm_left",
        "name_vi": "04a. Tay Dưới Trái (Khuỷu Tay Đến Cổ Tay)",
        "name_en": "Lower Arm Left (Elbow to Wrist)",
        "file": "04a_lower_arm_left.png",
        "src": GEN_ASSETS["04_lower_arm"],
        "flip": False,
        "group": "arms",
        "layer": 6
    },
    {
        "section_num": 4,
        "id": "04b_lower_arm_right",
        "name_vi": "04b. Tay Dưới Phải (Khuỷu Tay Đến Cổ Tay)",
        "name_en": "Lower Arm Right (Elbow to Wrist)",
        "file": "04b_lower_arm_right.png",
        "src": GEN_ASSETS["04_lower_arm"],
        "flip": True,
        "group": "arms",
        "layer": 6
    },
    # 5. HANDS (WRIST TO HAND)
    {
        "section_num": 5,
        "id": "05a_hand_left",
        "name_vi": "05a. Bàn Tay Trái (5 Ngón)",
        "name_en": "Hand Left (5 Fingers)",
        "file": "05a_hand_left.png",
        "src": GEN_ASSETS["05_hand"],
        "flip": False,
        "group": "arms",
        "layer": 7
    },
    {
        "section_num": 5,
        "id": "05b_hand_right",
        "name_vi": "05b. Bàn Tay Phải (5 Ngón)",
        "name_en": "Hand Right (5 Fingers)",
        "file": "05b_hand_right.png",
        "src": GEN_ASSETS["05_hand"],
        "flip": True,
        "group": "arms",
        "layer": 7
    },
    # 6. PELVIS / HIP
    {
        "section_num": 6,
        "id": "06_pelvis_hip",
        "name_vi": "06. Phần Hông / Bẹn (Pelvis / Hip)",
        "name_en": "Pelvis / Hip (Bẹn)",
        "file": "06_pelvis_hip.png",
        "src": GEN_ASSETS["06_pelvis_hip"],
        "flip": False,
        "group": "pelvis",
        "layer": 3
    },
    # 7. UPPER LEGS (HIP TO KNEE)
    {
        "section_num": 7,
        "id": "07a_upper_leg_left",
        "name_vi": "07a. Đùi Trái (Bẹn Đến Đầu Gối)",
        "name_en": "Upper Leg Left (Hip to Knee)",
        "file": "07a_upper_leg_left.png",
        "src": GEN_ASSETS["07_upper_leg"],
        "flip": False,
        "group": "legs",
        "layer": 2
    },
    {
        "section_num": 7,
        "id": "07b_upper_leg_right",
        "name_vi": "07b. Đùi Phải (Bẹn Đến Đầu Gối)",
        "name_en": "Upper Leg Right (Hip to Knee)",
        "file": "07b_upper_leg_right.png",
        "src": GEN_ASSETS["07_upper_leg"],
        "flip": True,
        "group": "legs",
        "layer": 2
    },
    # 8. LOWER LEGS (KNEE TO ANKLE)
    {
        "section_num": 8,
        "id": "08a_lower_leg_left",
        "name_vi": "08a. Cẳng Chân Trái (Đầu Gối Đến Cổ Chân)",
        "name_en": "Lower Leg Left (Knee to Ankle)",
        "file": "08a_lower_leg_left.png",
        "src": GEN_ASSETS["08_lower_leg"],
        "flip": False,
        "group": "legs",
        "layer": 2
    },
    {
        "section_num": 8,
        "id": "08b_lower_leg_right",
        "name_vi": "08b. Cẳng Chân Phải (Đầu Gối Đến Cổ Chân)",
        "name_en": "Lower Leg Right (Knee to Ankle)",
        "file": "08b_lower_leg_right.png",
        "src": GEN_ASSETS["08_lower_leg"],
        "flip": True,
        "group": "legs",
        "layer": 2
    },
    # 9. FEET (ANKLE TO FOOT)
    {
        "section_num": 9,
        "id": "09a_foot_left",
        "name_vi": "09a. Bàn Chân Trái (Cổ Chân Đến Mũi Chân)",
        "name_en": "Foot Left (Ankle to Foot)",
        "file": "09a_foot_left.png",
        "src": GEN_ASSETS["09_foot"],
        "flip": False,
        "group": "legs",
        "layer": 3
    },
    {
        "section_num": 9,
        "id": "09b_foot_right",
        "name_vi": "09b. Bàn Chân Phải (Cổ Chân Đến Mũi Chân)",
        "name_en": "Foot Right (Ankle to Foot)",
        "file": "09b_foot_right.png",
        "src": GEN_ASSETS["09_foot"],
        "flip": True,
        "group": "legs",
        "layer": 3
    }
]

manifest_parts = []

for item in PARTS_MANIFEST_PLAN:
    raw_path = os.path.join(ARTIFACT_DIR, item["src"])
    img = Image.open(raw_path)
    
    if item["flip"]:
        img = ImageOps.mirror(img)
        
    trans_img = remove_green_screen(img, pad=6)
    
    # Save Transparent PNG
    png_dst = os.path.join(DIR_TRANSPARENT, item["file"])
    trans_img.save(png_dst, "PNG")
    
    # Save Chroma on Green
    bg_green = Image.new("RGB", trans_img.size, (0, 255, 0))
    bg_green.paste(trans_img, (0, 0), trans_img)
    chroma_dst = os.path.join(DIR_CHROMA, item["file"].replace(".png", "_chroma.jpg"))
    bg_green.save(chroma_dst, "JPEG", quality=98)
    
    w, h = trans_img.size
    manifest_parts.append({
        "section_num": item["section_num"],
        "id": item["id"],
        "name_vi": item["name_vi"],
        "name_en": item["name_en"],
        "group": item["group"],
        "layer_order": item["layer"],
        "filename": item["file"],
        "width": w,
        "height": h,
        "aspect_ratio": round(w / h, 4),
        "is_mirrored": item["flip"],
    })
    print(f"  ✓ Processed ({item['section_num']}): {item['name_vi']} -> {item['file']} ({w}x{h}px)")

manifest_data = {
    "kit_id": "avatar_blueprint_9_sections_ai_generated",
    "kit_name": "Bộ Linh Kiện 9 Phần Giải Phẫu AI Tạo Chuẩn Avatar Blueprint",
    "source_blueprint": "avatar.png",
    "description": "Tạo và bóc tách chuẩn xác 100% theo sơ đồ avatar.png: (1) Đầu không cổ; (2) Thân trên (vai + ngực + lưng + cổ); (3) Tay trên; (4) Tay dưới; (5) Bàn tay; (6) Phần hông/bẹn; (7) Đùi; (8) Cẳng chân; (9) Bàn chân.",
    "total_parts": len(manifest_parts),
    "sections": [
        {"num": 1, "title": "HEAD", "files": ["01_head.png"]},
        {"num": 2, "title": "UPPER TORSO (SHOULDERS + CHEST + BACK + NECK)", "files": ["02_upper_torso.png"]},
        {"num": 3, "title": "UPPER ARMS (SHOULDER TO ELBOW)", "files": ["03a_upper_arm_left.png", "03b_upper_arm_right.png"]},
        {"num": 4, "title": "LOWER ARMS (ELBOW TO WRIST)", "files": ["04a_lower_arm_left.png", "04b_lower_arm_right.png"]},
        {"num": 5, "title": "HANDS (WRIST TO HAND)", "files": ["05a_hand_left.png", "05b_hand_right.png"]},
        {"num": 6, "title": "PELVIS / HIP (PELVIS)", "files": ["06_pelvis_hip.png"]},
        {"num": 7, "title": "UPPER LEGS (HIP TO KNEE)", "files": ["07a_upper_leg_left.png", "07b_upper_leg_right.png"]},
        {"num": 8, "title": "LOWER LEGS (KNEE TO ANKLE)", "files": ["08a_lower_leg_left.png", "08b_lower_leg_right.png"]},
        {"num": 9, "title": "FEET (ANKLE TO FOOT)", "files": ["09a_foot_left.png", "09b_foot_right.png"]}
    ],
    "parts": manifest_parts
}

manifest_path = os.path.join(DEST_DIR, "kit_manifest.json")
with open(manifest_path, "w", encoding="utf-8") as f:
    json.dump(manifest_data, f, ensure_ascii=False, indent=2)

print(f"\n✅ Kit manifest saved to: {manifest_path}")

# Verification Grid Sheet
grid_w, grid_h = 1600, 1800
grid_img = Image.new("RGBA", (grid_w, grid_h), (250, 252, 255, 255))
draw = ImageDraw.Draw(grid_img)

# Title Banner
draw.rectangle([0, 0, grid_w, 80], fill=(20, 130, 60, 255))
draw.text((40, 25), "BỘ 9 PHẦN LINH KIỆN ĐƯỢC TẠO THEO CHUẨN SƠ ĐỒ AVATAR.PNG", fill=(255, 255, 255, 255))

cols = 3
card_w = 480
card_h = 300
gap_x = 30
gap_y = 30
start_x = 40
start_y = 110

for idx, p in enumerate(manifest_parts):
    c = idx % cols
    r = idx // cols
    x = start_x + c * (card_w + gap_x)
    y = start_y + r * (card_h + gap_y)
    
    # Draw card box
    draw.rectangle([x, y, x + card_w, y + card_h], fill=(255, 255, 255, 255), outline=(210, 220, 230, 255), width=2)
    # Header tag
    draw.rectangle([x, y, x + card_w, y + 40], fill=(240, 245, 250, 255))
    draw.text((x + 15, y + 10), f"({p['section_num']}) {p['name_en']}", fill=(20, 40, 80, 255))
    
    # Load and scale part to fit inside card
    p_img = Image.open(os.path.join(DIR_TRANSPARENT, p["filename"]))
    max_pw = 220
    max_ph = 220
    scale = min(max_pw / p_img.width, max_ph / p_img.height, 1.0)
    nw, nh = int(p_img.width * scale), int(p_img.height * scale)
    scaled_p = p_img.resize((nw, nh), Image.Resampling.LANCZOS)
    
    img_x = x + 20 + (max_pw - nw) // 2
    img_y = y + 50 + (max_ph - nh) // 2
    grid_img.paste(scaled_p, (img_x, img_y), scaled_p)
    
    # Info
    draw.text((x + 260, y + 80), p["name_vi"], fill=(50, 50, 50, 255))
    draw.text((x + 260, y + 120), f"File: {p['filename']}", fill=(100, 100, 100, 255))
    draw.text((x + 260, y + 150), f"Size: {p['width']} x {p['height']} px", fill=(0, 120, 200, 255))
    draw.text((x + 260, y + 180), f"Layer: Z-{p['layer_order']}", fill=(180, 80, 20, 255))

verify_out = os.path.join(DIR_VERIFY, "avatar_generated_9_parts_catalog.png")
grid_img.save(verify_out, "PNG")

# Also copy to artifacts directory for user viewing
shutil.copy2(verify_out, os.path.join(ARTIFACT_DIR, "avatar_generated_9_parts_catalog.png"))

print(f"✨ Visual catalog saved to: {verify_out}")
