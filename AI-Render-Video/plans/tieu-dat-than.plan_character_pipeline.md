# Plan Nhân Vật: Cửu Châu Tiên Môn — Tiêu Dật Thần (Xiao Yichen)

## Thông Tin Dự Án Google Flow
- **Project Name**: `Cửu Châu Tiên Môn - Tiêu Dật Thần`
- **Project ID**: `79a7e17f-ebe3-4f0d-8c85-f270e4c03a7d`
- **Flow Web URL**: [https://labs.google/fx/tools/flow/project/79a7e17f-ebe3-4f0d-8c85-f270e4c03a7d](https://labs.google/fx/tools/flow/project/79a7e17f-ebe3-4f0d-8c85-f270e4c03a7d)
- **Output Folder**: `agent-veo3/output/tieu-dat-than/`

---

## Phần A — Mô Tả Nhân Vật

```yaml
character:
  name: "Tiêu Dật Thần (Xiao Yichen) — Cửu Châu Tiên Môn Đệ Tử"
  gender: "male"
  age: "young cultivator (20-21 years old appearance)"
  personality: "Điềm đạm, cương trực, khí chất thiếu hiệp tu tiên tuấn lãng"
  combat_style: "Hư không chưởng ấn / Chưởng pháp khí công tay không"
  
  # Ngoại hình
  hair: "Mái tóc đen nhánh búi cao đỉnh đầu cài ngọc quan bạc tinh xảo (high topknot with silver jade hair crown), dải lụa trắng nhẹ bay, tóc đen suối dài buông tự nhiên sau lưng"
  skin: "Tông màu da tự nhiên khỏe mạnh hồng hào sáng (Fair warm natural healthy skin tone), tuyệt đối không lớp men trắng bóng, màu da mặt và da cổ đồng nhất 100%"
  outfit: "Đạo bào tu tiên màu lam thẫm (Deep Royal Azure silk robe) phối áo lót trắng tuyết (pure white inner robe), viền thêu vân mây bạc (silver cloud trim), đai lưng lụa xanh đen thắt gọn phối ngọc bội bạch ngọc tròn, quần thụng trắng và ủng vải võ hiệp đen viền trắng đế bằng phẳng (flat martial cloth boots, zero heels)"
  primary_color: "Lam thẫm (Deep Royal Azure) & Trắng tuyết (Pure Snow White)"
  accent_color: "Bạc sương vân mây (Silver Clouds) & Bạch ngọc (White Jade)"
  
  # Vũ khí & Đạo cụ (BẮT BUỘC KHÔNG CÓ)
  weapon: "None (TUYỆT ĐỐI KHÔNG mang kiếm, không cầm đao hay pháp bảo để tránh AI render méo góc)"
  spell_element: "Linh khí lôi vân / khí công thanh khiết"
  
  # Art style
  style: "2D Xianxia anime chibi sprite style, clean linework, flat cel-shaded coloring, harmonious natural palette, strictly zero neon glow"
  chroma_bg: "#00FF00"
```

---

## Phần B — Tiến Độ Thực Thi 3 Tầng

### Tầng 1: Tạo Ảnh Gốc 0° (Master Root)
- **Status**: `COMPLETED (10/10)`
- **Media ID**: `856d88a1-764e-4a3b-b46f-501e24d415e6`
- **File**: `agent-veo3/output/tieu-dat-than/angle_0.png`
- **Đánh giá**: Đạo bào lam thẫm thêu vân mây bạc, áo lót trắng, búi ngọc quan bạc, tóc suối đen, mặt không ngũ quan tông da tự nhiên ấm áp hồng hào đồng nhất 100% với cổ và tay, không vũ khí, không neon, ủng vải võ hiệp đen viền trắng đế bằng phẳng.

### Tầng 2: 4 Góc Còn Lại (Quy Chuẩn Tham Chiếu Hai Pha Đồng Bộ 100%)
- **Status**: `COMPLETED (10/10 cho cả 4 góc)`
- **45° (Nghiêng 3/4)**: `angle_45.png` | Media ID: `2a6b8421-daf3-49e6-8026-d26873332569` *(ref: [0°, 90°])*
  - **Khắc phục lỗi vai/thân người**: Vai trái chủ động đưa ra tiền cảnh hạ thấp theo luật phối cảnh, vai phải thu sâu về phía sau. Mặt phẳng ngực và thân người xoay chéo 45° rõ rệt hướng 10h30. Vạt áo cổ chéo và nơ ngọc bội nghiêng theo góc 3/4. Hai mũi chân đặt chéo hướng 10h.
- **180° (Sau Lưng)**: `angle_180.png` | Media ID: `c0661381-f281-4d70-b879-fba0cad03995` *(ref: [0°])*
  - Lưng thẳng, nơ đai lưng xanh đen thắt sau lưng buông hai dải lụa, vân mây bạc thêu chân vạt áo, tóc suối suôn mượt sau lưng.
- **90° (Mạn Sườn)**: `angle_90.png` | Media ID: `a89bf807-02cd-416c-bc3a-3ba0e40daf02` *(ref: [0°])*
  - Góc nghiêng sườn trái 90° chuẩn tuyệt đối, che khuất 100% nửa thân phải, mũi chân trái hướng 9h, ngọc bội bên sườn hông.
- **135° (Lưng Lệch Trái)**: `angle_135.png` | Media ID: `169760e6-eaf9-4ce5-9bcc-80ccc458b88c` *(ref kép: [0°, 180°])*
  - Góc nhìn 3/4 sau lưng, nơ đai lưng và dải lụa trùng khớp 100% với ảnh 180°, vai trái ở tiền cảnh, vai phải thu sâu vào hậu cảnh.

### Tầng 3: Video Loop 4s & 1080p
- 5 hành động (`di-bo/`, `dung-yen/`, `chay/`, `danh-cong/`, `phong-thu/`) x 5 góc.
