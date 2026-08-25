export interface Asset2DComponentDef {
  id: string;
  nameVi: string;
  titleEn: string;
  summaryEn: string;
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
        titleEn: 'EXCLUSIVELY THE FLOATING FRONT BANGS / FRONT FRINGE HAIR LAYER.',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nThis component is EXCLUSIVELY the front fringe bangs hair layer physically hovering in front of the forehead and face (${hairColInfo.en}, ${hairTexInfo.en}, ${hairLenInfo.en}${hairAccInfo.en !== 'none' ? `, ${hairAccInfo.en}` : ''}).\nThe front bangs must float as an independent 2D hair cluster, completely separated and severed from the rest of the head, face, and back hair.\nDO NOT attach any back hair, rear hair mantle, ponytail, hair bun, top scalp, or facial skin!\nIn Cell [1,2] (180° Rear Back), because the front bangs are physically located on the front of the head and 100% occluded from behind, THIS CELL MUST REMAIN COMPLETELY EMPTY CHROMA GREEN (#00FF00).`,
        includedGeometry: [
          'floating front fringe bangs locks',
          'hair strands crossing in front of the forehead',
          'front fringe tips and middle locks belonging strictly to the front-bangs layer',
        ],
        excludedGeometry: [
          'back hair', 'rear hair mantle', 'hair falling behind the neck or shoulders',
          'hair bun on the back of the head', 'top scalp hair mass', 'head silhouette',
          'face', 'forehead skin', 'scalp', 'ears', 'eyebrows', 'eyes', 'eyelashes', 'nose', 'mouth', 'neck', 'body',
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
          ? 'EXCLUSIVELY THE SHORT REAR BACK HAIR AND NAPE LOCKS LAYER.'
          : 'EXCLUSIVELY THE REAR BACK HAIR MANTLE / LONG FLOWING BACK HAIR VOLUME LAYER.',
        summaryEn: isShortHair
          ? `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nShort back hair and nape hair layer (${hairColInfo.en}, ${hairTexInfo.en}, short hair style).\nContains ONLY the rear back head hair volume covering the nape and back of skull.\nDO NOT include any front bangs, forehead fringe, face, or body!`
          : `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nThe front bangs (mái tóc trước) and facial features have ALREADY been separated into independent layers!\nTherefore, across ALL views, this asset contains ONLY the long back hair mass, rear hair bun/crown, and long flowing hair streams cascading behind the back (${hairColInfo.en}, ${hairTexInfo.en}, ${hairLenInfo.en}).\nIn FRONT (0°) and THREE-QUARTER (45°) views, the front-center area where the face and front bangs belong MUST REMAIN A COMPLETELY HOLLOW / EMPTY GAP for later puppet assembly.\nDO NOT include any front bangs, front fringe, forehead locks, forehead skin, or facial features!`,
        includedGeometry: isShortHair
          ? [
              'short rear back hair volume',
              'nape hair locks contour',
              'rear crown hair texture',
            ]
          : [
              'rear back hair mass',
              'flowing back hair mantle cascading behind the shoulders and spine',
              'rear hair bun / hair crown ornaments on the rear of the head',
              'hollow empty front-center space in front views where face and bangs assemble',
            ],
        excludedGeometry: [
          'front bangs', 'front fringe', 'forehead hair', 'front facial hair framing the forehead',
          'forehead skin', 'face silhouette', 'eyes', 'eyebrows', 'nose', 'mouth', 'cheeks', 'chin', 'mannequin head base',
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
        titleEn: 'EXCLUSIVELY THE BLANK PORCELAIN FACE SKIN / HEAD BASE (NO HAIR, NO FEATURES).',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nA completely featureless, blank anime head and facial skin silhouette.\nABSOLUTELY NO hair of any kind (NO front bangs, NO back hair, NO side hair).\nABSOLUTELY NO facial features (NO eyes, NO eyebrows, NO nose, NO mouth).\nPure clean porcelain skin mannequin base for assembling modular eyes, nose, mouth and hair layers.`,
        includedGeometry: [
          'blank facial skin silhouette', 'forehead skin surface', 'cheeks', 'jawline', 'chin', 'neck connection base',
        ],
        excludedGeometry: [
          'hair of any kind', 'front bangs', 'front fringe', 'side hair', 'back hair', 'hair accessories',
          'eyebrows', 'eyes', 'eyelashes', 'iris', 'pupil', 'sclera', 'nose', 'mouth', 'ears', 'clothing', 'body',
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
        titleEn: 'EXCLUSIVELY THE PAIR OF ANIME IRISES AND PUPILS.',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nIsolated pair of floating circular anime iris discs and pupils (${eyeColInfo.en}) with internal color gradient reflections.\nDO NOT include sclera, eyelids, eyelashes, skin, or head!\nIn Cell [1,2] (180° Rear Back), the eyes are 100% occluded, so Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
        includedGeometry: [
          'left circular iris disc and pupil', 'right circular iris disc and pupil', 'internal iris color gradient and luster',
        ],
        excludedGeometry: [
          'sclera', 'white of eyes', 'eyelids', 'eyelashes', 'eyebrows', 'face skin', 'head', 'hair',
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
        titleEn: 'EXCLUSIVELY THE PAIR OF ANIME SCLERA (EYE SOCKET WHITES).',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nIsolated pair of smooth pure white anime sclera base shapes with subtle upper socket shadow.\nDO NOT include iris, pupil, highlights, eyelids, face skin, or head!\nIn Cell [1,2] (180° Rear Back), Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
        includedGeometry: [
          'left white sclera shape', 'right white sclera shape', 'subtle upper eye-socket shadow gradient',
        ],
        excludedGeometry: [
          'iris', 'pupil', 'highlights', 'eyelids', 'eyelashes', 'eyebrows', 'face skin', 'head', 'hair',
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
        titleEn: 'EXCLUSIVELY THE EYE SPARKLES AND HIGHLIGHT GLINTS.',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nIsolated crisp pure white reflection dots and star glints for anime eyes.\nDO NOT include iris, pupil, sclera, eyelids, face skin, or head!\nIn Cell [1,2] (180° Rear Back), Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
        includedGeometry: [
          'crisp circular white glint spots', 'starburst highlight glints', 'reflection sparkle shapes',
        ],
        excludedGeometry: [
          'iris', 'pupil', 'sclera', 'eyelids', 'face skin', 'head', 'hair',
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
        titleEn: 'EXCLUSIVELY THE EYELIDS AND BLINK KEYFRAME CONTOURS.',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nIsolated crisp anime upper/lower eyelid lineart and blinking stages.\nDO NOT include iris, pupil, sclera, eyebrows, nose, face skin, or head!\nIn Cell [1,2] (180° Rear Back), Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
        includedGeometry: [
          'upper lash line', 'lower lash line', 'eyelid crease line', 'blink keyframe contours (open, half-closed, closed)',
        ],
        excludedGeometry: [
          'iris', 'pupil', 'sclera', 'eyebrows', 'nose', 'face skin', 'head', 'hair',
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
        titleEn: 'EXCLUSIVELY THE PAIR OF ANIME EYEBROWS.',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nTwo isolated eyebrow hair strokes floating independently in space.\nDO NOT include forehead skin, DO NOT include eyes, DO NOT include hair, DO NOT include head!\nIn Cell [1,2] (180° Rear Back), the eyebrows are 100% occluded, so Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
        includedGeometry: ['left eyebrow stroke', 'right eyebrow stroke'],
        excludedGeometry: ['forehead skin', 'face skin', 'eyes', 'eyelashes', 'hair', 'nose', 'mouth', 'head'],
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
        titleEn: 'EXCLUSIVELY THE ANIME NOSE BRIDGE AND NOSE TIP.',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nInclude ONLY the delicate anime nose bridge contour and tip (${noseInfo.en}).\nDO NOT include eyes, DO NOT include mouth, DO NOT include chin, DO NOT include cheeks, DO NOT include facial skin outside the nose!\nIn Cell [1,2] (180° Rear Back), the nose is 100% occluded, so Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
        includedGeometry: ['nose bridge contour line', 'nose tip outline and subtle minimalist shading dot'],
        excludedGeometry: ['eyes', 'eyebrows', 'mouth', 'chin', 'cheeks', 'forehead', 'facial skin outside the nose', 'hair', 'head'],
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
        titleEn: 'EXCLUSIVELY THE PAIR OF ANIME EARS ARRANGED ON A 16:9 WIDESCREEN CANVAS DIVIDED INTO 2 EQUAL SIDE-BY-SIDE COLUMNS (LEFT HALF: LEFT EAR, RIGHT HALF: RIGHT EAR).',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\n16:9 widescreen canvas divided symmetrically into 2 equal side-by-side columns with clean spacing:\n- LEFT COLUMN (Ô Trái): Contains exclusively the floating Left Ear with crisp outer contour, inner cartilage folds, and earlobe.\n- RIGHT COLUMN (Ô Phải): Contains exclusively the floating Right Ear with matching proportion, scale, line weight, and lighting.\nBoth ears float independently as modular 2D puppet stickers.\nDO NOT connect ears to head, face skin, jaw, cheeks, hair, or body!\nSolid flat chroma key green background (#00FF00), zero drop shadows, no text, no dividers.`,
        includedGeometry: [
          'left column: left ear with detailed inner cartilage and earlobe',
          'right column: right ear with detailed inner cartilage and earlobe',
          'side-by-side 2-column layout on 16:9 canvas',
        ],
        excludedGeometry: ['face skin', 'forehead', 'jawline', 'hair', 'head', 'neck', 'body', 'middle dividing line'],
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
        titleEn: 'EXCLUSIVELY THE ANIME MOUTH AND LIP CONTOURS.',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nInclude ONLY the lips and mouth opening contour (${mouthInfo.en}).\nThe mouth is an independent floating 2D sticker layer.\nDO NOT include nose, DO NOT include chin, DO NOT include cheeks, DO NOT include surrounding facial skin, DO NOT include head!\nIn Cell [1,2] (180° Rear Back), the mouth is 100% occluded, so Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
        includedGeometry: ['upper lip line and color', 'lower lip line and color', 'mouth expression contour'],
        excludedGeometry: ['nose', 'chin', 'cheeks', 'facial skin surrounding the mouth', 'eyes', 'eyebrows', 'hair', 'head'],
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
        titleEn: 'EXCLUSIVELY THE COMPLETE PAIR OF ANIME EYES.',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nInclude complete pair of anime eyes (${eyeShapeInfo.en}, ${eyeColInfo.en}).\nThe eyes must float as an isolated independent 2D sticker layer.\nDO NOT include face skin, forehead, eyebrows, nose, mouth, hair, or head!\nIn Cell [1,2] (180° Rear Back), the eyes are 100% occluded, so Cell [1,2] MUST REMAIN PURE EMPTY CHROMA GREEN (#00FF00).`,
        includedGeometry: [
          'left eye complete structure (sclera, iris, pupil, lash line)',
          'right eye complete structure (sclera, iris, pupil, lash line)',
          'internal eye glints and reflections',
        ],
        excludedGeometry: ['face skin', 'forehead', 'eyebrows', 'nose', 'mouth', 'cheeks', 'hair', 'head'],
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
        titleEn: 'EXCLUSIVELY THE TORSO AND CHEST OUTFIT SEGMENT.',
        summaryEn: `CRITICAL 2D MODEL RIGGING DECOMPOSITION RULE:\nCostume chest tunic, waist sash, and collar garment (${costumeInfo.en}, ${costumeColorVi}).\nDO NOT include head, neck, arms, sleeves, hands, legs, feet, or flowing cape!`,
        includedGeometry: ['chest tunic / armor plate', 'waistband / sash', 'upper torso garment body'],
        excludedGeometry: ['head', 'neck', 'shoulders / arm sleeves', 'arms', 'hands', 'legs', 'feet', 'flowing cape'],
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
        titleEn: 'EXCLUSIVELY THE LEFT UPPER ARM SEGMENT FROM SHOULDER TO ELBOW.',
        summaryEn: `Left upper bicep arm sleeve segment (${costumeColorVi}).\nDO NOT include torso, chest, head, forearm, wrist, hand, or weapon!`,
        includedGeometry: ['left upper arm bicep', 'sleeve fabric covering the left upper arm'],
        excludedGeometry: ['torso', 'chest', 'neck', 'head', 'forearm', 'wrist', 'hand', 'weapon'],
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
        titleEn: 'EXCLUSIVELY THE LEFT FOREARM SEGMENT FROM ELBOW TO WRIST.',
        summaryEn: `Left forearm sleeve and bracer segment (${costumeColorVi}).\nDO NOT include upper arm, shoulder, torso, hand, fingers, or weapon!`,
        includedGeometry: ['left forearm', 'forearm bracer / cuff / sleeve fabric'],
        excludedGeometry: ['upper arm', 'shoulder', 'torso', 'hand', 'fingers', 'weapon'],
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
        titleEn: 'EXCLUSIVELY THE LEFT HAND FROM WRIST TO FINGERTIPS.',
        summaryEn: 'Left hand, palm, and fingers in specified pose.\nDO NOT include forearm, elbow, arm, torso, or weapon!',
        includedGeometry: ['left palm', 'left fingers', 'wrist joint connection line'],
        excludedGeometry: ['forearm', 'elbow', 'upper arm', 'torso', 'weapon'],
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
        titleEn: 'EXCLUSIVELY THE RIGHT UPPER ARM SEGMENT FROM SHOULDER TO ELBOW.',
        summaryEn: `Right upper bicep arm sleeve segment (${costumeColorVi}).\nDO NOT include torso, chest, head, forearm, wrist, hand, or weapon!`,
        includedGeometry: ['right upper arm bicep', 'sleeve fabric covering the right upper arm'],
        excludedGeometry: ['torso', 'chest', 'neck', 'head', 'forearm', 'wrist', 'hand', 'weapon'],
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
        titleEn: 'EXCLUSIVELY THE RIGHT FOREARM SEGMENT FROM ELBOW TO WRIST.',
        summaryEn: `Right forearm sleeve and bracer segment (${costumeColorVi}).\nDO NOT include upper arm, shoulder, torso, hand, fingers, or weapon!`,
        includedGeometry: ['right forearm', 'forearm bracer / cuff / sleeve fabric'],
        excludedGeometry: ['upper arm', 'shoulder', 'torso', 'hand', 'fingers', 'weapon'],
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
        titleEn: 'EXCLUSIVELY THE RIGHT HAND FROM WRIST TO FINGERTIPS.',
        summaryEn: 'Right hand, palm, and fingers in specified pose.\nDO NOT include forearm, elbow, arm, torso, or weapon!',
        includedGeometry: ['right palm', 'right fingers', 'wrist joint connection line'],
        excludedGeometry: ['forearm', 'elbow', 'upper arm', 'torso', 'weapon'],
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
        titleEn: 'EXCLUSIVELY THE LEFT THIGH SEGMENT FROM HIP TO KNEE.',
        summaryEn: `Left thigh garment/pants limb segment (${costumeColorVi}).\nDO NOT include torso, pelvis, shin, boot, or foot!`,
        includedGeometry: ['left thigh', 'fabric/pants covering the left thigh', 'hip joint connection line'],
        excludedGeometry: ['torso', 'pelvis', 'shin', 'boot', 'foot'],
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
        titleEn: 'EXCLUSIVELY THE LEFT SHIN AND BOOT SEGMENT FROM KNEE TO FOOT.',
        summaryEn: `Left lower leg and boot (${costumeColorVi}).\nDO NOT include thigh, hip, torso, or right leg!`,
        includedGeometry: ['left shin', 'left boot / footwear', 'knee cap guard'],
        excludedGeometry: ['thigh', 'hip', 'torso', 'right leg'],
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
        titleEn: 'EXCLUSIVELY THE RIGHT THIGH SEGMENT FROM HIP TO KNEE.',
        summaryEn: `Right thigh garment/pants limb segment (${costumeColorVi}).\nDO NOT include torso, pelvis, shin, boot, or foot!`,
        includedGeometry: ['right thigh', 'fabric/pants covering the right thigh', 'hip joint connection line'],
        excludedGeometry: ['torso', 'pelvis', 'shin', 'boot', 'foot'],
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
        titleEn: 'EXCLUSIVELY THE RIGHT SHIN AND BOOT SEGMENT FROM KNEE TO FOOT.',
        summaryEn: `Right lower leg and boot (${costumeColorVi}).\nDO NOT include thigh, hip, torso, or left leg!`,
        includedGeometry: ['right shin', 'right boot / footwear', 'knee cap guard'],
        excludedGeometry: ['thigh', 'hip', 'torso', 'left leg'],
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
        titleEn: 'EXCLUSIVELY THE FLOWING CAPE / MANTLE FABRIC LAYER.',
        summaryEn: `Flowing cape and fabric ribbons (${costumeColorVi}).\nDO NOT include character body, chest, arms, hands, legs, or head!`,
        includedGeometry: ['back cape drape', 'flowing ribbon tails', 'shoulder clasp attachments'],
        excludedGeometry: ['torso', 'chest', 'arms', 'hands', 'legs', 'head', 'character body'],
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
        titleEn: 'EXCLUSIVELY THE WEAPON / PROP ARTIFACT.',
        summaryEn: `Isolated weapon artifact (${propInfo.en}).\nDO NOT include character, hands, arms, body, or scenery!`,
        includedGeometry: ['blade / weapon body', 'hilt / handle', 'magical glow / aura directly emanating from weapon'],
        excludedGeometry: ['character', 'hands', 'arms', 'body', 'background scenery'],
        rearVisibility: 'visible',
        groupId: '04_props_costumes',
        groupNameVi: 'Trang Phục Bay & Vũ Khí',
        zIndex: 60,
        filePrefix: '06_vu_khi',
        idealAspectRatio: '9:16',
      };
  }
}
