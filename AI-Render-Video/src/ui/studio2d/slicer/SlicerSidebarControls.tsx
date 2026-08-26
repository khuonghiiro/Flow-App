import React from 'react';
import { Scissors, Sparkles, RefreshCw } from 'lucide-react';
import { GridCategoryDefinition } from '../../../core/assets/GridSliceRegistry';
import { Character2DAngle, Character2DPartType } from '../../../types/scene2d';
import { ChromaProcessOptions } from '../../../core/utils/ChromaDespeckleProcessor';

// Modularized Sub-Cards
import { SlicerSourceImageCard } from './sidebar/SlicerSourceImageCard';
import { SlicerChromaKeyCard } from './sidebar/SlicerChromaKeyCard';
import { SlicerDespeckleDefringeCard } from './sidebar/SlicerDespeckleDefringeCard';
import { SlicerAIMattingCard } from './sidebar/SlicerAIMattingCard';
import { SlicerAssemblyActionCard } from './sidebar/SlicerAssemblyActionCard';

export interface SlicerSidebarControlsProps {
  targetCategory?: string;
  onSelectTargetCategory?: (cat: string) => void;
  selectedCatId: string;
  onSelectCatId: (id: string) => void;
  customCategory?: GridCategoryDefinition | null;
  singleImageAngle?: Character2DAngle;
  onUpdateSingleImageAngle?: (angle: Character2DAngle) => void;
  singleImageSlot?: Character2DPartType;
  onUpdateSingleImageSlot?: (slot: Character2DPartType) => void;
  onAutoDetectAngleFromFilename?: () => void;
  onOpenJsonImportModal?: () => void;
  userUploadedImageUrl: string | null;
  totalLoadedCount?: number;
  onFileUpload: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onClearImage?: () => void;
  fileInputRef: React.RefObject<HTMLInputElement>;

  isEyedropperActive?: boolean;
  setIsEyedropperActive?: (active: boolean) => void;
  keyColorType: 'chroma_green' | 'pure_white' | 'custom';
  setKeyColorType: (type: 'chroma_green' | 'pure_white' | 'custom') => void;
  keyColorHex: string;
  setKeyColorHex: (hex: string) => void;
  isolationMode: 'all' | 'outer_only';
  setIsolationMode: (mode: 'all' | 'outer_only') => void;
  tolerance: number;
  setTolerance: (val: number) => void;
  feather: number;
  setFeather: (val: number) => void;
  shadowRetention: number;
  setShadowRetention: (val: number) => void;
  strokeWidth: number;
  setStrokeWidth: (w: number) => void;
  strokeColorHex: string;
  setStrokeColorHex: (c: string) => void;

  bgCleanupSubTab: 'chroma' | 'despeckle' | 'ai_matting';
  setBgCleanupSubTab: (tab: 'chroma' | 'despeckle' | 'ai_matting') => void;
  aiModel: string;
  setAiModel: (m: string) => void;
  aiScope: 'full_image' | 'all' | 'selected';
  setAiScope: (s: 'full_image' | 'all' | 'selected') => void;
  aiServerStatus: 'online' | 'offline' | 'checking';
  onRunAIMatting: () => void;
  isAIRunning: boolean;

  despeckleSize: number;
  setDespeckleSize: (val: number) => void;
  whiteSpeckleSensitivity: number;
  setWhiteSpeckleSensitivity: (val: number) => void;
  keepLargestIslandOnly: boolean;
  setKeepLargestIslandOnly: (val: boolean) => void;
  eyedropperTarget: 'chroma' | 'fringe' | 'smooth';
  setEyedropperTarget: (t: 'chroma' | 'fringe' | 'smooth') => void;
  cleanupMode: 'all' | 'defringe' | 'smooth' | 'despeckle';
  setCleanupMode: (m: 'all' | 'defringe' | 'smooth' | 'despeckle') => void;
  fringeColorType: 'chroma_green' | 'pure_white' | 'pure_black' | 'custom';
  setFringeColorType: (t: 'chroma_green' | 'pure_white' | 'pure_black' | 'custom') => void;
  fringeColorHex: string;
  setFringeColorHex: (hex: string) => void;
  defringeStrength: number;
  setDefringeStrength: (val: number) => void;
  edgeChoke: number;
  setEdgeChoke: (val: number) => void;
  edgeSmooth: number;
  setEdgeSmooth: (val: number) => void;
  smoothColorType: 'black' | 'white' | 'auto' | 'custom';
  setSmoothColorType: (t: 'black' | 'white' | 'auto' | 'custom') => void;
  smoothColorHex: string;
  setSmoothColorHex: (hex: string) => void;
  onRunDespeckleOnly?: () => void;
  onApplyAsNewBaseImage?: () => void;

