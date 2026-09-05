# Plan Mẫu: Tự Động Hóa Tạo Bộ Nhân Vật Từ Cây Kỹ Năng (Tab 4)

## Tổng Quan & Quy Chuẩn Tổ Chức

Tài liệu này là **Master Template** chuẩn cấp cao áp dụng cho toàn bộ quy trình tạo nhân vật. Khi người dùng yêu cầu tạo nhân vật mới, AI Agent tuân thủ nghiêm ngặt các quy tắc:

1. **Quy tắc tạo Project Google Flow riêng cho từng nhân vật (BẮT BUỘC)**:
   - Mỗi nhân vật **BẮT BUỘC là 1 project riêng biệt trên Google Flow** (`flow.google.com`).
   - Gọi API `POST /api/projects` với tên project là tên nhân vật (ví dụ: `name: "Bạch Vô Trần"`).
   - Đảm bảo mỗi nhân vật có một Project Card hiển thị riêng trên Dashboard của người dùng.

2. **Quy tắc tạo File Plan riêng cho từng nhân vật**:
   - Tạo file plan mới trong thư mục `plans/` với tiền tố tên nhân vật viết thường không dấu:
     `plans/<ten-nhan-vat-khong-dau>.plan_character_pipeline.md`
   - Lưu trữ đầy đủ Project ID, link trực tiếp trên Google Flow, registry 5 góc xoay và danh sách 25 video loop 4s.

3. **Quy tắc Thư Mục Output & Tải Video 1080p theo Hành Động**:
   - Thư mục gốc nhân vật: `agent-veo3/output/<ten-nhan-vat-khong-dau>/`.
   - Các thư mục con lưu trữ video `.mp4` 1080p vật lý:
     - `di-bo/`: Lưu 5 video đi bộ (`walk_0.mp4`, `walk_45.mp4`,..., `walk_180.mp4`).
     - `dung-yen/`: Lưu 5 video đứng yên (`idle_0.mp4`, `idle_45.mp4`,...).
     - `chay/`: Lưu 5 video chạy (`run_0.mp4`, `run_45.mp4`,...).
     - `danh-cong/`: Lưu 5 video tấn công (`attack_0.mp4`, `attack_45.mp4`,...).
     - `phong-thu/`: Lưu 5 video phòng thủ (`defend_0.mp4`, `defend_45.mp4`,...).

4. **Quy trình Tuyển Chọn Khắt Khe: Sinh Song Song & Đối Chiếu So Sánh Trực Quan**:
   - **Góc 0°**: Sinh **3 ảnh song song** (batch 3 candidates). AI Agent dùng `view_file` kiểm tra kỹ. Nếu có ít nhất 1 ảnh đạt 10/10 $\rightarrow$ Chọn làm mốc `media_id_0`. Nếu cả 3 ảnh đều không đạt hoặc bị lỗi $\rightarrow$ **Tiếp tục tạo batch 3 ảnh song song mới** cho đến khi có ảnh 0° đạt chuẩn thì thôi.
   - **Các góc còn lại (45°, 180°, 90°, 135°)**: Chỉ cần sinh **2 ảnh song song** (batch 2 candidates) dựa trên các ảnh mốc tương ứng đã chọn.
   - **Quy trình đối chiếu so sánh**: AI Agent nhận ảnh xong phải lập tức so sánh đối chiếu chi tiết (đai lưng, cổ áo, trâm cài, màu sắc từng lớp trang phục) với ảnh mốc 0°. Nếu candidate nào khớp $\rightarrow$ chọn làm mốc; nếu không khớp $\rightarrow$ tạo lại batch 2 ảnh mới cho góc đó.
   - **Tạo Video**: Chỉ khi đã đủ 5 ảnh mốc đạt chuẩn 10/10, lúc đó mới tạo video 4s loop tương ứng cho 5 góc.

---

## Phần A — Hồ Sơ Nhân Vật Mẫu & Thư Viện Phong Cách Đa Dạng

### 0. Cấu Trúc Khung Xương Chuẩn & Tỷ Lệ Đầu-Thân (Quy Chuẩn Từ `van-hoa-y/angle_0.png`)

