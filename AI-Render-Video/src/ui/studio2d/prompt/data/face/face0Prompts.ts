import { PromptItem } from '../../types';

export const FACE_0_PROMPTS: PromptItem[] = [
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
CRITICAL ISOLATION: Pure chroma green #00FF00 background. ONLY render: Eyebrows, Eyes, Nose bridge/tip, Mouth and cast shadows. NO hair, NO ears, NO head outline, NO neck, NO body.
MOTION: Face rests in serene neutral expression (0°). At midpoint (1.2s): eyelids blink smoothly and reopen with sparkling catchlights. First and last frames match seamlessly.`,
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
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Loop',
      keyPoints: ['Khóe miệng nâng cao', 'Mắt cong hình trăng khuyết'],
    },
    infoNote: '💡 Biểu cảm tươi vui, thân thiện khi chào hỏi hoặc vui mừng.',
    negativePrompt: `hair, ears, head outline, neck, body, scenery, frown, angry`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) HAPPY SMILING ON PURE CHROMA-KEY GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Nose, Mouth + cast shadows. NO hair/ears/head.
MOTION: Mouth curls into warm bright smile, revealing soft parting of lips. Eyes curve into crescent moons with gentle twinkling catchlights. Seamless loop.`,
  },
  {
    id: 'face_0_talk',
    title: 'Face 0° - Nói Chuyện & Đàm Thoại (Talking Loop)',
    subtitle: 'Ngũ quan chính diện cử động môi nói chuyện linh hoạt theo nhịp thoại 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '🗣️',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'talk', 'dialogue', 'speech', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Loop',
      keyPoints: ['Khuôn miệng đóng mở tự nhiên nhịp nhàng', 'Lông mày nhấp nhô biểu cảm'],
    },
    infoNote: '💡 Dùng để lồng tiếng (Voiceover / Lip-sync) cho video hoạt hình.',
    negativePrompt: `hair, ears, head outline, neck, body, scenery`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) NATURAL TALKING CYCLE ON PURE CHROMA-KEY GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Nose, Mouth + cast shadows.
MOTION: Mouth articulates varied natural speech syllables (A-I-U-E-O phonetic shapes). Eyebrows accentuate dialogue with subtle lively micro-expressions. Natural eye blinks included. Seamless loop.`,
  },
  {
    id: 'face_0_angry',
    title: 'Face 0° - Tức Giận & Chiến Đấu (Angry Combat)',
    subtitle: 'Ngũ quan chính diện trừng mắt tức giận, lông mày nhíu chặt, răng nghiến giận dữ',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😠',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'angry', 'fierce', 'combat', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Loop',
      keyPoints: ['Lông mày nhíu chéo sắc bén', 'Răng nghiến chặt, mắt trợn sắc'],
    },
    infoNote: '💡 Dùng trong các cảnh giao tranh, đối đầu hoặc tung tuyệt chiêu.',
    negativePrompt: `hair, ears, head outline, neck, body, smile, laughing`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) FIERCE ANGRY COMBAT EXPRESSION ON PURE CHROMA-KEY GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Nose, Mouth.
MOTION: Eyebrows steeply angled downward in fierce resolve. Eyes narrow into intense sharp glare with fiery pupil shine. Mouth grits teeth in determined roar/snarl. Tense breathing twitch. Seamless loop.`,
  },
  {
    id: 'face_0_surprised',
    title: 'Face 0° - Ngạc Nhiên & Choáng Váng (Surprised / Shock)',
    subtitle: 'Ngũ quan chính diện mắt mở to tròn, con ngươi thu nhỏ, miệng há tròn ngạc nhiên',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😲',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'surprised', 'shock', 'gasp', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Loop',
      keyPoints: ['Mắt mở to cực đại', 'Miệng há tròn chữ O ngạc nhiên'],
    },
    infoNote: '💡 Biểu cảm cho các khoảnh khắc bất ngờ, giật mình hoặc phát hiện bí mật.',
    negativePrompt: `hair, ears, head outline, neck, body, angry, sleepy`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) SURPRISED SHOCK EXPRESSION ON PURE CHROMA-KEY GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Nose, Mouth.
