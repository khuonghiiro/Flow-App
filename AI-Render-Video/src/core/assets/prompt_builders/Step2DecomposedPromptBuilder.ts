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
import { getComponentDef, Asset2DComponentDef } from './Asset2DComponentDefs';
import { JSON_SCHEMA_FIELD_GUIDE } from './Step1MasterPromptBuilder';

export { type Asset2DComponentDef, getComponentDef } from './Asset2DComponentDefs';

function clampPromptLength(prompt: string, maxLen = 3900): string {
  if (prompt.length <= maxLen) return prompt;
  return prompt.slice(0, maxLen - 3) + '...';
}

/**
 * Builds Step 2 Decomposed Isolated Parts Prompts (1:1 Single Asset, 1x4 Turnaround, 2x3 Multi-Angle Sheet)
 */
export function buildStep2DecomposedPrompt(config: AIPartPromptConfig): AIPromptResult {
  const sheet = config.sheet_type || 'single_isolated_1x1';
  let bgPromptColorEn = 'pure solid Chroma Green #00FF00';
  let bgPromptColorHex = '#00FF00';
  let bgTextVi = 'Nền xanh lá Chroma Green (#00FF00) phẳng 1 màu dứt khoát';

  if (config.bg_type === 'pure_white') {
    bgPromptColorEn = 'pure solid White #FFFFFF';
    bgPromptColorHex = '#FFFFFF';
    bgTextVi = 'Nền trắng tinh khiết (#FFFFFF) phẳng 1 màu';
  } else if (config.bg_type === 'chroma_gray') {
    bgPromptColorEn = 'neutral solid Dark Gray #333333';
    bgPromptColorHex = '#333333';
    bgTextVi = 'Nền xám đậm trung tính (#333333)';
  } else if (config.bg_type === 'pure_black') {
    bgPromptColorEn = 'pure solid Black #000000';
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

  // Tự động chọn tỉ lệ khung hình lý tưởng theo từng linh kiện hoặc theo lựa chọn người dùng
  let selectedAspectRatio: '1:1' | '3:4' | '4:3' | '16:9' | '9:16' | string = comp.idealAspectRatio || '1:1';
  if (config.aspect_ratio && config.aspect_ratio !== 'auto') {
    selectedAspectRatio = config.aspect_ratio;
  } else {
    if (sheet === 'single_isolated_1x1' || sheet === 'single_part') {
      selectedAspectRatio = comp.idealAspectRatio || '1:1';
    } else {
      selectedAspectRatio = '16:9';
    }
  }

  const userBatchCount = typeof config.batch_count === 'number' && config.batch_count > 0 ? config.batch_count : 1;
  const isMale = config.gender === 'nam';
  const genderLabelEn = isMale ? 'Male' : 'Female';

  const baseRefPrompt = `masterpiece, best quality, ultra detailed, 2D ${artStyleEn} character turnaround sheet, ONE SINGLE IDENTICAL ${genderLabelEn.toUpperCase()} CHARACTER.

CHARACTER SPECIFICATIONS:
- Gender: ${genderLabelEn}
- Art Style: ${artStyleEn}
- Proportion: ${bodyPropInfo.en}
- Eyes: ${eyeShapeInfo.en}, ${eyeColInfo.en}
- Nose & Mouth: ${noseInfo.en}, ${mouthInfo.en}
- Hairstyle: ${hairTexInfo.en}, ${hairColInfo.en}, ${hairLenInfo.en}${hairAccInfo.en !== 'none' ? `, ${hairAccInfo.en}` : ''}
- Costume: ${costumeInfo.en} (Color: ${costumeColorVi})
- Weapon / Props: ${propInfo.en}

TURNAROUND 5-VIEW SEQUENCE (16:9 Canvas):
1. VIEW 1 — FRONT (0°): Direct frontal orthographic view, full body from head to feet.
2. VIEW 2 — THREE-QUARTER (45°): 45-degree angle showing face depth, cheek, and shoulder curve.
3. VIEW 3 — SIDE PROFILE (90°): 90-degree clean lateral side profile showing nose bridge and spine silhouette.
4. VIEW 4 — REAR THREE-QUARTER (135°): 135-degree rear angle showing back waist sash and rear hair flow.
5. VIEW 5 — BACK (180°): Full rear back view showing back hair mantle, shoulder blade lines, and robe spine.
+ TOP-DOWN REFERENCE: Top-down view looking downward at the head crown and hair parting.

CONSISTENCY & RESTRICTIONS:
- 100% identical character rotated strictly around vertical axis.
- Clean 2D anime vector-like lineart, flat 2-tone cel shading, zero drop shadows, zero ambient occlusion on ground.
- Flat uniform ${bgPromptColorEn} (${bgPromptColorHex}) background with zero gradients or cast shadows.
- STRICTLY NO text, NO letters, NO numbers, NO labels, NO watermark, NO grid borders.

--ar 16:9`;

  const renderSpecs = `Clean crisp 2D anime lineart, flat 2-tone cel shading, vibrant colors, zero drop shadows, solid uniform ${bgPromptColorEn} (${bgPromptColorHex}) background for 1-click alpha keying, no text, no watermark, no border --ar ${selectedAspectRatio}`;

  // Helper to build explicit prompt for any angle of a component
  const buildPartAnglePrompt = (angleType: '000_front' | '045_three_quarter' | '090_side' | '135_rear' | '180_back' | 'high_angle' | 'low_angle') => {
    if (angleType === '180_back' && comp.rearVisibility === 'hidden') {
      return clampPromptLength(
        `pure empty blank canvas, uniform solid ${bgPromptColorEn} background (${bgPromptColorHex}), 100% empty space with zero graphics because ${comp.nameVi} is completely occluded and hidden when viewed from behind, no text, no borders --ar ${selectedAspectRatio}`
      );
    }

    let angleDesc = '';
    switch (angleType) {
      case '000_front':
        angleDesc = 'Direct 0° orthographic front view, facing camera squarely, perfectly upright and centered';
        break;
      case '045_three_quarter':
        angleDesc = 'Oblique 45° three-quarter angle view showing dimensional curve, depth, and thickness';
        break;
      case '090_side':
        angleDesc = 'Pure 90° lateral side profile view showing clean silhouette and joint edge contour';
        break;
      case '135_rear':
        angleDesc = 'Oblique 135° rear three-quarter angle view from behind';
        break;
      case '180_back':
        angleDesc = 'Direct 180° rear back view facing away from camera, showing backside surface texture';
        break;
      case 'high_angle':
        angleDesc = 'High-angle 3D top-down perspective looking downward from above';
        break;
      case 'low_angle':
        angleDesc = 'Low-angle bottom-up perspective looking upward from below';
        break;
    }

    return clampPromptLength(
      `masterpiece, ultra-detailed isolated 2D animation layer for 2D skeletal puppet rigging: ${comp.titleEn}. Camera View: ${angleDesc}. Character Artstyle: 2D ${artStyleEn} (${config.gender === 'nam' ? 'Male' : 'Female'}). Layer Details: Include ONLY ${comp.includedGeometry.join(', ')}. Rigging Separation Rule: This component is physically isolated and severed cleanly from the rest of the body. DO NOT include: ${comp.excludedGeometry.slice(0, 8).join(', ')}. ${renderSpecs}`
    );
  };

  const angle = config.view_angle || 'front';
  let angleKey: '000_front' | '045_three_quarter' | '090_side' | '180_back' | 'high_angle' | 'low_angle' = '000_front';
  let angleLabelVi = '0° Chính diện (Front 0°)';
  let angleDescEn = 'Camera directly facing the front of the isolated component';

  if (angle === 'three_quarter' || angle === '45' || angle === 'three_quarter_45') {
    angleKey = '045_three_quarter';
    angleLabelVi = '45° Nghiêng 3/4 (Three-Quarter 45°)';
    angleDescEn = 'Camera rotated 45 degrees showing three-quarter depth';
  } else if (angle === 'profile_side' || angle === '90' || angle === 'side_90') {
    angleKey = '090_side';
    angleLabelVi = '90° Nhìn ngang (Side Profile 90°)';
    angleDescEn = 'Camera rotated 90 degrees showing clean side silhouette';
  } else if (angle === 'back' || angle === '180' || angle === 'rear_180') {
    angleKey = '180_back';
    angleLabelVi = '180° Sau lưng (Rear Back 180°)';
    angleDescEn = comp.rearVisibility === 'hidden' ? 'Bị khuất từ sau lưng (ô rỗng)' : 'Camera directly facing the rear back of the component';
  } else if (angle === 'high_angle' || angle === 'top_down') {
    angleKey = 'high_angle';
    angleLabelVi = 'Trên cao nhìn xuống (High Angle)';
    angleDescEn = 'Camera positioned elevated above looking downward at the component';
  } else if (angle === 'low_angle' || angle === 'bottom_up') {
    angleKey = 'low_angle';
    angleLabelVi = 'Dưới hất lên (Low Angle)';
    angleDescEn = 'Camera positioned below looking upward at the component';
  }

  const singleAnglePromptItem = {
    name: `${comp.filePrefix}_${angleKey}`,
    part_id: comp.id,
    part_name: comp.nameVi,
    group_id: comp.groupId,
    group_name: comp.groupNameVi,
    angle: angleLabelVi,
    angle_id: angleKey,
    angle_deg: angleKey === '045_three_quarter' ? 45 : angleKey === '090_side' ? 90 : angleKey === '180_back' ? 180 : 0,
    z_index: comp.zIndex,
    save_filename: `${comp.filePrefix}_${angleKey}.png`,
    aspect_ratio: selectedAspectRatio,
    view_desc: angleDescEn,
    prompt: buildPartAnglePrompt(angleKey),
    count: userBatchCount,
  };

  // 1:1 SINGLE ASSET MODE
  if (sheet === 'single_isolated_1x1' || sheet === 'single_part') {

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
        aspect_ratio: selectedAspectRatio,
        view_desc: 'Góc chính diện 0° lơ lửng độc lập',
        prompt: buildPartAnglePrompt('000_front'),
        count: userBatchCount,
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
        aspect_ratio: selectedAspectRatio,
        view_desc: 'Góc nghiêng 3/4 45 độ',
        prompt: buildPartAnglePrompt('045_three_quarter'),
        count: userBatchCount,
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
        aspect_ratio: selectedAspectRatio,
        view_desc: 'Góc nhìn ngang 90 độ',
        prompt: buildPartAnglePrompt('090_side'),
        count: userBatchCount,
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
        aspect_ratio: selectedAspectRatio,
        view_desc: 'Góc quay trên cao nhìn chúc xuống',
        prompt: buildPartAnglePrompt('high_angle'),
        count: userBatchCount,
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
        aspect_ratio: selectedAspectRatio,
        view_desc: 'Góc quay dưới hất ngược lên',
        prompt: buildPartAnglePrompt('low_angle'),
        count: userBatchCount,
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
        aspect_ratio: selectedAspectRatio,
        view_desc: comp.rearVisibility === 'hidden' ? 'Bị khuất từ sau lưng (ô rỗng)' : 'Góc nhìn từ sau lưng',
        prompt: buildPartAnglePrompt('180_back'),
        count: userBatchCount,
      },
    ];

    const step2Prompts = config.json_scope === 'single_angle'
      ? [
          {
            name: `${comp.filePrefix}_${angleKey}`,
            part_id: comp.id,
            part_name: comp.nameVi,
            group_id: comp.groupId,
            group_name: comp.groupNameVi,
            angle: angleLabelVi,
            angle_id: angleKey,
            angle_deg: angleKey === '045_three_quarter' ? 45 : angleKey === '090_side' ? 90 : angleKey === '180_back' ? 180 : 0,
            z_index: comp.zIndex,
            save_filename: `${comp.filePrefix}_${angleKey}.png`,
            aspect_ratio: selectedAspectRatio,
            view_desc: angleDescEn,
            prompt: buildPartAnglePrompt(angleKey),
            count: userBatchCount,
          },
        ]
      : allComponentAnglesPrompts;

    const jsonPayload: any = {};
    if (config.include_base_prompt !== false) {
      jsonPayload.base_prompt = baseRefPrompt;
      jsonPayload.base_aspect_ratio = '16:9';
      jsonPayload.base_count = userBatchCount;
    }
    jsonPayload.prompts = step2Prompts;

    const promptJSON = JSON.stringify(jsonPayload, null, 2);

    const promptEnglish = `masterpiece, best quality, ultra detailed, ${selectedAspectRatio} aspect ratio, isolated 2D animation puppet layer: ${comp.titleEn}, angle: ${angleLabelVi}. Include ONLY: ${comp.includedGeometry.join(', ')}. DO NOT include: ${comp.excludedGeometry.slice(0, 10).join(', ')}. Solid uniform ${bgPromptColorEn} background, zero shadows, no text --ar ${selectedAspectRatio}`;
    const promptVietnamese = `【 ẢNH ĐƠN ${selectedAspectRatio} SIÊU NÉT — ${comp.nameVi} (${angleLabelVi}) 】\n• Tiêu đề: ${comp.titleEn}\n• Chi tiết cần vẽ: ${comp.includedGeometry.join(', ')}\n• Tuyệt đối loại trừ: ${comp.excludedGeometry.slice(0, 6).join(', ')}\n• Nền: ${bgTextVi}.`;
    const negativePrompt = 'full character, full body, head, face, extra limbs, multiple views, turnaround, comic panels, grid lines, borders, frames, divider lines, text, letters, numbers, watermark, signature, blurry, 3D CGI render, glow, rim light, color spill';
    const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;

    return {
      promptEnglish,
      promptVietnamese,
      promptJSON,
      promptGemini: promptVietnamese,
      gridStructureGuide: `📐 Khung ${selectedAspectRatio}: 1 Ảnh đơn siêu nét bóc tách chuẩn 2D.`,
      negativePrompt,
      fullCopyText,
    };
  }

  // 1x4 HORIZONTAL TURNAROUND (Default 16:9 or user selected ratio)
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
        aspect_ratio: selectedAspectRatio,
        view_desc: 'Góc chính diện 0° lơ lửng độc lập',
        prompt: buildPartAnglePrompt('000_front'),
        count: userBatchCount,
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
        aspect_ratio: selectedAspectRatio,
        view_desc: 'Góc nghiêng 3/4 45 độ',
        prompt: buildPartAnglePrompt('045_three_quarter'),
        count: userBatchCount,
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
        aspect_ratio: selectedAspectRatio,
        view_desc: 'Góc nhìn ngang 90 độ',
        prompt: buildPartAnglePrompt('090_side'),
        count: userBatchCount,
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
        aspect_ratio: selectedAspectRatio,
        view_desc: comp.rearVisibility === 'hidden' ? 'Bị khuất từ sau lưng (ô rỗng)' : 'Góc nhìn từ sau lưng',
        prompt: buildPartAnglePrompt('180_back'),
        count: userBatchCount,
      },
    ];

    const jsonPayload: any = {};
    if (config.include_base_prompt !== false) {
      jsonPayload.base_prompt = baseRefPrompt;
      jsonPayload.base_aspect_ratio = '16:9';
      jsonPayload.base_count = userBatchCount;
    }
    jsonPayload.prompts = config.json_scope === 'single_angle' ? [singleAnglePromptItem] : horizontal1x4Prompts;

    const promptJSON = JSON.stringify(jsonPayload, null, 2);

    const promptEnglish = `masterpiece, 4k resolution, ${selectedAspectRatio}, seamless 4-view horizontal rotation sequence of isolated ${comp.titleEn}: Front 0°, Three-Quarter 45°, Side Profile 90°, Rear Back 180°. Solid flat ${bgPromptColorEn} background, zero shadows, no text --ar ${selectedAspectRatio}`;
    const promptVietnamese = `【 CHUỖI XOAY NGANG 4 GÓC LIỀN MẠCH — ${comp.nameVi} (${selectedAspectRatio}) 】\n• Bố cục: 4 góc dàn ngang trên 1 hàng (0° ➔ 45° ➔ 90° ➔ 180°)\n• Nền: ${bgTextVi}.`;
    const negativePrompt = 'grid lines, divider lines, panel borders, box frames, comic panels, full character, full body, extra limbs, text, labels, watermark, blurry, 3D CGI render, glow';
    const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;

    return {
      promptEnglish,
      promptVietnamese,
      promptJSON,
      promptGemini: promptVietnamese,
      gridStructureGuide: `📐 Khung ${selectedAspectRatio} 1 Hàng: 4 góc dàn ngang tự nhiên.`,
      negativePrompt,
      fullCopyText,
    };
  }

  // 2x3 MULTI-ANGLE SHEET (Default 16:9 or user selected ratio)
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
      aspect_ratio: selectedAspectRatio,
      view_desc: 'Hàng trên Ô 1: Góc chính diện 0° lơ lửng độc lập',
      prompt: buildPartAnglePrompt('000_front'),
      count: userBatchCount,
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
      aspect_ratio: selectedAspectRatio,
      view_desc: 'Hàng trên Ô 2: Góc nghiêng 3/4 45 độ',
      prompt: buildPartAnglePrompt('045_three_quarter'),
      count: userBatchCount,
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
      aspect_ratio: selectedAspectRatio,
      view_desc: 'Hàng trên Ô 3: Góc nhìn ngang 90 độ',
      prompt: buildPartAnglePrompt('090_side'),
      count: userBatchCount,
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
      aspect_ratio: selectedAspectRatio,
      view_desc: 'Hàng dưới Ô 4: Góc quay trên cao nhìn chúc xuống',
      prompt: buildPartAnglePrompt('high_angle'),
      count: userBatchCount,
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
      aspect_ratio: selectedAspectRatio,
      view_desc: 'Hàng dưới Ô 5: Góc quay dưới hất ngược lên',
      prompt: buildPartAnglePrompt('low_angle'),
      count: userBatchCount,
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
      aspect_ratio: selectedAspectRatio,
      view_desc: comp.rearVisibility === 'hidden' ? 'Hàng dưới Ô 6: Bị khuất hoàn toàn (ô rỗng)' : 'Hàng dưới Ô 6: Mặt sau chi tiết',
      prompt: buildPartAnglePrompt('180_back'),
      count: userBatchCount,
    },
  ];

  const jsonPayload: any = {};
  if (config.include_base_prompt !== false) {
    jsonPayload.base_prompt = baseRefPrompt;
    jsonPayload.base_aspect_ratio = '16:9';
    jsonPayload.base_count = userBatchCount;
  }
  jsonPayload.prompts = config.json_scope === 'single_angle' ? [singleAnglePromptItem] : multiAngle2x3Prompts;

  const promptJSON = JSON.stringify(jsonPayload, null, 2);

  const promptEnglish = `masterpiece, 4k resolution, ${selectedAspectRatio}, modular 2D anime sprite sheet of isolated ${comp.titleEn} (6 views arranged in 2 rows of 3). Top: Front 0°, Three-Quarter 45°, Side Profile 90°. Bottom: High Angle, Low Angle, Rear Back 180°. Solid flat ${bgPromptColorEn} background, zero shadows, no text --ar ${selectedAspectRatio}`;
  const promptVietnamese = `【 BẢNG SPRITE 6 GÓC QUAY ĐIỆN ẢNH CHO 1 CHI TIẾT (LƯỚI 2×3 — ${selectedAspectRatio}) 】\n• Linh kiện: ${comp.nameVi}\n• Hàng trên: 1. Chính diện 0° | 2. Nghiêng 3/4 45° | 3. Nhìn ngang 90°\n• Hàng dưới: 4. Trên cao nhìn xuống | 5. Dưới hất lên | 6. Sau lưng 180°\n• Nền: ${bgTextVi}.`;
  const negativePrompt = 'grid lines, divider lines, cell borders, panel frames, black outlines around cells, comic panels, full character, full body, head, face, extra limbs, text, labels, watermark, blurry, 3D CGI render, glow, rim light';
  const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;

  return {
    promptEnglish,
    promptVietnamese,
    promptJSON,
    promptGemini: promptVietnamese,
    gridStructureGuide: `📐 Khung Cắt ${selectedAspectRatio}: Lưới 2 Hàng × 3 Cột điện ảnh.`,
    negativePrompt,
    fullCopyText,
  };
}
