# Plan Mẫu: Tự Động Hóa Tạo Bộ Nhân Vật Từ Cây Kỹ Năng (Tab 4)

## Tổng Quan & Quy Chuẩn Tổ Chức

Tài liệu này là **Master Template** chuẩn. Khi người dùng yêu cầu tạo nhân vật mới dựa trên template này, AI Agent tuân thủ nghiêm ngặt các quy chuẩn sau:

1. **Quy tắc tạo Project Google Flow riêng cho từng nhân vật (BẮT BUỘC)**:
   - Mỗi nhân vật **BẮT BUỘC là 1 project riêng biệt trên Google Flow** (`labs.google/fx/tools/flow`).
   - Trước khi sinh ảnh/video, AI Agent gọi API `POST /api/projects` với tên project là tên nhân vật (ví dụ: `name: "Vô Tâm Tiên Nhân"`).
   - Điều này đảm bảo mỗi nhân vật có một Project Card hiển thị riêng trên Dashboard của người dùng, không bị gom chung hay mất dấu.

2. **Quy tắc tạo File Plan riêng cho từng nhân vật**:
   - Tạo file plan mới trong thư mục `plans/` với tiền tố tên nhân vật viết thường không dấu:
     `plans/<ten-nhan-vat-khong-dau>.plan_character_pipeline.md`
     *(Ví dụ: Nhân vật `Vô Tâm Tiên Nhân` → tạo file `plans/vo-tam-tien-nhan.plan_character_pipeline.md`)*.
   - Lưu trữ đầy đủ Project ID, link truy cập trực tiếp trên Google Flow, registry 5 góc xoay và danh sách 25 video loop 4s.

3. **Quy tắc Thư Mục Output & Tải Video 1080p theo Hành Động**:
   - Thư mục gốc nhân vật: `agent-veo3/output/<ten-nhan-vat-khong-dau>/`.
   - Các thư mục con lưu trữ video `.mp4` 1080p vật lý theo từng hành động:
     - `di-bo/`: Lưu 5 video đi bộ (`walk_0.mp4`, `walk_45.mp4`,..., `walk_180.mp4`).
     - `dung-yen/`: Lưu 5 video đứng yên (`idle_0.mp4`, `idle_45.mp4`,...).
     - `chay/`: Lưu 5 video chạy (`run_0.mp4`, `run_45.mp4`,...).
     - `danh-cong/`: Lưu 5 video tấn công (`attack_0.mp4`, `attack_45.mp4`,...).
     - `phong-thu/`: Lưu 5 video phòng thủ (`defend_0.mp4`, `defend_45.mp4`,...).

4. **Quy trình thực thi nhanh 3 tầng**:
   - Tầng 1: Tạo project Google Flow → Sinh ảnh gốc 0° (`media_id_0`).
   - Tầng 2: Sinh song song 8 ảnh (2 candidates $\times$ 4 góc), AI Agent so sánh chọn Best Pick đạt 10/10.
   - Tầng 3: Sinh 25 video loop 4s song song qua 5 slots liên tục + upscale 1080p + tải về các folder con.

---

## Phần A — Mô Tả Nhân Vật Mẫu (Copy Vào File Plan Mới Khi Tạo Nhân Vật)

