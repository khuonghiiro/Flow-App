# MAP PRESETS — Saved Environment Layouts

## Purpose
Contains pre-configured 3D map layouts (.json). Each preset defines placed objects (trees, buildings, chairs, water, obstacles), weather, lighting, and default spawn points.

## How AI Uses Map Presets
AI directors can reference any saved map preset in the scene's \`environment\` block:
\`\`\`json
"environment": {
  "map": "presets/sakura_lake_village",
  "map_preset": "sakura_lake_village",
  "sky_time": "sunset"
}
\`\`\`

AI can also place actors directly at pre-defined spawn points like \`"lakeside_bench"\` or \`"arena_center"\`.
