# Plan Mẫu: Tự Động Hóa Tạo Bộ Nhân Vật Từ Cây Kỹ Năng (Tab 4)

## Tổng Quan & Quy Chuẩn Tổ Chức

Tài liệu này là **Master Template** chuẩn. Khi người dùng yêu cầu tạo nhân vật mới dựa trên template này, AI Agent tuân thủ nghiêm ngặt các quy chuẩn sau:

1. **Quy tắc tạo File Plan riêng cho từng nhân vật**:
   - Tạo file plan mới trong thư mục `plans/` với tiền tố tên nhân vật viết thường không dấu:
     `plans/<ten-nhan-vat-khong-dau>.plan_character_pipeline.md`
     *(Ví dụ: Nhân vật `Dạ Hàn` → tạo file `plans/da-han.plan_character_pipeline.md`)*.
   - File plan riêng này lưu toàn bộ mô tả, bảng Media ID, danh sách file ảnh/video và tiến độ riêng của nhân vật đó để dễ dàng theo dõi, tùy biến hoặc tiếp tục xử lý sau này.

2. **Quy tắc Thư Mục Output riêng cho từng nhân vật**:
   - Toàn bộ ảnh gốc 5 góc, các candidates và video của nhân vật được lưu riêng vào folder:
     `agent-veo3/output/<ten-nhan-vat-khong-dau>/`
     *(Ví dụ: `agent-veo3/output/da-han/angle_0.png`, `angle_45.png`,...)*, tránh bị lẫn lộn giữa các nhân vật khác nhau.

3. **Quy trình thực thi nhanh 3 tầng**:
   - Tầng 1: Sinh ảnh gốc 0° (`media_id_0`).
   - Tầng 2: Sinh song song 8 ảnh (2 candidates $\times$ 4 góc), AI Agent so sánh chọn Best Pick.
   - Tầng 3: Sinh 25 video loop 4s song song qua 5 slots liên tục.

---

## Phần A — Mô Tả Nhân Vật Mẫu (Copy Vào File Plan Mới Khi Tạo Nhân Vật)

```yaml
# ═══ ĐIỀN THÔNG TIN NHÂN VẬT VÀO ĐÂY ═══
character:
  name: "Tên Nhân Vật"
  gender: "male / female"
  age: "young adult (18-22)"
  personality: "Mô tả tính cách và phong thái nhân vật"
  combat_style: "Phong cách chiến đấu và nguyên tố phép thuật"
  
  # Ngoại hình
  hair: "Màu sắc và kiểu tóc chi tiết"
  skin: "Tông màu da (Fair porcelain / tan / etc.)"
  outfit: "Trang phục, đạo bào, giáp trụ, màu sắc viền áo"
  primary_color: "Màu chủ đạo chính"
  accent_color: "Màu điểm xuyết phụ"
  
  # Vũ khí & Phép thuật
  weapon: "Vũ khí đặc trưng"
  spell_element: "Nguyên tố hào quang / luồng khí"
  
  # Art style
  style: "2D Xianxia/Fantasy anime chibi style, bold clean linework, flat cel-shaded coloring"
  chroma_bg: "#00FF00"
```

---

## Phần B — Pipeline 3 Tầng

### Tầng 1: Tạo Ảnh Gốc 0° (Master Root)

```
Bước: text_to_image
Tỉ lệ: 9:16 (IMAGE_ASPECT_RATIO_PORTRAIT)
```

**Prompt 0°** = Lấy thông tin từ Phần A, ghép vào template:
> MASTER CHARACTER DESIGN — {style} — 0° DIRECT FRONT VIEW
> BLANK FACELESS HEAD (NO eyes, NO nose, NO mouth).
> Character: {name}, {gender}, {age}. Hair: {hair}. Outfit: {outfit}.
> Colors: {primary_color}. Accents: {accent_color}. Skin: {skin}.
> Solid chroma-key green {chroma_bg}. Full body centered.

