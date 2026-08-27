export type VectorizerPreset = 'ultra_match' | 'svg_ai' | 'anime' | 'flat' | 'lineart';

export type VectorizerViewMode = 'side_by_side' | 'split' | 'svg_only' | 'raster_only';

export interface VectorizerParams {
  colorPrecision: number;
  filterSpeckle: number;
  cornerThreshold: number;
  lengthThreshold: number;
  layerDifference: number;
  edgeSmoothing: number;
  colorMode: 'color' | 'binary';
  hierarchical: 'stacked' | 'cutout';
}

export interface VectorizerMetaStats {
  pathCount: number;
  sizeKb: number;
  timeMs: number;
  engine?: string;
}

export interface SampleImageItem {
  label: string;
  url: string;
}

export interface ImageToSvgVectorizerTabProps {
  onTransferToRigAssembler?: (svgUrl: string) => void;
  onTransferToGridSlicer?: (svgUrl: string) => void;
}

export const VECTORIZER_PRESETS: { id: VectorizerPreset; label: string; params: VectorizerParams }[] = [
  {
    id: 'ultra_match',
    label: '🔥 Khớp Chi Tiết 100% (High-Fidelity)',
    params: {
      colorPrecision: 8,
      filterSpeckle: 2,
      cornerThreshold: 28,
      lengthThreshold: 1.6,
      layerDifference: 6,
      edgeSmoothing: 0.0,
      colorMode: 'color',
      hierarchical: 'stacked',
    },
  },
  {
    id: 'svg_ai',
    label: '✨ AI Vector Art Mịn & Gọn (SVG AI ~80KB)',
    params: {
      colorPrecision: 6,
      filterSpeckle: 4,
      cornerThreshold: 45,
      lengthThreshold: 2.5,
      layerDifference: 14,
      edgeSmoothing: 0.8,
      colorMode: 'color',
      hierarchical: 'stacked',
    },
  },
  {
    id: 'anime',
    label: '🌸 Anime / Nhân Vật 2D',
    params: {
      colorPrecision: 7,
      filterSpeckle: 3,
      cornerThreshold: 35,
      lengthThreshold: 2.0,
      layerDifference: 8,
      edgeSmoothing: 0.5,
      colorMode: 'color',
      hierarchical: 'stacked',
    },
  },
  {
    id: 'flat',
    label: '💎 Siêu Nhỏ Gọn (Logo / Icon ~20KB)',
    params: {
      colorPrecision: 5,
      filterSpeckle: 10,
      cornerThreshold: 65,
      lengthThreshold: 4.5,
      layerDifference: 24,
      edgeSmoothing: 1.5,
      colorMode: 'color',
      hierarchical: 'stacked',
    },
  },
  {
    id: 'lineart',
    label: '🖋️ Nét Vẽ Line-Art Đơn Sắc',
    params: {
      colorPrecision: 2,
      filterSpeckle: 4,
      cornerThreshold: 35,
      lengthThreshold: 2.5,
      layerDifference: 16,
      edgeSmoothing: 1.5,
      colorMode: 'binary',
      hierarchical: 'stacked',
    },
  },
];

export const SAMPLE_IMAGES: SampleImageItem[] = [
  { label: '🖐️ Bàn Tay Xòe Anime', url: '/demo_rig/hand_000_front.jpg' },
  { label: '✊ Nắm Tay Anime', url: '/demo_rig/hand_045_three_quarter.jpg' },
  { label: '✌️ Chữ V Anime', url: '/demo_rig/hand_090_profile.jpg' },
  { label: '👤 Sau Lưng Anime', url: '/demo_rig/hand_180_back.jpg' },
];
