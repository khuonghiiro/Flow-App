# ASSET CATALOG — AI 3D Animation Studio

> **FOR AI AGENTS:** This file is the single source of truth for all available scene resources. Read this file carefully before generating JSON `MasterSceneConfig`.
> **Language Rule:** AI agents must read `ASSET_CATALOG.md` (English). Do not rely on `_VI.md` files which are formatted for human users.
> **Auto-generated:** 2026-08-20 14:50:50
> **Total assets:** 43 asset files (229.7 MB), 0 saved map presets

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

*No saved map presets found.*


---

## 3. Available Raw Asset Catalog

### Characters — Male (Nam)
| ID | Path | Format | Size | Ref Image |
|:---|:---|:---|---:|:---|
| `precision_strike_manekin` | `characters/man/precision_strike_manekin.glb` | GLB | 1.15 MB | `assets/characters/man/precision_strike_manekin.png` |
| `sample_avatar` | `characters/male/sample_avatar.vrm` | VRM | 10.28 MB | `assets/characters/male/sample_avatar.png` |



### Characters — Female (Nữ)
| ID | Path | Format | Size | Ref Image |
|:---|:---|:---|---:|:---|
| `tzitzimitl_female` | `characters/woman/tzitzimitl_female.glb` | GLB | 3.62 MB | `assets/characters/woman/tzitzimitl_female.png` |



### Characters — Base Bodies
| ID | Path | Format | Size | Ref Image |
|:---|:---|:---|---:|:---|
| `body_base_-_manekina` | `characters/base_bodies/male/body_base_-_manekina.glb` | GLB | 0.53 MB | `assets/characters/base_bodies/male/body_base_-_manekina.png` |
| `body_base_-_manekin` | `characters/base_bodies/man/body_base_-_manekin.glb` | GLB | 0.54 MB | `assets/characters/base_bodies/man/body_base_-_manekin.png` |
| `sample_avatar` | `characters/base_bodies/sample_avatar.vrm` | VRM | 10.28 MB | — |
| `sample_avatar` | `characters/sample_avatar.vrm` | VRM | 10.28 MB | — |



### Characters — Faces
| ID | Path | Format | Size | Ref Image |
|:---|:---|:---|---:|:---|
| `starlight_fragments_-_manekin` | `characters/faces/male/starlight_fragments_-_manekin.glb` | GLB | 0.19 MB | `assets/characters/faces/male/starlight_fragments_-_manekin.png` |
| `dawnbreaker_-_manekin` | `characters/faces/man/dawnbreaker_-_manekin.glb` | GLB | 0.20 MB | `assets/characters/faces/man/dawnbreaker_-_manekin.png` |



