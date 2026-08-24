import { AIPartPromptConfig } from '../../../types/scene2d';
import {
  AIPromptResult,
  getStyleLabel,
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
  getBodyProportionLabels,
} from './PromptLabelHelpers';
import { getComponentDef } from './Asset2DComponentDefs';

export { type Asset2DComponentDef, getComponentDef } from './Asset2DComponentDefs';

/**
 * Builds Step 2 Decomposed Isolated Parts Prompts (1:1 Single Asset, 1x4 Turnaround, 2x3 Multi-Angle Sheet)
 */
export function buildStep2DecomposedPrompt(config: AIPartPromptConfig): AIPromptResult {
  const sheet = config.sheet_type || 'single_isolated_1x1';
  let bgPromptColorEn = 'pure Chroma Green #00FF00';
  let bgPromptColorHex = '#00FF00';
  let bgTextVi = 'Nền xanh lá Chroma Green (#00FF00) phẳng 1 màu dứt khoát';

  if (config.bg_type === 'pure_white') {
    bgPromptColorEn = 'pure White #FFFFFF';
    bgPromptColorHex = '#FFFFFF';
    bgTextVi = 'Nền trắng tinh khiết (#FFFFFF) phẳng 1 màu';
  } else if (config.bg_type === 'chroma_gray') {
    bgPromptColorEn = 'neutral Dark Gray #333333';
    bgPromptColorHex = '#333333';
    bgTextVi = 'Nền xám đậm trung tính (#333333)';
  } else if (config.bg_type === 'pure_black') {
    bgPromptColorEn = 'pure Black #000000';
    bgPromptColorHex = '#000000';
    bgTextVi = 'Nền đen tuyền (#000000)';
  }

  const artStyleEn = config.custom_character_style?.trim() || config.character_style?.trim() || '2D Chinese Guoman / Xianxia Anime Artstyle';
  const styleLabelVi = getStyleLabel(config.character_style, config.custom_character_style);

  const eyeShapeInfo = getEyeShapeLabels(config.eye_shape, config.custom_eye_shape);
  const eyeColInfo = getEyeColorLabels(config.eye_color, config.custom_eye_color);
  const noseInfo = getNoseLabels(config.nose_shape, config.custom_nose_shape);
  const mouthInfo = getMouthLabels(config.mouth_style, config.custom_mouth_style);
  const costumeInfo = getCostumeLabels(config.costume_style, config.custom_costume_style);
  const costumeColorVi = config.costume_color?.trim() || 'Trắng bạc phối tím nhạt viền ngọc bích';
  const propInfo = getPropLabels(config.prop_item, config.custom_prop_item);
  const hairLenInfo = getHairLengthLabels(config.hair_length, config.custom_hair_length);
  const hairColInfo = getHairColorLabels(config.hair_color, config.custom_hair_color);
  const hairTexInfo = getHairTextureLabels(config.hair_texture, config.custom_hair_texture);
  const hairAccInfo = getHairAccessoryLabels(config.hair_accessories, config.custom_hair_accessories);
  const bodyPropInfo = getBodyProportionLabels(config.body_proportion, config.custom_body_proportion);

  const comp = getComponentDef(config.part_type || 'toc_truoc', {
    hairColInfo, hairTexInfo, hairLenInfo, hairAccInfo,
    eyeShapeInfo, eyeColInfo, noseInfo, mouthInfo,
    costumeInfo, costumeColorVi, propInfo,
  });

  const baseRefPrompt = `2D ${artStyleEn} character asset: ${config.gender === 'nam' ? 'Male' : 'Female'}, ${bodyPropInfo.en}, hair (${hairColInfo.en}, ${hairTexInfo.en}), costume (${costumeInfo.en}, color: ${costumeColorVi}), solid ${bgPromptColorEn} background, crisp 2D anime lineart, cel shading, zero shadows`;

  // 1:1 SINGLE ASSET
  if (sheet === 'single_isolated_1x1' || sheet === 'single_part' || config.aspect_ratio === '1:1') {
    const angle = config.view_angle || 'front';
    let angleLabelVi = '0° Chính diện (Front 0°)';
    let angleLabelEn = 'Orthographic Front 0° view';
    let angleDescEn = 'Camera directly facing the front of the isolated component';

    if (angle === 'three_quarter' || angle === '45' || angle === 'three_quarter_45') {
      angleLabelVi = '45° Nghiêng 3/4 (Three-Quarter 45°)';
      angleLabelEn = 'Orthographic Three-Quarter 45° view';
      angleDescEn = 'Camera rotated 45 degrees showing three-quarter depth';
    } else if (angle === 'profile_side' || angle === '90' || angle === 'side_90') {
      angleLabelVi = '90° Nhìn ngang (Side Profile 90°)';
      angleLabelEn = 'Orthographic Side Profile 90° view';
      angleDescEn = 'Camera rotated 90 degrees showing clean side silhouette';
    } else if (angle === 'back' || angle === '180' || angle === 'rear_180') {
      angleLabelVi = '180° Sau lưng (Rear Back 180°)';
      angleLabelEn = 'Orthographic Rear Back 180° view';
      angleDescEn = comp.rearVisibility === 'hidden' ? 'Pure empty space (hidden from behind)' : 'Camera directly facing the rear back of the component';
    } else if (angle === 'high_angle' || angle === 'top_down') {
      angleLabelVi = 'Trên cao nhìn xuống (High Angle)';
      angleLabelEn = 'Cinematic High-Angle top-down view';
      angleDescEn = 'Camera positioned elevated above looking downward at the component';
    } else if (angle === 'low_angle' || angle === 'bottom_up') {
      angleLabelVi = 'Dưới hất lên (Low Angle)';
      angleLabelEn = 'Cinematic Low-Angle bottom-up view';
      angleDescEn = 'Camera positioned below looking upward at the component';
    }

    const allComponentAnglesPrompts = [
      {
        name: `${comp.filePrefix}_000_front`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: '0° Front (Chính diện)',
        angle_id: '000_front',
        angle_deg: 0,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_000_front.png`,
        view_desc: 'Góc chính diện 0° lơ lửng độc lập',
        prompt: `0° front view of isolated ${comp.titleEn}. Include ONLY: ${comp.includedGeometry.join(', ')}. Zero face, zero body, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 4,
      },
      {
        name: `${comp.filePrefix}_045_three_quarter`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: '45° Three-Quarter (Nghiêng 3/4)',
        angle_id: '045_three_quarter',
        angle_deg: 45,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_045_three_quarter.png`,
        view_desc: 'Góc nghiêng 3/4 45 độ',
        prompt: `45° three-quarter view of isolated ${comp.titleEn}, depth and curve silhouette, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 2,
      },
      {
        name: `${comp.filePrefix}_090_side`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: '90° Side Profile (Nhìn ngang)',
        angle_id: '090_side',
        angle_deg: 90,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_090_side.png`,
        view_desc: 'Góc nhìn ngang 90 độ',
        prompt: `90° side profile view of isolated ${comp.titleEn}, clean side silhouette, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 2,
      },
      {
        name: `${comp.filePrefix}_high_angle`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: 'High Angle (Trên cao nhìn xuống)',
        angle_id: 'high_angle_top',
        angle_deg: 90,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_high_angle_top.png`,
        view_desc: 'Góc quay trên cao nhìn chúc xuống',
        prompt: `High-angle top-down perspective view of isolated ${comp.titleEn}, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 1,
      },
      {
        name: `${comp.filePrefix}_low_angle`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: 'Low Angle (Dưới hất lên)',
        angle_id: 'low_angle_bottom',
        angle_deg: 90,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_low_angle_bottom.png`,
        view_desc: 'Góc quay dưới hất ngược lên',
        prompt: `Low-angle bottom-up perspective view of isolated ${comp.titleEn}, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 1,
      },
      {
        name: `${comp.filePrefix}_180_back`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: '180° Back (Sau lưng)',
        angle_id: '180_back',
        angle_deg: 180,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_180_back.png`,
        view_desc: comp.rearVisibility === 'hidden' ? 'Bị khuất từ sau lưng (ô rỗng)' : 'Góc nhìn từ sau lưng',
        prompt: comp.rearVisibility === 'hidden'
          ? `pure empty solid ${bgPromptColorEn} background #00FF00, blank empty canvas --ar 1:1`
          : `180° rear back view of isolated ${comp.titleEn}, rear back texture, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 1,
      },
    ];

    const step2Prompts = config.json_scope === 'single_angle'
      ? [
          {
            name: `${comp.filePrefix}_${angle}`,
            part_id: comp.id,
            part_name: comp.nameVi,
            group_id: comp.groupId,
            group_name: comp.groupNameVi,
            angle: angleLabelVi,
            angle_id: angle,
            angle_deg: angle === 'three_quarter' ? 45 : angle === 'profile_side' ? 90 : angle === 'back' ? 180 : 0,
            z_index: comp.zIndex,
            save_filename: `${comp.filePrefix}_${angle}.png`,
            view_desc: angleDescEn,
            prompt: `masterpiece, 4k, 1:1 square, isolated ${comp.titleEn}, angle: ${angleLabelEn}. Include ONLY: ${comp.includedGeometry.join(', ')}. DO NOT include: ${comp.excludedGeometry.slice(0, 8).join(', ')}. Solid uniform ${bgPromptColorEn} background, clean lineart, flat cel shading, zero shadows, no text, no borders --ar 1:1`,
            count: 4,
          },
        ]
      : allComponentAnglesPrompts;

    const promptJSON = JSON.stringify(
      config.include_base_prompt === false
        ? { prompts: step2Prompts }
        : { base_prompt: baseRefPrompt, prompts: step2Prompts },
      null,
      2
    );

    const promptEnglish = `masterpiece, best quality, ultra detailed, 4k resolution, 1:1 square aspect ratio, isolated ${comp.titleEn}, angle: ${angleLabelEn}. Include ONLY: ${comp.includedGeometry.join(', ')}. DO NOT include: ${comp.excludedGeometry.slice(0, 10).join(', ')}. Solid uniform ${bgPromptColorEn} background, zero shadows, no text --ar 1:1`;
    const promptVietnamese = `【 ẢNH ĐƠN 1:1 SIÊU NÉT — ${comp.nameVi} (${angleLabelVi}) 】\n• Tiêu đề: ${comp.titleEn}\n• Nền: ${bgTextVi}.`;
    const negativePrompt = 'full character, full body, head, face, extra limbs, multiple views, turnaround, comic panels, grid lines, borders, frames, divider lines, text, letters, numbers, watermark, signature, blurry, 3D CGI render, glow, rim light, color spill';
    const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;

    return {
      promptEnglish,
      promptVietnamese,
      promptJSON,
      promptGemini: promptVietnamese,
      gridStructureGuide: '📐 Khung 1:1 vuông: 1 Ảnh đơn siêu nét.',
      negativePrompt,
      fullCopyText,
    };
  }

  // 1x4 HORIZONTAL TURNAROUND
  if (sheet === 'seamless_turnaround_1x4' || sheet === 'modular_bangs_3x1' || sheet === 'modular_backhair_3x1' || sheet === 'modular_torso_armor_3x1') {
    const horizontal1x4Prompts = [
      {
        name: `${comp.filePrefix}_000_front`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: '0° Front (Chính diện)',
        angle_id: '000_front',
        angle_deg: 0,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_000_front.png`,
        view_desc: 'Góc chính diện 0° lơ lửng độc lập',
        prompt: `0° front view of isolated ${comp.titleEn}. Include ONLY: ${comp.includedGeometry.join(', ')}. Zero face, zero body, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 4,
      },
      {
        name: `${comp.filePrefix}_045_three_quarter`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: '45° Three-Quarter (Nghiêng 3/4)',
        angle_id: '045_three_quarter',
        angle_deg: 45,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_045_three_quarter.png`,
        view_desc: 'Góc nghiêng 3/4 45 độ',
        prompt: `45° three-quarter view of isolated ${comp.titleEn}, depth and curve silhouette, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 2,
      },
      {
        name: `${comp.filePrefix}_090_side`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: '90° Side Profile (Nhìn ngang)',
        angle_id: '090_side',
        angle_deg: 90,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_090_side.png`,
        view_desc: 'Góc nhìn ngang 90 độ',
        prompt: `90° side profile view of isolated ${comp.titleEn}, clean side silhouette, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 2,
      },
      {
        name: `${comp.filePrefix}_180_back`,
        part_id: comp.id,
        part_name: comp.nameVi,
        group_id: comp.groupId,
        group_name: comp.groupNameVi,
        angle: '180° Back (Sau lưng)',
        angle_id: '180_back',
        angle_deg: 180,
        z_index: comp.zIndex,
        save_filename: `${comp.filePrefix}_180_back.png`,
        view_desc: comp.rearVisibility === 'hidden' ? 'Bị khuất từ sau lưng (ô rỗng)' : 'Góc nhìn từ sau lưng',
        prompt: comp.rearVisibility === 'hidden'
          ? `pure empty solid ${bgPromptColorEn} background #00FF00, blank empty canvas --ar 1:1`
          : `180° rear back view of isolated ${comp.titleEn}, rear back texture, solid ${bgPromptColorEn} background --ar 1:1`,
        count: 1,
      },
    ];

    const promptJSON = JSON.stringify(
      config.include_base_prompt === false
        ? { prompts: horizontal1x4Prompts }
        : { base_prompt: baseRefPrompt, prompts: horizontal1x4Prompts },
      null,
      2
    );

    const promptEnglish = `masterpiece, 4k resolution, 16:9, seamless 4-view horizontal rotation sequence of isolated ${comp.titleEn}: Front 0°, Three-Quarter 45°, Side Profile 90°, Rear Back 180°. Solid flat ${bgPromptColorEn} background, zero shadows, no text --ar 16:9`;
    const promptVietnamese = `【 CHUỖI XOAY NGANG 4 GÓC LIỀN MẠCH — ${comp.nameVi} (16:9) 】\n• Bố cục: 4 góc dàn ngang trên 1 hàng (0° ➔ 45° ➔ 90° ➔ 180°)\n• Nền: ${bgTextVi}.`;
    const negativePrompt = 'grid lines, divider lines, panel borders, box frames, comic panels, full character, full body, extra limbs, text, labels, watermark, blurry, 3D CGI render, glow';
    const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;

    return {
      promptEnglish,
      promptVietnamese,
      promptJSON,
      promptGemini: promptVietnamese,
      gridStructureGuide: '📐 Khung 16:9 1 Hàng: 4 góc dàn ngang tự nhiên.',
      negativePrompt,
      fullCopyText,
    };
  }

  // 2x3 MULTI-ANGLE SHEET
  const multiAngle2x3Prompts = [
    {
      name: `${comp.filePrefix}_000_front`,
      part_id: comp.id,
      part_name: comp.nameVi,
      group_id: comp.groupId,
      group_name: comp.groupNameVi,
      angle: '0° Front (Chính diện)',
      angle_id: '000_front',
      angle_deg: 0,
      z_index: comp.zIndex,
      save_filename: `${comp.filePrefix}_000_front.png`,
      view_desc: 'Hàng trên Ô 1: Góc chính diện 0° lơ lửng độc lập',
      prompt: `0° front view of isolated ${comp.titleEn}. Include ONLY: ${comp.includedGeometry.join(', ')}. Zero face, zero body, solid ${bgPromptColorEn} background --ar 1:1`,
      count: 4,
    },
    {
      name: `${comp.filePrefix}_045_three_quarter`,
      part_id: comp.id,
      part_name: comp.nameVi,
      group_id: comp.groupId,
      group_name: comp.groupNameVi,
      angle: '45° Three-Quarter (Nghiêng 3/4)',
      angle_id: '045_three_quarter',
      angle_deg: 45,
      z_index: comp.zIndex,
      save_filename: `${comp.filePrefix}_045_three_quarter.png`,
      view_desc: 'Hàng trên Ô 2: Góc nghiêng 3/4 45 độ',
      prompt: `45° three-quarter view of isolated ${comp.titleEn}, depth and curve silhouette, solid ${bgPromptColorEn} background --ar 1:1`,
      count: 2,
    },
    {
      name: `${comp.filePrefix}_090_side`,
      part_id: comp.id,
      part_name: comp.nameVi,
      group_id: comp.groupId,
      group_name: comp.groupNameVi,
      angle: '90° Side Profile (Nhìn ngang)',
      angle_id: '090_side',
      angle_deg: 90,
      z_index: comp.zIndex,
      save_filename: `${comp.filePrefix}_090_side.png`,
      view_desc: 'Hàng trên Ô 3: Góc nhìn ngang 90 độ',
      prompt: `90° side profile view of isolated ${comp.titleEn}, side silhouette, solid ${bgPromptColorEn} background --ar 1:1`,
      count: 2,
    },
    {
      name: `${comp.filePrefix}_high_angle`,
      part_id: comp.id,
      part_name: comp.nameVi,
      group_id: comp.groupId,
      group_name: comp.groupNameVi,
      angle: 'High Angle (Trên cao nhìn xuống)',
      angle_id: 'high_angle_top',
      angle_deg: 90,
      z_index: comp.zIndex,
      save_filename: `${comp.filePrefix}_high_angle_top.png`,
      view_desc: 'Hàng dưới Ô 4: Góc quay trên cao nhìn chúc xuống',
      prompt: `High-angle top-down perspective view of isolated ${comp.titleEn}, solid ${bgPromptColorEn} background --ar 1:1`,
      count: 1,
    },
    {
      name: `${comp.filePrefix}_low_angle`,
      part_id: comp.id,
      part_name: comp.nameVi,
      group_id: comp.groupId,
      group_name: comp.groupNameVi,
      angle: 'Low Angle (Dưới hất lên)',
      angle_id: 'low_angle_bottom',
      angle_deg: 90,
      z_index: comp.zIndex,
      save_filename: `${comp.filePrefix}_low_angle_bottom.png`,
      view_desc: 'Hàng dưới Ô 5: Góc quay dưới hất ngược lên',
      prompt: `Low-angle bottom-up perspective view of isolated ${comp.titleEn}, solid ${bgPromptColorEn} background --ar 1:1`,
      count: 1,
    },
    {
      name: `${comp.filePrefix}_180_back`,
      part_id: comp.id,
      part_name: comp.nameVi,
      group_id: comp.groupId,
      group_name: comp.groupNameVi,
      angle: '180° Rear Back (Sau lưng)',
      angle_id: '180_back',
      angle_deg: 180,
      z_index: comp.zIndex,
      save_filename: `${comp.filePrefix}_180_back.png`,
      view_desc: comp.rearVisibility === 'hidden' ? 'Hàng dưới Ô 6: Bị khuất hoàn toàn (ô rỗng)' : 'Hàng dưới Ô 6: Mặt sau chi tiết',
      prompt: comp.rearVisibility === 'hidden'
        ? `pure empty solid ${bgPromptColorEn} background #00FF00, blank empty canvas --ar 1:1`
        : `180° rear back view of isolated ${comp.titleEn}, rear back texture, solid ${bgPromptColorEn} background --ar 1:1`,
      count: 1,
    },
  ];

  const promptJSON = JSON.stringify(
    config.include_base_prompt === false
      ? { prompts: multiAngle2x3Prompts }
      : { base_prompt: baseRefPrompt, prompts: multiAngle2x3Prompts },
    null,
    2
  );

  const promptEnglish = `masterpiece, 4k resolution, 16:9, modular 2D anime sprite sheet of isolated ${comp.titleEn} (6 views arranged in 2 rows of 3). Top: Front 0°, Three-Quarter 45°, Side Profile 90°. Bottom: High Angle, Low Angle, Rear Back 180°. Solid flat ${bgPromptColorEn} background, zero shadows, no text --ar 16:9`;
  const promptVietnamese = `【 BẢNG SPRITE 6 GÓC QUAY ĐIỆN ẢNH CHO 1 CHI TIẾT (LƯỚI 2×3 — 16:9) 】\n• Linh kiện: ${comp.nameVi}\n• Hàng trên: 1. Chính diện 0° | 2. Nghiêng 3/4 45° | 3. Nhìn ngang 90°\n• Hàng dưới: 4. Trên cao nhìn xuống | 5. Dưới hất lên | 6. Sau lưng 180°\n• Nền: ${bgTextVi}.`;
  const negativePrompt = 'grid lines, divider lines, cell borders, panel frames, black outlines around cells, comic panels, full character, full body, head, face, extra limbs, text, labels, watermark, blurry, 3D CGI render, glow, rim light';
  const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;

  return {
    promptEnglish,
    promptVietnamese,
    promptJSON,
    promptGemini: promptVietnamese,
    gridStructureGuide: '📐 Khung Cắt 16:9: Lưới 2 Hàng × 3 Cột điện ảnh.',
    negativePrompt,
    fullCopyText,
  };
}
