export interface Asset2DComponentDef {
  id: string;
  nameVi: string;
  titleEn: string;
  summaryEn: string;
  isolationRule: string;
  includedGeometry: string[];
  excludedGeometry: string[];
  rearVisibility: 'visible' | 'hidden' | 'conditional';
  groupId: string;
  groupNameVi: string;
  zIndex: number;
  filePrefix: string;
  idealAspectRatio: '1:1' | '3:4' | '4:3' | '16:9' | '9:16';
}

export function getComponentDef(partType: string, options: {
  hairColInfo: { en: string; vi: string };
  hairTexInfo: { en: string; vi: string };
  hairLenInfo: { en: string; vi: string };
  hairAccInfo: { en: string; vi: string };
  eyeShapeInfo: { en: string; vi: string };
  eyeColInfo: { en: string; vi: string };
  noseInfo: { en: string; vi: string };
  mouthInfo: { en: string; vi: string };
  costumeInfo: { en: string; vi: string };
  costumeColorVi: string;
  propInfo: { en: string; vi: string };
}): Asset2DComponentDef {
  const {
    hairColInfo, hairTexInfo, hairLenInfo, hairAccInfo,
    eyeShapeInfo, eyeColInfo, noseInfo, mouthInfo,
    costumeInfo, costumeColorVi, propInfo
  } = options;

  switch (partType) {
    case 'toc_truoc':
      return {
        id: 'toc_truoc',
        nameVi: 'Mái Tóc Trước (Front Bangs Fringe)',
        titleEn: 'STANDALONE CLIP-ON FRONT BANGS FRINGE HAIRPIECE SPRITE (FOREHEAD LAYER ONLY)',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nShort clip-on front bangs fringe hairpiece in hair color (${hairColInfo.en}).\nFloating as an independent 2D foreground hairpiece sticker.\nMaximum vertical length is strictly at eyebrow and cheek level.\nPure solid Chroma Green #00FF00 background is 100% visible directly behind the bangs.\nStrictly ZERO back hair, ZERO long hair, ZERO ponytail, ZERO hair bun, ZERO hair behind neck, ZERO head or skull silhouette!`,
        isolationRule: 'Forehead fringe bangs hairpiece ONLY. Maximum vertical length reaches eyebrow and cheek level. Pure solid Chroma Green #00FF00 background is 100% visible directly behind the bangs. Strictly ZERO back hair, ZERO long hair, ZERO ponytail, ZERO hair bun, ZERO hair behind neck, ZERO scalp or head silhouette.',
        includedGeometry: [
          'short floating front fringe hair locks ending at eyebrow level',
          'short side temple wisps ending above chin level',
          `thin foreground hair strands in hair color (${hairColInfo.en})`,
          'top root cut horizontally flat at the hairline',
        ],
        excludedGeometry: [
          'full character', 'back hair', 'long hair', 'flowing hair', 'ponytail', 'hair bun', 'rear hair mantle', 'hair behind neck', 'scalp', 'skull', 'head', 'face skin', 'eyes', 'eyebrows', 'nose', 'mouth', 'neck', 'torso', 'body',
        ],
        rearVisibility: 'hidden',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 50,
        filePrefix: '05_toc_truoc',
        idealAspectRatio: '3:4',
      };

    case 'toc_sau': {
      const isShortHair = /ngắn|short|bob|tém|pixie|shoulder|vai/i.test(hairLenInfo.vi + ' ' + hairLenInfo.en);
      return {
        id: 'toc_sau',
        nameVi: isShortHair ? 'Tóc Sau Gáy Ngắn (Short Back Hair)' : 'Suối Tóc Sau Lưng (Back Hair Mantle)',
        titleEn: isShortHair
          ? 'STANDALONE DETACHED SHORT REAR BACK HAIR AND NAPE LOCKS SPRITE'
          : 'STANDALONE REAR BACK HAIR MANTLE VOLUME SPRITE (NO FRONT BANGS, HOLLOW FRONT CENTER)',
        summaryEn: isShortHair
          ? `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nShort back hair and nape hair layer (${hairColInfo.en}, ${hairTexInfo.en}, short hair style).\nContains ONLY the rear back head hair volume covering the nape and back of skull.\nDO NOT include any front bangs, forehead fringe, face, or body!`
          : `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nLong back hair mass, rear hair bun/crown, and flowing hair streams cascading behind the back (${hairColInfo.en}, ${hairTexInfo.en}, ${hairLenInfo.en}).\nIn FRONT (0°) and THREE-QUARTER (45°) views, the front-center area where the face and front bangs belong MUST REMAIN A COMPLETELY HOLLOW / EMPTY GAP for later puppet assembly.\nDO NOT include any front bangs, front fringe, face skin, eyes, nose, or mouth!`,
        isolationRule: 'Contains ONLY the rear back hair volume and back mantle cascading behind the spine. The front center where the face and front bangs go MUST BE A HOLLOW EMPTY GAP showing solid Chroma Green background. Strictly ZERO front bangs, ZERO face.',
        includedGeometry: isShortHair
          ? [
              'short rear back hair volume',
              'nape hair locks contour',
              'rear crown hair texture',
            ]
          : [
              'rear back hair mass',
              `flowing back hair mantle cascading behind the shoulders in hair color (${hairColInfo.en})`,
              'rear hair bun / hair crown ornaments on the rear of the head',
              'hollow empty front-center space where face and bangs assemble',
            ],
        excludedGeometry: [
          'full character', 'front bangs', 'front fringe', 'forehead hair', 'face skin', 'forehead', 'eyes', 'eyebrows', 'nose', 'mouth', 'cheeks', 'chin', 'neck skin', 'body',
        ],
        rearVisibility: 'visible',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 10,
        filePrefix: '01_toc_sau',
        idealAspectRatio: isShortHair ? '3:4' : '9:16',
      };
    }

    case 'khuon_mat_no_face':
    case 'khuon_mat':
      return {
        id: 'khuon_mat_no_face',
        nameVi: 'Khuôn Mặt Trần Không Ngũ Quan (Blank Face Base)',
        titleEn: 'BLANK PORCELAIN FACE SKIN MASK SPRITE (COMPLETELY BALD, ZERO FACIAL FEATURES)',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nA completely featureless, blank anime head and facial skin silhouette.\nABSOLUTELY NO hair of any kind (NO front bangs, NO back hair, NO side hair).\nABSOLUTELY NO facial features (NO eyes, NO eyebrows, NO nose, NO mouth).\nPure clean porcelain skin mannequin base for assembling modular eyes, nose, mouth and hair layers.`,
        isolationRule: 'Completely bald, blank featureless mannequin face mask. Zero hair anywhere on the head, zero front bangs, zero back hair, zero eyes, zero eyebrows, zero nose, zero mouth.',
        includedGeometry: [
          'completely blank porcelain facial skin silhouette',
          'smooth jawline and chin',
          'empty bald forehead surface',
          'neck connection base',
        ],
        excludedGeometry: [
          'hair of any kind', 'bangs', 'back hair', 'eyebrows', 'eyes', 'eyelashes', 'pupils', 'sclera', 'nose', 'mouth', 'lips', 'teeth', 'ears', 'clothes', 'body',
        ],
        rearVisibility: 'visible',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 30,
        filePrefix: '03_khuon_mat',
        idealAspectRatio: '3:4',
      };

    case 'trong_den_iris':
      return {
        id: 'trong_den_iris',
        nameVi: 'Mống Mắt & Con Ngươi Màu (Iris & Pupil Layer)',
        titleEn: 'ISOLATED PAIR OF ANIME EYE IRIS DISCS AND PUPILS ONLY',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nIsolated pair of floating circular anime iris discs and pupils (${eyeColInfo.en}) with internal color gradient reflections.\nDO NOT include sclera, eyelids, eyelashes, skin, or head!`,
        isolationRule: 'Pair of isolated anime iris discs and pupil stickers only. Zero sclera, zero eyelashes, zero face skin, zero head.',
        includedGeometry: [
          `pair of circular colored anime irises in color (${eyeColInfo.en})`,
          'crisp circular pupil center',
          'internal luminous iris gradient reflections',
        ],
        excludedGeometry: [
          'full character', 'sclera', 'eyeball whites', 'eyelids', 'eyelashes', 'eyebrows', 'face skin', 'forehead', 'head', 'hair', 'body',
        ],
        rearVisibility: 'hidden',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 42,
        filePrefix: '04a_trong_den_iris',
        idealAspectRatio: '1:1',
      };

    case 'trong_trang':
      return {
        id: 'trong_trang',
        nameVi: 'Tròng Trắng / Hốc Mắt (Sclera Base Layer)',
        titleEn: 'ISOLATED PAIR OF ANIME EYE SCLERA WHITE BASE SHAPES ONLY',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nIsolated pair of smooth pure white anime sclera base shapes with subtle upper socket shadow.\nDO NOT include iris, pupil, highlights, eyelids, face skin, or head!`,
        isolationRule: 'Pair of isolated anime sclera eye whites stickers only. Zero iris, zero pupil, zero eyelids, zero face skin.',
        includedGeometry: [
          'pair of pure white almond sclera shapes',
          'subtle upper eye-socket shadow gradient',
        ],
        excludedGeometry: [
          'full character', 'iris', 'pupil', 'colored eye', 'eyelids', 'eyelashes', 'eyebrows', 'face skin', 'head', 'hair', 'body',
        ],
        rearVisibility: 'hidden',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 41,
        filePrefix: '04b_trong_trang',
        idealAspectRatio: '1:1',
      };

    case 'diem_sang_mat':
      return {
        id: 'diem_sang_mat',
        nameVi: 'Điểm Sáng Mắt (Eye Sparkles & Highlights)',
        titleEn: 'ISOLATED PAIR OF CRISP WHITE EYE HIGHLIGHT GLINT SPOTS ONLY',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nIsolated crisp pure white reflection dots and star glints for anime eyes.\nDO NOT include iris, pupil, sclera, eyelids, face skin, or head!`,
        isolationRule: 'Pair of isolated crisp white highlight glint spots only.',
        includedGeometry: [
          'crisp circular white glint spots',
          'starburst highlight glints',
          'reflection sparkle shapes',
        ],
        excludedGeometry: [
          'full character', 'iris', 'pupil', 'sclera', 'eyelids', 'face skin', 'head', 'hair',
        ],
        rearVisibility: 'hidden',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 43,
        filePrefix: '04c_diem_sang_mat',
        idealAspectRatio: '1:1',
      };

    case 'mi_mat':
      return {
        id: 'mi_mat',
        nameVi: 'Mi Mắt & Chớp Mắt (Eyelids & Blink Keyframes)',
        titleEn: 'ISOLATED ANIME EYELID LASH LINE CONTOURS ONLY',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nIsolated crisp anime upper/lower eyelid lineart and blinking stages.\nDO NOT include iris, pupil, sclera, eyebrows, nose, face skin, or head!`,
        isolationRule: 'Isolated anime eyelid lash line contours only.',
        includedGeometry: [
          'upper lash line',
          'lower lash line',
          'eyelid crease line',
          'blink keyframe contours',
        ],
        excludedGeometry: [
          'full character', 'iris', 'pupil', 'sclera', 'eyebrows', 'nose', 'face skin', 'head', 'hair',
        ],
        rearVisibility: 'hidden',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 44,
        filePrefix: '04d_mi_mat',
        idealAspectRatio: '1:1',
      };

    case 'long_may':
      return {
        id: 'long_may',
        nameVi: 'Cặp Lông Mày (Eyebrows Only)',
        titleEn: 'ISOLATED PAIR OF ANIME EYEBROW STROKES ONLY',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nTwo isolated eyebrow hair strokes floating independently in space.\nDO NOT include forehead skin, DO NOT include eyes, DO NOT include hair, DO NOT include head!`,
        isolationRule: 'Pair of isolated anime eyebrow line strokes only. Zero forehead skin, zero eyes, zero head.',
        includedGeometry: [
          `left and right eyebrow line strokes in hair color (${hairColInfo.en})`,
        ],
        excludedGeometry: [
          'full character', 'forehead skin', 'face skin', 'eyes', 'eyelashes', 'hair', 'nose', 'mouth', 'head',
        ],
        rearVisibility: 'hidden',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 45,
        filePrefix: '04e_long_may',
        idealAspectRatio: '1:1',
      };

    case 'mui':
      return {
        id: 'mui',
        nameVi: 'Sống Mũi (Nose Only)',
        titleEn: 'ISOLATED ANIME NOSE BRIDGE AND NOSE TIP CONTOUR ONLY',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nInclude ONLY the delicate anime nose bridge contour and tip (${noseInfo.en}).\nDO NOT include eyes, mouth, chin, cheeks, facial skin, or head!`,
        isolationRule: 'Single isolated anime nose bridge and tip contour line only. Zero eyes, zero mouth, zero face skin.',
        includedGeometry: [
          'delicate anime nose bridge contour line',
          'minimalist nose tip outline and subtle shading dot',
        ],
        excludedGeometry: [
          'full character', 'eyes', 'eyebrows', 'mouth', 'chin', 'cheeks', 'forehead', 'facial skin outside the nose', 'hair', 'head',
        ],
        rearVisibility: 'hidden',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 35,
        filePrefix: '04f_mui',
        idealAspectRatio: '1:1',
      };

    case 'doi_tai':
    case 'mui_tai':
      return {
        id: 'doi_tai',
        nameVi: 'Đôi Tai Trái / Phải (Dual Ears 16:9 - 2 Cột)',
        titleEn: 'PAIR OF DETACHED ANIME EARS SIDE-BY-SIDE ON 16:9 CANVAS (LEFT HALF: LEFT EAR, RIGHT HALF: RIGHT EAR)',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\n16:9 widescreen canvas divided symmetrically into 2 equal side-by-side columns with clean spacing:\n- LEFT COLUMN: Contains exclusively the floating Left Ear with crisp outer contour, inner cartilage folds, and earlobe.\n- RIGHT COLUMN: Contains exclusively the floating Right Ear with matching proportion, scale, line weight, and lighting.\nDO NOT connect ears to head, face skin, jaw, cheeks, hair, or body!`,
        isolationRule: 'Pair of isolated ears arranged side-by-side on 16:9 canvas (left half: left ear, right half: right ear). Zero head, zero face, zero hair.',
        includedGeometry: [
          'left column: left ear with detailed inner cartilage and earlobe',
          'right column: right ear with detailed inner cartilage and earlobe',
          'side-by-side 2-column layout on 16:9 canvas',
        ],
        excludedGeometry: [
          'full character', 'face skin', 'forehead', 'jawline', 'hair', 'head', 'neck', 'body', 'middle dividing line',
        ],
        rearVisibility: 'visible',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 26,
        filePrefix: '04g_doi_tai',
        idealAspectRatio: '16:9',
      };

    case 'mieng':
      return {
        id: 'mieng',
        nameVi: 'Khẩu Hình Miệng (Mouth & Lips)',
        titleEn: 'ISOLATED ANIME MOUTH OPENING AND LIP CONTOURS ONLY',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nInclude ONLY the lips and mouth opening contour (${mouthInfo.en}).\nThe mouth is an independent floating 2D sticker layer.\nDO NOT include nose, chin, cheeks, facial skin, or head!`,
        isolationRule: 'Single isolated anime mouth opening and lip contours only. Zero nose, zero chin, zero face skin.',
        includedGeometry: [
          'upper lip line and color',
          'lower lip line and color',
          'mouth expression opening contour',
        ],
        excludedGeometry: [
          'full character', 'nose', 'chin', 'cheeks', 'facial skin surrounding the mouth', 'eyes', 'eyebrows', 'hair', 'head',
        ],
        rearVisibility: 'hidden',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 36,
        filePrefix: '04h_mieng',
        idealAspectRatio: '1:1',
      };

    case 'mat':
      return {
        id: 'mat',
        nameVi: 'Đôi Mắt Tổng Hợp (Full Anime Eyes)',
        titleEn: 'ISOLATED COMPLETE PAIR OF ANIME EYES ONLY',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nInclude complete pair of anime eyes (${eyeShapeInfo.en}, ${eyeColInfo.en}).\nThe eyes must float as an isolated independent 2D sticker layer.\nDO NOT include face skin, forehead, eyebrows, nose, mouth, hair, or head!`,
        isolationRule: 'Complete pair of anime eye stickers only. Zero face skin, zero eyebrows, zero nose, zero head.',
        includedGeometry: [
          'left eye complete structure (sclera, iris, pupil, lash line)',
          'right eye complete structure (sclera, iris, pupil, lash line)',
          'internal eye glints and reflections',
        ],
        excludedGeometry: [
          'full character', 'face skin', 'forehead', 'eyebrows', 'nose', 'mouth', 'cheeks', 'hair', 'head',
        ],
        rearVisibility: 'hidden',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 40,
        filePrefix: '04_ngu_quan_mat',
        idealAspectRatio: '1:1',
      };

    case 'than_co_ban':
      return {
        id: 'than_co_ban',
        nameVi: 'Thân Ngực & Eo Áo Giáp (Torso & Chest Armor)',
        titleEn: 'HEADLESS ARMLESS TORSO COSTUME ROBE GARMENT SPRITE (SEVERED AT NECK AND ARMHOLES)',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nCostume chest tunic, waist sash, and collar garment (${costumeInfo.en}, ${costumeColorVi}).\nCleanly severed at the neck collar, clean armholes severed at shoulders, clean waist cut.\nDO NOT include head, neck, arms, sleeves, hands, legs, feet, or flowing cape!`,
        isolationRule: 'Headless, neckless, armless costume tunic robe body only. Severed cleanly at collar, severed cleanly at armholes, severed cleanly at waist. Zero head, zero arms, zero legs.',
        includedGeometry: [
          'chest tunic / armor plate / robe body',
          `waistband / sash in color theme (${costumeColorVi})`,
          'clean collar opening cut at neck',
          'clean armhole openings cut at shoulders',
          'clean lower torso waist cut',
        ],
        excludedGeometry: [
          'full character', 'head', 'neck skin', 'face', 'shoulders / arm sleeves', 'arms', 'hands', 'legs', 'feet', 'flowing cape',
        ],
        rearVisibility: 'visible',
        groupId: '02_torso_arms',
        groupNameVi: 'Khớp Xương Thân & Cánh Tay',
        zIndex: 20,
        filePrefix: '02_than_co_ban',
        idealAspectRatio: '3:4',
      };

    case 'canh_tay_trai':
      return {
        id: 'canh_tay_trai',
        nameVi: 'Cánh Tay Trái - Bắp Tay (Left Upper Arm)',
        titleEn: 'SINGLE ISOLATED LEFT UPPER ARM SLEEVE CYLINDER (CUT AT SHOULDER AND ELBOW JOINTS)',
        summaryEn: `Left upper bicep arm sleeve segment (${costumeColorVi}).\nSevered cleanly at shoulder joint and elbow joint.\nDO NOT include torso, chest, head, forearm, wrist, hand, or weapon!`,
        isolationRule: 'Single isolated upper arm bicep sleeve cylinder only. Severed cleanly at shoulder joint and elbow joint. Zero torso, zero forearm, zero hand.',
        includedGeometry: [
          'left upper arm bicep limb tube',
          `sleeve fabric covering left upper arm in (${costumeColorVi})`,
          'clean cut line at shoulder joint',
          'clean cut line at elbow joint',
        ],
        excludedGeometry: [
          'full character', 'torso', 'chest', 'neck', 'head', 'forearm', 'wrist', 'hand', 'weapon', 'other arm',
        ],
        rearVisibility: 'visible',
        groupId: '02_torso_arms',
        groupNameVi: 'Khớp Xương Thân & Cánh Tay',
        zIndex: 21,
        filePrefix: '02a_canh_tay_trai',
        idealAspectRatio: '3:4',
      };

    case 'cang_tay_trai':
      return {
        id: 'cang_tay_trai',
        nameVi: 'Cẳng Tay Trái (Left Forearm)',
        titleEn: 'SINGLE ISOLATED LEFT FOREARM BRACER SLEEVE CYLINDER (CUT AT ELBOW AND WRIST JOINTS)',
        summaryEn: `Left forearm sleeve and bracer segment (${costumeColorVi}).\nSevered cleanly at elbow joint and wrist joint.\nDO NOT include upper arm, shoulder, torso, hand, fingers, or weapon!`,
        isolationRule: 'Single isolated forearm bracer sleeve cylinder only. Severed cleanly at elbow joint and wrist joint. Zero upper arm, zero hand, zero torso.',
        includedGeometry: [
          'left forearm limb tube',
          `forearm bracer / cuff / sleeve fabric in (${costumeColorVi})`,
          'clean cut line at elbow joint',
          'clean cut line at wrist joint',
        ],
        excludedGeometry: [
          'full character', 'upper arm', 'shoulder', 'torso', 'hand', 'fingers', 'weapon', 'other arm',
        ],
        rearVisibility: 'visible',
        groupId: '02_torso_arms',
        groupNameVi: 'Khớp Xương Thân & Cánh Tay',
        zIndex: 22,
        filePrefix: '02b_cang_tay_trai',
        idealAspectRatio: '3:4',
      };

    case 'ban_tay_trai':
      return {
        id: 'ban_tay_trai',
        nameVi: 'Bàn Tay Trái (Left Hand & Palm)',
        titleEn: 'SINGLE ISOLATED LEFT ANIME HAND AND FINGERS (SEVERED CLEANLY AT WRIST JOINT)',
        summaryEn: 'Left hand, palm, and fingers in specified pose.\nSevered cleanly at the wrist joint.\nDO NOT include forearm, elbow, arm, torso, or weapon!',
        isolationRule: 'Single detached hand only. Cut cleanly at the wrist joint. Zero arm, zero forearm, zero torso.',
        includedGeometry: [
          'left palm and fingers in clear gesture',
          'clean cut boundary at wrist joint',
        ],
        excludedGeometry: [
          'full character', 'forearm', 'elbow', 'upper arm', 'torso', 'body', 'weapon',
        ],
        rearVisibility: 'visible',
        groupId: '02_torso_arms',
        groupNameVi: 'Khớp Xương Thân & Cánh Tay',
        zIndex: 23,
        filePrefix: '02c_ban_tay_trai',
        idealAspectRatio: '1:1',
      };

    case 'canh_tay_phai':
      return {
        id: 'canh_tay_phai',
        nameVi: 'Cánh Tay Phải - Bắp Tay (Right Upper Arm)',
        titleEn: 'SINGLE ISOLATED RIGHT UPPER ARM SLEEVE CYLINDER (CUT AT SHOULDER AND ELBOW JOINTS)',
        summaryEn: `Right upper bicep arm sleeve segment (${costumeColorVi}).\nSevered cleanly at shoulder joint and elbow joint.\nDO NOT include torso, chest, head, forearm, wrist, hand, or weapon!`,
        isolationRule: 'Single isolated upper arm bicep sleeve cylinder only. Severed cleanly at shoulder joint and elbow joint. Zero torso, zero forearm, zero hand.',
        includedGeometry: [
          'right upper arm bicep limb tube',
          `sleeve fabric covering right upper arm in (${costumeColorVi})`,
          'clean cut line at shoulder joint',
          'clean cut line at elbow joint',
        ],
        excludedGeometry: [
          'full character', 'torso', 'chest', 'neck', 'head', 'forearm', 'wrist', 'hand', 'weapon', 'other arm',
        ],
        rearVisibility: 'visible',
        groupId: '02_torso_arms',
        groupNameVi: 'Khớp Xương Thân & Cánh Tay',
        zIndex: 19,
        filePrefix: '02d_canh_tay_phai',
        idealAspectRatio: '3:4',
      };

    case 'cang_tay_phai':
      return {
        id: 'cang_tay_phai',
        nameVi: 'Cẳng Tay Phải (Right Forearm)',
        titleEn: 'SINGLE ISOLATED RIGHT FOREARM BRACER SLEEVE CYLINDER (CUT AT ELBOW AND WRIST JOINTS)',
        summaryEn: `Right forearm sleeve and bracer segment (${costumeColorVi}).\nSevered cleanly at elbow joint and wrist joint.\nDO NOT include upper arm, shoulder, torso, hand, fingers, or weapon!`,
        isolationRule: 'Single isolated forearm bracer sleeve cylinder only. Severed cleanly at elbow joint and wrist joint. Zero upper arm, zero hand, zero torso.',
        includedGeometry: [
          'right forearm limb tube',
          `forearm bracer / cuff / sleeve fabric in (${costumeColorVi})`,
          'clean cut line at elbow joint',
          'clean cut line at wrist joint',
        ],
        excludedGeometry: [
          'full character', 'upper arm', 'shoulder', 'torso', 'hand', 'fingers', 'weapon', 'other arm',
        ],
        rearVisibility: 'visible',
        groupId: '02_torso_arms',
        groupNameVi: 'Khớp Xương Thân & Cánh Tay',
        zIndex: 18,
        filePrefix: '02e_cang_tay_phai',
        idealAspectRatio: '3:4',
      };

    case 'ban_tay_phai':
      return {
        id: 'ban_tay_phai',
        nameVi: 'Bàn Tay Phải (Right Hand & Palm)',
        titleEn: 'SINGLE ISOLATED RIGHT ANIME HAND AND FINGERS (SEVERED CLEANLY AT WRIST JOINT)',
        summaryEn: 'Right hand, palm, and fingers in specified pose.\nSevered cleanly at the wrist joint.\nDO NOT include forearm, elbow, arm, torso, or weapon!',
        isolationRule: 'Single detached hand only. Cut cleanly at the wrist joint. Zero arm, zero forearm, zero torso.',
        includedGeometry: [
          'right palm and fingers in clear gesture',
          'clean cut boundary at wrist joint',
        ],
        excludedGeometry: [
          'full character', 'forearm', 'elbow', 'upper arm', 'torso', 'body', 'weapon',
        ],
        rearVisibility: 'visible',
        groupId: '02_torso_arms',
        groupNameVi: 'Khớp Xương Thân & Cánh Tay',
        zIndex: 17,
        filePrefix: '02f_ban_tay_phai',
        idealAspectRatio: '1:1',
      };

    case 'dui_trai':
      return {
        id: 'dui_trai',
        nameVi: 'Đùi Trái (Left Thigh)',
        titleEn: 'SINGLE ISOLATED LEFT THIGH PANTS CYLINDER (CUT AT HIP AND KNEE JOINTS)',
        summaryEn: `Left thigh garment/pants limb segment (${costumeColorVi}).\nSevered cleanly at hip joint and knee joint.\nDO NOT include torso, pelvis, shin, boot, or foot!`,
        isolationRule: 'Single isolated thigh pants cylinder only. Severed cleanly at hip joint and knee joint. Zero torso, zero shin, zero foot.',
        includedGeometry: [
          'left thigh limb tube',
          `fabric/pants covering left thigh in (${costumeColorVi})`,
          'clean cut line at hip joint',
          'clean cut line at knee joint',
        ],
        excludedGeometry: [
          'full character', 'torso', 'pelvis', 'shin', 'boot', 'foot', 'other leg',
        ],
        rearVisibility: 'visible',
        groupId: '03_legs_feet',
        groupNameVi: 'Khớp Xương Chân & Giày',
        zIndex: 15,
        filePrefix: '03a_dui_trai',
        idealAspectRatio: '9:16',
      };

    case 'cang_chan_trai':
      return {
        id: 'cang_chan_trai',
        nameVi: 'Cẳng Chân & Giày Ủng Trái (Left Shin & Boot)',
        titleEn: 'SINGLE ISOLATED LEFT SHIN AND BOOT SEGMENT (SEVERED CLEANLY AT KNEE JOINT)',
        summaryEn: `Left lower leg and boot (${costumeColorVi}).\nSevered cleanly at knee joint.\nDO NOT include thigh, hip, torso, or right leg!`,
        isolationRule: 'Single isolated lower shin and boot segment only. Severed cleanly at knee joint. Zero thigh, zero torso.',
        includedGeometry: [
          'left shin limb tube',
          `left boot / footwear in (${costumeColorVi})`,
          'knee cap guard',
          'clean cut line at knee joint',
        ],
        excludedGeometry: [
          'full character', 'thigh', 'hip', 'torso', 'body', 'other leg',
        ],
        rearVisibility: 'visible',
        groupId: '03_legs_feet',
        groupNameVi: 'Khớp Xương Chân & Giày',
        zIndex: 16,
        filePrefix: '03b_cang_chan_trai',
        idealAspectRatio: '9:16',
      };

    case 'dui_phai':
      return {
        id: 'dui_phai',
        nameVi: 'Đùi Phải (Right Thigh)',
        titleEn: 'SINGLE ISOLATED RIGHT THIGH PANTS CYLINDER (CUT AT HIP AND KNEE JOINTS)',
        summaryEn: `Right thigh garment/pants limb segment (${costumeColorVi}).\nSevered cleanly at hip joint and knee joint.\nDO NOT include torso, pelvis, shin, boot, or foot!`,
        isolationRule: 'Single isolated thigh pants cylinder only. Severed cleanly at hip joint and knee joint. Zero torso, zero shin, zero foot.',
        includedGeometry: [
          'right thigh limb tube',
          `fabric/pants covering right thigh in (${costumeColorVi})`,
          'clean cut line at hip joint',
          'clean cut line at knee joint',
        ],
        excludedGeometry: [
          'full character', 'torso', 'pelvis', 'shin', 'boot', 'foot', 'other leg',
        ],
        rearVisibility: 'visible',
        groupId: '03_legs_feet',
        groupNameVi: 'Khớp Xương Chân & Giày',
        zIndex: 13,
        filePrefix: '03c_dui_phai',
        idealAspectRatio: '9:16',
      };

    case 'cang_chan_phai':
      return {
        id: 'cang_chan_phai',
        nameVi: 'Cẳng Chân & Giày Ủng Phải (Right Shin & Boot)',
        titleEn: 'SINGLE ISOLATED RIGHT SHIN AND BOOT SEGMENT (SEVERED CLEANLY AT KNEE JOINT)',
        summaryEn: `Right lower leg and boot (${costumeColorVi}).\nSevered cleanly at knee joint.\nDO NOT include thigh, hip, torso, or left leg!`,
        isolationRule: 'Single isolated lower shin and boot segment only. Severed cleanly at knee joint. Zero thigh, zero torso.',
        includedGeometry: [
          'right shin limb tube',
          `right boot / footwear in (${costumeColorVi})`,
          'knee cap guard',
          'clean cut line at knee joint',
        ],
        excludedGeometry: [
          'full character', 'thigh', 'hip', 'torso', 'body', 'other leg',
        ],
        rearVisibility: 'visible',
        groupId: '03_legs_feet',
        groupNameVi: 'Khớp Xương Chân & Giày',
        zIndex: 14,
        filePrefix: '03d_cang_chan_phai',
        idealAspectRatio: '9:16',
      };

    case 'ao_choang':
    case 'trang_phuc':
      return {
        id: 'ao_choang',
        nameVi: 'Áo Choàng / Tà Áo Bay (Cape & Robe Flow)',
        titleEn: 'ISOLATED FLOWING CAPE / MANTLE FABRIC SPRITE (ZERO CHARACTER BODY)',
        summaryEn: `Flowing cape and fabric ribbons (${costumeColorVi}).\nFloating as an independent garment piece.\nDO NOT include character body, chest, arms, hands, legs, or head!`,
        isolationRule: 'Detached flowing cape cloth mantle sprite only floating in mid-air. Zero character body, zero head, zero limbs.',
        includedGeometry: [
          `back cape drape fabric in (${costumeColorVi})`,
          'flowing ribbon tails',
          'shoulder clasp attachments',
        ],
        excludedGeometry: [
          'full character', 'torso', 'chest', 'arms', 'hands', 'legs', 'head', 'character body',
        ],
        rearVisibility: 'visible',
        groupId: '04_props_costumes',
        groupNameVi: 'Trang Phục Bay & Vũ Khí',
        zIndex: 8,
        filePrefix: '06a_ao_choang',
        idealAspectRatio: '9:16',
      };

    case 'vu_khi':
    default:
      return {
        id: 'vu_khi',
        nameVi: 'Vũ Khí & Pháp Bảo (Weapons & Props)',
        titleEn: 'ISOLATED WEAPON / MAGICAL PROP ITEM SPRITE (ZERO CHARACTER, ZERO HANDS)',
        summaryEn: `Isolated weapon artifact (${propInfo.en}).\nFloating as a standalone game inventory item prop.\nDO NOT include character, hands, arms, body, or scenery!`,
        isolationRule: 'Standalone weapon prop item sprite only floating in space. Zero character, zero hands holding weapon, zero body.',
        includedGeometry: [
          'complete weapon blade, hilt, scabbard',
          'clean silhouette of magical prop',
        ],
        excludedGeometry: [
          'full character', 'character figure', 'hands holding weapon', 'arms', 'body', 'background scenery',
        ],
        rearVisibility: 'visible',
        groupId: '04_props_costumes',
        groupNameVi: 'Trang Phục Bay & Vũ Khí',
        zIndex: 60,
        filePrefix: '06_vu_khi',
        idealAspectRatio: '9:16',
      };
  }
}
