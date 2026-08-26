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
  getSkinToneLabels,
} from './PromptLabelHelpers';
import { buildFilenameVariants } from './PartFilenameParser';

/**
 * Tài liệu chú giải các trường JSON cho AI và người dùng
 */
export const JSON_SCHEMA_FIELD_GUIDE = {
  _doc: 'FlowMy 2D Animation Studio - JSON Schema Guide cho AI Image Generator & 2D Rigging',
  base_prompt: 'Chuỗi mô tả ngoại hình gốc của nhân vật (phong cách, màu sắc, trang phục, mái tóc) làm mỏ neo đồng bộ nhất quán giữa tất cả các góc quay và linh kiện.',
  base_aspect_ratio: 'Tỉ lệ khung hình của ảnh tham chiếu gốc (mặc định 16:9).',
  base_count: 'Số lượng biến thể ảnh sinh riêng cho base_prompt nhân vật gốc (tối đa 4 ảnh).',
  base_save_filename: 'Tên tệp đích chuẩn cho ảnh mỏ neo Turnaround.',
  base_save_filenames: 'Mảng danh sách các tên tệp ứng viên khi sinh nhiều ảnh base (master_character_turnaround_01.png, 02...).',
  base_candidate_selection: 'Hướng dẫn chọn 1 ảnh base đẹp nhất làm chuẩn chung.',
  rule: 'Quy tắc kết xuất bắt buộc đính kèm (bắt buộc nền đơn sắc đồng nhất, không bóng đổ, nét vẽ khép kín, không watermark).',
  prompts: 'Danh sách các tác vụ sinh ảnh chi tiết cần thực thi.',
  'prompts[].name': 'Mã định danh duy nhất của layer / góc quay (dùng để lưu file và tự động load vào khớp xương).',
  'prompts[].part_id': 'Mã slot linh kiện 2D giải phẫu (ví dụ: master_character, toc_truoc, khuon_mat, canh_tay_trai, vu_khi).',
  'prompts[].part_name': 'Tên tiếng Việt của bộ phận / khớp xương.',
  'prompts[].group_id': 'Mã nhóm giải phẫu (01_head_face, 02_torso_arms, 03_legs_feet, 04_props_costumes, step1_master).',
  'prompts[].group_name': 'Tên tiếng Việt của nhóm giải phẫu.',
  'prompts[].angle': 'Tên góc quay hiển thị (Chính diện 0°, Nghiêng 3/4 45°, Nhìn ngang 90°, Sau lưng 180°...).',
  'prompts[].angle_id': 'Mã góc máy chuẩn hóa (000_front, 045_three_quarter, 090_side, 135_rear, 180_back, top_down...).',
  'prompts[].angle_deg': 'Độ góc quay số học (0..360) phục vụ xoay trục không gian và nội suy góc nhìn 3D.',
  'prompts[].z_index': 'Thứ tự sắp xếp độ sâu lớp vẽ từ dưới lên trên (số lớn hơn vẽ đè lên số nhỏ hơn).',
  'prompts[].save_filename': 'Tên file ảnh xuất đích thực sau khi chọn lọc để công cụ cắt lưới và Assembler tự động nạp.',
  'prompts[].save_filename_pattern': 'Mẫu đặt tên khi sinh nhiều biến thể ảnh ({index} = 01, 02...).',
  'prompts[].save_filenames': 'Danh sách tên tệp cụ thể cho từng ảnh biến thể tương ứng với count.',
  'prompts[].candidate_selection': 'Quy tắc chọn lọc 1 ảnh biến thể chuẩn nhất gán vào khung xương 2D.',
  'prompts[].view_desc': 'Mô tả góc nhìn và vị trí camera bằng tiếng Việt giúp người dùng và AI hiểu hướng quan sát.',
  'prompts[].rule': 'Quy tắc kết xuất bắt buộc của tác vụ (nền đơn sắc, tách biệt layer, cấm chữ...).',
  'prompts[].prompt': 'Câu lệnh tạo ảnh chi tiết hoàn chỉnh: mô tả hình dạng, chi tiết bóc tách, màu sắc, những gì cần vẽ và cấm vẽ, phông nền đơn sắc, quy tắc rule và tỉ lệ khung hình.',
  'prompts[].count': 'Số lượng ảnh AI cần sinh cho tác vụ này (đồng bộ theo ô nhập liệu).',
  'prompts[].aspect_ratio': 'Tỉ lệ khung hình của ảnh kết xuất (1:1, 3:4, 4:3, 16:9, 9:16).',
};

