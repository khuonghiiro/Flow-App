# BASE BODIES — Base Character Skeletons

## Purpose
Contains full character models with humanoid skeleton rigs, used as foundation for attaching modular parts (face, hair, costume).

## Supported Formats
- `.vrm` (recommended — includes BlendShapes for lip-sync and expressions)
- `.glb` (for characters not requiring lip-sync)

## Naming Convention
```
{gender}_{role}.vrm
```
Examples: `male_warrior.vrm`, `female_mage.vrm`, `child_body.vrm`

## Technical Requirements for VRM
- Skeleton: Humanoid standard bones (head, neck, spine, shoulders, arms, legs)
- Lip-sync BlendShapes: `aa`, `ih`, `ou`, `ee`, `oh`
- Expression BlendShapes: `blink`, `smile`, `angry`, `sorrow`, `surprised`
- Bone naming: VRM 0.0 or 1.0 standard

## AI Usage
When assembling a character, `base_body` is the foundation:
```json
{
  "assembly": {
    "base_body": "characters/base_bodies/male_warrior.vrm",
    "face": "characters/faces/face_male_young.glb",
    "hairstyle": "characters/hairstyles/hair_topknot.glb",
    "costume": "characters/costumes/costume_xianxia_white.glb"
  }
}
```
