// ─── AI Prompt Result & UI Label Localization Helpers ──────────────────
export interface AIPromptResult {
  promptEnglish: string;
  promptVietnamese: string;
  promptJSON: string;
  promptGemini: string;
  gridStructureGuide: string;
  negativePrompt: string;
  fullCopyText: string;
}

export const getSheetTypeLabel = (s: string) => {
  switch (s) {
    case 'hair_multi_angle_grid': return 'Bảng Tóc Đa Góc (4x5)';
    case 'eyes_grid': return 'Bảng Trạng Thái Mắt & Lông Mày';
    case 'mouth_grid': return 'Bảng Khẩu Hình Miệng Lip-sync';
    case 'nose_chin_grid': return 'Bảng Sống Mũi, Cằm & Tai';
    case 'costume_grid': return 'Bảng Đạo Bào / Trang Phục Rỗng';
    case 'weapons_grid': return 'Bảng Vũ Khí & Pháp Bảo';
    case 'limbs_hands_grid': return 'Bảng Tứ Chi & Bắt Quyết';
    case 'body_turnaround_grid': return 'Bảng Nhân Vật Hoàn Chỉnh';
    case 'step1_master_character': return 'Bảng Thiết Kế Nhân Vật (Master Turnaround Sheet)';
    default: return 'Linh Kiện Đơn Lẻ';
  }
};

export const getStyleLabel = (s: string, custom?: string) => {
  if (custom?.trim()) return custom.trim();
  if (!s) return '🌸 Anime Nhật Bản Chuẩn Đẹp';
  switch (s) {
    case 'anime_japan': return '🌸 Anime Nhật Bản Chuẩn Đẹp (Mắt to, sắc nét)';
    case 'auto_detect': return '🤖 AI Tự Hiểu & Sáng Tạo (Anime / Tự Nhiên)';
    case 'chibi': return '🌟 Anime Chibi Đáng Yêu (Đầu to mắt to 2.5D)';
    case 'custom': return custom?.trim() || 'Phong cách tùy chỉnh';
    default: return s;
  }
};

export const getGenderLabel = (g: string) => {
  return g === 'nam' ? 'Nam' : g === 'nu' ? 'Nữ' : 'Chung';
};

export const getEyeShapeLabels = (shape?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!shape) return { vi: 'Mắt anime to tròn sắc nét', en: 'large clear beautiful anime eyes' };
  switch (shape) {
    case 'chibi_sparkling_starry': return { vi: 'Mắt tròn to long lanh ánh sao (Anime)', en: 'giant sparkling round starry anime eyes with glossy highlights' };
    case 'chibi_happy_crescent': return { vi: 'Mắt cười híp cong lưỡi liềm', en: 'happy joyful crescent-shaped anime curved wink eyes' };
    case 'chibi_pouty_teary': return { vi: 'Mắt ươn ướt cún con dễ thương', en: 'cute puppy teary glossy anime eyes' };
    case 'large_clear': return { vi: 'Mắt to tròn trong sáng tinh anh (Anime)', en: 'large beautiful expressive anime eyes with clean pupil highlights' };
    case 'sharp_phoenix': return { vi: 'Mắt phượng sắc sảo cuốn hút', en: 'sharp defined anime eyes with prominent eyelashes' };
    case 'cold_swordsman': return { vi: 'Mắt kiếm khách kiên định lạnh lùng', en: 'cold determined swordsman anime eyes' };
    case 'fox_alluring': return { vi: 'Mắt hồ ly quyến rũ ma mị', en: 'alluring sleek anime eyes' };
    default: return { vi: shape, en: shape };
  }
};

export const getEyeColorLabels = (col?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!col) return { vi: 'Xanh lam ngọc phát sáng', en: 'azure cyan glowing iris' };
  switch (col) {
    case 'azure_blue': return { vi: 'Xanh lam ngọc phát sáng', en: 'glowing azure cyan iris' };
    case 'chibi_sweet_pink': return { vi: 'Hồng dâu tây kẹo ngọt', en: 'sweet strawberry pink iris' };
    case 'golden_amber': return { vi: 'Vàng mật ong hổ phách', en: 'golden amber glowing iris' };
    case 'emerald_green': return { vi: 'Xanh ngọc lục bảo', en: 'emerald green vibrant iris' };
    case 'crimson_red': return { vi: 'Đỏ rực hỏa diệm', en: 'crimson red fiery iris' };
    case 'mystic_purple': return { vi: 'Tím tử linh huyền ảo', en: 'mystic violet purple iris' };
    case 'obsidian_black': return { vi: 'Đen tuyền sâu thẳm', en: 'obsidian black deep glossy iris' };
    default: return { vi: col, en: col };
  }
};

