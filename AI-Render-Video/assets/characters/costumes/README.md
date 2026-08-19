# COSTUMES — Clothing and Robes

## Naming Convention
```
costume_{style}_{variant}.glb
```
Examples: `costume_knight_armor.glb`, `costume_xianxia_white.glb`, `costume_mage_robe.glb`, `costume_super_armor.glb`

## Technical Requirements
- Replaces or overlays torso and limb meshes on the base body.
- Supports runtime transformations via the `transformations` timeline track.

## AI Usage
```json
{ "assembly": { "costume": "characters/costumes/costume_xianxia_white.glb" } }
```
