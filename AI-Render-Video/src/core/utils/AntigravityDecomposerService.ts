/**
 * Antigravity Decomposer Service
 * Provides data structures, AI prompts, part decomposition templates,
 * and automated sprite sheet stitching to bridge AI generation with 3D Grid Slicing.
 */

export interface DecomposedPartItem {
  id: string;
  name: string;
  category: 'hair' | 'face' | 'torso' | 'limbs' | 'props';
  slotName: string;
  imageUrl: string;
  zIndex: number;
  selected: boolean;
  notes?: string;
}

export interface CharacterPresetOption {
  id: string;
  name: string;
  style: string;
  thumbnailUrl: string;
  prompt: string;
}

import { generateProceduralAnimeCharacter } from './AntigravityAIImageGenerator';

export const CHARACTER_STYLE_PRESETS: CharacterPresetOption[] = [
  {
    id: 'anime_girl_mage',
    name: 'Nữ Pháp Sư Anime (Anime Girl Mage)',
    style: 'Anime / Fantasy',
    thumbnailUrl: generateProceduralAnimeCharacter({
      prompt: 'anime girl mage, lavender purple hair, mystical staff, sheer cape, blue eyes',
      bgType: 'chroma_green',
    }),
    prompt: 'Masterpiece anime girl mage with long lavender purple hair, mystical glowing staff, flowing sheer chiffon cloak, intricate magical robes, vibrant blue eyes, clean full-body pose on pure solid chroma green background #00FF00, sharp lineart, high detail',
  },
  {
    id: 'cyberpunk_warrior',
    name: 'Chiến Binh Cyberpunk (Cyber Ninja)',
    style: 'Cyberpunk / Sci-Fi',
    thumbnailUrl: generateProceduralAnimeCharacter({
      prompt: 'cyberpunk cyborg ninja, glowing cyan visor, tactical dark armor, red accents',
      bgType: 'chroma_green',
    }),
    prompt: 'Futuristic cyberpunk cyborg ninja, glowing neon cyan visor, high-tech dark tactical armor, mechanical katana, red neon accents, standing front view on pure solid chroma green background #00FF00, anime cel shaded, crisp lineart',
  },
  {
    id: 'chibi_knight_hero',
    name: 'Hiệp Sĩ Hoàng Gia (Golden Paladin)',
    style: 'Chibi / Game RPG',
    thumbnailUrl: generateProceduralAnimeCharacter({
      prompt: 'golden knight hero, blonde yellow hair, golden armor, white cape, blue gem',
      bgType: 'chroma_green',
    }),
    prompt: 'Heroic knight paladin with golden blonde hair, silver and gold armor, ornate sword, royal white cape, brave expressive eyes, clean 2D anime character on pure solid chroma green background #00FF00',
  },
  {
    id: 'fantasy_elf_archer',
    name: 'Phù Thủy Hắc Ám (Dark Sorceress)',
    style: 'High Fantasy',
    thumbnailUrl: generateProceduralAnimeCharacter({
      prompt: 'dark sorceress, crimson red hair, black dress, dark glowing staff, red eyes',
      bgType: 'chroma_green',
    }),
    prompt: 'Elegant dark sorceress with long crimson red hair, dark gothic robes, ornate magical staff, ruby red eyes, heroic standing pose on pure solid chroma green background #00FF00',
  },
];

export interface PartDecompositionTemplate {
  id: string;
  category: 'hair' | 'face' | 'torso' | 'limbs' | 'props';
  name: string;
  slotName: string;
  zIndex: number;
  promptGuidance: string;
}

