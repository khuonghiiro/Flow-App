# ANIME EMOTE STICKERS — 2D Emotion GIFs & Overlays

## Purpose
Contains animated `.gif` or `.png` stickers for anime-style facial reactions and emotional cues.

## Supported Presets
- \`sweat_drop.gif\` (💧 Anime blue sweat drop — embarrassed, exhausted, nervous)
- \`anger_vein.gif\` (💢 Red throbbing cross vein — irritated, furious)
- \`sparkles.gif\` (✨ Golden sparkles — amazed, heroic, inspired)
- \`question_mark.gif\` (❓ Purple question mark — confused, curious)
- \`tears_stream.gif\` (😭 Dramatic anime tear waterfall)
- \`shock_lightning.gif\` (⚡ Shock electric flash behind eyes)

## AI Usage
Attach to actor tracks via \`gif_overlays\`:
\`\`\`json
"gif_overlays": [
  {
    "start": 2.0,
    "end": 4.5,
    "gif_path": "vfx/emotes/anger_vein.gif",
    "attach_to": "head",
    "offset": [0.2, 0.45, 0],
    "scale": 0.45
  }
]
\`\`\`
