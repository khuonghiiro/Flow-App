# Plan: Tự Động Hóa Tạo Bộ Nhân Vật Từ Cây Kỹ Năng (Tab 4)

## Tổng Quan

Plan này là template tái sử dụng cho AI agent. Khi muốn tạo nhân vật mới, chỉ cần:
1. Thay mô tả nhân vật ở **Phần A** (0° chính diện)
2. AI agent tự phân tích tính cách/kỹ năng nhân vật → điều chỉnh prompt action cho phù hợp
3. Chạy pipeline 3 tầng tự động

---

## Phần A — Mô Tả Nhân Vật (THAY ĐỔI KHI TẠO NHÂN VẬT MỚI)

```yaml
# ═══ THAY ĐỔI PHẦN NÀY CHO NHÂN VẬT MỚI ═══
character:
  name: "Lâm Tiêu (Lin Xiao)"
  gender: "female"
  age: "young adult (18-20)"
  personality: "Thanh thoát, lạnh lùng nhưng thiện lương. Kiếm khách tu tiên."
  combat_style: "Kiếm thuật phiêu diêu + Băng giá nguyên tố"
  
  # Ngoại hình
  hair: "Long silky platinum white hair with twin front braids, glowing jade hairpin"
  skin: "Fair porcelain skin tone"
  outfit: "Xianxia flowing silk daoist robes with wide sleeves, floating ribbons"
  primary_color: "Pure White & Soft Cyan Jade (#00E5FF)"
  accent_color: "Soft Platinum & Lilac Purple"
  
  # Vũ khí & Phép thuật
  weapon: "Enchanted Flying Sword (Lam Ngoc Kiem)"
  spell_element: "Ice & Cyan Frost Aura"
  
  # Art style
  style: "2D Xianxia/Fantasy anime chibi style, bold clean linework, flat cel-shaded coloring"
  chroma_bg: "#00FF00"
```

---

## Phần B — Pipeline 3 Tầng (AI Agent Tự Chạy)

### Tầng 1: Tạo Ảnh Gốc 0° (Master Root)

```
Bước: text_to_image
Tỉ lệ: 9:16 (Portrait)
API: IMAGE_ASPECT_RATIO_PORTRAIT
```

**Prompt 0°** = Lấy toàn bộ thông tin từ Phần A, ghép vào template:

> MASTER CHARACTER DESIGN — {style} — 0° DIRECT FRONT VIEW
> BLANK FACELESS HEAD (NO eyes, NO nose, NO mouth).
> Character: {name}, {gender}, {age}.
> Hair: {hair}. Outfit: {outfit}.
> Colors: {primary_color}. Accents: {accent_color}. Skin: {skin}.
> Solid chroma-key green {chroma_bg}. Full body centered.

**Output**: `media_id_0` → dùng làm tham chiếu cho Tầng 2.

---

### Tầng 2: Tạo 4 Góc Còn Lại (Ref = ảnh 0°)

```
Bước: image_to_image (reference = media_id_0)
Tỉ lệ: 9:16
Chạy song song 4 request, đợi tất cả xong
```

| Góc | Yêu cầu xoay | Điểm kiểm tra |
|-----|---------------|----------------|
| 45° | Xoay sâu 45° sang trái, vai trái gần camera | Vai trái lớn hơn vai phải, ngực chéo |
| 90° | Side profile thuần trái, chỉ thấy nửa trái | Chỉ thấy 1 vai, 1 tay, profile mỏng |
| 135° | Lưng-trái, camera nhìn từ phía sau bên phải | Thấy lưng, tóc phía sau, vai trái gần |
| 180° | Sau lưng hoàn toàn, đối xứng | Toàn bộ lưng, tóc phía sau, chân xa camera |

**Output**: `media_id_45`, `media_id_90`, `media_id_135`, `media_id_180`

**Validation & Kiểm Định Ảnh** (tích hợp `image_validator.py` + AI Agent Antigravity):
1. **Basic checks (Code tự động)**: Kích thước, tỉ lệ 9:16 portrait, % phông xanh `#00FF00` ≥ 5%, không bị trắng/đen.
2. **Tự động lưu local**: Ảnh được tải về `output/_validation/angle_{angle}_attempt_{n}.png`.
3. **AI Agent Antigravity Review trực tiếp**:
   - AI Agent dùng tool `view_file` xem trực tiếp file ảnh local đã tải.
   - So sánh trực tiếp ảnh các góc (45°, 90°, 135°, 180°) với ảnh gốc 0° (khớp trang phục, màu sắc, vũ khí, mặt trơn mannequin).
   - Đảm bảo đúng góc xoay và tư thế Sprite 2D.
   - Nếu phát hiện lỗi/méo mó: AI Agent tự phân tích điểm sai, tinh chỉnh prompt và retry (tối đa 5 lần) thay vì retry mù.
4. **Chuyển tầng**: Chỉ khi AI Agent xác nhận ảnh 5 góc đạt chuẩn mới kích hoạt Tầng 3 (sinh 25 video 4s loop).

---

### Tầng 3: Tạo Video 4s Seamless Loop (5 Slots Song Song)

```
Bước: image_to_video (start_frame = end_frame = ảnh góc tương ứng)
Thời lượng: 4 giây
Sliding Window: 5 slots chạy song song liên tục
```

#### Bảng Action & Thứ Tự Ưu Tiên