> [!IMPORTANT]
> **TIÊU CHUẨN GIẢI PHẪU HỌC BẮT BUỘC CHO MỌI NHÂN VẬT (ANATOMICAL PROPORTION & SCALE LOCK):**
> Lấy trực tiếp hình tượng chuẩn mực từ ảnh mẫu [van-hoa-y/angle_0.png](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/van-hoa-y/angle_0.png) làm quy chuẩn cốt lõi:
> 1. **Tỷ Lệ Chiều Cao Cơ Thể (Head-to-Body Ratio)**:
>    - Chuẩn **4.8 đến 5.0 chiều cao đầu** (mature semi-chibi / stylized manhwa anime sprite), giữ vững vóc dáng thanh tú, đĩnh đạc và trưởng thành.
>    - Chiều cao toàn thân chiếm **85% – 88% canvas dọc** (tính từ đỉnh búi tóc/quan miện đến đế giày). Cấm bị thu nhỏ lùi xa (zoom out) làm nhân vật lọt thỏm.
> 2. **Tỷ Lệ Đầu So Với Thân & Bờ Vai (Head vs Shoulder Scale)**:
>    - Kích thước đầu vừa vặn, cân xứng tự nhiên với bờ vai. Bờ vai có khung xương rõ ràng (vai nam rộng bằng 1.0 – 1.2 lần bề ngang đầu, vai nữ bằng 0.9 – 1.0 lần đầu).
>    - Cổ thon gọn có chiều dài rõ nét kết nối tự nhiên giữa cằm và cổ áo (cấm rụt cổ, cấm đầu dính liền vào ngực).
>    - Thân người (torso) có độ dài chuẩn mực, ngực và eo phân tách rõ nét bằng đai lưng bản rộng, không bị teo tóp ngắn cụt.
>    - Đôi chân dài thẳng thanh thoát (chiếm ~50-52% tổng chiều cao cơ thể), hai đế giày/hài tiếp đất vững chãi.
> 3. **CẢNH BÁO CẤM TỶ LỆ DỊ TẬT (STRICT ANTI-BOBBLEHEAD RULE)**:
>    - **TUYỆT ĐỐI CẤM** đầu to khổng lồ dị dạng kiểu baby chibi 2 – 3 đầu (bobblehead chibi) khiến thân hình bị teo nhỏ ngắn ngủn.
>    - Mọi nhân vật (cả nam, nữ và các hệ nhân vật khác) khi tạo ra đều **BẮT BUỘC khóa chặt cấu trúc tỷ lệ này** trong prompt để đảm bảo tính đồng bộ hoàn hảo trong toàn bộ vũ trụ game/video.

### 1. Template Khai Báo YAML (Copy Vào File Plan Mới)

