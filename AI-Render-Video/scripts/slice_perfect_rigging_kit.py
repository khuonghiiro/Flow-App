"""
Slice Perfect 2D Puppet Rigging Kit with Extended Rotation Pivot Caps (Chỏm xoay bo tròn chống hở góc khi cử động)
"""

import os
import sys
import json
import shutil
import numpy as np
from PIL import Image, ImageDraw, ImageOps, ImageFilter

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

MASTER_IMG_PATH = os.path.join(ARTIFACT_DIR, "test6_master_base_1787761895500.jpg")
master_img = Image.open(MASTER_IMG_PATH).convert("RGBA")
W, H = master_img.size

# Remove Green Screen
def get_master_rgba(img_rgba):
    arr = np.array(img_rgba, dtype=np.float32)
    r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
    greenness = g - np.maximum(r, b)
    alpha = np.clip(1.0 - (greenness - 12.0) / 20.0, 0.0, 1.0) * 255.0
    despilled_g = np.minimum(g, np.maximum(r, b) * 1.02 + 4.0)
    arr[:, :, 1] = np.where(greenness > 4.0, despilled_g, g)
    arr[:, :, 3] = alpha
    return Image.fromarray(np.uint8(arr), mode="RGBA")

full_cutout = get_master_rgba(master_img)
full_cutout.save(os.path.join(DIR_MASTER, "master_character_cutout.png"))

# Sample natural skin tone from chest
sample_skin = (252, 218, 196, 255)
sample_outline = (130, 70, 50, 255)

def create_component(polygon_pts, overlap_caps=[], crop_pad=6):
    """
    Cut polygon from full cutout, and add smooth convex overlap caps (chỏm bán nguyệt)
    at pivot joint points so limbs can rotate smoothly without holes.
    """
    mask = Image.new("L", (W, H), 0)
    draw_mask = ImageDraw.Draw(mask)
    draw_mask.polygon(polygon_pts, fill=255)

    comp = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    comp.paste(full_cutout, (0, 0), mask)

    # Draw extended overlap caps if any (chỏm xoay tròn bán nguyệt cho khớp)
    draw_comp = ImageDraw.Draw(comp)
    for cap in overlap_caps:
        cx, cy, radius = cap["cx"], cap["cy"], cap["r"]
        bbox = [cx - radius, cy - radius, cx + radius, cy + radius]
        draw_comp.ellipse(bbox, fill=sample_skin, outline=sample_outline, width=2)
        # Re-paste the original details on top so outline is only on the outer edge
        comp.paste(full_cutout, (0, 0), mask)

    # Crop to component bounding box
    alpha = comp.split()[3]
    bbox = alpha.getbbox()
    if bbox:
        cb = (
            max(0, bbox[0] - crop_pad),
            max(0, bbox[1] - crop_pad),
            min(W, bbox[2] + crop_pad),
            min(H, bbox[3] + crop_pad)
        )
        return comp.crop(cb), cb
    return comp, (0, 0, W, H)

