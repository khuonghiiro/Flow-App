# Plan Nhân Vật: Thanh Hư Tiên Tông — Lăng Vân Chu (Ling Yunchu)

## Thông Tin Dự Án Google Flow
- **Project Name**: `Thanh Hư Tiên Tông - Lăng Vân Chu`
- **Project ID**: `1bbd5b67-bb64-4726-a6f7-6a0060a06718`
- **Flow Web URL**: [https://labs.google/fx/tools/flow/project/1bbd5b67-bb64-4726-a6f7-6a0060a06718](https://labs.google/fx/tools/flow/project/1bbd5b67-bb64-4726-a6f7-6a0060a06718)
- **Output Folder**: `agent-veo3/output/lang-van-chu/`

---

## Phần A — Mô Tả Nhân Vật

```yaml
character:
  name: "Lăng Vân Chu (Ling Yunchu) — Thiếu Chủ Thanh Hư Tiên Tông"
  gender: "male"
  age: "young cultivator (21-22 years old appearance)"
  personality: "Thanh lãnh, trầm ổn, cao quý, xuất trần phiêu dật"
  combat_style: "Hư không chưởng ấn / Chưởng pháp khí công tay không"
  
  # Ngoại hình
  hair: "Mái tóc đen nhánh búi cao đỉnh đầu cài trâm bạc thanh nhã và quan nhỏ bạc (high topknot with silver hairpin), hai dải lụa trắng buông nhẹ hai bên tai, tóc đen suối dài buông thẳng tự nhiên sau lưng"
  skin: "Tông màu da trắng ngà / trắng sáng mịn màng thanh khiết chuẩn Ảnh 1 (Smooth pale fair ivory-white skin tone), màu da mặt, cổ và tay đồng nhất 100% không tì vết"
  outfit: "Đạo bào tu tiên lụa mờ màu xám bồ câu nhạt (Soft pale dove-grey matte daoist robe) phối áo lót trắng sương (frost-white inner robe), viền thêu chỉ tơ mực đen thanh nhã (delicate ink-black cloud trim), quần thụng trắng sương và ủng vải đen đế bằng (flat black cloth boots with white sole, zero heels). CHẤT LIỆU VẢI PHẲNG MỜ (FLAT MATTE), TUYỆT ĐỐI KHÔNG BÓNG BẨY, KHÔNG ÁNH BẠC PHẢN QUANG ẢO"
  primary_color: "Xám bồ câu nhạt mờ (Pale Dove-Grey Matte) & Trắng sương (Frost White)"
  accent_color: "Chỉ tơ mực đen (Ink Black) & Bạc thanh khiết (Silver)"
  
  # Quy chuẩn đai lưng & nơ (BẮT BUỘC RÕ RÀNG)
  waist_belt_logic:
    male: "Đai lưng đen bản rộng viền dải nẹp bạc trên dưới, ở giữa có khóa bạch ngọc lồng khung bạc hình học vát góc ôm sát eo phẳng phiu đồng bộ 100% như Ảnh 1. Phía sau lưng (135° & 180°) TUYỆT ĐỐI LÀ ĐAI PHẲNG TRƠN LIÊN TỤC, CẤM VẼ NƠ THẮT (strictly continuous flat belt band behind back, ZERO bow, ZERO ribbon knot)."
  
  # Độ rũ trang phục (Trọng lực, cấm gió giả tạo)
  fabric_physics: "Quần áo, tà áo và tay áo rủ thẳng tự nhiên theo trọng lực (natural downward drape under gravity, calm static fabric). TUYỆT ĐỐI KHÔNG để gió thổi hất vạt áo bay cao ra đằng sau ở góc 45° và 90°."
  
  # Vũ khí & Đạo cụ (BẮT BUỘC KHÔNG CÓ)
  weapon: "None (TUYỆT ĐỐI KHÔNG mang vũ khí, kiếm, đao hay pháp bảo trên người/sau lưng)"
  spell_element: "Thanh Hư chân khí / linh khí sương bạc"
  
  # Art style
  style: "2D Xianxia anime chibi sprite style, clean linework, flat cel-shaded coloring, harmonious natural palette, strictly zero neon glow, zero specular shiny reflections"
  chroma_bg: "#00FF00"
```

---

## Phần B — Tiến Độ Thực Thi 3 Tầng

### Tầng 1: Tạo Ảnh Gốc 0° (Master Root)
- **Status**: `COMPLETED (10/10 - Đồng bộ 100% Ảnh 1)`
- **Media ID**: `ffe04bbd-4dbb-4026-be49-7f7a84be9953`
- **File**: `agent-veo3/output/lang-van-chu/angle_0.png`
- **Đánh giá**: Đứng chính diện 100% (0.0° strict front view), 2 vai ngang bằng cân đối tuyệt đối. Màu da mặt trắng sáng mịn màng đồng nhất 100% với da cổ và bàn tay, chuẩn khớp với Ảnh 1. Chất vải xám bồ câu mờ phẳng (matte), không bóng bẩy, không ánh bạc ảo. Đai lưng có khung bạc ngọc bội vát góc và 2 đường nẹp viền bạc chuẩn 100% thiết kế Ảnh 1.

### Tầng 2: 4 Góc Còn Lại (Quy Chuẩn Tham Chiếu Đồng Bộ Toàn Bộ 5 Góc)
- **Status**: `COMPLETED (10/10 cho cả 4 góc đồng bộ màu da & trang phục)`
- **45° (Nghiêng 3/4)**: `angle_45.png` | Media ID: `1db3c44d-038e-41b5-a2c8-9fab1a170740` *(Ảnh 1 mẫu chuẩn)*
  - Mặt trắng sáng, chất vải matte xám nhạt không bóng, đai lưng ngọc bội khung bạc vát góc chuẩn đẹp.
  - Tà áo rũ tự nhiên theo trọng lực, không vểnh bay.
- **90° (Mạn Sườn)**: `angle_90.png` | Media ID: `117ef0cb-ab15-473f-a267-d5a3c392a070` *(ref: [0°, 45°])*
  - Góc nghiêng sườn trái 90° chuẩn tuyệt đối, da trắng sáng, vải matte xám rũ thẳng đứng dọc thân người, đai nẹp bạc ôm phẳng ngang eo.
- **180° (Sau Lưng)**: `angle_180.png` | Media ID: `9373315a-d67e-45a3-a38d-d9aba582c46b` *(ref: [0°])*
  - Đai lưng sau phẳng trơn liên tục ôm sát eo, **TUYỆT ĐỐI KHÔNG CÓ NƠ THẮT SAU LƯNG**. Tà áo rũ thẳng tự nhiên theo trọng lực, chất vải matte không bóng.
- **135° (Lưng Lệch Trái)**: `angle_135.png` | Media ID: `60b42cfc-4b75-4fbf-92fa-27e7932d9025` *(ref kép: [0°, 180°])*
  - Góc 3/4 sau lưng, đai lưng sau phẳng trơn không nơ khớp 100% với ảnh 180°, chất vải matte và da cổ trắng sáng đồng bộ.

### Tầng 3: Video Loop 4s & 1080p
- 5 hành động (`di-bo/`, `dung-yen/`, `chay/`, `danh-cong/`, `phong-thu/`) x 5 góc.