```yaml
character:
  name: "Tên Nhân Vật"
  gender: "male / female"
  age: "young adult (18-22) / mature / etc."
  personality: "Mô tả tính cách và phong thái (thanh lãnh / ma mị / tiêu dao / bá đạo / etc.)"
  combat_style: "Chưởng pháp / quyền pháp / kiếm khí chỉ pháp / hư không ấn (tay không)"

  # Tầng 1: Tóc & Phụ Kiện Đầu (Chọn phong cách từ Thư Viện Mục 2)
  hair: "Mô tả chi tiết kiểu tóc, lọn tóc mai buông rủ, trâm cài/quan phát/dải lụa tương ứng tính cách"

  # Khuôn mặt & Làn da (Chuẩn Sprite 2D)
  skin: "Tông màu da tự nhiên (Fair warm ivory / peach / etc.) khớp hoàn hảo đồng nhất giữa da mặt, da cổ và da tay"

  # Tầng 2 -> 6: Trang Phục Phân Tầng Chi Tiết (Layered Garments)
  outfit: "Trang phục phân tầng: Cổ áo giao lĩnh 2-3 lớp, trường bào dệt hoa văn chìm, áo khoác ngoài chuyển sắc hoặc sa mỏng, giáp vai/hộ uyển chạm khắc, hạ y xẻ tà đôi rủ thẳng, chiến ủng đế bằng"

  primary_color: "Màu chủ đạo chính (tránh màu neon chói gắt)"
  accent_color: "Màu điểm xuyết phụ thanh nhã"

  # Đai Lưng & Khóa Đai (Chọn phong cách từ Thư Viện Mục 3 - BẮT BUỘC ĐA DẠNG)
  waist_belt_logic:
    style_type: "Mô tả loại đai (đai gấm thêu / đai da nẹp kim loại / đai lụa quấn lơi / đai hộ tâm giáp)"
    front: "Chi tiết mặt trước: Khóa ngọc bài tròn / khóa đầu thú / nút thắt đồng tâm / dây ngọc bội rủ"
    back_rule:
      male: "Mặt sau lưng (135° & 180°) TUYỆT ĐỐI LÀ ĐAI PHẲNG TRƠN LIỀN MẠCH, CẤM VẼ NƠ THẮT (strictly continuous flat belt band behind back, ZERO bow, ZERO ribbon knot)."
      female: "Hoặc có nơ hồ điệp thắt buông dải lụa đồng bộ 0°, 135°, 180°, hoặc đai phẳng trơn."

  # Độ Rũ Vải & Trọng Lực (Cấm gió giả tạo)
  fabric_physics: "Quần áo, tà áo và tay áo rủ thẳng tự nhiên theo trọng lực (natural downward drape under gravity, calm static fabric). TUYỆT ĐỐI KHÔNG để gió thổi hất vạt áo bay cao ra đằng sau ở góc 45° và 90°."

  # Vũ Khí & Đạo Cụ (BẮT BUỘC KHÔNG CÓ)
  weapon: "None (TUYỆT ĐỐI KHÔNG mang vũ khí, kiếm, đàn, trượng hay đạo cụ trên người/sau lưng để tránh AI ảo giác méo góc)"
  spell_element: "Kiếm khí / hư không lực / phong lôi / hàn băng linh lực tự nhiên"

  # Art Style & Background
  style: "2D Xianxia/Fantasy anime chibi style, bold clean linework, flat cel-shaded coloring, harmonious natural palette, zero neon glow"
  chroma_bg: "#00FF00"
```

---

### 2. Thư Viện Kiểu Tóc & Phụ Kiện Đầu Đa Dạng (Không Rập Khuôn)

Tuyệt đối không lặp lại 1 kiểu tóc búi đơn điệu. Hãy chọn hoặc phối hợp theo tính cách nhân vật:

| Phong Cách Kiểu Tóc | Mô Tả Chi Tiết Cho Prompt | Tính Cách Phù Hợp |
|---|---|---|
| **Búi Nửa Đầu Tiên Khí**<br>*(Half-up Celestial)* | Tóc đen dài buộc nửa đầu, cố định bằng **Ngọc Quan** (Bạch Ngọc/Thanh Ngọc Guan Crown), cài song trâm bạc ngang búi tóc. Hai bên có **lọn tóc mai mềm mại buông rủ ôm nhẹ gò má** (`face-framing sidelocks`), đuôi tóc xõa dài mượt mà ngang hông. | Tiên môn thánh tử, kiếm tiên thanh lãnh, nho nhã thoát tục |
| **Đuôi Ngựa Cao Phong Khoáng**<br>*(High Ponytail Heroic)* | Toàn bộ mái tóc chải mượt buộc cao kiểu đuôi ngựa trên đỉnh đầu, giữ bằng khoen kim loại chạm hoa văn hoặc dải da đính đinh tán. Lọn tóc mái tỉa layer nhẹ buông tự nhiên trước trán, đuôi tóc dày dặn vắt nhẹ qua vai. | Thiếu niên kiếm hiệp, thiếu chủ tiêu sái, nhanh nhẹn hào sảng |
| **Búi Đỉnh Nghiêm Cẩn**<br>*(Traditional High Topknot)* | Tóc búi cao tròn trịa gọn gàng trên đỉnh đầu, lồng trong **Kim Quan hoặc Mộc Trâm** chạm khắc cổ kính, tóc được chải chuốt cẩn thận, không có tóc con lòa xòa, phong thái tôn nghiêm. | Chưởng môn, trưởng lão, võ sư chính đạo uy nghiêm |
| **Tóc Xõa Lãng Tử Tiêu Dao**<br>*(Flowing Rogue Locks)* | Mái tóc xõa dài bồng bềnh tự nhiên, chỉ buộc lơi một túm nhỏ phía sau bằng dải lụa mềm. Tóc mai hai bên buông dài quá cằm, vài sợi tóc lưa thưa trước trán phong trần bất cần đời. | Tán tu ma đạo, tửu kiếm tiên, lãng khách giang hồ |
| **Tết Bện Cổ Phong Đa Tầng**<br>*(Braided Intricate Warrior)* | Tóc tết các lọn nhỏ tinh xảo hai bên thái dương vòng ra sau gáy nhập vào suối tóc dài xõa, cài điểm xuyết các hạt ngọc nhỏ hoặc khuyên bạc mini, vừa hoang dã vừa tinh tế. | Chiến tướng trẻ, thiếu niên dị tộc, ma tướng du mục |
| **Tóc Nữ Song Hoàn / Linh Xà**<br>*(Female Twin Loops / Snake)* | Tóc búi đôi hình cánh bướm hoặc song hoàn hai bên đỉnh đầu, đính trâm hoa lưu ly và dây chuỗi ngọc rủ nhẹ qua tai, phần tóc sau buông dài thướt tha. | Thiếu nữ linh động, tiểu sư muội, tiên nữ hoạt bát |

