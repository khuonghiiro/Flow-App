# 🎭 STICKER BIỂU CẢM ANIME (ANIME EMOTE GIFS)

## Mục Đích
Chứa các tệp nhãn dán `.gif` hoạt họa 2D phong cách hoạt hình Nhật Bản (Anime) gắn lên đầu hoặc xung quanh nhân vật.

## Danh Sách Nhãn Dán Hỗ Trợ
- \`sweat_drop.gif\` (💧 Giọt mồ hôi chảy — ngượng ngùng, bối rối, sợ hãi)
- \`anger_vein.gif\` (💢 Dấu gân đỏ giật giật — tức giận, cáu kỉnh)
- \`sparkles.gif\` (✨ Tia sáng lấp lánh — hào hứng, khai sáng, uy phong)
- \`question_mark.gif\` (❓ Dấu hỏi chấm — thắc mắc, khó hiểu)
- \`tears_stream.gif\` (😭 Dòng lệ tuôn rơi phong cách hài hước)

## Cách Dùng Trong Kịch Bản
Thêm vào mảng \`gif_overlays\` của nhân vật:
\`\`\`json
"gif_overlays": [
  {
    "start": 2.0,
    "end": 4.5,
    "gif_path": "vfx/emotes/sweat_drop.gif",
    "attach_to": "head",
    "offset": [0.2, 0.45, 0]
  }
]
\`\`\`
