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

/**
 * Builds Step 1 Master Turnaround Prompts and JSON for 2D Character Reference
 */
export function buildStep1MasterPrompt(config: AIPartPromptConfig): AIPromptResult {
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

  const isMale = config.gender === 'nam';
  const genderLabelEn = isMale ? 'Male' : 'Female';
  const genderLabelVi = isMale ? 'Nam' : 'Nữ';
  const artStyleEn = config.custom_character_style?.trim() || config.character_style?.trim() || 'Chinese Guoman / 国漫 Xianxia Chibi';
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
  const bodyPropInfo = getBodyProportionLabels(config.body_proportion, config.custom_body_proportion);

  const baseDescEn = `2D ${artStyleEn} character turnaround sheet, ${genderLabelEn}, ${bodyPropInfo.en}, face (${eyeShapeInfo.en}, ${eyeColInfo.en}, ${noseInfo.en}, ${mouthInfo.en}), hair (${hairColInfo.en}, ${hairTexInfo.en}, ${hairLenInfo.en}${hairAccInfo.en !== 'none' ? `, ${hairAccInfo.en}` : ''}), costume (${costumeInfo.en}, color: ${costumeColorVi}), weapon: ${propInfo.en}, flat ${bgPromptColorEn} background, clean anime lineart, cel shading, zero shadows, no text, no borders --ar 16:9`;

  const promptEnglish = `masterpiece, best quality, ultra detailed, 2D ${artStyleEn} character turnaround sheet, ONE SINGLE IDENTICAL ${genderLabelEn.toUpperCase()} CHARACTER.

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
1. VIEW 1 — FRONT (0°): Direct frontal view, full body from head to feet.
2. VIEW 2 — THREE-QUARTER (45°): 45-degree angle showing face depth and shoulder.
3. VIEW 3 — SIDE PROFILE (90°): 90-degree clean side silhouette and nose profile.
4. VIEW 4 — REAR THREE-QUARTER (135°): 135-degree angle showing back sash and rear hair.
5. VIEW 5 — BACK (180°): Full rear view showing back hair mantle and robe spine.
+ TOP-DOWN REFERENCE: One smaller top-down view showing head crown and parting.

CONSISTENCY & RESTRICTIONS:
- 100% identical character rotated around vertical axis.
- Clean 2D anime lineart, flat cel shading, readable silhouettes for 2D rigging.
- Flat uniform ${bgPromptColorEn} (${bgPromptColorHex}) background with zero shadows.
- STRICTLY NO text, NO letters, NO numbers, NO labels, NO watermark, NO grid borders.

--ar 16:9`;

  const promptVietnamese = `【 BẢNG THIẾT KẾ NHÂN VẬT GỐC ĐA GÓC QUAY (CHARACTER TURNAROUND SHEET - 16:9) 】

════════════════════════════════════════════════════════════
1. THUỘC TÍNH NHÂN VẬT:
════════════════════════════════════════════════════════════
• Giới tính: ${genderLabelVi} | Phong cách: ${artStyleVi} | Tỷ lệ: ${bodyPropInfo.vi}
• Khuôn mặt: Mắt ${eyeShapeInfo.vi} (${eyeColInfo.vi}), mũi ${noseInfo.vi}, miệng ${mouthInfo.vi}
• Mái tóc: ${hairColInfo.vi}, ${hairTexInfo.vi}, ${hairLenInfo.vi} (${hairAccInfo.vi})
• Trang phục: ${costumeInfo.vi} (Màu sắc: ${costumeColorVi})
• Vũ khí / Pháp bảo: ${propInfo.vi}

════════════════════════════════════════════════════════════
2. BỐ CỤC 5 GÓC XOAY TOÀN THÂN (16:9):
════════════════════════════════════════════════════════════
1. Front (0° Chính diện): Mẫu tham chiếu chính, toàn thân thẳng góc máy.
2. 45° (Nghiêng 3/4): Thấy độ sâu khuôn mặt, tóc mai và vai.
3. Side (90° Nhìn ngang): Thấy sống mũi, cằm, vành tai và dáng suối tóc.
4. 135° (Nghiêng sau): Thấy tà áo choàng, thắt lưng và búi tóc sau gáy.
5. Back (180° Sau lưng): Thấy toàn bộ suối tóc và lưng áo.
+ Góc phụ Đỉnh Đầu (Top-Down): Soi đỉnh đầu từ trên xuống thấy đường rẽ ngôi và trâm cài.

• Nền: ${bgTextVi}.
• CẤM: KHÔNG CHỮ, KHÔNG SỐ, KHÔNG NHÃN DÁN, KHÔNG ĐƯỜNG KẺ KHUNG ĐEN, KHÔNG WATERMARK.`;

  const step1Prompts = [
    {
      name: 'master_000_front',
      part_id: 'master_character',
      part_name: 'Nhân Vật Gốc Toàn Thân',
      group_id: 'step1_master',
      group_name: 'Bảng Xoay Nhân Vật Gốc',
      angle: '0° Front (Chính diện)',
      angle_id: '000_front',
      angle_deg: 0,
      z_index: 0,
      save_filename: 'master_000_front.png',
      view_desc: 'Góc nhìn chính diện 0° toàn thân làm chuẩn tham chiếu chính',
      prompt: `0° front view of the character, facing camera, full body, head to feet, clear eyes and bangs, uniform solid ${bgPromptColorEn} background --ar 16:9`,
      count: 4,
    },
    {
      name: 'master_045_three_quarter',
      part_id: 'master_character',
      part_name: 'Nhân Vật Gốc Toàn Thân',
      group_id: 'step1_master',
      group_name: 'Bảng Xoay Nhân Vật Gốc',
      angle: '45° Three-Quarter (Nghiêng 3/4)',
      angle_id: '045_three_quarter',
      angle_deg: 45,
      z_index: 0,
      save_filename: 'master_045_three_quarter.png',
      view_desc: 'Góc xoay 45° hiển thị độ sâu ngũ quan, tóc mai và vai',
      prompt: `45° three-quarter angle view of the exact same character, depth of face and hair, uniform solid ${bgPromptColorEn} background --ar 16:9`,
      count: 2,
    },
    {
      name: 'master_090_side',
      part_id: 'master_character',
      part_name: 'Nhân Vật Gốc Toàn Thân',
      group_id: 'step1_master',
      group_name: 'Bảng Xoay Nhân Vật Gốc',
      angle: '90° Side Profile (Nhìn ngang)',
      angle_id: '090_side',
      angle_deg: 90,
      z_index: 0,
      save_filename: 'master_090_side.png',
      view_desc: 'Góc nhìn ngang 90° thấy sống mũi, cằm, tai và dáng suối tóc',
      prompt: `90° side profile view of the exact same character, clean facial silhouette and spine posture, uniform solid ${bgPromptColorEn} background --ar 16:9`,
      count: 2,
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
      view_desc: 'Góc xoay 135° thấy tà áo sau, thắt lưng và búi tóc',
      prompt: `135° rear three-quarter view of the exact same character, back of hair bun and sash, uniform solid ${bgPromptColorEn} background --ar 16:9`,
      count: 1,
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
      view_desc: 'Góc sau lưng 180° thấy trọn vẹn suối tóc và lưng áo',
      prompt: `180° rear back view of the exact same character, complete back hair mantle and costume spine, uniform solid ${bgPromptColorEn} background --ar 16:9`,
      count: 1,
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
      view_desc: 'Góc phụ soi đỉnh đầu từ trên xuống thấy đường rẽ ngôi và trâm cài',
      prompt: `Top-down view looking downward at the character head crown, hair parting and hair accessories, uniform solid ${bgPromptColorEn} background --ar 16:9`,
      count: 1,
    },
  ];

  const promptJSON = JSON.stringify(
    config.include_base_prompt === false
      ? { prompts: step1Prompts }
      : { base_prompt: baseDescEn, prompts: step1Prompts },
    null,
    2
  );

  const negativePrompt = 'realistic human face, small realistic eyes, 3D CGI render, photorealism, live-action, western comic style, ugly anatomy, deformed face, muddy colors, bad eyes, realistic skin texture, realistic wrinkles, dull eyes, pores, text, letters, words, labels, watermark, signature, bad proportions, divider lines, grid frames';
  const fullCopyText = `${promptEnglish}\n\nNegative prompt:\n${negativePrompt}`;
  const promptGemini = promptVietnamese;

  return {
    promptEnglish,
    promptVietnamese,
    promptJSON,
    promptGemini,
    gridStructureGuide: '📐 Bảng Xoay Nhân Vật: 5 góc toàn thân chuẩn 16:9.',
    negativePrompt,
    fullCopyText,
  };
}