```yaml
# ═══ ĐIỀN THÔNG TIN NHÂN VẬT VÀO ĐÂY ═══
character:
  name: "Tên Nhân Vật"
  gender: "male / female"
  age: "young adult (18-22)"
  personality: "Mô tả tính cách và phong thái nhân vật"
  combat_style: "Chưởng pháp / quyền pháp / thủ ấn phép thuật (tay không)"
  
  # Ngoại hình
  hair: "Màu sắc và kiểu tóc chi tiết, cài trâm/dải lụa mềm mại"
  skin: "Tông màu da tự nhiên (Fair peach / warm ivory / etc.) khớp hoàn hảo đồng nhất giữa da mặt, da cổ và da tay"
  outfit: "Đạo bào, lụa là, tà áo thướt tha, giày vải đế bằng (flat cloth shoes, zero heels)"
  primary_color: "Màu chủ đạo chính (tránh màu neon chói gắt)"
  accent_color: "Màu điểm xuyết phụ thanh nhã"
  
  # Quy chuẩn đai lưng & nơ (BẮT BUỘC RÕ RÀNG)
  waist_belt_logic:
    male: "Đai lưng bản rộng ôm sát eo phẳng phiu (broad structured waist belt). Phía trước có khóa ngọc/khóa bạc. Phía sau lưng (135° & 180°) TUYỆT ĐỐI LÀ ĐAI PHẲNG TRƠN, CẤM VẼ NƠ THẮT (strictly continuous flat belt band behind back, ZERO bow, ZERO ribbon knot)."
    female: "Tùy chọn ghi rõ: Hoặc có nơ hồ điệp thắt sau lưng (buông dải lụa đồng bộ 0°, 135°, 180°), hoặc đai trơn phẳng không nơ."
  
  # Độ rũ trang phục (Trọng lực, cấm gió giả tạo)
  fabric_physics: "Quần áo, tà áo và tay áo rủ thẳng tự nhiên theo trọng lực (natural downward drape under gravity, calm static fabric). TUYỆT ĐỐI KHÔNG để gió thổi hất vạt áo bay cao ra đằng sau ở góc 45° và 90°."
  
  # Vũ khí & Đạo cụ (BẮT BUỘC KHÔNG CÓ)
  weapon: "None (TUYỆT ĐỐI KHÔNG mang vũ khí, kiếm, đàn, trượng hay đạo cụ trên người/sau lưng để tránh AI ảo giác méo góc)"
  spell_element: "Khí công / phong lôi / nguyên tố tự nhiên nhẹ nhàng"
  
  # Art style
  style: "2D Xianxia/Fantasy anime chibi style, bold clean linework, flat cel-shaded coloring, harmonious natural palette, zero neon glow"
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
> STRICT TRUE FRONTAL POSE (0.0 DEGREES): Character stands facing 100% DIRECTLY forward at camera. Both shoulders are perfectly horizontal and level. Chest and torso face 100% straight forward towards the viewer with perfect bilateral symmetry. Both feet planted parallel and pointing directly forward towards viewer (12 o'clock). Head completely upright, centered, ZERO head tilt, ZERO 3/4 turn.
> NATURAL FABRIC DRAPE UNDER GRAVITY: Robes, sleeves, and hems hang straight down naturally under calm gravity. Strictly NO wind blowing, NO billowing fabric, NO flapping hems, NO flying coat tails.
> BLANK FACELESS HEAD & UNIFORM SKIN TONE: Completely BLANK, SMOOTH, FEATURELESS face surface (NO eyes, NO nose, NO mouth). Facial skin color MUST seamlessly and uniformly match neck and hands ({skin}) with 100% consistency across all angles. Clean flat cel-shaded skin, strictly zero facial features.
> FABRIC TEXTURE (STRICTLY FLAT MATTE, ZERO SHINE): {outfit}. STRICTLY FLAT MATTE FABRIC TEXTURE, ZERO METALLIC SHINE, ZERO SILVERY GLOSS, ZERO SPECULAR REFLECTIONS, ZERO SATIN SHEEN. Clean natural cloth drape.
> WAIST BELT: {waist_belt_description}. For males: structured flat continuous belt band, strictly zero bow at back.
> ZERO WEAPONS OR PROPS: Strictly NO weapons, NO sword on back or waist, NO musical instruments, NO props. Hands empty and relaxed.
> LIGHTING & COLOR: Clean natural cel-shading. Strictly ZERO neon lighting, ZERO harsh glowing rim reflections, zero specular highlights.
> Character: {name}, {gender}, {age}. Hair: {hair}.
> Colors: {primary_color}. Accents: {accent_color}. Skin: {skin}.
> Solid chroma-key green {chroma_bg}. Full body centered.

**Output**: `media_id_0` → tải về `output/<ten-nhan-vat>/angle_0.png`. AI Agent dùng `view_file` kiểm định đạt chuẩn mới chuyển Tầng 2.

### Tầng 2: Tạo 4 Góc Còn Lại — Quy Chuẩn Tham Chiếu Hai Pha (Multi-Reference Continuity)

> [!IMPORTANT]
> **1. QUY CHUẨN THAM CHIẾU TUẦN TỰ (REFERENCE CONTINUITY):**
> - **Góc 45° (Nghiêng 3/4)**: **CHỈ THAM CHIẾU DUY NHẤT ẢNH 0° (`[media_id_0]`)**. Tuyệt đối không tham chiếu ảnh 90° hay ảnh khác để tránh bị kéo lệch tỉ lệ.
> - **Góc 180° (Sau lưng)**: **CHỈ THAM CHIẾU DUY NHẤT ẢNH 0° (`[media_id_0]`)**.
> - **Góc 90° (Mạn sườn)**: **BẮT BUỘC THAM CHIẾU CẢ ẢNH 0° VÀ ẢNH 45° (`[media_id_0, media_id_45]`)**. Phải có ảnh 45° chuẩn trước mới sinh ảnh 90° để kế thừa góc xoay và giữ trọn chi tiết.
> - **Góc 135° (Lưng lệch trái)**: **BẮT BUỘC THAM CHIẾU CẢ ẢNH 0° VÀ ẢNH 180° (`[media_id_0, media_id_180]`)**.

> [!IMPORTANT]
> **2. QUY CHUẨN KHÓA TỈ LỆ VÓC DÁNG & CHIỀU CAO (STATURE & SCALE LOCK):**
> - Nhân vật ở mọi góc xoay (45°, 90°, 135°, 180°) **PHẢI GIỮ NGUYÊN 100% VÓC DÁNG CAO RÁO, TỈ LỆ CHIỀU CAO ĐẦU - CHÂN** như ảnh 0° gốc.
> - Chiều cao từ đỉnh búi tóc đến gót ủng phải chiếm đúng **85% – 90%** chiều cao khung hình portrait.
> - **TUYỆT ĐỐI KHÔNG để góc 45° hay 90° bị co nhỏ người (shrink / zoom-out), lùn đi hay biến thành chibi mini**.
> - **BẢO TOÀN TRỌN VẸN CHI TIẾT TRANG PHỤC**: Giữ đầy đủ từng đường viền thêu chỉ vàng/bạc, hoa văn tường vân, cổ áo chéo và cấu trúc đai lưng từ ảnh 0°, không được để mất nét hay giản lược chi tiết.

```
Pha 2A:
  - 45° : image_to_image (CHỈ ref: [media_id_0]) -> Khóa tỉ lệ chiều cao cao ráo, bước chéo 45°
  - 180°: image_to_image (CHỈ ref: [media_id_0]) -> Đai sau phẳng trơn cấm nơ
  -> Chọn Best Pick: media_id_45 và media_id_180

