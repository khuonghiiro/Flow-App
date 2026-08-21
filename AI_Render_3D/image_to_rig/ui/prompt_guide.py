"""
Comprehensive AI Prompt & Character Image Creation Guide for 3D Generation & Auto-Rigging.
Provides structured guidelines, technical specifications, and copyable prompt templates.
"""

from typing import Dict, List


PROMPT_TEMPLATES: Dict[str, Dict[str, str]] = {
    "nam_casual": {
        "title": "🧑 Nhân vật Nam Hiện Đại (Casual Modern Male - A-Pose)",
        "description": "Thích hợp cho game hiện đại, metaverse, thời trang đường phố.",
        "prompt": (
            "full body shot of a handsome athletic young man standing in symmetrical A-pose, "
            "arms 45 degrees apart from torso, palms open, legs shoulder-width apart, "
            "wearing clean fitted dark blue denim jeans, white minimalist cotton t-shirt, "
            "stylish modern sneakers with flat rubber soles, short textured modern hairstyle with clean neck clearance, "
            "neutral facial expression looking straight at camera, soft studio diffused lighting, "
            "even ambient illumination without harsh shadows, isolated on solid neutral gray studio background #808080, "
            "8k resolution, photorealistic PBR material textures, game ready asset --ar 3:4"
        ),
        "negative_prompt": (
            "deformed hands, extra limbs, bad anatomy, crossed arms, hands in pockets, "
            "high contrast shadows, dramatic rim lighting, motion blur, cropped head, cropped feet, "
            "complex background, holding items, floor shadows"
        ),
    },
    "nu_anime_stylized": {
        "title": "👧 Nhân vật Nữ Anime / Stylized (Stylized Female - A-Pose)",
        "description": "Chuẩn phong cách Genshin / Honkai / Anime 3D, dễ chuyển đổi VRM.",
        "prompt": (
            "full body turnaround view of a stylized anime heroine standing in standard A-pose, "
            "arms slightly spread at 45 degrees, fingers relaxed, feet flat on ground shoulder-width apart, "
            "wearing neat futuristic school uniform jacket with distinct seamlines, pleated short skirt, "
            "tidy twintail hairstyle tied back with clear neck and shoulder gap, cute sporty ankle sneakers, "
            "symmetrical front view, soft anime cel-shading with clear PBR diffuse texture, "
            "clean white studio backdrop #FFFFFF, sharp outline, high quality 3d model concept --ar 3:4"
        ),
        "negative_prompt": (
            "hair covering shoulders, long floor dress, hands behind back, tilted head, "
            "harsh dark shadow, cluttered background, accessories overlapping joints, cutoff toes"
        ),
    },
    "cyberpunk_scout": {
        "title": "⚡ Chiến Binh Cyberpunk (Cyberpunk Scout - PBR Textures)",
        "description": "Nhân vật viễn tưởng với vật liệu da, kim loại và vải công nghệ cao.",
        "prompt": (
            "full body orthographic front view of a cyberpunk operative in perfect A-pose, "
            "symmetrical anatomy, arms extended 45 degrees, feet planted on level ground, "
            "wearing tactical techwear jacket with distinct matte fabric roughness, carbon fiber shoulder guards, "
            "modular combat cargo pants with clear knee joint separation, heavy combat boots with defined tread, "
            "sleek cybernetic headset tightly fitted, clean material separation between matte cloth and polished metal accents, "
            "balanced neutral studio illumination, no dark cast shadows, solid gray backdrop #808080, "
            "Unreal Engine 5 PBR material showcase --ar 3:4"
        ),
        "negative_prompt": (
            "weapons in hand, gun attached, backpack occluding spine, asymmetrical stance, "
            "neon lens flare, extreme bloom, motion blur, floor reflection"
        ),
    },
    "fantasy_knight": {
        "title": "🛡️ Hiệp Sĩ Giáp Fantasy (Fantasy Knight - Clean Armor)",
        "description": "Nhân vật trung cổ với giáp kim loại bóng bẩy và lớp lót da.",
        "prompt": (
            "full length front view of a medieval knight in lightweight plate armor standing in neutral A-pose, "
            "arms held away from body at 45 degree angle, hands open and empty, legs straight with gap between thighs, "
            "fitted polished steel breastplate, leather undergarments with fine stitch texture, "
            "segmented elbow and knee couters, symmetrical armored sabatons standing flat, "
            "open-face bascinet helmet well-fitted, even neutral lighting showing clear specular metallic reflection, "
            "isolated on seamless light gray background, 3D character modeling concept --ar 3:4"
        ),
        "negative_prompt": (
            "holding sword, shield equipped, long flowing cape covering back, asymmetrical pose, "
            "deep dark shadows, rust patches covering joints, blurry geometry"
        ),
    },
    "turnaround_sheet": {
        "title": "🔄 Bảng Vẽ Xoay 4 Góc (Character Turnaround Sheet 360°)",
        "description": "Tạo 4 góc nhìn đồng bộ (Trước, 3/4, Ngang, Sau) để đối chiếu vật liệu 3D.",
        "prompt": (
            "character model sheet, turnaround reference chart, full body of the same character from 4 angles: "
            "front view 0 degree, three-quarter front 45 degree, side profile 90 degree, back view 180 degree, "
            "consistent standing A-pose across all views, identical clothing and color palette, "
            "clean orthographic alignment, soft neutral studio lighting, isolated on solid white background, "
            "3D artist reference sheet, game development orthographic character sheet --ar 16:9"
        ),
        "negative_prompt": (
            "different characters, inconsistent outfits, dynamic action poses, perspective distortion, "
            "dramatic sunlight, dark background"
        ),
    },
}


