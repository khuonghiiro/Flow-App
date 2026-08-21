# 📦 AI Models Directory (`models/`)

Thư mục lưu trữ tập trung các weights, checkpoints và mô hình trí tuệ nhân tạo phục vụ quy trình **Image-to-Rig 3D Pipeline**:

## Cấu Trúc Thư Mục

```
models/
├── triposr/            # Checkpoint TripoSR (Tạo Mesh 3D từ ảnh 2D)
│   ├── config.yaml
│   └── model.ckpt      # ~1.67 GB (stabilityai/TripoSR)
├── rembg/              # Mô hình tách phông nền tự động
│   └── u2net.onnx      # ~176 MB (danielgatis/rembg / U2NET_HOME)
└── unirig/             # Mô hình dự đoán khung xương và skinning weights
    └── README.txt      # (VAST-AI-Research/UniRig)
```

## Cách Tải Model Tự Động

### Cách 1: Qua dòng lệnh CLI
```bash
python download_models.py
```

### Cách 2: Qua giao diện Web UI
Khởi chạy ứng dụng `python app.py` hoặc click đúp `_start.bat`, sau đó chuyển sang Tab **"📦 5. Quản Lý & Tải Model (models/)"** và bấm **"📥 Tải tất cả Model"**.

### Cách 3: Qua REST API
```bash
curl -X POST http://localhost:8000/api/v1/models/download
```
