# Plan Nhân Vật: Linh Nguyệt Tiên Cơ — Vân Hi (Yun Xi)

## Thông Tin Dự Án Google Flow
- **Project Name**: `Linh Nguyệt Tiên Cơ - Vân Hi`
- **Project ID**: `698fe101-ecbe-4102-8474-e27c1d350643`
- **Flow Web URL**: [https://labs.google/fx/tools/flow/project/698fe101-ecbe-4102-8474-e27c1d350643](https://labs.google/fx/tools/flow/project/698fe101-ecbe-4102-8474-e27c1d350643)
- **Output Folder**: `agent-veo3/output/van-hi/`

---

## Phần A — Mô Tả Nhân Vật

```yaml
character:
  name: "Vân Hi (Yun Xi) — Linh Nguyệt Tiên Cơ"
  gender: "female"
  age: "young maiden (19-20 years old appearance)"
  personality: "Thanh nhã, điềm đạm, đoan trang, thuần tịnh xuất trần"
  combat_style: "Thủ ấn linh phong / chưởng pháp khí công tay không"
  
  # Ngoại hình
  hair: "Mái tóc đen tuyền mềm mại buông xõa dài, búi nửa đầu thanh nhã cài trâm bạc hoa sen và dải lụa lam nhạt buông dài"
  skin: "Tông màu da tự nhiên trắng hồng hào (Fair natural warm peach skin tone), màu da mặt và da cổ đồng nhất 100%, tuyệt đối không lớp men trắng bóng"
  outfit: "Đạo bào lụa nguyệt bạch phối tà lụa xanh lam khói nhạt thướt tha, tay áo rộng, đai lưng lụa thắt hoa mai phối ngọc bội thanh ngọc, giày vải thêu hoa sen đế bằng (flat cloth shoes, zero heels)"
  primary_color: "Nguyệt bạch (Moonlit White) & Lam khói nhạt (Soft Mist Blue)"
  accent_color: "Bạc thanh nhã (Soft Silver) & Thanh ngọc (Celadon Jade)"
  
  # Vũ khí & Đạo cụ (BẮT BUỘC KHÔNG CÓ)
  weapon: "None (TUYỆT ĐỐI KHÔNG mang vũ khí, kiếm hay đạo cụ để tránh AI render méo góc)"
  spell_element: "Linh khí gió êm dịu (Soft ambient wind)"
  
  # Art style
  style: "2D Xianxia/Fantasy anime chibi sprite style, bold clean linework, flat cel-shaded coloring, harmonious natural palette, strictly zero neon glow"
  chroma_bg: "#00FF00"
```

---

## Phần B — Tiến Độ Thực Thi 3 Tầng

### Tầng 1: Tạo Ảnh Gốc 0° (Master Root)
- **Status**: `COMPLETED (10/10)`
- **Media ID**: `27e15856-1b40-4730-a832-d7692a8f0ad3`
- **File**: `agent-veo3/output/van-hi/angle_0.png`
- **Đánh giá**: Chibi 2D tiên hiệp thuần mỹ, mặt trơn da tự nhiên đồng nhất hoàn hảo với da cổ và tay (zero white mask), trang phục nguyệt bạch phối lam khói thanh nhã, không vũ khí, không neon, giày vải đế bằng.

### Tầng 2: 4 Góc Còn Lại (Multi-Candidate Song Song)
- **Status**: `COMPLETED (10/10 cho cả 4 góc)`
- **45°**: `angle_45.png` | Media ID: `8b60a7a1-33c8-43c4-b288-b95e255a16ed` (Xoay 45° chéo 10h, thấy contour má và tai trái, chân bước chéo)
- **90°**: `angle_90.png` | Media ID: `c31fb878-6c1c-4b58-b2a3-55ce25435a1e` (Nhìn nghiêng trái 100% hướng 9h, sườn mỏng, che khuất 100% nửa thân phải)
- **135°**: `angle_135.png` | Media ID: `078a952e-4945-4bb8-8529-109e316e9c5b` (Lưng lệch trái 135°, dải nơ thắt lưng sau đối xứng chuẩn xác)
- **180°**: `angle_180.png` | Media ID: `0ac777a5-63c9-4be2-aa13-bb77265bbb36` (Sau lưng 100% đối xứng, tóc buông xõa dài, trâm bạc, gót chân đều)

### Tầng 3: 25 Video Loop 4s & 1080p
- Thư mục con:
  - `agent-veo3/output/van-hi/di-bo/` (5 videos)
  - `agent-veo3/output/van-hi/dung-yen/` (5 videos)
  - `agent-veo3/output/van-hi/chay/` (5 videos)
  - `agent-veo3/output/van-hi/danh-cong/` (5 videos)
  - `agent-veo3/output/van-hi/phong-thu/` (5 videos)
