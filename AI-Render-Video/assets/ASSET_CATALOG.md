# ASSET CATALOG — AI 3D Animation Studio

> **FOR AI AGENTS:** This file is the single source of truth for all available scene resources. Read this file carefully before generating JSON `MasterSceneConfig`.
> **Language Rule:** AI agents must read `ASSET_CATALOG.md` (English). Do not rely on `_VI.md` files which are formatted for human users.
> **Auto-generated:** 2026-08-19 08:42:49
> **Total assets:** 5 asset files (23.5 MB), 2 saved map presets

---

## 1. AI Guidelines for Scene JSON Generation

### Step 1: Map Selection & Saved Map Presets
You can use a raw map file OR reference a **Saved Map Preset** directly:

**Option A: Using a Saved Map Preset (Recommended when user asks for a saved map):**
```json
"environment": {
  "map": "farming_village",
  "map_preset": "sakura_lake_village",
  "sky_time": "sunset",
  "weather": { "fog": 0.012, "wind": 0.35 }
}
```
*Benefit:* When using a map preset, you can place actors at named spawn points (e.g. `[-3.5, 0, -1.8]` at lakeside bench) and interact with preset props (e.g. `"props.stone_bench_01"` or `"props.sakura_tree_01"`).

**Option B: Standard Raw Map:**
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
  "spawn_point": [-3.5, 0, -1.8]
}
```
*Backward Compatibility:* If modular parts are not available, specify `model: "characters/sample_avatar.vrm"` directly.

### Step 3: Animation & Expression Selection
Use exact IDs from the tables below:
- **Movement Action:** `idle`, `walk`, `run`, `fly_to`, `arms_crossed`, `hands_behind_back`, `meditate`
- **Facial Expression:** `neutral`, `angry`, `pain`, `smile`, `cold`, `arrogant`, `contempt`, `wise`, `fierce`, `meditative`
- **Combat & Interactions:** `combat_actions`, `combat_master`, `object_interactions`, `transformations`

---

## 2. Saved Map Presets (Configured Environments)

#### Preset ID: `sakura_lake_village` — Làng Hoa Anh Đào Ven Hồ
- **File**: `maps/presets/sakura_lake_village.json`
- **Description**: Ngôi làng thanh tịnh ven hồ nước, có 2 hàng cây hoa anh đào lớn, một ghế dài đá ngồi ngắm cảnh hướng ra hồ, và vườn thảo dược phía đông.
- **Base Map**: `farming_village` | **Default Sky/Weather**: sunset, Fog: 0.012
- **Named Spawn Points**:
  - `"lakeside_bench"`: [-3.5, 0, -1.8]
  - `"village_entrance"`: [0, 0, 4]
  - `"sakura_tree_north"`: [4, 0, -3]
  - `"herb_garden"`: [0, 0, -5]
- **Placed Objects & Interactables**:
  - `sakura_tree_01` (nature) at [4, 0, -3] — Asset: `props/nature/tree_sakura.glb` (Socket: climb)
  - `stone_bench_01` (furniture) at [-3.5, 0, -1.8] — Asset: `props/furniture/chair_wooden.glb` (Socket: sit)
  - `herb_farm_plot` (nature) at [0, 0, -5] — Asset: `props/tools/farm_plot.glb` (Socket: harvest)
  - `night_lantern_stand` (furniture) at [-2.8, 0, -1.5] — Asset: `props/furniture/lantern_prop.glb`

#### Preset ID: `xianxia_mountain_arena` — Vấn Đỉnh Phong — Đấu Trường Tiên Giới
- **File**: `maps/presets/xianxia_mountain_arena.json`
- **Description**: Đỉnh núi mây mù bao phủ, có các cột đá khắc phù văn cổ xưa xung quanh đài tỷ võ, hướng bắc có tảng đá linh khí tọa thiền.
- **Base Map**: `medieval_fantasy_book` | **Default Sky/Weather**: sunrise, Fog: 0.02
- **Named Spawn Points**:
  - `"challenger_1_west"`: [-4, 0, 0]
  - `"challenger_2_east"`: [4, 0, 0]
  - `"meditation_stone_north"`: [0, 0, -4.5]
  - `"arena_center"`: [0, 0, 0]
- **Placed Objects & Interactables**:
  - `meditation_stone_01` (nature) at [0, 0, -4.5] — Asset: `props/nature/rock_large.glb` (Socket: stand)
  - `ancient_pillar_west` (building) at [-5.5, 0, -2] — Asset: `props/buildings/tower_mage.glb`
  - `ancient_pillar_east` (building) at [5.5, 0, -2] — Asset: `props/buildings/tower_mage.glb`



---

## 3. Available Raw Asset Catalog

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

## 4. Supported Actions & Expressions Reference

### Locomotion & Body Actions (40 Actions)
- **Basic:** `idle`, `walk`, `run`, `sit`, `climb`
- **Advanced Locomotion:** `fly_to`, `dash_to`, `teleport`, `kneel`, `bow`, `meditate`
- **Combat:** `heavy_slash_combo`, `fast_slash`, `magic_blast`, `punch_kick`, `fly_back_knockdown`, `stagger_back`, `block_defend`, `dodge`
- **Xianxia Poses:** `arms_crossed`, `hands_behind_back`, `fist_salute`, `finger_spell`, `power_charge`, `flying_stance`
- **Object Interactions:** `pickup_right`, `carry_two_hands`, `drink`, `pour`, `dig`, `water_plants`, `plant_seed`, `harvest`, `wave`, `dance`, `throw`

### Facial Expressions (21 Expressions)
- **Standard:** `neutral`, `angry`, `pain`, `smile`, `smirk`, `sad`, `serious`, `surprised`, `shock`
- **Xianxia Dramatic:** `cold`, `arrogant`, `contempt`, `wise`, `fierce`, `meditative`, `menacing`, `compassionate`, `determined`