export const PART_DECOMPOSITION_TEMPLATES: PartDecompositionTemplate[] = [
  // 1. Tóc (Hair)
  {
    id: 'hair_front',
    category: 'hair',
    name: 'Tóc Mái Trước (Front Bangs)',
    slotName: 'hair_front',
    zIndex: 10,
    promptGuidance: 'Isolate front bangs and front hair strands only, maintaining semi-transparent flowing tips on pure chroma green #00FF00',
  },
  {
    id: 'hair_back',
    category: 'hair',
    name: 'Tóc Sau Lưng (Back Hair / Ponytail)',
    slotName: 'hair_back',
    zIndex: 1,
    promptGuidance: 'Isolate back flowing hair and ponytail only, complete silhouette on pure chroma green #00FF00',
  },

  // 2. Khuôn mặt & Biểu cảm (Face & Features)
  {
    id: 'head_base',
    category: 'face',
    name: 'Đầu & Khuôn Mặt Trần (Head / Face Base)',
    slotName: 'head',
    zIndex: 5,
    promptGuidance: 'Isolate head and facial skin base without overlapping hair, clear neck line on pure chroma green #00FF00',
  },
  {
    id: 'eyes_pair',
    category: 'face',
    name: 'Đôi Mắt & Lông Mày (Eyes & Eyebrows)',
    slotName: 'eyes',
    zIndex: 7,
    promptGuidance: 'Isolate anime eyes, pupils with light catchlights, thin eyebrows and eyelid creases on pure chroma green #00FF00',
  },
  {
    id: 'mouth_expression',
    category: 'face',
    name: 'Miệng & Nụ Cười (Mouth Expression)',
    slotName: 'mouth',
    zIndex: 6,
    promptGuidance: 'Isolate mouth smile and facial blush expression on pure chroma green #00FF00',
  },

  // 3. Thân & Trang Phục (Torso & Clothing)
  {
    id: 'torso_body',
    category: 'torso',
    name: 'Thân Mình & Áo Trong (Torso Base)',
    slotName: 'torso',
    zIndex: 3,
    promptGuidance: 'Isolate upper torso, shirt and inner chest clothing on pure chroma green #00FF00',
  },
  {
    id: 'outer_jacket',
    category: 'torso',
    name: 'Áo Khoác / Giáp Ngực (Jacket / Armor)',
    slotName: 'armor',
    zIndex: 4,
    promptGuidance: 'Isolate outer coat, jacket, armor plates and collar on pure chroma green #00FF00',
  },
  {
    id: 'skirt_pants',
    category: 'torso',
    name: 'Váy / Quần (Skirt / Pants / Belt)',
    slotName: 'pelvis',
    zIndex: 3,
    promptGuidance: 'Isolate lower waist, belt, pleated skirt or pants on pure chroma green #00FF00',
  },

  // 4. Tứ Chi (Limbs & Hands)
  {
    id: 'arm_left',
    category: 'limbs',
    name: 'Cánh Tay Trái (Left Arm & Hand)',
    slotName: 'arm_left',
    zIndex: 8,
    promptGuidance: 'Isolate left shoulder, upper arm, forearm and posed hand on pure chroma green #00FF00',
  },
  {
    id: 'arm_right',
    category: 'limbs',
    name: 'Cánh Tay Phải (Right Arm & Hand)',
    slotName: 'arm_right',
    zIndex: 2,
    promptGuidance: 'Isolate right arm and hand in holding or action pose on pure chroma green #00FF00',
  },
  {
    id: 'legs_pair',
    category: 'limbs',
    name: 'Đôi Chân & Giày (Legs & Boots)',
    slotName: 'legs',
    zIndex: 2,
    promptGuidance: 'Isolate thighs, calves, boots and shoes standing firmly on pure chroma green #00FF00',
  },

  // 5. Phụ kiện & Vũ khí (Props & VFX)
  {
    id: 'flowing_cape',
    category: 'props',
    name: 'Áo Choàng / Dải Lụa Bay (Flowing Cape / Silk)',
    slotName: 'cape',
    zIndex: 0,
    promptGuidance: 'Isolate ethereal flowing sheer cape and ribbon with soft translucent gradient on pure chroma green #00FF00',
  },
  {
    id: 'main_weapon',
    category: 'props',
    name: 'Vũ Khí / Pháp Trượng (Weapon / Staff / Sword)',
    slotName: 'weapon',
    zIndex: 9,
    promptGuidance: 'Isolate glowing magical weapon or sword with sparkle effects on pure chroma green #00FF00',
  },
];

/**
 * Automatically stitches individual decomposed parts into a uniform Sprite Sheet Grid Canvas
 * Returns the composite Data URL and recommended grid configuration (cols, rows, category ID)
 */
export async function stitchDecomposedPartsToSpriteSheet(
  parts: DecomposedPartItem[],
  targetCols: number = 3
): Promise<{
  spriteSheetDataUrl: string;
  cols: number;
  rows: number;
  totalWidth: number;
  totalHeight: number;
  cellWidth: number;
  cellHeight: number;
}> {
  if (parts.length === 0) {
    throw new Error('Không có linh kiện nào để ghép thành Sprite Sheet.');
  }

  // Load all images asynchronously
  const loadedImages = await Promise.all(
    parts.map(
      (part) =>
        new Promise<{ part: DecomposedPartItem; img: HTMLImageElement }>((resolve, reject) => {
          const img = new Image();
          img.crossOrigin = 'anonymous';
          img.onload = () => resolve({ part, img });
          img.onerror = () => reject(new Error(`Không thể nạp ảnh linh kiện: ${part.name}`));
          img.src = part.imageUrl;
        })
    )
  );

  const cellWidth = 384;
  const cellHeight = 384;
  const cols = Math.min(targetCols, parts.length);
  const rows = Math.ceil(parts.length / cols);
  const totalWidth = cols * cellWidth;
  const totalHeight = rows * cellHeight;

  const canvas = document.createElement('canvas');
  canvas.width = totalWidth;
  canvas.height = totalHeight;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Không thể khởi tạo 2D Canvas context');

  // Fill background with standard Chroma Green for unified downstream matting
  ctx.fillStyle = '#00FF00';
  ctx.fillRect(0, 0, totalWidth, totalHeight);

  // Place each part inside its grid cell
  loadedImages.forEach(({ img }, index) => {
    const col = index % cols;
    const row = Math.floor(index / cols);
    const x = col * cellWidth;
    const y = row * cellHeight;

    // Draw part image centered within cell
    const scale = Math.min((cellWidth - 16) / img.width, (cellHeight - 16) / img.height);
    const drawW = img.width * scale;
    const drawH = img.height * scale;
    const drawX = x + (cellWidth - drawW) / 2;
    const drawY = y + (cellHeight - drawH) / 2;

    ctx.drawImage(img, drawX, drawY, drawW, drawH);
  });

  return {
    spriteSheetDataUrl: canvas.toDataURL('image/png'),
    cols,
    rows,
    totalWidth,
    totalHeight,
    cellWidth,
    cellHeight,
  };
}
