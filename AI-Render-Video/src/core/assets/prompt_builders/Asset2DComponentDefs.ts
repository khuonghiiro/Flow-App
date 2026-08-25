export interface Asset2DComponentDef {
  id: string;
  nameVi: string;
  titleEn: string;
  assetTag: string;
  summaryEn: string;
  positiveContent: string;
  excludeShort: string;
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
  bangsStyleInfo?: { en: string; vi: string };
  eyeShapeInfo: { en: string; vi: string };
  eyeColInfo: { en: string; vi: string };
  noseInfo: { en: string; vi: string };
  mouthInfo: { en: string; vi: string };
  costumeInfo: { en: string; vi: string };
  costumeColorVi: string;
  propInfo: { en: string; vi: string };
}): Asset2DComponentDef {
  const {
    hairColInfo, hairTexInfo, hairLenInfo, hairAccInfo, bangsStyleInfo,
    eyeShapeInfo, eyeColInfo, noseInfo, mouthInfo,
    costumeInfo, costumeColorVi, propInfo
  } = options;

  switch (partType) {
    case 'toc_truoc': {
      const bangsDescEn = bangsStyleInfo ? bangsStyleInfo.en : 'see-through delicate airy anime bangs ending at eyebrow level';
      const bangsDescVi = bangsStyleInfo ? bangsStyleInfo.vi : 'Mái thưa tỉa lớp thanh thoát';
      return {
        id: 'toc_truoc',
        nameVi: `Mái Tóc Trước Trang Trí (${bangsDescVi})`,
        titleEn: 'STANDALONE FRONT BANGS CLIP-ON HAIR ACCESSORY SPRITE',
        assetTag: 'FRONT_BANGS_HAIR_ACCESSORY_ATTACHMENT_ONLY',
        summaryEn: `Standalone clip-on front bangs hair accessory piece in hair color (${hairColInfo.en}), styled as ${bangsDescEn}, floating alone on empty green canvas like a decorative hair attachment.`,
        positiveContent: `An independent decorative clip-on front bangs hair accessory attachment in hair color (${hairColInfo.en}), featuring ${bangsDescEn} with a clean cut top attachment edge. It is a standalone decorative hair overlay asset floating completely alone in mid-air on empty green screen, designed to be placed onto a bald/plain base head. The area behind the bangs is 100% pure flat Chroma Green #00FF00 with zero skull mass and zero back hair.`,
        excludeShort: 'base head, back hair, rear hair mass, long hair behind, ponytail, hair bun, head, skull, face, eyes, neck, body',
        includedGeometry: [
          `clip-on front bangs hair accessory in style: ${bangsDescEn}`,
          'side temple wisps ending above chin level',
          `thin foreground hair strands in hair color (${hairColInfo.en})`,
          'top root attachment edge cut horizontally flat',
        ],
        excludedGeometry: [
          'base head', 'back hair', 'rear hair mass', 'long hair behind', 'ponytail', 'hair bun', 'head', 'skull', 'face', 'eyes', 'neck', 'body',
        ],
        rearVisibility: 'hidden',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 50,
        filePrefix: '05_toc_truoc',
        idealAspectRatio: '3:4',
      };
    }

    case 'toc_sau': {
      const isShortHair = /ngắn|short|bob|tém|pixie|shoulder|vai/i.test(hairLenInfo.vi + ' ' + hairLenInfo.en);
      return {
        id: 'toc_sau',
        nameVi: isShortHair ? 'Tóc Nền Sau Gáy Ngắn (Plain Base Short Back Hair)' : 'Tóc Nền Sau Lưng Trơn (Plain Base Back Hair Mantle)',
        titleEn: isShortHair
          ? 'STANDALONE PLAIN BASE SHORT REAR BACK HAIR SPRITE'
          : 'STANDALONE PLAIN BASE LONG REAR BACK HAIR BACKDROP SPRITE',
        assetTag: isShortHair ? 'PLAIN_BASE_SHORT_REAR_HAIR_ONLY' : 'PLAIN_BASE_LONG_REAR_HAIR_BACKDROP_ONLY',
        summaryEn: isShortHair
          ? `Plain base rear nape hair in hair color (${hairColInfo.en}) with clean exposed forehead and zero front bangs.`
          : `Plain base cascading rear back hair backdrop in hair color (${hairColInfo.en}) with clean exposed forehead and zero front bangs.`,
        positiveContent: isShortHair
          ? `A clean plain base rear nape hairstyle in hair color (${hairColInfo.en}) without any decorative front bangs. In front view (0°), it has a clean exposed forehead with hair pulled back smoothly, leaving a clear open face area ready for a clip-on front bangs attachment. Zero front bangs, zero forehead fringe.`
          : `A clean plain base cascading rear back hair backdrop in hair color (${hairColInfo.en}) without any decorative front bangs. In front view (0°), it has a clean exposed forehead with hair pulled back smoothly along the sides, leaving a clear open face area ready for a clip-on front bangs attachment. Zero front bangs, zero forehead fringe.`,
        excludeShort: 'front bangs, forehead fringe, front hair overlay, decorative bangs, face skin, eyes, eyebrows, nose, mouth, body',
        includedGeometry: isShortHair
          ? [
              'clean plain base short rear hair',
              'pulled-back side hair contour',
              'open exposed forehead area',
            ]
          : [
              'clean plain base rear back hair mass',
              `flowing back hair mantle cascading down in hair color (${hairColInfo.en})`,
              'rear hair bun / plain crown base',
              'open exposed forehead and face area for bangs attachment',
            ],
        excludedGeometry: [
          'front bangs', 'forehead fringe', 'front hair overlay', 'decorative bangs', 'face skin', 'eyes', 'eyebrows', 'nose', 'mouth', 'body',
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
        titleEn: 'BLANK PORCELAIN FACE SKIN MASK SPRITE',
        assetTag: 'BLANK_PORCELAIN_FACE_BASE_ONLY',
        summaryEn: 'Completely featureless blank porcelain anime head and facial skin silhouette, zero facial features and bald scalp.',
        positiveContent: 'A completely featureless blank porcelain anime head and facial skin silhouette with smooth bare chin and jawline, zero facial features and bald scalp, like a smooth blank mannequin mask ready for modular layers.',
        excludeShort: 'hair, front bangs, back hair, eyes, eyebrows, nose, mouth, clothes, body',
        includedGeometry: [
          'completely blank porcelain facial skin silhouette',
          'smooth jawline and chin',
          'empty bald forehead surface',
          'neck connection base',
        ],
        excludedGeometry: [
          'hair of any kind', 'bangs', 'back hair', 'eyebrows', 'eyes', 'nose', 'mouth', 'body',
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
        assetTag: 'EYE_IRIS_AND_PUPIL_DISCS_ONLY',
        summaryEn: `Pair of circular anime eye iris discs and pupils (${eyeColInfo.en}) with internal color gradient reflections.`,
        positiveContent: `A pair of circular anime eye iris discs and dark pupil centers in color (${eyeColInfo.en}) with internal luminous gradient reflections, floating as isolated graphic stickers.`,
        excludeShort: 'sclera, eyeball whites, eyelids, eyelashes, face skin, head',
        includedGeometry: [
          `pair of circular colored anime irises in color (${eyeColInfo.en})`,
          'crisp circular pupil center',
          'internal luminous iris gradient reflections',
        ],
        excludedGeometry: [
          'sclera', 'eyelids', 'eyelashes', 'face skin', 'head', 'body',
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
        assetTag: 'EYE_SCLERA_WHITES_ONLY',
        summaryEn: 'Pair of smooth pure white anime sclera base shapes with subtle upper socket shadow.',
        positiveContent: 'A pair of pure white anime eye sclera almond base shapes with subtle upper shadow shading, floating as isolated sticker graphics.',
        excludeShort: 'iris, pupil, colored eyes, eyelids, eyelashes, face skin, head',
        includedGeometry: [
          'pair of pure white almond sclera shapes',
          'subtle upper eye-socket shadow gradient',
        ],
        excludedGeometry: [
          'iris', 'pupil', 'eyelids', 'eyelashes', 'face skin', 'head', 'body',
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
        assetTag: 'EYE_HIGHLIGHT_SPARKLES_ONLY',
        summaryEn: 'Crisp pure white reflection dots and star glints for anime eyes.',
        positiveContent: 'A pair of crisp pure white anime eye highlight glint dots and star reflection shapes, floating as isolated sticker graphics.',
        excludeShort: 'iris, pupil, sclera, eyelids, face skin, head',
        includedGeometry: [
          'crisp circular white glint spots',
          'starburst highlight glints',
          'reflection sparkle shapes',
        ],
        excludedGeometry: [
          'iris', 'pupil', 'sclera', 'face skin', 'head', 'hair',
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
        assetTag: 'EYELID_LASH_LINES_ONLY',
        summaryEn: 'Crisp anime upper/lower eyelid lineart and blinking keyframe contours.',
        positiveContent: 'A pair of crisp anime upper and lower eyelid line contours and lash strokes, floating as isolated lineart sticker graphics.',
        excludeShort: 'iris, pupil, sclera, eyebrows, nose, face skin, head',
        includedGeometry: [
          'upper lash line',
          'lower lash line',
          'eyelid crease line',
        ],
        excludedGeometry: [
          'iris', 'pupil', 'sclera', 'eyebrows', 'face skin', 'head',
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
        assetTag: 'EYEBROW_STROKES_ONLY',
        summaryEn: `Two isolated eyebrow hair strokes in hair color (${hairColInfo.en}).`,
        positiveContent: `A pair of isolated anime eyebrow line strokes in hair color (${hairColInfo.en}), floating as isolated lineart graphics.`,
        excludeShort: 'eyes, eyelids, forehead skin, face, head, body',
        includedGeometry: [
          `left and right eyebrow line strokes in hair color (${hairColInfo.en})`,
        ],
        excludedGeometry: [
          'forehead skin', 'face skin', 'eyes', 'hair', 'head', 'body',
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
        titleEn: 'ISOLATED ANIME NOSE BRIDGE AND TIP CONTOUR ONLY',
        assetTag: 'NOSE_BRIDGE_CONTOUR_ONLY',
        summaryEn: `Delicate anime nose bridge contour and tip (${noseInfo.en}).`,
        positiveContent: `A delicate anime nose bridge contour line and subtle nose tip outline (${noseInfo.en}), floating as a single minimalist lineart graphic.`,
        excludeShort: 'eyes, mouth, chin, cheeks, face skin, head',
        includedGeometry: [
          'delicate anime nose bridge contour line',
          'minimalist nose tip outline and subtle shading dot',
        ],
        excludedGeometry: [
          'eyes', 'eyebrows', 'mouth', 'facial skin outside the nose', 'head',
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
        titleEn: 'PAIR OF DETACHED ANIME EARS SIDE-BY-SIDE ON 16:9 CANVAS',
        assetTag: 'PAIR_OF_DETACHED_EARS_2_COLUMNS',
        summaryEn: 'Side-by-side pair of anime ears on 16:9 canvas (left half: left ear, right half: right ear).',
        positiveContent: 'A side-by-side pair of anime ears arranged on 16:9 canvas (left half contains the left ear with detailed cartilage folds; right half contains the right ear with matching proportion and lighting), floating as detached prosthetic ear sprites.',
        excludeShort: 'head, face skin, jaw, hair, neck, body, middle dividing border line',
        includedGeometry: [
          'left column: left ear with detailed inner cartilage and earlobe',
          'right column: right ear with detailed inner cartilage and earlobe',
          'side-by-side 2-column layout on 16:9 canvas',
        ],
        excludedGeometry: [
          'face skin', 'forehead', 'jawline', 'hair', 'head', 'neck', 'body',
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
        assetTag: 'MOUTH_AND_LIP_CONTOUR_ONLY',
        summaryEn: `Lip and mouth opening contour (${mouthInfo.en}).`,
        positiveContent: `An isolated anime mouth opening contour with upper and lower lip curves (${mouthInfo.en}), floating as an independent graphic sticker.`,
        excludeShort: 'nose, chin, cheeks, face skin, head',
        includedGeometry: [
          'upper lip line and color',
          'lower lip line and color',
          'mouth expression opening contour',
        ],
        excludedGeometry: [
          'nose', 'chin', 'cheeks', 'facial skin surrounding mouth', 'head',
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
        assetTag: 'COMPLETE_PAIR_OF_ANIME_EYES_ONLY',
        summaryEn: `Complete pair of anime eyes (${eyeShapeInfo.en}, ${eyeColInfo.en}).`,
        positiveContent: `A complete pair of anime eyes (${eyeShapeInfo.en}, ${eyeColInfo.en}) including sclera, colored irises, pupils, lash lines, and glints, floating as isolated sticker graphics.`,
        excludeShort: 'face skin, forehead, eyebrows, nose, mouth, head',
        includedGeometry: [
          'left eye complete structure (sclera, iris, pupil, lash line)',
          'right eye complete structure (sclera, iris, pupil, lash line)',
          'internal eye glints and reflections',
        ],
        excludedGeometry: [
          'face skin', 'forehead', 'eyebrows', 'nose', 'mouth', 'head',
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
        titleEn: 'HEADLESS ARMLESS TORSO COSTUME ROBE GARMENT SPRITE',
        assetTag: 'HEADLESS_ARMLESS_TORSO_COSTUME_ONLY',
        summaryEn: `Costume chest tunic, waist sash, and collar garment (${costumeInfo.en}, ${costumeColorVi}).`,
        positiveContent: `A headless, armless costume tunic and waist sash robe garment in color theme (${costumeColorVi}, ${costumeInfo.en}), with clean hollow collar cut at neck, clean hollow armholes at shoulders, and clean waist cut, as if displayed on an invisible torso mannequin.`,
        excludeShort: 'head, neck skin, arms, hands, legs, feet, flowing cape',
        includedGeometry: [
          'chest tunic / armor plate / robe body',
          `waistband / sash in color theme (${costumeColorVi})`,
          'clean collar opening cut at neck',
          'clean armhole openings cut at shoulders',
          'clean lower torso waist cut',
        ],
        excludedGeometry: [
          'head', 'neck skin', 'arms', 'hands', 'legs', 'feet', 'cape',
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
        titleEn: 'SINGLE ISOLATED LEFT UPPER ARM SLEEVE CYLINDER',
        assetTag: 'LEFT_UPPER_ARM_SLEEVE_ONLY',
        summaryEn: `Left upper bicep arm sleeve segment (${costumeColorVi}).`,
        positiveContent: `A single isolated left upper arm sleeve cylinder in color (${costumeColorVi}), cut cleanly at the shoulder joint and elbow joint, floating as a detached limb segment.`,
        excludeShort: 'torso, chest, head, forearm, hand, other arm',
        includedGeometry: [
          'left upper arm bicep limb tube',
          `sleeve fabric covering left upper arm in (${costumeColorVi})`,
          'clean cut line at shoulder joint',
          'clean cut line at elbow joint',
        ],
        excludedGeometry: [
          'torso', 'chest', 'neck', 'head', 'forearm', 'hand', 'other arm',
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
        titleEn: 'SINGLE ISOLATED LEFT FOREARM BRACER SLEEVE CYLINDER',
        assetTag: 'LEFT_FOREARM_BRACER_ONLY',
        summaryEn: `Left forearm sleeve and bracer segment (${costumeColorVi}).`,
        positiveContent: `A single isolated left forearm bracer sleeve cylinder in color (${costumeColorVi}), cut cleanly at the elbow joint and wrist joint, floating as a detached limb segment.`,
        excludeShort: 'upper arm, shoulder, torso, hand, fingers, other arm',
        includedGeometry: [
          'left forearm limb tube',
          `forearm bracer / cuff / sleeve fabric in (${costumeColorVi})`,
          'clean cut line at elbow joint',
          'clean cut line at wrist joint',
        ],
        excludedGeometry: [
          'upper arm', 'shoulder', 'torso', 'hand', 'fingers', 'other arm',
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
        titleEn: 'SINGLE ISOLATED LEFT ANIME HAND AND FINGERS',
        assetTag: 'LEFT_HAND_AND_FINGERS_ONLY',
        summaryEn: 'Left hand, palm, and fingers severed cleanly at wrist joint.',
        positiveContent: 'A single isolated left anime hand and fingers in clear gesture, cut cleanly at the wrist joint, floating as a detached glove/hand sprite.',
        excludeShort: 'forearm, arm, torso, body, weapon',
        includedGeometry: [
          'left palm and fingers in clear gesture',
          'clean cut boundary at wrist joint',
        ],
        excludedGeometry: [
          'forearm', 'elbow', 'arm', 'torso', 'body', 'weapon',
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
        titleEn: 'SINGLE ISOLATED RIGHT UPPER ARM SLEEVE CYLINDER',
        assetTag: 'RIGHT_UPPER_ARM_SLEEVE_ONLY',
        summaryEn: `Right upper bicep arm sleeve segment (${costumeColorVi}).`,
        positiveContent: `A single isolated right upper arm sleeve cylinder in color (${costumeColorVi}), cut cleanly at the shoulder joint and elbow joint, floating as a detached limb segment.`,
        excludeShort: 'torso, chest, head, forearm, hand, other arm',
        includedGeometry: [
          'right upper arm bicep limb tube',
          `sleeve fabric covering right upper arm in (${costumeColorVi})`,
          'clean cut line at shoulder joint',
          'clean cut line at elbow joint',
        ],
        excludedGeometry: [
          'torso', 'chest', 'neck', 'head', 'forearm', 'hand', 'other arm',
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
        titleEn: 'SINGLE ISOLATED RIGHT FOREARM BRACER SLEEVE CYLINDER',
        assetTag: 'RIGHT_FOREARM_BRACER_ONLY',
        summaryEn: `Right forearm sleeve and bracer segment (${costumeColorVi}).`,
        positiveContent: `A single isolated right forearm bracer sleeve cylinder in color (${costumeColorVi}), cut cleanly at the elbow joint and wrist joint, floating as a detached limb segment.`,
        excludeShort: 'upper arm, shoulder, torso, hand, fingers, other arm',
        includedGeometry: [
          'right forearm limb tube',
          `forearm bracer / cuff / sleeve fabric in (${costumeColorVi})`,
          'clean cut line at elbow joint',
          'clean cut line at wrist joint',
        ],
        excludedGeometry: [
          'upper arm', 'shoulder', 'torso', 'hand', 'fingers', 'other arm',
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
        titleEn: 'SINGLE ISOLATED RIGHT ANIME HAND AND FINGERS',
        assetTag: 'RIGHT_HAND_AND_FINGERS_ONLY',
        summaryEn: 'Right hand, palm, and fingers severed cleanly at wrist joint.',
        positiveContent: 'A single isolated right anime hand and fingers in clear gesture, cut cleanly at the wrist joint, floating as a detached glove/hand sprite.',
        excludeShort: 'forearm, arm, torso, body, weapon',
        includedGeometry: [
          'right palm and fingers in clear gesture',
          'clean cut boundary at wrist joint',
        ],
        excludedGeometry: [
          'forearm', 'elbow', 'arm', 'torso', 'body', 'weapon',
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
        titleEn: 'SINGLE ISOLATED LEFT THIGH PANTS CYLINDER',
        assetTag: 'LEFT_THIGH_PANTS_CYLINDER_ONLY',
        summaryEn: `Left thigh garment/pants limb segment (${costumeColorVi}).`,
        positiveContent: `A single isolated left thigh pants limb tube in color (${costumeColorVi}), cut cleanly at the hip joint and knee joint, floating as a detached limb segment.`,
        excludeShort: 'torso, pelvis, shin, boot, foot, other leg',
        includedGeometry: [
          'left thigh limb tube',
          `fabric/pants covering left thigh in (${costumeColorVi})`,
          'clean cut line at hip joint',
          'clean cut line at knee joint',
        ],
        excludedGeometry: [
          'torso', 'pelvis', 'shin', 'boot', 'foot', 'other leg',
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
        titleEn: 'SINGLE ISOLATED LEFT SHIN AND BOOT SEGMENT',
        assetTag: 'LEFT_SHIN_AND_BOOT_ONLY',
        summaryEn: `Left lower leg and boot (${costumeColorVi}).`,
        positiveContent: `A single isolated left lower shin and boot segment in color (${costumeColorVi}) with knee guard, cut cleanly at the knee joint, floating as a detached boot sprite.`,
        excludeShort: 'thigh, hip, torso, body, other leg',
        includedGeometry: [
          'left shin limb tube',
          `left boot / footwear in (${costumeColorVi})`,
          'knee cap guard',
          'clean cut line at knee joint',
        ],
        excludedGeometry: [
          'thigh', 'hip', 'torso', 'body', 'other leg',
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
        titleEn: 'SINGLE ISOLATED RIGHT THIGH PANTS CYLINDER',
        assetTag: 'RIGHT_THIGH_PANTS_CYLINDER_ONLY',
        summaryEn: `Right thigh garment/pants limb segment (${costumeColorVi}).`,
        positiveContent: `A single isolated right thigh pants limb tube in color (${costumeColorVi}), cut cleanly at the hip joint and knee joint, floating as a detached limb segment.`,
        excludeShort: 'torso, pelvis, shin, boot, foot, other leg',
        includedGeometry: [
          'right thigh limb tube',
          `fabric/pants covering right thigh in (${costumeColorVi})`,
          'clean cut line at hip joint',
          'clean cut line at knee joint',
        ],
        excludedGeometry: [
          'torso', 'pelvis', 'shin', 'boot', 'foot', 'other leg',
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
        titleEn: 'SINGLE ISOLATED RIGHT SHIN AND BOOT SEGMENT',
        assetTag: 'RIGHT_SHIN_AND_BOOT_ONLY',
        summaryEn: `Right lower leg and boot (${costumeColorVi}).`,
        positiveContent: `A single isolated right lower shin and boot segment in color (${costumeColorVi}) with knee guard, cut cleanly at the knee joint, floating as a detached boot sprite.`,
        excludeShort: 'thigh, hip, torso, body, other leg',
        includedGeometry: [
          'right shin limb tube',
          `right boot / footwear in (${costumeColorVi})`,
          'knee cap guard',
          'clean cut line at knee joint',
        ],
        excludedGeometry: [
          'thigh', 'hip', 'torso', 'body', 'other leg',
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
        titleEn: 'ISOLATED FLOWING CAPE / MANTLE FABRIC SPRITE',
        assetTag: 'FLOWING_CAPE_MANTLE_FABRIC_ONLY',
        summaryEn: `Flowing cape and fabric ribbons (${costumeColorVi}).`,
        positiveContent: `A detached flowing cape cloth mantle in color (${costumeColorVi}) with shoulder clasps and floating ribbon tails, floating as an independent garment piece.`,
        excludeShort: 'character body, torso, chest, arms, hands, legs, head',
        includedGeometry: [
          `back cape drape fabric in (${costumeColorVi})`,
          'flowing ribbon tails',
          'shoulder clasp attachments',
        ],
        excludedGeometry: [
          'torso', 'chest', 'arms', 'hands', 'legs', 'head', 'character body',
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
        titleEn: 'ISOLATED WEAPON / MAGICAL PROP ITEM SPRITE',
        assetTag: 'WEAPON_ITEM_PROP_ONLY',
        summaryEn: `Isolated weapon artifact (${propInfo.en}).`,
        positiveContent: `A standalone weapon artifact prop (${propInfo.en}) with blade, hilt, scabbard, and clean silhouette, floating as an independent inventory item.`,
        excludeShort: 'character figure, hands holding weapon, body, scenery',
        includedGeometry: [
          'complete weapon blade, hilt, scabbard',
          'clean silhouette of magical prop',
        ],
        excludedGeometry: [
          'character figure', 'hands holding weapon', 'arms', 'body', 'background scenery',
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
