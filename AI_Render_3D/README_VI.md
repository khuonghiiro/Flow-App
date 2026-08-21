# Hướng Dẫn Sử Dụng Module Image-to-Rig Pipeline

Module xử lý tự động chuyển đổi ảnh nhân vật 2D thành Model 3D hoàn chỉnh có sẵn khung xương (Skeleton) và trọng số da (Skinning Weights) định dạng `.glb` tương thích 100% với Three.js `GLTFLoader`.

Tối ưu hóa chạy cục bộ trên máy tính trang bị card đồ họa **NVIDIA RTX 3060 12GB VRAM** và 32GB RAM.

---

## 📋 Mục Lục
1. [Khởi động Nhanh 1-Click (`_start.bat`)](#1-khởi-động-nhanh-1-click-_startbat)
2. [Quản Lý & Tải Model AI Vào Thư Mục `models/`](#2-quản-lý--tải-model-ai-vào-thư-mục-models)
3. [Cẩm Nang Prompt AI Tạo Ảnh Nhân Vật & Vật Liệu 3D Chuẩn](#3-cẩm-nang-prompt-ai-tạo-ảnh-nhân-vật--vật-liệu-3d-chuẩn)
4. [Cấu trúc Project Đã Triển Khai & Cấu Hình Chi Tiết](#4-cấu-trúc-project-đã-triển-khai--cấu-hình-chi-tiết)
5. [Giao diện Web Gradio (Dành cho Artist)](#5-giao-diện-web-gradio-dành-cho-artist)
6. [Quy trình 4 Giai đoạn](#6-quy-trình-4-giai-đoạn)
7. [Xử lý Biểu Cảm & Blendshapes (Quan trọng)](#7-xử-lý-biểu-cảm--blendshapes-quan-trọng)
8. [Tích hợp TypeScript / Three.js (Dành cho Dev)](#8-tích-hợp-typescript--threejs-dành-cho-dev)
9. [Kiểm tra Model bằng Trình Xem 3D Mẫu](#9-kiểm-tra-model-bằng-trình-xem-3d-mẫu)

---

## 1. Khởi động Nhanh 1-Click (`_start.bat`)

Chỉ cần **nhấp đúp chuột vào file `_start.bat`** (hoặc gõ `.\_start.bat` trong terminal):
1. Tự động kích hoạt môi trường ảo Python `venv`.
2. Khởi động ngầm **FastAPI Backend Server** tại `http://localhost:8000`.
3. Khởi động **Gradio Web UI** tại `http://localhost:7860`.
4. **Tự động mở trình duyệt web** đưa bạn thẳng vào giao diện làm việc mà không cần gõ lệnh thủ công từng server.

---

## 2. Quản Lý & Tải Model AI Vào Thư Mục `models/`

Tất cả các file weights / checkpoints AI được quản lý tập trung tại thư mục `models/` nằm cùng cấp root:

```
models/
├── triposr/            # Checkpoint TripoSR (Tạo Mesh 3D từ ảnh 2D)
│   ├── config.yaml
│   └── model.ckpt      # ~1.67 GB (stabilityai/TripoSR)
├── rembg/              # Mô hình tách phông nền tự động (U2NET_HOME)
│   └── u2net.onnx      # ~176 MB (danielgatis/rembg)
└── unirig/             # Mô hình dự đoán khung xương và skinning weights
    └── README.txt      # (VAST-AI-Research/UniRig)
```

### Các Cách Tải Model:
1. **Dòng lệnh CLI (Khuyên dùng)**:
   ```powershell
   python download_models.py
   ```
2. **Qua Web UI**:
   - Mở Web UI -> Chuyển sang Tab **"📦 5. Quản Lý & Tải Model (models/)"**.
   - Bấm **"📥 Tải tất cả Model (~2.4 GB)"** hoặc tải từng model riêng lẻ.
3. **Qua REST API**:
   ```bash
   curl -X POST http://localhost:8000/api/v1/models/download
   ```

---

## 3. Cẩm Nang Prompt AI Tạo Ảnh Nhân Vật & Vật Liệu 3D Chuẩn

Để mô hình 3D (TripoSR) và bộ xương (UniRig) nhận diện và tạo mesh chính xác nhất, ảnh 2D đầu vào cần tuân thủ các quy tắc sau:

### 3.1. Kích Thước & Tỷ Lệ Ảnh
* **Kích thước vuông tối ưu**: `1024 x 1024 px` (Tỷ lệ 1:1).
* **Kích thước toàn thân**: `896 x 1152 px` hoặc `768 x 1024 px` (Tỷ lệ 3:4).
* **Tỷ lệ nhân vật (Framing)**: Nhân vật đứng thẳng chiếm **80% - 85%** chiều cao khung hình. Chừa **10% khoảng trống trên đỉnh đầu và 10% dưới đáy chân** (tuyệt đối không để viền ảnh cắt cụt đỉnh đầu hay mũi chân).
* **Phông nền**: Nền trong suốt (PNG Alpha) hoặc nền đơn sắc **xám studio `#808080`** / **trắng `#FFFFFF`**.

### 3.2. Số Lượng Ảnh & Tư Thế
* **Chế độ Single-View 3D (TripoSR)**: Chỉ cần **1 ảnh chính diện duy nhất (Front View 0°)** chất lượng cao.
* **Chế độ Multi-View / Turnaround Sheet 360°**: Bộ **4 góc nhìn đồng bộ**: Mặt trước (0°), Nghiêng 3/4 (45°), Góc sườn (90°), Mặt sau (180°).
* **Tư thế bắt buộc**: **A-Pose (Tay dang 45°)**:
  * Hai cánh tay dang nghiêng 45° so với thân người, bàn tay mở tự nhiên (*relaxed open palms*).
  * Hai chân đứng thẳng mở rộng bằng vai, hai bàn chân song song hướng về phía trước.
  * Tránh: Khoanh tay, đút tay túi quần, vắt chéo chân, tư thế ngồi hay nghiêng người.

### 3.3. Quy Chuẩn Trang Phục, Giày Dép, Tóc & Phụ Kiện
* 👕 **Áo (Tops, Shirts, Jackets)**: Form dáng gọn gàng; tay áo tách rời khỏi thân sườn tạo khe hở nách rõ ràng; nếp gấp vải vừa phải.
* 👖 **Quần & Váy (Pants, Trousers, Skirts)**: Đáy quần và 2 ống chân phải tách rời nhau; ống quần không trùm kín bàn chân; váy nên là váy ngắn trên gối hoặc xẻ tà để lộ khớp gối.
* 👟 **Giày / Dép (Shoes, Boots, Footwear)**: Hai đế giày đặt phẳng tiếp xúc mặt đất; phân định rõ cổ chân/mắt cá chân và mu bàn chân.
* 💇 **Tóc (Hair & Hairstyles)**: Kiểu tóc phân khối rõ ràng (solid clumps/strands); tóc **không được che kín toàn bộ cổ và vai** để tránh dính mesh đầu vào ngực.
* 🧢 **Mũ & Phụ kiện (Hats, Glasses, Props)**: Mũ đội vừa vặn trên đầu; **KHÔNG** cho nhân vật cầm vũ khí/túi xách trên tay ở ảnh tạo rig (đạo cụ nên import gắn vào xương sau).

### 3.4. Kỹ Thuật Tạo Sinh Vật Liệu PBR
* **Ánh sáng chụp AI**: `soft diffused studio lighting, even ambient illumination, neutral color temperature 5500K`.
* **Tránh ánh sáng bóng đổ gắt**: Không dùng `dramatic shadows, harsh contrast, dark cinematic shadows` vì vệt đen sẽ bị nướng cố định vào texture map.
* **Keywords PBR**: `matte cotton fabric texture`, `smooth leather with subtle specular sheen`, `polished chrome metal reflections`, `matte rubber sole`.

### 3.5. Mẫu Prompt Sẵn Sàng Copy:
```text
full body shot of an athletic character standing in symmetrical A-pose, arms 45 degrees apart from torso, palms open, legs shoulder-width apart, wearing clean fitted casual clothes with distinct seamlines, modern sneakers with flat rubber soles, neat hairstyle with clean neck clearance, neutral facial expression looking at camera, soft studio diffused lighting, even ambient illumination without harsh shadows, isolated on solid neutral gray studio background #808080, 8k resolution, photorealistic PBR material textures, game ready asset --ar 3:4
```

---

## 4. Cấu trúc Project Đã Triển Khai & Cấu Hình Chi Tiết

| Thư mục / File | Trách nhiệm chính |
| :--- | :--- |
| [`models/`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/models/) | **Thư mục lưu trữ tập trung** các weights TripoSR, UniRig, RemBG U2Net |
| [`image_to_rig/tools/model_downloader.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/tools/model_downloader.py) | **Module quản lý & tải model AI** vào thư mục `models/` kèm tính toán dung lượng |
| [`image_to_rig/ui/prompt_guide.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/ui/prompt_guide.py) | **Cẩm nang hướng dẫn prompt AI & kho mẫu template copy 1-click** |
| [`image_to_rig/ui/models_tab.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/ui/models_tab.py) | **Tab quản lý model trên Web UI** hỗ trợ tải 1-click và giám sát dung lượng |
| [`download_models.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/download_models.py) | **Script CLI tải model tự động** từ terminal hoặc batch file |
| [`image_to_rig/config.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/config.py) | **Cấu hình VRAM RTX 3060 (12GB)**, đường dẫn `models/`, thư mục tạm và xuất file |
| [`image_to_rig/core/queue_manager.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/queue_manager.py) | **Quản lý hàng đợi GPU Mutex** (đơn job `max_concurrent=1`, chống tràn 12GB VRAM) |
| [`image_to_rig/core/validator.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/validator.py) | **Kiểm tra định dạng ảnh**, kích thước, độ tương phản và tính đối xứng hình học mesh |
| [`image_to_rig/core/stage1_mesh.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/stage1_mesh.py) | **Giai đoạn 1**: Chạy **TripoSR** nạp checkpoint từ `models/triposr` tạo mesh `.obj` + UV texture |
| [`image_to_rig/core/stage2_rig.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/stage2_rig.py) | **Giai đoạn 2**: Chạy **UniRig** dự đoán Skeleton & Skinning Weights Humanoid tiêu chuẩn |
| [`image_to_rig/core/stage3_export.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/stage3_export.py) | **Giai đoạn 3**: Đóng gói file glTF 2.0 Binary (`.glb`) có skinning hợp lệ + `metadata.json` |
| [`image_to_rig/core/stage4_blendshapes.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/stage4_blendshapes.py) | **Giai đoạn 4**: Nhánh A (bảo toàn ARKit VRM) & Nhánh B (tách face scaffold và sinh script tạo shape keys) |
| [`image_to_rig/core/pipeline.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/core/pipeline.py) | **Điều phối toàn trình** Image-to-Rig (kết nối Stage 1 -> 2 -> 3 -> 4) |
| [`image_to_rig/ui/gradio_app.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/ui/gradio_app.py) | **Giao diện Web Gradio** tích hợp 6 tab chức năng, Model3D preview, Prompt guide & Model manager |
| [`image_to_rig/api/server.py`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/image_to_rig/api/server.py) | **REST API FastAPI** cung cấp endpoints cho backend Studio TypeScript gọi trực tiếp |
| [`_start.bat`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/_start.bat) | **Script khởi động 1-Click**: Bật API Server -> Bật Web UI -> Tự động mở trình duyệt web |
| [`setup.bat`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/setup.bat) & [`setup.sh`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/setup.sh) | Script cài đặt môi trường tự động (PyTorch CUDA 12.1 + dependencies) |

---

## 5. Giao diện Web Gradio (Dành cho Artist)

Để khởi động giao diện trực quan:
```powershell
.\_start.bat
```
Hoặc:
```powershell
python app.py
```
Trình duyệt sẽ tự động mở tại địa chỉ: **`http://localhost:7860`**

### Các Tab Chức Năng:
1. **🚀 1. Tạo Model 3D & Auto-Rig**: Tải ảnh, xem kiểm tra hợp lệ, tạo mesh, auto rig và tải file `.glb`.
2. **📖 2. Hướng Dẫn Prompt & Chuẩn Ảnh AI**: Tra cứu kích thước, số lượng ảnh, quy tắc quần áo, giày dép, tóc, mũ và sao chép mẫu prompt 1-click.
3. **👤 3. Chuyển Đổi VRoid / VRM**: Bảo toàn 52+ blendshapes ARKit khi chuyển từ `.vrm` sang `.glb`.
4. **🎭 4. Sculpt Biểu Cảm Khuôn Mặt (Blender)**: Tải face scaffold và script tạo nhanh shape keys trong Blender.
5. **📦 5. Quản Lý & Tải Model (models/)**: Kiểm tra dung lượng ổ đĩa và tải các mô hình AI về máy cục bộ.
6. **⚙️ 6. Giám Sát Phần Cứng GPU**: Theo dõi lượng VRAM cấp phát/còn trống trên NVIDIA RTX 3060.

---

## 6. Quy trình 4 Giai đoạn

| Giai đoạn | Công nghệ | Đầu vào | Đầu ra | Thời gian (RTX 3060) |
| :--- | :--- | :--- | :--- | :--- |
| **Stage 1: Image to Mesh** | TripoSR (`models/triposr`) | Ảnh 2D (512x512) | Mesh thô `.obj` + Texture atlas `.png` | ~8 - 15 giây |
| **Stage 2: Auto Rig** | UniRig (`models/unirig`) | Mesh `.obj` | 19+ Humanoid Bones + Skinning Weights | ~2 - 4 giây |
| **Stage 3: Convert & Export** | `pygltflib` / `trimesh` | Mesh + Bones + Weights | File `.glb` hợp lệ glTF 2.0 + `metadata.json` | ~0.5 giây |
| **Stage 4: Blendshapes** | VRM Extractor / Blender Helper | File VRM hoặc Head Submesh | Morph targets / Script Blender sculpt | Bán tự động |

---

## 7. Xử lý Biểu Cảm & Blendshapes (Quan trọng)

Hệ thống phân chia rõ 2 nhánh xử lý:

### Nhánh A: Nhân vật từ VRoid Studio (`.vrm`)
- Toàn bộ hơn 52 chuẩn ARKit Blendshapes (nháy mắt, lipsync `a, i, u, e, o`, cảm xúc vui/buồn/giận) đã có sẵn.
- Chuyển sang **Tab 3 trên Web UI** để chuyển đổi và bảo toàn 100% morph targets này.

### Nhánh B: Nhân vật sinh từ ảnh 2D (TripoSR)
- Hệ thống tự động tách riêng vùng khuôn mặt thành file `<tên>_face_scaffold.obj` và tạo sẵn script `<tên>_setup_shapekeys.py`.
- **Thao tác nhanh cho Artist (2-3 phút)**:
  1. Mở phần mềm Blender.
  2. Mở tab *Scripting* và chạy script `setup_shapekeys.py` vừa sinh ra.
  3. Script sẽ tự tạo sẵn các Shape Key: `Basis`, `mouth_open`, `eye_blink_left`, `eye_blink_right`, `smile_left`, `smile_right`.
  4. Chuyển sang **Sculpt Mode**, kéo nhẹ khóe miệng / mí mắt để hoàn thiện.

---

## 8. Tích hợp TypeScript / Three.js (Dành cho Dev)

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

## 9. Kiểm tra Model bằng Trình Xem 3D Mẫu

Mở file [`examples/sample_viewer.html`](file:///d:/_DuAn/App_Desktop/workflows/Flow-My/AI_Render_3D/examples/sample_viewer.html) trực tiếp bằng trình duyệt (Chrome/Edge/Firefox).
- Kéo thả file `.glb` bất kỳ vừa sinh ra vào vùng hiển thị.
- Bấm **🦴 Bật/Tắt Khung Xương** để xem cấu trúc skeleton bên trong.
- Bấm **👋 Chạy Animation Thử Nghiệm** để xem tay nhân vật chuyển động với skinning weights mượt mà.