MOTION: Eyebrows arch high in shock. Eyes widen wide showing full round pupils and bright reflection. Mouth drops open in small gasp (O-shape). Subtle trembling breath. Seamless loop.`,
  },
  {
    id: 'face_0_sad',
    title: 'Face 0° - Buồn Bã & U Sầu (Sad / Melancholy)',
    subtitle: 'Ngũ quan chính diện đuôi mắt cụp, lông mày gãy khúc đau buồn, môi mím nhẹ',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😢',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'sad', 'sorrow', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Loop',
      keyPoints: ['Đầu lông mày nhíu lên, đuôi xệ xuống', 'Mắt buồn rầu rơm rớm'],
    },
    infoNote: '💡 Biểu cảm tâm trạng thất vọng, buồn tủi hoặc tiếc nuối.',
    negativePrompt: `hair, ears, head outline, neck, body, smile, laughing`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) SAD MELANCHOLY ON PURE CHROMA-KEY GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Nose, Mouth.
MOTION: Inner eyebrows tilt upward in sorrow. Eyelids droop gently, eyes glistening with moist sheen. Corners of mouth turn down slightly in tender grief. Slow soft blink. Seamless loop.`,
  },
  {
    id: 'face_0_wink',
    title: 'Face 0° - Nháy Mắt Tinh Nghịch (Playful Wink)',
    subtitle: 'Ngũ quan chính diện nháy một bên mắt tinh nghịch, khóe môi cười ranh mãnh',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😉',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'wink', 'playful', 'cute', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Loop',
      keyPoints: ['Một mắt nhắm tít vui vẻ', 'Một mắt mở to lấp lánh', 'Mép môi nhếch cười'],
    },
    infoNote: '💡 Biểu cảm đáng yêu, trêu chọc hoặc kết thúc câu nói vui nhộn.',
    negativePrompt: `hair, ears, head outline, neck, body, angry, crying`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) PLAYFUL WINK ON PURE CHROMA-KEY GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Nose, Mouth.
MOTION: Left eye winks tightly shut in playful anime wink with cute star sparkle, right eye stays wide and sparkling. Mouth smirks happily with one corner raised. Quick wink & hold. Seamless loop.`,
  },
  // ─── BIỂU CẢM MỚI BỔ SUNG ───
  {
    id: 'face_0_cry',
    title: 'Face 0° - Khóc Lóc & Rơi Lệ (Crying Tears)',
    subtitle: 'Ngũ quan chính diện khóc nấc, những giọt lệ trong suốt lăn dài từ khóe mắt',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😭',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'crying', 'tears', 'weeping', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Loop',
      keyPoints: ['Nước mắt rưng rưng rồi trào ra', 'Miệng run rẩy kìm nén'],
    },
    infoNote: '💡 Biểu cảm đau đớn tột cùng, rơi lệ xúc động hoặc bi thương.',
    negativePrompt: `hair, ears, head outline, neck, body, smile, happy`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) CRYING TEARS STREAMING ON PURE CHROMA-KEY GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Nose, Mouth, and translucent sparkling tear droplets.
MOTION: Eyes brim with large shimmering anime tears that continuously spill over lower lids and trickle down the invisible cheeks. Quivering trembling lip parted in sorrowful sob. Seamless loop.`,
  },
  {
    id: 'face_0_fear',
    title: 'Face 0° - Sợ Hãi & Hoảng Loạn (Terrified / Fear)',
    subtitle: 'Ngũ quan chính diện đồng tử co rút, lông mày run rẩy tột cùng hoảng sợ',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😨',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'fear', 'scared', 'panic', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Loop',
      keyPoints: ['Đồng tử co thắt nhỏ xíu', 'Miệng run lẩy bẩy', 'Đổ mồ hôi hột'],
    },
    infoNote: '💡 Dùng khi đối mặt quái vật khổng lồ hoặc nguy hiểm cận kề.',
    negativePrompt: `hair, ears, head outline, neck, body, smile, brave`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) TERRIFIED FEAR PANIC ON PURE CHROMA-KEY GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Nose, Mouth + subtle nervous sweat drops.