Pha 2B:
  - 90° : image_to_image (ref: [media_id_0, media_id_45]) -> Kế thừa tỉ lệ cao và chi tiết từ 0° và 45°
  - 135°: image_to_image (ref: [media_id_0, media_id_180]) -> Nội suy 3/4 sau lưng, đai phẳng cấm nơ
  -> Chọn Best Pick: media_id_90 và media_id_135
```

#### Tiêu Chí Đánh Giá Chọn Ứng Viên Tốt Nhất (Best Pick):

| Góc | Tham Chiếu (Reference IDs) | Công thức ép góc, tỉ lệ & Tiêu chí chọn Best Pick |
|-----|----------------------------|--------------------------------------------------|
| **45°** | `[media_id_0]` (Duy nhất 0°) | **Khóa tỉ lệ vóc dáng cao 1:1 với 0°**, thế bước chéo bất đối xứng (`asymmetrical 3/4 stepping pose`): Thân ngực xoay 45° hướng 10h, giữ 100% hoa văn thêu từ 0°. |
| **180°** | `[media_id_0]` (Duy nhất 0°) | **Khóa tỉ lệ cao ráo**, sau lưng 100% đối xứng, đai lưng phẳng trơn liên tục ôm eo, **TUYỆT ĐỐI CẤM NƠ THẮT SAU LƯNG**. |
| **90°** | `[media_id_0, media_id_45]` (Cả 0° và 45°) | **Khóa tỉ lệ cao ráo**, lát cắt sườn trái 90° mỏng, vải rũ thẳng đứng theo trọng lực (cấm gió thổi vạt áo bay ra sau), giữ trọn vẹn hoa văn thêu tay áo và gấu áo. |
| **135°** | `[media_id_0, media_id_180]` (Cả 0° và 180°) | **Khóa tỉ lệ cao ráo**, lưng lệch trái 135°, đai sau phẳng trơn không nơ khớp 100% ảnh 180°. |

**Output**: Bộ 4 ảnh Best Pick: `media_id_45`, `media_id_90`, `media_id_135`, `media_id_180`  
Lưu vào: `agent-veo3/output/<ten-nhan-vat-khong-dau>/` (`angle_45.png`, `angle_90.png`, `angle_135.png`, `angle_180.png`).

---

### Tầng 3: Tạo Video 4s Seamless Loop (Bám Sát Thực Tế Nhân Vật, Cấm Bịa Phụ Kiện)

> [!CAUTION]
> **QUY TẮC CẤM BỊA ĐẶT PHỤ KIỆN TRONG PROMPT VIDEO (ZERO HALLUCINATED PROPS):**
> - Tuyệt đối **KHÔNG** đưa các từ khóa miêu tả phụ kiện không có thật trên nhân vật vào prompt video, ví dụ: `"hanging jade pendants swinging"`, `"red hair ribbons"`, `"sash tassels swaying"`, v.v.
> - Nếu nhân vật có ngọc bội gắn cố định trên đai lưng thì ngọc bội là khối tĩnh, **KHÔNG ĐƯỢC miêu tả đung đưa theo gió** (tránh AI tưởng tượng ngọc treo lủng lẳng làm hỏng kết cấu đai).
> - Chỉ cho phép chuyển động tự nhiên trên các bộ phận thực tế:
>   - **Tà áo, tay áo rộng và vạt áo**: Rung rinh nhẹ nhàng theo làn gió tự nhiên (`soft ambient breeze flutters robe sleeves and coat hems`).
>   - **Suối tóc dài sau lưng**: Chuyển động hữu cơ mềm mại (`long hair flowing with gentle secondary motion`).
>   - **Nhịp thở**: Ngực phập phồng nhẹ nhàng đều đặn (`subtle chest respiration rhythm`).
>   - **Đôi tay**: Buông tự nhiên bên hông với cử động ngón tay tối thiểu (`hands rest naturally with minimal subtle resting finger adjustments`).

```
Bước: image_to_video (start_frame = end_frame = ảnh góc tương ứng)
Thời lượng: 4 giây
Hàng đợi: Sliding Window 5 slots chạy song song liên tục
Output Folder: agent-veo3/output/<ten-nhan-vat-khong-dau>/videos/<action_key>/
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