| # | Action Key | Tên | Mô tả | Số prompts |
|---|-----------|------|--------|------------|
| 1 | `walk` | Đi bộ | Bước chân tại chỗ, tay vung nhẹ | 5 (×5 góc) |
| 2 | `idle` | Đứng yên | Thở nhẹ, tóc/áo lay | 5 |
| 3 | `run` | Chạy | Chạy tại chỗ nhanh, tóc bay | 5 |
| 4 | `attack` | Đánh công | Vung vũ khí theo combat_style | 5 |
| 5 | `defend` | Phòng thủ | Tư thế thủ với vũ khí | 5 |

**Tổng**: 25 video (5 actions × 5 góc)

---

## Phần C — AI Agent Phân Tích & Điều Chỉnh Prompt

> **Đây là phần quan trọng nhất.** AI agent PHẢI phân tích tính cách và kỹ năng nhân vật
> trước khi sinh prompt action. Không dùng prompt generic.

### Quy trình phân tích:

1. **Đọc Phần A** → Trích xuất `personality`, `combat_style`, `weapon`, `spell_element`

2. **Kiểm tra từng action prompt** có phù hợp với nhân vật không:

   | Nhân vật | Kiếm khách tu tiên | Võ sỹ quyền anh | Pháp sư nguyên tố |
   |----------|-------------------|-----------------|-------------------|
   | Attack | Vung kiếm phiêu diêu, ánh sáng kiếm | Đấm, cú hook, uppercut | Bắn phép, triệu hồi nguyên tố |
   | Defend | Kiếm chắn trước ngực, tư thế phòng ngự | Giơ tay đỡ, né người | Khiên phép thuật, barrier |
   | Idle | Kiếm cầm bên hông, tóc bay | Tư thế quyền anh sẵn sàng | Tay phát sáng, nguyên tố xoay |
   | Run | Chạy kiếm giữ sau lưng, áo bay | Chạy tay nắm đấm | Chạy có vệt phép phía sau |

3. **Nếu prompt mặc định không khớp** → AI agent TẠO LẠI prompt mới:
   - Giữ nguyên cấu trúc template (góc, camera, loop requirement)
   - Thay đổi mô tả hành động cho khớp `combat_style` và `weapon`
   - Thêm hiệu ứng phép thuật nếu có `spell_element`

### Ví dụ điều chỉnh:

**Nhân vật: Kiếm khách tu tiên (Lâm Tiêu)**
```
Attack 0°: Vung kiếm Lam Ngọc Kiếm theo vòng cung từ phải sang trái,
           ánh sáng kiếm xanh lạnh lóe sáng, tóc và áo bay theo đà kiếm.
           Frost aura bao quanh lưỡi kiếm.
```

**Nhân vật: Võ sỹ quyền anh (nếu thay Phần A)**
```
Attack 0°: Tung cú đấm thẳng mạnh bằng tay phải,
           vai phải xoay theo lực đấm, chân trước trụ vững.
           Không có vũ khí, chỉ dùng nắm đấm.
```

---

## Phần D — Cơ Chế Song Song & Retry

### Sliding Window = 5

```
Queue: [walk_0, walk_45, walk_90, walk_135, walk_180, idle_0, idle_45, ...]
         ↓        ↓       ↓        ↓          ↓
       Slot1    Slot2   Slot3    Slot4      Slot5

Khi Slot3 xong → nạp idle_0 vào Slot3 ngay lập tức
Khi Slot1 lỗi  → retry walk_0 tại Slot1 cho đến khi thành công
```

### Retry Logic

```
max_retries = 5 (mỗi task)
Nếu fail:
  1. Đợi 10 giây
  2. Retry cùng prompt
  3. Nếu fail 3 lần liên tiếp → AI agent sửa lại prompt rồi retry
  4. Nếu fail 5 lần → đánh dấu SKIP, log lỗi, chuyển task tiếp
```

---

## Phần E — Cách Sử Dụng Plan Này

### Tạo nhân vật mới:

1. Copy file này → đổi tên (ví dụ: `plan_warrior_zhang_wei.md`)
2. Thay toàn bộ **Phần A** bằng mô tả nhân vật mới
3. Gửi cho AI agent: _"Chạy plan tạo nhân vật theo file `plans/plan_warrior_zhang_wei.md`"_
4. AI agent sẽ:
   - Đọc Phần A → lấy thông tin nhân vật
   - Phân tích tính cách/kỹ năng (Phần C) → điều chỉnh prompt action
   - Chạy pipeline 3 tầng (Phần B) với sliding window 5 slots (Phần D)
   - Báo cáo kết quả khi hoàn tất

### Thêm action mới:

1. Thêm vào bảng Action ở Phần B (ví dụ: `jump`, `sit`, `cast_spell`)
2. Mô tả hành động cơ bản trong bảng
3. AI agent sẽ tự tạo prompt chi tiết dựa trên tính cách nhân vật

---

## Ghi Chú Kỹ Thuật

| Mục | Giá trị |
|-----|---------|
| API Image Gen | `POST /api/flow/generate-image` |
| API Video Gen | `POST /api/flow/generate-video` |
| Video Loop | `start_image_media_id` = `end_image_media_id` = ảnh góc tương ứng |
| Thời lượng video | 4 giây |
| Tỉ lệ ảnh | 9:16 (`IMAGE_ASPECT_RATIO_PORTRAIT`) |
| Phông nền | `#00FF00` (Chroma Key) |
| Prompt templates | `agent-veo3/agent/services/prompt_templates.py` |
| Pipeline code | `agent-veo3/agent/services/skill_tree_pipeline.py` |
| Flow client | `agent-veo3/agent/services/flow_client.py` |
