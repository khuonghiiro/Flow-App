# Hướng Dẫn Sử Dụng Module Image-to-Rig Pipeline

Module xử lý tự động chuyển đổi ảnh nhân vật 2D thành Model 3D hoàn chỉnh có sẵn khung xương (Skeleton) và trọng số da (Skinning Weights) định dạng `.glb` tương thích 100% với Three.js `GLTFLoader`.

Tối ưu hóa chạy cục bộ trên máy tính trang bị card đồ họa **NVIDIA RTX 3060 12GB VRAM** và 32GB RAM.

---

## 📋 Mục Lục
1. [Yêu cầu hệ thống & Cài đặt](#1-yêu-cầu-hệ-thống--cài-đặt)
2. [Giao diện Web Gradio (Dành cho Artist)](#2-giao-diện-web-gradio-dành-cho-artist)
3. [Quy trình 4 Giai đoạn](#3-quy-trình-4-giai-đoạn)
4. [Xử lý Biểu Cảm & Blendshapes (Quan trọng)](#4-xử-lý-biểu-cảm--blendshapes-quan-trọng)
5. [Tích hợp TypeScript / Three.js (Dành cho Dev)](#5-tích-hợp-typescript--threejs-dành-cho-dev)
6. [Kiểm tra Model bằng Trình Xem 3D Mẫu](#6-kiểm-tra-model-bằng-trình-xem-3d-mẫu)

---

## 1. Yêu cầu hệ thống & Cài đặt

### Yêu cầu
- Windows 10/11 64-bit hoặc Linux Ubuntu 22.04+
- Python 3.10 hoặc 3.11
- Card đồ họa NVIDIA RTX 3060 12GB (hỗ trợ CUDA 12.1 trở lên)

### Cài đặt nhanh bằng 1 click

**Trên Windows**:
Nhấp đúp chuột vào file `setup.bat` hoặc chạy trong PowerShell:
```powershell
.\setup.bat
```

Script sẽ tự động:
- Tạo môi trường ảo `venv`.
- Cài đặt PyTorch với CUDA 12.1.
- Cài đặt đầy đủ các thư viện TripoSR, UniRig, Trimesh, Pygltflib, Gradio, FastAPI.

---

## 2. Giao diện Web Gradio (Dành cho Artist)

Để khởi động giao diện trực quan:
```powershell
python app.py
```
Trình duyệt sẽ tự động mở tại địa chỉ: **`http://localhost:7860`**

### Các thao tác trên giao diện:
1. **Tải ảnh 2D**: Kéo thả ảnh nhân vật (PNG, JPG, WEBP). Hệ thống tự kiểm tra độ phân giải và cảnh báo nếu nền phức tạp.
2. **Giai đoạn 1 (Tạo Mesh)**: Bấm `🔨 Giai đoạn 1: Tạo Mesh` để sinh mesh 3D `.obj` kèm vân bề mặt texture atlas và xem trước ngay trên khung Model3D.
3. **Giai đoạn 2 (Auto Rig)**: Bấm `🦴 Giai đoạn 2: Auto Rig` để phân tích đối xứng và gắn xương Humanoid tiêu chuẩn.
4. **Chạy Toàn Trình**: Bấm `⚡ Chạy Toàn Trình (Generate & Rig)` để hoàn tất cả quy trình chỉ với 1 click.
5. **Tải kết quả**: Nhận file `.glb` (chứa mesh + skeleton + weights) và xem file `metadata.json` đính kèm.

---

## 3. Quy trình 4 Giai đoạn

| Giai đoạn | Công nghệ | Đầu vào | Đầu ra | Thời gian (RTX 3060) |
| :--- | :--- | :--- | :--- | :--- |
| **Stage 1: Image to Mesh** | TripoSR (`stabilityai/TripoSR`) | Ảnh 2D (512x512) | Mesh thô `.obj` + Texture atlas `.png` | ~8 - 15 giây |
| **Stage 2: Auto Rig** | UniRig (`VAST-AI-Research/UniRig`) | Mesh `.obj` | 19+ Humanoid Bones + Skinning Weights | ~2 - 4 giây |
| **Stage 3: Convert & Export** | `pygltflib` / `trimesh` | Mesh + Bones + Weights | File `.glb` hợp lệ glTF 2.0 + `metadata.json` | ~0.5 giây |
| **Stage 4: Blendshapes** | VRM Extractor / Blender Helper | File VRM hoặc Head Submesh | Morph targets / Script Blender sculpt | Bán tự động |

---

## 4. Xử lý Biểu Cảm & Blendshapes (Quan trọng)

> [!IMPORTANT]
> **Thực tế kỹ thuật**: Các mô hình AI hiện nay (TripoSR, InstantMesh, UniRig) chỉ tạo ra mesh và xương mềm toàn thân, **chưa thể tự động tạo chuẩn xác các shape keys / blendshapes biểu cảm khuôn mặt** (nháy mắt, cười, mở khẩu hình lipsync).

Hệ thống phân chia rõ 2 nhánh xử lý:

### Nhánh A: Nhân vật từ VRoid Studio (`.vrm`)
- Nếu bạn có sẵn file `.vrm`, toàn bộ hơn 52 chuẩn ARKit Blendshapes (nháy mắt, lipsync `a, i, u, e, o`, cảm xúc vui/buồn/giận) đã có sẵn.
- Chuyển sang **Tab 2 trên Web UI** để chuyển đổi và bảo toàn 100% morph targets này.

### Nhánh B: Nhân vật sinh từ ảnh 2D (TripoSR)
- Hệ thống tự động tách riêng vùng khuôn mặt thành file `<tên>_face_scaffold.obj` và tạo sẵn script `<tên>_setup_shapekeys.py`.
- **Thao tác nhanh cho Artist (2-3 phút)**:
  1. Mở phần mềm Blender.
  2. Mở tab *Scripting* và chạy script `setup_shapekeys.py` vừa sinh ra.
  3. Script sẽ tự tạo sẵn các Shape Key: `Basis`, `mouth_open`, `eye_blink_left`, `eye_blink_right`, `smile_left`, `smile_right`.
  4. Chuyển sang **Sculpt Mode**, kéo nhẹ khóe miệng / mí mắt để hoàn thiện.

---

## 5. Tích hợp TypeScript / Three.js (Dành cho Dev)

### Chạy REST API Server
```powershell
python server.py
```
Swagger UI tài liệu API có tại: `http://localhost:8000/docs`

### Code mẫu load trong Three.js (TypeScript)
```typescript
import { loadRiggedCharacter, playWaveAnimation } from './examples/threejs_loader_example';

// 1. Tải nhân vật từ file GLB đã xuất
const character = await loadRiggedCharacter('/outputs/hero_rigged.glb');
scene.add(character.scene);

// 2. Danh sách xương đã nhận diện
console.log('Các xương:', Array.from(character.bones.keys()));

// 3. Render loop animation
function animate(time: number) {
  playWaveAnimation(character, time);
  renderer.render(scene, camera);
}
```

---

## 6. Kiểm tra Model bằng Trình Xem 3D Mẫu

Mở file `examples/sample_viewer.html` trực tiếp bằng trình duyệt (Chrome/Edge/Firefox).
- Kéo thả file `.glb` bất kỳ vừa sinh ra vào vùng hiển thị.
- Bấm **🦴 Bật/Tắt Khung Xương** để xem cấu trúc skeleton bên trong.
- Bấm **👋 Chạy Animation Thử Nghiệm** để xem tay nhân vật chuyển động với skinning weights mượt mà.
