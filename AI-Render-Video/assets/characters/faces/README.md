# FACES — Modular Face Meshes

## Purpose
Contains separate face meshes that attach to the `head` bone of a base body.
Allows changing character facial features without replacing the entire model.

## Naming Convention
```
face_{gender}_{trait}.glb
```
Examples: `face_male_young.glb`, `face_female_cute.glb`, `face_old_wise.glb`, `face_fierce_warrior.glb`

## Technical Requirements
- Attach point: `head` bone
- Mesh must match base body head scale (~0.32 width)
- Should include: eyes, nose, mouth, eyebrows
- Optional BlendShapes: `blink`, `smile`, `angry`, `pain`

## AI Usage
```json
{ "assembly": { "face": "characters/faces/face_fierce_warrior.glb" } }
```
If `face` is null or omitted, the default face from base_body is used.
