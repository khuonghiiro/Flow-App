# 👤 THƯ MỤC NHÂN VẬT 3D (CHARACTERS)

---

## 📌 1. Mục Đích & Vai Trò (Purpose)
Chứa các mô hình nhân vật 3D (Actors) được đưa vào các thước phim.

- **Định dạng khuyến nghị:** `.vrm` (VRM 0.0 hoặc 1.0) hoặc `.glb`.
- **Lý do dùng VRM:** File VRM đã tích hợp sẵn hệ thống xương hình người chuẩn (Humanoid Bone Rig) và các hình thái cơ mặt (BlendShapes / Morph Targets) như: chớp mắt (`blink`), khẩu hình (`aa`, `ih`, `ou`, `ee`, `oh`), biểu cảm (`smile`, `angry`, `sorrow`, `surprised`).

---

## 📂 2. Quy Tắc Đặt Tên File (Naming Convention)
- Đặt tên theo cú pháp chữ thường không dấu, nối bằng gạch dưới:
  - `hero_knight.vrm`
  - `dark_mage.vrm`
  - `village_girl.vrm`
  - `monster_orc.glb`

---

## 🤖 3. Hướng Dẫn Cho AI (How AI Uses This Folder)
Khi AI lên kịch bản `MasterSceneConfig`, trường `actors[].model` sẽ trỏ đến file trong thư mục này:

```json
{
  "id": "actor_dark_mage",
  "name": "Phù Thủy Hắc Ám",
  "model": "characters/dark_mage.vrm",
  "spawn_point": [2.0, 0, 1.0],
  "tracks": {
    "movement": [{ "start": 0.0, "end": 5.0, "action": "walk" }],
    "speech": [{ "line_ref": "dlg_01", "expressions": [{ "type": "smirk", "weight": 0.8 }] }]
  }
}
```
