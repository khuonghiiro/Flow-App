# 📼 THƯ MỤC XUẤT VIDEO & PHỤ ĐỀ (EXPORTS)

---

## 📌 1. Mục Đích & Vai Trò (Purpose)
Chứa các thành phẩm video và phụ đề sau khi User hoặc AI bấm nút **"Xuất MP4"** trên Studio.

---

## 📂 2. Các Loại File Xuất Ra
1. **Video MP4 / WebM HFR:**
   - Định dạng: `.mp4` (H.264 / AAC) hoặc `.webm` (VP9 / Opus).
   - Tốc độ khung hình: 30 FPS, 60 FPS hoặc **120 FPS (Siêu Mượt WebCodecs GPU)**.
   - Tùy chọn: Đã thiêu phụ đề cứng (Burn-in Subtitles) trực tiếp lên từng khung hình pixel nếu bật `burn_in_export: true`.
2. **File Phụ Đề Rời (.srt):**
   - File subtitle định dạng chuẩn `.srt` với timestamp mili-giây khớp 100% từng câu thoại, dùng để upload lên YouTube / TikTok / Facebook.

---

## 📂 3. Quy Ước Đặt Tên File Xuất (Output Naming Convention)
- Video: `{scene_id}_{target_fps}fps.mp4` (Ví dụ: `scene_tree_climbing_120fps.mp4`).
- Phụ đề: `{scene_id}_subtitles.srt` (Ví dụ: `scene_tree_climbing_subtitles.srt`).
