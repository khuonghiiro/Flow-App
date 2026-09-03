import { PromptItem } from '../../types';

export const FACE_SINGLE_EXPRESSION_PROMPTS: PromptItem[] = [
  // ─── GÓC 0° CHÍNH DIỆN (FRONT EXPRESSIONS) ───
  {
    id: 'face_0_blink',
    title: 'Face 0° - Chớp Mắt Tự Nhiên (Neutral Blink)',
    subtitle: 'Ngũ quan chính diện chớp mắt tự nhiên 2-3s lặp vô tận trên nền xanh #00FF00',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '👀',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'blink', 'neutral', 'loop', 'idle'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front (Face Only)',
      loopType: 'Seamless Loop',
      keyPoints: ['Chỉ có lông mày, mắt, mũi, miệng', 'Chớp mắt 1 nhịp ở giữa', 'Không vẽ tóc/tai/đầu'],
    },
    infoNote: '💡 Biểu cảm trạng thái nghỉ mặc định cho nhân vật ở góc chính diện.',
    negativePrompt: `hair, bangs, ears, head outline, skull, neck, body, shoulders, clothes, background scenery`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) NEUTRAL BLINK CYCLE ON PURE CHROMA-KEY GREEN.

CRITICAL ISOLATION:
- Pure chroma green #00FF00 background
- ONLY render: Eyebrows, Eyes, Nose bridge/tip, Mouth and natural cast shadows beneath them
- NO hair, NO ears, NO head outline, NO jaw, NO neck, NO body

MOTION:
- Face rests in gentle, serene neutral expression facing directly forward (0°)
- At midpoint (1.2s): both eyelids blink down smoothly and reopen crisp with vibrant eye catchlights
- Subtle breathing rise/fall of facial cast shadows
- First and last frames match seamlessly for looping

CONSTRAINTS:
- 0° direct front view throughout
- Solid #00FF00 green background`,
  },
  {
    id: 'face_0_smile',
    title: 'Face 0° - Mỉm Cười & Vui Vẻ (Happy Smile)',
    subtitle: 'Ngũ quan chính diện mỉm cười rạng rỡ, mắt cười híp cong hình vầng trăng',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😊',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'happy', 'smile', 'loop', 'joy'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front (Face Only)',
      loopType: 'Seamless Loop',
      keyPoints: ['Khóe môi cong lên nụ cười tươi', 'Mắt cong lấp lánh ánh cười', 'Lông mày giãn nở vui vẻ'],
    },
    infoNote: '💡 Dùng cho các cảnh nhân vật chào đón, vui mừng, đắc thắng hoặc tương tác thân thiện.',
    negativePrompt: `hair, bangs, ears, head outline, neck, body, clothes, frowning, angry, crying`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) HAPPY SMILING ANIMATION ON CHROMA-KEY GREEN.

ISOLATION:
- Solid chroma-key green #00FF00 background
- ONLY render: Eyebrows, Eyes, Nose, Smiling Mouth and cast shadows
- NO hair, NO ears, NO head contour, NO neck, NO body

MOTION:
- Eyebrows raise pleasantly with joyful expression
- Eyes crinkle into sparkling happy crescents with warm animated highlights
- Mouth smiles warmly, parting softly to show bright teeth/smile line
- Gentle natural breathing motion, seamless loop to start pose

