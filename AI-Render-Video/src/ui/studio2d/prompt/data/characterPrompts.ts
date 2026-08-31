import { PromptItem } from '../types';

export const CHARACTER_PROMPTS: PromptItem[] = [
  {
    id: 'character_base',
    title: 'Tạo Nhân Vật Gốc Không Face - Chính Diện 0° (Faceless Master Front View)',
    subtitle: 'Tạo nhân vật gốc mặt trơn (Blank Faceless) góc trực diện 0° chính diện để dễ dàng ghép ngũ quan & biểu cảm sau này',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Nhân Vật',
    icon: '👤',
    promptType: 'image',
    tags: ['master', 'character', 'base', 'design', 'chibi', 'xianxia', 'faceless', 'mannequin', 'front', '0deg', '2d'],
    infoNote: '💡 Thiết kế mặt trơn chính diện (Blank Faceless 0° Front View): Nhân vật đứng thẳng góc 0° trực diện đối mặt camera, khuôn mặt hoàn toàn không vẽ mắt, mũi, miệng (mặt trơn nhẵn như mannequin anime). Giúp bạn dễ dàng tách và lắp ghép hàng chục layer biểu cảm ngũ quan (cười, khóc, nháy mắt, tức giận...) ở các bước tiếp theo!',
    negativePrompt: `eyes, eyebrows, pupils, eyelashes, mouth, lips, teeth, nose, nostrils, facial expression, drawing on face, detailed face, smile, blush, three-quarter view, 3/4 view, side view, back view, profile view, turned body, tilted head, angled view, rotated body, extra characters, multiple people, background scenery, props held in hands, 3D rendering, realistic textures, photorealism, duplicate characters, wrong number of limbs, deformed proportions, blurry, low quality, text, watermark, outfit too revealing, off-model, inconsistent design`,
    rawPrompt: `MASTER CHARACTER DESIGN — 2D XIANXIA CHIBI — 0° DIRECT FRONT VIEW (BLANK FACELESS MANNEQUIN)

DEFAULT PERSPECTIVE & POSE (MẶC ĐỊNH CHÍNH DIỆN 0° TRỰC DIỆN):
- Character MUST be facing DIRECTLY forward at camera (0° strict front view)
- Head facing 100% straight forward towards viewer (NO head turn, NO head tilt, NO 3/4 angle)
- Symmetrical standing pose, torso upright, shoulders level, both arms relaxed at sides
- Both legs straight facing forward, full body visible from top of head to feet
- Centered on canvas with balanced symmetrical proportions

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
- Ears: Symmetrical natural ears visible at sides of hair
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

NEGATIVE PROMPT: eyes, eyebrows, pupils, eyelashes, mouth, lips, nose, nostrils, facial expression, drawing on face, detailed face, smile, blush, three-quarter view, 3/4 view, side view, back view, profile view, turned body, tilted head, angled view, rotated body, extra characters, multiple people, background scenery, props held in hands, 3D rendering, realistic textures, photorealism, duplicate characters, wrong number of limbs, deformed proportions, blurry, low quality, text, watermark, outfit too revealing, off-model, inconsistent design`,
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
    title: '0° - Chính Diện (0° Front View)',
    subtitle: 'Khóa toàn bộ đầu, tóc, thân, tay, chân, mũi chân thẳng trục 0° đối diện camera',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Góc Nhìn',
    icon: '🎯',
    promptType: 'image',
    tags: ['angle', '0deg', 'front', 'turnaround', 'faceless'],
    infoNote: '💡 Góc trực diện 0°: Toàn bộ đầu, thân, hai chân và hai mũi chân đều hướng thẳng 100% về phía camera.',
    negativePrompt: `eyes, eyebrows, mouth, nose, facial expression, 2.5D, 3D CGI, realistic rendering, side view, 3/4 view, rear view, turned feet, angled toes, tilted head, dramatic pose, weapon, handheld object, extra characters, scenery, shadow, gradient, text, watermark`,
    rawPrompt: `MASTER CHARACTER — 2D XIANXIA CHIBI — 0° DIRECT FRONT VIEW (BLANK FACELESS)

CRITICAL ANATOMICAL ALIGNMENT (ĐỒNG BỘ TOÀN BỘ ĐẦU, TÓC, THÂN, CHÂN, MŨI CHÂN 0° CHÍNH DIỆN):
- HEAD & FACE: Facing 100% directly towards camera. Smooth blank faceless porcelain head (NO eyes, NO nose, NO mouth).
- HAIR & BANGS: Symmetrical front bangs and side tufts framing face, hair accessories balanced left-right.
- TORSO & SHOULDERS: Symmetrical upright standing torso, shoulders perfectly level facing camera.
- ARMS & HANDS: Both arms relaxed at sides, empty relaxed hands.
- LEGS, BOOTS & TOES (CHÂN & MŨI CHÂN): Both legs straight and upright facing forward. Both shoes/boots and toes POINT DIRECTLY AT CAMERA. (NO angled feet).

IDENTITY LOCK — keep exactly from master design:
- Blank faceless head shape, hairstyle, hair color, skin tone, chibi proportions, costume, colors, accessories, silhouette.

STYLE: Pure flat 2D anime/Guoman chibi illustration. Bold clean linework, flat cel-shaded coloring, minimal shading. NO 2.5D, NO 3D/CGI, NO realistic rendering.

BACKGROUND: Flat chroma-key green #00FF00, pure and uniform.

COMPOSITION: One character, full body (head to feet fully visible), centered, plenty of space around edges, no text/watermark.`,
  },
  {
    id: 'angle45',
    title: '45° - Xoay Trái (Deep 3/4 Left View - Chuẩn 45 Độ)',
    subtitle: 'Xoay sâu đúng chuẩn 45° (nằm chính giữa góc 0° và 90°), loại bỏ góc xoay nông 20°-30°',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Góc Nhìn',
    icon: '↖️',
    promptType: 'image',
    tags: ['angle', '45deg', 'three_quarter', 'turnaround', 'faceless', 'left', 'deep_turn'],
    infoNote: '💡 Chuẩn 45° Góc Sâu (Deep 3/4 View): Nhân vật xoay sâu đúng góc 45° (nằm chính giữa 0° và 90°), toàn bộ thân, ngực, vai và hai chân đều quay chéo 45° sang trái, không bị hiện tượng xoay nhẹ 20°-30° như trước.',
    negativePrompt: `shallow angle, slight turn, subtle turn, weak rotation, 10 degree angle, 15 degree angle, 20 degree angle, 25 degree angle, 30 degree angle, almost front view, mostly front view, front facing chest, barely turned head, front-facing feet, feet pointing forward, forward-facing toes, full side profile 90deg, rear view, rotated to right side, facing right, twisted body, twisted spine, mixed perspective, eyes, eyebrows, mouth, nose, facial expression, drawing on face, 3D render, realistic textures, low quality`,
    rawPrompt: `MASTER CHARACTER — 2D XIANXIA CHIBI — DEEP 45° THREE-QUARTER LEFT VIEW (PRONOUNCED 45-DEGREE ISOMETRIC PERSPECTIVE)

CRITICAL ANGLE REQUIREMENT — DEEP 45° ROTATION (XOAY SÂU ĐÚNG CHUẨN 45 ĐỘ):
- The angle MUST be a PRONOUNCED, DEEP 45° ISOMETRIC VIEW — EXACTLY HALFWAY BETWEEN 0° FRONT AND 90° SIDE PROFILE
- NOT a slight head turn, NOT 20 degrees, NOT 30 degrees, NOT a shallow twist
- The entire body plane (head, chest, waist, and hips) is rotated a full 45 degrees towards the left edge

ANATOMICAL ALIGNMENT (ĐỒNG BỘ TOÀN BỘ CƠ THỂ THEO GÓC 45° SÂU):
- HEAD & FACE: Head is turned a full 45 degrees left. Clear 3/4 contour showing faceless cheekbone and jawline pointing left (NO eyes, NO nose, NO mouth).
- HAIR & BANGS: Left side of hair/bangs is prominently in the foreground; right side of hair is receded in depth behind the head.
- TORSO & SHOULDERS: Chest plane is angled a full 45° to the left. Left shoulder is in the front foreground, right shoulder is distinctly set back in depth.
- HIPS & SASH: Waist, belt, and sash knot are viewed at 45° diagonal angle.
- LEGS, BOOTS & TOES (CHÂN & MŨI CHÂN CHÉO 45°): Both legs and boots MUST BE IN A SOLID 45° DIAGONAL STANCE pointing towards the bottom-left. Left foot is placed forward pointing 45° left; right foot is placed slightly behind pointing 45° left. (ABSOLUTELY NO feet facing camera, NO forward toes).
- ARMS & HANDS: Left arm visible in front-left, right arm partially occluded behind torso. Empty hands.

IDENTITY LOCK — keep exactly from 0° reference:
- Blank faceless head shape, hairstyle, hair color, skin tone
- Chibi body proportions, costume design, exact colors, jewelry, accessories, silhouette
- Do NOT redesign or alter any item

STYLE: Pure flat 2D anime/Guoman chibi illustration. Bold linework, flat cel-shaded colors, minimal shading. NO 2.5D, NO 3D, NO realistic rendering.

BACKGROUND: Flat chroma-key green #00FF00.

COMPOSITION: One character, full body (head to feet visible), centered, plenty of space around, no text/watermark.`,
  },
  {
    id: 'angle90',
    title: '90° - Side Profile (Ngang Trái - Hướng Cạnh Trái)',
    subtitle: 'Đồng bộ toàn bộ đầu, tóc, ngực, hông, chân và mũi chân nhìn ngang 90° sang CẠNH TRÁI ảnh',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Góc Nhìn',
    icon: '⬅️',
    promptType: 'image',
    tags: ['angle', '90deg', 'side', 'profile', 'turnaround', 'faceless', 'left'],
    infoNote: '💡 Góc 90° Ngang Trái: Đồng bộ TOÀN BỘ từ đầu, ngực, hông đến 2 chân và mũi chân đều nhìn ngang 90° sang CẠNH TRÁI (loại bỏ hoàn toàn tư thế chân hướng về phía trước).',
    negativePrompt: `front view, 3/4 view, three-quarter view, front-facing feet, forward-facing shoes, feet pointing to camera, front-facing torso, front-facing chest, head facing front, facing right side, rotated to right, twisted body, twisted spine, mixed angles, eyes, eyebrows, mouth, nose, facial features, 3D render, realistic textures`,
    rawPrompt: `MASTER CHARACTER — 2D XIANXIA CHIBI — 90° FULL LEFT SIDE PROFILE (FACING DIRECTLY TOWARDS LEFT EDGE)

CRITICAL ANATOMICAL ALIGNMENT (ĐỒNG BỘ TOÀN BỘ ĐẦU, TÓC, THÂN, HÔNG, CHÂN, MŨI CHÂN NHÌN NGANG 90° SANG TRÁI):
- HEAD & PROFILE: Head is rotated 90 degrees in pure side profile facing DIRECTLY towards the LEFT EDGE of the canvas. Smooth blank faceless side silhouette (NO facial features, NO eyes/nose/mouth).
- HAIR & ACCESSORIES: Hairstyle in full 2D side view. Front bangs contour left cheek, long back hair cascades down right/back edge.
- TORSO & CHEST: Torso in pure 90° side profile facing left. Left shoulder and left chest line visible to viewer, right side occluded behind.
- HIPS & WAIST: Full side profile of belt, sash knot, and robe silhouette.
- LEGS, BOOTS & TOES (CHÂN & MŨI CHÂN): Both legs, shoes/boots, and toes MUST POINT 100% TO THE LEFT (pure side view feet). Left leg is in front, right leg partially visible behind. (ABSOLUTELY NO feet facing front, NO feet pointing at camera).
- ARMS & HANDS: Left arm relaxed at side facing viewer, right arm hidden behind body. Empty hands.

IDENTITY LOCK — keep exactly from 0° reference:
- Hairstyle side view, hair color, skin tone
- Chibi body proportions, costume profile, exact colors, accessories, silhouette
- Do NOT redesign or alter any item

STYLE: Pure flat 2D anime/Guoman chibi illustration. Bold linework, flat cel-shaded colors, minimal shading. NO 2.5D, NO 3D, NO realistic rendering.

BACKGROUND: Flat chroma-key green #00FF00.

COMPOSITION: One character, full body (head to feet visible), centered, plenty of space around, no text/watermark.`,
  },
  {
    id: 'angle135',
    title: '135° - Lưng Lệch Phải (Back-Right View - Hướng Cạnh Phải)',
    subtitle: 'Đồng bộ toàn bộ đầu, tóc, lưng, hông, chân và mũi chân xoay 135° quay lưng lệch sang CẠNH PHẢI ảnh',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Góc Nhìn',
    icon: '↘️',
    promptType: 'image',
    tags: ['angle', '135deg', 'back_right', 'turnaround', 'faceless', 'right'],
    infoNote: '💡 Góc 135° Lưng Lệch Phải: Nhân vật đứng quay lưng về phía camera, đồng bộ đầu, lưng, hai chân và mũi chân xoay 45° hướng về phía CẠNH PHẢI khung hình.',
    negativePrompt: `front view, 3/4 front view, face visible, front-facing chest, front-facing feet, facing left side, rotated to left, side profile 90, full back 180, twisted body, twisted spine, eyes, mouth, nose, 3D render, realistic`,
    rawPrompt: `MASTER CHARACTER — 2D XIANXIA CHIBI — 135° BACK-RIGHT THREE-QUARTER VIEW (FACING TOWARDS RIGHT EDGE)

Use the 180° BACK-VIEW reference image (or 0° front reference) as the ABSOLUTE SOURCE OF TRUTH.

CRITICAL ANATOMICAL ALIGNMENT (ĐỒNG BỘ TOÀN BỘ ĐẦU, TÓC, LƯNG, HÔNG, CHÂN XOAY 135° LƯNG LỆCH PHẢI):
- HEAD & HAIR: Head is facing away from camera, angled 45 degrees towards the RIGHT EDGE of the canvas. Back of hairstyle, hairpins, and back hair contour prominent. Smooth curvature (NO face visible).
- TORSO & BACK: Back of torso is mostly visible to camera, angled 45° towards right edge. Right shoulder closer to right side, left shoulder slightly back.
- HIPS & SASH: Back sash, ribbons, and robes draping viewed from 135° back-right angle.
- LEGS, BOOTS & TOES (CHÂN & MŨI CHÂN): Both legs and boots MUST POINT 45° TOWARDS THE RIGHT EDGE away from camera. Heels visible from behind-right perspective. (NO front-facing feet).
- ARMS & HANDS: Arms at sides in 135° perspective, empty hands.

IDENTITY LOCK — keep exactly from reference:
- Same hairstyle back details from 180° reference
- Same outfit back details, sash, ribbons, and colors from reference
- Same accessories arrangement from reference
- Only the rotation changes (angled 45° towards right edge), design stays 100% identical

STYLE: Pure flat 2D anime/Guoman chibi illustration. Bold linework, flat colors, minimal shading. NO 2.5D, NO 3D, NO realistic rendering.

BACKGROUND: Flat chroma-key green #00FF00.

COMPOSITION: One character, full body (head to feet visible), centered, plenty of space around, no text/watermark.`,
  },
  {
    id: 'angle180',
    title: '180° - Sau Lưng (180° Perfect Rear View)',
    subtitle: 'Khóa toàn bộ đầu, tóc sau lưng, thân sau, gót chân thẳng trục 180° đối diện camera',
    stepCategory: 'step1_character',
    stepLabel: 'Bước 1: Góc Nhìn',
    icon: '🔄',
    promptType: 'image',
    tags: ['angle', '180deg', 'back', 'turnaround'],
    infoNote: '💡 Góc 180° Sau Lưng: Nhân vật đứng quay lưng 100% về phía camera, đầu thẳng trục, hai gót chân hướng về camera, hai mũi chân hướng về phía trước.',
    negativePrompt: `front view, face visible, front-facing chest, front-facing feet, side view, 3/4 view, head turn, body rotation, asymmetrical pose, tilted head, eyes, nose, mouth, 3D render, realistic`,
    rawPrompt: `MASTER CHARACTER — 2D XIANXIA CHIBI — 180° PERFECT REAR VIEW (LƯNG ĐỐI DIỆN CAMERA)

Use the 0° front-view reference to understand character design.
NOW recreate this character in perfect 180° back view (lưng).

CRITICAL ANATOMICAL ALIGNMENT (ĐỒNG BỘ TOÀN BỘ ĐẦU, TÓC, LƯNG, HÔNG, CHÂN THẲNG TRỤC 180° TỪ SAU LƯNG):
- HEAD & HAIR: Head facing 100% directly away from camera (180°). Full back of hairstyle and hair ornaments symmetrically displayed. NO face or profile visible.
- TORSO & BACK: Spine perfectly vertical and centered. Full back of robes and collar symmetrically facing camera.
- HIPS & SASH: Back sash, waist knot, and rear robe folds centered.
- LEGS, BOOTS & HEELS (CHÂN & GÓT CHÂN): Both legs straight and symmetrical. Both heels facing camera, feet pointing straight forward away from viewer.
- ARMS & HANDS: Symmetrical arms at sides, empty hands.

IDENTITY LOCK — keep exactly from 0° reference:
- Hairstyle back view, hair color, skin tone, chibi proportions, costume rear details, colors, accessories.

STYLE: Pure flat 2D anime/Guoman chibi illustration. Bold linework, flat colors, minimal shading. NO 2.5D, NO 3D, NO realistic rendering.

BACKGROUND: Flat chroma-key green #00FF00.

COMPOSITION: One character, full body (head to feet visible), centered, plenty of space around, no text/watermark.`,
  },
];