export const getNoseLabels = (nose?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!nose) return { vi: 'Sống mũi thẳng cao thanh tú', en: 'tall straight delicate anime nose bridge' };
  switch (nose) {
    case 'chibi_tiny_dot': return { vi: 'Mũi chấm nhỏ xíu anime', en: 'cute microscopic dot anime nose' };
    case 'chibi_no_nose': return { vi: 'Ẩn mũi (Anime không vẽ mũi)', en: 'classic anime style with no visible nose' };
    case 'small_delicate': return { vi: 'Mũi nhỏ nhắn thanh tú', en: 'small delicate anime nose' };
    case 'sharp_defined': return { vi: 'Sống mũi cao sắc nét', en: 'sharp refined anime nose bridge' };
    case 'straight_high_bridge': return { vi: 'Sống mũi thẳng cao thanh tú', en: 'tall straight delicate anime nose bridge' };
    default: return { vi: nose, en: nose };
  }
};

export const getMouthLabels = (mouth?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!mouth) return { vi: 'Cười nhếch môi tự tin', en: 'confident subtle smirk' };
  switch (mouth) {
    case 'chibi_cat_mouth': return { vi: 'Miệng mèo tinh nghịch :3', en: 'cute playful cat-like :3 smile mouth' };
    case 'chibi_surprised_o': return { vi: 'Miệng chữ O ngơ ngác đáng yêu', en: 'cute open surprised circle O-mouth' };
    case 'chibi_puffed_cheek': return { vi: 'Phồng má ngậm bánh bao dễ thương', en: 'cute chubby puffed cheeks with pouty mouth' };
    case 'chibi_big_smile': return { vi: 'Cười tít mắt hớn hở', en: 'big joyful open happy anime smile' };
    case 'confident_smirk': return { vi: 'Cười nhếch môi tự tin', en: 'confident subtle smirk' };
    case 'gentle_smile': return { vi: 'Nụ cười dịu dàng tươi tắn', en: 'gentle serene sweet anime smile' };
    case 'battle_roar': return { vi: 'Nghiêm nghị tập trung chiến đấu', en: 'determined focused expression' };
    default: return { vi: mouth, en: mouth };
  }
};

export const getCostumeLabels = (costume?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!costume) return { vi: 'Đạo bào tu tiên thướt tha cách tân', en: 'stylish anime costume with flowing sleeves' };
  switch (costume) {
    case 'dao_bao_tien_hiep': return { vi: 'Đạo bào tu tiên thướt tha cách tân', en: 'stylish modern anime costume with flowing sleeves and ornamental trim' };
    case 'bach_y_tien_tu': return { vi: 'Bạch y thanh khiết xếp ly thướt tha', en: 'pure white elegant flowing anime robes with delicate ribbons' };
    case 'kiem_khach_ao_vai': return { vi: 'Trang phục kiếm khách lãng tử phong trần', en: 'stylish anime wanderer adventurer outfit with leather accents' };
    case 'hac_y_ma_dao': return { vi: 'Hắc y huyền bí viền đỏ quyền lực', en: 'dark stylish anime coat with crimson flame trim' };
    case 'hoang_toc_kim_bao': return { vi: 'Kim bào hoàng gia thêu chỉ vàng quý phái', en: 'imperial golden embroidered anime coat with luxury details' };
    default: return { vi: costume, en: costume };
  }
};