# Precise Anatomical Segmentation Polygons
PARTS_DEF = [
    {
        "id": "01_dau",
        "name": "Đầu (Không Cổ, Không Má Hồng)",
        "file": "01_dau.png",
        "group": "head",
        "layer": 10,
        "poly": [
            (200, 100), (568, 100), (620, 300), (600, 460),
            (470, 472), (384, 475), (298, 472), (168, 460), (148, 300)
        ],
        "caps": []
    },
    {
        "id": "02_than_co",
        "name": "Thân + Cổ (Giữ Nguyên Cổ, Vai & Hông Tròn)",
        "file": "02_than_co.png",
        "group": "torso",
        "layer": 5,
        "poly": [
            (320, 460), (448, 460), (455, 505), (510, 525), (460, 600),
            (455, 780), (460, 850), (384, 865), (308, 850), (313, 780),
            (308, 600), (258, 525), (313, 505)
        ],
        "caps": [
            {"cx": 384, "cy": 465, "r": 20} # Top neck dome
        ]
    },
    # Left Arm Segments (Character's Right / Viewer's Left)
    {
        "id": "03a_bap_tay_trai",
        "name": "Vai / Bắp Tay Trái (1: Vai Đến Khuỷu)",
        "file": "03a_bap_tay_trai.png",
        "group": "arms",
        "layer": 4,
        "poly": [
            (258, 505), (315, 525), (280, 620), (220, 690), (180, 680), (230, 580)
        ],
        "caps": [
            {"cx": 286, "cy": 515, "r": 28}, # Shoulder rotation cap (chui dưới thân vai)
            {"cx": 200, "cy": 685, "r": 20}  # Elbow rotation cap (chui dưới cẳng tay)
        ]
    },
    {
        "id": "04a_cang_tay_trai",
        "name": "Cẳng Tay Trái (5: Khuỷu Đến Cổ Tay)",
        "file": "04a_cang_tay_trai.png",
        "group": "arms",
        "layer": 6,
        "poly": [
            (180, 675), (225, 685), (160, 785), (115, 775)
        ],
        "caps": [
            {"cx": 202, "cy": 680, "r": 22}, # Elbow joint top cap
            {"cx": 137, "cy": 780, "r": 16}  # Wrist joint bottom cap
        ]
    },
    {
        "id": "05a_ban_tay_trai",
        "name": "Bàn Tay Trái 5 Ngón (6: Cổ Tay Đến Ngón)",
        "file": "05a_ban_tay_trai.png",
        "group": "arms",
        "layer": 7,
        "poly": [
            (115, 770), (160, 780), (150, 840), (55, 850), (60, 790)
        ],
        "caps": [
            {"cx": 137, "cy": 775, "r": 18} # Wrist insertion cap
        ]
    },
    # Right Arm Segments (Character's Left / Viewer's Right)
    {
        "id": "03b_bap_tay_phai",
        "name": "Vai / Bắp Tay Phải (1: Vai Đến Khuỷu)",
        "file": "03b_bap_tay_phai.png",
        "group": "arms",
        "layer": 4,
        "poly": [
            (453, 525), (510, 505), (538, 580), (588, 680), (548, 690), (488, 620)
        ],
        "caps": [
            {"cx": 482, "cy": 515, "r": 28}, # Right shoulder cap
            {"cx": 568, "cy": 685, "r": 20}  # Right elbow cap
        ]
    },
    {
        "id": "04b_cang_tay_phai",
        "name": "Cẳng Tay Phải (5: Khuỷu Đến Cổ Tay)",
        "file": "04b_cang_tay_phai.png",
        "group": "arms",
        "layer": 6,
        "poly": [
            (543, 685), (588, 675), (653, 775), (608, 785)
        ],
        "caps": [
            {"cx": 566, "cy": 680, "r": 22}, # Right elbow top cap
            {"cx": 631, "cy": 780, "r": 16}  # Right wrist cap
        ]
    },
    {
        "id": "05b_ban_tay_phai",
        "name": "Bàn Tay Phải 5 Ngón (6: Cổ Tay Đến Ngón)",
        "file": "05b_ban_tay_phai.png",
        "group": "arms",
        "layer": 7,
        "poly": [
            (608, 780), (653, 770), (708, 790), (713, 850), (618, 840)
        ],
        "caps": [
            {"cx": 631, "cy": 775, "r": 18} # Right wrist cap
        ]
    },
    # Left Leg Segments (Character's Right / Viewer's Left)
    {
        "id": "06a_dui_trai",
        "name": "Đùi Trái (2: Háng Đến Gối)",
        "file": "06a_dui_trai.png",
        "group": "legs",
        "layer": 2,
        "poly": [
            (308, 845), (384, 855), (370, 1025), (300, 1025)
        ],
        "caps": [
            {"cx": 346, "cy": 845, "r": 35}, # Hip rotation dome (chui dưới háng)
            {"cx": 335, "cy": 1025, "r": 24} # Knee rotation dome
        ]
    },
    {
        "id": "07a_cang_chan_trai",
        "name": "Cẳng Chân Trái (3: Gối Đến Cổ Chân)",
        "file": "07a_cang_chan_trai.png",
        "group": "legs",
        "layer": 3,
        "poly": [
            (300, 1020), (370, 1020), (360, 1220), (290, 1220)
        ],
        "caps": [
            {"cx": 335, "cy": 1020, "r": 26}, # Knee top dome
            {"cx": 325, "cy": 1220, "r": 18}  # Ankle bottom dome
        ]
    },
    {
        "id": "08a_ban_chan_trai",
        "name": "Bàn Chân Trái (4: Cổ Chân Đến Ngón)",
        "file": "08a_ban_chan_trai.png",
        "group": "legs",
        "layer": 4,
        "poly": [
            (290, 1215), (360, 1215), (365, 1290), (280, 1290)
        ],
        "caps": [
            {"cx": 325, "cy": 1215, "r": 20} # Ankle top dome
        ]
    },
    # Right Leg Segments (Character's Left / Viewer's Right)
    {
        "id": "06b_dui_phai",
        "name": "Đùi Phải (2: Háng Đến Gối)",
        "file": "06b_dui_phai.png",
        "group": "legs",
        "layer": 2,
        "poly": [
            (384, 855), (460, 845), (468, 1025), (398, 1025)
        ],
        "caps": [
            {"cx": 422, "cy": 845, "r": 35}, # Right hip rotation dome
            {"cx": 433, "cy": 1025, "r": 24} # Right knee dome
        ]
    },
    {
        "id": "07b_cang_chan_phai",
        "name": "Cẳng Chân Phải (3: Gối Đến Cổ Chân)",
        "file": "07b_cang_chan_phai.png",
        "group": "legs",
        "layer": 3,
        "poly": [
            (398, 1020), (468, 1020), (478, 1220), (408, 1220)
        ],
        "caps": [
            {"cx": 433, "cy": 1020, "r": 26}, # Right knee top dome
            {"cx": 443, "cy": 1220, "r": 18}  # Right ankle dome
        ]
    },
    {
        "id": "08b_ban_chan_phai",
        "name": "Bàn Chân Phải (4: Cổ Chân Đến Ngón)",
        "file": "08b_ban_chan_phai.png",
        "group": "legs",
        "layer": 4,
        "poly": [
            (408, 1215), (478, 1215), (488, 1290), (403, 1290)
        ],
        "caps": [
            {"cx": 443, "cy": 1215, "r": 20} # Right ankle top dome
        ]
    }
]