---

### 3. Thư Viện Đai Lưng & Khóa Đai Đa Dạng (Không Rập Khuôn)

Mỗi nhân vật phải có thiết kế đai lưng mang cá tính riêng biệt:

| Kiểu Đai Lưng | Cấu Trúc Phân Tầng & Khóa Đai (Mặt Trước) | Quy Chuẩn Mặt Sau |
|---|---|---|
| **Đai Gấm Tam Tầng Ngọc Bội**<br>*(Tiên Môn Quý Tộc)* | Đai lưng bản rộng 3 tầng gấm dệt viền chỉ ánh kim; chính giữa đính **Đại Ngọc Bội tròn chạm lộng hoa văn rồng/phượng**, từ khóa ngọc buông rủ 2 dải dây lụa tết ngọc bội thanh thoát. | Phía sau lưng là đai phẳng trơn liền mạch, **CẤM NƠ**. |
| **Đai Da Chiến Tướng Nẹp Kim Loại**<br>*(Chiến Tướng / Ma Đạo)* | Đai da thuộc đen/nâu dày dặn ôm khít eo, viền nẹp khung kim loại hắc thiết hoặc đồng thau; khóa đai hình **Hộ Tâm Kính** hoặc mặt thao thiết/đầu rồng uy nghi, đính đinh tán nổi. | Phía sau lưng là bản đai da trơn phẳng nẹp chắc chắn, **CẤM NƠ**. |
| **Đai Dây Thừng Bện Tiêu Dao**<br>*(Lãng Khách Giang Hồ)* | Đai vải thô dệt nhiều lớp, bên ngoài quấn thêm một vòng dây thừng bện phong cách giang hồ mộc mạc, thắt nút đơn giản buông nhẹ một đoạn dây ngắn bên sườn. | Phía sau lưng ôm sát người tự nhiên, **CẤM NƠ BƯỚM**. |
| **Đai Kép Bất Đối Xứng**<br>*(Asymmetric Double Belt)* | Gồm 1 đai gấm chính bản rộng ôm eo và 1 đai da phụ bản nhỏ đeo chéo vát qua hông, đính các túi gấm phù chú nhỏ gọn gàng. | Phía sau đai chính phẳng phiu, đai chéo đi liền mạch. |
| **Đai Corset Nữ Hiệp / Nơ Hồ Điệp**<br>*(Female Elegant Sash)* | Đai lụa/gấm ôm sát eo tôn dáng, đính chuỗi hạt lưu ly hoặc trâm ngọc cài eo; phía sau lưng thắt nơ hồ điệp buông dài 2 dải lụa mềm mại rủ xuống tà váy. | Nơ sau thắt trang nhã, buông đồng bộ ở cả 0°, 135°, 180°. |

---

## Phần B — Pipeline 3 Tầng: Sinh Song Song & Đối Chiếu So Sánh Khép Kín