export const getPropLabels = (prop?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!prop) return { vi: 'Phi kiếm phát sáng linh lực lam ngọc', en: 'spirit sword glowing with azure energy' };
  switch (prop) {
    case 'flying_sword': return { vi: 'Phi kiếm phát sáng linh lực lam ngọc', en: 'spirit sword glowing with azure energy' };
    case 'feather_fan': return { vi: 'Quạt lông vũ thái cực tiên gia', en: 'celestial feather fan' };
    case 'talisman_scrolls': return { vi: 'Cuộn bùa chú phù lục phát quang', en: 'glowing spiritual talisman scrolls' };
    case 'gourd_wine': return { vi: 'Hồ lô tiên tửu ngọc bích', en: 'celestial wine gourd' };
    case 'jade_hairpin': return { vi: 'Trâm cài ngọc bích đính dải lụa', en: 'carved jade hairpin with silk ribbons' };
    default: return { vi: prop, en: prop };
  }
};

export const getHairLengthLabels = (len?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!len) return { vi: 'Tóc dài ngang lưng suôn mượt', en: 'long waist-length flowing hair' };
  switch (len) {
    case 'short': return { vi: 'Tóc ngắn cá tính năng động', en: 'stylish short anime layered hair' };
    case 'medium_shoulder': return { vi: 'Tóc ngang vai tỉa tầng bồng bềnh', en: 'shoulder-length medium layered anime hair' };
    case 'very_long_flowing': return { vi: 'Tóc dài thướt tha chấm gót', en: 'floor-length flowing hair cascading down' };
    case 'top_knot_daoist': return { vi: 'Búi tóc củ tỏi đỉnh đầu cài trâm', en: 'traditional high top-knot bun with hairpin' };
    case 'long_waist': return { vi: 'Tóc dài ngang lưng suôn mượt', en: 'long waist-length flowing hair' };
    default: return { vi: len, en: len };
  }
};

export const getHairColorLabels = (col?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!col) return { vi: 'Đen tuyền óng ả', en: 'jet black silky' };
  switch (col) {
    case 'jet_black': return { vi: 'Đen tuyền óng ả', en: 'jet black silky' };
    case 'silver_white': return { vi: 'Bạch kim (Trắng bạc lấp lánh)', en: 'silver white luminous' };
    case 'crimson_red': return { vi: 'Đỏ rực hỏa diệm', en: 'fiery crimson red' };
    case 'azure_blue': return { vi: 'Xanh lam ngọc phát sáng', en: 'glowing azure cyan' };
    case 'golden_blonde': return { vi: 'Vàng kim ánh dương', en: 'golden blonde radiant' };
    case 'mystic_purple': return { vi: 'Tím tử linh huyền bí', en: 'mystic violet purple' };
    default: return { vi: col, en: col };
  }
};

export const getHairTextureLabels = (tex?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!tex) return { vi: 'Thẳng mượt như suối lụa', en: 'straight silky smooth strands' };
  switch (tex) {
    case 'wavy_curls': return { vi: 'Xoăn sóng bồng bềnh lượn lờ', en: 'wavy voluminous locks' };
    case 'wild_spiky': return { vi: 'Đánh rối tỉa nhọn năng động', en: 'action spiky locks' };
    case 'braided_traditional': return { vi: 'Tết bím cầu kỳ cách tân', en: 'traditional braided anime strands' };
    case 'straight_silky': return { vi: 'Thẳng mượt như suối lụa', en: 'straight silky smooth strands' };
    default: return { vi: tex, en: tex };
  }
};

export const getHairAccessoryLabels = (acc?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!acc) return { vi: 'Trâm cài ngọc bích đính dải lụa', en: 'carved jade hairpin with delicate silk ribbons' };
  switch (acc) {
    case 'jade_hairpin': return { vi: 'Trâm cài ngọc bích đính dải lụa', en: 'carved jade hairpin with delicate silk ribbons' };
    case 'golden_crown': return { vi: 'Vương miện vàng kim tinh xảo', en: 'ornate golden crown' };
    case 'flowing_ribbons': return { vi: 'Dải lụa mềm bay phất phơ', en: 'fluttering silk ribbons' };
    case 'none': return { vi: 'Không có phụ kiện', en: 'none' };
    default: return { vi: acc, en: acc };
  }
};

