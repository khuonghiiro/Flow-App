# 🎨 THƯ MỤC TÀI NGUYÊN TỔNG HỢP (ASSETS ROOT)

---

## 📌 1. Mục Đích & Vai Trò (Purpose)
Thư mục `assets/` là kho chứa toàn bộ tài nguyên đầu vào (3D models, textures, audio, animations, maps) phục vụ cho hệ thống AI và Studio dựng phim 3D.

- **Đối với User:** Bạn có thể thả các file nhân vật, đạo cụ, âm thanh, hoạt ảnh hoặc bản đồ vào các thư mục con tương ứng.
- **Đối với AI Agent:** Khi AI đọc thư mục này, AI sẽ quét (Scan) danh mục tài nguyên để biết studio hiện có những nhân vật, map, vũ khí, hiệu ứng nào để tự động lên kịch bản dựng phim.

---

## 📂 2. Cấu Trúc Các Thư Mục Con (Folder Tree)

```text
assets/
├── characters/     # Model nhân vật 3D (.vrm, .glb)
├── maps/           # Bản đồ môi trường, cảnh quan, skybox (.glb, .gltf)
├── props/          # Đạo cụ, bàn ghế, cây cối, vũ khí, đồ vật tương tác
├── audio/          # Âm thanh (nhạc nền BGM, hiệu ứng SFX, giọng lồng tiếng TTS)
│   ├── bgm/        # Nhạc nền cảnh quay (.mp3, .wav)
│   ├── sfx/        # Tiếng động hiệu ứng chém, bước chân, va chạm (.mp3, .wav)
│   └── dialogues/  # File âm thanh giọng đọc thoại được tạo từ TTS
└── animations/     # Hoạt ảnh chuyển động cơ thể, mocap (.bvh, .fbx, .json)
```

---

## ⚙️ 3. Quy Ước Cho AI Khi Đọc Tài Nguyên (AI Instruction Guidelines)
1. **Quét định danh (ID matching):** Mỗi file trong thư mục này được gán một `id` tương ứng với đường dẫn tương đối (Ví dụ: `characters/dark_mage.vrm` có ID là `dark_mage`).
2. **Kiểm tra định dạng hợp lệ:**
   - 3D Model: `.vrm` (khuyến nghị cho nhân vật vì có xương mặt morph lip-sync), `.glb`, `.gltf` (cho map/đạo cụ).
   - Audio: `.mp3`, `.wav`, `.ogg`.
   - Motion: `.bvh`, `.glb`, `.json`.
