# 🏃 THƯ MỤC HOẠT ẢNH & CHUYỂN ĐỘNG (ANIMATIONS & MOCAP)

---

## 📌 1. Mục Đích & Vai Trò (Purpose)
Chứa các dữ liệu chuyển động (Animation Clips / Mocap) để áp dụng lên khung xương Humanoid của nhân vật 3D:
- **Định dạng:** `.bvh`, `.fbx`, `.glb`, hoặc JSON keyframe math curves.

---

## 📂 2. Danh Mục Các Hành Động Chuẩn (Standard Actions)
1. **Di chuyển cơ bản:**
   - `idle`: Đứng thở nhịp nhàng, chớp mắt tự nhiên.
   - `walk`: Đi bộ tự nhiên với đầu gối gập và tay vung đối xứng.
   - `run`: Chạy nhanh tiến tới đích.
2. **Tương tác thông minh:**
   - `sit`: Ngồi ghế (gập đùi 90°, gập đầu gối hạ người tại chỗ, tay đặt lên đùi).
   - `climb`: Trèo cây (quay mặt ôm vỏ cây, ngửa đầu nhìn lên, tay kéo người lên, chân đạp nhịp nhàng).
   - `talk_gesture`: Đứng hoa chân múa tay khi nói chuyện.
3. **Chiến đấu & Combat:**
   - `heavy_slash_combo`: Vung kiếm chém combo 3 nhát dũng mãnh.
   - `stagger_back`: Bị trúng đòn loạng choạng giật lùi về phía sau kèm mặt đau đớn.
   - `fly_back_knockdown`: Bị lực chém đánh văng ngã ra sau.
   - `magic_blast`: Niệm phép phóng chưởng.

---

## 🤖 3. Hướng Dẫn Cho AI (How AI Uses This Folder)
Khi AI lên timeline di chuyển của nhân vật (`tracks.movement`), chỉ cần gán tên hành động vào trường `action`:
```json
{
  "start": 0.0,
  "end": 6.0,
  "action": "walk",
  "destination": [1.2, 0, -1.0]
}
```
Bộ diễn hoạt `ActorAnimator.ts` sẽ tự động tính toán pha chuyển tiếp (blend weight) mượt mà giữa các hành động.
