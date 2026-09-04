# Plan Nhân Vật: Lan Đình Tiên Cơ — Thẩm Nhược Lan (Shen Ruolan)

## Thông Tin Dự Án Google Flow
- **Project Name**: `Lan Đình Tiên Cơ - Thẩm Nhược Lan`
- **Project ID**: `29951668-e794-44cb-81e6-dfa24338fc53`
- **Flow Web URL**: [https://labs.google/fx/tools/flow/project/29951668-e794-44cb-81e6-dfa24338fc53](https://labs.google/fx/tools/flow/project/29951668-e794-44cb-81e6-dfa24338fc53)
- **Output Folder**: `agent-veo3/output/tham-nhuoc-lan/`

---

## Phần A — Mô Tả Nhân Vật

```yaml
character:
  name: "Thẩm Nhược Lan (Shen Ruolan) — Lan Đình Tiên Cơ"
  gender: "female"
  age: "young maiden (18-19 years old appearance)"
  personality: "Thanh nhã, đoan trang, dịu dàng, thuần khiết như hoa lan sớm"
  combat_style: "Thủ ấn linh mộc / chưởng pháp khí công tay không"
  
  # Ngoại hình
  hair: "Mái tóc đen tuyền mượt mà búi đôi hồ điệp hai bên kết hợp tóc suối buông dài qua vai, cài trâm ngọc bích chạm hoa lan trắng và dải lụa mỏng màu ngọc bích nhạt buông rủ"
  skin: "Tông màu da tự nhiên trắng hồng hào (Fair natural warm peach skin tone), màu da mặt và da cổ đồng nhất 100%, tuyệt đối không lớp men trắng bóng hay lệch tông"
  outfit: "Đạo bào lụa men ngọc bích nhạt (pale celadon / seafoam jade) phối áo trong trắng tuyết, viền thêu hoa lan bạc sương, tay áo rộng thướt tha, đai lưng lụa thắt nơ hồ điệp phối ngọc bội hoa lan tròn, váy xếp ly mềm mại, giày vải thêu hoa lan đế bằng (flat cloth shoes, zero heels)"
  primary_color: "Men ngọc bích nhạt (Pale Celadon Jade) & Trắng tuyết (Pure Snow White)"
  accent_color: "Bạc sương thanh nhã (Soft Silver) & Ngọc thạch (Jadeite Green)"
  
  # Vũ khí & Đạo cụ (BẮT BUỘC KHÔNG CÓ)
  weapon: "None (TUYỆT ĐỐI KHÔNG mang vũ khí, kiếm hay đạo cụ để tránh AI render méo góc)"
  spell_element: "Linh khí mộc dịu mát (Gentle green floral breeze)"
  
  # Art style
  style: "2D Xianxia/Fantasy anime chibi sprite style, bold clean linework, flat cel-shaded coloring, harmonious natural palette, strictly zero neon glow"
  chroma_bg: "#00FF00"
```

---

## Phần B — Tiến Độ Thực Thi 3 Tầng

### Tầng 1: Tạo Ảnh Gốc 0° (Master Root)
- **Status**: `COMPLETED (10/10)`
- **Media ID**: `7e567363-3e90-4e9a-a51b-eb9e0748e206`
- **File**: `agent-veo3/output/tham-nhuoc-lan/angle_0.png`
- **Đánh giá**: Chibi 2D thanh khiết tuyệt đẹp, búi tóc hồ điệp cài hoa lan, da tự nhiên trắng hồng đồng nhất 100% với cổ và tay, không vũ khí, không neon, giày vải thêu hoa lan đế bằng phẳng.

### Tầng 2: 4 Góc Còn Lại (Quy Chuẩn Tham Chiếu Hai Pha Đồng Bộ 100%)
- **Status**: `COMPLETED (10/10 cho cả 4 góc)`
- **45° (Nghiêng 3/4)**: `angle_45.png` | Media ID: `3428e2de-1784-45d6-9d2d-5f83a51dedbb` *(ref: [0°, 90°])*
  - Vai trái chủ động đẩy ra phía trước (tiền cảnh), vai phải thu sâu về phía sau theo luật phối cảnh 3D. Thân người và mặt phẳng ngực xoay chéo 45° rõ rệt hướng 10h30. Vạt áo cổ chéo và nơ ngọc bội nghiêng theo góc 3/4. Búi tóc hồ điệp đôi, hoa lan trắng và dải lụa buông rủ hoàn toàn đồng bộ với ảnh 0°. Hai mũi chân xoay chéo 10h.
- **180° (Sau Lưng)**: `angle_180.png` | Media ID: `d3ce9ec7-89a9-419b-bb57-780a3d3f00d6` *(ref: [0°])*
  - Sống lưng thẳng tắp, tóc suối đen cài trâm hoa lan, nơ đai lưng lụa xanh thắt sau lưng và viền hoa bạc đối xứng hoàn mỹ.
- **90° (Mạn Sườn)**: `angle_90.png` | Media ID: `d8dec0a1-bf89-4d9e-9ae9-246fc247e91d` *(ref kép: [0°, 45°])*
  - Lát cắt mạn sườn trái 90° nội suy chính xác từng nếp váy, che khuất 100% thân phải, mũi chân hướng 9h.
- **135° (Lưng Lệch Trái)**: `angle_135.png` | Media ID: `105b0e59-69c4-4a9f-91c2-73f6d9c7dfd7` *(ref kép: [0°, 180°])*
  - Khớp nối hoàn hảo giữa mặt trước 0° và mặt sau 180°, nơ đai lưng xanh sau và viền hoa bạc trùng khớp 100% với ảnh 180°, triệt tiêu hoàn toàn lỗi lệch trang phục!

### Tầng 3: 25 Video Loop 4s & 1080p
- Thư mục con:
  - `agent-veo3/output/tham-nhuoc-lan/di-bo/` (5 videos)
  - `agent-veo3/output/tham-nhuoc-lan/dung-yen/` (5 videos)
  - `agent-veo3/output/tham-nhuoc-lan/chay/` (5 videos)
  - `agent-veo3/output/tham-nhuoc-lan/danh-cong/` (5 videos)
  - `agent-veo3/output/tham-nhuoc-lan/phong-thu/` (5 videos)