## Phần C — AI Agent Phân Tích & Điều Chỉnh Prompt Chuyển Động

> [!IMPORTANT]
> **ĐÂY LÀ PHẦN THEN CHỐT ĐỂ DẬP TẮT LỖI AI "ẢO GIÁC" CHUYỂN ĐỘNG.**  
> Khi render video `walk` và `run`, AI diffusion thường dễ bị lỗi:
> 1. **"Tay múa loạn xạ"**: Tự ý vung tay lên cao quá đầu, múa võ, chắp tay làm phép, vẫy chào hoặc nhảy múa mất kiểm soát.
> 2. **"Nhân vật nhân nhót / Nhảy tưng tưng"**: Nhảy chồm lên không trung (`hopping/bouncing`), nhấc chân quá cao, thân mình vặn vẹo (`body twisting`), mặt trơn bị biến dạng vẽ thêm miệng méo xệch.
>
> **AI Agent BẮT BUỘC phân hóa rõ chuyển động theo độ tuổi/thể trạng** và **gắn chặt Bộ Khung Khống Chế Chuyển Động (Motion Constraints Lock)** vào mọi prompt video!

---

### 1. Bảng Phân Hóa Chuyển Động Toàn Diện Theo Độ Tuổi & Giới Tính (Universal Archetype Motion Matrix)