export const getBodyProportionLabels = (prop?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!prop) return { vi: 'Chibi đáng yêu 2.5 đầu, đầu to thân nhỏ gọn gàng', en: 'Cute chibi proportions, large head, short compact body, 2.5 heads tall, small hands and feet' };
  switch (prop) {
    case 'chibi_2_5': return { vi: 'Chibi đáng yêu 2.5 đầu, đầu to thân nhỏ gọn gàng', en: 'Cute chibi proportions, large head, short compact body, 2.5 heads tall, small hands and feet' };
    case 'chibi_2_heads': return { vi: 'Super Chibi 2 đầu kawaii', en: 'Super kawaii chibi 2 heads tall, tiny body, giant head' };
    case 'anime_standard': return { vi: 'Anime tiêu chuẩn 6-7 đầu, thon thả thanh thoát', en: 'Standard 2D anime proportions, 6 to 7 heads tall, slender elegant body, long limbs' };
    case 'heroic_martial': return { vi: 'Hiệp khách oai phong 7.5 đầu', en: 'Heroic martial cultivator proportions, 7.5 heads tall, athletic powerful build' };
    default: return { vi: prop, en: prop };
  }
};

export const getBangsStyleLabels = (bangs?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!bangs) return { vi: 'Mái thưa tỉa lớp thanh thoát (See-through Bangs)', en: 'see-through delicate airy anime bangs' };
  switch (bangs) {
    case 'see_through_airy': return { vi: 'Mái thưa tỉa lớp thanh thoát (See-through Bangs)', en: 'see-through delicate airy anime bangs ending at eyebrow level' };
    case 'blunt_straight': return { vi: 'Mái bằng ngang trán (Blunt Straight Bangs)', en: 'straight cut blunt horizontal anime bangs covering forehead' };
    case 'side_swept_7_3': return { vi: 'Mái xéo rẽ ngôi 7/3 (Side-Swept Bangs 7/3)', en: 'side-swept anime bangs parted 7/3 sweeping across forehead' };
    case 'curtain_parted_5_5': return { vi: 'Mái rẽ đôi ngôi giữa 5/5 (Curtain Center-Parted Bangs)', en: 'curtain center-parted anime bangs framing both temples' };
    case 'hime_cut_tendrils': return { vi: 'Mái Hime hai lọn dài ôm má (Hime Cut & Side Tendrils)', en: 'straight blunt front bangs with long straight hime-cut side tendrils framing cheeks' };
    case 'spiky_action': return { vi: 'Mái nhọn so le lộn xộn năng động (Spiky Action Bangs)', en: 'spiky jagged layered anime front bangs with sharp tips' };
    default: return { vi: bangs, en: bangs };
  }
};

export const getSkinToneLabels = (tone?: string, custom?: string) => {
  if (custom?.trim()) return { vi: custom.trim(), en: custom.trim() };
  if (!tone) {
    return {
      vi: '🌸 Da Anime Trắng Hồng Sứ (Fair porcelain, rosy-pink)',
      en: 'fair porcelain anime skin tone, soft rosy-pink undertone, pale peachy-white complexion with subtle pink blush shading at cheeks and joints, smooth cel-shaded soft gradient anime skin (no realistic pores/texture)',
    };
  }
  switch (tone) {
    case 'fair_porcelain_pink':
      return {
        vi: '🌸 Da Anime Trắng Hồng Sứ (Fair porcelain, rosy-pink)',
        en: 'fair porcelain anime skin tone, soft rosy-pink undertone, pale peachy-white complexion with subtle pink blush shading at cheeks and joints, smooth cel-shaded soft gradient anime skin (no realistic pores/texture)',
      };
    case 'porcelain_white':
      return {
        vi: '⚪ Da Trắng Sứ BJD (Pure porcelain white)',
        en: 'pure porcelain white bjd mannequin skin, smooth milky ivory tone, clean cel-shaded anime skin',
      };
    case 'warm_peach':
      return {
        vi: '🍑 Da Đào Tự Nhiên (Warm peach / beige)',
        en: 'warm light peach beige anime skin tone, natural soft warmth, smooth cel-shaded anime skin',
      };
    case 'tan_sunkissed':
      return {
        vi: '🍫 Da Ngăm Khỏe Khoắn (Tan / sun-kissed)',
        en: 'healthy tan sun-kissed warm bronze anime skin tone, athletic radiant glow, smooth cel-shaded anime skin',
      };
    default:
      return { vi: tone, en: tone };
  }
};

