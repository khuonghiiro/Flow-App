# HAIRSTYLES — Character Hair Meshes

## Naming Convention
```
hair_{style}_{trait}.glb
```
Examples: `hair_short_spiky.glb`, `hair_long_flowing.glb`, `hair_topknot.glb`, `hair_twin_tails.glb`

## Technical Requirements
- Attach point: `head` bone.
- Supports runtime tinting via `hair_color` in the assembly config.

## AI Usage
```json
{ "assembly": { "hairstyle": "characters/hairstyles/hair_topknot.glb", "hair_color": "#1a1a2e" } }
```