export function getPromptRules(config: AIPartPromptConfig, bgPromptColorEn: string, bgPromptColorHex: string): string {
  if (config.custom_rules && config.custom_rules.trim().length > 0) {
    return config.custom_rules.trim();
  }
  return `MANDATORY RENDERING RULES: Strictly flat solid uniform pure ${bgPromptColorEn} (${bgPromptColorHex}) background with ZERO gradient and zero ambient shadow. Clean crisp 2D lineart with closed paths for 1-click chroma keying alpha cutout. Strictly NO background scenery, NO cast shadows, NO text, NO labels, NO watermark, NO borders.`;
}

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
 * Builds Step 1 Master Turnaround Prompts and JSON for 2D Character Reference
 */
export function buildStep1MasterPrompt(config: AIPartPromptConfig): AIPromptResult {
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

  const isMale = config.gender === 'nam';
  const genderLabelEn = isMale ? 'Male' : 'Female';
  const genderLabelVi = isMale ? 'Nam' : 'Nữ';
  const artStyleEn = config.custom_character_style?.trim() || config.character_style?.trim() || 'Chinese Guoman / 国漫 Xianxia Chibi Anime';
  const artStyleVi = getStyleLabel(config.character_style, config.custom_character_style);

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
  const skinToneInfo = getSkinToneLabels(config.skin_tone, config.custom_skin_tone);

  const selectedAspectRatio = config.aspect_ratio || '16:9';
  const userBatchCount = typeof config.batch_count === 'number' && config.batch_count > 0 ? config.batch_count : 1;
  const ruleText = getPromptRules(config, bgPromptColorEn, bgPromptColorHex);

  const baseDescEn = `masterpiece, ultra-detailed 2D ${artStyleEn} character reference: ${genderLabelEn}, ${bodyPropInfo.en}, skin: ${skinToneInfo.en}, face (${eyeShapeInfo.en}, ${eyeColInfo.en}, ${noseInfo.en}, ${mouthInfo.en}), hair (${hairColInfo.en}, ${hairTexInfo.en}, ${hairLenInfo.en}, front bangs style: ${bangsStyleInfo.en}${hairAccInfo.en !== 'none' ? `, ${hairAccInfo.en}` : ''}), costume (${costumeInfo.en}, color theme: ${costumeColorVi}), weapon/prop: ${propInfo.en}, clean crisp 2D anime lineart, flat cel shading, zero cast shadows, flat solid ${bgPromptColorEn} background, no scenery, no text, no watermark`;

  const promptEnglish = `masterpiece, best quality, ultra detailed, 2D ${artStyleEn} character turnaround sheet, ONE SINGLE IDENTICAL ${genderLabelEn.toUpperCase()} CHARACTER.

CHARACTER SPECIFICATIONS:
- Gender: ${genderLabelEn}
- Art Style: ${artStyleEn}
- Skin Tone: ${skinToneInfo.en}
- Proportion: ${bodyPropInfo.en}
- Eyes: ${eyeShapeInfo.en}, ${eyeColInfo.en}
- Nose & Mouth: ${noseInfo.en}, ${mouthInfo.en}
- Hairstyle: ${hairTexInfo.en}, ${hairColInfo.en}, ${hairLenInfo.en}, front bangs style: ${bangsStyleInfo.en}${hairAccInfo.en !== 'none' ? `, ${hairAccInfo.en}` : ''}
- Costume: ${costumeInfo.en} (Color: ${costumeColorVi})
- Weapon / Props: ${propInfo.en}

TURNAROUND 5-VIEW SEQUENCE (${selectedAspectRatio} Canvas):
1. VIEW 1 — FRONT (0°): Direct frontal orthographic view, bilateral symmetry, full body from head to feet.
2. VIEW 2 — THREE-QUARTER LEFT (45°): 45-degree angle turned towards viewer's left showing face depth, cheek, and shoulder curve.
3. VIEW 3 — SIDE PROFILE LEFT (90°): 90-degree clean lateral side profile facing sideways to the left showing nose bridge and spine silhouette.
4. VIEW 4 — REAR THREE-QUARTER LEFT (135°): 135-degree rear angle turned towards rear left showing back waist sash and rear hair flow.
5. VIEW 5 — BACK (180°): Full rear back view showing back hair mantle, shoulder blade lines, and robe spine.
+ TOP-DOWN REFERENCE: Top-down view looking downward at the head crown and hair parting.

CONSISTENCY & RESTRICTIONS:
- 100% identical character rotated strictly around vertical axis.
- Clean 2D anime vector-like lineart, flat 2-tone cel shading, zero drop shadows, zero ambient occlusion on ground.
- Flat uniform ${bgPromptColorEn} (${bgPromptColorHex}) background with zero gradients or cast shadows.
- STRICTLY NO text, NO letters, NO numbers, NO labels, NO watermark, NO grid borders.`;

  const promptVietnamese = `【 BẢNG THIẾT KẾ NHÂN VẬT GỐC ĐA GÓC QUAY (CHARACTER TURNAROUND SHEET - ${selectedAspectRatio}) 】

════════════════════════════════════════════════════════════
1. THUỘC TÍNH NHÂN VẬT:
════════════════════════════════════════════════════════════
• Giới tính: ${genderLabelVi} | Phong cách: ${artStyleVi} | Tỷ lệ: ${bodyPropInfo.vi}
• Khuôn mặt: Mắt ${eyeShapeInfo.vi} (${eyeColInfo.vi}), mũi ${noseInfo.vi}, miệng ${mouthInfo.vi}
• Mái tóc: ${hairColInfo.vi}, ${hairTexInfo.vi}, ${hairLenInfo.vi}, kiểu mái trước: ${bangsStyleInfo.vi} (${hairAccInfo.vi})
• Trang phục: ${costumeInfo.vi} (Màu sắc: ${costumeColorVi})
• Vũ khí / Pháp bảo: ${propInfo.vi}

════════════════════════════════════════════════════════════
2. BỐ CỤC 5 GÓC XOAY TOÀN THÂN (${selectedAspectRatio}):
════════════════════════════════════════════════════════════
1. Front (0° Chính diện - Đối xứng): Mẫu tham chiếu chính, toàn thân thẳng đứng trực diện góc máy.
2. 45° (Nghiêng 3/4 Trái): Thấy độ sâu khuôn mặt, má, tóc mai và vai quay 45° sang trái.
3. Side (90° Nhìn ngang Trái): Thấy sống mũi, cằm, vành tai và dáng suối tóc nhìn nghiêng sang trái.
4. 135° (Nghiêng sau Trái): Thấy tà áo choàng, thắt lưng và búi tóc sau gáy quay 135° sang trái.
5. Back (180° Sau lưng): Thấy toàn bộ suối tóc và lưng áo từ phía sau.
+ Góc phụ Đỉnh Đầu (Top-Down): Soi đỉnh đầu từ trên xuống thấy đường rẽ ngôi và trâm cài.

• Nền: ${bgTextVi}.
• Quy tắc bắt buộc: ${ruleText}.
• CẤM: KHÔNG CHỮ, KHÔNG SỐ, KHÔNG NHÃN DÁN, KHÔNG ĐƯỜNG KẺ KHUNG ĐEN, KHÔNG WATERMARK.`;

  const commonVisualRules = `Clean crisp 2D anime lineart, flat 2-tone cel shading, vibrant colors, zero cast shadows, solid uniform flat ${bgPromptColorEn} (${bgPromptColorHex}) background, no scenery, no text, no watermark`;

  const step1Prompts = [
    {
      name: 'master_000_front',
      part_id: 'master_character',
      part_name: 'Nhân Vật Gốc Toàn Thân',
      group_id: 'step1_master',
      group_name: 'Bảng Xoay Nhân Vật Gốc',
      angle: '0° Front (Chính diện - Đối xứng)',
      angle_id: '000_front',
      angle_deg: 0,
      z_index: 0,
      save_filename: 'master_000_front.png',
      aspect_ratio: selectedAspectRatio,
      view_desc: 'Góc nhìn chính diện 0° toàn thân làm chuẩn tham chiếu chính',
      prompt: clampPromptLength(
        `masterpiece, ultra-detailed 2D ${artStyleEn} character, ${genderLabelEn}, ${bodyPropInfo.en}. Camera: Strictly 0° dead-center orthographic front-facing view, perfectly perpendicular to camera, facing directly forward straight at the lens, perfect bilateral symmetry along vertical center axis, strictly ZERO yaw angle rotation, ZERO tilt, full body from head crown to shoes, standing upright with relaxed arms. Character features: face (${eyeShapeInfo.en}, ${eyeColInfo.en}, ${noseInfo.en}, ${mouthInfo.en}), hair (${hairColInfo.en}, ${hairTexInfo.en}, ${hairLenInfo.en}, front bangs style: ${bangsStyleInfo.en}${hairAccInfo.en !== 'none' ? `, ${hairAccInfo.en}` : ''}), outfit (${costumeInfo.en}, color: ${costumeColorVi}), holding weapon (${propInfo.en}). ${commonVisualRules}`
      ),
      count: userBatchCount,
    },
    {
      name: 'master_045_three_quarter',
      part_id: 'master_character',
      part_name: 'Nhân Vật Gốc Toàn Thân',
      group_id: 'step1_master',
      group_name: 'Bảng Xoay Nhân Vật Gốc',
      angle: '45° Three-Quarter Left (Nghiêng 3/4 Trái)',
      angle_id: '045_three_quarter',
      angle_deg: 45,
      z_index: 0,
      save_filename: 'master_045_three_quarter.png',
      aspect_ratio: selectedAspectRatio,
      view_desc: 'Góc xoay 45° sang trái hiển thị độ sâu ngũ quan, tóc mai và vai',
      prompt: clampPromptLength(
        `masterpiece, ultra-detailed 2D ${artStyleEn} character, 100% identical ${genderLabelEn}, ${bodyPropInfo.en}. Camera: Standard 45° three-quarter oblique angle view turned towards viewer's left, full body from head to feet, clearly showing cheekbone depth, 3/4 facial contour, nose bridge curve, chest garment depth, and shoulder angle with near/far perspective foreshortening. Hair: (${hairColInfo.en}, ${hairTexInfo.en}, front bangs style: ${bangsStyleInfo.en}). Outfit: (${costumeInfo.en}, color: ${costumeColorVi}). Weapon: (${propInfo.en}). ${commonVisualRules}`
      ),
      count: userBatchCount,
    },
    {
      name: 'master_090_side',
      part_id: 'master_character',
      part_name: 'Nhân Vật Gốc Toàn Thân',
      group_id: 'step1_master',
      group_name: 'Bảng Xoay Nhân Vật Gốc',
      angle: '90° Side Profile Left (Nhìn ngang Trái)',
      angle_id: '090_side',
      angle_deg: 90,
      z_index: 0,
      save_filename: 'master_090_side.png',
      aspect_ratio: selectedAspectRatio,
      view_desc: 'Góc nhìn ngang 90° sang trái thấy sống mũi, cằm, tai và dáng suối tóc',
      prompt: clampPromptLength(
        `masterpiece, ultra-detailed 2D ${artStyleEn} character, 100% identical ${genderLabelEn}, ${bodyPropInfo.en}. Camera: Pure 90° lateral side profile view facing sideways to the viewer's left, full body from head to feet, showing clean facial silhouette with nose bridge, lips, chin, ear placement, spine posture, front bangs style: ${bangsStyleInfo.en}, and flowing back hair cascading down the spine. Outfit: (${costumeInfo.en}, color: ${costumeColorVi}). ${commonVisualRules}`
      ),
      count: userBatchCount,
    },
    {
      name: 'master_135_rear',
      part_id: 'master_character',
      part_name: 'Nhân Vật Gốc Toàn Thân',
      group_id: 'step1_master',
      group_name: 'Bảng Xoay Nhân Vật Gốc',
      angle: '135° Rear Three-Quarter (Nghiêng sau)',
      angle_id: '135_rear',
      angle_deg: 135,
      z_index: 0,
      save_filename: 'master_135_rear.png',
      aspect_ratio: selectedAspectRatio,
      view_desc: 'Góc xoay 135° thấy tà áo sau, thắt lưng và búi tóc',
      prompt: clampPromptLength(
        `masterpiece, ultra-detailed 2D ${artStyleEn} character, 100% identical ${genderLabelEn}, ${bodyPropInfo.en}. Camera: 135° rear three-quarter oblique view from behind, full body, showing rear back of hair bun, hair ribbons, back neckline, shoulder blades, waist sash buckle, and flowing robe mantle. ${commonVisualRules}`
      ),
      count: userBatchCount,
    },
    {
      name: 'master_180_back',
      part_id: 'master_character',
      part_name: 'Nhân Vật Gốc Toàn Thân',
      group_id: 'step1_master',
      group_name: 'Bảng Xoay Nhân Vật Gốc',
      angle: '180° Back (Sau lưng)',
      angle_id: '180_back',
      angle_deg: 180,
      z_index: 0,
      save_filename: 'master_180_back.png',
      aspect_ratio: selectedAspectRatio,
      view_desc: 'Góc sau lưng 180° thấy trọn vẹn suối tóc và lưng áo',
      prompt: clampPromptLength(
        `masterpiece, ultra-detailed 2D ${artStyleEn} character, 100% identical ${genderLabelEn}, ${bodyPropInfo.en}. Camera: 180° direct rear back orthographic view facing away from camera, full body from head crown to heels. Complete back hair mantle (${hairColInfo.en}, ${hairLenInfo.en}), back of costume robe spine (${costumeInfo.en}, color: ${costumeColorVi}), back of sash. Zero front face visible. ${commonVisualRules}`
      ),
      count: userBatchCount,
    },
    {
      name: 'master_top_down',
      part_id: 'master_character',
      part_name: 'Nhân Vật Gốc Toàn Thân',
      group_id: 'step1_master',
      group_name: 'Bảng Xoay Nhân Vật Gốc',
      angle: 'Top-Down (Đỉnh đầu)',
      angle_id: 'top_down',
      angle_deg: 90,
      z_index: 0,
      save_filename: 'master_top_down.png',
      aspect_ratio: selectedAspectRatio,
      view_desc: 'Góc phụ soi đỉnh đầu từ trên xuống thấy đường rẽ ngôi và trâm cài',
      prompt: clampPromptLength(
        `masterpiece, ultra-detailed 2D ${artStyleEn} character reference. Camera: Top-down vertical bird's-eye view looking directly down at the character's head crown, clearly showing the hair parting line, front bangs style: ${bangsStyleInfo.en}, hair crown volume, top hair accessories (${hairAccInfo.en}), and shoulder top silhouette. Hair: (${hairColInfo.en}, ${hairTexInfo.en}). ${commonVisualRules}`
      ),
      count: userBatchCount,
    },
  ];

  const userBaseCount = typeof config.base_count === 'number' && config.base_count > 0 ? Math.min(4, Math.max(1, config.base_count)) : userBatchCount;

  const step1PromptsWithMeta = step1Prompts.map((p) => {
    const fnMeta = buildFilenameVariants(p.save_filename, p.count);
    return {
      ...p,
      save_filename: fnMeta.save_filename,
      save_filename_pattern: fnMeta.save_filename_pattern,
      save_filenames: fnMeta.save_filenames,
      candidate_selection: fnMeta.candidate_selection,
      rule: ruleText,
    };
  });

  const jsonPayload: any = {};
  if (config.include_base_prompt !== false) {
    const baseFnMeta = buildFilenameVariants('master_character_turnaround.png', userBaseCount);
    jsonPayload.base_prompt = baseDescEn;
    jsonPayload.base_aspect_ratio = selectedAspectRatio;
    jsonPayload.base_count = userBaseCount;
    jsonPayload.base_save_filename = baseFnMeta.save_filename;
    jsonPayload.base_save_filenames = baseFnMeta.save_filenames;
    jsonPayload.base_candidate_selection = baseFnMeta.candidate_selection;
  }
  jsonPayload.rule = ruleText;
  jsonPayload.prompts = step1PromptsWithMeta;

  const promptJSON = JSON.stringify(jsonPayload, null, 2);

  const negativePrompt = 'realistic human face, small realistic eyes, 3D CGI render, photorealism, live-action, western comic style, ugly anatomy, deformed face, muddy colors, bad eyes, realistic skin texture, realistic wrinkles, dull eyes, pores, text, letters, words, labels, watermark, signature, bad proportions, divider lines, grid frames';
  const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;
  const promptGemini = promptVietnamese;

  return {
    promptEnglish,
    promptVietnamese,
    promptJSON,
    promptGemini,
    gridStructureGuide: `📐 Bảng Xoay Nhân Vật: 5 góc toàn thân chuẩn ${selectedAspectRatio}.`,
    negativePrompt,
    fullCopyText,
  };
}
