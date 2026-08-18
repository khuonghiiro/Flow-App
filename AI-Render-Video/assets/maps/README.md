# 🗺️ THƯ MỤC BẢN ĐỒ & MÔI TRƯỜNG (MAPS & ENVIRONMENTS)

---

## 📌 1. Mục Đích & Vai Trò (Purpose)
Chứa các bản đồ 3D thế giới, địa hình, công trình kiến trúc, bối cảnh dựng phim.

- **Định dạng:** `.glb`, `.gltf`, hoặc các procedural map modules được đăng ký trong engine.
- **Thành phần đi kèm:**
  - Lớp bề mặt địa hình (Terrain / Ground Mesh).
  - Lưới điều hướng di chuyển (Navigation Mesh / NavMesh) để nhân vật tự động tìm đường tránh vật cản.
  - Vị trí các điểm xuất hiện (Spawn Points) và khu vực tương tác.

---

## 📂 2. Danh Sách Map Mặc Định Có Sẵn
1. `farming_village`: Ngôi làng nông trại yên bình (đường đất, hàng rào gỗ, ruộng lúa, cây cổ thụ, quán nước).
2. `cyber_city`: Thành phố tương lai neon cyberpunk.
3. `ancient_temple`: Ngôi đền cổ hoang sơ đổ nát.
4. `dungeon_arena`: Đấu trường ngục tối phục vụ combat.

---

## 🤖 3. Hướng Dẫn Cho AI (How AI Uses This Folder)
Trong cấu hình `MasterSceneConfig`, trường `environment.map` sẽ chọn map từ thư mục này:

```json
{
  "environment": {
    "map": "farming_village",
    "sky_time": "sunset",
    "weather": {
      "fog": 0.01,
      "wind": 0.4,
      "rain": 0.0
    }
  }
}
```
- `sky_time`: `sunrise` (bình minh), `noon` (buổi trưa), `sunset` (hoàng hôn), `night` (ban đêm).
- AI tự động điều chỉnh hướng mặt trời, màu ánh sáng và bóng đổ phù hợp với map.
