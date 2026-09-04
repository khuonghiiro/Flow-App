# Plan Nhân Vật: Lão Mộc (Lao Mu / Ancient Craftsman)

> **File:** `plans/lao-moc.plan_character_pipeline.md`  
> **Output Folder:** `agent-veo3/output/lao-moc/`  
> **Mô tả:** Nhân vật nam, 50-60 tuổi, thân hình gầy gò phong sương, quần áo lao động sản xuất thời xưa.

---

## Phần A — Mô Tả Nhân Vật

```yaml
character:
  name: "Lão Mộc (Lao Mu / Elder Craftsman)"
  gender: "male"
  age: "elderly (50-60 years old)"
  body_type: "thin slender gaunt lean weathered physique"
  personality: "Cần cù, khắc khổ, trầm mặc kiên định. Nghệ nhân / thợ mộc lao động thời xưa."
  combat_style: "Trượng pháp mộc công + Khí kình lao động dẻo dai"
  
  # Ngoại hình
  hair: "Messy salt-and-pepper grey hair tied in a modest high topknot with a weathered wooden pin, thin wispy side strands"
  skin: "Weathered sun-tanned bronze skin tone with subtle mature age lines"
  outfit: "Vintage ancient artisan coarse hemp cloth tunic with rolled-up sleeves, patched earthy brown trousers, straw rope belt sash, woven hemp arm wraps"
  primary_color: "Earthy Weathered Brown & Coarse Raw Linen (#6D4C41, #8D6E63)"
  accent_color: "Faded Indigo Blue & Straw Jute (#3949AB, #D7CCC8)"
  
  # Vũ khí & Dụng cụ
  weapon: "Weathered Ironwood Carving Staff / Heavy Artisan Mallet"
  spell_element: "Earthy Wood & Golden Amber Dust"
  
  # Art style
  style: "2D Xianxia/Ancient Chinese anime chibi style, bold clean linework, flat cel-shaded coloring"
  chroma_bg: "#00FF00"
```

---

## Phần B — Registry 5 Góc Xoay (Đã Hoàn Tất 100% — Đạt Chuẩn 10/10)

Toàn bộ ảnh được lưu tại thư mục:  
`d:\_DuAn\App_Desktop\workflows\Flow-My\AI-Render-Video\agent-veo3\output\lao-moc\`

| Góc | Trạng Thái | Media ID | File Ảnh Local | Đánh Giá Kiểm Định AI Agent |
|:---:|:---:|:---:|:---|:---|
| **0°** | `PASSED` (10/10) | `f0cb4f16-8794-4561-9952-03adf4dbb685` | `angle_0.png` | Mặt trơn mannequin, thân hình gầy 50-60t, áo vải thô nâu vá gối/vai, thắt lưng dây thừng, búi tóc trâm gỗ, phông xanh `#00FF00`. |
| **45°** | `PASSED` (10/10) | `2aab56f1-dd35-4445-ac3a-ad78a9ae09be` | `angle_45.png` (c1) | Thế bước chéo 45° sâu, chân trái vươn foreground, chân phải lùi sau, đầu xoay 45° lộ rõ tai và má phải. (Đạt xuất sắc từ batch 2 ứng viên). |
| **90°** | `PASSED` (10/10) | `d09f9851-fbf1-499b-876d-9c391ff896d0` | `angle_90.png` (c1) | **Thuần 90° mạn sườn trái tuyệt đối (100% Pure Profile)**, thân hình dẹt mỏng, vai và tay phải ẩn hoàn toàn phía sau thân người, hai chân hướng thẳng sang 9h. |
| **135°** | `PASSED` (10/10) | `6c37255a-bc7f-4139-b4c3-a44944fb7acb` | `angle_135.png` (c2) | Lưng lệch trái 135°, thấy rõ lưng áo nâu có đường may dọc, nơ thắt lưng dây thừng rủ đẹp, búi tóc trâm gỗ nhìn từ góc sau. |
| **180°** | `PASSED` (10/10) | `5c920e89-e213-4f8e-845a-ab1abab23e9d` | `angle_180.png` (c2) | Sau lưng 180° hoàn toàn đối xứng, hai gót chân hướng camera, cột sống thẳng đứng, lưng áo vải thô và búi tóc cân xứng tuyệt đối. |

---

## Phần C — Hành Động Tầng 3 (25 Video 4s Seamless Loop)

> [!NOTE]
> Áp dụng phân hóa **Lão Thợ / Người Già (50–60t)** kết hợp **Bộ Khung Khống Chế Chống Ảo Giác (Strict Anti-Glitch Lock)**: Khóa biên độ tay sát thắt lưng, cấm nhảy nhót, cấm múa tay loạn xạ.

1. **`walk` (Đi Bộ Lão Thợ 4s Loop — Bước Ngắn, Chậm Rãi, Trầm Tĩnh)**:
   - Dáng hơi khom phong sương, bước chân ngắn, đầm chắc tại chỗ.
   - Hai tay thả lỏng buông xuôi sát hông hoặc vịn cán trượng, vung góc hẹp cực nhỏ dưới thắt lưng.
   - Tuyệt đối cấm nhảy tưng tưng, cấm vung tay lên cao quá ngực, cấm biến dạng mặt trơn.
2. **`run` (Rảo Bước Khẩn Trương 4s Loop — Hurried Shuffling Jog)**:
   - Thân hình gầy gò chúi nhẹ về trước, bước chân dồn dập là là sát mặt sàn.
   - Trọng tâm hạ thấp, hai tay co nhẹ giữ chặt trước bụng/trượng để giữ thăng bằng.
   - Không nhấc gối quá cao, không nhảy bật lên không trung.
3. **`idle` (Thở Nhẹ Nghỉ Ngơi 4s Loop)**: Đứng chống trượng mộc/búa, ngực phập phồng nhẹ nhàng, dải dây thừng lay chuyển tĩnh tại.
4. **`attack` (Vung Trượng Gõ Búa 4s Loop)**: Bổ mạnh công cụ mộc công về phía trước tóe bụi hoàng kim rồi thu về thế thủ vững vàng.
5. **`defend` (Dựng Khiên Gỗ / Chắn Trượng 4s Loop)**: Dựng đứng thân trượng gỗ chắn ngang ngực thế thủ vững như bàn thạch.
