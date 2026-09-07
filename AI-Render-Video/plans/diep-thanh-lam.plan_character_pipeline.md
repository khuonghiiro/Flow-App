# Kế Hoạch Tạo Bộ Nhân Vật: Diệp Thanh Lam (Băng Lam Tiên Tử)

## 1. Thông Tin Dự Án Google Flow
- **Nhân vật**: Diệp Thanh Lam (Băng Lam Tiên Tử - Phong cách Manhwa)
- **Project Name**: `Diep Thanh Lam`
- **Project ID**: `7c7b4d45-2416-4902-b77a-1601a0aecff9`
- **Google Flow Project Link**: [https://flow.google.com/project/7c7b4d45-2416-4902-b77a-1601a0aecff9](https://flow.google.com/project/7c7b4d45-2416-4902-b77a-1601a0aecff9)
- **Thư mục output**: `agent-veo3/output/diep-thanh-lam/`
- **Giới tính**: Female (Nữ)

## 2. Hồ Sơ Nhân Vật (YAML Specification)

```yaml
character:
  name: "Diệp Thanh Lam (Ye Qinglan)"
  gender: "female"
  age: "young adult (18-20)"
  personality: "Thanh nhã thoát tục, ôn nhu nội liễm, băng cơ ngọc cốt, tiên khí thuần khiết"
  combat_style: "Băng lam kiếm vũ, hư không điểm chỉ, tay không linh lực"

  hair: "Tóc đen tuyền dài mượt buông qua eo, búi đôi kiểu tiên khí hồ điệp đính trâm hoa lưu ly băng lam và trâm bạc, hai lọn tóc mai mềm mại buông rủ ôm nhẹ gò má thanh tú"
  skin: "Fair warm ivory natural jade skin tone, đồng nhất hoàn toàn giữa mặt, cổ và hai bàn tay"

  outfit: "Trường bào lụa tiên môn nữ 2 tầng: tầng trong lụa trắng tuyết giao lĩnh, tầng ngoài lụa sa mỏng màu xanh ngọc băng lam viền chỉ bạc và hoa văn tinh xảo; tà váy nhiều lớp rủ thẳng tự nhiên xuôi theo trọng lực; chiến ủng/hài vải thêu hoa sen đế bằng tuyệt đối (flat cloth lotus shoes, zero heels)"

  primary_color: "Pure Snow White & Pale Icy Azure"
  accent_color: "Soft Celestial Silver & Mist Cyan"

  waist_belt_logic:
    style_type: "Đai gấm thắt eo thanh mảnh đính ngọc hoa băng lam"
    front: "Hoa cài ngọc băng lam tròn chạm lộng, dải lụa nhỏ rủ nhẹ"
    back_rule:
      female: "Rear waist delicate butterfly ribbon sash draping calmly downward without flapping (đồng bộ nơ hồ điệp rủ phẳng ở 0°, 135°, 180°)"

  fabric_physics: "natural downward drape under calm gravity, strictly calm static fabric, ZERO fake wind"
  weapon: "None (empty hands, pure martial arts aura)"
  spell_element: "Băng lam thanh khí"
  style: "2D Xianxia/Fantasy manhwa anime chibi sprite, bold clean linework, flat cel-shaded coloring, mature 4.8-5.0 heads ratio"
  chroma_bg: "#00FF00"
```

## 3. Hệ Thống Tham Chiếu Pose Guide Mannequin Nữ
- Thư mục mannequin gốc: `public/mannequins/female/`
  - 0°: `public/mannequins/female/angle_0.png`
  - 45°: `public/mannequins/female/angle_45.png`
  - 90°: `public/mannequins/female/angle_90.png`
  - 135°: `public/mannequins/female/angle_135.png`
  - 180°: `public/mannequins/female/angle_180.png`

## 4. Registry 5 Góc Xoay

| Góc | Media ID (Flow) | File Vật Lý | Trạng Thái | Ghi Chú |
|---|---|---|---|---|
| **0°** | `973a46d8-4d96-4657-aea8-ec7861080166` | [angle_0.png](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/angle_0.png) | **Hoàn Thành** | Root Identity (Candidate 0) - Chuẩn Manhwa Nữ |
| **45°** | `941eff2c-e453-4c20-a9f4-4d35ecba78f9` | [angle_45.png](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/angle_45.png) | **Hoàn Thành** | Dual-Ref Mannequin Nữ 45° (3/4 Front-Left) |
| **90°** | `ce1c46ec-2051-4049-af53-29e338b59a42` | [angle_90.png](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/angle_90.png) | **Hoàn Thành** | Dual-Ref Mannequin Nữ 90° (True Pure Side Profile) |
| **135°** | `3ffa0744-297e-4c5f-96be-2aee38ae7d8c` | [angle_135.png](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/angle_135.png) | **Hoàn Thành** | Dual-Ref Mannequin Nữ 135° (Thân xoay 8h, chân trái nghiêng 9h, nơ lệch 45°) |
| **180°** | `636a69fb-00ea-4e42-b488-0fa7e9f6d44d` | [angle_180.png](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/angle_180.png) | **Hoàn Thành** | Dual-Ref Mannequin Nữ 180° (Symmetrical Rear View) |

## 5. Registry Hoạt Ảnh Động Tác (Action Videos Seamless Loop)

### Tiến Độ Tổng Quan Đối Ứng Với `character_meta.json`:
- **5 Ảnh Mốc Cơ Bản**: 0° [OK], 45° [OK], 90° [OK], 135° [OK], 180° [OK] (100%)
- **Đứng Yên (`idle`)**: 5/5 góc [Hoàn Thành 100%]
- **Đi Bộ (`walk`)**: 5/5 góc [Hoàn Thành 100%]
- **Chạy (`run`)**: 5/5 góc [Hoàn Thành 100%]
- **Đánh Công (`attack`)**: 5/5 góc [Hoàn Thành 100%]
- **Nhóm Diễn Xuất Hoạt Hình (Tier 2 Acting)**: `wave`, `bow`, `cover_mouth_laugh`, `talking`, `nod`, `think` — [Sẵn sàng chạy tiếp đợt 4 song song]
- **Nhóm Cảm Xúc & Combat Còn Lại (Tier 3 & 4)**: `surprise`, `cheer`, `sad`, `angry`, `defend`, `hurt` — [Sẵn sàng chạy tiếp đợt 4 song song]

| Động Tác | Góc | Media ID (Flow) | Video MP4 | Thông Số | Trạng Thái |
|---|---|---|---|---|---|
| **Đứng Yên** | 0° | `a605fef8-8cfc-4ebf-8701-4ba120e66d7e` | [idle_0.mp4](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/diep-thanh-lam/dung-yen/idle_0.mp4) | 720x1280, 4.00s | **Hoàn Thành** |
| **Đứng Yên** | 45° | `f354384a-563f-4808-a06f-d61a624efd9a` | [idle_45.mp4](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/diep-thanh-lam/dung-yen/idle_45.mp4) | 720x1280, 4.00s | **Hoàn Thành** |
| **Đứng Yên** | 90° | `8819674d-10ed-4371-ae0a-74ff2fe9524e` | [idle_90.mp4](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/diep-thanh-lam/dung-yen/idle_90.mp4) | 720x1280, 4.00s | **Hoàn Thành** |
| **Đứng Yên** | 135° | `666d1700-ff30-456e-aeb6-e0bb6b4560f7` | [idle_135.mp4](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/diep-thanh-lam/dung-yen/idle_135.mp4) | 720x1280, 4.00s | **Hoàn Thành** |
| **Đứng Yên** | 180° | `dea10d52-8055-47c6-a5b9-7db305e4f2ab` | [idle_180.mp4](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/diep-thanh-lam/dung-yen/idle_180.mp4) | 720x1280, 4.00s | **Hoàn Thành** |
| **Đi Bộ** | 45° | `d6db6716-34e9-4a06-9220-2adaa01a0c6c` | [walk_45.mp4](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/diep-thanh-lam/di-bo/walk_45.mp4) | 720x1280, 4.00s | **Hoàn Thành** |
| **Đi Bộ** | 135° | `a798bd89-94ea-4f7f-a831-b6597c28838e` | [walk_135.mp4](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/diep-thanh-lam/di-bo/walk_135.mp4) | 720x1280, 4.00s | **Hoàn Thành** |
| **Chạy** | 45° | `ed5a260b-f956-4bcc-a4fa-6f7b013cb42d` | [run_45.mp4](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/diep-thanh-lam/chay/run_45.mp4) | 720x1280, 4.00s | **Hoàn Thành** |
| **Chạy** | 135° | `d4dcfeef-d95c-4323-8c9c-67a07b8302f2` | [run_135.mp4](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/diep-thanh-lam/chay/run_135.mp4) | 720x1280, 4.00s | **Hoàn Thành** |
| **Đánh Công** | 0° | `0450cd90-a8c2-420d-8ff8-ed512270751e` | [attack_0.mp4](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/diep-thanh-lam/danh-cong/attack_0.mp4) | 720x1280, 4.00s | **Hoàn Thành** |
| **Đánh Công** | 90° | `8fdb09b7-df23-41b9-bba4-ea31b014aad4` | [attack_90.mp4](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/diep-thanh-lam/danh-cong/attack_90.mp4) | 720x1280, 4.00s | **Hoàn Thành** |
| **Đánh Công** | 180° | `6fa3cf32-97c7-4359-961a-26dd8480fafd` | [attack_180.mp4](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI-Render-Video/agent-veo3/output/diep-thanh-lam/danh-cong/attack_180.mp4) | 720x1280, 4.00s | **Hoàn Thành** |
| **Đi Bộ (Gốc 4.0s)** | 0° | `1aaa6e1b-12cb-4a4a-a544-74bf62a30139` | [walk_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/di-bo/walk_0.mp4) | 720x1280, **4.00s**, 3458 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Đi Bộ (Gốc 4.0s)** | 90° | `ecd1f189-113c-45b3-bb66-d2f7ac6e3179` | [walk_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/di-bo/walk_90.mp4) | 720x1280, **4.00s**, 3085 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Chạy (Gốc 4.0s)** | 90° | `b27cc60f-b0f0-47ec-8158-5ef728277ca8` | [run_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/chay/run_90.mp4) | 720x1280, **4.00s**, 4530 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Đi Bộ (Gốc 4.0s)** | 180° | `3af7ceef-bfe9-4b00-a217-bd59ddea2130` | [walk_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/di-bo/walk_180.mp4) | 720x1280, **4.00s**, 2943 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Đánh Công (Gốc 4.0s)** | 135° | `ac3c707e-fc0c-4f53-bce3-2d58addf294c` | [attack_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/danh-cong/attack_135.mp4) | 720x1280, **4.00s**, 3968 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Chạy (Gốc 4.0s)** | 0° | `fca94866-3d76-4f13-906e-825fd921f453` | [run_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/chay/run_0.mp4) | 720x1280, **4.00s**, 5060 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Chạy (Gốc 4.0s)** | 180° | `e83fc682-eb82-4623-bc84-8bb477a81912` | [run_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/chay/run_180.mp4) | 720x1280, **4.00s**, 4346 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Đánh Công (Gốc 4.0s)** | 45° | `20ca5c92-e0f7-40c1-9290-a6a778b639ce` | [attack_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/danh-cong/attack_45.mp4) | 720x1280, **4.00s**, 2854 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Vẫy Tay Chào (Gốc 4.0s)** | 0° | `db6ac099-a085-463a-943f-48bf3152be39` | [wave_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/vay-tay/wave_0.mp4) | 720x1280, **4.00s**, 1247 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Vẫy Tay Chào (Gốc 4.0s)** | 135° | `60b66506-b881-4d52-b87a-5e984bbd0935` | [wave_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/vay-tay/wave_135.mp4) | 720x1280, **4.00s**, 1420 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Vẫy Tay Chào (Gốc 4.0s)** | 180° | `cf33c95b-aef3-46ac-860f-ac550fb39745` | [wave_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/vay-tay/wave_180.mp4) | 720x1280, **4.00s**, 1603 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Vẫy Tay Chào (Gốc 4.0s)** | 45° | `2f30cbf5-4abb-4445-b0e7-3097352f447b` | [wave_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/vay-tay/wave_45.mp4) | 720x1280, **4.00s**, 1777 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Vẫy Tay Chào (Gốc 4.0s)** | 90° | `53b266d6-0f09-4f10-8ab6-cf17215e94c3` | [wave_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/vay-tay/wave_90.mp4) | 720x1280, **4.00s**, 1147 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Hành Lễ Cúi Chào (Gốc 4.0s)** | 180° | `50ab240f-7958-458c-a341-357c0e336d0b` | [bow_180.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/hanh-le/bow_180.mp4) | 720x1280, **4.00s**, 2390 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Hành Lễ Cúi Chào (Gốc 4.0s)** | 90° | `c76b28f2-5da3-4e18-869e-3cd441ab4f3f` | [bow_90.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/hanh-le/bow_90.mp4) | 720x1280, **4.00s**, 2208 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Hành Lễ Cúi Chào (Gốc 4.0s)** | 45° | `c17751ca-bd76-4dcf-8efc-37989e4c7000` | [bow_45.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/hanh-le/bow_45.mp4) | 720x1280, **4.00s**, 2216 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Hành Lễ Cúi Chào (Gốc 4.0s)** | 135° | `141da072-6e22-4eb8-ba94-ab833d55e1ee` | [bow_135.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/hanh-le/bow_135.mp4) | 720x1280, **4.00s**, 2317 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |
| **Hành Lễ Cúi Chào (Gốc 4.0s)** | 0° | `e5ff8dd5-b425-481d-b19d-a0b5a3a7a4ef` | [bow_0.mp4](file:///e:/UngDung_PC/Flow-App/AI-Render-Video/agent-veo3/output/diep-thanh-lam/hanh-le/bow_0.mp4) | 720x1280, **4.00s**, 2141 KB | **Hoàn Thành** (Seamless Loop 4s i2v_fl) |

### 6. Chiến Thuật Gửi Song Song Đa Luồng (Parallel Multi-Request Execution)

Áp dụng cơ chế gửi hàng loạt request song song (`concurrency: 5`) để Google Cloud GPU render đồng thời, không chờ tuần tự từng video:

#### Phân Bổ Các Đợt Chạy Song Song Cụ Thể:
* **Đợt 1 (Bổ sung Đi Bộ song song)**: Gửi đồng loạt 3 góc thiếu `walk_0`, `walk_90`, `walk_180`:
  ```bash
  python agent-veo3/scripts/generate_action_loop.py --character diep-thanh-lam --action walk --angle 0,90,180 --concurrency 3
  ```
* **Đợt 2 (Bổ sung Chạy song song)**: Gửi đồng loạt 3 góc thiếu `run_0`, `run_90`, `run_180`:
  ```bash
  python agent-veo3/scripts/generate_action_loop.py --character diep-thanh-lam --action run --angle 0,90,180 --concurrency 3
  ```
* **Đợt 3 (Bổ sung Tấn Công song song)**: Gửi đồng loạt 2 góc thiếu `attack_45`, `attack_135`:
  ```bash
  python agent-veo3/scripts/generate_action_loop.py --character diep-thanh-lam --action attack --angle 45,135 --concurrency 2
  ```
* **Đợt 4 (Tự động chạy bù toàn bộ Tier 2, 3, 4 song song 5 luồng)**:
  ```bash
  # Tự động gửi song song theo lô 5 tác vụ cùng lúc theo đúng thứ tự ưu tiên
  python agent-veo3/scripts/generate_action_loop.py --character diep-thanh-lam --resume --concurrency 5
  ```