CONSTRAINTS:
- Strictly 0° front view
- Pure chroma green #00FF00`,
  },
  {
    id: 'face_0_talk',
    title: 'Face 0° - Khẩu Hình Đối Thoại (Speaking Lip-Sync)',
    subtitle: 'Ngũ quan chính diện cử động môi nói chuyện tự nhiên (A - O - I - E) lặp 3 giây',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '🗣️',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'talk', 'speaking', 'lip_sync', 'dialogue', 'loop'],
    videoGuide: {
      duration: '3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front (Face Only)',
      loopType: 'Dialogue Loop',
      keyPoints: ['Môi mở đóng tự nhiên theo âm vị', 'Mắt chớp nhẹ nhịp nhàng', 'Bóng môi dưới cử động linh hoạt'],
    },
    infoNote: '💡 Cực kỳ hữu ích để ghép đè lên nhân vật khi nói chuyện trong các đoạn cutscene hoặc thuyết minh!',
    negativePrompt: `hair, bangs, ears, head contour, neck, body, clothes, exaggerated jaw, tongue sticking out`,
    rawPrompt: `TASK: Image-to-video 3-second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) NATURAL SPEAKING LIP-SYNC ON CHROMA GREEN.

ISOLATION:
- Pure solid chroma green #00FF00
- ONLY render: Eyebrows, expressive Eyes, Nose, and articulative Mouth with cast shadows
- NO hair, NO ears, NO head/jaw silhouette, NO neck, NO body

MOTION:
- Mouth articulates fluent anime speech phonemes: softly opens, forms shapes for "A - O - I - E - M"
- Lips move smoothly without distortion; teeth visible naturally inside mouth cavity
- Eyebrows and eyes subtly emote in sync with vocal cadence, with one soft natural blink
- Returns to resting speech pose at loop point

CONSTRAINTS:
- 0° direct frontal alignment
- Solid #00FF00 chroma green`,
  },
  {
    id: 'face_0_angry',
    title: 'Face 0° - Tức Giận & Chiến Đấu (Fierce Combat Stare)',
    subtitle: 'Ngũ quan chính diện chau mày giận dữ, ánh mắt sắc bén đằng đằng sát khí',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😠',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'angry', 'combat', 'fierce', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front (Face Only)',
      loopType: 'Intense Loop',
      keyPoints: ['Chân mày hạ thấp chau chặt', 'Đồng tử nheo sắc sảo', 'Miệng mím chặt hoặc nghiến răng nhẹ'],
    },
    infoNote: '💡 Dùng cho các cảnh chiến đấu, tung chiêu thức tối thượng hoặc đối đầu căng thẳng.',
    negativePrompt: `hair, bangs, ears, head outline, smiling, happy, crying tears, neck, body, clothes`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) INTENSE ANGRY COMBAT EXPRESSION ON CHROMA GREEN.

ISOLATION:
- Pure chroma green #00FF00 background
- ONLY render: Furrowed Eyebrows, Piercing narrowed Eyes, Nose with sharp shadow, Tight combat Mouth
- NO hair, NO ears, NO head outline, NO neck, NO body

MOTION:
- Eyebrows angled steeply inward and down in furious determination
- Eyes narrowed into a razor-sharp warrior glare with fiery intense pupils
- Mouth clenched firmly with subtle fierce tension
- Subtle rhythmic breathing pulsation of anger

CONSTRAINTS:
- 0° direct front view
- Pure #00FF00 chroma green`,
  },
  {
    id: 'face_0_surprised',
    title: 'Face 0° - Kinh Ngạc & Mở To Mắt (Surprised Shock)',
    subtitle: 'Ngũ quan chính diện mở to kinh ngạc, lông mày nhướng cao, miệng chữ O',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😲',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'surprised', 'shock', 'loop'],
    videoGuide: {
      duration: '2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front (Face Only)',
      loopType: 'Snappy Loop',
      keyPoints: ['Mắt mở to tròn đồng tử co nhẹ', 'Lông mày nhướng cao lên trán', 'Miệng há mở chữ O'],
    },
    infoNote: '💡 Thích hợp khi nhân vật bị tập kích bất ngờ, phát hiện bí mật hoặc trúng bẫy.',
    negativePrompt: `hair, bangs, ears, head outline, smiling, sleepy, closed eyes, neck, body`,
    rawPrompt: `TASK: Image-to-video 2-second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) SURPRISED SHOCKED EXPRESSION ON CHROMA GREEN.

ISOLATION:
- Pure chroma green #00FF00 background
- ONLY render: High-arched Eyebrows, Wide astonished Eyes, Nose, Surprised "O" Mouth with shadows
- NO hair, NO ears, NO head contour, NO neck, NO body

MOTION:
- Eyebrows jump up high on the forehead in shock
- Eyes snap open wide, pupils dilate and shake slightly with disbelief
- Mouth drops open into a clean rounded "O" expression
- Holds for dramatic beat, then resets smoothly

CONSTRAINTS:
- 0° front view throughout
- Solid #00FF00 chroma green`,
  },
  {
    id: 'face_0_sad',
    title: 'Face 0° - Buồn Bã & U Sầu (Sad Melancholy)',
    subtitle: 'Ngũ quan chính diện chân mày ủ rũ, ánh mắt đượm buồn nhìn xuống, khóe môi trễ',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😢',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'sad', 'crying', 'melancholy', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 0° Front (Face Only)',
      loopType: 'Subtle Loop',
      keyPoints: ['Chân mày cụp về hai bên thái dương', 'Ánh mắt buồn bã long lanh', 'Khóe môi trễ xuống u sầu'],
    },
    infoNote: '💡 Dùng cho các cảnh chia ly, thất bại hoặc hồi tưởng bi thương trong cốt truyện.',
    negativePrompt: `hair, bangs, ears, head outline, laughing, smiling, aggressive, neck, body`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) SAD MELANCHOLY EXPRESSION ON CHROMA GREEN.