### Tầng 1: Tạo Ảnh Gốc 0° (Master Root Identity) — Sinh 3 Ảnh Song Song

```
Bước: text_to_image
Số lượng: BẮT BUỘC sinh 3 ẢNH SONG SONG (Candidate 1, 2, 3)
Tỉ lệ: 9:16 (IMAGE_ASPECT_RATIO_PORTRAIT)
```

#### Quy Chuẩn Giải Phẫu Góc 0°:
- **Tỷ Lệ Khung Xương (Bắt Buộc Theo Mục 0)**: Chiều cao chiếm 85-88% canvas dọc, tỷ lệ ~4.8 đến 5.0 đầu (mature semi-chibi). Đầu cân xứng với bờ vai, thân người thon thả cao ráo, chân dài tiếp đất vững chãi. **TUYỆT ĐỐI CẤM đầu to khổng lồ dị dạng (chibi bobblehead) thân hình teo tóp**.
- **Hướng Nhìn & Trục Cơ Thể**: Nhân vật đứng thẳng 100% đối xứng trực diện, ngực và mặt nhìn thẳng vào tâm camera (0.0° tuyệt đối). Hai vai ngang bằng nằm ngang, hai chân tiếp đất song song hướng 12h.
- **Khuôn Mặt & Da**: Mặt phẳng trơn nhẵn 100% không mắt mũi miệng (blank faceless), da mặt đồng nhất hoàn toàn với cổ và tay.
- **Vải & Trọng Lực**: Rủ thẳng tự nhiên xuôi theo trọng lực, không có gió thổi giả tạo.
- **Không Vũ Khí**: Hai tay buông thả lỏng tự nhiên, tay không.

#### Quy Trình Kiểm Duyệt Tuyển Chọn Góc 0°:
AI Agent dùng `view_file` phóng to kiểm tra cả 3 ảnh:
1. **Kiểm tra tỷ lệ đầu & thân**: Đầu có bị quá to hay thân bị quá bé/teo tóp không? Chiều cao có đạt 85-88% canvas không?
2. **Kiểm tra phụ kiện & tóc**: Trâm cài có bị lỗi cắm xuyên dị tật hay mọc thêm đuôi trâm thừa không? Búi tóc có cân đối không? Lọn tóc mai có tự nhiên không?
3. **Kiểm tra mặt**: Có bị AI vẽ lem mắt/miệng méo không? Màu da có đồng nhất với cổ/tay không?
4. **Kiểm tra đai lưng & phân tầng trang phục**: Khóa đai, nếp gấp cổ áo và màu sắc các tầng có chuẩn theo hồ sơ không?
5. **Quyết định**:
   - Nếu có ít nhất 1 ảnh đạt 10/10 $\rightarrow$ Chọn làm `media_id_0`, lưu file `angle_0.png`.
   - **Nếu cả 3 ảnh đều dính lỗi hoặc không có ảnh nào ưng ý $\rightarrow$ TIẾP TỤC TẠO BATCH 3 ẢNH SONG SONG MỚI**, lặp lại cho đến khi nào có ảnh đạt 10/10 thì thôi, tuyệt đối không chấp nhận ảnh lỗi làm mốc!

---

### Tầng 2: Tạo 4 Góc Còn Lại — Hệ Thống Pose Guide + Dual-Ref

> [!IMPORTANT]
> **HỆ THỐNG POSE GUIDE (Ảnh Mẫu Hướng)**
> Sử dụng **ảnh mannequin/hình nộm đơn giản** trên nền xanh cho mỗi góc xoay. Khi tạo nhân vật ở góc X°, truyền **2 references**: ảnh nhân vật (identity) + ảnh pose guide (hướng xoay).
>
> **Pose Guide Files** (lưu cố định tại `agent-veo3/output/pose-guides/`):
> - `pose_guide_45.jpg` — mannequin xoay 45° (⚠️ dùng Dual-Ref T2I thay thế)
> - `pose_guide_90.jpg` — mannequin nghiêng 90° thuần sang trái
> - `pose_guide_135.jpg` — mannequin quay lưng nghiêng 135°
>
> **Lưu ý**: Pose guide 45° (mannequin trắng đơn giản) **không hiệu quả** — model copy nguyên mannequin. Dùng **Dual-Ref T2I** thay thế cho 45°.

