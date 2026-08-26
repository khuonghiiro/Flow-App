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
}, angleType?: string): Asset2DComponentDef {
  const {
    hairColInfo, hairTexInfo, hairLenInfo, hairAccInfo, bangsStyleInfo,
    eyeShapeInfo, eyeColInfo, noseInfo, mouthInfo,
    costumeInfo, costumeColorVi, propInfo
  } = options;

  const isAngle0 = !angleType || angleType === '000_front' || angleType === 'front';
  const isAngle45 = angleType === '045_three_quarter' || angleType === 'three_quarter' || angleType === '45';
  const isAngle90 = angleType === '090_side' || angleType === 'profile_side' || angleType === '90';

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
        positiveContent: `An independent standalone stylized front bangs hair layer in hair color (${hairColInfo.en}), featuring ${bangsDescEn} framing the forehead, textured layered front hair locks, and side temple wisps ending near cheekbone level, with the top root following the natural rounded arc of the forehead hairline. Floating completely alone in mid-air on empty green screen like an overlay hair piece ready to place onto a bald mannequin head. The area behind the bangs is 100% pure flat Chroma Green #00FF00 with zero skull, zero face, and zero back hair.`,
        excludeShort: 'base head, back hair, rear hair mass, long hair behind, ponytail, hair bun, head, skull, face, eyes, neck, body, straight geometric box cut',
        includedGeometry: [
          `clip-on front bangs hair accessory in style: ${bangsDescEn}`,
          'side temple wisps ending above chin level',
          `thin foreground hair strands in hair color (${hairColInfo.en})`,
          'natural rounded root arc matching forehead hairline',
        ],
        excludedGeometry: [
          'base head', 'back hair', 'rear hair mass', 'long hair behind', 'ponytail', 'hair bun', 'head', 'skull', 'face', 'eyes', 'neck', 'body', 'straight box cut',
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
    case 'dau':
    case 'dau_khong_co':
      return {
        id: 'dau_khong_co',
        nameVi: 'Đầu Anime Trần (Không Cổ - Head Only)',
        titleEn: 'BLANK ANIME HEAD ONLY (NO NECK - CHIN BASE)',
        assetTag: 'BLANK_ANIME_HEAD_NO_NECK_CHIN_BASE_ONLY',
        summaryEn: 'Completely featureless blank anime head with clean rounded jawline and chin base, strictly NO neck attached.',
        positiveContent: 'An isolated 2D cutout rigging asset: Blank anime head only from crown to chin/jawline. Smooth fair peach anime skin tone, clean rounded chin with smooth convex bottom contour, cute anime ears on sides, completely bald scalp, zero facial features. Strictly NO neck attached (neck belongs to the torso component). Front view 0° orthographic, centered on flat chroma green #00FF00 background.',
        excludeShort: 'neck, collarbones, body, torso, hair, eyes, eyebrows, nose, mouth, clothes',
        includedGeometry: [
          'blank anime head silhouette',
          'smooth jawline and chin contour',
          'cute ears on left and right',
          'clean convex bottom arc at chin',
        ],
        excludedGeometry: [
          'neck', 'collarbones', 'body', 'torso', 'hair', 'eyebrows', 'eyes', 'nose', 'mouth', 'clothes',
        ],
        rearVisibility: 'visible',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 30,
        filePrefix: '01_dau',
        idealAspectRatio: '1:1',
      };

    case 'trong_den_iris':
      return {
        id: 'trong_den_iris',
        nameVi: isAngle0
          ? 'Mống Mắt Đơn Đối Xứng (Single Iris 1:1)'
          : isAngle45
            ? 'Cặp Mống Mắt Phối Cảnh 3/4 (3/4 Perspective Irises)'
            : 'Mống Mắt Nhìn Ngang (Single Profile Iris)',
        titleEn: isAngle0
          ? 'ISOLATED SINGLE ANIME EYE IRIS DISC AND PUPIL ONLY'
          : isAngle45
            ? 'PAIR OF ANIME EYE IRIS DISCS IN 45° THREE-QUARTER PERSPECTIVE'
            : 'ISOLATED SINGLE LATERAL PROFILE ANIME IRIS DISC',
        assetTag: isAngle0
          ? 'SINGLE_EYE_IRIS_AND_PUPIL_DISC_ONLY'
          : isAngle45
            ? 'THREE_QUARTER_PAIR_OF_IRIS_DISCS_ONLY'
            : 'SINGLE_LATERAL_PROFILE_IRIS_DISC_ONLY',
        summaryEn: isAngle0
          ? `Single circular anime eye iris disc and pupil (${eyeColInfo.en}) with internal color gradient reflections.`
          : isAngle45
            ? `Pair of anime eye iris discs (${eyeColInfo.en}) in 45° three-quarter perspective turned towards viewer's left.`
            : `Single anime eye iris disc in lateral 90° profile view.`,
        positiveContent: isAngle0
          ? `A single isolated circular anime eye iris disc and dark pupil center in color (${eyeColInfo.en}) with internal luminous gradient reflections, floating as an isolated graphic sticker (bilaterally symmetrical single iris asset).`
          : isAngle45
            ? `A pair of anime eye iris discs in color (${eyeColInfo.en}) arranged in 45° three-quarter perspective turned towards the viewer's left (near iris circular and wide, far iris perspective-compressed and slightly oval), floating as isolated graphic stickers.`
            : `A single isolated anime eye iris disc seen in lateral 90° profile view (curved dome slice in color (${eyeColInfo.en}) with pupil edge), floating as an isolated graphic sticker.`,
        excludeShort: isAngle0
          ? 'pair of irises, second iris, other eye, sclera, eyeball whites, eyelids, eyelashes, face skin, head'
          : 'sclera, eyeball whites, eyelids, eyelashes, face skin, head',
        includedGeometry: isAngle0
          ? [`single circular colored anime iris in color (${eyeColInfo.en})`, 'crisp circular pupil center', 'internal luminous iris gradient reflections']
          : [`colored anime irises in color (${eyeColInfo.en})`, 'pupil center', 'gradient reflections'],
        excludedGeometry: ['sclera', 'eyelids', 'eyelashes', 'face skin', 'head', 'body'],
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
        nameVi: isAngle0
          ? 'Tròng Trắng Đơn Có Bóng Hốc Mắt (Single Sclera Base 1:1)'
          : isAngle45
            ? 'Cặp Tròng Trắng Phối Cảnh 3/4 (3/4 Perspective Scleras)'
            : 'Tròng Trắng Nhìn Ngang (Single Profile Sclera)',
        titleEn: isAngle0
          ? 'ISOLATED SINGLE ANIME EYE SCLERA WHITE BASE WITH UPPER SOCKET SHADOW ONLY'
          : isAngle45
            ? 'PAIR OF ANIME EYE SCLERA BASE SHAPES IN 45° THREE-QUARTER PERSPECTIVE'
            : 'ISOLATED SINGLE LATERAL PROFILE SCLERA SHAPE',
        assetTag: isAngle0
          ? 'SINGLE_EYE_SCLERA_WHITE_WITH_SOCKET_SHADOW_ONLY'
          : isAngle45
            ? 'THREE_QUARTER_PAIR_OF_SCLERA_WHITES_ONLY'
            : 'SINGLE_LATERAL_PROFILE_SCLERA_WHITE_ONLY',
        summaryEn: isAngle0
          ? 'Single smooth pure white anime sclera base shape with realistic upper socket shadow gradient.'
          : isAngle45
            ? 'Pair of pure white anime sclera base shapes in 45° three-quarter perspective.'
            : 'Single anime sclera shape in lateral 90° profile.',
        positiveContent: isAngle0
          ? 'A single isolated almond-shaped pure white anime eye sclera base with a soft smooth dark purple-gray ambient upper eye-socket shadow gradient along the upper curved border (reproducing the realistic shadow cast by the upper eyelid onto the white eyeball), floating as an independent sticker graphic on green screen (bilaterally symmetrical single sclera asset).'
          : isAngle45
            ? 'A pair of pure white anime eye sclera base shapes with upper shadow gradient arranged in 45° three-quarter perspective turned towards the viewer\'s left (near sclera almond and wide, far sclera perspective-foreshortened), floating as isolated sticker graphics.'
            : 'A single isolated pure white anime eye sclera triangular slice in lateral 90° side profile view with upper shadow, floating as an isolated sticker graphic.',
        excludeShort: isAngle0
          ? 'iris, pupil, black eyelash lines, eyebrows, face skin, head, body'
          : 'iris, pupil, black eyelash lines, eyebrows, face skin, head, body',
        includedGeometry: [
          'pure white almond sclera shapes',
          'smooth upper eye-socket ambient shadow gradient',
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
        nameVi: isAngle0
          ? 'Điểm Sáng Mắt Đơn Đối Xứng (Single Highlight 1:1)'
          : isAngle45
            ? 'Cặp Điểm Sáng Mắt Phối Cảnh 3/4'
            : 'Điểm Sáng Mắt Nhìn Ngang',
        titleEn: isAngle0
          ? 'ISOLATED SINGLE ANIME EYE HIGHLIGHT GLINT SPOTS ONLY'
          : isAngle45
            ? 'PAIR OF ANIME EYE HIGHLIGHT GLINT SPOTS IN 45° THREE-QUARTER PERSPECTIVE'
            : 'ISOLATED SINGLE LATERAL PROFILE EYE HIGHLIGHT GLINT',
        assetTag: isAngle0
          ? 'SINGLE_EYE_HIGHLIGHT_SPARKLES_ONLY'
          : isAngle45
            ? 'THREE_QUARTER_PAIR_OF_HIGHLIGHT_SPARKLES_ONLY'
            : 'SINGLE_LATERAL_PROFILE_HIGHLIGHT_SPARKLES_ONLY',
        summaryEn: 'Crisp pure white reflection dots and star glints for anime eyes.',
        positiveContent: isAngle0
          ? 'A single isolated cluster of crisp pure white anime eye highlight glint dots and star reflection shapes, floating as an isolated sticker graphic (bilaterally symmetrical single highlight asset).'
          : isAngle45
            ? 'A pair of crisp pure white anime eye highlight glint clusters arranged in 45° three-quarter perspective turned towards the viewer\'s left (near cluster wider, far cluster perspective-compressed), floating as isolated sticker graphics.'
            : 'A single isolated cluster of crisp pure white anime eye highlight glints in lateral 90° profile view, floating as an isolated sticker graphic.',
        excludeShort: isAngle0
          ? 'pair of highlights, second eye highlights, iris, pupil, sclera, eyelids, face skin, head'
          : 'iris, pupil, sclera, eyelids, face skin, head',
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
        nameVi: isAngle0
          ? 'Mi Mắt Đơn Đối Xứng (Single Eyelash Contour 1:1)'
          : isAngle45
            ? 'Cặp Mi Mắt Phối Cảnh 3/4 (3/4 Perspective Eyelashes)'
            : 'Mi Mắt Nhìn Ngang (Single Profile Eyelash)',
        titleEn: isAngle0
          ? 'ISOLATED SINGLE ANIME PURE EYELASH LINE CONTOUR ONLY'
          : isAngle45
            ? 'PAIR OF ANIME EYELASH LINE CONTOURS IN 45° THREE-QUARTER PERSPECTIVE'
            : 'ISOLATED SINGLE LATERAL PROFILE EYELASH LINE',
        assetTag: isAngle0
          ? 'SINGLE_EYELASH_CONTOUR_ONLY'
          : isAngle45
            ? 'THREE_QUARTER_PAIR_OF_EYELASH_CONTOURS_ONLY'
            : 'SINGLE_LATERAL_PROFILE_EYELASH_CONTOUR_ONLY',
        summaryEn: 'Crisp anime upper/lower eyelash lineart without any inner circular iris outlines or white fill.',
        positiveContent: isAngle0
          ? 'A single isolated pair of crisp sharp anime black upper and lower eyelash line curves with delicate corner lash strokes matching an almond anime eye shape. The middle area between the upper and lower lash lines is 100% pure flat Chroma Green #00FF00 empty background, completely hollow with zero circles, zero iris lines, and zero white fill, floating as a standalone lineart overlay sticker (bilaterally symmetrical single eyelid asset).'
          : isAngle45
            ? 'A pair of crisp sharp anime black upper and lower eyelash line curves arranged in 45° three-quarter perspective turned towards the viewer\'s left (near lash line wider and fully contoured, far lash line foreshortened), with hollow green center, floating as isolated lineart sticker graphics.'
            : 'A single isolated crisp sharp anime black eyelash line curve seen in lateral 90° side profile view facing sideways to the viewer\'s left, floating as an isolated lineart sticker graphic.',
        excludeShort: isAngle0
          ? 'circular iris outline, round circle in center, pupil, sclera white fill, eyeball, eyebrows, face skin, head, body'
          : 'circular iris outline, round circle in center, pupil, sclera white fill, eyeball, eyebrows, face skin, head, body',
        includedGeometry: [
          'sharp black upper lash line curve',
          'sharp black lower lash line curve',
          'corner eyelashes',
        ],
        excludedGeometry: [
          'circular iris outline', 'pupil', 'sclera white fill', 'eyebrows', 'face skin', 'head',
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
        nameVi: isAngle0
          ? 'Lông Mày Đơn Đối Xứng (Single Eyebrow 1:1)'
          : isAngle45
            ? 'Cặp Lông Mày Phối Cảnh 3/4 (3/4 Perspective Eyebrows)'
            : 'Lông Mày Nhìn Ngang (Single Profile Eyebrow)',
        titleEn: isAngle0
          ? 'ISOLATED SINGLE ANIME EYEBROW STROKE ONLY'
          : isAngle45
            ? 'PAIR OF ANIME EYEBROWS IN 45° THREE-QUARTER PERSPECTIVE'
            : 'ISOLATED SINGLE LATERAL PROFILE ANIME EYEBROW STROKE',
        assetTag: isAngle0
          ? 'SINGLE_EYEBROW_STROKE_ONLY'
          : isAngle45
            ? 'THREE_QUARTER_PAIR_OF_ANIME_EYEBROWS_ONLY'
            : 'SINGLE_LATERAL_PROFILE_EYEBROW_ONLY',
        summaryEn: isAngle0
          ? `Single isolated eyebrow hair stroke in hair color (${hairColInfo.en}).`
          : isAngle45
            ? `Pair of anime eyebrows in hair color (${hairColInfo.en}) in 45° three-quarter perspective.`
            : `Single anime eyebrow stroke in lateral 90° profile.`,
        positiveContent: isAngle0
          ? `A single isolated delicate anime eyebrow line stroke in hair color (${hairColInfo.en}), elegant tapered calligraphy arch stroke, floating as an independent lineart graphic (bilaterally symmetrical single eyebrow asset).`
          : isAngle45
            ? `A pair of anime eyebrows arranged in 45° three-quarter perspective turned towards the viewer's left (the near left-side eyebrow is longer, wider, and arched; the far right-side eyebrow is shorter and foreshortened in perspective depth) in hair color (${hairColInfo.en}), floating as independent lineart graphics.`
            : `A single isolated anime eyebrow stroke seen in lateral 90° side profile view facing sideways to the viewer's left in hair color (${hairColInfo.en}), floating as an independent lineart graphic.`,
        excludeShort: isAngle0
          ? 'pair of eyebrows, second eyebrow, other eyebrow, eyes, eyelids, forehead skin, face, head, body'
          : 'eyes, eyelids, forehead skin, face, head, body',
        includedGeometry: [
          `eyebrow line stroke in hair color (${hairColInfo.en})`,
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
        nameVi: isAngle90 ? 'Sống Mũi Nhìn Ngang (Side Profile Nose)' : 'Sống Mũi Thanh Tú (Nose Only)',
        titleEn: 'ISOLATED ANIME NOSE BRIDGE AND TIP CONTOUR ONLY',
        assetTag: 'NOSE_BRIDGE_CONTOUR_ONLY',
        summaryEn: `Delicate anime nose bridge contour and tip (${noseInfo.en}).`,
        positiveContent: isAngle90
          ? `A single isolated anime nose bridge and tip contour seen in lateral 90° side profile view facing sideways to the viewer's left (${noseInfo.en}), floating as a single minimalist lineart graphic.`
          : isAngle45
            ? `A delicate anime nose bridge contour line and subtle nose tip outline in 45° three-quarter perspective turned towards viewer's left (${noseInfo.en}), floating as a single minimalist lineart graphic.`
            : `A delicate minimalist anime nose bridge contour line and subtle nose tip outline with soft bottom shadow dot (${noseInfo.en}), floating as a single minimalist lineart graphic sticker.`,
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
    case 'tai':
      return {
        id: 'doi_tai',
        nameVi: isAngle0
          ? 'Vành Tai Đơn Đối Xứng (Single Ear 1:1)'
          : isAngle45
            ? 'Vành Tai Góc Nghiêng 3/4 (3/4 Perspective Ear)'
            : 'Vành Tai Nhìn Ngang (Single Profile Ear)',
        titleEn: isAngle0
          ? 'ISOLATED SINGLE DETACHED ANIME EAR SPRITE'
          : isAngle45
            ? 'ISOLATED ANIME EAR IN 45° THREE-QUARTER PERSPECTIVE'
            : 'ISOLATED SINGLE LATERAL SIDE PROFILE EAR SPRITE',
        assetTag: isAngle0
          ? 'SINGLE_DETACHED_ANIME_EAR_ONLY'
          : isAngle45
            ? 'THREE_QUARTER_ANIME_EAR_ONLY'
            : 'SINGLE_LATERAL_SIDE_PROFILE_EAR_ONLY',
        summaryEn: isAngle0
          ? 'Single isolated anime ear on 1:1 canvas (bilaterally symmetrical single ear asset).'
          : isAngle45
            ? 'Single anime ear seen in 45° three-quarter view.'
            : 'Single anime ear seen in lateral 90° side profile.',
        positiveContent: isAngle0
          ? 'A single isolated anime ear with detailed inner cartilage curves, earlobe, and smooth porcelain skin tone, cut cleanly at the base where it attaches to the side of the head, floating as an independent prosthetic ear sticker (bilaterally symmetrical single ear asset).'
          : isAngle45
            ? 'A single isolated anime ear seen in 45° three-quarter perspective turned towards viewer\'s left, showing outer helix curvature, antihelix fold, and earlobe depth, floating as an independent prosthetic ear sticker.'
            : 'A single isolated anime ear seen in lateral 90° side profile view facing sideways to the viewer\'s left, showing clear outer helix rim, antihelix fold, tragus, and earlobe in smooth anime skin tone, floating as an independent prosthetic ear sticker.',
        excludeShort: isAngle0
          ? 'pair of ears, second ear, other ear, head, face skin, jaw, hair, neck, body'
          : 'head, face skin, jaw, hair, neck, body',
        includedGeometry: [
          'single ear with detailed inner cartilage and earlobe',
          'clean cut at base connection to head',
        ],
        excludedGeometry: [
          'face skin', 'forehead', 'jawline', 'hair', 'head', 'neck', 'body',
        ],
        rearVisibility: 'visible',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 26,
        filePrefix: '04g_tai',
        idealAspectRatio: '1:1',
      };

    case 'mieng':
      return {
        id: 'mieng',
        nameVi: 'Khẩu Hình Miệng (Mouth & Lips)',
        titleEn: 'ISOLATED ANIME MOUTH OPENING AND LIP CONTOURS ONLY',
        assetTag: 'MOUTH_AND_LIP_CONTOUR_ONLY',
        summaryEn: `Lip and mouth opening contour (${mouthInfo.en}).`,
        positiveContent: isAngle90
          ? `An isolated anime mouth opening contour seen in lateral 90° side profile view facing sideways to the viewer's left (${mouthInfo.en}), floating as an independent graphic sticker.`
          : isAngle45
            ? `An isolated anime mouth opening contour with upper and lower lip curves in 45° three-quarter perspective turned towards viewer's left (${mouthInfo.en}), floating as an independent graphic sticker.`
            : `An isolated anime mouth opening contour with upper and lower lip curves (${mouthInfo.en}) and soft pink inner tone, floating as an independent graphic sticker.`,
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
        nameVi: isAngle0
          ? 'Con Mắt Đơn Đối Xứng (Single Complete Eye 1:1)'
          : isAngle45
            ? 'Cặp Mắt Phối Cảnh 3/4 (3/4 Perspective Pair of Eyes)'
            : 'Con Mắt Nhìn Ngang (Single Profile Eye)',
        titleEn: isAngle0
          ? 'ISOLATED SINGLE COMPLETE ANIME EYE SPRITE'
          : isAngle45
            ? 'PAIR OF ANIME EYES IN 45° THREE-QUARTER PERSPECTIVE'
            : 'ISOLATED SINGLE LATERAL SIDE PROFILE ANIME EYE SPRITE',
        assetTag: isAngle0
          ? 'SINGLE_COMPLETE_ANIME_EYE_ONLY'
          : isAngle45
            ? 'THREE_QUARTER_PAIR_OF_ANIME_EYES_ONLY'
            : 'SINGLE_LATERAL_SIDE_PROFILE_ANIME_EYE_ONLY',
        summaryEn: isAngle0
          ? `Single complete anime eye (${eyeShapeInfo.en}, ${eyeColInfo.en}).`
          : isAngle45
            ? `Pair of anime eyes (${eyeShapeInfo.en}, ${eyeColInfo.en}) in 45° three-quarter perspective.`
            : `Single anime eye in lateral 90° profile.`,
        positiveContent: isAngle0
          ? `A single isolated complete anime eye (large expressive almond eye, pure white sclera, vibrant crystal iris in color (${eyeColInfo.en}) with detailed radial light reflections and dark pupil center, sharp black upper anime lash line with delicate corner lashes, and crisp pure white highlight glints), floating as an independent sticker graphic (bilaterally symmetrical single eye asset).`
          : isAngle45
            ? `A pair of anime eyes arranged in 45° three-quarter perspective turned towards the viewer's left (the near left-side eye is larger, wider, and fully open; the far right-side eye is narrower and foreshortened in perspective depth). Both eyes have pure white sclera, luminous crystal irises in color (${eyeColInfo.en}) with pupil dots, sharp anime lash lines, and crisp glints, floating as isolated sticker graphics.`
            : `A single isolated anime eye seen in lateral 90° side profile view facing sideways to the viewer's left (characteristic triangular conical anime eye profile silhouette, curved upper lash angle, curved iris dome in (${eyeColInfo.en}) with glowing pupil reflections, and white sclera triangular corner), floating as an independent sticker graphic.`,
        excludeShort: isAngle0
          ? 'pair of eyes, second eye, other eye, eyebrows, face skin, forehead, nose, mouth, head'
          : 'face skin, forehead, eyebrows, nose, mouth, head, body',
        includedGeometry: [
          'complete eye structure (sclera, iris, pupil, lash line)',
          'internal eye glints and reflections',
        ],
        excludedGeometry: [
          'face skin', 'forehead', 'eyebrows', 'nose', 'mouth', 'head',
        ],
        rearVisibility: 'hidden',
        groupId: '01_head_face',
        groupNameVi: 'Khuôn Mặt & Ngũ Quan',
        zIndex: 40,
        filePrefix: '04_mat',
        idealAspectRatio: '1:1',
      };

    case 'than_mannequin':
    case 'than_co':
    case 'than':
      return {
        id: 'than_co',
        nameVi: 'Thân Kèm Cổ (Torso with Attached Neck)',
        titleEn: 'SMOOTH ANIME TORSO WITH ATTACHED NECK',
        assetTag: 'SMOOTH_ANIME_TORSO_WITH_ATTACHED_NECK',
        summaryEn: 'Smooth fair peach anime torso body with full neck attached, ready for head attachment.',
        positiveContent: 'Isolated 2D cutout rigging asset: The TORSO segment of a 2D anime character WITH FULL NECK ATTACHED. Smooth fair peach anime skin tone with soft rosy undertone, clean collarbones, slender waist, smooth hips. The neck extends upward naturally from the shoulders with a clean rounded top contour where the head connects. Left and right shoulders and hips have clean rounded insertion sockets. Front view 0° orthographic, centered on flat chroma green #00FF00 background.',
        excludeShort: 'head, upper arms, forearms, hands, thighs, shins, feet, clothes',
        includedGeometry: [
          'smooth anime torso body',
          'full neck attached extending upward',
          'clean rounded shoulder sockets',
          'clean rounded hip sockets',
        ],
        excludedGeometry: [
          'head', 'upper arms', 'forearms', 'hands', 'thighs', 'shins', 'feet', 'clothes',
        ],
        rearVisibility: 'visible',
        groupId: '02_mannequin_limbs',
        groupNameVi: 'Khung Xương Cơ Thể Mannequin',
        zIndex: 11,
        filePrefix: '02_than_co',
        idealAspectRatio: '3:4',
      };

    case 'bap_tay':
    case 'canh_tay_trai':
    case 'canh_tay_phai':
      return {
        id: 'bap_tay',
        nameVi: 'Bắp Tay (Chỏm lồi vai & khuỷu)',
        titleEn: 'CHIBI UPPER ARM WITH CONVEX DOME CAPS',
        assetTag: 'CHIBI_UPPER_ARM_CONVEX_DOME_CAPS',
        summaryEn: 'Fair porcelain upper arm segment with convex rounded dome caps at shoulder and elbow. Angled ~45°.',
        positiveContent: 'Isolated 2D cutout rigging asset: A SINGLE SHORT UPPER ARM SEGMENT ONLY (shoulder to elbow). Fair porcelain anime skin tone with soft rosy-pink undertone, pale peachy-white complexion with subtle pink blush shading at joints, smooth cel-shaded anime shading. Single short tapered cylinder angled ~45° outward. CONVEX JOINT CAPS: Both the top end (shoulder) and bottom end (elbow) terminate with a smooth convex rounded arc (dome-shaped overlap cap) that bulges outward slightly past the cut line for seamless 2D cutout puppet rigging. Centered on solid bright green chroma-key background (#00FF00).',
        excludeShort: 'torso, forearm, hand, clothes',
        includedGeometry: [
          'fair porcelain upper arm at 45°',
          'convex dome cap at shoulder (top)',
          'convex dome cap at elbow (bottom)',
        ],
        excludedGeometry: [
          'torso', 'forearm', 'hand', 'clothes',
        ],
        rearVisibility: 'visible',
        groupId: '02_mannequin_limbs',
        groupNameVi: 'Khung Xương Cơ Thể Mannequin',
        zIndex: 12,
        filePrefix: '02a_bap_tay',
        idealAspectRatio: '3:4',
      };

    case 'cang_tay':
    case 'cang_tay_trai':
    case 'cang_tay_phai':
      return {
        id: 'cang_tay',
        nameVi: 'Cẳng Tay (Chỏm lồi khuỷu & cổ tay)',
        titleEn: 'CHIBI FOREARM WITH CONVEX DOME CAPS',
        assetTag: 'CHIBI_FOREARM_CONVEX_DOME_CAPS',
        summaryEn: 'Fair porcelain forearm segment with convex rounded dome caps at elbow and wrist. Angled ~45°.',
        positiveContent: 'Isolated 2D cutout rigging asset: A SINGLE SHORT FOREARM SEGMENT ONLY (elbow to wrist). Fair porcelain anime skin tone with soft rosy-pink undertone, pale peachy-white complexion with subtle pink blush shading at joints, smooth cel-shaded anime shading. Single short tapered cylinder angled ~45° outward. CONVEX JOINT CAPS: Both the top end (elbow) and bottom end (wrist) terminate with a smooth convex rounded arc (dome-shaped overlap cap) that bulges outward slightly past the cut line for seamless overlap in 2D puppet rigging. Centered on solid bright green chroma-key background (#00FF00).',
        excludeShort: 'upper arm, hand, torso, clothes',
        includedGeometry: [
          'fair porcelain forearm at 45°',
          'convex dome cap at elbow (top)',
          'convex dome cap at wrist (bottom)',
        ],
        excludedGeometry: [
          'upper arm', 'hand', 'torso', 'clothes',
        ],
        rearVisibility: 'visible',
        groupId: '02_mannequin_limbs',
        groupNameVi: 'Khung Xương Cơ Thể Mannequin',
        zIndex: 13,
        filePrefix: '02b_cang_tay',
        idealAspectRatio: '3:4',
      };

    case 'ban_tay':
    case 'ban_tay_trai':
    case 'ban_tay_phai':
      return {
        id: 'ban_tay',
        nameVi: 'Bàn Tay (Chỏm lồi cổ tay + 5 ngón)',
        titleEn: 'CHIBI HAND WITH CONVEX WRIST CAP',
        assetTag: 'CHIBI_HAND_CONVEX_WRIST_CAP',
        summaryEn: 'Fair porcelain hand with convex rounded dome cap at wrist and 5 spread fingers.',
        positiveContent: 'Isolated 2D cutout rigging asset: The LEFT HAND from wrist to fingertips with 5 spread open fingers. Fair porcelain anime skin tone with soft rosy-pink undertone, pale peachy-white complexion with subtle pink blush shading, smooth cel-shaded anime shading. CONVEX JOINT CAP: At the top (wrist connection), the wrist end terminates with a smooth convex rounded arc (dome-shaped overlap cap) bulging outward slightly past the cut line, so when connected to the forearm in 2D puppet rigging there is no concave gap. Centered on solid bright green chroma-key background (#00FF00).',
        excludeShort: 'forearm, arm, torso, clothes',
        includedGeometry: [
          'fair porcelain palm and 5 spread fingers',
          'convex dome cap at wrist (top)',
        ],
        excludedGeometry: [
          'forearm', 'arm', 'torso', 'clothes',
        ],
        rearVisibility: 'visible',
        groupId: '02_mannequin_limbs',
        groupNameVi: 'Khung Xương Cơ Thể Mannequin',
        zIndex: 14,
        filePrefix: '02c_ban_tay',
        idealAspectRatio: '1:1',
      };

    case 'dui':
    case 'dui_trai':
    case 'dui_phai':
      return {
        id: 'dui',
        nameVi: 'Đùi (Chỏm lồi háng & gối)',
        titleEn: 'CHIBI THIGH WITH CONVEX DOME CAPS',
        assetTag: 'CHIBI_THIGH_CONVEX_DOME_CAPS',
        summaryEn: 'Fair porcelain thigh segment with convex rounded dome caps at hip and knee. Front view.',
        positiveContent: 'Isolated 2D cutout rigging asset: The LEFT THIGH segment between hip and knee, viewed from the FRONT (0° orthographic) at slight outward angle. Fair porcelain anime skin tone with soft rosy-pink undertone, pale peachy-white complexion with subtle pink blush shading, smooth cel-shaded anime shading. CONVEX JOINT CAPS: Both the top end (hip connection) and bottom end (knee connection) terminate with a smooth convex rounded arc (dome-shaped overlap cap) that bulges outward slightly past the cut line, so when connected and rotated in 2D puppet rigging there is no concave gap. Centered on solid bright green chroma-key background (#00FF00).',
        excludeShort: 'torso, shin, foot, clothes',
        includedGeometry: [
          'fair porcelain thigh front view',
          'convex dome cap at hip (top)',
          'convex dome cap at knee (bottom)',
        ],
        excludedGeometry: [
          'torso', 'shin', 'foot', 'clothes',
        ],
        rearVisibility: 'visible',
        groupId: '02_mannequin_limbs',
        groupNameVi: 'Khung Xương Cơ Thể Mannequin',
        zIndex: 12,
        filePrefix: '03a_dui',
        idealAspectRatio: '3:4',
      };

    case 'cang_chan':
    case 'cang_chan_trai':
    case 'cang_chan_phai':
      return {
        id: 'cang_chan',
        nameVi: 'Cẳng Chân (Chỏm lồi gối & mắt cá)',
        titleEn: 'CHIBI SHIN WITH CONVEX DOME CAPS',
        assetTag: 'CHIBI_SHIN_CONVEX_DOME_CAPS',
        summaryEn: 'Fair porcelain shin segment with convex rounded dome caps at knee and ankle. Front view.',
        positiveContent: 'Isolated 2D cutout rigging asset: The LEFT SHIN segment between knee and ankle, viewed from the FRONT (0° orthographic). Fair porcelain anime skin tone with soft rosy-pink undertone, pale peachy-white complexion with subtle pink blush shading, smooth cel-shaded anime shading. CONVEX JOINT CAPS: Both the top end (knee connection) and bottom end (ankle connection) terminate with a smooth convex rounded arc (dome-shaped overlap cap) that bulges outward slightly past the cut line for seamless 2D cutout puppet rigging. Centered on solid bright green chroma-key background (#00FF00).',
        excludeShort: 'thigh, foot, torso, clothes',
        includedGeometry: [
          'fair porcelain shin front view',
          'convex dome cap at knee (top)',
          'convex dome cap at ankle (bottom)',
        ],
        excludedGeometry: [
          'thigh', 'foot', 'torso', 'clothes',
        ],
        rearVisibility: 'visible',
        groupId: '02_mannequin_limbs',
        groupNameVi: 'Khung Xương Cơ Thể Mannequin',
        zIndex: 13,
        filePrefix: '03b_cang_chan',
        idealAspectRatio: '3:4',
      };

    case 'ban_chan':
      return {
        id: 'ban_chan',
        nameVi: 'Bàn Chân (Chỏm lồi mắt cá → Ngón)',
        titleEn: 'CHIBI FOOT WITH CONVEX ANKLE CAP',
        assetTag: 'CHIBI_FOOT_CONVEX_ANKLE_CAP',
        summaryEn: 'Fair porcelain foot with convex rounded dome cap at ankle. Front view.',
        positiveContent: 'Isolated 2D cutout rigging asset: The LEFT FOOT from ankle to toes, viewed from the FRONT (0° orthographic) flat on the ground. Fair porcelain anime skin tone with soft rosy-pink undertone, pale peachy-white complexion with subtle pink blush shading, smooth cel-shaded anime shading. CONVEX JOINT CAP: At the top (ankle connection), the ankle end terminates with a smooth convex rounded arc (dome-shaped overlap cap) that bulges outward upward past the cut line, so when connected to the shin in 2D puppet rigging there is no concave gap. Centered on solid bright green chroma-key background (#00FF00).',
        excludeShort: 'shin, leg, torso, clothes, shoes',
        includedGeometry: [
          'fair porcelain foot with toes front view',
          'convex dome cap at ankle (top)',
        ],
        excludedGeometry: [
          'shin', 'leg', 'thigh', 'shoes', 'boots', 'clothes',
        ],
        rearVisibility: 'visible',
        groupId: '02_mannequin_limbs',
        groupNameVi: 'Khung Xương Cơ Thể Mannequin',
        zIndex: 14,
        filePrefix: '03c_ban_chan',
        idealAspectRatio: '1:1',
      };

    case 'than_co_ban':
      return {
        id: 'than_co_ban',
        nameVi: 'Thân Áo Vest V-Neck (Đơn Giản, Không Vòng Cổ)',
        titleEn: 'SIMPLE V-NECK SLEEVELESS VEST NO COLLAR BAND',
        assetTag: 'SIMPLE_VNECK_VEST_NO_COLLAR_BAND_ONLY',
        summaryEn: `Simple V-neck vest, V opening is green screen, no collar band (${costumeInfo.en}, ${costumeColorVi}).`,
        positiveContent: `A standalone simple xianxia cultivator sleeveless vest/bodice in color theme (${costumeColorVi}, ${costumeInfo.en}). The vest has a simple V-neckline — just two front fabric panels that cross at the chest forming a V shape. The triangular V opening area is pure #00FF00 green screen (showing through to character chest skin beneath). There is NO collar band, NO collar ring, NO neckline loop above the V — the vest fabric simply ends at the shoulder straps and the V lapels, nothing connects them across the top. Open armholes. Clean flat waist bottom hem. Floating on green screen (bilaterally symmetrical garment asset).`,
        excludeShort: 'collar band, collar ring, neckline loop, sleeves, belt, skirt, bare skin, head, neck, arms, hands, legs, feet, cape',
        includedGeometry: [
          'front vest body with two crossing V-neck lapels',
          'V-neck opening as pure green screen',
          'shoulder straps (no collar band connecting them)',
          'open armholes',
          'clean flat waist bottom hem',
        ],
        excludedGeometry: [
          'collar band', 'collar ring', 'neckline loop', 'sleeves', 'belt', 'skirt', 'head', 'neck skin', 'arms', 'hands', 'legs', 'feet', 'cape',
        ],
        rearVisibility: 'visible',
        groupId: '03_props_costumes',
        groupNameVi: 'Trang Phục & Y Phục Bóc Tách',
        zIndex: 51,
        filePrefix: '02_than_co_ban',
        idealAspectRatio: '3:4',
      };

    case 'ong_tay_xoe':
      return {
        id: 'ong_tay_xoe',
        nameVi: 'Ống Tay Áo Xòe & Bao Cổ Tay (Flowing Sleeves & Bracers)',
        titleEn: 'MODULAR FLOWING LOWER SLEEVE GARMENT SPRITE',
        assetTag: 'MODULAR_FLOWING_LOWER_SLEEVE_GARMENT_ONLY',
        summaryEn: `Flowing lower sleeve garment and inner bracer wrap (${costumeColorVi}).`,
        positiveContent: `A single isolated xianxia cultivator wide flowing lower sleeve garment piece in color theme (${costumeColorVi}), with decorative border trims and forearm bracer wrap, hollow at the elbow joint top and hollow at the wrist exit, designed to slide over an anime forearm for 2D puppet rigging, floating alone on green screen (bilaterally symmetrical single sleeve asset).`,
        excludeShort: 'bare skin, arm, hand, torso, chest, body, head, legs',
        includedGeometry: [
          `flowing sleeve fabric drape in (${costumeColorVi})`,
          'inner forearm bracer wrap and decorative cuff',
          'hollow elbow top opening',
          'hollow wrist bottom opening',
        ],
        excludedGeometry: [
          'bare skin', 'arm', 'hand', 'torso', 'head', 'legs',
        ],
        rearVisibility: 'visible',
        groupId: '03_props_costumes',
        groupNameVi: 'Trang Phục & Y Phục Bóc Tách',
        zIndex: 25,
        filePrefix: '06c_ong_tay_xoe',
        idealAspectRatio: '3:4',
      };

    case 'vat_ao_duoi':
      return {
        id: 'vat_ao_duoi',
        nameVi: 'Vạt Áo / Tà Áo Dưới Thắt Lưng (Robe Skirt Flap)',
        titleEn: 'MODULAR LOWER ROBE SKIRT FLAP SPRITE',
        assetTag: 'MODULAR_LOWER_ROBE_SKIRT_FLAP_ONLY',
        summaryEn: `Lower robe skirt garment and decorative tassels (${costumeColorVi}).`,
        positiveContent: `A standalone xianxia cultivator lower robe skirt garment piece in color theme (${costumeColorVi}), with decorative border trims and side hanging pendant knot tassels, cut cleanly with a hollow upper waist band opening, designed to drape from the waist down over the thighs and knees, floating alone on green screen (bilaterally symmetrical single garment asset).`,
        excludeShort: 'chest, torso, arms, bare skin, thighs, legs, feet, head',
        includedGeometry: [
          `lower robe skirt drape fabric in (${costumeColorVi})`,
          'front center flap and side tails',
          'hanging pendant knot tassels',
          'hollow upper waist opening',
        ],
        excludedGeometry: [
          'chest', 'torso', 'arms', 'bare skin', 'legs', 'head',
        ],
        rearVisibility: 'visible',
        groupId: '03_props_costumes',
        groupNameVi: 'Trang Phục & Y Phục Bóc Tách',
        zIndex: 22,
        filePrefix: '06b_vat_ao_duoi',
        idealAspectRatio: '3:4',
      };

    case 'ung_giay':
      return {
        id: 'ung_giay',
        nameVi: 'Ủng Giày Tiên Hiệp (Cultivator Boots)',
        titleEn: 'MODULAR CULTIVATOR BOOTS SPRITE',
        assetTag: 'MODULAR_CULTIVATOR_BOOTS_ONLY',
        summaryEn: `Knee-high cultivator boots with border trims (${costumeColorVi}).`,
        positiveContent: `A single isolated pair of xianxia cultivator knee-high boots in color theme (${costumeColorVi}) with decorative border trim and reinforced soles, hollow at the knee top opening, designed to slide over bare anime shins and feet for 2D puppet rigging, floating alone on green screen (bilaterally symmetrical boots asset).`,
        excludeShort: 'bare skin, thighs, torso, arms, head, body',
        includedGeometry: [
          `knee-high boot shafts and soles in (${costumeColorVi})`,
          'decorative border trims',
          'hollow knee top openings',
        ],
        excludedGeometry: [
          'bare skin', 'thighs', 'torso', 'arms', 'head', 'body',
        ],
        rearVisibility: 'visible',
        groupId: '03_props_costumes',
        groupNameVi: 'Trang Phục & Y Phục Bóc Tách',
        zIndex: 24,
        filePrefix: '06d_ung_giay',
        idealAspectRatio: '9:16',
      };

    case 'ong_ao_bap_tay':
      return {
        id: 'ong_ao_bap_tay',
        nameVi: 'Ống Áo Bắp Tay (Upper Arm Sleeve)',
        titleEn: 'MODULAR UPPER ARM BICEP SLEEVE GARMENT SPRITE',
        assetTag: 'MODULAR_UPPER_ARM_SLEEVE_GARMENT_ONLY',
        summaryEn: `Upper bicep sleeve garment segment (${costumeColorVi}).`,
        positiveContent: `A single isolated xianxia cultivator upper bicep arm sleeve segment in color theme (${costumeColorVi}) with decorative trim borders, hollow at the shoulder joint top opening and hollow at the elbow bottom opening, designed to slide over an anime upper arm (Z-index 53), floating alone on green screen (bilaterally symmetrical single sleeve asset).`,
        excludeShort: 'bare skin, forearm, hand, torso, chest, body, head',
        includedGeometry: [
          `upper arm sleeve fabric drape in (${costumeColorVi})`,
          'hollow shoulder top opening',
          'hollow elbow bottom opening',
        ],
        excludedGeometry: [
          'bare skin', 'forearm', 'hand', 'torso', 'head', 'body',
        ],
        rearVisibility: 'visible',
        groupId: '03_props_costumes',
        groupNameVi: 'Trang Phục & Y Phục Bóc Tách',
        zIndex: 53,
        filePrefix: '06e_ong_ao_bap_tay',
        idealAspectRatio: '3:4',
      };

    case 'dai_lung':
      return {
        id: 'dai_lung',
        nameVi: 'Đai Lưng Thắt Eo (Flat Front Waist Sash & Jade Pendant)',
        titleEn: 'MODULAR FLAT FRONT XIANXIA WAIST SASH BELT SPRITE',
        assetTag: 'MODULAR_FLAT_FRONT_WAIST_SASH_BELT_ONLY',
        summaryEn: `Flat front waistband with center buckle and hanging jade pendant knot (${costumeColorVi}).`,
        positiveContent: `A standalone flat front xianxia cultivator waist sash belt (front view only: horizontal cloth sash band in color theme (${costumeColorVi}) across the waist with ornate golden center buckle and hanging circular jade knot tassel pendant), strictly front-facing 2D flat silhouette, ZERO 3D oval loop, zero visible rear ring, zero backside perspective, strictly front flat band designed to overlay across the waist seam between tunic and pants (Z-index 52), floating alone on green screen (bilaterally symmetrical single belt asset).`,
        excludeShort: '3D ring loop, backside of belt, open oval, chest, torso, legs, pants, arms, bare skin, head',
        includedGeometry: [
          `flat front waistband sash fabric in (${costumeColorVi})`,
          'central gold clasp or buckle',
          'hanging circular jade knot tassel pendant',
        ],
        excludedGeometry: [
          '3D ring loop', 'backside of belt', 'open oval', 'chest', 'torso', 'legs', 'pants', 'arms', 'bare skin', 'head',
        ],
        rearVisibility: 'visible',
        groupId: '03_props_costumes',
        groupNameVi: 'Trang Phục & Y Phục Bóc Tách',
        zIndex: 52,
        filePrefix: '06f_dai_lung',
        idealAspectRatio: '1:1',
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
        groupId: '03_props_costumes',
        groupNameVi: 'Trang Phục & Y Phục Bóc Tách',
        zIndex: 2,
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
        groupId: '04_props_weapons',
        groupNameVi: 'Vũ Khí & Pháp Bảo Tiên Hiệp',
        zIndex: 60,
        filePrefix: '06_vu_khi',
        idealAspectRatio: '9:16',
      };
  }
}