manifest_parts = []
saved_pieces = {}

for p in PARTS_DEF:
    piece_img, bbox = create_component(p["poly"], p.get("caps", []))
    
    # Save Transparent PNG
    dst_png = os.path.join(DIR_TRANSPARENT, p["file"])
    piece_img.save(dst_png, "PNG")
    
    # Save Chroma JPG (on green)
    bg_green = Image.new("RGB", piece_img.size, (0, 255, 0))
    bg_green.paste(piece_img, (0, 0), piece_img)
    dst_chroma = os.path.join(DIR_CHROMA, p["file"].replace(".png", "_chroma.jpg"))
    bg_green.save(dst_chroma, "JPEG", quality=98)
    
    w, h = piece_img.size
    saved_pieces[p["id"]] = {"img": piece_img, "bbox": bbox, "layer": p["layer"]}
    
    manifest_parts.append({
        "id": p["id"],
        "name": p["name"],
        "group": p["group"],
        "layer_order": p["layer"],
        "filename": p["file"],
        "width": w,
        "height": h,
        "origin_offset_in_master": {"x": bbox[0], "y": bbox[1]},
        "aspect_ratio": round(w / h, 4)
    })
    print(f"  ✓ Rigging Slice exported: {p['file']} ({w}x{h}px)")

# Save Manifest
manifest_data = {
    "kit_id": "smooth_anime_rigging_kit_v6",
    "kit_name": "Smooth Anime Rigging Kit V6 (Extended Overlap Pivot Caps)",
    "description": "Bộ linh kiện 2D Puppet Animation chuẩn Spine/Live2D: Có chỏm bo tròn bán nguyệt mở rộng (Overlap Caps) tại khớp vai, khuỷu, cổ tay, háng, gối, cổ chân. Khi lắp ráp và xoay góc cử động không bao giờ bị hở lỗ hay gãy khúc.",
    "gender": "neutral_smooth",
    "skin_tone": "clean_fair_peach_no_blush",
    "total_parts": len(manifest_parts),
    "parts": manifest_parts
}

with open(os.path.join(DEST_DIR, "kit_manifest.json"), "w", encoding="utf-8") as f:
    json.dump(manifest_data, f, ensure_ascii=False, indent=2)

print("\n✅ Saved kit_manifest.json")

# Build Layered Assembly Verification Image
verify_canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))

# Paste in strict z-index order
# 1. Legs (2) -> Shins (3) -> Feet (4)
# 2. Upper arms (4) -> Forearms (6) -> Hands (7)
# 3. Torso (5)
# 4. Head (10)
sorted_parts = sorted(PARTS_DEF, key=lambda x: x["layer"])
for p in sorted_parts:
    info = saved_pieces[p["id"]]
    bx = info["bbox"]
    verify_canvas.paste(info["img"], (bx[0], bx[1]), info["img"])

verify_path = os.path.join(DIR_VERIFY, "layered_assembly_test.png")
verify_canvas.save(verify_path, "PNG")

# Also copy to artifacts directory for user viewing
shutil.copy2(verify_path, os.path.join(ARTIFACT_DIR, "rigging_assembly_verification.png"))

print(f"✨ Verification assembly saved to: {verify_path}")
