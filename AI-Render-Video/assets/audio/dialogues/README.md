# 🗣️ THƯ MỤC FILE THOẠI LỒNG TIẾNG (TTS DIALOGUES)

---

## 📌 1. Mục Đích & Vai Trò (Purpose)
Chứa các file audio giọng lồng tiếng của từng câu thoại được sinh tự động bởi hệ thống Text-to-Speech (TTS) hoặc do User thu âm đưa vào.

---

## 📂 2. Quy Tắc Đặt Tên Tự Động (Auto Naming Rule)
Mỗi câu thoại được đặt tên chuẩn theo cú pháp:
`{scene_id}_{speaker_id}_{line_id}.mp3`

Ví dụ:
- `scene_village_clash_actor_dark_mage_dlg_01.mp3`
- `scene_village_clash_actor_warrior_dlg_02.mp3`
- `scene_tree_climbing_actor_dark_mage_dlg_tree_01.mp3`

---

## 🤖 3. Hướng Dẫn Cho AI (How AI Uses This Folder)
1. Trong `dialogues_manifest`, trường `audio_path` sẽ trỏ đến file trong thư mục này:
   ```json
   {
     "line_id": "dlg_tree_01",
     "speaker_id": "actor_dark_mage",
     "speaker_name": "Phù Thủy",
     "text": "Từ ngọn cây này có thể bao quát toàn bộ ngôi làng!",
     "audio_path": "audio/dialogues/scene_tree_climbing_actor_dark_mage_dlg_tree_01.mp3",
     "status": "ready",
     "start_time": 4.5,
     "estimated_duration": 3.5,
     "actual_duration": 3.42
   }
   ```
2. **Khẩu hình & Lip-Sync:**
   Hệ thống `ActorLipSync.ts` sẽ đọc độ dài thực tế `actual_duration` của file audio này và kích hoạt chuyển động môi miệng (`aa`, `ih`, `ou`, `ee`, `oh`) khớp nhịp 100% với giọng nói phát ra.
