# 🎬 THƯ MỤC KỊCH BẢN PHIM (SCENES & TIMELINE SCRIPTS)

---

## 📌 1. Mục Đích & Vai Trò (Purpose)
Chứa toàn bộ các file kịch bản dựng phim (`.json` hoặc `.yaml`) theo chuẩn cấu trúc `MasterSceneConfig`.

- **Đối với User:** Bạn có thể lưu các kịch bản phim bạn đã tạo ra tại đây, hoặc copy/paste các file kịch bản mẫu để Studio tải lên.
- **Đối với AI Agent:** Khi người dùng yêu cầu dựng một thước phim mới, AI sẽ tạo một file JSON trong thư mục này và nạp trực tiếp vào studio để phát ngay trên trình duyệt.

---

## 📂 2. Ví Dụ Cấu Trúc 1 File Kịch Bản Chuẩn (`scene_example.json`)

```json
{
  "scene_id": "scene_01_dai_chien",
  "title": "Tập 1: Đại Chiến Ngôi Làng",
  "fps": 60,
  "duration": 18.0,

  "environment": {
    "map": "farming_village",
    "sky_time": "sunset",
    "weather": { "fog": 0.01, "wind": 0.3 }
  },

  "subtitles_config": {
    "enable_overlay": true,
    "burn_in_export": true,
    "font_size": 22,
    "show_speaker_name": true,
    "position": "bottom",
    "text_color": "#ffffff"
  },

  "dialogues_manifest": [
    {
      "line_id": "dlg_01",
      "speaker_id": "actor_dark_mage",
      "speaker_name": "Phù Thủy",
      "speaker_color": "#a855f7",
      "text": "Từ ngọn cây này không ai có thể thấy được ta!",
      "voice_config": { "voice_id": "vi-VN-HoaiMyNeural", "speed": 1.0, "emotion": "serious" },
      "start_time": 4.5,
      "estimated_duration": 3.5
    },
    {
      "line_id": "dlg_02",
      "speaker_id": "actor_warrior",
      "speaker_name": "Chiến Binh",
      "speaker_color": "#eab308",
      "text": "Ngươi trốn kỹ quá, mau ra đây đấu một trận!",
      "voice_config": { "voice_id": "vi-VN-NamMinhNeural", "speed": 1.05, "emotion": "angry" },
      "start_time": 8.5,
      "estimated_duration": 4.0
    }
  ],

  "camera_tracks": [
    {
      "start": 0.0,
      "end": 4.5,
      "shot_type": "cinematic_dolly",
      "from": [7.8, 3.2, 3.8],
      "to": [5.2, 2.8, 1.2],
      "look_at": "actor_dark_mage.head",
      "fov": 48
    },
    {
      "start": 4.5,
      "end": 8.5,
      "shot_type": "face_close_up",
      "follow_target": "actor_dark_mage",
      "fov": 34
    },
    {
      "start": 8.5,
      "end": 14.0,
      "shot_type": "cinematic_dolly",
      "from": [1.2, 1.6, -0.6],
      "to": [1.6, 1.8, -0.9],
      "look_at": "actor_warrior.head",
      "fov": 46
    }
  ],

  "actors": [
    {
      "id": "actor_dark_mage",
      "name": "Phù Thủy",
      "model": "characters/dark_mage.vrm",
      "spawn_point": [2, 0, 1],
      "tracks": {
        "movement": [
          { "start": 0.0, "end": 18.0, "action": "climb", "target_object": "props.village_tree_01" }
        ],
        "speech": [
          { "line_ref": "dlg_01", "expressions": [{ "time_offset": 0.0, "type": "smirk", "weight": 0.8 }] }
        ]
      }
    },
    {
      "id": "actor_warrior",
      "name": "Chiến Binh",
      "model": "characters/hero_knight.vrm",
      "spawn_point": [-1.0, 0, 1.5],
      "tracks": {
        "movement": [
          { "start": 0.0, "end": 6.0, "action": "walk", "destination": [1.2, 0, -1.0] },
          { "start": 6.0, "end": 18.0, "action": "talk_gesture", "look_at": "actor_dark_mage.head" }
        ],
        "speech": [
          { "line_ref": "dlg_02", "expressions": [{ "time_offset": 0.0, "type": "surprised", "weight": 0.8 }] }
        ]
      }
    }
  ]
}
```
