import { PromptItem } from '../../types';

export const FACE_90_PROMPTS: PromptItem[] = [
  {
    id: 'face_90_profile',
    title: 'Face 90° - Chớp Mắt Nhìn Nghiêng (Side Profile Blink)',
    subtitle: 'Ngũ quan nhìn ngang 90° sang trái chớp mắt tự nhiên 2-3s lặp vô tận trên nền xanh #00FF00',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '👀',
    promptType: 'video',
    tags: ['face', '90deg', 'side', 'profile', 'blink', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Left Side View (Face Only)',
      loopType: 'Seamless Loop',
      keyPoints: [
        'Chỉ thấy 1 mắt nhìn nghiêng hình tam giác nhọn đặc trưng anime',
        'Sống mũi nhô ra bên trái, môi trên môi dưới nhìn ngang',
        'Nền #00FF00',
      ],
    },
    infoNote: '💡 Trạng thái nghỉ cơ bản cho nhân vật ở góc nhìn nghiêng 90°.',
    negativePrompt: `hair, ears, head outline, neck, body, front view 0deg, 45deg, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (90° FULL LEFT SIDE PROFILE) NEUTRAL BLINK ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY single side-profile eyebrow, triangular profile eye, nose bridge/tip silhouette pointing left, and profile mouth contour. NO hair, NO ears, NO skull outline, NO neck.
MOTION: Profile eye blinks naturally with smooth lid motion in pure side view (90°). Calm breathing. Seamless loop.`,
  },
  {
    id: 'face_90_talk',
    title: 'Face 90° - Nói Chuyện Nhìn Nghiêng (Side Profile Talking)',
    subtitle: 'Ngũ quan nhìn ngang 90° cử động môi nói chuyện linh hoạt theo nhịp thoại',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '🗣️',
    promptType: 'video',
    tags: ['face', '90deg', 'side', 'profile', 'talk', 'dialogue', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Left View',
      loopType: 'Seamless Loop',
      keyPoints: ['Môi trên dưới mở đóng theo biên độ nhìn nghiêng', 'Lông mày nhấp nhô sống động'],
    },
    infoNote: '💡 Dùng để lồng thoại trong cảnh nhân vật bước đi và nói chuyện nhìn ngang.',
    negativePrompt: `hair, ears, head outline, neck, body, closed mouth static, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (90° LEFT SIDE PROFILE) TALKING LIP SYNC ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY single profile eyebrow, side eye, nose tip, and profile lips.
MOTION: Profile lips articulate speech clearly opening and closing in varied expressive shapes facing left edge. Natural eye blinks included. Eyebrow arches with tone. Seamless loop.`,
  },
  {
    id: 'face_90_smile',
    title: 'Face 90° - Mỉm Cười Nhìn Nghiêng (Side Profile Smile)',
    subtitle: 'Ngũ quan nhìn ngang 90° mỉm cười thanh tao, khóe môi khẽ cong lên nhìn nghiêng',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😊',
    promptType: 'video',
    tags: ['face', '90deg', 'side', 'smile', 'happy', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Left View',
      loopType: 'Seamless Loop',
      keyPoints: ['Khóe môi cong lên góc 90°', 'Mắt híp cong hạnh phúc'],
    },
    infoNote: '💡 Nụ cười dịu dàng nhìn nghiêng, rất thơ mộng và tao nhã.',
    negativePrompt: `hair, ears, head outline, neck, body, frown, crying`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (90° LEFT SIDE PROFILE) GENTLE SERENE SMILE ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY profile eyebrow, side eye, nose, and lips.
MOTION: Side-profile lips soften and lift into an elegant serene smile. Triangular profile eye curves into smiling arch with warm glimmer. Seamless loop.`,
  },
  {
    id: 'face_90_angry',
    title: 'Face 90° - Nghiến Răng Giận Dữ (Side Profile Angry Gritting)',
    subtitle: 'Ngũ quan nhìn ngang 90° lông mày chúc xuống, răng nghiến chặt trừng mắt',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😠',
    promptType: 'video',
    tags: ['face', '90deg', 'side', 'angry', 'fierce', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Left View',
      loopType: 'Seamless Loop',
      keyPoints: ['Lông mày găm xuống sắc bén', 'Răng nghiến chặt nhìn nghiêng'],
    },
    infoNote: '💡 Biểu cảm chiến đấu căng thẳng nhìn từ cạnh bên.',
    negativePrompt: `hair, ears, head outline, neck, body, smile, laughing`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (90° LEFT SIDE PROFILE) FIERCE ANGRY GRIT ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY profile eyebrow, side eye, nose, mouth.
MOTION: Profile eyebrow drops low and menacing over the narrowed glaring eye. Profile lips pull back slightly exposing tightly gritted teeth facing left. Intense breath twitch. Seamless loop.`,
  },
  {
    id: 'face_90_surprised',
    title: 'Face 90° - Ngạc Nhiên Nhìn Nghiêng (Side Profile Shock)',
    subtitle: 'Ngũ quan nhìn ngang 90° mắt mở to, miệng há nhỏ thốt lên kinh ngạc',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😲',
    promptType: 'video',
    tags: ['face', '90deg', 'side', 'surprised', 'shock', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Left View',
      loopType: 'Seamless Loop',
      keyPoints: ['Mắt mở tròn ở góc nghiêng', 'Môi hé mở bất ngờ'],
    },
    infoNote: '💡 Phản ứng bất ngờ khi chứng kiến sự việc bất thường ở hướng bên.',
    negativePrompt: `hair, ears, head outline, neck, body, sleepy, calm`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (90° LEFT SIDE PROFILE) SURPRISED GASP ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY profile eyebrow, eye, nose, mouth.
MOTION: Profile eye expands wide showing bright astonished pupil. Profile lips part in a sudden small gasp toward the left. High-raised eyebrow. Seamless loop.`,
  },
  {
    id: 'face_90_sad',
    title: 'Face 90° - U Sầu Nhìn Xa Xăm (Side Profile Sad Gaze)',
    subtitle: 'Ngũ quan nhìn ngang 90° ánh mắt cụp xuống xa xăm đượm buồn, khóe môi trĩu nhẹ',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😢',
    promptType: 'video',
    tags: ['face', '90deg', 'side', 'sad', 'sorrow', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Left View',
      loopType: 'Seamless Loop',
      keyPoints: ['Ánh mắt nhìn xa buồn bã', 'Hơi thở dài qua làn môi nghiêng'],
    },
    infoNote: '💡 Góc quay nghệ thuật cho các phân cảnh hồi tưởng hoặc cô đơn.',
    negativePrompt: `hair, ears, head outline, neck, body, smile, happy`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (90° LEFT SIDE PROFILE) DISTANT POIGNANT SADNESS ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY profile eyebrow, side eye, nose, lips.
MOTION: Profile eye gazes downward into the distance with subtle wet glisten. Slow heavy blink. Corner of profile mouth angles softly down in quiet sigh. Seamless loop.`,
  },
  {
    id: 'face_90_cry',
    title: 'Face 90° - Giọt Lệ Lăn Dài (Side Profile Tear Drop)',
    subtitle: 'Ngũ quan nhìn ngang 90° giọt nước mắt trong suốt rơi lăn chậm dọc sống mũi má nghiêng',
    stepCategory: 'step4_face',
    stepLabel: 'Bước 4: Biểu Cảm Đơn',
    icon: '😭',
    promptType: 'video',
    tags: ['face', '90deg', 'side', 'cry', 'tear', 'loop'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Left View',
      loopType: 'Seamless Loop',
      keyPoints: ['Giọt lệ trào ra từ mắt nghiêng', 'Môi mím run'],
    },
    infoNote: '💡 Phân cảnh rơi lệ kinh điển trong các thước phim hoạt hình xúc động.',
    negativePrompt: `hair, ears, head outline, neck, body, smile, laughing`,
    rawPrompt: `TASK: Image-to-video 2-3 second seamless loop FLOATING 2D FACIAL FEATURES (90° LEFT SIDE PROFILE) TEAR ROLLING DOWN ON PURE CHROMA GREEN.
CRITICAL ISOLATION: Pure chroma green #00FF00. ONLY profile eyebrow, side eye, nose, mouth + sparkling tear droplet.
MOTION: A shimmering single tear forms at the corner of the side eye and rolls slowly down the cheek contour. Profile lips tremble gently. Seamless loop.`,
  },
];