| Hình Tượng (Archetype) | `walk` (Đi bộ 4s Seamless Loop) | `run` (Chạy bộ 4s Seamless Loop) |
|:---|:---|:---|
| 🧑 **Nam Thanh Niên**<br>*(Young Male / Martial Artist: 16–30t)* | **Đĩnh đạc, dứt khoát, tự tin (`steady confident upright walk`)**:<br>• Lưng thẳng tắp, vai mở rộng, mắt nhìn thẳng.<br>• Bước chân sải đều đặn, nhịp nhàng dứt khoát tại chỗ.<br>• Hai tay buông tự nhiên, vung góc hẹp dọc theo hông (`narrow-amplitude pendulum swing strictly below chest level`).<br>• Trọng tâm cơ thể cân bằng tuyệt đối, không lắc hông. | **Nhanh nhẹn, thể thao (`dynamic athletic forward jog`)**:<br>• Cơ thể hơi đổ nhẹ về phía trước một góc nhỏ có kiểm soát (`slight athletic forward lean`).<br>• Hai khuỷu tay gập góc vuông ~90° đánh nhịp đều đặn sát mạn sườn (`elbows bent at 90° pumping close to ribs`).<br>• Bước chân dứt khoát, tần số nhanh, nhấc gối gọn gàng.<br>• Phong thái võ hiệp/chiến binh, không vung tay tán loạn. |
| 🌸 **Thiếu Nữ**<br>*(Young Maiden / Teen Girl: 14–20t)* | **Uyển chuyển, nhẹ nhàng, e ấp (`graceful delicate light-footed walk`)**:<br>• Dáng người thanh thoát, lưng thẳng, bước chân nhỏ nhắn khép nép (`demure petite steps`).<br>• Hai tay để hờ khép nhẹ gần eo hoặc tà váy, biên độ vung tay cực khẽ (`modest subtle arm movement close to dress`).<br>• Tà áo/váy và dải tóc bay nhẹ nhàng bồng bềnh theo nhịp bước.<br>• Tuyệt đối không vung tay mạnh bạo, không lắc lư quá đà. | **Rảo bước nhỏ thoăn thoắt (`graceful light trot / petite brisk run`)**:<br>• Bước chân ngắn, nhanh nhẹn, nhí nhảnh nhưng giữ ý tứ.<br>• Hai bàn tay co nhẹ ngang eo hoặc hơi đưa nhẹ sang hai bên giữ thăng bằng (`hands held lightly near waist`).<br>• Chân nhấc nhẹ thanh thoát, tóc và dải lụa bay lượn mềm mại.<br>• Không sải chân quá rộng hay thô bạo, không vung tay loạn xạ. |
| 💃 **Phụ Nữ Trưởng Thành**<br>*(Mature Woman / Lady: 25–45t)* | **Đoan trang, quý phái, đĩnh đạc (`elegant poised majestic stride`)**:<br>• Lưng thẳng tắp, ngực mở rộng, bước đi điềm đạm, khoan thai.<br>• Hông chuyển động nhịp nhàng tự nhiên theo giải phẫu học cơ thể (không lố lăng).<br>• Hai tay buông xuôi đoan trang hoặc đặt tay nhẹ ngang eo/vạt áo (`composed graceful hand carriage`).<br>• Tà áo dài thướt tha lay chuyển uyển chuyển theo trục cơ thể cân bằng. | **Dứt khoát, gọn gàng, khí chất (`purposeful dignified brisk run`)**:<br>• Cơ thể giữ trục ổn định, sải chân nhanh gọn gàng.<br>• Hai tay gập vừa phải đánh nhịp nhịp nhàng sát mạn sườn, không vung tay quá đà.<br>• Tà áo chuyển động uyển chuyển, giữ trọn thần thái kiêu hãnh của nữ nhân trưởng thành/nữ hiệp. |
| 🧒 **Trẻ Con / Thiếu Nhi**<br>*(Child / Kid: 5–12t)* | **Hồn nhiên, líu ríu, tinh nghịch (`playful innocent child steps`)**:<br>• Bước chân ngắn, vô tư, nhịp điệu lí lắc trẻ thơ (`cheerful pitter-patter footwork`).<br>• Hai tay vung tự nhiên một cách ngây thơ nhưng trong tầm kiểm soát sát hông.<br>• Đầu ngẩng cao hiếu kỳ, bước chân dậm dậm đáng yêu tại chỗ.<br>• Giữ vững thăng bằng, không ngã, không co giật. | **Chạy lon ton rộn ràng (`energetic scampering child sprint`)**:<br>• Chạy thoăn thoắt, bước chân dồn dập, líu ríu vui tươi.<br>• Hai tay co sát người đánh nhịp lí lắc, người hơi chúi nhẹ về phía trước.<br>• Bàn chân tiếp đất nhanh nhẹn trên mặt phẳng sàn.<br>• Hoạt bát đáng yêu, không nhảy bổ hay trượt ngã. |
| 👴👵 **Ông Lão / Bà Lão**<br>*(Elderly: 50–70+t)* | **Chậm rãi, trầm tĩnh, cẩn trọng (`slow deliberate measured walk`)**:<br>• Dáng hơi khom nhẹ phong sương (`subtle weathered stoop / slightly bent back`).<br>• Bước chân ngắn, tiếp đất đầm chắc, thận trọng từng bước.<br>• Hai tay thả lỏng buông xuôi sát thân người, hoặc cầm/chống chắc trượng gậy, vung biên độ cực nhỏ sát thắt lưng (`minimal subtle arm sway strictly close to waist / hands resting on staff`).<br>• Điệu bộ khoan thai, tĩnh tại, tuyệt đối không nhảy nhót. | **Rảo bước dồn dập của người già (`hurried shuffling jog`)**:<br>• Thân người gầy gò chúi nhẹ về trước, bước chân dồn dập khẩn trương (`short quick shuffling footsteps`).<br>• **Bàn chân luôn là là sát mặt đất**, trọng tâm hạ thấp vững vàng (`feet stay low to the floor plane, low stride height`).<br>• Hai tay co nhẹ giữ chặt trước bụng hoặc ghì chắc trượng gậy để giữ thăng bằng, vai hơi nhấp nhô nhẹ.<br>• Không nhấc chân cao, không nhảy bật lên không trung. |