ISOLATION:
- Pure chroma green #00FF00 background
- ONLY render: Downturned sorrowful Eyebrows, Melancholy Eyes with glistening glassy highlights, Nose, Down-turned lips
- NO hair, NO ears, NO head silhouette, NO neck, NO body

MOTION:
- Eyebrows slope downward outward in sorrow
- Eyes gaze gently downward with deep emotional pathos, blinking slowly and heavily
- Corner of lips droop slightly in sorrowful resignation
- Gentle sorrowful breathing motion

CONSTRAINTS:
- 0° direct front view
- Pure solid green #00FF00`,
  },
  {
    id: 'face_0_wink',
    title: 'Face 0° - Nháy Mắt Tinh Nghịch (Playful Wink)',
    subtitle: 'Ngũ quan chính diện nháy 1 mắt tinh nghịch, khóe môi cười mỉm đáng yêu',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😉',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'wink', 'playful', 'cute', 'loop'],
    videoGuide: {
      duration: '2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front (Face Only)',
      loopType: 'Seamless Loop',
      keyPoints: ['Một mắt nháy khép tinh nghịch', 'Mắt còn lại mở to long lanh', 'Miệng cười mỉm cute'],
    },
    infoNote: '💡 Tuyệt vời cho các nhân vật anime chibi có tính cách lém lỉnh, nhí nhảnh hoặc tạo dáng chiến thắng.',
    negativePrompt: `hair, bangs, ears, head contour, both eyes closed, both eyes open continuously, neck, body`,
    rawPrompt: `TASK: Image-to-video 2-second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) PLAYFUL ANIME WINK ON CHROMA GREEN.

ISOLATION:
- Pure chroma green #00FF00 background
- ONLY render: Eyebrows, One winking eye and one open sparkling eye, Nose, Cute smiling mouth with shadows
- NO hair, NO ears, NO head outline, NO neck, NO body

MOTION:
- Right eye winks tightly shut with a cheerful cute crinkle, while Left eye remains wide and sparkles with charm
- Left eyebrow raises slightly in playful amusement
- Mouth curves into an adorable smirk/smile
- Winking eye smoothly reopens to complete seamless loop

CONSTRAINTS:
- 0° front view
- Solid #00FF00 chroma green`,
  },

  // ─── GÓC 45° XOAY TRÁI (3/4 LEFT EXPRESSIONS) ───
  {
    id: 'face_45_blink',
    title: 'Face 45° - Chớp Mắt Tự Nhiên (3/4 Neutral Blink)',
    subtitle: 'Ngũ quan góc 3/4 chéo trái chớp mắt tự nhiên có luật phối cảnh xa gần',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '👀',
    promptType: 'video',
    tags: ['face', '45deg', 'isometric', 'blink', 'neutral', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left (Face Only)',
      loopType: 'Seamless Loop',
      keyPoints: ['Mắt gần lớn hơn mắt xa', 'Sống mũi nghiêng 3/4', 'Chớp mắt tự nhiên'],
    },
    infoNote: '💡 Trạng thái chờ mặc định cho khuôn mặt ở góc 3/4 Isometric.',
    negativePrompt: `hair, bangs, ears, head outline, neck, body, front view, side profile`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (45° THREE-QUARTER LEFT VIEW) NEUTRAL BLINK CYCLE ON CHROMA GREEN.

ISOLATION & 3/4 PERSPECTIVE:
- Pure chroma green #00FF00 background
- ONLY render: Left eye (near, prominent), Right eye (far, foreshortened), 3/4 angled Eyebrows, 3/4 Nose bridge, 3/4 neutral Mouth and cast shadows
- NO hair, NO ears, NO head outline, NO neck, NO body

MOTION:
- Face rests in tranquil 3/4 left poise
- At 1.2s: both eyes blink synchronously in 3/4 perspective
- Smooth return to open relaxed gaze

CONSTRAINTS:
- Strict 45° left angle throughout
- Solid #00FF00 green background`,
  },
  {
    id: 'face_45_smile',
    title: 'Face 45° - Mỉm Cười & Đối Thoại (3/4 Happy Talk)',
    subtitle: 'Ngũ quan góc 3/4 chéo trái cười tươi và mở miệng nói chuyện thanh thoát',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😊',
    promptType: 'video',
    tags: ['face', '45deg', 'isometric', 'smile', 'talk', 'dialogue', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left (Face Only)',
      loopType: 'Dialogue Loop',
      keyPoints: ['Khẩu hình 3/4 tự nhiên', 'Ánh mắt cười long lanh', 'Đổ bóng chuẩn 45°'],
    },
    infoNote: '💡 Góc thoại đẹp nhất và chuẩn điện ảnh nhất trong game RPG nhập vai.',
    negativePrompt: `hair, bangs, ears, head outline, neck, body, angry, weeping`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (45° THREE-QUARTER LEFT VIEW) HAPPY DIALOGUE TALK ON CHROMA GREEN.

ISOLATION & 3/4 SPECIFICATIONS:
- Solid #00FF00 green background
- ONLY render: 3/4 angled Eyebrows, 3/4 smiling Eyes with bright catchlights, 3/4 Nose with directional shadow, 3/4 talking Mouth
- NO hair, NO ears, NO head silhouette, NO neck, NO body

MOTION:
- Both eyes sparkle with warm gentle affection
- Mouth opens in natural conversational 3/4 anime dialogue cadence
- Shadow under lower lip tracks mouth motions seamlessly

CONSTRAINTS:
- 45° left angle
- Pure #00FF00 chroma green`,
  },
  {
    id: 'face_45_angry',
    title: 'Face 45° - Chiến Đấu Sát Khí (3/4 Combat Glare)',
    subtitle: 'Ngũ quan góc 3/4 chéo trái nheo mắt sắc sảo, sát khí chiến đấu áp đảo',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😠',
    promptType: 'video',
    tags: ['face', '45deg', 'isometric', 'angry', 'combat', 'glare', 'loop'],
    videoGuide: {
      duration: '2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left (Face Only)',
      loopType: 'Intense Loop',
      keyPoints: ['Ánh mắt 3/4 nheo sắc bén', 'Lông mày cau chặt chéo', 'Môi mím kiên định'],
    },
    infoNote: '💡 Thích hợp ghép đè khi nhân vật đang tung đòn đánh công ở góc 45°!',
    negativePrompt: `hair, bangs, ears, head contour, laughing, smiling, crying, neck, body`,
    rawPrompt: `TASK: Image-to-video 2-second seamless loop FLOATING 2D FACIAL FEATURES (45° THREE-QUARTER LEFT VIEW) FIERCE COMBAT GLARE ON CHROMA GREEN.

ISOLATION:
- Pure solid chroma-key green #00FF00
- ONLY render: 3/4 furrowed Eyebrows, 3/4 intense narrowed warrior Eyes, 3/4 Nose with sharp contrast shadow, 3/4 clenched combat Mouth
- NO hair, NO ears, NO head outline, NO neck, NO body

MOTION:
- Eyes narrow aggressively with lethal determination towards 3/4 opponent
- Eyebrows press down hard at inner brow
- Lips locked in firm resolute tension

CONSTRAINTS:
- Maintain strict 45° left orientation
- Pure #00FF00 green`,
  },
  {
    id: 'face_45_surprised',
    title: 'Face 45° - Kinh Ngạc & Mở To Mắt (3/4 Surprised)',
    subtitle: 'Ngũ quan góc 3/4 chéo trái mở to mắt kinh ngạc, há miệng sững sờ',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😲',
    promptType: 'video',
    tags: ['face', '45deg', 'isometric', 'surprised', 'shock', 'loop'],
    videoGuide: {
      duration: '2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left (Face Only)',
      loopType: 'Snappy Loop',
      keyPoints: ['Mắt mở to ở góc 3/4', 'Lông mày nhướng cao', 'Miệng mở sững sờ'],
    },
    infoNote: '💡 Dùng cho biểu cảm giật mình khi né đòn hoặc bị bất ngờ ở góc 45°.',
    negativePrompt: `hair, bangs, ears, head outline, neck, body, closed eyes, smiling`,
    rawPrompt: `TASK: Image-to-video 2-second seamless loop FLOATING 2D FACIAL FEATURES (45° THREE-QUARTER LEFT VIEW) STARTLED SURPRISED EXPRESSION ON CHROMA GREEN.

ISOLATION:
- Pure chroma green #00FF00 background
- ONLY render: 3/4 high arched Eyebrows, 3/4 wide astonished Eyes, 3/4 Nose, 3/4 open surprised Mouth
- NO hair, NO ears, NO head/skull silhouette, NO neck, NO body

MOTION:
- Near and far eyes snap open in startling revelation
- Eyebrows jump high
- Mouth parts into a surprised 3/4 open oval
- Resets smoothly to loop

CONSTRAINTS:
- Strict 45° left perspective
- Pure chroma green #00FF00`,
  },

  // ─── GÓC 90° NHÌN NGANG (SIDE PROFILE) ───
  {
    id: 'face_90_profile',
    title: 'Face 90° - Biểu Cảm Nhìn Ngang (Side Profile Expression)',
    subtitle: 'Ngũ quan nhìn ngang 90° chớp mắt và mỉm cười thanh thoát',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '⬅️',
    promptType: 'video',
    tags: ['face', '90deg', 'side', 'profile', 'expression', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 90° Profile (Face Only)',
      loopType: 'Seamless Loop',
      keyPoints: ['1 mắt profile chớp nhẹ', 'Đường nét sống mũi và môi nghiêng', 'Nền xanh #00FF00'],
    },
    infoNote: '💡 Biểu cảm nhìn ngang cho các cảnh nhân vật đi cảnh ngang hoặc nhìn ra xa.',
    negativePrompt: `hair, bangs, ears, head silhouette, neck, body, front view, 3/4 view`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES IN PURE 90° SIDE PROFILE ON CHROMA GREEN.

ISOLATION:
- Solid chroma green #00FF00 background
- ONLY render in pure 90° left profile: Profile Eyebrow, Profile Eye with eyelashes, Profile Nose ridge/tip, Profile Lips opening softly into serene smile
- NO hair, NO ear, NO head contour, NO neck, NO body

MOTION:
- Profile eye blinks softly at midpoint
- Lips curve into gentle knowing profile smile
- Clean seamless loop

CONSTRAINTS:
- Strictly 90° side profile
- Pure #00FF00 green`,
  },

  // ─── GÓC 135° NGOÁI NHÌN (OVER-THE-SHOULDER GLANCE) ───
  {
    id: 'face_135_glance',
    title: 'Face 135° - Ánh Mắt Ngoái Nhìn (Over-the-Shoulder Glance)',
    subtitle: 'Ngũ quan ngoái nhìn qua vai từ góc 135° chớp mắt bí ẩn và mỉm cười nhạt',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '↘️',
    promptType: 'video',
    tags: ['face', '135deg', 'back_right', 'glance', 'mysterious', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 FPS',
      camera: 'Static Lock 135° Glance (Face Only)',
      loopType: 'Subtle Loop',
      keyPoints: ['Mắt liếc nhìn về sau vai hướng camera', 'Khóe môi cười bí ẩn', 'Chỉ ngũ quan trên nền xanh'],
    },
    infoNote: '💡 Biểu cảm ngoái nhìn đầy khí chất và ma mị cho nhân vật khi quay lưng bước đi.',
    negativePrompt: `hair, bangs, ears, full body, back robes, front view, neck, head silhouette`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES FOR 135° OVER-THE-SHOULDER GLANCE ON CHROMA GREEN.

ISOLATION:
- Pure chroma green #00FF00 background
- ONLY render: Right eye looking back towards viewer over shoulder, right eyebrow arch, nose tip contour, subtle lip corner smile
- NO hair, NO back of head, NO neck, NO body, NO clothing

MOTION:
- Eye holds cool, mysterious backward gaze, softly blinks
- Corner of lips curls in a subtle enigmatic smile
- Seamless loop matching start frame

CONSTRAINTS:
- 135° backward glance perspective
- Solid #00FF00 chroma green`,
  },
];