#### Checklist Kiểm Duyệt Bắt Buộc (Áp Dụng Cho Mọi Góc):

| # | Hạng Mục Kiểm Tra | Mô Tả Chi Tiết — Nếu Vi Phạm → LOẠI & TẠO LẠI |
|---|---|---|
| 1 | **Tay & Ngón Tay** | Hai tay có bị dị tật không? Ngón tay có bị thừa/thiếu/méo/dính liền? Bàn tay có bị biến dạng kỳ quái? |
| 2 | **Chân & Bàn Chân** | Đôi chân có bị gãy/cong/biến dạng? Giày/ủng có bị méo hay thiếu? Hai chân có cân đối tự nhiên? |
| 3 | **Hướng Xoay Đúng Góc** | Thân người (ngực, vai, hông, chân) có **THỰC SỰ** xoay đúng góc yêu cầu không? Cấm chỉ xoay đầu mà thân vẫn giữ hướng cũ! |
| 4 | **Quần Áo & Chi Tiết Trang Phục** | Các lớp áo, cổ áo, đai lưng, tà áo có khớp với ảnh 0° không? Có bị thiếu chi tiết, thừa chi tiết, hay bị AI tự thêm vật lạ? |
| 5 | **Tóc & Phụ Kiện Đầu** | Búi tóc, trâm cài, quan miện có bị lỗi (cắm xuyên, mọc thêm đuôi trâm, búi tóc biến dạng)? |
| 6 | **Tỷ Lệ Cơ Thể** | Tỷ lệ đầu-thân có chuẩn ~4.8-5.0 đầu, chiếm 85-88% canvas? Đầu có bị to quá hay thân bị bé? |

#### Chiến Thuật Tham Chiếu Theo Từng Góc:

| Góc | Phương Pháp | Refs | Ghi Chú |
|---|---|---|---|
| **0°** | `text_to_image` (T2I) | Không | Master image, tạo 3 ảnh chọn 1 |
| **45°** | **Dual-Ref T2I Fallback** | `[media_id_0, media_id_45_t2i]` | Bước 1: Thử I2I ref 0° → Bước 2: T2I 45° (không ref) → Bước 3: I2I dual-ref |
| **90°** | **Pose Guide** | `[media_id_0, pose_guide_90]` | Mannequin 90° hướng dẫn xoay — **hiệu quả cao** |
| **180°** | `image_to_image` | `[media_id_0]` | Ref duy nhất 0° — AI xoay 180° tốt tự nhiên |
| **135°** | **Pose Guide** | `[media_id_180, pose_guide_135]` | Mannequin 135° hướng dẫn xoay — **hiệu quả cao** |

#### Sơ Đồ Thực Thi:

```
Pha 2A (SONG SONG sau khi chốt 0°):
  - 45° : Dual-Ref T2I Fallback:
       1. T2I tạo ảnh 45° (không ref, prompt chi tiết) → media_id_45_t2i
       2. I2I ref: [media_id_0, media_id_45_t2i] → Sinh 2 ảnh
  - 90° : I2I ref: [media_id_0, pose_guide_90_media_id] → Sinh 2 ảnh
  - 180°: I2I ref: [media_id_0] → Sinh 2 ảnh
  → Checklist kiểm duyệt → chốt hoặc tạo lại

Pha 2B (Sau khi 180° chốt):
  - 135°: I2I ref: [media_id_180, pose_guide_135_media_id] → Sinh 2 ảnh
  → Checklist kiểm duyệt → chốt hoặc tạo lại
```



---

### Tầng 3: Tạo 25 Video Loop 4s Seamless

> [!CAUTION]
> **ĐIỀU KIỆN TIÊN QUYẾT TRƯỚC KHI TẠO VIDEO:**
> - Chỉ khi đã tuyển chọn và chốt **ĐỦ 5 ẢNH MỐC ĐẠT CHUẨN 10/10**, lúc đó mới được phép kích hoạt Tầng 3 tạo video!
> - Tuyệt đối **KHÔNG** đưa từ khóa miêu tả phụ kiện không có thật vào prompt video.
> - Giữ vững nhịp thở tự nhiên, tà áo và suối tóc khẽ lay động mềm mại theo gió thoảng (`tranquil delicate breeze`), hai tay buông tự nhiên hoặc thi triển động tác dứt khoát.

