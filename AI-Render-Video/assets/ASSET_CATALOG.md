# ASSET CATALOG — AI 3D Animation Studio

> **FOR AI AGENTS:** This file is the single source of truth for all available scene resources. Read this file carefully before generating JSON `MasterSceneConfig`.
> **Language Rule:** AI agents must read `ASSET_CATALOG.md` (English). Do not rely on `_VI.md` files which are formatted for human users.
> **Auto-generated:** 2026-08-19 08:23:22
> **Total assets:** 5 files, 23.5 MB

---

## 1. AI Guidelines for Scene JSON Generation

### Step 1: Map Selection
Reference an available environment model via `environment.map`:
```json
"environment": {
  "map": "medieval_fantasy_book",
  "sky_time": "sunset",
  "weather": { "fog": 0.015, "wind": 0.3 }
}
```

### Step 2: Modular Character Assembly
Characters can be composed from modular parts using the `assembly` object:
```json
{
  "id": "actor_cultivator",
  "name": "Master Li",
  "model": "characters/sample_avatar.vrm",
  "assembly": {
    "base_body": "characters/base_bodies/male_warrior.vrm",
    "face": "characters/faces/face_male_young.glb",
    "hairstyle": "characters/hairstyles/hair_topknot.glb",
    "costume": "characters/costumes/costume_xianxia_white.glb",
    "accessories": ["characters/accessories/acc_headband.glb"],
    "skin_color": "#ffd1b3",
    "hair_color": "#1a1a2e"
  },
  "spawn_point": [0, 0, 0]
}
```
*Backward Compatibility:* If modular parts are not available, specify `model: "characters/sample_avatar.vrm"` directly.

### Step 3: Animation & Expression Selection
Use exact IDs from the tables below:
- **Movement Action:** `idle`, `walk`, `run`, `fly_to`, `arms_crossed`, `hands_behind_back`, `meditate`
- **Facial Expression:** `neutral`, `angry`, `pain`, `smile`, `cold`, `arrogant`, `contempt`, `wise`, `fierce`, `meditative`
- **Combat & Interactions:** `combat_actions`, `combat_master`, `object_interactions`, `transformations`

---

## 2. Available Asset Catalog

### Characters — Base Bodies
| ID | Path | Format | Size |
|:---|:---|:---|---:|
| `sample_avatar` | `characters/sample_avatar.vrm` | VRM | 10.28 MB |


### Characters — Faces
*No face assets found. Add .glb to characters/faces/*


### Characters — Hairstyles
*No hairstyle assets found. Add .glb to characters/hairstyles/*


### Characters — Beards
*No beard assets found. Add .glb to characters/beards/*


### Characters — Costumes
*No costume assets found. Add .glb to characters/costumes/*


### Characters — Accessories
*No accessory assets found. Add .glb to characters/accessories/*


### Props — Weapons
*No weapon assets found. Add .glb to props/weapons/*


### Props — Tools
*No tool assets found. Add .glb to props/tools/*


### Props — Consumables
*No consumable assets found. Add .glb to props/consumables/*


### Props — Furniture
*No furniture assets found. Add .glb to props/furniture/*


### Props — Buildings
*No building assets found. Add .glb to props/buildings/*


### Props — Nature
*No nature assets found. Add .glb to props/nature/*


### Props — Vehicles
*No vehicle assets found. Add .glb to props/vehicles/*


### Props — Legacy Root
| ID | Path | Format | Size |
|:---|:---|:---|---:|
| `duck_prop` | `props/duck_prop.glb` | GLB | 0.11 MB |
| `lantern_prop` | `props/lantern_prop.glb` | GLB | 9.42 MB |


### Maps & Environments
| ID | Path | Format | Size |
|:---|:---|:---|---:|
| `scene` | `maps/medieval_fantasy_book/scene.gltf` | GLTF | 0.03 MB |
| `medieval_fantasy_book` | `maps/medieval_fantasy_book.glb` | GLB | 3.70 MB |


### Audio — Background Music (BGM)
*No BGM tracks found.*


### Audio — Combat Sound Effects
*No combat sound effects found.*


### Audio — Interaction Sound Effects
*No interaction sound effects found.*


### Audio — Ambient Sounds
*No ambient sounds found.*


### Animations — Combat
*Procedural animations active. Optional mocap clips in animations/combat/*


### Animations — Interactions
*Procedural animations active. Optional mocap clips in animations/interaction/*


### Animations — Xianxia Cultivation
*Procedural poses active via XianxiaPoseLibrary. Optional clips in animations/xianxia/*


### Animations — Locomotion
*Procedural locomotion active. Optional clips in animations/locomotion/*


### VFX Textures
*Procedural VFX shaders active.*


---

## 3. Supported Actions & Expressions Reference

### Locomotion & Body Actions (40 Actions)
- **Basic:** `idle`, `walk`, `run`, `sit`, `climb`
- **Advanced Locomotion:** `fly_to`, `dash_to`, `teleport`, `kneel`, `bow`, `meditate`
- **Combat:** `heavy_slash_combo`, `fast_slash`, `magic_blast`, `punch_kick`, `fly_back_knockdown`, `stagger_back`, `block_defend`, `dodge`
- **Xianxia Poses:** `arms_crossed`, `hands_behind_back`, `fist_salute`, `finger_spell`, `power_charge`, `flying_stance`
- **Object Interactions:** `pickup_right`, `carry_two_hands`, `drink`, `pour`, `dig`, `water_plants`, `plant_seed`, `harvest`, `wave`, `dance`, `throw`

### Facial Expressions (21 Expressions)
- **Standard:** `neutral`, `angry`, `pain`, `smile`, `smirk`, `sad`, `serious`, `surprised`, `shock`
- **Xianxia Dramatic:** `cold`, `arrogant`, `contempt`, `wise`, `fierce`, `meditative`, `menacing`, `compassionate`, `determined`
