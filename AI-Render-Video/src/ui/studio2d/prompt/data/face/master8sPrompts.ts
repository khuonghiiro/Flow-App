import { PromptItem } from '../../types';

export const FACE_MASTER_8S_PROMPTS: PromptItem[] = [
  {
    id: 'face_angle0_8s',
    title: 'Face 8s Master - 0° Chính Diện (8s Full Expressions)',
    subtitle: 'Chuỗi 8 giây chuyển đổi đầy đủ 4 biểu cảm (Tự nhiên -> Cười nói -> Ngạc nhiên -> Giận dữ) trên nền xanh #00FF00',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Ngũ Quan 8s',
    icon: '⏱️',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'master_8s', 'timeline', 'expressions', 'composite'],
    videoGuide: {
      duration: '8.0 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front (Face Only)',
      loopType: '8s Sequence Loop',
      keyPoints: [
        'CHỈ vẽ: Lông mày, mắt, mũi, miệng và đổ bóng tương ứng',
        'TUYỆT ĐỐI KHÔNG vẽ tóc, tai, viền đầu, cổ hay thân',
        'Phân đoạn 8 giây rõ ràng theo nhịp: 0-2s, 2-4s, 4-6s, 6-8s',
        'Thuận tiện đưa vào Slicer để cắt 4 biểu cảm riêng biệt',
      ],
    },
    infoNote: '💡 PROMPT 8 GIÂY CHÍNH DIỆN: Tạo 1 video 8s chứa toàn bộ 4 biểu cảm. Sau đó đưa vào Tab Video Animation Slicer để cắt lấy từng biểu cảm ghép lên đầu nhân vật 0°!',
    negativePrompt: `hair, bangs, hairstyle, ears, head outline, skull, jawline contour, neck, body, shoulders, clothes, background scenery, 3d render, photorealism, low quality, messy lines, gradients on green, artifacts`,
    rawPrompt: `TASK: Image-to-video 8-SECOND ANIMATION OF FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) ON PURE CHROMA-KEY GREEN.

CRITICAL ISOLATION RULES (CHỈ VẼ NGŨ QUAN & ĐỔ BÓNG):
- Solid pure chroma green #00FF00 background fills 100% of canvas
- ONLY render: Eyebrows, Eyes (pupils, iris, eyelids, lashes), Nose bridge/tip, Mouth (lips, teeth, smile/open mouth) and their corresponding facial cast shadows
- STRICTLY FORBIDDEN: NO hair, NO bangs, NO ears, NO head outline, NO jawline, NO neck, NO body, NO clothes
- The facial features float centrally on the green screen at 0° direct front view, perfectly proportioned for anime chibi head compositing

8-SECOND TIMELINE (CHUYỂN ĐỔI BIỂU CẢM THEO GIÂY):
- [0.0s – 2.0s: NEUTRAL & BLINK]
  * Eyebrows relaxed and level
  * Gentle calm eyes; at 1.0s both eyes blink naturally (eyelids close and smoothly reopen in 0.25s)
  * Soft closed mouth resting in gentle serene expression
  * Subtle breathing rise/fall of facial cast shadows

- [2.0s – 4.0s: HAPPY SMILE & SPEAKING]
  * Eyebrows raise slightly with joy
  * Eyes curve into happy crescent crescents with lively catchlights
  * Mouth blossoms into warm smile, then opens naturally in conversational lip-sync shapes ("A - O - I - E")
  * Shadow under lower lip dynamic with mouth movement

- [4.0s – 6.0s: SURPRISED & SHOCKED]
  * Eyebrows arch high up in astonishment
  * Eyes open wide and round in shock, pupils dilate with drama
  * Mouth opens in a crisp surprised "O" shape showing slight depth
  * Sharper cast shadow under nose and lower lip

- [6.0s – 8.0s: ANGRY COMBAT & RESET]
  * Eyebrows furrow down tightly towards center in fierce determination
  * Eyes narrow sharply into intense warrior combat stare
  * Mouth clinches firmly with subtle bared teeth/grit
  * At 7.8s – 8.0s, face smoothly relaxes back to neutral resting state matching 0.0s frame

CONSTRAINTS:
- Pure 0° direct frontal alignment throughout
- Pure #00FF00 chroma green with ZERO background shadows
- Clean, bold anime linework`,
  },
  {
    id: 'face_angle45_8s',
    title: 'Face 8s Master - 45° Xoay Trái (8s 3/4 Expressions)',
    subtitle: 'Chuỗi 8 giây chuyển đổi 4 biểu cảm ở góc chéo 3/4 trái có luật phối cảnh xa gần',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Ngũ Quan 8s',
    icon: '⏱️',
    promptType: 'video',
    tags: ['face', '45deg', 'isometric', 'master_8s', 'timeline', 'expressions', 'composite'],
    videoGuide: {
      duration: '8.0 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left (Face Only)',
      loopType: '8s Sequence Loop',
      keyPoints: [
        'Ngũ quan góc 3/4: Mắt gần lớn hơn, mắt xa thu nhỏ theo phối cảnh',
        'Sống mũi nghiêng nhẹ sang trái, đổ bóng về phía bên phải',
        'CHỈ lông mày, mắt, mũi, miệng trên nền xanh #00FF00',
        'Tuyệt đối không vẽ tóc, tai, viền đầu',
      ],
    },
    infoNote: '💡 PROMPT 8 GIÂY GÓC 45°: Góc 3/4 là góc phổ biến nhất khi diễn hoạt đối thoại và chiến đấu trong game 2.5D!',
    negativePrompt: `hair, bangs, hairstyle, ears, head outline, skull, neck, body, shoulders, clothes, front view, side profile, messy lines, background objects`,
    rawPrompt: `TASK: Image-to-video 8-SECOND ANIMATION OF FLOATING 2D FACIAL FEATURES AT 45° THREE-QUARTER LEFT VIEW ON CHROMA-KEY GREEN.

ISOLATION & PERSPECTIVE SPECIFICATIONS (45° LEFT VIEW):
- Solid pure chroma-key green #00FF00 background
- ONLY render: Left eye (near, full size), Right eye (far, perspective foreshortened), Eyebrows angled in 3/4 perspective, Nose bridge pointing left with directional cast shadow, Mouth positioned in 3/4 alignment
- STRICTLY FORBIDDEN: NO hair, NO ears, NO head/jaw contour, NO neck, NO body, NO clothing

8-SECOND TIMELINE (45° LEFT PERSPECTIVE):
- [0.0s – 2.0s: 3/4 NEUTRAL & BLINK]
  * Poised 45° neutral face, calm gaze directed towards 3/4 left
  * At 1.0s: both eyes perform natural synchronized blink maintaining 3/4 perspective
  * Nose shadow angled consistently towards the right

- [2.0s – 4.0s: 3/4 HAPPY SMILE & TALKING]
  * Eyebrows lift pleasantly; both eyes smile with lively 3/4 highlights
  * Mouth opens in 3/4 perspective smiling dialogue motion with natural phoneme shapes
  * Cast shadow under lower lip moves dynamically

- [4.0s – 6.0s: 3/4 SURPRISED & SHOCKED]
  * Eyebrows jump upward in astonishment
  * Near and far eyes open wide in startled expression, pupils tense
  * Mouth drops open into startled 3/4 oval shape

- [6.0s – 8.0s: 3/4 FIERCE COMBAT & RESET]
  * Inner brow points slant downward sharply into angry warrior glare
  * Eyes narrow into menacing combat focus
  * Mouth tightens into resolute grit
  * At 7.8s – 8.0s, face relaxes smoothly back into 45° neutral resting pose matching start

CONSTRAINTS:
- Constant 45° left angle throughout (no angle rotation)
- Pure solid green #00FF00 background`,
  },
  {
    id: 'face_angle90_8s',
    title: 'Face 8s Master - 90° Side Profile (8s Profile Expressions)',
    subtitle: 'Chuỗi 8 giây chuyển đổi 4 biểu cảm nhìn ngang toàn phần (Side Profile)',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Ngũ Quan 8s',
    icon: '⏱️',
    promptType: 'video',
    tags: ['face', '90deg', 'side', 'profile', 'master_8s', 'timeline', 'expressions'],
    videoGuide: {
      duration: '8.0 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Profile (Face Only)',
      loopType: '8s Sequence Loop',
      keyPoints: [
        'Nhìn ngang thuần túy: 1 bên lông mày, 1 mắt nghiêng, sống mũi profile, nửa môi',
        'Đổ bóng chóp mũi và môi dưới chuẩn góc 90°',
        'CHỈ có ngũ quan trên nền xanh #00FF00',
      ],
    },
    infoNote: '💡 PROMPT 8 GIÂY GÓC 90°: Dùng cho game đi cảnh màn hình ngang (Platformer) và các phân cảnh nhìn nghiêng điện ảnh.',
    negativePrompt: `hair, bangs, ear, head silhouette, skull, neck, body, clothes, 3/4 angle, front view, floating artifacts`,
    rawPrompt: `TASK: Image-to-video 8-SECOND ANIMATION OF FLOATING 2D FACIAL FEATURES IN FULL 90° SIDE PROFILE ON PURE CHROMA-KEY GREEN.

ISOLATION & PROFILE SPECIFICATIONS:
- Solid chroma green #00FF00 background
- ONLY render in pure 90° left profile: Left eyebrow side view, Left eye in profile (triangular lash silhouette, visible iris/pupil), Profile nose curve (bridge and tip), Left lip profile (upper/lower lips opening and closing in profile) and subtle profile cast shadows
- NO hair, NO ear, NO skull/jaw outline, NO neck, NO body

8-SECOND TIMELINE (90° SIDE PROFILE):
- [0.0s – 2.0s: PROFILE NEUTRAL & BLINK]
  * Serene profile gaze looking straight left; smooth profile blink at 1.0s
  * Profile nose line and lip rest naturally

- [2.0s – 4.0s: PROFILE SMILE & SPEAKING]
  * Eyebrow arches gently; eye crinkles in profile smile
  * Profile lips open and articulate natural dialogue speech shapes

- [4.0s – 6.0s: PROFILE SURPRISED]
  * Eyebrow leaps up high; profile eye widens in surprise
  * Mouth opens in startled profile "O"

- [6.0s – 8.0s: PROFILE COMBAT & RESET]
  * Eyebrow presses down low in grim intensity; eye narrows fiercely
  * Jaw/lips tense into a combat grimace
  * At 7.8s – 8.0s, seamlessly returns to neutral profile resting pose

CONSTRAINTS:
- Pure 90° side profile alignment throughout
- Pure #00FF00 green background`,
  },
  {
    id: 'face_angle135_8s',
    title: 'Face 8s Master - 135° Ngoái Nhìn (8s Over-the-Shoulder)',
    subtitle: 'Chuỗi 8 giây biểu cảm khi nhân vật quay lưng 135° ngoái đầu nhìn lại về phía camera',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Ngũ Quan 8s',
    icon: '⏱️',
    promptType: 'video',
    tags: ['face', '135deg', 'back_right', 'glance', 'master_8s', 'timeline', 'expressions'],
    videoGuide: {
      duration: '8.0 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 135° Glance (Face Only)',
      loopType: '8s Sequence Loop',
      keyPoints: [
        'Góc ngoái đầu nhìn lại từ sau lưng lệch phải 135°',
        'Mắt nhìn xéo qua vai về phía camera',
        'Đổ bóng góc sau ngược sáng',
        'CHỈ vẽ ngũ quan và bóng đổ trên nền xanh #00FF00',
      ],
    },
    infoNote: '💡 PROMPT 8 GIÂY GÓC 135° NGOÁI NHÌN: Cực kỳ đắt giá cho các cảnh nhân vật quay lưng bước đi nhưng ngoái đầu lại nhìn cảnh cáo, mỉm cười bí ẩn hoặc liếc mắt sắc bén!',
    negativePrompt: `hair, bangs, ears, back of head, body, back robes, shoulders, arms, turning around full front, messy shadows`,
    rawPrompt: `TASK: Image-to-video 8-SECOND ANIMATION OF FLOATING 2D FACIAL FEATURES FOR 135° OVER-THE-SHOULDER GLANCE ON CHROMA-KEY GREEN.

ISOLATION & GLANCE PERSPECTIVE:
- Pure chroma green #00FF00 background
- Character body is angled 135° away (facing upper-right), with head turned back towards viewer
- ONLY render: Right eyebrow & partial left eyebrow, Right eye looking back towards camera with sharp corner glance, tip of nose silhouette, lip edge/corner in backward glance perspective, matching directional shadows
- STRICTLY NO hair, NO body, NO shoulders, NO clothes

8-SECOND TIMELINE (135° BACKWARD GLANCE):
- [0.0s – 2.0s: CALM RETROSPECTIVE BLINK]
  * Cool, detached backward glance over shoulder; smooth blink at 1.0s

- [2.0s – 4.0s: SUBTLE SMIRK & WHISPER]
  * Subtle knowing smirk curls at the visible corner of lips
  * Eyebrow raises slyly in mysterious expression

- [4.0s – 6.0s: SUDDEN ALERT & WIDE EYE]
  * Brow twitches upward; eye snaps wide in alert recognition of danger behind

- [6.0s – 8.0s: SHARP COMBAT THREAT & RESET]
  * Brow slants sharply down; pupil narrows into razor-sharp lethal warning gaze
  * Corner of mouth tightens into dangerous smirk
  * At 7.8s – 8.0s, relaxes back into serene mysterious backward glance

CONSTRAINTS:
- Maintain 135° glance orientation throughout
- Pure solid chroma green #00FF00`,
  },
];
