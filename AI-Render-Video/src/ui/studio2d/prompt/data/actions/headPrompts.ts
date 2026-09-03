import { PromptItem } from '../../types';

export const HEAD_PROMPTS: PromptItem[] = [
  // ─── 1. GÓC 0° CHÍNH DIỆN ───
  {
    id: 'head_angle0',
    title: 'Cử Động Đầu - 0° Chính Diện (0° Front Head Motion & Nod)',
    subtitle: 'Gật đầu tán thành kết hợp lắc nhẹ và nghiêng đầu trực diện 0° đối mặt camera 2-3s lặp',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Cử Động Đầu',
    icon: '👤',
    promptType: 'video',
    tags: ['head', '0deg', 'front', 'nod', 'shake', 'dialogue', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 0° Front View (Upper Body / Head Focus)',
      loopType: 'Seamless Loop',
      keyPoints: [
        'Khung hình tĩnh khóa trực diện chính diện 0°',
        'Thân người, vai và lồng ngực đứng im tuyệt đối',
        'Đầu gật nhẹ dứt khoát 1 nhịp → ngước lên → nghiêng nhẹ 5° suy tư',
        'Tóc mái và trang sức trâm cài lay động mềm mại theo quán tính',
      ],
    },
    infoNote: '💡 Hoạt ảnh cử động đầu trực diện 0° dùng cho đối thoại, chào hỏi, biểu thị cảm xúc và tạo sự sống động tự nhiên.',
    negativePrompt: `eyes, eyebrows, mouth, nose, facial expression, body turning, body rotation, walking, arms flailing, floating, rotating camera, 3D CGI rendering, photorealism, low quality, distorted neck`,
    rawPrompt: `TASK: Image-to-video 2-3 second animation: NATURAL HEAD MOTION & AFFIRMATIVE NOD (0° DIRECT FRONT VIEW).
Use the 0° front faceless anime mannequin reference image as LOCKED IDENTITY SOURCE. ONLY ANIMATE HEAD.

CHARACTER LOCK:
- Keep completely faceless smooth head (DO NOT generate eyes, nose, or mouth)
- Lock hairstyle, twin braids, jade hairpin, daoist robe costume, colors, and proportions exactly as reference
- Shoulders, arms, torso, and hips remain 100% still and level facing camera

HEAD MOTION TIMELINE (0° FRONT VIEW):
- Frame 0.0 - 0.8s: Head smoothly nods downward (chin lowers ~15° in polite affirmative acknowledgment)
- Frame 0.8 - 1.6s: Head elevates back to neutral level eye-line with a gentle natural overshoot
- Frame 1.6 - 2.4s: Subtle 5° inquisitive tilt to the right, then returns perfectly centered
- Secondary Physics: Front fringe/bangs and floating hair ribbons dip downward and swing rhythmically following head cadence

SEAMLESS LOOP:
- Frame 1 matches final resting frame exactly
- Chroma green #00FF00 background.`,
  },

  // ─── 2. GÓC 45° XOAY TRÁI ───
  {
    id: 'head_angle45',
    title: 'Cử Động Đầu - 45° Xoay Trái (45° Three-Quarter Head Motion)',
    subtitle: 'Nghiêng đầu, gật chào và ngoảnh nhẹ ở góc 3/4 sang bên trái 2-3s lặp',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Cử Động Đầu',
    icon: '↙️',
    promptType: 'video',
    tags: ['head', '45deg', 'left', 'nod', 'tilt', 'dialogue', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 45° Left View',
      loopType: 'Seamless Loop',
      keyPoints: [
        'Góc quay 45° 3/4 bên trái tạo chiều sâu điện ảnh',
        'Thân trên giữ nguyên tư thế 45°',
        'Đầu gật nhẹ tán thành rồi hơi ngoảnh sang 10° quan sát',
        'Lọn tóc bên má đung đưa tự nhiên',
      ],
    },
    infoNote: '💡 Góc 45° là góc vàng cho các cảnh đối thoại 2 người trong phim hoạt hình anime.',
    negativePrompt: `eyes, mouth, nose, facial features, body rotating, footsteps, moving arms, 3D CGI, blurry silhouette, distorted proportions`,
    rawPrompt: `TASK: Image-to-video 2-3 second animation: THREE-QUARTER HEAD MOTION & NOD (45° LEFT PERSPECTIVE).
Use the 45° left faceless reference image as LOCKED IDENTITY SOURCE. ONLY ANIMATE HEAD.

CHARACTER LOCK:
- Faceless featureless head surface (NO eyes, NO mouth, NO nose)
- Maintain identical hair color, clothing details, and chibi anime silhouette
- Torso locked at 45° diagonal angle, shoulders relaxed and stationary

HEAD MOTION TIMELINE (45° LEFT VIEW):
- Frame 0.0 - 1.0s: Head tilts forward gracefully in a warm 45° affirmative nod (chin moves down along 45° axis)
- Frame 1.0 - 2.0s: Head smoothly rises, turns an additional ~10° to left as if listening intently
- Frame 2.0 - 3.0s: Head rotates smoothly back to initial 45° orientation
- Secondary Dynamics: Flowing hair tufts along the side of face swing with soft elastic bounce

SEAMLESS LOOP:
- Perfectly seamless transition between first and last frame
- Chroma green #00FF00 background.`,
  },

  // ─── 3. GÓC 90° NHÌN NGANG ───
  {
    id: 'head_angle90',
    title: 'Cử Động Đầu - 90° Nhìn Ngang (90° Side Profile Head Motion)',
    subtitle: 'Đầu ngẩng cao suy ngẫm rồi cúi nhẹ trên trục bên nhìn ngang 90° 2-3s lặp',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Cử Động Đầu',
    icon: '⬅️',
    promptType: 'video',
    tags: ['head', '90deg', 'side', 'profile', 'nod', 'look_up', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 90° Left Side Profile View',
      loopType: 'Seamless Loop',
      keyPoints: [
        'Góc nhìn ngang 90° hoàn hảo',
        'Cằm nâng lên ngắm nhìn trời cao → hạ xuống vị trí chuẩn',
        'Đường viền cổ và cằm sắc nét',
        'Đuôi tóc phía sau lay động theo nhịp ngửa/cúi đầu',
      ],
    },
    infoNote: '💡 Rất phù hợp cho cảnh nhân vật đứng suy ngẫm, nghe lời dạy hoặc ngắm trăng sao.',
    negativePrompt: `eyes, mouth, nose, face turning to camera, torso moving, arms swinging, 3D CGI, jitter`,
    rawPrompt: `TASK: Image-to-video 2-3 second animation: PURE SIDE PROFILE HEAD MOVEMENT (90° LEFT VIEW).
Use the 90° side profile faceless reference image.

CHARACTER LOCK:
- Keep faceless blank head (NO eyes/mouth/nose)
- Strict 90° side silhouette lock, maintain exact hairstyle, robes, and color palette
- Body, spine, and chest remain upright and completely immobile

HEAD MOTION TIMELINE (90° SIDE VIEW):
- Frame 0.0 - 1.0s: Chin tilts up ~20° in thoughtful contemplation, looking toward the horizon
- Frame 1.0 - 2.0s: Head lowers softly past neutral position into a gentle 10° downward nod
- Frame 2.0 - 3.0s: Returns gracefully to original horizontal eye-level gaze
- Secondary Physics: Rear ponytail and long back ribbons follow head motion with fluid organic lag

SEAMLESS LOOP:
- Returns to starting horizontal side-profile pose
- Chroma green #00FF00 background.`,
  },

  // ─── 4. GÓC 135° LƯNG PHẢI ───
  {
    id: 'head_angle135',
    title: 'Cử Động Đầu - 135° Lưng Phải (135° Over-Shoulder Glance)',
    subtitle: 'Đầu ngoái nhìn qua vai về phía camera rồi quay lại hướng lưng 135° 2-3s lặp',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Cử Động Đầu',
    icon: '↘️',
    promptType: 'video',
    tags: ['head', '135deg', 'back_right', 'glance', 'look_back', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 135° Back-Right View',
      loopType: 'Seamless Loop',
      keyPoints: [
        'Góc nhìn từ phía sau lưng lệch phải 135°',
        'Đầu quay qua vai 25° hướng về phía người xem',
        'Dừng quan sát 0.6s rồi quay lại góc cũ',
        'Khăn lụa và lọn tóc sau gáy xoay vặn nhẹ theo đốt sống cổ',
      ],
    },
    infoNote: '💡 Động tác điện ảnh kinh điển khi nhân vật quay đầu nhìn lại người đang gọi tên mình.',
    negativePrompt: `full body twist, walking away, arms flailing, facial features, eyes, mouth, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 2-3 second animation: OVER-SHOULDER HEAD TURN & GLANCE (135° BACK-RIGHT VIEW).
Use the 135° back-right faceless reference image.

CHARACTER LOCK:
- Faceless head (clean smooth scalp, no facial features)
- Back of kimono/robes, jade hairpin, and hair silhouette stay 100% faithful
- Torso locked in 135° back-angle stance, no shoulder rotation

HEAD MOTION TIMELINE (135° VIEW):
- Frame 0.0 - 0.8s: Head rotates ~25° over the right shoulder toward camera as if hearing a voice
- Frame 0.8 - 1.8s: Brief pause in observant stance, chin slightly raised with curiosity
- Frame 1.8 - 2.8s: Head rotates smoothly back to original 135° resting orientation
- Secondary Motion: Hair strands along back of neck shift with neck pivot and settle softly

SEAMLESS LOOP:
- Perfectly continuous loop matching start and end frames
- Chroma green #00FF00 background.`,
  },

  // ─── 5. GÓC 180° SAU LƯNG ───
  {
    id: 'head_angle180',
    title: 'Cử Động Đầu - 180° Sau Lưng (180° Rear Head Motion)',
    subtitle: 'Đầu cúi nhẹ hoặc nghiêng suy tư nhìn hoàn toàn từ phía sau lưng 180° 2-3s lặp',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Cử Động Đầu',
    icon: '⬆️',
    promptType: 'video',
    tags: ['head', '180deg', 'rear', 'back', 'nod', 'tilt', 'video'],
    videoGuide: {
      duration: '2 - 3 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock 180° Direct Rear View',
      loopType: 'Seamless Loop',
      keyPoints: [
        'Góc nhìn từ sau lưng 180°',
        'Búi tóc và trâm cài sau đầu cân đối tuyệt đối',
        'Đầu cúi nhẹ biểu thị sự tôn trọng hoặc suy tư',
        'Dải lụa buông rủ nhấp nhô nhịp nhàng',
      ],
    },
    infoNote: '💡 Tạo chiều sâu cảm xúc bí ẩn hoặc thể hiện sự quyết tâm trước khi bước vào trận chiến.',
    negativePrompt: `face visible, front view, body turning around, walking, 3D CGI rendering, blurry`,
    rawPrompt: `TASK: Image-to-video 2-3 second animation: REAR-VIEW SOLEMN HEAD NOD & TILT (180° PURE BACK VIEW).
Use the 180° rear faceless reference image as LOCKED IDENTITY SOURCE.

CHARACTER LOCK:
- Only back of head visible (hair buns, braids, hairpin ornaments, back neckline)
- Robe back patterns, hair color, and proportions locked strictly
- Back of shoulders and spine remain stable and centered

HEAD MOTION TIMELINE (180° VIEW):
- Frame 0.0 - 1.0s: Back of head lowers slightly in a solemn respectful nod
- Frame 1.0 - 2.0s: Head tilts 5° to the left in contemplative posture
- Frame 2.0 - 3.0s: Returns to centered upright stance
- Secondary Motion: Symmetrical floating ribbons and long back hair strands sway with subtle springiness

SEAMLESS LOOP:
- Frame 1 perfectly matches last frame
- Chroma green #00FF00 background.`,
  },

  // ─── 6. CÁC BIỂU CẢM ĐẦU ĐẶC BIỆT CHUYÊN DỤNG ───
  {
    id: 'head_shake',
    title: 'Lắc Đầu Phủ Định (Head Shake - Refusal & Disagreement)',
    subtitle: 'Lắc đầu từ chối, phủ nhận hoặc lắc đầu bất lực dứt khoát 1.5-2s lặp',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Cử Động Đầu',
    icon: '🤨',
    promptType: 'video',
    tags: ['head_shake', 'no', 'disagree', 'refusal', 'expression', 'dialogue'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock (Upper Body / Head Focus)',
      loopType: 'Seamless Loop',
      keyPoints: [
        'Thân mình và bờ vai giữ nguyên vị trí',
        'Đầu lắc sang trái 15° → về giữa → lắc sang phải 15°',
        'Nhịp lắc dứt khoát mang tính phủ định rõ ràng',
        'Tóc mái và trang sức văng nhẹ theo nhịp quay',
      ],
    },
    infoNote: '💡 Dùng cho các phân đoạn nhân vật từ chối lời mời, phủ nhận cáo buộc hoặc tỏ vẻ hoang mang.',
    negativePrompt: `eyes, mouth, nose, body swinging, arms moving, erratic shaking, extreme neck distortion, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 1.5-2 second animation: SHARP HEAD SHAKE OF DISAGREEMENT / REFUSAL.
Use the faceless reference image. ONLY MOVE HEAD.

MOTION PATTERN:
- Body and torso remain completely motionless and level
- Frame 0-0.5s: Head rotates left ~18°
- Frame 0.5-1.0s: Head passes center and rotates right ~18°
- Frame 1.0-1.5s: Smaller secondary counter-shake (left ~8°) then settles smoothly back to center
- Flowing bangs and hairpins whip naturally with inertia

SEAMLESS LOOP: Returns to centered resting pose at loop point. Chroma green #00FF00 background.`,
  },
  {
    id: 'head_nod',
    title: 'Gật Đầu Tán Thành (Head Nod - Agreement & Confidence)',
    subtitle: 'Gật đầu đồng ý, chào hỏi tự tin hoặc biểu thị đã hiểu rõ 1.5-2s lặp',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Cử Động Đầu',
    icon: '✅',
    promptType: 'video',
    tags: ['head_nod', 'yes', 'agree', 'affirmative', 'expression', 'dialogue'],
    videoGuide: {
      duration: '1.5 - 2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock (Upper Body Focus)',
      loopType: 'Seamless Loop',
      keyPoints: [
        'Đầu gật xuống 1 nhịp sâu rồi bật nhẹ lên',
        'Cử chỉ thể hiện sự thấu suốt và tin cậy',
        'Lọn tóc rủ xuống theo trọng lực rồi đàn hồi về vị trí cũ',
      ],
    },
    infoNote: '💡 Hoạt ảnh nhận nhiệm vụ, đồng thuận ký hiệp ước hoặc đáp lại lời chào của đồng đội.',
    negativePrompt: `eyes, mouth, nose, body swinging, erratic shaking, side to side rotation, 3D CGI`,
    rawPrompt: `TASK: Image-to-video 1.5-2 second animation: CONFIDENT AFFIRMATIVE HEAD NOD.
Use the faceless reference image.

MOTION PATTERN:
- Body firmly anchored, zero torso movement
- Frame 0-0.7s: Clean deliberate downward chin nod (~20° pitch)
- Frame 0.7-1.4s: Rebounds softly upwards past center (~5° elevation) with serene confidence
- Frame 1.4-2.0s: Settles smoothly to dead center
- Hair accessories bounce gently at bottom of nod

SEAMLESS LOOP: Ends on center forward pose. Chroma green #00FF00 background.`,
  },
  {
    id: 'look_aside',
    title: 'Ngó Sang Một Bên (Look Aside - Alert Glance)',
    subtitle: 'Ngoảnh đầu quan sát xung quanh cảnh giác hoặc e ấp tránh ánh nhìn 2s lặp',
    stepCategory: 'step3_actions',
    stepLabel: 'Bước 3: Cử Động Đầu',
    icon: '👀',
    promptType: 'video',
    tags: ['look_aside', 'glance', 'observe', 'head_turn', 'curious'],
    videoGuide: {
      duration: '2 giây',
      fps: '24 / 30 FPS',
      camera: 'Static Lock',
      loopType: 'Seamless Loop',
      keyPoints: [
        'Đầu quay nhanh 25° sang một bên',
        'Giữ nguyên 0.6 giây như đang lắng nghe tiếng động',
        'Từ từ quay đầu lại chính diện',
      ],
    },
    infoNote: '💡 Làm cho nhân vật có thần thái sống động, luôn để ý đến môi trường xung quanh.',
    negativePrompt: `body turning wildly, walking away, facial features, 3D CGI, blurry`,
    rawPrompt: `TASK: Image-to-video 2 second animation: ALERT HEAD TURN AND GLANCE ASIDE.
Use the faceless reference image.

MOTION PATTERN:
- Body remains perfectly still facing forward
- Frame 0-0.6s: Head turns swiftly 25° to side
- Frame 0.6-1.3s: Holds observant pose, listening/watching
- Frame 1.3-2.0s: Smoothly returns head back to front center
- Hair ornaments sway on the initial snap turn and settle

SEAMLESS LOOP: Centers on front pose matching frame 1. Chroma green #00FF00 background.`,
  },
];
