import { PromptItem } from '../../types';

export const FACE_SPECIAL_PROMPTS: PromptItem[] = [
  // ─── 1. HÀI HƯỚC / TRÊU ĐÙA (4 GÓC) ───
  {
    id: 'face_0_funny',
    title: 'Face 0° - Hài Hước & Trêu Đùa (Funny 0° Front)',
    subtitle: 'Ngũ quan chính diện cười toe toét mắt nhắm tít, thè lưỡi trêu đùa 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '🤪',
    promptType: 'video',
    tags: ['face', 'funny', '0deg', 'front', 'playful', 'derp', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 0° Front', loopType: 'Seamless Loop', keyPoints: ['Mắt nhắm tít chữ D ngược', 'Miệng cười ngoác thè lưỡi'] },
    infoNote: '💡 Biểu cảm hài hước tấu hài chính diện anime chibi.',
    negativePrompt: `hair, ears, head outline, realistic teeth, scary, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) COMEDIC PLAYFUL DERP FACE ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Mouth, cheek blush. NO hair/ears/head.
MOTION: Frame 0-1s: Eyes squeeze shut into cartoon crescent arches (> <), mouth opens wide in laughter. Frame 1-2s: Winks one eye with star catchlight, pink tongue pokes out corner of mouth (:P). Frame 2-3s: Returns smoothly to start.`,
  },
  {
    id: 'face_45_funny',
    title: 'Face 45° - Hài Hước Góc 3/4 (Funny 45° Three-Quarter)',
    subtitle: 'Ngũ quan xoay trái 45° nháy mắt tinh nghịch, cười toe toét 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '🤪',
    promptType: 'video',
    tags: ['face', 'funny', '45deg', 'left', 'playful', 'derp', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 45° Left', loopType: 'Seamless Loop', keyPoints: ['Trục xoay 45°', 'Nháy mắt tinh quái', 'Má ửng hồng'] },
    infoNote: '💡 Biểu cảm hài hước góc 3/4 cực kỳ sinh động trong hội thoại.',
    negativePrompt: `hair, ears, head outline, scary, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (45° LEFT THREE-QUARTER VIEW) PLAYFUL COMEDIC FACE ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 45° Eyebrows, Eyes, Mouth. NO hair/ears/head.
MOTION: 45° perspective. Right eye winks tight with bouncy anime star effect, mouth curves up in asymmetric grinning chuckle with cute tongue tip showing. Seamless loop.`,
  },
  {
    id: 'face_90_funny',
    title: 'Face 90° - Hài Hước Nhìn Ngang (Funny 90° Side Profile)',
    subtitle: 'Ngũ quan nhìn ngang 90° cười híp mắt ngoác miệng, giọt mồ hôi anime 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '🤪',
    promptType: 'video',
    tags: ['face', 'funny', '90deg', 'side', 'comedy', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 90° Side', loopType: 'Seamless Loop', keyPoints: ['Trục nghiêng 90°', 'Mắt cong cười híp', 'Khẩu hình mở to'] },
    infoNote: '💡 Góc nghiêng tấu hài khi nhân vật cười ngượng hoặc bị bắt bài.',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (90° SIDE PROFILE VIEW) COMEDIC LAUGHING ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY single side profile eye, eyebrow, mouth on 90° axis.
MOTION: 90° profile eye crinkles into sharp happy chevron (>), profile mouth snaps open wide in joyful cartoon guffaw, slight quivering laugh vibration. Seamless loop.`,
  },
  {
    id: 'face_135_funny',
    title: 'Face 135° - Hài Hước Ngoái Nhìn (Funny 135° Over-Shoulder)',
    subtitle: 'Ngũ quan ngoái nhìn qua vai 135° cười trêu chọc đối phương 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '🤪',
    promptType: 'video',
    tags: ['face', 'funny', '135deg', 'glance', 'tease', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 135° Back Glance', loopType: 'Seamless Loop', keyPoints: ['Ngoái nhìn 135°', 'Ánh mắt tinh quái', 'Nụ cười trêu tức'] },
    infoNote: '💡 Vừa chạy vừa ngoái lại trêu chọc đối thủ.',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (135° OVER-SHOULDER GLANCE) PLAYFUL TEASING ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 135° reverse-angle eye and mouth corner.
MOTION: Eye glances sharply back toward viewer with mischievous glimmer, mouth corner smirks playfully with cheeky chuckle. Seamless loop.`,
  },

  // ─── 2. NHAM HIỂM / TÀ KHÍ (4 GÓC) ───
  {
    id: 'face_0_sinister',
    title: 'Face 0° - Nham Hiểm & Tà Khí (Sinister 0° Front)',
    subtitle: 'Ánh mắt sắc lẹm, nụ cười nhếch mép gian xảo chính diện 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '😈',
    promptType: 'video',
    tags: ['face', 'sinister', '0deg', 'front', 'evil', 'villain', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 0° Front', loopType: 'Breathing Loop', keyPoints: ['Mắt hẹp đồng tử đỏ', 'Khóe cười tà khí'] },
    infoNote: '💡 Biểu cảm phản diện, mưu kế độc địa chính diện.',
    negativePrompt: `hair, ears, head outline, friendly, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) SINISTER SCHEMING SMILE ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Mouth, dark eye shadows. NO hair/ears/head.
MOTION: Narrowed predatory eyes with sharp red pupils, slanted angular eyebrows. Mouth curls into asymmetric sinister smirk widening with chilling confidence. Seamless loop.`,
  },
  {
    id: 'face_45_sinister',
    title: 'Face 45° - Nham Hiểm Góc 3/4 (Sinister 45° Three-Quarter)',
    subtitle: 'Ánh mắt nhìn xéo 3/4 sắc bén, nụ cười nửa miệng tà ác 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '😈',
    promptType: 'video',
    tags: ['face', 'sinister', '45deg', 'left', 'evil', 'scheming', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 45° Left', loopType: 'Breathing Loop', keyPoints: ['Góc 3/4 nhìn xéo', 'Khóe môi nhọn tà khí'] },
    infoNote: '💡 Góc vàng biểu thị mưu toan gian trá trong phim hoạt hình.',
    negativePrompt: `hair, ears, head outline, happy, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (45° THREE-QUARTER VIEW) SINISTER EVIL SMIRK ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 45° angled Eyebrows, Eyes, Mouth.
MOTION: 45° angled gaze peering from beneath furrowed brow, pupils contracted into dangerous slit, mouth corner curls into a wickedly sharp villainous smile. Seamless loop.`,
  },
  {
    id: 'face_90_sinister',
    title: 'Face 90° - Nham Hiểm Nhìn Ngang (Sinister 90° Side Profile)',
    subtitle: 'Góc nghiêng 90° khóe miệng nhọn như đao kiếm, ánh nhìn lạnh băng 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '😈',
    promptType: 'video',
    tags: ['face', 'sinister', '90deg', 'side', 'cold', 'villain', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 90° Side', loopType: 'Seamless Loop', keyPoints: ['Mặt bên 90°', 'Khóe miệng nhếch cao', 'Đồng tử hẹp'] },
    infoNote: '💡 Góc nghiêng kinh điển khi kẻ phản diện độc thoại hoặc lộ sát khí.',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (90° SIDE VIEW) COLD SINISTER PROFILE ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 90° side profile eye, brow, mouth.
MOTION: 90° profile eye glares with frozen cruelty, profile mouth corner draws upward into a sharp chilling razor smirk. Subtle menacing pulse in iris catchlight. Seamless loop.`,
  },
  {
    id: 'face_135_sinister',
    title: 'Face 135° - Nham Hiểm Ngoái Nhìn (Sinister 135° Over-Shoulder)',
    subtitle: 'Ngoái đầu qua vai 135° liếc nhìn đầy sát ý và mưu hiểm 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '😈',
    promptType: 'video',
    tags: ['face', 'sinister', '135deg', 'back', 'glance', 'assassin', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 135° Back Glance', loopType: 'Seamless Loop', keyPoints: ['Ngoái nhìn sau lưng', 'Ánh mắt đỏ phát sáng'] },
    infoNote: '💡 Khoảnh khắc sát thủ trước khi ra tay hoặc quay lưng phản bội.',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (135° REAR GLANCE) MENACING OVER-SHOULDER STARE ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 135° angled eye and mouth line.
MOTION: Single visible eye cuts back sharply over shoulder with glowing crimson pupil, mouth corner pulls tight in a deadly suppressed smirk. Seamless loop.`,
  },

  // ─── 3. BÍ HIỂM / KHÓ ĐOÁN (4 GÓC) ───
  {
    id: 'face_0_mysterious',
    title: 'Face 0° - Bí Hiểm & Khó Đoán (Mysterious 0° Front)',
    subtitle: 'Đôi mắt sâu thẳm tĩnh lặng, nụ cười nửa miệng phảng phất vẻ huyền bí 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '🎭',
    promptType: 'video',
    tags: ['face', 'mysterious', '0deg', 'front', 'enigmatic', 'poker', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 0° Front', loopType: 'Seamless Loop', keyPoints: ['Mắt sâu không đáy', 'Môi khép hờ bí ẩn'] },
    infoNote: '💡 Tạo vẻ thần bí cho nhân vật mưu sĩ, cao nhân hoặc nội gián.',
    negativePrompt: `hair, ears, head outline, goofy, crying, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) MYSTERIOUS ENIGMATIC EXPRESSION ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Mouth.
MOTION: Serene, deep eyes with soft glowing highlights. Subtle micro-smile at closed lips. Slow deliberate blink at 1.5s, opening with tranquil depth. Seamless loop.`,
  },
  {
    id: 'face_45_mysterious',
    title: 'Face 45° - Bí Hiểm Góc 3/4 (Mysterious 45° Three-Quarter)',
    subtitle: 'Nghiêng 45° trầm mặc, ánh mắt đăm chiêu sâu thẳm khó đoán 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '🎭',
    promptType: 'video',
    tags: ['face', 'mysterious', '45deg', 'left', 'enigmatic', 'calm', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 45° Left', loopType: 'Seamless Loop', keyPoints: ['Góc 3/4 trầm tư', 'Khóe môi mỉm nhẹ khó dò'] },
    infoNote: '💡 Biểu cảm của cao nhân khi giải thích thiên cơ hoặc lắng nghe bí mật.',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (45° THREE-QUARTER VIEW) ENIGMATIC SAGE EXPRESSION ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 45° Eyebrows, Eyes, Mouth.
MOTION: 45° three-quarter gaze poised in calm insight, eyes gently refocusing as if reading fate, corners of mouth remain impeccably composed with faint enigmatic serenity. Seamless loop.`,
  },
  {
    id: 'face_90_mysterious',
    title: 'Face 90° - Bí Hiểm Nhìn Ngang (Mysterious 90° Side Profile)',
    subtitle: 'Nhìn ngang 90° trầm ngâm ngắm nhìn chân trời xa xăm 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '🎭',
    promptType: 'video',
    tags: ['face', 'mysterious', '90deg', 'side', 'sage', 'contemplate', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 90° Side', loopType: 'Seamless Loop', keyPoints: ['Mặt bên 90°', 'Ánh mắt xa xăm', 'Môi khép hờ'] },
    infoNote: '💡 Cảnh nhân vật đứng bên vách núi nhìn mây trôi suy ngẫm việc đời.',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (90° SIDE VIEW) MYSTERIOUS CONTEMPLATIVE GAZE ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 90° side profile features.
MOTION: Profile eye gazes serenely toward the horizon with cosmic depth, calm eyelid lowers slightly in philosophical contemplation. Seamless loop.`,
  },
  {
    id: 'face_135_mysterious',
    title: 'Face 135° - Bí Hiểm Ngoái Nhìn (Mysterious 135° Over-Shoulder)',
    subtitle: 'Ngoái nhìn qua vai 135° với ánh mắt sâu thẳm giấu kín tâm sự 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '🎭',
    promptType: 'video',
    tags: ['face', 'mysterious', '135deg', 'back', 'secret', 'glance', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 135° Back Glance', loopType: 'Seamless Loop', keyPoints: ['Ngoái nhìn sau lưng', 'Ánh mắt bí ẩn'] },
    infoNote: '💡 Trước khi rời đi, nhân vật quay lại nhìn một cái nhìn ẩn chứa ngàn lời nói.',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (135° OVER-SHOULDER GLANCE) MYSTERIOUS PARTING GAZE ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 135° eye and mouth line.
MOTION: Eye turns back over shoulder with unspoken secrets and haunting stillness, holding a lingering enigmatic look before steadying. Seamless loop.`,
  },

  // ─── 4. KHINH BỈ / COI THƯỜNG (4 GÓC) ───
  {
    id: 'face_0_disdain',
    title: 'Face 0° - Khinh Bỉ & Coi Thường (Disdain 0° Front)',
    subtitle: 'Nửa mi mắt sụp xuống nhìn từ trên cao, khóe môi trễ xuống lạnh lùng 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '😒',
    promptType: 'video',
    tags: ['face', 'disdain', '0deg', 'front', 'scorn', 'cold', 'arrogant', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 0° Front', loopType: 'Seamless Loop', keyPoints: ['Mắt nhìn xếch xuống', 'Một bên mày nhướng nhẹ'] },
    infoNote: '💡 Thiên kim ngạo kiều hoặc đối thủ coi thường kẻ khác.',
    negativePrompt: `hair, ears, head outline, laughing, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) COLD DISDAIN & SCORN ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Mouth.
MOTION: Half-lidded eyes casting condescending downward stare, one eyebrow arched higher in haughtiness, small pouty mouth pulled slightly downward in distaste. Seamless loop.`,
  },
  {
    id: 'face_45_disdain',
    title: 'Face 45° - Khinh Bỉ Góc 3/4 (Disdain 45° Three-Quarter)',
    subtitle: 'Liếc nhìn xéo góc 45° coi thường không thèm để vào mắt 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '😒',
    promptType: 'video',
    tags: ['face', 'disdain', '45deg', 'left', 'scorn', 'haughty', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 45° Left', loopType: 'Seamless Loop', keyPoints: ['Góc 3/4 liếc xuống', 'Miệng bĩu nhẹ'] },
    infoNote: '💡 Khiêu khích đối phương trong tranh luận hoặc thi đấu.',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (45° THREE-QUARTER VIEW) HAUGHTY DISDAINFUL GLARE ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 45° Eyebrows, Eyes, Mouth.
MOTION: 45° downward side-eye glare full of dismissal, upper lip twitches in subtle arrogant scoff. Seamless loop.`,
  },
  {
    id: 'face_90_disdain',
    title: 'Face 90° - Khinh Bỉ Nhìn Ngang (Disdain 90° Side Profile)',
    subtitle: 'Hếch cằm ngửa mặt nhìn ngang 90°, khóe môi trễ xuống ngạo mạn 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '😒',
    promptType: 'video',
    tags: ['face', 'disdain', '90deg', 'side', 'arrogance', 'proud', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 90° Side', loopType: 'Seamless Loop', keyPoints: ['Mặt bên hếch cằm', 'Mắt cụp nửa mi'] },
    infoNote: '💡 Tư thế ngạo mạn tuyệt đối của kẻ bề trên.',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (90° SIDE VIEW) PROUD CONDESCENDING PROFILE ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 90° side profile features.
MOTION: Chin elevated in haughty contempt, heavy half-closed eyelid looking downward along cheekline, profile mouth pursed in aloof displeasure. Seamless loop.`,
  },
  {
    id: 'face_135_disdain',
    title: 'Face 135° - Khinh Bỉ Ngoái Nhìn (Disdain 135° Over-Shoulder)',
    subtitle: 'Ngoái nhìn qua vai 135° hờ hững khinh thị rồi quay đi 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '😒',
    promptType: 'video',
    tags: ['face', 'disdain', '135deg', 'back', 'dismissal', 'cold', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 135° Back Glance', loopType: 'Seamless Loop', keyPoints: ['Liếc mắt hờ hững', 'Không thèm để tâm'] },
    infoNote: '💡 "Ngươi không xứng làm đối thủ của ta."',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (135° OVER-SHOULDER GLANCE) DISMISSIVE COLD GLARE ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 135° eye and mouth line.
MOTION: Eye sweeps back with cold indifference, pauses with a chilling look of bored superiority, mouth line unmoved and cold. Seamless loop.`,
  },

  // ─── 5. CHOÁNG VÁNG / MẮT XOẮN ỐC (4 GÓC) ───
  {
    id: 'face_0_dizzy',
    title: 'Face 0° - Choáng Váng & Mắt Xoắn Ốc (Dizzy 0° Front)',
    subtitle: 'Đồng tử xoay vòng xoắn ốc (@ @), miệng lượn sóng ngơ ngác chính diện 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '😵',
    promptType: 'video',
    tags: ['face', 'dizzy', '0deg', 'front', 'spiral', 'stunned', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 0° Front', loopType: 'Continuous Loop', keyPoints: ['Đồng tử xoay xoắn ốc', 'Miệng lượn sóng run run'] },
    infoNote: '💡 Hoạt cảnh trúng đòn choáng (Stun), say rượu hoặc ngất xỉu.',
    negativePrompt: `hair, ears, head outline, sharp pupils, angry, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (0° FRONT VIEW) DIZZY SPIRAL EYES ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY Eyebrows, Eyes, Mouth.
MOTION: Cartoon spiral pupils (@ @) spinning steadily clockwise in dizzy stupor, wavy squiggly mouth trembling softly, eyebrows wobbling asynchronously. Seamless loop.`,
  },
  {
    id: 'face_45_dizzy',
    title: 'Face 45° - Choáng Váng Góc 3/4 (Dizzy 45° Three-Quarter)',
    subtitle: 'Mắt xoắn ốc nghiêng 45° chao đảo lảo đảo 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '😵',
    promptType: 'video',
    tags: ['face', 'dizzy', '45deg', 'left', 'spiral', 'wobbly', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 45° Left', loopType: 'Continuous Loop', keyPoints: ['Góc 3/4 chao đảo', 'Mắt xoay xoắn ốc'] },
    infoNote: '💡 Lảo đảo mất thăng bằng góc 3/4 sau khi bị ăn đòn.',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (45° THREE-QUARTER VIEW) DIZZY SPIRAL EYES ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 45° Eyebrows, Eyes, Mouth.
MOTION: 45° perspective. Both eyes exhibit spinning stylized anime spirals wobbling in disorientation, wavy mouth slack and confused. Seamless loop.`,
  },
  {
    id: 'face_90_dizzy',
    title: 'Face 90° - Choáng Váng Nhìn Ngang (Dizzy 90° Side Profile)',
    subtitle: 'Mắt xoay tít góc nghiêng 90°, miệng há hốc ngơ ngác 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '😵',
    promptType: 'video',
    tags: ['face', 'dizzy', '90deg', 'side', 'faint', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 90° Side', loopType: 'Continuous Loop', keyPoints: ['Mặt bên 90°', 'Xoắn ốc quay tròn'] },
    infoNote: '💡 Choáng váng nhìn từ góc bên cạnh.',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (90° SIDE VIEW) DIZZY PROFILE CONFUSION ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 90° side profile features.
MOTION: Profile eye features a spinning hypnotic anime spiral (@), open slack jaw softly undulating in groggy stupor. Seamless loop.`,
  },
  {
    id: 'face_135_dizzy',
    title: 'Face 135° - Choáng Váng Ngoái Nhìn (Dizzy 135° Over-Shoulder)',
    subtitle: 'Ngoái nhìn sau lưng 135° với ánh mắt xoắn ốc lảo đảo mất phương hướng 2-3s lặp',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đặc Biệt',
    icon: '😵',
    promptType: 'video',
    tags: ['face', 'dizzy', '135deg', 'back', 'disoriented', 'loop'],
    videoGuide: { duration: '2 - 3s', fps: '24 / 30 FPS', camera: 'Static Lock 135° Back Glance', loopType: 'Continuous Loop', keyPoints: ['Ngoái nhìn sau lưng', 'Mắt xoay hoa mắt'] },
    infoNote: '💡 Bị đánh bay hoặc quay cuồng nhìn về phía sau.',
    negativePrompt: `hair, ears, head outline, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (135° OVER-SHOULDER GLANCE) DISORIENTED DIZZY STARE ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY 135° eye and mouth line.
MOTION: Glancing eye spins with cartoon dizzy spiral (@), brows crooked and shaking, mouth line wobbling in dizzy bewilderment. Seamless loop.`,
  },

  // ─── TƯƠNG THÍCH NGƯỢC (ALIASES) ───
  { id: 'face_funny', title: 'Hài Hước (Mặc định 0°)', subtitle: 'Alias 0°', stepCategory: 'step4_face', stepLabel: 'Bước 4', icon: '🤪', promptType: 'video', tags: ['face', 'funny'], negativePrompt: '', rawPrompt: 'TASK: Refer to face_0_funny' },
  { id: 'face_sinister', title: 'Nham Hiểm (Mặc định 0°)', subtitle: 'Alias 0°', stepCategory: 'step4_face', stepLabel: 'Bước 4', icon: '😈', promptType: 'video', tags: ['face', 'sinister'], negativePrompt: '', rawPrompt: 'TASK: Refer to face_0_sinister' },
  { id: 'face_mysterious', title: 'Bí Hiểm (Mặc định 0°)', subtitle: 'Alias 0°', stepCategory: 'step4_face', stepLabel: 'Bước 4', icon: '🎭', promptType: 'video', tags: ['face', 'mysterious'], negativePrompt: '', rawPrompt: 'TASK: Refer to face_0_mysterious' },
  { id: 'face_disdain', title: 'Khinh Bỉ (Mặc định 0°)', subtitle: 'Alias 0°', stepCategory: 'step4_face', stepLabel: 'Bước 4', icon: '😒', promptType: 'video', tags: ['face', 'disdain'], negativePrompt: '', rawPrompt: 'TASK: Refer to face_0_disdain' },
  { id: 'face_dizzy', title: 'Choáng Váng (Mặc định 0°)', subtitle: 'Alias 0°', stepCategory: 'step4_face', stepLabel: 'Bước 4', icon: '😵', promptType: 'video', tags: ['face', 'dizzy'], negativePrompt: '', rawPrompt: 'TASK: Refer to face_0_dizzy' },
];
