# Kế Hoạch Tạo Bộ Nhân Vật: Hàn Lăng Phong (Phong Lôi Kiếm Tử)

## 1. Thông Tin Dự Án Google Flow
- **Nhân vật**: Hàn Lăng Phong (Phong Lôi Kiếm Tử / Manhwa Style)
- **Project Name**: `Hàn Lăng Phong`
- **Project ID**: `09c32739-99ca-4d85-a250-558faf36d638`
- **Google Flow Project Link**: [https://flow.google.com/project/09c32739-99ca-4d85-a250-558faf36d638](https://flow.google.com/project/09c32739-99ca-4d85-a250-558faf36d638)
- **Thư mục output**: `agent-veo3/output/han-lang-phong/`
- **Giới tính**: Male (Nam)

## 2. Hồ Sơ Nhân Vật (YAML Specification)

```yaml
character:
  name: "Hàn Lăng Phong (Han Lingfeng)"
  gender: "male"
  age: "young adult (20-22)"
  personality: "Lãnh tĩnh kiên nghị, tiêu sái phong khoáng, kiếm ý lăng lệ"
  combat_style: "Phong lôi quyền pháp, kiếm khí chỉ pháp, tay không linh lực"

  hair: "Tóc đen tuyền dày mượt buộc đuôi ngựa cao (High Ponytail Heroic) với khoen bạc tinh tế, lọn tóc mái tỉa layer nhẹ buông tự nhiên trước trán, hai lọn tóc mai buông rủ ôm nhẹ gò má, đuôi tóc dày dặn buông dài ngang lưng"
  skin: "Fair warm ivory natural healthy skin tone, đồng nhất hoàn toàn giữa mặt, cổ và hai bàn tay"

  outfit: "Trường bào tiên hiệp huyền lôi 2 tầng: tầng trong lụa trắng sương mai giao lĩnh, tầng ngoài lụa sa màu xanh chàm thẫm (deep midnight indigo & slate teal) viền chỉ bạc lôi vân tinh tế; tà áo xẻ đôi rủ thẳng tự nhiên xuôi theo trọng lực; chiến ủng vải đế bằng đế thấp tuyệt đối (strictly flat cloth martial boots, zero heels)"

  primary_color: "Deep Midnight Indigo & Frost White"
  accent_color: "Slate Teal & Celestial Silver"

  waist_belt_logic:
    style_type: "Đai da thuộc nẹp viền bạc chạm lôi vân"
    front: "Khóa ngọc bài chữ nhật viền bạc chạm phù điêu phong lôi, dải ngọc bội nhỏ tết lụa rủ thanh nhã bên sườn"
    back_rule:
      male: "strictly continuous flat belt band behind back, ZERO bow, ZERO ribbon knot"

  fabric_physics: "natural downward drape under calm gravity, strictly calm static fabric, ZERO fake wind"
  weapon: "None (empty hands, pure martial arts aura, strictly zero weapons, zero props)"
  spell_element: "Phong lôi thanh kiếm khí"
  style: "2D Xianxia/Fantasy manhwa anime chibi sprite, bold clean linework, flat cel-shaded coloring, mature 4.8-5.0 heads ratio"
  chroma_bg: "#00FF00"
```

## 3. Hệ Thống Tham Chiếu Pose Guide Mannequin Nam
- Thư mục mannequin gốc: `public/mannequins/male/`
  - 0°: `public/mannequins/male/angle_0.png`
  - 45°: `public/mannequins/male/angle_45.png`
  - 90°: `public/mannequins/male/angle_90.png`
  - 135°: `public/mannequins/male/angle_135.png`
  - 180°: `public/mannequins/male/angle_180.png`

## 4. Registry 5 Góc Xoay

| Góc | Media ID (Flow) | File Vật Lý | Trạng Thái | Ghi Chú |
|---|---|---|---|---|
| **0°** | `8bcd85e1-4c37-4f58-a92d-0546aabc19e4` | `angle_0.png` | **Hoàn Thành** | Master Root Identity (Candidate 1) |
| **45°** | `e44acb30-e69a-4a25-83e3-76950582f18f` | `angle_45.png` | **Hoàn Thành** | Dual-Ref Mannequin (3/4 Front-Left) |
| **90°** | `921cbaf2-26eb-482e-9a22-0418dafd51e1` | `angle_90.png` | **Hoàn Thành** | Dual-Ref Mannequin (Pure Left Profile) |
| **135°** | `8d968c8d-503e-448a-91dc-54e9977bea67` | `angle_135.png` | **Hoàn Thành** | Dual-Ref Mannequin (3/4 Back-Left, Tỷ lệ chuẩn 5 đầu) |
| **180°** | `5ff23040-e0ff-4ab9-815b-b8b4a8a53f23` | `angle_180.png` | **Hoàn Thành** | Dual-Ref Mannequin (Symmetrical Back, Tỷ lệ chuẩn 5 đầu, Đai phẳng) |

## 5. Registry Hoạt Ảnh Động Tác (Walk/Run: 4s Seamless Loop | Các Động Tác Khác: 8s Single-Frame i2v - Model Veo 3.1 Lite Priority 0-Token)

| Động Tác | Góc | Media ID (Flow) | Video MP4 | Thông Số | Trạng Thái |
|---|---|---|---|---|---|
| **Đứng Yên (Gốc 8.0s)** | 0° | `17cbb4a7-e366-4206-b72a-4ce87715969e` | [idle_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/dung-yen/idle_0.mp4) | 720x1280, **8.00s**, 1649 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Đứng Yên (Gốc 8.0s)** | 45° | `a680a098-9163-4970-ac1d-499eb1c58887` | [idle_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/dung-yen/idle_45.mp4) | 720x1280, **8.00s**, 1788 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Đứng Yên (Gốc 8.0s)** | 90° | `c10fc4d7-40a3-495d-a8ba-5dc04e3b3830` | [idle_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/dung-yen/idle_90.mp4) | 720x1280, **8.00s**, 1534 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Đứng Yên (Gốc 8.0s)** | 135° | `02f85ec8-0bba-48ac-8ac9-842b7dbe452b` | [idle_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/dung-yen/idle_135.mp4) | 720x1280, **8.00s**, 1909 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Đứng Yên (Gốc 8.0s)** | 180° | `eba4f566-6373-45a9-b5a7-0dd30401d477` | [idle_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/dung-yen/idle_180.mp4) | 720x1280, **8.00s**, 1618 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Đi Bộ (Gốc 4.0s Loop)** | 0° | `1060776d-b33d-460b-991e-7b32934d54cc` | [walk_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/di-bo/walk_0.mp4) | 720x1280, **4.00s**, 1801 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Đi Bộ (Gốc 4.0s Loop)** | 45° | `cf0591de-4520-4867-b948-fa7e888b7258` | [walk_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/di-bo/walk_45.mp4) | 720x1280, **4.00s**, 1480 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Đi Bộ (Gốc 4.0s Loop)** | 90° | `d8ab32e9-47a5-4ea8-b229-5e0fa1dfb423` | [walk_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/di-bo/walk_90.mp4) | 720x1280, **4.00s**, 1364 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Đi Bộ (Gốc 4.0s Loop)** | 135° | `9f702f77-31e0-43ef-a85f-1b6fcb150374` | [walk_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/di-bo/walk_135.mp4) | 720x1280, **4.00s**, 1654 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Đi Bộ (Gốc 4.0s Loop)** | 180° | `ebef48c5-99d6-4f34-805b-da5aeb1f194b` | [walk_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/di-bo/walk_180.mp4) | 720x1280, **4.00s**, 1439 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Chạy (Gốc 4.0s Loop)** | 45° | `e1f2ec75-b297-414f-940c-f5d720f6821f` | [run_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/chay/run_45.mp4) | 720x1280, **4.00s**, 2277 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Chạy (Gốc 4.0s Loop)** | 0° | `513f798c-66e7-41a8-bb77-94c19a886609` | [run_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/chay/run_0.mp4) | 720x1280, **4.00s**, 2173 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Chạy (Gốc 4.0s Loop)** | 90° | `86b4df5c-ed12-45c9-bc6e-1699384d715a` | [run_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/chay/run_90.mp4) | 720x1280, **4.00s**, 2165 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Chạy (Gốc 4.0s Loop)** | 135° | `089de5e9-bf75-41ae-95c2-a1fede278b5e` | [run_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/chay/run_135.mp4) | 720x1280, **4.00s**, 2222 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Chạy (Gốc 4.0s Loop)** | 180° | `0224b590-a08b-43a8-9b57-89f2664a9356` | [run_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/chay/run_180.mp4) | 720x1280, **4.00s**, 1744 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Vẫy Tay Chào (Gốc 8.0s)** | 0° | `3072f415-01c8-4a97-9ab7-d4909427d427` | [wave_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/vay-tay/wave_0.mp4) | 720x1280, **8.00s**, 1429 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Vẫy Tay Chào (Gốc 8.0s)** | 45° | `5e1ef0ca-7e29-4d1a-b9f1-ea2c8d1c1685` | [wave_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/vay-tay/wave_45.mp4) | 720x1280, **8.00s**, 1526 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Vẫy Tay Chào (Gốc 8.0s)** | 90° | `b9528d73-c0c4-4f25-be94-10330e6685e7` | [wave_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/vay-tay/wave_90.mp4) | 720x1280, **8.00s**, 1650 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Vẫy Tay Chào (Gốc 8.0s)** | 135° | `cd68acde-6d6e-4b28-b399-e8415aff7e44` | [wave_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/vay-tay/wave_135.mp4) | 720x1280, **8.00s**, 1977 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Vẫy Tay Chào (Gốc 8.0s)** | 180° | `7d846d61-1577-440c-befe-3b17c3a43820` | [wave_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/vay-tay/wave_180.mp4) | 720x1280, **8.00s**, 1490 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Hành Lễ Cúi Chào (Gốc 4.0s)** | 0° | `e5abf223-d5b5-43ba-9edb-9cccbcdd917d` | [bow_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/hanh-le/bow_0.mp4) | 720x1280, **4.00s**, 1036 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Hành Lễ Cúi Chào (Gốc 8.0s)** | 45° | `8e228095-b137-4c1b-b269-e841313b4371` | [bow_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/hanh-le/bow_45.mp4) | 720x1280, **8.00s**, 2094 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Hành Lễ Cúi Chào (Gốc 8.0s)** | 90° | `c05cfcfb-499a-4946-bd69-51876e692f51` | [bow_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/hanh-le/bow_90.mp4) | 720x1280, **8.00s**, 1797 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Hành Lễ Cúi Chào (Gốc 8.0s)** | 135° | `a0f199b5-6546-435a-96db-844fcbf7bb00` | [bow_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/hanh-le/bow_135.mp4) | 720x1280, **8.00s**, 2094 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Hành Lễ Cúi Chào (Gốc 8.0s)** | 180° | `118046ff-cf64-4b00-904b-54c7e7f44ca5` | [bow_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/hanh-le/bow_180.mp4) | 720x1280, **8.00s**, 1792 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Che Miệng Cười (Gốc 8.0s)** | 0° | `2ec9c29a-687e-4ef9-849a-03e6447edbd0` | [cover_mouth_laugh_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/che-mieng-cuoi/cover_mouth_laugh_0.mp4) | 720x1280, **8.00s**, 1574 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Che Miệng Cười (Gốc 8.0s)** | 90° | `04876da8-f309-4883-9159-1da344476954` | [cover_mouth_laugh_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/che-mieng-cuoi/cover_mouth_laugh_90.mp4) | 720x1280, **8.00s**, 1369 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Che Miệng Cười (Gốc 8.0s)** | 45° | `0c6b8065-61bc-49c5-b1d8-33b22d8f2159` | [cover_mouth_laugh_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/che-mieng-cuoi/cover_mouth_laugh_45.mp4) | 720x1280, **8.00s**, 1689 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Che Miệng Cười (Gốc 4.0s)** | 180° | `eafdc683-56bc-4da2-989a-084d1fb8168d` | [cover_mouth_laugh_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/che-mieng-cuoi/cover_mouth_laugh_180.mp4) | 720x1280, **4.00s**, 1183 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Che Miệng Cười (Gốc 4.0s)** | 135° | `5fb2ebcc-af5e-49ee-9f3c-ea0217b91f16` | [cover_mouth_laugh_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/che-mieng-cuoi/cover_mouth_laugh_135.mp4) | 720x1280, **4.00s**, 991 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Nói Chuyện (Gốc 8.0s)** | 0° | `6ed59973-7731-4d48-a0c0-58ba925e6172` | [talking_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/noi-chuyen/talking_0.mp4) | 720x1280, **8.00s**, 1685 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Nói Chuyện (Gốc 4.0s)** | 90° | `a6b03b1d-7836-459a-bdca-eb11769731c1` | [talking_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/noi-chuyen/talking_90.mp4) | 720x1280, **4.00s**, 808 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Nói Chuyện (Gốc 4.0s)** | 180° | `f3a7f4d3-3da9-41ba-8ec4-99cbf18e3336` | [talking_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/noi-chuyen/talking_180.mp4) | 720x1280, **4.00s**, 895 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Gật Đầu (Gốc 8.0s)** | 0° | `9a27af8c-e32d-4905-97a0-2e07cd2385f9` | [nod_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/gat-dau/nod_0.mp4) | 720x1280, **8.00s**, 1999 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Nói Chuyện (Gốc 8.0s)** | 135° | `07db28e6-87a0-406c-9797-ceb9c40d876e` | [talking_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/noi-chuyen/talking_135.mp4) | 720x1280, **8.00s**, 2033 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Gật Đầu (Gốc 4.0s)** | 45° | `f475b125-b247-4229-80cc-f66bd1860b61` | [nod_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/gat-dau/nod_45.mp4) | 720x1280, **4.00s**, 989 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Gật Đầu (Gốc 4.0s)** | 90° | `d0978244-b69b-4d39-bd68-6745ec3c1c65` | [nod_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/gat-dau/nod_90.mp4) | 720x1280, **4.00s**, 1115 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Gật Đầu (Gốc 4.0s)** | 135° | `48aa7367-5c09-4047-8f45-84d0a08903bc` | [nod_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/gat-dau/nod_135.mp4) | 720x1280, **4.00s**, 934 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Gật Đầu (Gốc 4.0s)** | 180° | `169c2eb4-7cd7-474c-a308-c3bd20f5b442` | [nod_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/gat-dau/nod_180.mp4) | 720x1280, **4.00s**, 1000 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Suy Nghĩ (Gốc 4.0s)** | 0° | `14708413-d7e7-4b00-b332-e0ffb2fba6b8` | [think_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/suy-nghi/think_0.mp4) | 720x1280, **4.00s**, 848 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Suy Nghĩ (Gốc 4.0s)** | 45° | `ba750cde-cad9-4310-b70d-dcc0af5b26fd` | [think_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/suy-nghi/think_45.mp4) | 720x1280, **4.00s**, 874 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Suy Nghĩ (Gốc 4.0s)** | 90° | `031d211e-d8ef-4efd-81ad-ae7d29e91e6a` | [think_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/suy-nghi/think_90.mp4) | 720x1280, **4.00s**, 737 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Suy Nghĩ (Gốc 4.0s)** | 135° | `ce0e73ec-7da2-414d-94b1-05731f7263c8` | [think_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/suy-nghi/think_135.mp4) | 720x1280, **4.00s**, 936 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Suy Nghĩ (Gốc 4.0s)** | 180° | `3b4ad7de-1384-4913-a74f-c9f2f91677bd` | [think_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/suy-nghi/think_180.mp4) | 720x1280, **4.00s**, 855 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Kinh Ngạc (Gốc 4.0s)** | 0° | `d2cdbb8c-8b2a-416e-869e-24bc9279dd90` | [surprise_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/kinh-ngac/surprise_0.mp4) | 720x1280, **4.00s**, 1129 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Kinh Ngạc (Gốc 4.0s)** | 45° | `ed2d2fa2-97cd-498b-9e93-1968fca05238` | [surprise_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/kinh-ngac/surprise_45.mp4) | 720x1280, **4.00s**, 1268 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Kinh Ngạc (Gốc 4.0s)** | 135° | `5adb772b-4358-44b1-8b13-a6fe073e1498` | [surprise_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/kinh-ngac/surprise_135.mp4) | 720x1280, **4.00s**, 1239 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Kinh Ngạc (Gốc 4.0s)** | 180° | `e70b6c72-23ee-473c-8546-04cc04fef3df` | [surprise_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/kinh-ngac/surprise_180.mp4) | 720x1280, **4.00s**, 1005 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Kinh Ngạc (Gốc 4.0s)** | 90° | `b6267247-9900-4470-a289-e63d438c1af7` | [surprise_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/kinh-ngac/surprise_90.mp4) | 720x1280, **4.00s**, 1106 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Reo Hò Ăn Mừng (Gốc 4.0s)** | 0° | `096f5b49-9d89-4a17-b09f-806a83daf086` | [cheer_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/reo-ho/cheer_0.mp4) | 720x1280, **4.00s**, 1264 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Reo Hò Ăn Mừng (Gốc 8.0s)** | 45° | `0e1d29ee-5a0a-467f-8b4b-15fa1972b33b` | [cheer_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/reo-ho/cheer_45.mp4) | 720x1280, **8.00s**, 1654 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Reo Hò Ăn Mừng (Gốc 4.0s)** | 90° | `d73f903c-f303-49be-9982-f003af3b03a3` | [cheer_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/reo-ho/cheer_90.mp4) | 720x1280, **4.00s**, 955 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Reo Hò Ăn Mừng (Gốc 8.0s)** | 135° | `c87d7e12-bad4-4c36-8f18-82a969cb5f62` | [cheer_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/reo-ho/cheer_135.mp4) | 720x1280, **8.00s**, 2133 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Buồn Bã Thở Dài (Gốc 8.0s)** | 0° | `6c2b31c0-6dc3-44ca-867f-ce74a61a68fa` | [sad_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/buon-ba/sad_0.mp4) | 720x1280, **8.00s**, 1801 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Reo Hò Ăn Mừng (Gốc 4.0s)** | 180° | `5c63fa99-88f7-451e-b87c-fb5d0e37fa0c` | [cheer_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/reo-ho/cheer_180.mp4) | 720x1280, **4.00s**, 992 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Buồn Bã Thở Dài (Gốc 8.0s)** | 90° | `a9a0dcac-053a-49b6-835c-c814004f7d90` | [sad_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/buon-ba/sad_90.mp4) | 720x1280, **8.00s**, 1573 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Tức Giận Dỗi (Gốc 8.0s)** | 0° | `393b81d0-71ab-4afd-9648-bee1cb9ca739` | [angry_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/tuc-gian/angry_0.mp4) | 720x1280, **8.00s**, 2201 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Tức Giận Dỗi (Gốc 8.0s)** | 45° | `7c55dbe4-7455-4a36-b4d7-51940949c3b9` | [angry_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/tuc-gian/angry_45.mp4) | 720x1280, **8.00s**, 2007 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Nói Chuyện (Gốc 4.0s)** | 45° | `3d17e304-887a-405c-8022-2f4b72430295` | [talking_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/noi-chuyen/talking_45.mp4) | 720x1280, **4.00s**, 900 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Tức Giận Dỗi (Gốc 4.0s)** | 135° | `51465658-8d2c-4860-901f-6ee20fa48e1e` | [angry_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/tuc-gian/angry_135.mp4) | 720x1280, **4.00s**, 1079 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Tức Giận Dỗi (Gốc 4.0s)** | 90° | `ea3470d1-5300-4db9-af17-6610807e5077` | [angry_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/tuc-gian/angry_90.mp4) | 720x1280, **4.00s**, 921 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Tức Giận Dỗi (Gốc 4.0s)** | 180° | `dc05fdef-f066-4c93-b0d8-d2eea44f3e2b` | [angry_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/tuc-gian/angry_180.mp4) | 720x1280, **4.00s**, 1159 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Đánh Công (Gốc 8.0s)** | 0° | `d0be1d0c-c65a-4b16-980a-e09c19962c7a` | [attack_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/danh-cong/attack_0.mp4) | 720x1280, **8.00s**, 3460 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Đánh Công (Gốc 4.0s)** | 45° | `67e9fe8e-a586-4d5c-8f19-58d152bec454` | [attack_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/danh-cong/attack_45.mp4) | 720x1280, **4.00s**, 1574 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Đánh Công (Gốc 8.0s)** | 90° | `d48cd680-ecd6-46cc-8901-e05a1a5daa8d` | [attack_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/danh-cong/attack_90.mp4) | 720x1280, **8.00s**, 2481 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Đánh Công (Gốc 4.0s)** | 135° | `20398b83-63a2-46d8-93a9-36f3ada78ff7` | [attack_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/danh-cong/attack_135.mp4) | 720x1280, **4.00s**, 1755 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Đánh Công (Gốc 8.0s)** | 180° | `4b4a1c84-132b-44f8-9ae6-ffe42b2e6585` | [attack_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/danh-cong/attack_180.mp4) | 720x1280, **8.00s**, 2643 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Phòng Thủ (Gốc 8.0s)** | 45° | `dfc97014-c30a-42b2-820b-52455f123eaf` | [defend_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/phong-thu/defend_45.mp4) | 720x1280, **8.00s**, 1759 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Phòng Thủ (Gốc 4.0s)** | 0° | `4a9bb42e-6317-497e-82ee-dbf1b3d0dc71` | [defend_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/phong-thu/defend_0.mp4) | 720x1280, **4.00s**, 1312 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Buồn Bã Thở Dài (Gốc 4.0s)** | 45° | `00051574-536a-4741-90a6-a9b3106bf9ea` | [sad_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/buon-ba/sad_45.mp4) | 720x1280, **4.00s**, 1193 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Phòng Thủ (Gốc 4.0s)** | 90° | `2ec1e36b-e907-42bc-8139-8c58283c51e8` | [defend_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/phong-thu/defend_90.mp4) | 720x1280, **4.00s**, 1037 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Phòng Thủ (Gốc 8.0s)** | 135° | `1e773c77-b6bd-416f-ac0a-0932d2c114dd` | [defend_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/phong-thu/defend_135.mp4) | 720x1280, **8.00s**, 1926 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Buồn Bã Thở Dài (Gốc 8.0s)** | 135° | `2f7a1eaf-2328-4668-9ba4-f7a7373a4608` | [sad_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/buon-ba/sad_135.mp4) | 720x1280, **8.00s**, 1776 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Buồn Bã Thở Dài (Gốc 4.0s)** | 180° | `865c747a-8855-4d39-a9bb-6c8a7ddd50ef` | [sad_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/buon-ba/sad_180.mp4) | 720x1280, **4.00s**, 935 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Phòng Thủ (Gốc 4.0s)** | 180° | `b865c2fc-2d8f-4baa-971b-88a3cb998d05` | [defend_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/phong-thu/defend_180.mp4) | 720x1280, **4.00s**, 1198 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Trúng Đòn (Gốc 8.0s)** | 0° | `0aad890b-8518-4b90-b199-8597c5b84f7c` | [hurt_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/trung-don/hurt_0.mp4) | 720x1280, **8.00s**, 2040 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Trúng Đòn (Gốc 8.0s)** | 45° | `259250e7-e8e3-435f-b763-8740568bd141` | [hurt_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/trung-don/hurt_45.mp4) | 720x1280, **8.00s**, 2692 KB | **Hoàn Thành** (Single Frame 8s i2v) |
| **Trúng Đòn (Gốc 4.0s)** | 90° | `81262e24-d640-4f08-a9c8-8b04bfa1fb61` | [hurt_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/trung-don/hurt_90.mp4) | 720x1280, **4.00s**, 1214 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Trúng Đòn (Gốc 4.0s)** | 135° | `138c4153-332b-4f87-a181-f0a5e2d93c3e` | [hurt_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/trung-don/hurt_135.mp4) | 720x1280, **4.00s**, 1433 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Trúng Đòn (Gốc 4.0s)** | 180° | `0555d377-4090-4354-aa9d-2f17eb2c0510` | [hurt_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/han-lang-phong/trung-don/hurt_180.mp4) | 720x1280, **4.00s**, 1409 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |

