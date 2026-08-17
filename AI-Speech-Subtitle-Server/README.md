# 🎙️ AI Speech-to-Subtitle & Translation Server

> **Hệ thống nhận diện giọng nói, tách mốc thời gian phụ đề & dịch thuật đa ngôn ngữ tự động (Hỗ trợ 4 Engine AI hàng đầu: Faster-Whisper, WhisperX, SenseVoice, Meta SeamlessM4T).**  
> Tích hợp sẵn **Giao diện Web UI hiện đại** và **REST API chuẩn** cho [FlowMy Workflow](https://github.com/khuonghiiro/Flow-App) (`HttpRequestNode`).

---

## 🌟 4 Dự Án AI Tùy Chọn Theo Cấu Hình Máy

1. **⚡ Faster-Whisper + NLLB-200** *(Khuyên dùng nhất)*:
   - Cân bằng hoàn hảo giữa tốc độ và độ chính xác. Dịch phụ đề sang tiếng Việt cực chuẩn.
   - *Yêu cầu: GPU 4GB - 8GB VRAM (hoặc CPU đa nhân).*
2. **🎯 WhisperX Alignment**:
   - Mốc thời gian chính xác từng từ (Word-Level Phoneme Alignment), chống trôi phụ đề cho video dài.
   - *Yêu cầu: GPU >= 8GB - 12GB VRAM.*
3. **🚀 SenseVoice Ultra-Fast**:
   - Siêu nhẹ, siêu nhanh (nhanh hơn Whisper 10x), nhận diện đa ngôn ngữ tức thì.
   - *Yêu cầu: Thuần CPU / Laptop mỏng nhẹ hoặc GPU 2GB - 4GB.*
4. **🌐 Meta SeamlessM4T v2**:
   - Mô hình All-in-One của Meta AI dịch trực tiếp từ tiếng nói sang văn bản đa ngôn ngữ.
   - *Yêu cầu: GPU khủng >= 12GB - 16GB VRAM.*

---

## 🚀 Khởi Chạy Nhanh Trong 1 Click

### 1. Cài đặt môi trường
Chạy file:
```cmd
install_env.bat
```
*(File này sẽ tự động tạo môi trường ảo `.venv` cô lập trong thư mục dự án, tự động phát hiện card đồ họa NVIDIA CUDA để cài PyTorch tối ưu nhất, và cài các thư viện qua `pnpm` & `pip`).*

### 2. Khởi chạy
- **Chạy có giao diện Web UI**: Chạy `start_app_with_ui.bat` $\rightarrow$ Trình duyệt tự mở tại `http://127.0.0.1:8765`.
- **Chỉ chạy Server API chạy nền (Headless)**: Chạy `start_server_only.bat`.

---

## 📡 Tích Hợp Vào FlowMy (`HttpRequestNode`)

- **Method**: `POST`
- **URL**: `http://127.0.0.1:8765/api/transcribe`
- **Body JSON**:
```json
{
  "audio_path": "{{linkAudio}}",
  "chunkIndex": {{chunkIndex}},
  "target_lang": "vi",
  "engine": "faster-whisper",
  "model_size": "small"
}
```

- **Kết quả trả về tự động gán vào Timeline Video của FlowMy**:
```json
{
  "chunkIndex": 0,
  "language": "en",
  "segments": [
    {
      "start": 1.25,
      "end": 4.50,
      "text": "Xin chào các bạn đã quay trở lại với kênh"
    }
  ]
}
```

---

## 📂 Quản Lý Model Đã Tải

Mặc định tất cả các model được lưu tại thư mục `models/` trong dự án (hoặc thư mục bạn chỉ định trên Web UI). Bạn có thể xem dung lượng và bấm nút **"🗑 Xóa Model"** trên Web UI bất kỳ lúc nào để giải phóng ổ cứng.