MOTION: Pupils shrink to pinpricks vibrating with terror. Eyebrows raised and twitching irregularly. Mouth open in a silent breathless shudder, teeth chattering softly. Seamless loop.`,
  },
  {
    id: 'face_0_smirk',
    title: 'Face 0° - Cười Nhếch Mép & Ngạo Nghễ (Smirk / Cocky)',
    subtitle: 'Ngũ quan chính diện cười khẩy nửa miệng, ánh mắt sắc lẹm đầy tự tin',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😏',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'smirk', 'cocky', 'confident', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Loop',
      keyPoints: ['Một bên khóe môi nhếch cao', 'Ánh mắt nửa cười nửa khinh thường'],
    },
    infoNote: '💡 Biểu cảm nhân vật phản diện quyến rũ hoặc thiên tài tự tin chiến thắng.',
    negativePrompt: `hair, ears, head outline, neck, body, crying, sad`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) ARROGANT COCKY SMIRK ON PURE CHROMA-KEY GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Nose, Mouth.
MOTION: One eyebrow subtly arches while the other stays sharp and level. Right corner of mouth lifts into a razor-sharp arrogant smirk. Eyes narrow with supreme confidence and gleaming glint. Seamless loop.`,
  },
  {
    id: 'face_0_shy',
    title: 'Face 0° - E Thẹn & Đỏ Mặt (Blushing Shy)',
    subtitle: 'Ngũ quan chính diện mắt bẽn lẽn nhìn xuống, vệt má hồng đỏ ửng ngượng ngùng',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😳',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'shy', 'blush', 'cute', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Loop',
      keyPoints: ['Vệt ửng hồng phớt nhẹ hai gò má', 'Mắt liếc xuống ngượng ngùng', 'Môi chúm chím'],
    },
    infoNote: '💡 Dùng cho các phân cảnh tỏ tình, khen ngợi hoặc xấu hổ.',
    negativePrompt: `hair, ears, head outline, neck, body, angry, fierce`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) SHY BLUSHING CUTENESS ON PURE CHROMA-KEY GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Nose, Mouth, and soft gradient pink blush marks beneath eyes.
MOTION: Eyes dart downward and sideways shyly with fluttery fast blinking. Soft cute pink blush warms up under eyes. Lips press together in bashful smile. Seamless loop.`,
  },
  {
    id: 'face_0_focus',
    title: 'Face 0° - Quyết Tâm & Tập Trung (Focused / Determined)',
    subtitle: 'Ngũ quan chính diện ánh mắt sáng rực kiên định, chuẩn bị xuất kích',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '🔥',
    promptType: 'video',
    tags: ['face', '0deg', 'front', 'focus', 'determined', 'resolve', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front',
      loopType: 'Seamless Loop',
      keyPoints: ['Lông mày hạ thấp kiên nghị', 'Ánh mắt không chớp nhìn thẳng mục tiêu'],
    },
    infoNote: '💡 Biểu cảm thể hiện ý chí thép, trước khi tung đòn quyết định.',
    negativePrompt: `hair, ears, head outline, neck, body, crying, silly`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) UNWAVERING DETERMINED FOCUS ON PURE CHROMA-KEY GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Nose, Mouth.
MOTION: Eyebrows set firmly straight. Eyes blaze with fierce unblinking concentration and steady golden-white catchlights. Mouth set in a firm, resolute line of absolute resolve. Seamless loop.`,
  },
];
