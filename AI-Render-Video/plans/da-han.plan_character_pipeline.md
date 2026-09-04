# Plan Nhân Vật: Dạ Hàn (Ye Han / Shadow Blade)

> **File:** `plans/da-han.plan_character_pipeline.md`  
> **Output Folder:** `agent-veo3/output/da-han/`  
> **Trạng thái:** Đã hoàn thành 100% Bộ 5 Góc Xoay Đạt Chuẩn (0°, 45°, 90°, 135°, 180°)

---

## Phần A — Mô Tả Nhân Vật

```yaml
character:
  name: "Dạ Hàn (Ye Han / Shadow Blade)"
  gender: "male"
  age: "young adult (20-22)"
  personality: "Lạnh lùng, quyết đoán, phong trần bí ẩn. Hắc y kiếm khách."
  combat_style: "Ám ảnh tốc độ kiếm thuật + Hắc lôi âm ảnh"
  
  # Ngoại hình
  hair: "Layered spiky long snowy white hair tied in a high ponytail with silver ring, sharp side bangs"
  skin: "Fair pale porcelain skin tone"
  outfit: "Sleek layered pitch-black Xianxia martial artist robes with silver trim, dark leather vambraces and wide belt sash"
  primary_color: "Midnight Obsidian Black (#111111)"
  accent_color: "Polished Silver & Dark Charcoal"
  
  # Vũ khí & Phép thuật
  weapon: "Obsidian Dark Katana / Black Shadow Sword"
  spell_element: "Dark Shadow & Silver Spark Aura"
  
  # Art style
  style: "2D Xianxia/Fantasy anime chibi style, bold clean linework, flat cel-shaded coloring"
  chroma_bg: "#00FF00"
```

---

## Phần B — Registry 5 Góc Xoay Cơ Thể (Đã Hoàn Thành)

Toàn bộ ảnh đã được kiểm định trực quan qua `view_file` bởi AI Agent và lưu tại thư mục:  
`d:\_DuAn\App_Desktop\workflows\Flow-My\AI-Render-Video\agent-veo3\output\da-han\`

| Góc | Trạng Thái | Media ID (Google Flow) | File Ảnh Local | Đánh Giá Kiểm Định AI Agent |
|:---:|:---:|:---:|:---|:---|
| **0°** | `COMPLETED` | `03052098-9c98-4e83-8982-aa2dda8e7b86` | [angle_0.png](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/da-han/angle_0.png) | Trực diện đối xứng 100%, mặt trơn mannequin, tóc trắng đuôi ngựa cao, đạo bào đen viền bạc, phông `#00FF00`. |
| **45°** | `COMPLETED` | `28335fe0-9eb6-4d68-b5a7-27487d852916` | [angle_45.png](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/da-han/angle_45.png) | Góc 45° sâu (Deep 3/4): thế bước chéo bất đối xứng, vai trái vươn foreground, vai phải lùi sau, thân xoay 45°. |
| **90°** | `COMPLETED` | `484640f6-876e-46fc-984d-73e6de236c47` | [angle_90.png](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/da-han/angle_90.png) | Thuần 90° Side Profile 100%: Lát cắt mạn sườn mảnh mai, vai phải bị che khuất 100%, mũi chân chỉ hoàn toàn sang trái. |
| **135°** | `COMPLETED` | `5e67264a-12e2-40f6-a897-17b4f5d86267` | [angle_135.png](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/da-han/angle_135.png) | Lưng lệch trái 135°: Camera nhìn từ sau-phải, thấy trọn lưng áo, búi tóc sau, dải thắt lưng sau rủ xuống. |
| **180°** | `COMPLETED` | `c7bce5fd-d5ef-4f64-9484-133fff751697` | [angle_180.png](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/da-han/angle_180.png) | Sau lưng 180° đối xứng: Gáy, đuôi tóc trắng dài, lưng đạo bào đen, hai gót chân hướng thẳng camera. |

---

## Phần C — Hành Động Tầng 3 (25 Video 4s Seamless Loop)

Quy chuẩn: **Start Frame = End Frame = Media ID của góc tương ứng** (thời lượng 4 giây).

### Bảng 5 Hành Động Phù Hợp Nhân Vật Dạ Hàn:

1. **`walk` (Đi Bộ 4s Loop)**:
   - Bước chân dứt khoát tại chỗ, tay vung nhịp nhàng, tà đạo bào đen và đuôi ngựa trắng lay nhẹ theo nhịp bước.
2. **`idle` (Thế Thủ Đứng Yên 4s Loop)**:
   - Thở nhẹ tại chỗ, ngực phập phồng tinh tế, đuôi tóc và vạt áo đen lay chuyển nhẹ trong gió tà, tay đặt hờ bên chuôi kiếm hắc ngọc.
3. **`run` (Chạy Nhanh 4s Loop)**:
   - Thân người hơi chúi về phía trước, bước chạy nhanh tại chỗ, đuôi tóc trắng bay mạnh về phía sau.
4. **`attack` (Ám Ảnh Kiếm Vũ 4s Loop)**:
   - Rút thanh Obsidian Katana chém một đường kiếm bán nguyệt tóe tia sét bạc và hắc lôi rồi thu kiếm hoàn mỹ về thế thủ ban đầu.
5. **`defend` (Phong Thủ Kiếm Thuẫn 4s Loop)**:
   - Vung kiếm chắn chéo trước ngực, chân hạ trọng tâm vững chãi, luồng khí âm ảnh bao bọc cơ thể, kết thúc quay về tư thế chuẩn.

---

## Phần D — Hướng Dẫn Kích Hoạt Tầng 3

Chạy lệnh tự động 5 slots song song liên tục để sinh 25 video vào thư mục `agent-veo3/output/da-han/videos/`:
```bash
& "agent-veo3/.venv/Scripts/python.exe" scratch/run_stage3_sliding_window.py --character da-han
```