---

### 2. Bộ Khung Khống Chế "Chống Ảo Giác" AI (Anti-Glitch Motion Constraints)

Để triệt tiêu hoàn toàn hiện tượng tay múa loạn xạ, nhân nhót, tóc giả giật cục, và bước chân đi ngang sai trục: mỗi prompt video **BẮT BUỘC** phải gắn khối chỉ thị Motion Lock sau:

```text
CRITICAL MOTION STABILITY CONSTRAINTS (STRICT ANTI-GLITCH LOCK):
1. NATURAL UNFORCED GAIT & ARM LOCK: 
   - Movement is natural, authentic, and dignified. Strictly ZERO exaggerated modeling, stiff artificial posturing, or forced posing.
   - Hands and forearms strictly stay below chest level at all times, moving ONLY in a narrow organic pendulum arc parallel to hips. 
   - STRICTLY ZERO wild arm flailing, ZERO arm waving, ZERO hand gestures, ZERO dancing, ZERO acrobatic/theatrical posing.
2. 45-DEGREE DIAGONAL STRIDE TRAJECTORY LOCK (CRITICAL FOR 45° & 135° ANGLES):
   - At 45° (and 135°), legs, feet, and stride direction MUST step strictly along the true 45-degree diagonal axis aligned with the body orientation vector.
   - STRICTLY FORBIDDEN to step sideways or crab-walk while the torso faces 45°. Both feet naturally track forward along the diagonal plane in anatomically correct cadence.
   - Feet remain firmly anchored to the floor plane in a clean walk/run cycle. STRICTLY ZERO hopping, ZERO bouncing up and down, ZERO airborne jumping, ZERO floating.
3. ROOTED REALISTIC HAIR & SECONDARY MOTION PHYSICS:
   - Hair is realistically rooted to the scalp with organic, soft secondary physics following body inertia and gentle breeze.
   - STRICTLY ZERO rigid wig swaying, ZERO detached floating wig effect, ZERO artificial bobbing. Long hair and ribbons flow naturally with soft fluid cloth physics.
4. IDLE BREEZE & RESPIRATION DYNAMICS (FOR IDLE CYCLE):
   - In standing still (idle), character remains calmly grounded. Visual dynamism comes ONLY from gentle ambient mountain wind fluttering robe hems, wide flowing sleeves, and natural long hair falling behind.
   - STRICTLY ZERO hallucinated accessories: ZERO swinging jade pendants (jade medallion on belt is a fixed solid buckle), ZERO fake hair ribbons, ZERO dangling sash tassels.
   - Subtle rhythmic chest breathing respiration. Hands rest naturally with minimal resting micro-adjustments.
5. TORSO, HEAD, UNIFORM SKIN & ZERO PROPS PRESERVATION:
   - Torso, shoulders, and head remain rock-steady and level. STRICTLY ZERO torso twisting, ZERO erratic head bobbing, ZERO body contortions or jittering.
   - The completely blank, smooth mannequin head MUST remain intact with uniform natural skin color seamlessly matching the neck and hands. STRICTLY ZERO facial expressions, ZERO mouth opening, ZERO facial distortion or morphing, ZERO shiny white mask contrast.
   - STRICTLY ZERO weapons, swords, instruments, or props. STRICTLY ZERO neon glow, neon reflections, or harsh glowing rim lighting.
```

