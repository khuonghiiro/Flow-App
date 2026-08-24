import { AIPartPromptConfig } from '../../../types/scene2d';
import {
  AIPromptResult,
  getSheetTypeLabel,
  getStyleLabel,
  getGenderLabel,
  getEyeShapeLabels,
  getEyeColorLabels,
  getNoseLabels,
  getMouthLabels,
  getCostumeLabels,
  getPropLabels,
  getHairLengthLabels,
  getHairColorLabels,
  getHairTextureLabels,
  getHairAccessoryLabels,
} from './PromptLabelHelpers';

/**
 * Builds multi-cell grid sheet prompts for full body and facial feature sheets
 */
export function buildGridSheetsPrompt(config: AIPartPromptConfig, sheet: string): AIPromptResult {
  let bgTextEn = 'isolated on solid pure flat white background #FFFFFF, clean flat cutout, zero drop shadows, no ambient occlusion, strictly neutral lighting';
  let bgTextVi = 'Nền trắng tinh khiết (#FFFFFF) phẳng 1 màu, viền tương phản cao dễ cắt';
  let bgPromptColorEn = 'pure Chroma Green #00FF00';

  if (config.bg_type === 'chroma_green' || !config.bg_type) {
    bgTextEn = 'isolated on solid flat pure chroma green background #00FF00, uniform flat single color, high contrast edge, strictly flat neutral unlit shading, absolutely zero ambient color spill, zero green fringe on edges, no bounce light, no rim lighting, no global illumination, pure matte colors';
    bgTextVi = 'Nền xanh lá Chroma Green (#00FF00) phẳng 1 màu dứt khoát để cắt phông tức thì';
    bgPromptColorEn = 'pure Chroma Green #00FF00';
  } else if (config.bg_type === 'pure_white') {
    bgTextEn = 'isolated on solid pure flat white background #FFFFFF, clean flat cutout, zero drop shadows, no ambient occlusion, strictly neutral lighting';
    bgTextVi = 'Nền trắng tinh khiết (#FFFFFF) phẳng 1 màu';
    bgPromptColorEn = 'pure White #FFFFFF';
  } else if (config.bg_type === 'chroma_gray') {
    bgTextEn = 'isolated on solid flat neutral dark gray background #333333, uniform flat single color, high contrast edge, zero shadows, no color spill, perfect for white hair extraction';
    bgTextVi = 'Nền xám đậm trung tính (#333333) không bóng đổ, chuẩn bóc tách tóc trắng/bạc';
    bgPromptColorEn = 'neutral Dark Gray #333333';
  } else if (config.bg_type === 'pure_black') {
    bgTextEn = 'isolated on solid flat pure black background #000000, uniform flat single color, high contrast edge, zero shadows';
    bgTextVi = 'Nền đen tuyền (#000000) không bóng đổ, dùng cho chi tiết phát sáng';
    bgPromptColorEn = 'pure Black #000000';
  }

  const noTextEn = 'clean graphic asset only, strictly NO text, NO letters, NO words, NO numbers, NO watermark, NO labels, NO typography, NO captions, NO annotations, NO border writing';
  const styleTextEn = config.character_style === 'custom' && config.custom_character_style?.trim()
    ? config.custom_character_style.trim()
    : config.character_style === 'chibi'
      ? 'cute 2D anime chibi character artstyle, adorable 2-head to 3-head kawaii proportions, giant sparkling expressive anime eyes, chubby cheeks, soft cute face, clean bold outlines, vibrant flat cel shading, authentic Japanese chibi anime illustration'
      : 'masterpiece 2D Japanese anime character artstyle, Kyoto Animation and Ufotable aesthetic, large gorgeous sparkling expressive anime eyes, detailed pupil reflections with multiple light sparkles, clean thick anime lash lines, aesthetic beautiful anime facial proportions, delicate cute anime nose and mouth, sharp crisp anime lineart, flat vibrant cel shading, high quality digital anime illustration';

  const styleLabelVi = getStyleLabel(config.character_style, config.custom_character_style);

  const eyeColInfo = getEyeColorLabels(config.eye_color, config.custom_eye_color);
  const hairLenInfo = getHairLengthLabels(config.hair_length, config.custom_hair_length);
  const hairColInfo = getHairColorLabels(config.hair_color, config.custom_hair_color);
  const hairTexInfo = getHairTextureLabels(config.hair_texture, config.custom_hair_texture);
  const hairAccInfo = getHairAccessoryLabels(config.hair_accessories, config.custom_hair_accessories);

  let promptEnglish = '';
  let promptVietnamese = '';
  let promptJSON = '';
  let gridStructureGuide = '';

  if (sheet === 'hair_multi_angle_grid') {
    const ar = config.aspect_ratio || '16:9';
    promptEnglish = [
      `masterpiece, best quality, ultra detailed 2D anime character hair turnaround model sheet`,
      `consistent modular hair layers sprite sheet organized in a clean 3-row by 5-column grid on ${bgPromptColorEn}`,
      `strictly headless pure hair component assets only, transparent invisible mannequin, no human face, no skin, no body, no human ears`,
      `Column 1: 0° Front View, Column 2: 45° Three-Quarter View, Column 3: 90° Side Profile View, Column 4: 135° Back Three-Quarter View, Column 5: 180° Rear Back View`,
      `[ROW 1 - FRONT BANGS FRINGE]: pure front fringe bangs floating alone across 5 angles, strictly no back hair`,
      `[ROW 2 - SIDEBURNS & CHEEK LOCKS]: floating sideburn cheek locks hugging the face contours across 5 angles, strictly no ears, no skin`,
      `[ROW 3 - BACK HAIR MANTLE & VOLUME]: complete back hair flowing down across 5 angles, pure rear hair layer with empty hollow face center`,
      `${hairColInfo.en} hair, ${hairLenInfo.en}, ${hairTexInfo.en}${hairAccInfo.en !== 'none' ? `, accessory: ${hairAccInfo.en}` : ''}`,
      styleTextEn,
      bgTextEn,
      `extremely crisp high-contrast edge separation, hard edge sticker cutout, unlit flat shading, absolutely ZERO background color bleeding, ZERO ambient green color spill onto hair strands, ZERO glowing halo around hair contours, 100% opaque solid matte color borders, modular puppet assembly ready`,
      `--ar ${ar}`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE LINH KIỆN TÓC 3 DÃY × 5 GÓC QUAY CHUẨN ĐIỆN ẢNH (TỶ LỆ ${ar}) 】\n• Tóc: ${hairColInfo.vi}, ${hairLenInfo.vi}, ${hairTexInfo.vi}.\n• Nền: ${bgTextVi}.`;
    gridStructureGuide = `📐 Khung Cắt ${ar}: Lưới 3 Hàng × 5 Cột chuẩn 15 ô linh kiện.`;

    const jsonSpec = {
      project: 'Flow-App 2D Motion Comic Engine',
      workflow_step: 'Step 2 - Cinematographic Modular Hair Turnaround Grid Decomposition',
      title: 'Bảng Bóc Tách Tóc Đa Tầng Theo Độ Sâu Z-Index & 5 Góc Quay Chuẩn Cinematic (3 Rows x 5 Columns)',
      art_style: styleLabelVi,
      resolution: '4K Ultra High Definition (3840x2160)',
      aspect_ratio: `${ar} Aspect Ratio (3 Rows x 5 Columns)`,
      background: bgTextVi,
      hair_specifications: {
        color: `${hairColInfo.vi} (${hairColInfo.en})`,
        length: `${hairLenInfo.vi} (${hairLenInfo.en})`,
        texture: `${hairTexInfo.vi} (${hairTexInfo.en})`,
        accessories: `${hairAccInfo.vi} (${hairAccInfo.en})`,
      },
      cinematographic_layer_breakdown: {
        total_rows: 3,
        total_columns: 5,
        camera_angles: [
          'Column 1: 0° Front View (Chính diện)',
          'Column 2: 45° Three-Quarter View (Nghiêng 3/4)',
          'Column 3: 90° Full Side Profile View (Nhìn ngang vành tai 90 độ)',
          'Column 4: 135° Back Three-Quarter View (Nghiêng sau 135 độ)',
          'Column 5: 180° Full Rear Back View (Sau lưng toàn cảnh 180 độ)',
        ],
        rows_definition: [
          {
            row: 'Row 1 (Z-Index Cao Nhất - Trên Cùng): Tóc Mái Trước Trán (Front Bangs Fringe)',
            description: '5 floating front fringe bangs pieces across 5 camera angles. Pure bangs only, strictly no back hair.',
          },
          {
            row: 'Row 2 (Z-Index Trung Gian - Ôm Mặt): Lọn Tóc Mai 2 Bên Má (Sideburns & Cheek Locks)',
            description: '5 floating sideburn cheek locks hugging the jawline and face contour across 5 angles. Strictly no ears, no skin.',
          },
          {
            row: 'Row 3 (Z-Index Thấp Nhất - Dưới Cùng): Suối Tóc Sau Đầu (Back Hair Mantle & Flowing Volume)',
            description: '5 complete back hair streams cascading down across 5 angles. Hollow face center to assemble behind head base.',
          },
        ],
      },
    };
    promptJSON = JSON.stringify(jsonSpec, null, 2);
  } else if (sheet === 'eyes_grid') {
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio sprite sheet layout`,
      `strictly NO face skin, NO head outline, NO nose, NO mouth, isolated floating eyes and eyebrows component assets only`,
      `sharp detailed pupil highlights, clean cel shading`,
      `organized in a clean 4-row by 5-column grid with visible spacing:`,
      `[ROW 1 - OPEN EYES ACROSS CAMERA ANGLES]: Col 1: 0° front symmetrical eyes; Col 2: 45° three-quarter left eyes; Col 3: 90° side profile single eye; Col 4: 135° back glance eye; Col 5: extreme low-angle eyes`,
      `[ROW 2 - BLINKING CLOSED EYELIDS]: Col 1: 0° closed blinking eyelid; Col 2: 45° closed eyelid; Col 3: 90° closed profile eyelid; Col 4: gentle smiling closed eyes; Col 5: tightly shut pain eyelid`,
      `[ROW 3 - EMOTION EYE STATES]: Col 1: fierce combat glowing sword intent iris; Col 2: shocked widened pupil; Col 3: smiling happy eyes; Col 4: cold calculating gaze; Col 5: spiritual awakening glowing aura eyes`,
      `[ROW 4 - EYEBROWS ONLY]: Col 1: neutral swordsman eyebrows; Col 2: furrowed angry eyebrows; Col 3: raised curious eyebrow; Col 4: 90° profile eyebrow; Col 5: battle frown eyebrows`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `--ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE ĐÔI MẮT & CHỚP MẮT (KHÔNG CÓ DA MẶT) (4K - 16:9) 】\n• Nền: ${bgTextVi}.`;
    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 5 Cột chuẩn 4K.`;
  } else if (sheet === 'mouth_grid') {
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio sprite sheet layout`,
      `strictly NO chin, NO nose, NO face skin, isolated floating lips and mouth expression sprite sheet only`,
      `organized in a clean 4-row by 5-column grid with visible spacing:`,
      `[ROW 1 - FRONT 0° LIP-SYNC TALK CYCLE]: Col 1: neutral closed mouth 'M'; Col 2: open mouth 'A'; Col 3: round mouth 'O/U'; Col 4: wide mouth 'I/E'; Col 5: gentle confident smile`,
      `[ROW 2 - ANGLED 45° & 90° PROFILE MOUTH]: Col 1: 45° speaking open; Col 2: 45° closed smirk; Col 3: 90° profile side mouth neutral; Col 4: 90° profile speaking open; Col 5: 90° profile shouting mouth`,
      `[ROW 3 - BATTLE & EMOTION MOUTH STATES]: Col 1: roaring combat battle shout with visible teeth; Col 2: fierce grit teeth in pain; Col 3: broad radiant laughter; Col 4: sarcastic cold smirk; Col 5: gasping shock mouth`,
      `[ROW 4 - COMBAT DETAIL & BLOOD TRACE]: Col 1: subtle blood trickle on lip corner; Col 2: holding jade talisman in mouth; Col 3: heavy panting mouth; Col 4: biting lower lip; Col 5: clenched teeth`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `--ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE KHẨU HÌNH MIỆNG (KHÔNG CÓ DA MẶT) (4K - 16:9) 】\n• Nền: ${bgTextVi}.`;
    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 5 Cột chuẩn 4K.`;
  } else if (sheet === 'nose_chin_grid') {
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio sprite sheet layout`,
      `isolated 2D anime character nose, chin jawline contour, and ear anatomy sprite sheet`,
      `organized in 4 clean rows across camera angles:`,
      `[ROW 1 - NOSE BRIDGES ONLY]: 0°, 45°, 90°, 135°, low-angle`,
      `[ROW 2 - CHIN & JAWLINE CONTOURS (NO HAIR)]: 0°, 45°, 90°, 135°, 180°`,
      `[ROW 3 - EARS ONLY (NO HAIR)]: 0°, 45°, 90°, 135°, 180°`,
      `[ROW 4 - FOREHEAD MARKS & FACIAL DETAILS]: celestial mark, demonic mark, scar, third-eye`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `--ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE SỐNG MŨI, CẰM NHỌN 90°, TAI & THẦN ẤN (4K - 16:9) 】\n• Nền: ${bgTextVi}.`;
    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 5 Cột chuẩn 4K.`;
  } else if (sheet === 'costume_grid') {
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio sprite sheet layout`,
      `hollow clothes only, empty traditional xianxia daoist robes, strictly NO human body inside, no head, no legs`,
      `color theme: ${config.color_theme || 'celestial azure blue with gold trim and white silk inner layer'}`,
      `organized in 4 rows across 4 camera angles (0° Front, 45° 3/4 View, 90° Side Profile, 180° Back View)`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `--ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE TRANG PHỤC ĐẠO BÀO RỖNG RUỘT (4K - 16:9) 】\n• Nền: ${bgTextVi}.`;
    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 4 Cột chuẩn 4K.`;
  } else if (sheet === 'weapons_grid') {
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio sprite sheet layout`,
      `isolated 2D xianxia weapon and prop sprite sheet, celestial glowing flying sword`,
      `organized in 4 clean rows with visible spacing`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `--ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE VŨ KHÍ & PHÁP BẢO (4K - 16:9) 】\n• Nền: ${bgTextVi}.`;
    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 5 Cột chuẩn 4K.`;
  } else if (sheet === 'limbs_hands_grid') {
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio sprite sheet layout`,
      `isolated 2D anime character limbs, hand seals and leg boots sprite sheet`,
      `organized in 4 clean rows on uniform background`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `--ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG SPRITE TỨ CHI & BÀN TAY BẮT QUYẾT (4K - 16:9) 】\n• Nền: ${bgTextVi}.`;
    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Dãy x 5 Cột chuẩn 4K.`;
  } else if (sheet === 'body_turnaround_grid') {
    promptEnglish = [
      `masterpiece, best quality, ultra detailed 4k Chinese 2D Donghua modular puppet component sprite sheet`,
      `Xianxia cultivation manhua character anatomy parts decomposition on ${bgPromptColorEn}`,
      `organized in a 4-row by 5-column grid layout with clean spacing between parts:`,
      `[ROW 1 - HEAD & HAIR LAYERS]: headless head base, floating front bangs fringe, sideburn locks, flowing back hair mantle, jade hairpin top bun`,
      `[ROW 2 - FACIAL FEATURES & EXPRESSIONS]: isolated expressive phoenix eyes glowing azure, closed blinking eyelid, open speaking mouth, sharp nose bridge, neutral ear`,
      `[ROW 3 - DAOIST ROBES & COSTUME]: hollow daoist upper chest robe with gold trim, flowing lower skirt hem, inner pleated skirt, golden waist sash belt with dangling jade pendant, jade tassel sash`,
      `[ROW 4 - LIMBS, HAND SEALS & FLYING WEAPONS]: detached wide sleeves, arms, two-finger daoist sword seal gesture hand, martial arts boots, glowing azure flying spirit sword`,
      `strictly flat clean cutout sticker assets, crisp high-contrast outlines, unlit flat shading, zero green ambient color spill, zero glow on edges, 100% opaque solid colors, --ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 BẢNG BÓC TÁCH TOÀN BỘ LINH KIỆN CƠ THỂ & ĐẠO BÀO (20 LINH KIỆN) 】\n• Bố cục: 4 Hàng × 5 Cột.\n• Nền: ${bgTextVi}.`;
    gridStructureGuide = `📐 Khung Cắt 16:9: Lưới 4 Hàng × 5 Cột chuẩn 20 ô linh kiện cơ thể.`;
  } else {
    promptEnglish = [
      `masterpiece, ultra high quality, 4k resolution, 16:9 aspect ratio`,
      `isolated 2D puppet component for ${config.part_type}`,
      `color theme: ${config.color_theme || 'vibrant'}`,
      noTextEn,
      styleTextEn,
      bgTextEn,
      `--ar 16:9`,
    ].join(', ');

    promptVietnamese = `【 LINH KIỆN ĐƠN LẺ (${config.part_type}) (4K - 16:9) 】\n• Nền: ${bgTextVi}.`;
    gridStructureGuide = `📐 Khung Cắt Đơn: Kích thước 4K chuẩn 16:9.`;
  }

  const negativePrompt = 'text, letters, words, writing, captions, labels, watermark, signature, numbers, alphabet, font, row names, column numbers, annotations, border text, human face, eyes, nose, mannequin head, complex background, gradient background, drop shadow, anti-aliased green halo, 3D photorealistic render, low quality, noise, messy borders';

  if (!promptJSON) {
    promptJSON = JSON.stringify(
      {
        project: 'Flow-App 2D Motion Comic Engine',
        sheet_type: sheet,
        style: config.character_style,
        gender: config.gender,
        prompt: promptEnglish,
        negative_prompt: negativePrompt,
      },
      null,
      2
    );
  }

  const promptGemini = `Hãy vẽ ra một bức ảnh linh kiện 2D theo yêu cầu: ${getSheetTypeLabel(sheet)}. Phong cách: ${styleLabelVi}. Nền: ${bgTextVi}. ${gridStructureGuide}`;
  const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;

  return {
    promptEnglish,
    promptVietnamese,
    promptJSON,
    promptGemini,
    gridStructureGuide,
    negativePrompt,
    fullCopyText,
  };
}
