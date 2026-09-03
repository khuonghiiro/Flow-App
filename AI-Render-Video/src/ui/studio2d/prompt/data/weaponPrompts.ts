import { PromptItem } from '../types';

export const WEAPON_PROMPTS: PromptItem[] = [
  {
    id: 'weapon_sword',
    title: 'Vũ Khí: Kiếm (Sword Attachment)',
    subtitle: 'Gắn kiếm tiên/katana/trọng kiếm vào tay nhân vật trên nền xanh',
    stepCategory: 'step5_weapons',
    stepLabel: 'Bước 5: Vũ Khí',
    icon: '🗡️',
    promptType: 'attachment',
    tags: ['weapon', 'sword', 'blade', 'katana', 'prop', 'xianxia'],
    infoNote: '💡 Sử dụng ảnh nhân vật ở tư thế tay cầm kiếm (ở Bước 1) và prompt này để AI vẽ kiếm khớp chuẩn xác vào lòng bàn tay.',
    negativePrompt: `multiple weapons, weapon floating disconnected from hands, 3D CGI weapon, photorealistic texture, changed character outfit, blurry hilt`,
    rawPrompt: `WEAPON ATTACHMENT — ADD SWORD TO CHARACTER

CRITICAL: Use character's hand-position reference image (with hands posed
for holding objects). ADD SWORD ONLY — do not change character design.

CHARACTER LOCK: Keep face, hairstyle, hair color, skin tone, costume,
colors, accessories EXACTLY as in reference. Only add sword.

SWORD SPECIFICATIONS:
- Type: [CHOOSE: katana / flying sword / longsword / claymore / curved dao]
- Length: proportional to chibi character (~half to full body height)
- Blade color: [specify: silver steel / glowing cyan jade / dark obsidian / gold]
- Blade style: sleek sharp edge with subtle glowing runic inscription
- Hilt & Guard: ornate daoist guard with flowing silk tassel at pommel

POSITIONING:
- Held securely in character's gripped hands
- Blade angled dynamically across body or raised at ready position
- Perfectly aligned with hand sockets

STYLE: Same flat 2D anime/chibi cel-shaded art style.

BACKGROUND: Pure flat chroma-key green #00FF00.`,
  },
  {
    id: 'weapon_staff',
    title: 'Vũ Khí: Gậy Phép (Magical Staff)',
    subtitle: 'Gắn trượng phép, gậy ngọc, pháp bảo nguyên tố',
    stepCategory: 'step5_weapons',
    stepLabel: 'Bước 5: Vũ Khí',
    icon: '🔱',
    promptType: 'attachment',
    tags: ['weapon', 'staff', 'wand', 'magic', 'mage', 'crystal'],
    infoNote: '💡 Trượng phép có đỉnh ngọc phát sáng linh lực, tạo điểm nhấn ấn tượng cho nhân vật pháp sư.',
    negativePrompt: `wrong weapon type, sword instead of staff, detached staff, photorealistic wood, altered character face`,
    rawPrompt: `WEAPON ATTACHMENT — ADD MAGICAL STAFF TO CHARACTER

CRITICAL: Use character's hand-position reference image. ADD STAFF ONLY.

CHARACTER LOCK: All character traits remain 100% identical.

STAFF SPECIFICATIONS:
- Length: tall staff, roughly 1.3 - 1.5x character height
- Shaft: carved white jade / polished dark spirit wood with silver inlay
- Headpiece: floating glowing crystal orb encircled by ornate golden lotus crescent
- Magical aura: soft luminous particles hovering around the top crystal

POSITIONING:
- Held firmly in character's hand at upper third of staff
- Base rested near ground or hovering slightly

STYLE: Pure flat 2D anime/chibi cel-shaded aesthetic.

BACKGROUND: Flat chroma-key green #00FF00.`,
  },
  {
    id: 'weapon_bow',
    title: 'Vũ Khí: Cung Tên (Bow & Arrow)',
    subtitle: 'Gắn cung tiên, dây cung phát sáng và tư thế giương cung',
    stepCategory: 'step5_weapons',
    stepLabel: 'Bước 5: Vũ Khí',
    icon: '🏹',
    promptType: 'attachment',
    tags: ['weapon', 'bow', 'arrow', 'archer', 'quiver'],
    infoNote: '💡 Sử dụng ảnh nhân vật ở tư thế kéo cung để có góc ngắm bắn hoàn hảo.',
    negativePrompt: `multiple bows, broken string, arrow detached, realistic textures, changed clothes`,
    rawPrompt: `WEAPON ATTACHMENT — ADD BOW & ARROW TO CHARACTER

CRITICAL: Use character in bow-draw pose reference. ADD BOW ONLY.

BOW SPECIFICATIONS:
- Bow type: elegant recurve spirit bow with wings motif
- Material: enchanted luminous crystal and flexible spirit wood
- String: thin glowing line of condensed spirit energy
- Arrow: energy arrow nocked on string with glowing arrowhead
- Optional: compact stylized quiver on back

POSITIONING:
- Left hand holds bow grip firmly extended
- Right hand draws string back to cheek level

STYLE: Flat 2D anime cel-shaded linework.

BACKGROUND: Flat chroma-key green #00FF00.`,
  },
  {
    id: 'weapon_spell',
    title: 'Phát Động Phép (Spell & Magic Effects)',
    subtitle: 'Gắn hiệu ứng chưởng lực: Lửa, Băng tuyết, Sấm sét, Thánh quang, Hắc ám',
    stepCategory: 'step5_weapons',
    stepLabel: 'Bước 5: Vũ Khí & Phép',
    icon: '✨',
    promptType: 'attachment',
    tags: ['spell', 'magic', 'fireball', 'ice', 'lightning', 'energy', 'vfx'],
    infoNote: '💡 Chọn 1 trong 5 hệ nguyên tố bên dưới để thêm luồng linh lực phát sáng rực rỡ xung quanh tay nhân vật.',
    negativePrompt: `photorealistic smoke, messy noise, 3D CGI volume, obscured character face, multiple elements clashing`,
    rawPrompt: `MAGICAL EFFECT ATTACHMENT — ADD SPELL CASTING EFFECT

CRITICAL: Use character in casting pose. ADD MAGICAL FX ONLY.

SELECT ONE SPELL ELEMENT:

1. ICE & CYAN FROST:
- Crystalline ice shards and glowing frost mist swirling between open palms
- Color: Bright cyan (#00E5FF) and frosty white
- Effect: Sharp 2D stylized ice spikes and drifting sparkle dust

2. FIREBALL / SOLAR FLAME:
- Blazing spherical energy orb radiating heat ripples and trailing ember sparks
- Color: Radiant orange-gold and crimson red
- Effect: Stylized 2D anime flames circling hands

3. LIGHTNING / PLASMA BOLTS:
- Crackling electric arcs dancing across fingers and forearms
- Color: Bright violet-purple and electric white
- Effect: Sharp jagged lightning bolts with radiant corona

4. RADIANT HOLY LIGHT:
- Sacred glowing magic circle with celestial runes orbiting hands
- Color: Pure radiant gold and warm white

5. DARK SHADOW ENERGY:
- Swirling shadowy wisps and dark matter energy tentacles
- Color: Deep violet, obsidian black, and crimson aura

STYLE: Crisp 2D anime VFX with glowing cel-shaded contours.

BACKGROUND: Flat chroma-key green #00FF00.`,
  },
];
