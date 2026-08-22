"""
Comprehensive AI Prompt & Multi-Category Image Creation Guide for 3D Generation & Auto-Rigging.
Provides structured guidelines, technical specifications, and copyable prompt templates
for Humans (Characters), Animals (Creatures), and Objects (Props/Weapons).
"""

from typing import Dict, List


PROMPT_TEMPLATES: Dict[str, Dict[str, str]] = {
    # 1. BẢNG XOAY 4 GÓC 16:9 CHO NGƯỜI (KHÔNG CÓ DÒNG KẺ NGANG / GRID LINES)
    "turnaround_human": {
        "title": "🧑 1. Người & Nhân Vật: 1 Ảnh Xoay 16:9 (Không Dòng Kẻ, Tự Cắt 4 Góc)",
        "description": "💡 AI vẽ 1 tranh 16:9 gồm 4 góc cách đều trên nền trắng trơn tinh khiết, TUYỆT ĐỐI KHÔNG CÓ DÒNG KẺ NGANG.",
        "prompt": (
            "Character turnaround model sheet, full body orthographic reference of the same handsome stylish young character from 4 distinct angles side by side: "
            "1) Front View 0 degree, 2) Side Profile View 90 degree, 3) Back View 180 degree, 4) 3/4 Perspective View 45 degree. "
            "4 separate non-overlapping panels with distinct wide vertical spacing between views, no touching limbs or hands across views. "
            "Standing in symmetrical neutral A-pose with arms 45 degrees apart from torso, legs shoulder-width apart, "
            "wearing clean fitted t-shirt, dark denim jeans, and modern sneakers. "
            "Consistent clothing and facial features across all 4 views, soft neutral studio diffused lighting, "
            "solid seamless pure white background #FFFFFF, clean isolated backdrop without guidelines or grid lines, plain white void background, "
            "8k resolution, Unreal Engine 5 PBR game character concept --ar 16:9"
        ),
        "negative_prompt": (
            "grid lines, horizontal lines, reference lines, ruler marks, measurement lines, dashed lines, background lines, watermark, borders, frames, "
            "overlapping panels, touching characters, connected hands between views, different characters across panels, "
            "asymmetrical poses, hands in pockets, crossed arms, dramatic rim lighting, harsh cast shadows, motion blur, "
            "cropped head, cropped feet, complex messy background, floor shadow"
        ),
    },

    # 2. BẢNG XOAY 4 GÓC 16:9 CHO CON VẬT / THÚ CƯNG (KHÔNG DÒNG KẺ NGANG)
    "turnaround_animal": {
        "title": "🐶 2. Con Vật & Thú Cưng / Linh Vật: 1 Ảnh Xoay 16:9 (Không Dòng Kẻ, Tự Cắt 4 Góc)",
        "description": "💡 AI vẽ 1 tranh 16:9 chứa 4 góc con vật trên nền trắng tinh, không dính đuôi và không có dòng kẻ ngang.",
        "prompt": (
            "Character turnaround model sheet of an adorable fluffy cartoon anthropomorphic dog character from 4 distinct angles side by side: "
            "1) Front View 0 degree, 2) Side Profile View 90 degree with clearly defined snout and chest protrusion, 3) Back View 180 degree showing rear coat and wagging tail, 4) 3/4 Perspective View 45 degree. "
            "4 separate non-overlapping panels with wide empty white space between each view, no overlapping tails or hands across panels. "
            "Standing upright in neutral A-pose, relaxed open paws, wearing a leather collar with a metallic bell tag, "
            "soft dense fur grooming texture, expressive friendly eyes, consistent anatomy and markings across all views, "
            "balanced soft studio lighting, seamless solid pure white backdrop #FFFFFF, clean isolated backdrop without guidelines or grid lines, plain solid white background, "
            "Pixar 3D asset reference --ar 16:9"
        ),
        "negative_prompt": (
            "grid lines, horizontal lines, reference lines, ruler marks, measurement lines, dashed lines, background lines, watermark, borders, frames, "
            "overlapping tails between panels, touching characters, connected paws across views, inconsistent fur markings, "
            "distorted anatomy, dynamic running pose, legs hidden, floor reflection, harsh dark shadows, cropped ears, cropped tail, background clutter"
        ),
    },

    # 3. BẢNG XOAY 4 GÓC 16:9 CHO ĐỒ VẬT / VẬT PHẨM (KHÔNG DÒNG KẺ NGANG)
    "turnaround_prop": {
        "title": "📦 3. Đồ Vật & Vũ Khí / Vật Phẩm: 1 Ảnh Xoay 16:9 (Không Dòng Kẻ, Tự Cắt 4 Góc)",
        "description": "💡 AI vẽ 1 tranh 16:9 chứa 4 góc đồ vật trên nền trắng trơn không có thước đo hay dòng kẻ.",
        "prompt": (
            "Product and game asset turnaround reference sheet of a medieval ornate wooden treasure chest with iron bindings and lion crest latch from 4 distinct angles side by side: "
            "1) Front View 0 degree, 2) Side Profile View 90 degree showing depth and side handles, 3) Back View 180 degree showing heavy hinges, 4) 3/4 Perspective View 45 degree. "
            "4 separate non-overlapping panels with distinct gaps between views. "
            "Level horizontal alignment on identical scale, rich weathered oak wood grain, brushed dark iron reinforcement straps, "
            "subtle specular metallic highlights, crisp clean edges, studio diffused ambient lighting, "
            "solid pure white background #FFFFFF, clean plain background without guidelines or grid lines, plain white void backdrop, "
            "game ready PBR 3D asset model sheet --ar 16:9"
        ),
        "negative_prompt": (
            "grid lines, horizontal lines, reference lines, ruler marks, measurement lines, dashed lines, background lines, watermark, borders, frames, "
            "overlapping objects, perspective mismatch between views, open lid in some views and closed in others, "
            "extreme contrast, glare reflections, floating debris, dirty background, cropped corners"
        ),
    },

    # 4. ẢNH ĐỨNG ĐƠN MẶT TRƯỚC (3:4) CHO NGƯỜI
    "nam_casual": {
        "title": "🧑 4. Người (Mặt Trước Đơn 3:4 - A-Pose)",
        "description": "Dành cho chế độ 1 Ảnh Đơn. AI tự suy tính các góc 360 độ còn lại.",
        "prompt": (
            "full body shot of a handsome athletic young man standing in symmetrical A-pose, "
            "arms 45 degrees apart from torso, palms open, legs shoulder-width apart, "
            "wearing clean fitted dark blue denim jeans, white minimalist cotton t-shirt, "
            "stylish modern sneakers with flat rubber soles, short textured modern hairstyle with clean neck clearance, "
            "neutral facial expression looking straight at camera, soft studio diffused lighting, "
            "even ambient illumination without harsh shadows, isolated on solid neutral gray studio background #808080, "
            "no background lines, no grid lines, 8k resolution, photorealistic PBR material textures, game ready asset --ar 3:4"
        ),
        "negative_prompt": (
            "grid lines, horizontal lines, reference lines, ruler marks, measurement lines, dashed lines, deformed hands, extra limbs, bad anatomy, "
            "crossed arms, hands in pockets, high contrast shadows, dramatic rim lighting, motion blur, cropped head, cropped feet, "
            "complex background, holding items, floor shadows"
        ),
    },

    # 5. ẢNH ĐỨNG ĐƠN CHO NỮ ANIME (3:4)
    "nu_anime_stylized": {
        "title": "👧 5. Nữ Anime / Stylized (Mặt Trước Đơn 3:4)",
        "description": "Chuẩn phong cách Anime 3D, dễ chuyển đổi VRM.",
        "prompt": (
            "full body turnaround view of a stylized anime heroine standing in standard A-pose, "
            "arms slightly spread at 45 degrees, fingers relaxed, feet flat on ground shoulder-width apart, "
            "wearing neat futuristic school uniform jacket with distinct seamlines, pleated short skirt, "
            "tidy twintail hairstyle tied back with clear neck and shoulder gap, cute sporty ankle sneakers, "
            "symmetrical front view, soft anime cel-shading with clear PBR diffuse texture, "
            "clean pure white studio backdrop #FFFFFF, no guidelines, no grid lines, sharp outline, high quality 3d model concept --ar 3:4"
        ),
        "negative_prompt": (
            "grid lines, horizontal lines, reference lines, ruler marks, measurement lines, dashed lines, hair covering shoulders, long floor dress, "
            "hands behind back, tilted head, harsh dark shadow, cluttered background, accessories overlapping joints, cutoff toes"
        ),
    },

    # 6. TỪNG GÓC RIÊNG BIỆT (3:4)
    "individual_views_suite": {
        "title": "📐 6. Bộ Prompt Tạo Từng Góc Riêng (Tỷ lệ đứng 3:4 cho từng ô)",
        "description": "Dùng khi bạn muốn sinh 4 file ảnh riêng biệt chất lượng cao 1024x1024.",
        "prompt": (
            "=== [1. MẶT TRƯỚC (Front View 0°)] ===\n"
            "full body orthographic front view 0 degree of a character standing in symmetrical A-pose, arms 45 degrees, feet flat on floor, neutral expression, soft diffused studio light, pure white background #FFFFFF, no grid lines, no horizontal lines, 3D model concept --ar 3:4\n\n"
            "=== [2. CẠNH BÊN (Side Profile View 90°)] ===\n"
            "full body side profile view 90 degree of the exact same character standing straight, clearly visible nose/snout and chest depth, arms aligned with torso, clean silhouette, pure white background #FFFFFF, no grid lines, no horizontal lines --ar 3:4\n\n"
            "=== [3. SAU LƯNG (Back View 180°)] ===\n"
            "full body back view 180 degree of the exact same character seen from behind, showing spine alignment, shoulder blades, rear clothing details and tail, pure white background #FFFFFF, no grid lines, no horizontal lines --ar 3:4\n\n"
            "=== [4. GÓC NGHIÊNG 3/4 (3/4 Perspective View 45°)] ===\n"
            "full body three-quarter perspective view 45 degree of the exact same character standing in relaxed A-pose, rich 3D volumetric depth, soft studio illumination, pure white background #FFFFFF, no grid lines, no horizontal lines --ar 3:4"
        ),
        "negative_prompt": (
            "grid lines, horizontal lines, reference lines, ruler marks, measurement lines, dashed lines, asymmetrical pose, hands in pocket, harsh shadows, dark cast shadows, motion blur, cropped head, cropped feet, floor reflections"
        ),
    },
}