### Characters — Hairstyles
*No hairstyle assets found. Add .glb to characters/hairstyles/*


### Characters — Beards
*No beard assets found. Add .glb to characters/beards/*


### Characters — Costumes
| ID | Path | Format | Size | Ref Image |
|:---|:---|:---|---:|:---|
| `amber_nectar_-_manekina` | `characters/costumes/male/amber_nectar_-_manekina.glb` | GLB | 3.37 MB | `assets/characters/costumes/male/amber_nectar_-_manekina.png` |
| `precision_strike_-_manekina` | `characters/costumes/male/precision_strike_-_manekina.glb` | GLB | 0.99 MB | `assets/characters/costumes/male/precision_strike_-_manekina.png` |
| `scary_cat_-_manekina` | `characters/costumes/male/scary_cat_-_manekina.glb` | GLB | 1.37 MB | — |
| `amber_nectar_-_manekin` | `characters/costumes/man/amber_nectar_-_manekin.glb` | GLB | 3.43 MB | `assets/characters/costumes/man/amber_nectar_-_manekin.png` |
| `precision_strike_-_manekin` | `characters/costumes/man/precision_strike_-_manekin.glb` | GLB | 1.15 MB | `assets/characters/costumes/man/precision_strike_-_manekin.png` |
| `scary_cat_-_manekin` | `characters/costumes/man/scary_cat_-_manekin.glb` | GLB | 0.90 MB | `assets/characters/costumes/man/scary_cat_-_manekin.png` |
| `sleuths_verdict_-_manekin` | `characters/costumes/man/sleuths_verdict_-_manekin.glb` | GLB | 3.19 MB | — |



### Characters — Accessories
*No accessory assets found. Add .glb to characters/accessories/*


### SkyBoxs 360° Panoramas
| ID | Path | Format | Size | Ref Image |
|:---|:---|:---|---:|:---|
| `binh_minh_it_may_1` | `SkyBoxs/binh_minh/it_may/binh_minh_it_may_1.png` | PNG | 0.86 MB | `assets/SkyBoxs/binh_minh/it_may/binh_minh_it_may_1.png` |
| `binh_minh_khong_may_1` | `SkyBoxs/binh_minh/khong_may/binh_minh_khong_may_1.png` | PNG | 0.86 MB | `assets/SkyBoxs/binh_minh/khong_may/binh_minh_khong_may_1.png` |
| `binh_minh_nhieu_may_1` | `SkyBoxs/binh_minh/nhieu_may/binh_minh_nhieu_may_1.png` | PNG | 0.86 MB | `assets/SkyBoxs/binh_minh/nhieu_may/binh_minh_nhieu_may_1.png` |
| `buoi_chieu_it_may_1` | `SkyBoxs/buoi_chieu/it_may/buoi_chieu_it_may_1.png` | PNG | 0.86 MB | `assets/SkyBoxs/buoi_chieu/it_may/buoi_chieu_it_may_1.png` |
| `buoi_chieu_khong_may_1` | `SkyBoxs/buoi_chieu/khong_may/buoi_chieu_khong_may_1.png` | PNG | 0.86 MB | `assets/SkyBoxs/buoi_chieu/khong_may/buoi_chieu_khong_may_1.png` |
| `buoi_chieu_nhieu_may_1` | `SkyBoxs/buoi_chieu/nhieu_may/buoi_chieu_nhieu_may_1.png` | PNG | 1.35 MB | `assets/SkyBoxs/buoi_chieu/nhieu_may/buoi_chieu_nhieu_may_1.png` |
| `buoi_sang_it_may_1` | `SkyBoxs/buoi_sang/it_may/buoi_sang_it_may_1.png` | PNG | 0.86 MB | `assets/SkyBoxs/buoi_sang/it_may/buoi_sang_it_may_1.png` |
| `buoi_sang_khong_may_1` | `SkyBoxs/buoi_sang/khong_may/buoi_sang_khong_may_1.png` | PNG | 1.00 MB | `assets/SkyBoxs/buoi_sang/khong_may/buoi_sang_khong_may_1.png` |
| `buoi_sang_nhieu_may_1` | `SkyBoxs/buoi_sang/nhieu_may/buoi_sang_nhieu_may_1.png` | PNG | 1.00 MB | `assets/SkyBoxs/buoi_sang/nhieu_may/buoi_sang_nhieu_may_1.png` |
| `buoi_toi_it_may_1` | `SkyBoxs/buoi_toi/it_may/buoi_toi_it_may_1.png` | PNG | 0.27 MB | `assets/SkyBoxs/buoi_toi/it_may/buoi_toi_it_may_1.png` |
| `buoi_toi_khong_may_1` | `SkyBoxs/buoi_toi/khong_may/buoi_toi_khong_may_1.png` | PNG | 1.05 MB | `assets/SkyBoxs/buoi_toi/khong_may/buoi_toi_khong_may_1.png` |
| `buoi_toi_nhieu_may_1` | `SkyBoxs/buoi_toi/nhieu_may/buoi_toi_nhieu_may_1.png` | PNG | 1.05 MB | `assets/SkyBoxs/buoi_toi/nhieu_may/buoi_toi_nhieu_may_1.png` |
| `buoi_trua_it_may_1` | `SkyBoxs/buoi_trua/it_may/buoi_trua_it_may_1.png` | PNG | 1.00 MB | `assets/SkyBoxs/buoi_trua/it_may/buoi_trua_it_may_1.png` |
| `buoi_trua_khong_may_1` | `SkyBoxs/buoi_trua/khong_may/buoi_trua_khong_may_1.png` | PNG | 1.00 MB | `assets/SkyBoxs/buoi_trua/khong_may/buoi_trua_khong_may_1.png` |
| `buoi_trua_nhieu_may_1` | `SkyBoxs/buoi_trua/nhieu_may/buoi_trua_nhieu_may_1.png` | PNG | 1.00 MB | `assets/SkyBoxs/buoi_trua/nhieu_may/buoi_trua_nhieu_may_1.png` |
| `giong_bao_it_may_1` | `SkyBoxs/giong_bao/it_may/giong_bao_it_may_1.png` | PNG | 1.35 MB | `assets/SkyBoxs/giong_bao/it_may/giong_bao_it_may_1.png` |
| `giong_bao_nhieu_may_1` | `SkyBoxs/giong_bao/nhieu_may/giong_bao_nhieu_may_1.png` | PNG | 1.35 MB | `assets/SkyBoxs/giong_bao/nhieu_may/giong_bao_nhieu_may_1.png` |
| `skybox-alien` | `SkyBoxs/skybox-alien.png` | PNG | 1.35 MB | `assets/SkyBoxs/skybox-alien.png` |
| `skybox-day` | `SkyBoxs/skybox-day.png` | PNG | 1.00 MB | `assets/SkyBoxs/skybox-day.png` |
| `skybox-morning` | `SkyBoxs/skybox-morning.png` | PNG | 0.86 MB | `assets/SkyBoxs/skybox-morning.png` |
| `skybox-night` | `SkyBoxs/skybox-night.png` | PNG | 1.05 MB | `assets/SkyBoxs/skybox-night.png` |
| `skybox-space` | `SkyBoxs/skybox-space.png` | PNG | 0.27 MB | `assets/SkyBoxs/skybox-space.png` |



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
| ID | Path | Format | Size | Ref Image |
|:---|:---|:---|---:|:---|
| `duck_prop` | `props/duck_prop.glb` | GLB | 0.11 MB | — |
| `lantern_prop` | `props/lantern_prop.glb` | GLB | 9.42 MB | — |



### Maps — Environments
| ID | Path | Format | Size | Ref Image |
|:---|:---|:---|---:|:---|
| `cathedral` | `maps/cathedral.glb` | GLB | 103.80 MB | — |
| `game_pirate_adventure_map` | `maps/game_pirate_adventure_map.glb` | GLB | 7.48 MB | — |
| `zone9_real_light` | `maps/zone9_real_light.glb` | GLB | 36.33 MB | — |



### Audio — Background Music (BGM)
*No BGM audio found. Add .mp3/.wav to audio/bgm/*


### Audio — Combat SFX
*No combat SFX found. Add .mp3 to audio/sfx/combat/*


### Audio — Interaction SFX
*No interaction SFX found. Add .mp3 to audio/sfx/interaction/*


### Audio — Ambient SFX
*No ambient SFX found. Add .mp3 to audio/sfx/ambient/*


### Animations — Combat
*Procedural combat animation system active.*


### Animations — Interaction
*Procedural interaction animation system active.*


### Animations — Xianxia
*XianxiaPoseLibrary 13 poses active.*


### Animations — Locomotion
*Procedural locomotion active.*


### VFX — Visual Effects
*Internal particle shader VFX active.*


---

## 4. Supported Actions & Expressions Reference

### Body Actions (40 Actions)
- **Locomotion:** `idle`, `walk`, `run`, `sit`, `climb`
- **Special:** `fly_to`, `dash_to`, `teleport`, `kneel`, `bow`, `meditate`
- **Combat:** `heavy_slash_combo`, `fast_slash`, `magic_blast`, `punch_kick`, `fly_back_knockdown`, `stagger_back`, `block_defend`, `dodge`
- **Xianxia Poses:** `arms_crossed`, `hands_behind_back`, `fist_salute`, `finger_spell`, `power_charge`, `flying_stance`
- **Life Interactions:** `pickup_right`, `carry_two_hands`, `drink`, `pour`, `dig`, `water_plants`, `plant_seed`, `harvest`, `wave`, `dance`, `throw`

### Facial Expressions (21 Expressions)
- **Standard:** `neutral`, `angry`, `pain`, `smile`, `smirk`, `sad`, `serious`, `surprised`, `shock`
- **Xianxia Dramatic:** `cold`, `arrogant`, `contempt`, `wise`, `fierce`, `meditative`, `menacing`, `compassionate`, `determined`