  paddingInset: number;
  setPaddingInset: (inset: number) => void;
  enableSmartCrop?: boolean;
  setEnableSmartCrop?: (enable: boolean) => void;
  smartCropPadding?: number;
  setSmartCropPadding?: (pad: number) => void;
  isProcessing: boolean;
  assemblySuccess: boolean;
  onAutoSliceAndAssemble: () => void;
  onCommitSliderChange?: (overrides?: Partial<ChromaProcessOptions>) => void;
  slicedCount: number;
  totalCellCount: number;
  onOpenSaveKitModal?: () => void;
  onOpenCatalogModal?: () => void;
  checkedCount?: number;
  onBatchSeparateChecked?: () => Promise<void>;
  isBatchProcessing?: boolean;
}

export const SlicerSidebarControls: React.FC<SlicerSidebarControlsProps> = ({
  targetCategory = 'character',
  onSelectTargetCategory,
  selectedCatId,
  onSelectCatId,
  customCategory,
  singleImageAngle,
  onUpdateSingleImageAngle,
  singleImageSlot,
  onUpdateSingleImageSlot,
  onAutoDetectAngleFromFilename,
  onOpenJsonImportModal,
  userUploadedImageUrl,
  totalLoadedCount,
  onFileUpload,
  onClearImage,
  fileInputRef,

  isEyedropperActive,
  setIsEyedropperActive,
  keyColorType,
  setKeyColorType,
  keyColorHex,
  setKeyColorHex,
  isolationMode,
  setIsolationMode,
  tolerance,
  setTolerance,
  feather,
  setFeather,
  shadowRetention,
  setShadowRetention,
  strokeWidth,
  setStrokeWidth,
  strokeColorHex,
  setStrokeColorHex,

  bgCleanupSubTab,
  setBgCleanupSubTab,
  aiModel,
  setAiModel,
  aiScope,
  setAiScope,
  aiServerStatus,
  onRunAIMatting,
  isAIRunning,

  despeckleSize,
  setDespeckleSize,
  whiteSpeckleSensitivity,
  setWhiteSpeckleSensitivity,
  keepLargestIslandOnly,
  setKeepLargestIslandOnly,
  eyedropperTarget,
  setEyedropperTarget,
  cleanupMode,
  setCleanupMode,
  fringeColorType,
  setFringeColorType,
  fringeColorHex,
  setFringeColorHex,
  defringeStrength,
  setDefringeStrength,
  edgeChoke,
  setEdgeChoke,
  edgeSmooth,
  setEdgeSmooth,
  smoothColorType,
  setSmoothColorType,
  smoothColorHex,
  setSmoothColorHex,
  onRunDespeckleOnly,
  onApplyAsNewBaseImage,

  paddingInset,
  setPaddingInset,
  enableSmartCrop,
  setEnableSmartCrop,
  smartCropPadding,
  setSmartCropPadding,
  isProcessing,
  assemblySuccess,
  onAutoSliceAndAssemble,
  onCommitSliderChange,
  slicedCount,
  totalCellCount,
  onOpenSaveKitModal,
  onOpenCatalogModal,
  checkedCount = 0,
  onBatchSeparateChecked,
  isBatchProcessing = false,
}) => {
  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        overflowY: 'auto',
        paddingRight: 4,
        minHeight: 0,
        fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
      }}
    >
      {/* CARD 1: NGUỒN ẢNH & KHUNG LƯỚI */}
      <SlicerSourceImageCard
        targetCategory={targetCategory}
        onSelectTargetCategory={onSelectTargetCategory}
        isSingleImageMode={selectedCatId === 'single_full_image'}
        singleImageAngle={singleImageAngle}
        onUpdateSingleImageAngle={onUpdateSingleImageAngle}
        singleImageSlot={singleImageSlot}
        onUpdateSingleImageSlot={onUpdateSingleImageSlot}
        onAutoDetectAngleFromFilename={onAutoDetectAngleFromFilename}
        onOpenJsonImportModal={onOpenJsonImportModal}
        userUploadedImageUrl={userUploadedImageUrl}
        totalLoadedCount={totalLoadedCount}
        onFileUpload={onFileUpload}
        onClearImage={onClearImage}
        fileInputRef={fileInputRef}
      />

      {/* Batch info: compact single-line when checked > 0 */}
      {checkedCount > 0 && (
        <div style={{
          background: 'rgba(124, 58, 237, 0.12)',
          borderRadius: 6,
          border: '1px solid rgba(124, 58, 237, 0.3)',
          padding: '5px 8px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}>
          <span style={{ fontSize: 10, fontWeight: 700, color: '#c084fc' }}>
            🎯 {checkedCount} ảnh đã chọn
          </span>
          <span style={{ fontSize: 8, color: '#94a3b8', fontStyle: 'italic' }}>
            Áp dụng cho tất cả
          </span>
        </div>
      )}

      {/* CARD 2: CHẾ ĐỘ TÁCH NỀN & BỘ LỌC */}
      <div
        style={{
          background: 'linear-gradient(180deg, rgba(24, 34, 53, 0.7) 0%, rgba(15, 23, 42, 0.8) 100%)',
          borderRadius: 8,
          border: '1px solid rgba(34, 197, 94, 0.25)',
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 8,
          boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderBottom: '1px solid rgba(255,255,255,0.06)', paddingBottom: 6 }}>
          <div style={{ fontSize: 11.5, fontWeight: 700, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 6, letterSpacing: '0.2px' }}>
            <Scissors size={14} color="#4ade80" /> 2. Chế độ bóc tách nền
          </div>
        </div>

        {/* 3 Sub-tabs Bar */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1.15fr', gap: 5, padding: 3, background: '#090d16', borderRadius: 8, border: '1px solid rgba(255,255,255,0.1)' }}>
          <button
            onClick={() => setBgCleanupSubTab('chroma')}
            style={{
              height: 30,
              fontSize: 10.5,
              fontWeight: 600,
              borderRadius: 6,
              border: bgCleanupSubTab === 'chroma' ? '1.5px solid #0284c7' : '1px solid transparent',
              background: bgCleanupSubTab === 'chroma' ? 'linear-gradient(135deg, rgba(2,132,199,0.35), rgba(14,165,233,0.15))' : 'transparent',
              color: bgCleanupSubTab === 'chroma' ? '#38bdf8' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
            }}
          >
            🎨 1. Tách màu
          </button>
          <button
            onClick={() => setBgCleanupSubTab('despeckle')}
            style={{
              height: 30,
              fontSize: 10.5,
              fontWeight: 600,
              borderRadius: 6,
              border: bgCleanupSubTab === 'despeckle' ? '1.5px solid #10b981' : '1px solid transparent',
              background: bgCleanupSubTab === 'despeckle' ? 'linear-gradient(135deg, rgba(16,185,129,0.35), rgba(52,211,153,0.15))' : 'transparent',
              color: bgCleanupSubTab === 'despeckle' ? '#4ade80' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
            }}
          >
            🧹 2. Khử rác
          </button>
          <button
            onClick={() => setBgCleanupSubTab('ai_matting')}
            style={{
              height: 30,
              fontSize: 10.5,
              fontWeight: 600,
              borderRadius: 6,
              border: bgCleanupSubTab === 'ai_matting' ? '1.5px solid #c084fc' : '1px solid transparent',
              background: bgCleanupSubTab === 'ai_matting' ? 'linear-gradient(135deg, rgba(147,51,234,0.4), rgba(217,70,239,0.2))' : 'transparent',
              color: bgCleanupSubTab === 'ai_matting' ? '#f0abfc' : '#c084fc',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
            }}
          >
            🤖 3. AI Matting
          </button>
        </div>

        {/* Tab 1 */}
        {bgCleanupSubTab === 'chroma' && (
          <SlicerChromaKeyCard
            keyColorType={keyColorType}
            setKeyColorType={setKeyColorType}
            keyColorHex={keyColorHex}
            setKeyColorHex={setKeyColorHex}
            isEyedropperActive={isEyedropperActive}
            setIsEyedropperActive={setIsEyedropperActive}
            eyedropperTarget={eyedropperTarget}
            setEyedropperTarget={setEyedropperTarget}
            isolationMode={isolationMode}
            setIsolationMode={setIsolationMode}
            tolerance={tolerance}
            setTolerance={setTolerance}
            feather={feather}
            setFeather={setFeather}
            shadowRetention={shadowRetention}
            setShadowRetention={setShadowRetention}
            strokeWidth={strokeWidth}
            setStrokeWidth={setStrokeWidth}
            strokeColorHex={strokeColorHex}
            setStrokeColorHex={setStrokeColorHex}
            onCommitSliderChange={onCommitSliderChange}
          />
        )}

        {/* Tab 2 */}
        {bgCleanupSubTab === 'despeckle' && (
          <SlicerDespeckleDefringeCard
            cleanupMode={cleanupMode}
            setCleanupMode={setCleanupMode}
            fringeColorType={fringeColorType}
            setFringeColorType={setFringeColorType}
            fringeColorHex={fringeColorHex}
            setFringeColorHex={setFringeColorHex}
            isEyedropperActive={isEyedropperActive}
            setIsEyedropperActive={setIsEyedropperActive}
            eyedropperTarget={eyedropperTarget}
            setEyedropperTarget={setEyedropperTarget}
            defringeStrength={defringeStrength}
            setDefringeStrength={setDefringeStrength}
            edgeChoke={edgeChoke}
            setEdgeChoke={setEdgeChoke}
            edgeSmooth={edgeSmooth}
            setEdgeSmooth={setEdgeSmooth}
            smoothColorType={smoothColorType}
            setSmoothColorType={setSmoothColorType}
            smoothColorHex={smoothColorHex}
            setSmoothColorHex={setSmoothColorHex}
            despeckleSize={despeckleSize}
            setDespeckleSize={setDespeckleSize}
            whiteSpeckleSensitivity={whiteSpeckleSensitivity}
            setWhiteSpeckleSensitivity={setWhiteSpeckleSensitivity}
            keepLargestIslandOnly={keepLargestIslandOnly}
            setKeepLargestIslandOnly={setKeepLargestIslandOnly}
            isProcessing={isProcessing}
            onRunDespeckleOnly={onRunDespeckleOnly}
            onCommitSliderChange={onCommitSliderChange}
          />
        )}

        {/* Tab 3 */}
        {bgCleanupSubTab === 'ai_matting' && (
          <SlicerAIMattingCard
            aiServerStatus={aiServerStatus}
            aiModel={aiModel}
            setAiModel={setAiModel}
            aiScope={aiScope}
            setAiScope={setAiScope}
            totalCellCount={totalCellCount}
            isAIRunning={isAIRunning}
            onRunAIMatting={onRunAIMatting}
          />
        )}
      </div>

      {/* CARD 3: XUẤT BẢN & LẮP RÁP 3D */}
      <SlicerAssemblyActionCard
        isProcessing={isProcessing}
        assemblySuccess={assemblySuccess}
        slicedCount={slicedCount}
        totalCellCount={totalCellCount}
        paddingInset={paddingInset}
        setPaddingInset={setPaddingInset}
        enableSmartCrop={enableSmartCrop}
        setEnableSmartCrop={setEnableSmartCrop}
        smartCropPadding={smartCropPadding}
        setSmartCropPadding={setSmartCropPadding}
        onCommitSliderChange={onCommitSliderChange}
        onAutoSliceAndAssemble={onAutoSliceAndAssemble}
        onOpenSaveKitModal={onOpenSaveKitModal}
        onOpenCatalogModal={onOpenCatalogModal}
        onApplyAsNewBaseImage={onApplyAsNewBaseImage}
        checkedCount={checkedCount}
        onBatchSeparateChecked={onBatchSeparateChecked}
        isBatchProcessing={isBatchProcessing}
      />
    </div>
  );
};
