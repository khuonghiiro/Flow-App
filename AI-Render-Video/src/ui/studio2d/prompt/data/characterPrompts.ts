import { PromptItem } from '../types';

export const CHARACTER_PROMPTS: PromptItem[] = [
  {
    id: 'character_base',
    title: 'Tạo Nhân Vật Gốc Không Face (Faceless Master Character)',
    subtitle: 'Tạo nhân vật gốc mặt trơn (Blank Faceless) để dễ dàng ghép ngũ quan & biểu cảm sau này',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Nhân Vật',
    icon: '👤',
    promptType: 'image',
    tags: ['master', 'character', 'base', 'design', 'chibi', 'xianxia', 'faceless', 'mannequin', '2d'],
    infoNote: '💡 Thiết kế mặt trơn (Blank Faceless): Khuôn mặt hoàn toàn không vẽ mắt, lông mày, mũi, miệng (mặt trơn nhẵn như mannequin anime). Giúp bạn dễ dàng tách và lắp ghép hàng chục layer biểu cảm ngũ quan (cười, khóc, nháy mắt, tức giận...) ở các bước tiếp theo!',
    negativePrompt: `eyes, eyebrows, pupils, eyelashes, mouth, lips, teeth, nose, nostrils, facial expression, drawing on face, detailed face, smile, blush, extra characters, multiple people, background scenery, props held in hands, 3D rendering, realistic textures, photorealism, duplicate characters, wrong number of limbs, deformed proportions, blurry, low quality, text, watermark, outfit too revealing, off-model, inconsistent design`,
    rawPrompt: `MASTER CHARACTER DESIGN — 2D XIANXIA CHIBI (BLANK FACELESS MANNEQUIN)

CRITICAL REQUIREMENT — BLANK FACELESS HEAD (KHÔNG FACE / NO FACE):
- The head MUST have a completely BLANK, SMOOTH, FEATURELESS face surface
- Absolutely NO eyes, NO eyebrows, NO pupils, NO eyelashes
- Absolutely NO nose, NO nostrils
- Absolutely NO mouth, NO lips, NO teeth
- Pure smooth uniform porcelain skin across the entire facial region like a faceless anime mannequin doll
- Ready for modular facial expression and eye layer compositing

CHARACTER CONCEPT:
- Genre: 2D Xianxia/Fantasy anime chibi style
- Body Type: Chibi (oversized head ~40% of body, small compact body, 2.5 - 3 heads tall)
- Age Appearance: [SPECIFY: young teen / young adult / mature adult]
- Gender: [SPECIFY: male / female / non-binary]
- Race/Species: [SPECIFY: human / elf / demon / spirit / other]
- Role: [SPECIFY: warrior / mage / healer / assassin / support / other]

HAIR & HEAD DESIGN:
- Hair style: [SPECIFY: long/short, wavy/straight, specific style name, twin front bangs framing face]
- Hair color: [SPECIFY: primary + secondary highlights if any]
- Skin tone: [SPECIFY: fair porcelain / olive / tan / dark / supernatural tone]
- Ears: Natural human/elf ears visible at sides of hair
- Head accessories: [SPECIFY: hairpin, ribbons, headband, crown or none]
- FACE CANVAS: 100% BLANK FACELESS SMOOTH SKIN SURFACE (NO facial features)

OUTFIT DESIGN:
- Outfit type: [SPECIFY: robes / dress / armor / casual / hybrid]
- Primary colors: [SPECIFY: 2-3 main colors]
- Accent colors: [SPECIFY: 1-2 accent colors]
- Fabric style: [SPECIFY: silk / leather / metal / enchanted / mixed]
- Design elements: [SPECIFY: long sleeves / short sleeves / sleeveless, length, patterns]
- Details: [SPECIFY: ribbons, belts, buckles, embroidery, runes, etc.]

ACCESSORIES:
- Jewelry: [SPECIFY: necklace / earrings / bracelets / rings with descriptions]
- Backpack/Quiver: [SPECIFY: present or not, what kind if present]
- Special items: [SPECIFY: any magical auras or glowing elements]

COLOR PALETTE (critical for consistency):
- Hair: RGB or hex code
- Skin: RGB or hex code
- Outfit Primary: RGB or hex code
- Outfit Secondary: RGB or hex code
- Accents: RGB or hex code

POSE & POSITION:
- Standing naturally at rest
- Front view facing camera (0°)
- One leg slightly forward
- Arms at sides relaxed (empty relaxed hands)
- Full body visible from head to feet
- Centered on canvas

STYLE REQUIREMENTS:
- Pure flat 2D anime/chibi illustration
- Bold clean linework
- Flat cel-shaded coloring
- Minimal shading (only for depth, not realistic)
- NO 2.5D effects
- NO 3D rendering
- NO realistic textures
- NO photorealism

BACKGROUND:
- Solid chroma-key green #00FF00
- No scenery, no props, no shadow

COMPOSITION:
- One character only
- Full body visible (head to feet)
- Centered, plenty of space around
- No text, no watermark
- Clean and professional

NEGATIVE PROMPT: eyes, eyebrows, pupils, eyelashes, mouth, lips, nose, nostrils, facial expression, drawing on face, detailed face, smile, blush, extra characters, multiple people, background scenery, props held in hands, 3D rendering, realistic textures, photorealism, duplicate characters, wrong number of limbs, deformed proportions, blurry, low quality, text, watermark, outfit too revealing, off-model, inconsistent design`,
  },
  {
    id: 'character_hands',
    title: 'Tư Thế Tay (Hand Positions)',
    subtitle: 'Tạo các biến thể tay mở, nắm đấm, cầm vũ khí vô hình để ghép sau này',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Nhân Vật',
    icon: '🤲',
    promptType: 'image',
    tags: ['hands', 'pose', 'grip', 'weapon', 'casting', 'limbs'],
    infoNote: '💡 Sau khi có ảnh nhân vật gốc mặt trơn, tạo ảnh này với tư thế tay chuẩn bị sẵn (cầm vô hình). Sau đó bạn sẽ dễ dàng ghép kiếm, cung, trượng phép vào khớp nối tay.',
    negativePrompt: `eyes, eyebrows, mouth, nose, facial expression, different hair, different outfit, different body, changed proportions, extra characters, added props visible (except as invisible objects), changed skin color, changed hair color, changed outfit colors, 3D rendering, realistic style, photorealism, distorted hands, wrong number of fingers, asymmetrical hand positions`,
    rawPrompt: `CHARACTER WITH HAND POSITIONS — 2D XIANXIA CHIBI (BLANK FACELESS)

CRITICAL: Use the existing character design. Keep the head 100% BLANK FACELESS (NO eyes, NO mouth, NO nose). Only modify HAND POSITIONS.
Everything else stays identical to reference character.

BASE CHARACTER LOCK:
- Face: 100% blank faceless smooth skin (same as reference)
- Hair: exact same style and color
- Skin: exact same tone
- Outfit: exact same design and colors
- Body: exact same proportions and posture
- ONLY change: hand positions and arm angles

HAND POSITION INSTRUCTION:
[SELECT ONE FROM BELOW]

OPTION A - OPEN PALMS (Ready to cast/grab):
- Both hands open, palms visible
- Fingers slightly spread, natural curved position
- Palms facing forward/upward
- Arms extended slightly forward
- Hands at waist to chest height

OPTION B - FISTS CLENCHED (Combat ready):
- Both hands in tight fists
- Thumbs outside fists naturally
- Arms bent at elbows, fists near body
- Posture aggressive but controlled

OPTION C - HOLDING POSITION (Empty hands gripping invisible prop):
- Both hands positioned as if holding/gripping object
- Hands shaped to hold [sword / staff / bow / spell orb]
- Grip width and height specified
- Fingers naturally curved around invisible object

OPTION D - SWORD GRIP POSITION:
- Both hands on invisible sword hilt
- Hands stacked or slightly offset
- Wrists straight, elbows bent
- Sword would be in front of body

OPTION E - BOW DRAW POSITION:
- Left arm extended forward (holding invisible bow)
- Right arm pulled back to ear (drawing invisible string)
- Body angled slightly to side

STYLE REQUIREMENTS:
- Same flat 2D anime chibi style
- Bold clean linework
- Flat cel-shaded colors
- NO 3D, NO realistic rendering

BACKGROUND:
- Solid chroma-key green #00FF00

COMPOSITION:
- Same character, same position, full body visible
- Only hands and arms modified`,
  },
  {
    id: 'angle0',
    title: '0° - Chính Diện (Front View)',
    subtitle: 'Khóa nhận diện nhân vật góc nhìn thẳng 0° (Mặt trơn không face)',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Góc Nhìn',
    icon: '🎯',
    promptType: 'image',
    tags: ['angle', '0deg', 'front', 'turnaround', 'faceless'],
    infoNote: '💡 Góc trực diện 0° chuẩn để làm avatar, mặt đứng và các hoạt ảnh đi lại đối diện camera.',
    negativePrompt: `eyes, eyebrows, mouth, nose, facial expression, 2.5D, 3D CGI, realistic rendering, side view, 3/4 view, rear view, dramatic pose, weapon, handheld object, extra characters, scenery, shadow, gradient, text, watermark`,
    rawPrompt: `MASTER CHARACTER — 2D XIANXIA CHIBI — 0° FRONT VIEW (BLANK FACELESS)

Use the reference character design as ABSOLUTE SOURCE OF TRUTH.

IDENTITY LOCK — keep exactly: BLANK FACELESS smooth head (no eyes, no nose, no mouth, no eyebrows), hairstyle, hair color,
skin tone, chibi body proportions, costume, colors, accessories, jewelry,
silhouette. Do NOT redesign any element.

POSE: Character body and face facing directly toward camera (0°). Natural
standing/ready pose, front view. One leg slightly forward, torso upright,
shoulders relaxed, arms at sides relaxed.

HANDS: Both hands completely empty and relaxed, no weapon/prop/object.

STYLE: Pure flat 2D anime/Guoman chibi illustration. Clean bold linework,
flat cel-shaded coloring, minimal shading. NO 2.5D, NO 3D/CGI,
NO realistic rendering.

BACKGROUND: Flat chroma-key green #00FF00, pure and uniform.

COMPOSITION: One character, full body (head to feet fully visible),
centered, empty space around edges, no text/watermark.`,
  },
  {
    id: 'angle45',
    title: '45° - Xoay Trái (3/4 Left View)',
    subtitle: 'Xoay nhân vật một góc 45° sang bên trái (Mặt trơn không face)',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Góc Nhìn',
    icon: '↖️',
    promptType: 'image',
    tags: ['angle', '45deg', 'three_quarter', 'turnaround', 'faceless'],
    infoNote: '💡 Góc 45° là góc đẹp nhất cho game 2.5D, animation hội thoại và di chuyển phối cảnh.',
    negativePrompt: `eyes, eyebrows, mouth, nose, facial expression, front view, full side profile, rear view, dramatic pose, weapon, handheld object, extra characters, scenery, shadow, gradient, text, watermark, rotated wrong direction`,
    rawPrompt: `MASTER CHARACTER — 2D XIANXIA CHIBI — 45° LEFT ROTATION (BLANK FACELESS)

Use reference character design. Recreate in 45° left three-quarter view.

IDENTITY LOCK — keep exactly: BLANK FACELESS smooth head (no eyes, no nose, no mouth), hairstyle, hair color, skin tone,
body proportions, costume, colors, accessories, jewelry, silhouette.

POSE: Character body and face rotated 45 degrees to the left. Three-quarter
left view. One leg slightly forward, arms relaxed at sides. Same ready posture.

HANDS: Both hands empty and relaxed, no weapon/prop/object.

STYLE: Pure flat 2D anime/Guoman chibi. Bold linework, flat colors,
minimal shading. NO 2.5D, NO 3D, NO realistic rendering.

BACKGROUND: Flat chroma-key green #00FF00.

COMPOSITION: One character, full body, centered, empty space, no text/watermark.`,
  },
  {
    id: 'angle90',
    title: '90° - Side Profile (Ngang Trái)',
    subtitle: 'Nhìn nghiêng hoàn toàn 90° sang trái (Mặt trơn không face)',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Góc Nhìn',
    icon: '⬅️',
    promptType: 'image',
    tags: ['angle', '90deg', 'side', 'profile', 'turnaround', 'faceless'],
    infoNote: '💡 Dành cho game đi cảnh màn hình ngang (Side-scroller) hoặc cắt sprite chuyển động chân.',
    negativePrompt: `eyes, eyebrows, mouth, nose, facial features, front view, 3/4 view, rear view, dramatic pose, weapon, handheld object, extra characters, scenery, shadow, gradient, text, watermark, wrong angle`,
    rawPrompt: `MASTER CHARACTER — 2D XIANXIA CHIBI — 90° LEFT SIDE VIEW (BLANK FACELESS)

Use reference character design. Recreate in 90° full left side profile.

IDENTITY LOCK — keep exactly: BLANK FACELESS smooth head profile (no facial features), hairstyle, hair color, skin tone,
body proportions, costume, colors, accessories, jewelry, silhouette.

POSE: Character body rotated 90 degrees to the left. Full side profile,
left side facing camera. One leg slightly forward, arms relaxed at sides.

HANDS: Both hands empty and relaxed, no weapon/prop/object.

STYLE: Pure flat 2D anime/Guoman chibi. Bold linework, flat colors,
minimal shading. NO 2.5D, NO 3D, NO realistic rendering.

BACKGROUND: Flat chroma-key green #00FF00.

COMPOSITION: One character, full body, centered, empty space, no text/watermark.`,
  },
  {
    id: 'angle135',
    title: '135° - Lưng Lệch Trái (Back-Left View)',
    subtitle: 'Góc nhìn 3/4 từ phía sau lưng nhìn chếch sang trái',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Góc Nhìn',
    icon: '↙️',
    promptType: 'image',
    tags: ['angle', '135deg', 'back_left', 'turnaround'],
    infoNote: '💡 Giúp hoàn thiện hệ thống Turnaround 360° 8 hướng cho chuyển động xoay người mượt mà.',
    negativePrompt: `180 degree back view, rotated wrong direction, side profile, front view, tilted body, twisted spine, off-center, not properly rotated`,
    rawPrompt: `MASTER CHARACTER — 2D XIANXIA CHIBI — 135° BACK-LEFT ROTATION

You have a 180° back-view reference image.

TASK: Rotate this reference pose 45° to the LEFT to create 135° back-left
view. Keep ALL details from 180° reference (hair, outfit, colors, accessories,
back details) — only rotate 45° left.

At 135°, you should show:
- Back three-quarter view (mostly back, slightly left side visible)
- Left shoulder/back-left side visible compared to 180°
- Same hairstyle back details from reference
- Same outfit back details from reference
- Same accessories arrangement from reference
- Only the angle changes (45° rotation left), design stays identical

POSE: Standing with back mostly to camera, slightly rotated left. One leg
slightly forward. Torso upright, arms relaxed at sides.

HANDS: Both hands empty and relaxed, no weapon/prop/object.

STYLE: Pure flat 2D anime/Guoman chibi. Bold linework, flat colors,
minimal shading.

BACKGROUND: Flat chroma-key green #00FF00.

COMPOSITION: One character, full body, centered, empty space, no text/watermark.`,
  },
  {
    id: 'angle180',
    title: '180° - Sau Lưng (Back View)',
    subtitle: 'Nhìn trực diện từ phía sau lưng 180°',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Góc Nhìn',
    icon: '🔄',
    promptType: 'image',
    tags: ['angle', '180deg', 'back', 'turnaround'],
    infoNote: '💡 Thể hiện chi tiết tóc sau lưng, thắt lưng, tà áo sau và phụ kiện đeo lưng.',
    negativePrompt: `front view, side view, 3/4 view, angled body, rotated body, twisted body, tilted body, leaning body, shoulder tilt, head tilt, asymmetrical, off-center, face visible, not back view, body facing sideways`,
    rawPrompt: `MASTER CHARACTER — 2D XIANXIA CHIBI — 180° BACK VIEW

Use the 0° front-view reference to understand character design.
NOW recreate this character in perfect 180° back view (lưng).

CHARACTER DESIGN FROM 0° REFERENCE:
- Hairstyle: [match front reference]
- Hair color: [exact color from reference]
- Skin tone: [exact tone from reference]
- Body proportions: [exact chibi proportions]
- Costume: [exact same outfit, same colors]
- Accessories: [exact same items, same arrangement]

CREATE 180° BACK VIEW:
- Character stands with back to camera (lưng đối mặt camera)
- Back of hairstyle visible (how hair looks from behind)
- Back of outfit visible (how outfit looks from behind)
- Back of accessories visible from behind
- Draw what the front reference character looks like from 180° angle

POSE: Standing naturally with back view. One leg slightly forward. Torso
upright, shoulders relaxed, arms at sides.

BODY ALIGNMENT:
- Spine perfectly vertical, centered on screen
- Shoulders perfectly level (no tilt, no rotation)
- Head centered above spine (no tilt, no turn)
- Body perfectly symmetric left-right
- Completely upright, no lean, no angle

HANDS: Both hands empty, no weapon/prop/object.

STYLE: Pure flat 2D anime/Guoman chibi. Bold linework, flat colors,
minimal shading. NO 2.5D, NO 3D, NO realistic rendering.

BACKGROUND: Flat chroma-key green #00FF00.

COMPOSITION: One character, full body, centered, empty space, no text/watermark.`,
  },
];