---

### 3. Phân Tích Tính Cách & Phong Cách Chiến Đấu (Combat & Personality Adaptation)

AI agent phân tích `personality`, `combat_style`, `weapon`, `spell_element` để tinh chỉnh động tác chiến đấu (`attack`, `defend`):

| Nhân vật | Kiếm khách tu tiên trẻ | Lão thợ mộc / Lão nông đứng tuổi | Pháp sư / Phù thủy |
|---|---|---|---|
| **Walk** | Bước nhanh tự tin, tay hờ trên chuôi kiếm bên hông. | Bước chậm rãi trầm mặc, tay tựa cán trượng/búa, hơi khom lưng. | Bước thanh thoát, áo choàng bay nhẹ, hai tay thu trong tay áo. |
| **Run** | Chạy lướt nhẹ như ngự kiếm, kiếm ghì sau lưng. | Rảo bước dồn dập, ghì chặt cán búa/trượng trước ngực. | Rảo bước nhanh, tà áo phù thủy tung bay theo gió. |
| **Attack** | Vung kiếm phiêu diêu, kiếm khí lạnh lóe sáng. | Bổ mạnh búa/trượng mộc công xuống đất tóe bụi hoàng kim rồi thu thế. | Bắn phép nguyên tố từ xa, vòng tròn ma pháp xoay chuyển. |
| **Defend** | Dựng kiếm chắn trước ngực theo thế Thái Cực. | Dựng đứng thân trượng gỗ chắn ngang ngực vững như bàn thạch. | Dựng khiên năng lượng phép thuật phát sáng. |
| **Idle** | Đứng thẳng uy nghiêm, gió thổi tà áo và đuôi tóc bay. | Đứng chống trượng mộc, ngực phập phồng thở nhẹ sau giờ lao động. | Nổi lơ lửng hạt phép nhỏ quanh lòng bàn tay tĩnh lặng. |

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
