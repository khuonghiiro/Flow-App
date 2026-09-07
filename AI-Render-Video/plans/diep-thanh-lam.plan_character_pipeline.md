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
- **Đi Bộ (`walk`)**: 2/5 góc [45°, 135° OK] — Còn thiếu: 0°, 90°, 180°
- **Chạy (`run`)**: 2/5 góc [45°, 135° OK] — Còn thiếu: 0°, 90°, 180°
- **Đánh Công (`attack`)**: 3/5 góc [0°, 90°, 180° OK] — Còn thiếu: 45°, 135°
- **Nhóm Diễn Xuất Hoạt Hình (Tier 2 Acting)**: `wave`, `bow`, `cover_mouth_laugh`, `talking`, `nod`, `think` — [Đang chờ tạo bù theo thứ tự ưu tiên]
- **Nhóm Cảm Xúc & Combat Còn Lại (Tier 3 & 4)**: `surprise`, `cheer`, `sad`, `angry`, `defend`, `hurt` — [Đang chờ tạo bù]

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

### Lệnh Tạo Tiếp Bù Các Hoạt Ảnh Thiếu (Auto-Resume):
```bash
# Quét tiến độ
python agent-veo3/scripts/generate_action_loop.py --character diep-thanh-lam --status

# Tạo bù tự động theo đúng thứ tự ưu tiên
python agent-veo3/scripts/generate_action_loop.py --character diep-thanh-lam --resume
```



