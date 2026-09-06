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
| **0°** | *Đang sinh...* | `angle_0.png` | **Đang tiến hành** | Batch 3 candidates |
| **45°** | *Chờ 0°* | `angle_45.png` | **Chờ** | Dual-Ref Mannequin Nữ 45° |
| **90°** | *Chờ 0°* | `angle_90.png` | **Chờ** | Dual-Ref Mannequin Nữ 90° |
| **135°** | *Chờ 180°* | `angle_135.png` | **Chờ** | Dual-Ref Mannequin Nữ 135° (Pose Guide First) |
| **180°** | *Chờ 0°* | `angle_180.png` | **Chờ** | Dual-Ref Mannequin Nữ 180° |

## 5. Registry Hoạt Ảnh Động Tác (Action Videos 4s Seamless Loop)

| Động Tác | Góc | Media ID (Flow) | Video MP4 | Thông Số | Trạng Thái |
|---|---|---|---|---|---|
| **Đứng Yên (Idle Breathing)** | 0° | *Chờ ảnh 0°* | `dung-yen/idle_0.mp4` | 720x1280 / 1080p, 4.0s loop | **Chờ** |