**Output**: `media_id_0` → tải về `output/<ten-nhan-vat>/angle_0.png`. AI Agent dùng `view_file` kiểm định đạt chuẩn mới chuyển Tầng 2.

---

### Tầng 2: Tạo 4 Góc Còn Lại — Kích Hoạt 8 Slots Song Song (2 Ảnh/Góc)

> [!IMPORTANT]
> **CƠ CHẾ MULTI-CANDIDATE (8 REQUESTS SONG SONG CÙNG LÚC):**
> - Thay vì tạo 1 ảnh mỗi góc rồi nếu lỗi mới tạo lại: **Lập tức bắn song song 8 requests** (mỗi góc 45°, 90°, 135°, 180° sinh **2 ảnh cùng lúc**: `candidate_1` và `candidate_2`).
> - Cả 8 requests đều chỉ dùng `media_id_0` làm reference và chạy đồng thời qua `asyncio.gather`.
> - Thời gian sinh cả 8 ảnh chỉ mất khoảng **30–45 giây**!
> - **Lợi ích**: AI Agent dùng `view_file` so sánh trực quan giữa 2 ứng viên của từng góc $\rightarrow$ chọn ngay ảnh có chi tiết đẹp nhất, góc xoay giải phẫu chuẩn nhất làm **Best Pick**.
> - Nếu cả 2 ảnh của 1 góc nào đó đều chưa chuẩn: AI Agent chỉ sinh tiếp 2 ảnh cho riêng góc đó để so sánh tiếp đến khi đạt 10/10.

```
Bước: image_to_image (reference = media_id_0)
Tỉ lệ: 9:16 (IMAGE_ASPECT_RATIO_PORTRAIT)
Thực thi: 8 requests chạy song song đồng thời (4 góc × 2 candidates/góc)
```

#### Tiêu Chí Đánh Giá Chọn Ứng Viên Tốt Nhất (Best Pick):

| Góc | Công thức ép góc (Override Front Bias) | Tiêu chí chọn Best Pick giữa 2 Candidates |
|-----|---------------------------------------|--------------------------------------------|
| **45°** | **Thế bước chéo bất đối xứng (`asymmetrical 3/4 stepping pose`)**<br>*(Camera isometric góc 45° bên trái)* | - Ứng viên nào có **vai trái & chân trái bước hẳn ra phía trước**, ngực xoay chéo 45° rõ rệt hơn thì chọn.<br>- Loại bỏ ảnh bị phẳng ngực hoặc dáng đứng trực diện. |
| **90°** | **Camera trực diện mạn sườn trái (`pure flank profile`)**<br>*(Nhìn 100% hướng cạnh trái 9 o'clock)* | - Ứng viên nào có **lát cắt thân mỏng nhất**, vai phải và tay phải bị che khuất 100%, mũi chân chỉ hoàn toàn sang trái thì chọn. |
| **135°** | **Lưng lệch trái 135° (Back-Left 3/4 View)** | - Ứng viên nào thấy rõ lưng áo sau, dải thắt lưng sau và độ nghiêng 3/4 sau đẹp hơn. |
| **180°** | **Sau lưng hoàn toàn 180° đối xứng** | - Ứng viên nào có đuôi tóc, lưng áo và hai gót chân đối xứng thẳng camera nhất. |

**Output**: Bộ 4 ảnh Best Pick: `media_id_45`, `media_id_90`, `media_id_135`, `media_id_180`  
Lưu vào: `agent-veo3/output/<ten-nhan-vat-khong-dau>/` (`angle_45.png`, `angle_90.png`, `angle_135.png`, `angle_180.png`).

---

### Tầng 3: Tạo Video 4s Seamless Loop (5 Slots Song Song Liên Tục)

```
Bước: image_to_video (start_frame = end_frame = ảnh góc tương ứng)
Thời lượng: 4 giây
Hàng đợi: Sliding Window 5 slots chạy song song liên tục
Output Folder: agent-veo3/output/<ten-nhan-vat-khong-dau>/videos/
```
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