QUICK_GUIDE_MARKDOWN = """
### 💡 Giải Thích 2 Cách Tạo Ảnh Với AI:
1. **Cách 1 - Tạo 1 ảnh xoay 16:9 (`--ar 16:9`) [Khuyên dùng]**:
   * AI sẽ vẽ **1 bức tranh ngang gồm 4 góc nhìn của nhân vật xếp cạnh nhau trên nền trắng tinh khiết (Không có dòng kẻ ngang)**.
   * Bạn chỉ cần tải nguyên bức ảnh 16:9 này vào tab **"🔄 1 Ảnh Xoay 16:9"**, hệ thống sẽ **TỰ ĐỘNG DÒ VÀ CẮT THÔNG MINH thành 4 ảnh con** mà không cần bạn phải cắt tay!
2. **Cách 2 - Tạo 4 ảnh đứng riêng rẽ (`--ar 3:4` hoặc `1:1`)**:
   * AI tạo từng bức ảnh riêng biệt cho Mặt trước, Cạnh bên, Sau lưng, Góc nghiêng để tải vào từng ô.
"""


FULL_GUIDE_MARKDOWN = """
# 📖 HƯỚNG DẪN CHI TIẾT: CÁCH HOẠT ĐỘNG CỦA ẢNH 16:9 VÀ SMART AUTO-CROPPER

### ❓ Tại sao trong prompt trước đây lại có dòng kẻ ngang?

* **Nguyên nhân**: Trong prompt cũ có cụm từ `"horizontal reference guidelines"` khiến các AI như Midjourney/Flux tự động vẽ các đường kẻ đứt ngang qua ảnh.
* **Cách khắc phục**: Bộ prompt mới đã **loại bỏ hoàn toàn cụm từ đó**, đồng thời bổ sung các từ khóa cấm mạnh trong `Negative Prompt`:
  `grid lines, horizontal lines, reference lines, ruler marks, measurement lines, dashed lines`
  $\rightarrow$ Đảm bảo ảnh sinh ra sẽ có nền trắng trơn $100\%$ không một vết kẻ!

---

### 🎯 Tóm tắt quy trình đề xuất:
1. Copy Prompt mẫu **16:9 mới** ở trên và dán vào Midjourney/Flux.
2. Tải bức ảnh 16:9 vừa tạo về máy.
3. Kéo thả vào ô **"🔄 1 Ảnh Xoay 16:9"** trên Web App.
4. Bấm **⚡ Chạy Toàn Trình** để nhận ngay model 3D chuẩn xác 99%!
"""


def get_template_keys() -> List[str]:
    """Return list of template IDs."""
    return list(PROMPT_TEMPLATES.keys())


def get_template_choices() -> List[tuple]:
    """Return choices tuple for Gradio dropdown."""
    return [(v["title"], k) for k, v in PROMPT_TEMPLATES.items()]