QUICK_GUIDE_MARKDOWN = """
### 💡 Quy Tắc Chuẩn Để Model Tạo 3D Đạt Độ Chuẩn Xác Cao Nhất:
* **Tư thế bắt buộc**: **A-Pose** (2 tay dang nghiêng 45°, 2 chân mở rộng bằng vai, ngón tay mở tự nhiên).
* **Bố cục khung hình**: Nhân vật chiếm **80-85%** chiều cao, chừa 10% đỉnh đầu và 10% đáy chân (không bị cụt đầu/chân).
* **Phông nền**: Nền trong suốt hoặc phông đơn sắc **xám studio (`#808080`)** hoặc **trắng (`#FFFFFF`)**.
* **Ánh sáng**: Ánh sáng khuếch tán đồng đều (Soft studio light), **không có bóng đổ gắt** trên người hoặc dưới sàn.
"""


FULL_GUIDE_MARKDOWN = """
# 📖 CẨM NANG TOÀN DIỆN: TẠO ẢNH NHÂN VẬT & VẬT LIỆU AI CHUẨN 3D

Tài liệu này cung cấp toàn bộ quy chuẩn kỹ thuật giúp bạn tạo sinh ảnh nhân vật 2D bằng AI (Midjourney, Stable Diffusion, Flux, DALL-E) đạt độ chính xác tối đa khi đưa vào Pipeline tạo Mesh 3D (**TripoSR**) và Dựng Xương Tự Động (**UniRig**).

---

## 1. 📐 Kích Thước & Tỷ Lệ Ảnh (Image Dimensions & Specs)

| Tiêu Chí | Khuyến Nghị Chuẩn | Ghi Chú Kỹ Thuật |
| :--- | :--- | :--- |
| **Kích thước ảnh vuông** | **1024 x 1024 px** (Tỷ lệ 1:1) | Chuẩn tối ưu cho TripoSR NeRF voxel grid. |
| **Kích thước ảnh toàn thân** | **896 x 1152 px** hoặc **768 x 1024 px** (3:4) | Đảm bảo chi tiết từ đỉnh đầu tới gót giày không bị nén vỡ hạt. |
| **Tỷ lệ nhân vật (Framing)** | **80% - 85%** chiều cao ảnh | **Chừa 10% lề trên đỉnh đầu và 10% lề dưới đáy bàn chân.** Tuyệt đối không để viền ảnh cắt cụt tóc hoặc ngón chân. |
| **Định dạng file** | **PNG (24-bit / 32-bit Alpha)** hoặc JPG chất lượng cao (>95%) | PNG giúp giữ nguyên độ sắc nét của đường biên (edges). |
| **Phông nền (Background)** | Nền trong suốt (Alpha) hoặc xám studio `#808080` / trắng `#FFFFFF` | Nền đơn sắc giúp module `rembg` tách nền chính xác 100%, không bị lẹm viền tóc. |

---

## 2. 📷 Số Lượng Ảnh & Góc Chụp (Image Count & Camera Angles)

* 🎯 **Chế độ Single-View 3D (Mặc định TripoSR)**:
  * Chỉ cần **1 ảnh chính diện duy nhất (Front View 0°)** với chất lượng cao nhất.
  * Góc camera: **Chính diện ngang tầm ngực (Orthographic / Eye-level)**, không chụp góc hất từ dưới lên (Low-angle) hay góc dòm từ trên xuống (High-angle).

* 🔄 **Chế độ Multi-View / Turnaround Sheet (Tạo sinh vật liệu 360°)**:
  * Khi bạn muốn đối chiếu và hoàn thiện chất liệu Texture mặt sau/bên sườn, nên tạo bộ **4 góc ảnh**:
    1. **Mặt trước (Front View 0°)**
    2. **Góc nghiêng 3/4 (Three-Quarter 45°)**
    3. **Góc sườn ngang (Side Profile 90°)**
    4. **Mặt sau lưng (Back View 180°)**

* 🧍 **Quy Chuẩn Tư Thế (A-Pose vs T-Pose)**:
  * **Khuyên dùng số 1: A-Pose (Tay dang 45°)**: Hai cánh tay dang chéo tạo góc 45° so với thân sườn, ngón tay duỗi thả lỏng tự nhiên, hai chân đứng thẳng song song rộng bằng vai. Tư thế này giúp thuật toán UniRig nhận dạng rõ khớp vai, khuỷu tay, cổ tay và xương háng mà không bị căng cơ méo nách.
  * **Tránh tuyệt đối**: Khoanh tay trước ngực, đút tay túi quần, vắt chéo chân, tư thế ngồi hoặc nghiêng người.

---

## 3. 👔 Hướng Dẫn Trang Phục, Vật Dụng & Phụ Kiện (Garments & Accessories)

Để hệ thống trích xuất Mesh 3D sắc nét và gán trọng số xương (Skinning Weights) mượt mà, hãy lưu ý các chi tiết sau:

### 👕 3.1. Áo (Tops, Shirts, Jackets)
* **Tay áo & Thân sườn**: Tay áo phải tách rời khỏi sườn áo, có khe hở rõ ràng ở nách.
* **Nếp gấp vải**: Nếp nhăn vải vừa phải; tránh áo quá rộng thùng thình bay phấp phới hoặc áo choàng phủ kín ngực làm che mất vị trí khớp xương.
* **Đường may (Seams)**: Thêm từ khóa *“distinct seamlines, structured collar”* để AI vẽ rõ ranh giới các phần áo.

### 👖 3.2. Quần & Váy (Pants, Trousers, Skirts)
* **Khoảng hở đũng quần**: Hai đùi và ống quần phải có rãnh phân cách rõ ràng; không để 2 chân dính chùm thành một khối đặc.
* **Chiều dài ống quần**: Ống quần nên dừng ở trên mắt cá chân hoặc chạm vừa vặn cổ giày, không trùm kín che mất mu bàn chân.
* **Đối với Váy**: Nên là váy ngắn trên đầu gối hoặc váy xẻ để thấy được khớp đầu gối. Tránh váy dạ hội dài quét đất (vì UniRig sẽ không thể đoán được vị trí 2 chân bên trong).

### 👟 3.3. Giày & Dép (Shoes, Sneakers, Boots, Sandals)
* **Đế tiếp đất (Ground Plane)**: Hai đế giày/dép phải đặt phẳng trên cùng một mặt phẳng ngang, mũi chân hướng về phía trước.
* **Mắt cá chân**: Phân biệt rõ ranh giới giữa cổ chân và thân giày.
* **Độ dày đế**: Tránh đế giày biến dạng méo mó hoặc chìm lấp dưới mặt cỏ/nước.

### 💇 3.4. Tóc & Kiểu Tóc (Hair & Hairstyles)
* **Dạng khối tóc (Solid Hair Clumps)**: Ưu tiên kiểu tóc có lọn khối rõ ràng (solid anime clumps, structured short hair, ponytail buộc gọn).
* **Khoảng cách Cổ - Vai**: Tóc **KHÔNG ĐƯỢC** phủ kín toàn bộ cổ và vai. Phải chừa khoảng hở quanh cổ để khi xoay đầu, phần tóc không kéo dính rách mesh ngực/vai.

### 🧢 3.5. Mũ, Kính & Phụ Kiện Đầu (Hats, Helmets, Glasses)
* Mũ lưỡi trai, mũ len, nón giáp cần nằm ôm vừa vặn trên đầu; không đội mũ lệch quá mức hoặc quá rộng che kín nửa mặt.
* Kính mắt nên có gọng rõ nét ôm sát sống mũi và tai.

### 🎒 3.6. Đạo Cụ & Bàn Tay (Props & Hands)
* **Bàn tay**: Bàn tay phải mở tự nhiên hoặc nắm nhẹ (*relaxed open palms*), các ngón tay tách biệt nhau.
* **Đạo cụ (Vũ khí, Túi xách, Điện thoại)**: **KHÔNG** cho nhân vật cầm kiếm, súng, gậy hay túi xách trên tay trong ảnh tạo rig. Thuật toán sẽ nhầm vũ khí là xương ngón tay và kéo biến dạng. Đạo cụ nên được import gắn vào xương sau khi rig xong!

---

## 4. 🎨 Kỹ Thuật Tạo Sinh Vật Liệu 3D (PBR Textures & Lighting)

Để ảnh 2D khi nướng vào Texture map 3D lên màu chuẩn trong WebGL / Three.js / Blender:

1. **Ánh Sáng AI (Studio Lighting)**:
   * Luôn thêm từ khóa: `soft diffused studio lighting, even ambient illumination, neutral color temperature 5500K`.
   * **Tránh từ khóa**: `dramatic shadows, harsh contrast, volumetric god rays, intense rim light, dark cinematic shadows`. Bóng đổ đậm trên người sẽ bị "nướng chết" vào texture, khiến nhân vật quay sang hướng khác vẫn có vệt đen loang lổ.

2. **Từ Khóa Mô Tả Vật Liệu PBR**:
   * **Vải thường**: `matte cotton fabric texture, breathable knit pattern, realistic diffuse roughness`
   * **Vải Jeans**: `dark indigo denim texture, visible twill weave pattern`
   * **Da thuộc**: `smooth full-grain leather, subtle specular sheen, soft leather micro-creases`
   * **Kim loại**: `polished chrome surface, satin metallic brushed steel, clean metallic reflection`
   * **Cao su / Nhựa**: `matte rubber sole, satin thermoplastic polymer, clean material boundary`

---

## 5. 💡 Cú Pháp Tham Số Phổ Biến (Parameters Cheat-sheet)

* **Midjourney**: `--ar 3:4 --style raw --v 6.0 --no harsh shadows, floor reflection, holding items`
* **Stable Diffusion / WebUI**:
  * **Sampling**: DPM++ 2M Karras, 30 steps, CFG Scale 7.0
  * **Negative Prompt**: `deformed hands, missing fingers, extra limbs, bad anatomy, hands in pocket, crossed legs, floor shadow, dark background, motion blur, cropped head, cropped feet`
* **Flux.1**: `hyper-realistic full-body A-pose character concept, 8k, neutral studio lighting`
"""


def get_template_keys() -> List[str]:
    """Return list of template IDs."""
    return list(PROMPT_TEMPLATES.keys())


def get_template_choices() -> List[tuple]:
    """Return choices tuple for Gradio dropdown."""
    return [(v["title"], k) for k, v in PROMPT_TEMPLATES.items()]
