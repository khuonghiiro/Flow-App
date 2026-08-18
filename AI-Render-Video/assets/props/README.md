# 🪑 THƯ MỤC ĐẠO CỤ & VẬT THỂ THÔNG MINH (PROPS & SMART SOCKETS)

---

## 📌 1. Mục Đích & Vai Trò (Purpose)
Chứa các vật thể 3D trong bối cảnh mà nhân vật có thể tương tác hoặc gắn vào người:
- **Đồ vật tương tác (Interactables):** Ghế ngồi (`wooden_chair`), Cây leo (`village_tree`), Luống cày cuốc (`farm_plot`), Cửa ra vào, Rương báu.
- **Vũ khí & Trang bị cầm tay (Equipments):** Kiếm (`flame_sword`), Trượng phép (`magic_staff`), Cung tên, Khiên chắn.

---

## 📍 2. Cơ Chế Smart Socket (Điểm Neo Tương Tác)
Mỗi vật thể tương tác đều có các điểm neo (`Socket`) định nghĩa trong `SmartSocketRegistry.ts`:
- **`entryPosition`**: Vị trí nhân vật bước tới đứng trước khi bắt đầu hành động (ví dụ: mép đệm ghế `z = -1.65`, gốc cây `z = -2.4`).
- **`targetPosition`**: Vị trí tọa độ đích sau khi tương tác xong (ví dụ: mông đặt trên ghế `y = 0.52`, ngồi trên cành cây `y = 2.3`).
- **`targetRotationY`**: Góc quay mặt chuẩn của nhân vật khi ngồi hoặc bám vào vật thể.

---

## 🤖 3. Hướng Dẫn Cho AI (How AI Uses This Folder)
Khi AI lên kịch bản chuyển động nhân vật, trường `target_object` sẽ trỏ đến mã ID của đạo cụ:

```json
{
  "start": 0.0,
  "end": 8.0,
  "action": "sit",
  "target_object": "props.wooden_chair_01"
}
```
- Khi nhận diện `props.wooden_chair_01`, AI và hệ thống diễn hoạt sẽ kích hoạt chuỗi hành động 4 pha tự nhiên (Đi tới ghế -> Xoay người tại chỗ -> Gập đầu gối hạ người -> Ngồi thư giãn).
