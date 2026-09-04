# Plan Nhân Vật: Cửu U Kiếm Các — Dạ Vô Nhai (Ye Wunya)

## Thông Tin Dự Án Google Flow
- **Project Name**: `Cuu U Kiem Cac - Da Vo Nhai`
- **Project ID**: `7de76f25-e719-452f-8a03-fb6eece897e4`
- **Flow Web URL**: [https://labs.google/fx/tools/flow/project/7de76f25-e719-452f-8a03-fb6eece897e4](https://labs.google/fx/tools/flow/project/7de76f25-e719-452f-8a03-fb6eece897e4)
- **Output Folder**: `agent-veo3/output/da-vo-nhai/`

---

## Phần A — Mô Tả Nhân Vật

```yaml
character:
  name: "Dạ Vô Nhai (Ye Wunya) — Thiếu Các Chủ Cửu U Kiếm Các"
  gender: "male"
  age: "young cultivator (22-23 years old appearance)"
  personality: "Lãnh khốc, cô ngạo, vương giả, thâm sâu khó lường"
  combat_style: "Hư Không Kiếm Chỉ / Chưởng Khí Tay Không"
  
  # Ngoại hình
  hair: "Mái tóc đen tuyền suối mượt búi cao đỉnh đầu với kim quan vàng kim thanh nhã và trâm cài vàng (high topknot with sleek golden coronet and hairpin), hai dải lọn tóc mai mảnh buông nhẹ hai bên thái dương, tóc suối dài đen mượt buông thẳng tự nhiên sau lưng"
  skin: "Tông màu da trắng ngà mịn màng thanh khiết (Smooth pale fair ivory-white skin tone), màu da mặt, cổ và hai bàn tay đồng nhất 100% không tì vết"
  outfit: "Đạo bào võ hiệp huyền huyễn nhiều lớp siêu chi tiết: Lớp trong là áo lụa trắng tuyết viền chỉ vàng kim cổ chéo (crisp white silk inner robe with gold filigree collar); Lớp ngoài là đại bào màu lam dạ thẫm / mực xanh đêm (midnight ink-navy outer daoist martial robe) thêu chỉ vàng họa tiết tường vân uốn lượn tinh xảo (intricate flowing golden cloud and dragon-rune embroidery along lapels, cuffs, and hem borders); Quần thụng trắng tuyết và ủng vải đen thêu chỉ vàng đế bằng (flat black cloth boots with white flat soles, strictly zero heels). CHẤT LIỆU VẢI PHẲNG MỜ (FLAT MATTE), TUYỆT ĐỐI KHÔNG BÓNG BẨY, KHÔNG PHẢN QUANG ÁNH KIM ẢO"
  primary_color: "Lam Dạ Thẫm (Midnight Ink-Navy) & Tuyết Bạch (Snow White)"
  accent_color: "Chỉ Vàng Kim Cổ Điển (Antique Gold Embroidery) & Kim Quan (Gold Accents)"
  
  # Quy chuẩn đai lưng & nơ (BẮT BUỘC RÕ RÀNG)
  waist_belt_logic:
    male: "Đai lưng đen bản rộng nẹp 2 đường viền chỉ vàng kim trên dưới sắc sảo, ở giữa cài ngọc bội bạch ngọc tròn bọc khung vàng kim nổi bật (broad black silk waist sash with dual gold metallic borders and a circular white jade medallion framed in gold). Phía sau lưng (135° & 180°) TUYỆT ĐỐI LÀ ĐAI PHẲNG TRƠN LIÊN TỤC, CẤM VẼ NƠ THẮT (strictly continuous flat belt band behind back, ZERO bow, ZERO ribbon knot)."
  
  # Độ rũ trang phục (Trọng lực, cấm gió giả tạo)
  fabric_physics: "Quần áo, tà áo và tay áo rủ thẳng tự nhiên theo trọng lực (natural downward drape under gravity, calm static fabric). TUYỆT ĐỐI KHÔNG để gió thổi hất vạt áo bay cao ra đằng sau ở góc 45° và 90°."
  
  # Vũ khí & Đạo cụ (BẮT BUỘC KHÔNG CÓ)
  weapon: "None (TUYỆT ĐỐI KHÔNG mang kiếm trên tay hay sau lưng, hai tay buông tự nhiên)"
  spell_element: "Cửu U Kiếm Khí / U Lam Linh Khí"
  
  # Art style
  style: "2D Xianxia anime chibi sprite style, clean linework, flat cel-shaded coloring, harmonious rich palette, strictly zero neon glow, zero specular shiny reflections"
  chroma_bg: "#00FF00"
```

---

## Phần B — Kế Hoạch 3 Tầng

- **Tầng 1**: Sinh ảnh gốc 0° (Master Root) đối xứng hoàn hảo, da trắng sáng, áo nhiều chi tiết thêu chỉ vàng kim lộng lẫy, đai lưng ngọc tròn viền vàng.
- **Tầng 2**: Sinh 4 góc còn lại (45°, 90°, 135°, 180°) dùng chuẩn tham chiếu hai pha nội suy.
- **Tầng 3**: Sinh video 4s seamless loop cho các góc và tải về `output/da-vo-nhai/videos/`.
