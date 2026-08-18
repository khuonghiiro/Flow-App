# 💥 THƯ MỤC HIỆU ỨNG ÂM THANH (SOUND EFFECTS - SFX)

---

## 📌 1. Mục Đích & Vai Trò (Purpose)
Chứa các mẫu âm thanh ngắn (0.5s - 3s) thể hiện các va chạm vật lý và kỹ năng:

- **Combat SFX:**
  - `sword_slash_heavy.wav`: Tiếng chém kiếm mạnh.
  - `metal_clash_sparks.wav`: Tiếng kiếm va vào nhau nảy lửa.
  - `body_hit_impact.wav`: Tiếng trúng đòn vật lý.
  - `knockdown_fall.wav`: Tiếng ngã đập người xuống đất.
- **Environment & Foley SFX:**
  - `footstep_grass.wav`: Tiếng bước chân trên cỏ.
  - `footstep_wood.wav`: Tiếng bước chân trên sàn gỗ.
  - `tree_leaf_rustle.wav`: Tiếng lá cây xào xạc khi leo cây hoặc ẩn nấp.
  - `wood_creak_sit.wav`: Tiếng ghế gỗ kêu kẽo kẹt khi ngồi xuống.

## 🤖 2. Hướng Dẫn Cho AI
Hệ thống `CombatVFXTrigger.ts` và `TrackEvaluator.ts` sẽ tự động trigger các file SFX tương ứng ngay đúng mốc `impact_time` của cú chém hoặc bước chân nhân vật.
