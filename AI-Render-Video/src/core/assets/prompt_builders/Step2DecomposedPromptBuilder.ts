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
  getBangsStyleLabels,
  getBodyProportionLabels,
} from './PromptLabelHelpers';
import { getComponentDef, Asset2DComponentDef } from './Asset2DComponentDefs';
import { buildFilenameVariants } from './PartFilenameParser';
import { buildStep1MasterPrompt } from './Step1MasterPromptBuilder';

export { type Asset2DComponentDef, getComponentDef } from './Asset2DComponentDefs';

function clampPromptLength(prompt: string, maxLen = 3900): string {
  const trimmed = prompt.trim();
  if (trimmed.length <= maxLen) return trimmed;
  const arMatch = trimmed.match(/\s+--ar\s+([0-9]+:[0-9]+)$/);
  if (arMatch) {
    const arSuffix = arMatch[0];
    const maxBodyLen = maxLen - arSuffix.length - 3;
    return trimmed.slice(0, maxBodyLen) + '...' + arSuffix;
  }
  return trimmed.slice(0, maxLen - 3) + '...';
}

/**
 * Builds Step 2 Decomposed Isolated Parts Prompts (1:1 Single Asset, 1x4 Turnaround, 2x3 Multi-Angle Sheet)
 * Following Google's Official Nano Banana Pro (Gemini 3 Pro Image) Pseudo-Code Prompting Standard
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

  const artStyleEn = config.custom_character_style?.trim() || config.character_style?.trim() || 'Chinese Guoman / 国漫 Xianxia Chibi Anime';
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
  const bangsStyleInfo = getBangsStyleLabels(config.bangs_style, config.custom_bangs_style);
  const bodyPropInfo = getBodyProportionLabels(config.body_proportion, config.custom_body_proportion);

  const comp = getComponentDef(config.part_type || 'toc_truoc', {
    hairColInfo, hairTexInfo, hairLenInfo, hairAccInfo, bangsStyleInfo,
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

  const userBatchCount = typeof config.batch_count === 'number' && config.batch_count > 0 ? Math.min(4, Math.max(1, config.batch_count)) : 1;
  const userBaseCount = typeof config.base_count === 'number' && config.base_count > 0 ? Math.min(4, Math.max(1, config.base_count)) : 1;
  
  const ruleText = config.custom_rules?.trim() ||
    `MANDATORY: Solid flat ${bgPromptColorHex} ${bgPromptColorEn} background, zero shadows, no text, no watermark. Subject MUST be positioned exactly at the dead-center (centered horizontally and vertically on both X and Y axes) with generous equal green padding on all 4 borders, fully visible, zero cropping, nothing touching edges. If reference image is provided, use it ONLY for color/style matching — do NOT reproduce full character, body pose, or other anatomy.`;

  const step1MasterResult = buildStep1MasterPrompt(config);
  const baseRefPrompt = step1MasterResult.promptEnglish;

  // Helper to build explicit prompt for any angle of a component using Google Pseudo-Code standard
  const buildPartAnglePrompt = (angleType: '000_front' | '045_three_quarter' | '090_side' | '135_rear' | '180_back' | 'high_angle' | 'low_angle') => {
    if (angleType === '180_back' && comp.rearVisibility === 'hidden') {
      return clampPromptLength(
        `Generate an isolated sprite:\nASSET: EMPTY_BLANK_CANVAS\nCONTENT: Pure empty canvas with zero graphics, completely hollow space because ${comp.nameVi} is completely hidden and occluded from the rear.\nVIEW: 180° rear back view\nCANVAS: solid ${bgPromptColorHex} ${bgPromptColorEn}, 100% empty space.`
      );
    }

    let angleDesc = '';
    switch (angleType) {
      case '000_front':
        angleDesc = 'Strictly 0° dead-center orthographic front-facing view, perfectly perpendicular to camera, facing directly straight forward at the lens, perfect bilateral symmetry along vertical center axis, strictly ZERO yaw angle rotation, ZERO tilt, strictly NO 3/4 turn';
        break;
      case '045_three_quarter':
        angleDesc = 'Standard 45° three-quarter oblique view, turned precisely 45 degrees, showing dimensional depth and 3/4 contour';
        break;
      case '090_side':
        angleDesc = 'Pure 90° lateral side profile view, perfectly perpendicular sideways, exactly 90 degrees turned, showing flat lateral silhouette with zero front face visible';
        break;
      case '135_rear':
        angleDesc = 'Standard 135° rear three-quarter oblique view from behind, turned 135 degrees, showing back profile and rear volume';
        break;
      case '180_back':
        angleDesc = 'Strictly 180° direct rear back orthographic view, facing completely away from camera, bilateral symmetry from behind, zero front features visible';
        break;
      case 'high_angle':
        angleDesc = 'Strictly high-angle top-down bird-eye perspective looking directly downward at the asset';
        break;
      case 'low_angle':
        angleDesc = 'Strictly low-angle bottom-up worm-eye perspective looking directly upward from below';
        break;
    }

    const bangsField = comp.id === 'toc_truoc' ? `\nBANGS_STYLE: ${bangsStyleInfo.en}` : '';
    let extractionRule = `Replicate the EXACT ${comp.titleEn} shape, silhouette, and colors from the reference character if provided. Strictly omit all surrounding body parts.`;

    if (comp.id === 'toc_truoc') {
      extractionRule = `Extract/Generate ONLY the standalone front bangs clip-on hair accessory (${bangsStyleInfo.en}) as a detached decorative hair attachment floating alone on green screen. Strictly erase, omit, and replace with green background: all back hair, rear hair mantle, head, skull, face, eyes, and body.`;
    } else if (comp.id === 'toc_sau') {
      extractionRule = `Extract/Generate ONLY the plain base rear back hair (${hairLenInfo.en}, ${hairTexInfo.en}) with clean exposed forehead and pulled-back hairline. Strictly zero decorative front bangs or forehead fringe.`;
    }

    return clampPromptLength(
      `Task: Extract isolated 2D anime layer sprite.\nASSET: ${comp.assetTag}\nCONTENT: ${comp.positiveContent}${bangsField}\nVIEW: ${angleDesc}\nCOMPOSITION: Perfectly centered horizontally and vertically at the exact middle of the canvas, floating standalone with generous equal green padding on all 4 borders (top, bottom, left, right), zero cropping, fully contained inside the frame.\nSTYLE: 2D ${artStyleEn}, clean crisp anime lineart, flat 2-tone cel shading\nCANVAS: solid ${bgPromptColorHex} ${bgPromptColorEn}, dead-center placement, nothing else visible except the asset itself\nEXTRACTION_DIRECTIVE: ${extractionRule}\nEXCLUDE: ${comp.excludeShort}, off-center placement, shifted to edges, touching canvas border, cropped sprite, 3/4 angled turn on front view, rotated yaw angle, tilted camera, off-center perspective, dynamic pose tilt, Dutch angle.`
    );
  };

  const angle = config.view_angle || 'front';
  let angleKey: '000_front' | '045_three_quarter' | '090_side' | '180_back' | 'high_angle' | 'low_angle' = '000_front';
  let angleLabelVi = '0° Chính diện (Front 0°)';
  let angleDescEn = 'Strictly 0° dead-center orthographic front-facing view, perfectly perpendicular to camera, facing directly straight forward at the lens, perfect bilateral symmetry along vertical center axis, strictly ZERO yaw angle rotation, ZERO tilt, strictly NO 3/4 turn';

  if (angle === 'three_quarter' || angle === '45' || angle === 'three_quarter_45') {
    angleKey = '045_three_quarter';
    angleLabelVi = '45° Nghiêng 3/4 (Three-Quarter 45°)';
    angleDescEn = 'Standard 45° three-quarter oblique view, turned precisely 45 degrees, showing dimensional depth and 3/4 contour';
  } else if (angle === 'profile_side' || angle === '90' || angle === 'side_90') {
    angleKey = '090_side';
    angleLabelVi = '90° Nhìn ngang (Side Profile 90°)';
    angleDescEn = 'Pure 90° lateral side profile view, perfectly perpendicular sideways, exactly 90 degrees turned, showing flat lateral silhouette with zero front face visible';
  } else if (angle === 'back' || angle === '180' || angle === 'rear_180') {
    angleKey = '180_back';
    angleLabelVi = '180° Sau lưng (Rear Back 180°)';
    angleDescEn = comp.rearVisibility === 'hidden' ? 'Bị khuất từ sau lưng (ô rỗng)' : 'Strictly 180° direct rear back orthographic view, facing completely away from camera, bilateral symmetry from behind, zero front features visible';
  } else if (angle === 'high_angle' || angle === 'top_down') {
    angleKey = 'high_angle';
    angleLabelVi = 'Trên cao nhìn xuống (High Angle)';
    angleDescEn = 'Strictly high-angle top-down bird-eye perspective looking directly downward at the asset';
  } else if (angle === 'low_angle' || angle === 'bottom_up') {
    angleKey = 'low_angle';
    angleLabelVi = 'Dưới hất lên (Low Angle)';
    angleDescEn = 'Strictly low-angle bottom-up worm-eye perspective looking directly upward from below';
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
    rule: ruleText,
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
        rule: ruleText,
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
        rule: ruleText,
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
        rule: ruleText,
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
        rule: ruleText,
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
        rule: ruleText,
        prompt: buildPartAnglePrompt('low_angle'),
        count: userBatchCount,
      },
      ...(comp.rearVisibility !== 'hidden' ? [
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
          view_desc: 'Góc nhìn từ sau lưng',
          rule: ruleText,
          prompt: buildPartAnglePrompt('180_back'),
          count: userBatchCount,
        },
      ] : []),
    ];

    const singleAngleList = (angleKey === '180_back' && comp.rearVisibility === 'hidden')
      ? []
      : [singleAnglePromptItem];

    const step2Prompts = config.json_scope === 'single_angle'
      ? singleAngleList
      : allComponentAnglesPrompts;

    const jsonPayload: any = {};
    if (config.include_base_prompt !== false) {
      const baseFnMeta = buildFilenameVariants('master_character_turnaround.png', userBaseCount);
      jsonPayload.base_prompt = baseRefPrompt;
      jsonPayload.base_aspect_ratio = '16:9';
      jsonPayload.base_count = userBaseCount;
      jsonPayload.base_save_filename = baseFnMeta.save_filename;
      jsonPayload.base_save_filenames = baseFnMeta.save_filenames;
      jsonPayload.base_candidate_selection = baseFnMeta.candidate_selection;
    }
    jsonPayload.rule = ruleText;
    jsonPayload.prompts = step2Prompts.map((p) => {
      const fnMeta = buildFilenameVariants(p.save_filename, p.count);
      return {
        ...p,
        save_filename: fnMeta.save_filename,
        save_filename_pattern: fnMeta.save_filename_pattern,
        save_filenames: fnMeta.save_filenames,
        candidate_selection: fnMeta.candidate_selection,
      };
    });

    const promptJSON = JSON.stringify(jsonPayload, null, 2);

    const promptEnglish = buildPartAnglePrompt(angleKey);
    const promptVietnamese = `【 ẢNH ĐƠN ${selectedAspectRatio} TÁCH RỜI — ${comp.nameVi} (${angleLabelVi}) 】\n• ASSET: ${comp.assetTag}\n• CONTENT: ${comp.positiveContent}\n• VIEW: ${angleDescEn}\n• EXCLUDE: ${comp.excludeShort}\n• Nền: ${bgTextVi}.\n• Quy tắc: ${ruleText}.`;
    const negativePrompt = `${comp.excludeShort}, full character, full body, head, face, extra limbs, background scenery, floor shadows, borders, frames, grid lines, text, letters, numbers, watermark, blurry, 3D CGI render, photorealism`;
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
        rule: ruleText,
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
        rule: ruleText,
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
        rule: ruleText,
        prompt: buildPartAnglePrompt('090_side'),
        count: userBatchCount,
      },
      ...(comp.rearVisibility !== 'hidden' ? [
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
          view_desc: 'Góc nhìn từ sau lưng',
          rule: ruleText,
          prompt: buildPartAnglePrompt('180_back'),
          count: userBatchCount,
        },
      ] : []),
    ];

    const horizontal1x4SingleList = (angleKey === '180_back' && comp.rearVisibility === 'hidden')
      ? []
      : [singleAnglePromptItem];

    const jsonPayload: any = {};
    if (config.include_base_prompt !== false) {
      const baseFnMeta = buildFilenameVariants('master_character_turnaround.png', userBaseCount);
      jsonPayload.base_prompt = baseRefPrompt;
      jsonPayload.base_aspect_ratio = '16:9';
      jsonPayload.base_count = userBaseCount;
      jsonPayload.base_save_filename = baseFnMeta.save_filename;
      jsonPayload.base_save_filenames = baseFnMeta.save_filenames;
      jsonPayload.base_candidate_selection = baseFnMeta.candidate_selection;
    }
    jsonPayload.rule = ruleText;
    const targetPrompts = config.json_scope === 'single_angle' ? horizontal1x4SingleList : horizontal1x4Prompts;
    jsonPayload.prompts = targetPrompts.map((p) => {
      const fnMeta = buildFilenameVariants(p.save_filename, p.count);
      return {
        ...p,
        save_filename: fnMeta.save_filename,
        save_filename_pattern: fnMeta.save_filename_pattern,
        save_filenames: fnMeta.save_filenames,
        candidate_selection: fnMeta.candidate_selection,
      };
    });

    const promptJSON = JSON.stringify(jsonPayload, null, 2);

    const bangsField = comp.id === 'toc_truoc' ? `\nBANGS_STYLE: ${bangsStyleInfo.en}` : '';
    let extractionRule = `Replicate the EXACT ${comp.titleEn} shape, silhouette, and colors from the reference character if provided. Strictly omit all surrounding body parts.`;
    if (comp.id === 'toc_truoc') {
      extractionRule = `Extract/Generate ONLY the standalone front bangs clip-on hair accessory (${bangsStyleInfo.en}) as a detached decorative hair attachment floating alone on green screen. Strictly erase, omit, and replace with green background: all back hair, rear hair mantle, head, skull, face, eyes, and body.`;
    } else if (comp.id === 'toc_sau') {
      extractionRule = `Extract/Generate ONLY the plain base rear back hair (${hairLenInfo.en}, ${hairTexInfo.en}) with clean exposed forehead and pulled-back hairline. Strictly zero decorative front bangs or forehead fringe.`;
    }

    const promptEnglish = clampPromptLength(
      `Task: Extract isolated 4-view rotation sequence.\nASSET: ${comp.assetTag}_TURNAROUND_1X4\nCONTENT: 4 sequential rotation views of ${comp.positiveContent}${bangsField} arranged horizontally in 1 row (0° Front, 45° Three-Quarter, 90° Side Profile, 180° Rear Back).\nCOMPOSITION: All 4 rotation views evenly spaced and vertically centered along the horizontal middle axis of the canvas, generous equal padding on all borders, zero cropping.\nSTYLE: 2D ${artStyleEn}, clean crisp anime lineart, flat 2-tone cel shading\nCANVAS: solid ${bgPromptColorHex} ${bgPromptColorEn}, nothing else visible except the 4 views\nEXTRACTION_DIRECTIVE: ${extractionRule}\nEXCLUDE: ${comp.excludeShort}, off-center placement, touching borders, cropped sprites, comic panel borders, box frames, dividing lines.`
    );
    const promptVietnamese = `【 CHUỖI XOAY NGANG 4 GÓC LIỀN MẠCH — ${comp.nameVi} (${selectedAspectRatio}) 】\n• Bố cục: 4 góc dàn ngang trên 1 hàng (0° ➔ 45° ➔ 90° ➔ 180°)\n• Nền: ${bgTextVi}.\n• Quy tắc: ${ruleText}.`;
    const negativePrompt = `${comp.excludeShort}, full character, full body, mannequin, head, face, extra limbs, comic panels, panel borders, grid lines, divider lines, text, labels, watermark, blurry, 3D CGI render, photorealism`;
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
      rule: ruleText,
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
      rule: ruleText,
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
      rule: ruleText,
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
      rule: ruleText,
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
      rule: ruleText,
      prompt: buildPartAnglePrompt('low_angle'),
      count: userBatchCount,
    },
    ...(comp.rearVisibility !== 'hidden' ? [
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
        view_desc: 'Hàng dưới Ô 6: Góc nhìn từ sau lưng',
        rule: ruleText,
        prompt: buildPartAnglePrompt('180_back'),
        count: userBatchCount,
      },
    ] : []),
  ];

  const multiAngle2x3SingleList = (angleKey === '180_back' && comp.rearVisibility === 'hidden')
    ? []
    : [singleAnglePromptItem];

  const jsonPayload: any = {};
  if (config.include_base_prompt !== false) {
    const baseFnMeta = buildFilenameVariants('master_character_turnaround.png', userBaseCount);
    jsonPayload.base_prompt = baseRefPrompt;
    jsonPayload.base_aspect_ratio = '16:9';
    jsonPayload.base_count = userBaseCount;
    jsonPayload.base_save_filename = baseFnMeta.save_filename;
    jsonPayload.base_save_filenames = baseFnMeta.save_filenames;
    jsonPayload.base_candidate_selection = baseFnMeta.candidate_selection;
  }
  jsonPayload.rule = ruleText;
  const targetPrompts = config.json_scope === 'single_angle' ? multiAngle2x3SingleList : multiAngle2x3Prompts;
  jsonPayload.prompts = targetPrompts.map((p) => {
    const fnMeta = buildFilenameVariants(p.save_filename, p.count);
    return {
      ...p,
      save_filename: fnMeta.save_filename,
      save_filename_pattern: fnMeta.save_filename_pattern,
      save_filenames: fnMeta.save_filenames,
      candidate_selection: fnMeta.candidate_selection,
    };
  });

  const promptJSON = JSON.stringify(jsonPayload, null, 2);

  const bangsField2x3 = comp.id === 'toc_truoc' ? `\nBANGS_STYLE: ${bangsStyleInfo.en}` : '';
  let extractionRule2x3 = `Replicate the EXACT ${comp.titleEn} shape, silhouette, and colors from the reference character if provided. Strictly omit all surrounding body parts.`;
  if (comp.id === 'toc_truoc') {
    extractionRule2x3 = `Extract/Generate ONLY the standalone front bangs clip-on hair accessory (${bangsStyleInfo.en}) as a detached decorative hair attachment floating alone on green screen. Strictly erase, omit, and replace with green background: all back hair, rear hair mantle, head, skull, face, eyes, and body.`;
  } else if (comp.id === 'toc_sau') {
    extractionRule2x3 = `Extract/Generate ONLY the plain base rear back hair (${hairLenInfo.en}, ${hairTexInfo.en}) with clean exposed forehead and pulled-back hairline. Strictly zero decorative front bangs or forehead fringe.`;
  }

  const promptEnglish = clampPromptLength(
    `Task: Extract isolated 6-view sprite sheet.\nASSET: ${comp.assetTag}_SPRITE_SHEET_2X3\nCONTENT: 6 multi-angle views of ${comp.positiveContent}${bangsField2x3} arranged in a 2x3 grid (Row 1: 0° Front, 45° Three-Quarter, 90° Side. Row 2: High Angle, Low Angle, 180° Back).\nCOMPOSITION: All 6 sprite views perfectly centered within their respective grid cells, uniform scale, generous padding between cells and outer canvas borders, zero cropping.\nSTYLE: 2D ${artStyleEn}, clean crisp anime lineart, flat 2-tone cel shading\nCANVAS: solid ${bgPromptColorHex} ${bgPromptColorEn}, nothing else visible except the sprite cells\nEXTRACTION_DIRECTIVE: ${extractionRule2x3}\nEXCLUDE: ${comp.excludeShort}, off-center cells, touching borders, cropped sprites, grid lines, divider lines, cell borders, panel frames.`
  );
  const promptVietnamese = `【 BẢNG SPRITE 6 GÓC QUAY ĐIỆN ẢNH CHO 1 CHI TIẾT (LƯỚI 2×3 — ${selectedAspectRatio}) 】\n• Linh kiện: ${comp.nameVi}\n• Hàng trên: 1. Chính diện 0° | 2. Nghiêng 3/4 45° | 3. Nhìn ngang 90°\n• Hàng dưới: 4. Trên cao nhìn xuống | 5. Dưới hất lên | 6. Sau lưng 180°\n• Nền: ${bgTextVi}.\n• Quy tắc: ${ruleText}.`;
  const negativePrompt = `${comp.excludeShort}, grid lines, divider lines, cell borders, panel frames, black outlines around cells, comic panels, full character, full body, head, face, extra limbs, text, labels, watermark, blurry, 3D CGI render, photorealism`;
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
