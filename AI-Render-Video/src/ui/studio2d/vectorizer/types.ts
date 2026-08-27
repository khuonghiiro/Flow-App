export type VectorizerPreset = 'ultra_match' | 'anime' | 'detailed' | 'flat' | 'lineart';

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
    label: '🔥 Khớp Màu 100% (Ultra Match)',
    params: {
      colorPrecision: 8,
      filterSpeckle: 1,
      cornerThreshold: 22,
      lengthThreshold: 1.4,
      layerDifference: 2,
      edgeSmoothing: 1.0,
      colorMode: 'color',
      hierarchical: 'stacked',
    },
  },
  {
    id: 'anime',
    label: '🌸 Anime / Manga',
    params: {
      colorPrecision: 8,
      filterSpeckle: 2,
      cornerThreshold: 28,
      lengthThreshold: 2.0,
      layerDifference: 5,
      edgeSmoothing: 1.5,
      colorMode: 'color',
      hierarchical: 'stacked',
    },
  },
  {
    id: 'detailed',
    label: '🎨 Tranh Chi Tiết',
    params: {
      colorPrecision: 8,
      filterSpeckle: 1,
      cornerThreshold: 24,
      lengthThreshold: 1.5,
      layerDifference: 3,
      edgeSmoothing: 1.0,
      colorMode: 'color',
      hierarchical: 'stacked',
    },
  },
  {
    id: 'flat',
    label: '🖌️ Mảng Phẳng / Logo',
    params: {
      colorPrecision: 6,
      filterSpeckle: 6,
      cornerThreshold: 60,
      lengthThreshold: 4.0,
      layerDifference: 12,
      edgeSmoothing: 2.0,
      colorMode: 'color',
      hierarchical: 'cutout',
    },
  },
  {
    id: 'lineart',
    label: '🖋️ Nét Vẽ Line-Art',
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