```
Bước: image_to_video (start_image = end_image = ảnh mốc tương ứng)
Thời lượng: 4 giây
Chế độ: 5 slots chạy song song (Sliding Window)
Output: 25 video 1080p lưu vào 5 thư mục con: di-bo/, dung-yen/, chay/, danh-cong/, phong-thu/
```

---

## Phần C — Bộ Khung Khống Chế Chuyển Động Video (Motion Constraints Lock)

Mỗi prompt video bắt buộc phải gắn kèm khối khống chế chống biến dạng:

```text
CRITICAL MOTION STABILITY CONSTRAINTS (STRICT ANTI-GLITCH LOCK):
1. NATURAL UNFORCED GAIT & ARM LOCK: 
   - Movement is natural, authentic, and dignified. Strictly ZERO exaggerated modeling or forced posing.
   - Hands and forearms strictly stay below chest level at all times, moving ONLY in a narrow organic pendulum arc parallel to hips.
   - STRICTLY ZERO wild arm flailing, ZERO arm waving, ZERO hand gestures, ZERO dancing, ZERO theatrical posing.
2. 45-DEGREE DIAGONAL STRIDE TRAJECTORY LOCK (FOR 45° & 135°):
   - At 45° and 135°, stride direction MUST step strictly along the true 45-degree diagonal axis aligned with body orientation.
   - STRICTLY FORBIDDEN to crab-walk sideways. Feet remain anchored to floor plane. ZERO hopping, ZERO bouncing, ZERO floating.
3. STRICT ARM & HAND IMMOBILITY LOCK FOR IDLE:
   - In standing still (idle), arms hang completely relaxed straight down along sides and DO NOT MOVE.
   - TRANQUIL ANIME BREEZE: Faint, whisper-soft anime breeze caresses character. Hair tips and flowing robe hems waft with tender micro-motion. Body, head, and feet remain completely static.
4. UNIFORM SKIN & ZERO PROPS PRESERVATION:
   - Completely blank, smooth mannequin head MUST remain intact with uniform natural skin color matching neck and hands. STRICTLY ZERO facial expressions, ZERO mouth opening.
   - STRICTLY ZERO weapons, swords, instruments, or props. STRICTLY ZERO neon glow.
```

---

## Phần D — Hướng Dẫn Vận Hành Quy Trình Cho AI Agent

Khi người dùng ra lệnh tạo nhân vật mới:
1. **Bước 1**: Đọc yêu cầu $\rightarrow$ Chọn/tạo phong cách tóc và đai lưng đa dạng từ Thư Viện Mục 2 & 3.
2. **Bước 2**: Gọi API `POST /api/projects` tạo Project riêng trên Google Flow $\rightarrow$ Lưu link dự án.
3. **Bước 3**: Tạo file plan `plans/<ten-nhan-vat>.plan_character_pipeline.md` và thư mục output vật lý.
4. **Bước 4 (Stage 1)**: Sinh batch 3 candidates cho góc 0° $\rightarrow$ Dùng `view_file` kiểm tra trâm cài, cổ áo, mặt trơn $\rightarrow$ Chọn Best Pick đạt 10/10 (hoặc retry nếu lỗi).
5. **Bước 5 (Stage 2)**:
   - Pha 2A: Sinh batch 3 candidates cho 45° (xoay theo tay phải) và 180° (đai phẳng cấm nơ) $\rightarrow$ Tuyển chọn Best Pick.
   - Pha 2B: Sinh batch 3 candidates cho 90° (vai xa che khuất) và 135° $\rightarrow$ Tuyển chọn Best Pick.
6. **Bước 6 (Stage 3)**: Sinh 25 video 4s loop qua sliding window 5 slots $\rightarrow$ Tải về các folder `di-bo/`, `dung-yen/`, `chay/`, `danh-cong/`, `phong-thu/`.
7. **Bước 7**: Báo cáo tổng kết đầy đủ với link kiểm tra từng file.
