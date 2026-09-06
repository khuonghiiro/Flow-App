# Kế Hoạch Tạo Bộ Nhân Vật: Bạch Vô Trần (Tiên Môn Kiếm Tử)

## 1. Thông Tin Dự Án Google Flow
- **Nhân vật**: Bạch Vô Trần (Thanh Lãnh Kiếm Tiên)
- **Project Name**: `Bach Vo Tran`
- **Project ID**: `50e2c64e-887c-495e-9da3-ae112aa9f385`
- **Google Flow Project Link**: [https://flow.google.com/project/50e2c64e-887c-495e-9da3-ae112aa9f385](https://flow.google.com/project/50e2c64e-887c-495e-9da3-ae112aa9f385)
- **Thư mục output**: `agent-veo3/output/bach-vo-tran/`
- **Giới tính**: Male (Nam)

## 2. Hồ Sơ Nhân Vật (YAML Specification)

```yaml
character:
  name: "Bạch Vô Trần (Bai Wuchen)"
  gender: "male"
  age: "young adult (20-22)"
  personality: "Thanh lãnh cao quý, kiếm ý nội liễm, tiêu sái thoát tục"
  combat_style: "Kiếm khí chỉ pháp, ngự kiếm hư không, tay không linh lực"

  hair: "Tóc đen dài mượt buộc nửa đầu kiểu tiên khí (Half-up Celestial), cố định bằng Bạch Ngọc Quan chạm viền bạc, hai lọn tóc mai buông rủ thanh tú ôm gò má, suối tóc xõa mượt ngang lưng"
  skin: "Fair warm ivory natural jade skin tone, đồng nhất hoàn toàn giữa mặt, cổ và hai bàn tay"

  outfit: "Trường bào tiên môn giao lĩnh 2 tầng màu trắng tuyết (pure snow-white daoist robe), viền chỉ lam băng thanh nhã (pale icy blue & silver trim), tà áo đôi rủ thẳng tự nhiên xuôi theo trọng lực, chiến ủng vải thêu đế bằng đế thấp tuyệt đối (strictly flat cloth daoist boots, zero heels)"

  primary_color: "Pure Snow White & Pale Icy Cyan"
  accent_color: "Soft Celestial Silver & Jade Blue"

  waist_belt_logic:
    style_type: "Đai gấm tam tầng ngọc bội thanh nhã"
    front: "Khóa ngọc bài tròn chạm phù điêu tường vân mây trắng, dải dây lụa tết ngọc bội nhỏ rủ nhẹ bên hông"
    back_rule:
      male: "strictly continuous flat belt band behind back, ZERO bow, ZERO ribbon knot"

  fabric_physics: "natural downward drape under calm gravity, strictly calm static fabric, ZERO fake wind"
  weapon: "None (empty hands, pure martial arts aura)"
  spell_element: "Băng lôi thanh kiếm khí"
  style: "2D Xianxia/Fantasy anime chibi sprite, bold clean linework, flat cel-shaded coloring, mature 4.8-5.0 heads ratio"
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
| **0°** | `1fe21225-dfdd-4290-aecb-6717ef01734c` | `angle_0.png` | **Hoàn Thành** | Master Root Identity (Candidate 0) |
| **45°** | `751ca73d-a9d1-4efe-bfd7-38efe8da7c4d` | `angle_45.png` | **Hoàn Thành** | Dual-Ref Mannequin (3/4 Front-Left) |
| **90°** | `6971838f-17c9-479f-aa43-961f6ab6fc06` | `angle_90.png` | **Hoàn Thành** | Dual-Ref Mannequin (Pure Left Profile) |
| **135°** | `acc7ff6a-81c1-4afb-986f-d938676fa3a1` | `angle_135.png` | **Hoàn Thành** | Dual-Ref Mannequin (3/4 Back-Left, Chuẩn Pose Mannequin 135°) |
| **180°** | `fa506703-05e0-4c8c-bff1-39b4281d3479` | `angle_180.png` | **Hoàn Thành** | Dual-Ref Mannequin (Symmetrical Back) |

## 5. Registry Hoạt Ảnh Động Tác (Action Videos 4s Seamless Loop)

| Động Tác | Góc | Media ID (Flow) | Video MP4 | Thông Số | Trạng Thái |
|---|---|---|---|---|---|
| **Đứng Yên (Idle Loop)** | 0° | `db51dc4a-6e86-4a3e-ba00-83ddba3b152d` | `dung-yen/idle_0.mp4` | 720x1280, 4.0s, 1.17 MB | **Hoàn Thành** |

