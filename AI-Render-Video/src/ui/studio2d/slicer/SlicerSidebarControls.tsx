import React from 'react';
import {
  Scissors,
  Sparkles,
  Upload,
  Layers,
  Save,
  Check,
  RefreshCw,
  Sliders,
  ChevronDown,
  Info,
  Palette,
  Pipette,
} from 'lucide-react';
import {
  GRID_CATEGORY_DEFINITIONS,
} from '../../../core/assets/GridSliceRegistry';

import { ChromaProcessOptions } from '../../../core/utils/ChromaDespeckleProcessor';

export interface SlicerSidebarControlsProps {
  selectedCatId: string;
  onSelectCatId: (id: string) => void;
  userUploadedImageUrl: string | null;
  onFileUpload: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onResetToDemoImage: (key?: 'default' | 'chibi' | 'irregular_ai') => void;
  onClearImage?: () => void;
  fileInputRef: React.RefObject<HTMLInputElement>;
  isEyedropperActive?: boolean;
  setIsEyedropperActive?: (active: boolean) => void;
  // Background removal params
  keyColorType: 'chroma_green' | 'pure_white' | 'custom';
  setKeyColorType: (type: 'chroma_green' | 'pure_white' | 'custom') => void;
  keyColorHex: string;
  setKeyColorHex: (hex: string) => void;
  isolationMode: 'all' | 'outer_only';
  setIsolationMode: (mode: 'all' | 'outer_only') => void;
  tolerance: number;
  setTolerance: (tol: number) => void;
  feather: number;
  setFeather: (f: number) => void;
  shadowRetention?: number;
  setShadowRetention?: (val: number) => void;
  strokeWidth?: number;
  setStrokeWidth?: (val: number) => void;
  strokeColorHex?: string;
  setStrokeColorHex?: (hex: string) => void;
  // Despeckle params
  bgCleanupSubTab: 'chroma' | 'despeckle' | 'ai_matting';
  setBgCleanupSubTab: (tab: 'chroma' | 'despeckle' | 'ai_matting') => void;
  // AI Matting properties
  aiModel?: string;
  setAiModel?: (model: string) => void;
  aiScope?: 'full_image' | 'all' | 'selected';
  setAiScope?: (scope: 'full_image' | 'all' | 'selected') => void;
  aiServerStatus?: 'online' | 'offline' | 'checking';
  onRunAIMatting?: () => void;
  onRunFastBFSMatting?: () => void;
  isAIRunning?: boolean;
  despeckleSize: number;
  setDespeckleSize: (size: number) => void;
  whiteSpeckleSensitivity: number;
  setWhiteSpeckleSensitivity: (sens: number) => void;
  keepLargestIslandOnly: boolean;
  setKeepLargestIslandOnly: (keep: boolean) => void;
  // Advanced Despeckle & Edge Cleanup properties
  eyedropperTarget?: 'chroma' | 'fringe';
  setEyedropperTarget?: (target: 'chroma' | 'fringe') => void;
  cleanupMode?: 'all' | 'defringe' | 'smooth' | 'despeckle';
  setCleanupMode?: (mode: 'all' | 'defringe' | 'smooth' | 'despeckle') => void;
  fringeColorType?: 'chroma_green' | 'pure_white' | 'pure_black' | 'custom';
  setFringeColorType?: (type: 'chroma_green' | 'pure_white' | 'pure_black' | 'custom') => void;
  fringeColorHex?: string;
  setFringeColorHex?: (hex: string) => void;
  defringeStrength?: number;
  setDefringeStrength?: (val: number) => void;
  edgeChoke?: number;
  setEdgeChoke?: (val: number) => void;
  edgeSmooth?: number;
  setEdgeSmooth?: (val: number) => void;
  onRunDespeckleOnly?: () => void;
  // Slicing & Actions
  // Cumulative / Multi-Pass mode & Apply as Base
  isCumulativeProcessing?: boolean;
  setIsCumulativeProcessing?: (val: boolean) => void;
  onResetToRawSlices?: () => void;
  onApplyAsNewBaseImage?: () => void;
  // Padding Inset
  paddingInset?: number;
  setPaddingInset?: (val: number) => void;
  isProcessing: boolean;
  assemblySuccess: boolean;
  onAutoSliceAndAssemble: () => void;
  slicedCount: number;
  totalCellCount: number;
  onOpenSaveKitModal: () => void;
  onOpenCatalogModal?: () => void;
  onCommitSliderChange?: (overrides?: Partial<ChromaProcessOptions>) => void;
}

export const SlicerSidebarControls: React.FC<SlicerSidebarControlsProps> = ({
  selectedCatId,
  onSelectCatId,
  userUploadedImageUrl,
  onFileUpload,
  onResetToDemoImage,
  onClearImage,
  fileInputRef,
  isEyedropperActive = false,
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
  shadowRetention = 100,
  setShadowRetention,
  strokeWidth = 0,
  setStrokeWidth,
  strokeColorHex = '#000000',
  setStrokeColorHex,
  bgCleanupSubTab,
  setBgCleanupSubTab,
  aiModel = 'birefnet-general',
  setAiModel,
  aiScope = 'all',
  setAiScope,
  aiServerStatus = 'offline',
  onRunAIMatting,
  onRunFastBFSMatting,
  isAIRunning = false,
  despeckleSize,
  setDespeckleSize,
  whiteSpeckleSensitivity,
  setWhiteSpeckleSensitivity,
  keepLargestIslandOnly,
  setKeepLargestIslandOnly,
  eyedropperTarget = 'chroma',
  setEyedropperTarget,
  cleanupMode = 'all',
  setCleanupMode,
  fringeColorType = 'chroma_green',
  setFringeColorType,
  fringeColorHex = '#00ff00',
  setFringeColorHex,
  defringeStrength = 60,
  setDefringeStrength,
  edgeChoke = 0,
  setEdgeChoke,
  edgeSmooth = 2,
  setEdgeSmooth,
  onRunDespeckleOnly,
  isCumulativeProcessing = false,
  setIsCumulativeProcessing,
  onResetToRawSlices,
  onApplyAsNewBaseImage,
  paddingInset = 0,
  setPaddingInset,
  isProcessing,
  assemblySuccess,
  onAutoSliceAndAssemble,
  slicedCount,
  totalCellCount,
  onOpenSaveKitModal,
  onOpenCatalogModal,
  onCommitSliderChange,
}) => {
  const currentCat = GRID_CATEGORY_DEFINITIONS.find((c) => c.id === selectedCatId) || GRID_CATEGORY_DEFINITIONS[0];

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 10,
        background: '#070b14',
        padding: 10,
        borderRadius: 10,
        border: '1px solid rgba(255,255,255,0.08)',
        overflowY: 'auto',
        color: '#e2e8f0',
        fontFamily: "var(--font-main, 'Be Vietnam Pro', 'Inter', system-ui, sans-serif)",
        boxSizing: 'border-box',
      }}
    >
      {/* ========================================================
          CARD 1: NGUỒN ẢNH & CẤU HÌNH KHUNG LƯỚI
         ======================================================== */}
      <div
        style={{
          background: 'linear-gradient(180deg, rgba(24, 34, 53, 0.7) 0%, rgba(15, 23, 42, 0.8) 100%)',
          borderRadius: 8,
          border: '1px solid rgba(56, 189, 248, 0.25)',
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 8,
          boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
        }}
      >
        {/* Card Header */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderBottom: '1px solid rgba(255,255,255,0.06)', paddingBottom: 6 }}>
          <div style={{ fontSize: 11.5, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6, letterSpacing: '0.2px' }}>
            <Layers size={14} color="#38bdf8" /> 1. Nguồn ảnh & Khung lưới
          </div>
          <span
            style={{
              fontSize: 9.5,
              fontWeight: 600,
              padding: '2px 7px',
              borderRadius: 4,
              background: selectedCatId === 'single_full_image' ? 'rgba(34, 197, 94, 0.2)' : 'rgba(56, 189, 248, 0.15)',
              color: selectedCatId === 'single_full_image' ? '#4ade80' : '#38bdf8',
              border: selectedCatId === 'single_full_image' ? '1px solid #22c55e' : '1px solid rgba(56, 189, 248, 0.3)',
            }}
          >
            {selectedCatId === 'single_full_image' ? '🖼️ 1 Ảnh đơn' : `${currentCat.rows} Hàng × ${currentCat.cols} Cột`}
          </span>
        </div>

        {/* Row 1: Mode Switcher (Ảnh đơn vs Cắt lưới Sprite) - Synchronized height: 32px */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
          <button
            onClick={() => onSelectCatId('single_full_image')}
            style={{
              height: 32,
              fontSize: 10.5,
              fontWeight: 600,
              borderRadius: 6,
              border: selectedCatId === 'single_full_image' ? '1.5px solid #22c55e' : '1px solid rgba(255,255,255,0.1)',
              background: selectedCatId === 'single_full_image' ? 'linear-gradient(135deg, #15803d, #166534)' : 'rgba(0,0,0,0.3)',
              color: selectedCatId === 'single_full_image' ? '#ffffff' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 5,
              boxShadow: selectedCatId === 'single_full_image' ? '0 2px 8px rgba(34, 197, 94, 0.4)' : 'none',
              boxSizing: 'border-box',
            }}
            title="Tắt khung lưới, xử lý toàn bộ bức ảnh tải lên như 1 vật thể hoàn chỉnh"
          >
            🖼️ Ảnh đơn (Tắt lưới)
          </button>
          <button
            onClick={() => {
              if (selectedCatId === 'single_full_image') {
                onSelectCatId('hair_multi_angle_grid');
              }
            }}
            style={{
              height: 32,
              fontSize: 10.5,
              fontWeight: 600,
              borderRadius: 6,
              border: selectedCatId !== 'single_full_image' ? '1.5px solid #0284c7' : '1px solid rgba(255,255,255,0.1)',
              background: selectedCatId !== 'single_full_image' ? 'linear-gradient(135deg, #0284c7, #0369a1)' : 'rgba(0,0,0,0.3)',
              color: selectedCatId !== 'single_full_image' ? '#ffffff' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 5,
              boxShadow: selectedCatId !== 'single_full_image' ? '0 2px 8px rgba(2, 132, 199, 0.4)' : 'none',
              boxSizing: 'border-box',
            }}
            title="Bật khung lưới để cắt ảnh Sprite Sheet thành nhiều ô"
          >
            🔲 Cắt lưới Sprite
          </button>
        </div>

        {/* Row 2: Category Dropdown (Synchronized height: 34px) */}
        <select
          value={selectedCatId}
          onChange={(e) => onSelectCatId(e.target.value)}
          style={{
            width: '100%',
            height: 34,
            padding: '0 10px',
            fontSize: 11,
            background: '#0b1329',
            color: '#38bdf8',
            border: '1.5px solid #0284c7',
            borderRadius: 6,
            fontWeight: 600,
            cursor: 'pointer',
            outline: 'none',
            boxSizing: 'border-box',
          }}
        >
          {GRID_CATEGORY_DEFINITIONS.map((cat) => (
            <option key={cat.id} value={cat.id}>
              {cat.label} ({cat.rows}x{cat.cols})
            </option>
          ))}
        </select>

        {/* Row 3: Upload Main Action Button (Synchronized height: 34px) */}
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          onChange={onFileUpload}
          style={{ display: 'none' }}
        />
        <button
          onClick={() => fileInputRef.current?.click()}
          style={{
            width: '100%',
            height: 34,
            fontSize: 11.5,
            fontWeight: 700,
            borderRadius: 6,
            background: 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)',
            color: '#ffffff',
            border: '1px solid rgba(56, 189, 248, 0.5)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 6,
            boxShadow: '0 2px 8px rgba(2, 132, 199, 0.35)',
            boxSizing: 'border-box',
          }}
        >
          <Upload size={14} /> {userUploadedImageUrl ? '📤 Tải ảnh khác lên' : '📤 Tải ảnh Sprite Sheet lên'}
        </button>

        {/* Row 4: Demo Samples & Clear Actions (Synchronized height: 28px) */}
        <div style={{ display: 'grid', gridTemplateColumns: userUploadedImageUrl ? '1fr 1fr 1fr 1fr' : '1fr 1fr 1fr', gap: 5 }}>
          <button
            onClick={() => onResetToDemoImage('chibi')}
            style={{
              height: 28,
              fontSize: 10,
              fontWeight: 600,
              borderRadius: 5,
              background: 'rgba(16, 185, 129, 0.15)',
              color: '#6ee7b7',
              border: '1px solid rgba(16, 185, 129, 0.35)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              boxSizing: 'border-box',
            }}
            title="Dùng ảnh mẫu bóc tách tóc Chibi 4x5"
          >
            🌟 Mẫu Chibi
          </button>

          <button
            onClick={() => onResetToDemoImage('default')}
            style={{
              height: 28,
              fontSize: 10,
              fontWeight: 600,
              borderRadius: 5,
              background: 'rgba(255,255,255,0.06)',
              color: '#cbd5e1',
              border: '1px solid rgba(255,255,255,0.12)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              boxSizing: 'border-box',
            }}
            title="Dùng ảnh mẫu tóc Kiếm Khách"
          >
            🗡️ Kiếm Khách
          </button>

          <button
            onClick={() => onResetToDemoImage('irregular_ai' as any)}
            style={{
              height: 28,
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 5,
              background: 'linear-gradient(135deg, rgba(2, 132, 199, 0.25), rgba(139, 92, 246, 0.25))',
              color: '#38bdf8',
              border: '1px solid rgba(56, 189, 248, 0.4)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              boxSizing: 'border-box',
            }}
            title="Nạp ảnh Sprite Sheet bị lệch lề AI để kiểm tra thuật toán Auto-Fit tự căn khung"
          >
            🎯 Test Lệch AI
          </button>

          {userUploadedImageUrl && onClearImage && (
            <button
              onClick={onClearImage}
              style={{
                height: 28,
                fontSize: 10,
                fontWeight: 600,
                borderRadius: 5,
                background: 'rgba(239, 68, 68, 0.15)',
                color: '#fca5a5',
                border: '1px solid rgba(239, 68, 68, 0.35)',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 4,
                boxSizing: 'border-box',
              }}
              title="Xóa ảnh hiện tại và để trống khung làm việc"
            >
              🗑️ Để trống
            </button>
          )}
        </div>

        {/* Row 5: Padding Inset Control */}
        <div style={{ background: 'rgba(0,0,0,0.3)', padding: 8, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 4 }}>
            <span style={{ fontWeight: 600, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
              ✂️ Thu viền ô (Padding Inset):
            </span>
            <span style={{ color: '#4ade80', fontWeight: 700 }}>{paddingInset}px</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            {setPaddingInset && (
              <input
                type="range"
                min="0"
                max="40"
                step="1"
                value={paddingInset}
                onChange={(e) => setPaddingInset(parseInt(e.target.value, 10))}
                onPointerUp={() => { if (onCommitSliderChange) onCommitSliderChange(); }}
                onTouchEnd={() => { if (onCommitSliderChange) onCommitSliderChange(); }}
                onKeyUp={() => { if (onCommitSliderChange) onCommitSliderChange(); }}
                style={{ flex: 1, accentColor: '#38bdf8' }}
              />
            )}
            {setPaddingInset && (
              <div style={{ display: 'flex', gap: 3 }}>
                {[0, 4, 8].map((val) => (
                  <button
                    key={val}
                    onClick={() => { setPaddingInset(val); if (onCommitSliderChange) onCommitSliderChange(); }}
                    style={{
                      height: 22,
                      padding: '0 6px',
                      fontSize: 9,
                      fontWeight: 600,
                      background: paddingInset === val ? '#0284c7' : 'rgba(255,255,255,0.1)',
                      color: paddingInset === val ? '#ffffff' : '#cbd5e1',
                      border: 'none',
                      borderRadius: 3,
                      cursor: 'pointer',
                      boxSizing: 'border-box',
                    }}
                  >
                    {val}px
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ========================================================
          CARD 2: CHẾ ĐỘ TÁCH NỀN & BỘ LỌC (3 SUB-TABS)
         ======================================================== */}
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
        {/* Card Header */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderBottom: '1px solid rgba(255,255,255,0.06)', paddingBottom: 6 }}>
          <div style={{ fontSize: 11.5, fontWeight: 700, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 6, letterSpacing: '0.2px' }}>
            <Scissors size={14} color="#4ade80" /> 2. Chế độ bóc tách nền
          </div>
        </div>

        {/* 3 Sub-tabs Bar - Synchronized height: 34px */}
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
              boxShadow: bgCleanupSubTab === 'chroma' ? '0 0 10px rgba(56,189,248,0.2)' : 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              boxSizing: 'border-box',
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
              boxShadow: bgCleanupSubTab === 'despeckle' ? '0 0 10px rgba(74,222,128,0.2)' : 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              boxSizing: 'border-box',
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
              boxShadow: bgCleanupSubTab === 'ai_matting' ? '0 0 12px rgba(192,132,252,0.3)' : 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              boxSizing: 'border-box',
            }}
          >
            🤖 3. AI Matting
          </button>
        </div>

        {/* ---------------- SUB-TAB 1: TÁCH MÀU ---------------- */}
        {bgCleanupSubTab === 'chroma' ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
            {/* Color Selection & Eyedropper Card */}
            <div style={{ background: 'rgba(15, 23, 42, 0.65)', padding: 9, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', display: 'flex', flexDirection: 'column', gap: 7 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontSize: 10.5, color: '#e2e8f0', fontWeight: 600, display: 'flex', alignItems: 'center', gap: 5 }}>
                  🎯 Chọn màu nền cần bóc:
                </span>
                {/* Current Color Swatch Badge */}
                <div style={{ display: 'flex', alignItems: 'center', gap: 5, padding: '2px 7px', background: '#090e1a', borderRadius: 4, border: '1px solid rgba(255,255,255,0.15)' }}>
                  <div
                    style={{
                      width: 14,
                      height: 14,
                      borderRadius: 3,
                      background: keyColorType === 'chroma_green' ? '#00ff00' : keyColorType === 'pure_white' ? '#ffffff' : keyColorHex,
                      border: '1px solid rgba(255,255,255,0.4)',
                    }}
                  />
                  <span style={{ fontSize: 10, fontFamily: 'monospace', fontWeight: 700, color: '#38bdf8' }}>
                    {keyColorType === 'chroma_green' ? '#00FF00' : keyColorType === 'pure_white' ? '#FFFFFF' : keyColorHex.toUpperCase()}
                  </span>
                </div>
              </div>

              {/* Color Presets Grid - Synchronized height: 30px */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 5 }}>
                <button
                  onClick={() => {
                    setKeyColorType('chroma_green');
                    if (setIsEyedropperActive) setIsEyedropperActive(false);
                    if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'chroma_green' });
                  }}
                  style={{
                    height: 30,
                    fontSize: 10,
                    fontWeight: 600,
                    borderRadius: 6,
                    border: keyColorType === 'chroma_green' ? '1.5px solid #22c55e' : '1px solid rgba(255,255,255,0.1)',
                    background: keyColorType === 'chroma_green' ? 'rgba(34, 197, 94, 0.25)' : 'rgba(0,0,0,0.3)',
                    color: keyColorType === 'chroma_green' ? '#4ade80' : '#94a3b8',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 4,
                    boxSizing: 'border-box',
                  }}
                >
                  <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#22c55e', display: 'inline-block' }} />
                  Xanh lá
                </button>

                <button
                  onClick={() => {
                    setKeyColorType('pure_white');
                    if (setIsEyedropperActive) setIsEyedropperActive(false);
                    if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'pure_white' });
                  }}
                  style={{
                    height: 30,
                    fontSize: 10,
                    fontWeight: 600,
                    borderRadius: 6,
                    border: keyColorType === 'pure_white' ? '1.5px solid #ffffff' : '1px solid rgba(255,255,255,0.1)',
                    background: keyColorType === 'pure_white' ? 'rgba(255,255,255,0.2)' : 'rgba(0,0,0,0.3)',
                    color: keyColorType === 'pure_white' ? '#ffffff' : '#94a3b8',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 4,
                    boxSizing: 'border-box',
                  }}
                >
                  <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#ffffff', border: '1px solid #666', display: 'inline-block' }} />
                  Trắng tinh
                </button>

                <button
                  onClick={() => {
                    setKeyColorType('custom');
                    if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'custom', keyColorHex });
                  }}
                  style={{
                    height: 30,
                    fontSize: 10,
                    fontWeight: 600,
                    borderRadius: 6,
                    border: keyColorType === 'custom' ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    background: keyColorType === 'custom' ? 'rgba(56, 189, 248, 0.25)' : 'rgba(0,0,0,0.3)',
                    color: keyColorType === 'custom' ? '#38bdf8' : '#94a3b8',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 4,
                    boxSizing: 'border-box',
                  }}
                >
                  🎨 Mã tùy chọn
                </button>
              </div>

              {/* EYEDROPPER & CUSTOM COLOR ROW - Synchronized height: 32px */}
              <div style={{ display: 'flex', gap: 6, alignItems: 'center', background: 'rgba(0,0,0,0.35)', padding: 6, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
                {/* Eyedropper Button */}
                <button
                  onClick={() => {
                    if (setEyedropperTarget) setEyedropperTarget('chroma');
                    if (setIsEyedropperActive) {
                      setIsEyedropperActive(!isEyedropperActive);
                    }
                  }}
                  style={{
                    flex: 1,
                    height: 32,
                    fontSize: 10.5,
                    fontWeight: 700,
                    borderRadius: 5,
                    background: isEyedropperActive && eyedropperTarget === 'chroma'
                      ? 'linear-gradient(135deg, #f59e0b 0%, #d97706 100%)'
                      : 'linear-gradient(135deg, rgba(56,189,248,0.2) 0%, rgba(2,132,199,0.2) 100%)',
                    color: isEyedropperActive && eyedropperTarget === 'chroma' ? '#000000' : '#38bdf8',
                    border: isEyedropperActive && eyedropperTarget === 'chroma' ? '1.5px solid #fbbf24' : '1px solid rgba(56,189,248,0.4)',
                    boxShadow: isEyedropperActive && eyedropperTarget === 'chroma' ? '0 0 12px rgba(245,158,11,0.5)' : 'none',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 6,
                    transition: 'all 0.15s ease',
                    boxSizing: 'border-box',
                  }}
                  title="Công cụ Hút Màu Pixel (Eyedropper): Nhấp vào đây rồi nhấp trực tiếp lên ảnh để lấy chính xác mã màu nền"
                >
                  <Pipette size={14} />
                  {isEyedropperActive && eyedropperTarget === 'chroma' ? '🎯 Đang hút màu nền...' : '💧 Hút màu từ ảnh'}
                </button>

                {/* Color input picker */}
                <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                  <input
                    type="color"
                    value={keyColorType === 'chroma_green' ? '#00ff00' : keyColorType === 'pure_white' ? '#ffffff' : keyColorHex}
                    onChange={(e) => {
                      setKeyColorType('custom');
                      setKeyColorHex(e.target.value);
                      if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'custom', keyColorHex: e.target.value });
                    }}
                    style={{ width: 32, height: 32, padding: 0, border: 'none', borderRadius: 4, cursor: 'pointer', background: 'transparent', boxSizing: 'border-box' }}
                    title="Bảng chọn màu chi tiết"
                  />
                  <input
                    type="text"
                    value={keyColorHex}
                    onChange={(e) => {
                      setKeyColorType('custom');
                      setKeyColorHex(e.target.value);
                      if (onCommitSliderChange && e.target.value.length === 7) {
                        onCommitSliderChange({ keyColorType: 'custom', keyColorHex: e.target.value });
                      }
                    }}
                    style={{ width: 72, height: 32, padding: '0 6px', fontSize: 10.5, background: '#090e1a', color: '#38bdf8', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4, fontFamily: 'monospace', textAlign: 'center', fontWeight: 600, boxSizing: 'border-box' }}
                    placeholder="#000000"
                  />
                </div>
              </div>

              {/* Eyedropper Instruction Tooltip */}
              {isEyedropperActive && (
                <div style={{ background: 'rgba(245, 158, 11, 0.15)', padding: '5px 8px', borderRadius: 5, border: '1px solid rgba(245, 158, 11, 0.4)', fontSize: 9.5, color: '#fef08a', display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span>🎯</span>
                  <span><b>Chế độ hút màu:</b> Rê chuột qua ảnh để soi mã màu & nhấp chuột vào màu bạn muốn tách!</span>
                </div>
              )}
            </div>

            {/* Quick BFS Action Button (Synchronized height: 34px) */}
            <button
              onClick={onRunFastBFSMatting}
              disabled={isProcessing}
              style={{
                width: '100%',
                height: 34,
                fontSize: 11,
                fontWeight: 700,
                borderRadius: 6,
                background: 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)',
                color: '#ffffff',
                border: '1px solid rgba(56, 189, 248, 0.5)',
                cursor: isProcessing ? 'not-allowed' : 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 6,
                boxShadow: '0 3px 10px rgba(2, 132, 199, 0.35)',
                boxSizing: 'border-box',
                letterSpacing: '0.1px',
              }}
              title="Tách sạch nền 100% bằng thuật toán BFS loang từ viền, bảo vệ tròng trắng mắt và quần áo trắng"
            >
              <Sparkles size={14} />
              ⚡ Tách nền Smart BFS (Trực tiếp trình duyệt)
            </button>

            {/* Isolation Mode Buttons - Synchronized height: 30px */}
            <div>
              <div style={{ fontSize: 9.5, color: '#94a3b8', marginBottom: 4, fontWeight: 600 }}>Phạm vi bóc tách:</div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.2fr', gap: 5 }}>
                <button
                  onClick={() => {
                    setIsolationMode('all');
                    if (onCommitSliderChange) onCommitSliderChange({ isolationMode: 'all' });
                  }}
                  style={{
                    height: 30,
                    fontSize: 10,
                    fontWeight: 600,
                    borderRadius: 5,
                    border: isolationMode === 'all' ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    background: isolationMode === 'all' ? 'rgba(56, 189, 248, 0.25)' : 'rgba(0,0,0,0.3)',
                    color: isolationMode === 'all' ? '#38bdf8' : '#94a3b8',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 4,
                    boxSizing: 'border-box',
                  }}
                  title="Tách toàn bộ mọi khoảng màu trùng khớp trên toàn bộ ô/ảnh"
                >
                  🌐 Tách toàn bộ
                </button>
                <button
                  onClick={() => {
                    setIsolationMode('outer_only');
                    if (onCommitSliderChange) onCommitSliderChange({ isolationMode: 'outer_only' });
                  }}
                  style={{
                    height: 30,
                    fontSize: 10,
                    fontWeight: 600,
                    borderRadius: 5,
                    border: isolationMode === 'outer_only' ? '1.5px solid #eab308' : '1px solid rgba(255,255,255,0.1)',
                    background: isolationMode === 'outer_only' ? 'rgba(234, 179, 8, 0.25)' : 'rgba(0,0,0,0.3)',
                    color: isolationMode === 'outer_only' ? '#facc15' : '#94a3b8',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 4,
                    boxSizing: 'border-box',
                  }}
                  title="Chỉ tách nền viền ngoài, bảo vệ tròng mắt trắng/quần áo bên trong"
                >
                  🔲 Tách viền ngoài
                </button>
              </div>
            </div>

            {/* Tolerance Slider */}
            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 7, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 3 }}>
                <span style={{ fontWeight: 600, color: '#38bdf8' }}>🎯 Độ nhạy tách màu (Tolerance):</span>
                <span style={{ color: '#38bdf8', fontWeight: 700 }}>{tolerance}</span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                <input
                  type="range"
                  min="1"
                  max="100"
                  value={tolerance}
                  onChange={(e) => setTolerance(parseInt(e.target.value))}
                  onPointerUp={(e) => {
                    const val = parseInt((e.target as HTMLInputElement).value);
                    if (onCommitSliderChange) onCommitSliderChange({ tolerance: val });
                  }}
                  onTouchEnd={(e) => {
                    const val = parseInt((e.target as HTMLInputElement).value);
                    if (onCommitSliderChange) onCommitSliderChange({ tolerance: val });
                  }}
                  onKeyUp={(e) => {
                    const val = parseInt((e.target as HTMLInputElement).value);
                    if (onCommitSliderChange) onCommitSliderChange({ tolerance: val });
                  }}
                  style={{ flex: 1, accentColor: '#38bdf8' }}
                />
                <div style={{ display: 'flex', gap: 3 }}>
                  {[1, 25, 50].map((val) => (
                    <button
                      key={val}
                      onClick={() => { setTolerance(val); if (onCommitSliderChange) onCommitSliderChange({ tolerance: val }); }}
                      style={{
                        height: 22,
                        padding: '0 6px',
                        fontSize: 9,
                        fontWeight: 600,
                        background: tolerance === val ? '#0284c7' : 'rgba(255,255,255,0.1)',
                        color: tolerance === val ? '#ffffff' : '#cbd5e1',
                        border: 'none',
                        borderRadius: 3,
                        cursor: 'pointer',
                        boxSizing: 'border-box',
                      }}
                    >
                      {val}
                    </button>
                  ))}
                </div>
              </div>
            </div>

            {/* Feather Slider */}
            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 7, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 3 }}>
                <span style={{ fontWeight: 600, color: '#38bdf8' }}>🪶 Làm mềm viền (Feather):</span>
                <span style={{ color: '#38bdf8', fontWeight: 700 }}>{feather}px</span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                <input
                  type="range"
                  min="0"
                  max="15"
                  value={feather}
                  onChange={(e) => setFeather(parseInt(e.target.value))}
                  onPointerUp={(e) => {
                    const val = parseInt((e.target as HTMLInputElement).value);
                    if (onCommitSliderChange) onCommitSliderChange({ feather: val });
                  }}
                  onTouchEnd={(e) => {
                    const val = parseInt((e.target as HTMLInputElement).value);
                    if (onCommitSliderChange) onCommitSliderChange({ feather: val });
                  }}
                  onKeyUp={(e) => {
                    const val = parseInt((e.target as HTMLInputElement).value);
                    if (onCommitSliderChange) onCommitSliderChange({ feather: val });
                  }}
                  style={{ flex: 1, accentColor: '#38bdf8' }}
                />
                <div style={{ display: 'flex', gap: 3 }}>
                  {[0, 1, 2].map((val) => (
                    <button
                      key={val}
                      onClick={() => { setFeather(val); if (onCommitSliderChange) onCommitSliderChange({ feather: val }); }}
                      style={{
                        height: 22,
                        padding: '0 6px',
                        fontSize: 9,
                        fontWeight: 600,
                        background: feather === val ? '#0284c7' : 'rgba(255,255,255,0.1)',
                        color: feather === val ? '#ffffff' : '#cbd5e1',
                        border: 'none',
                        borderRadius: 3,
                        cursor: 'pointer',
                        boxSizing: 'border-box',
                      }}
                    >
                      {val}px
                    </button>
                  ))}
                </div>
              </div>
            </div>

            {/* Giữ bóng đổ mờ / Vải voan lụa mỏng xuyên thấu (Soft Shadow & Translucent Silk Extraction) */}
            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 7, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 3 }}>
                <span style={{ fontWeight: 600, color: '#a78bfa' }}>✨ Giữ bóng mờ & Vải lụa xuyên thấu (Translucent Silk):</span>
                <span style={{ color: '#a78bfa', fontWeight: 700 }}>{shadowRetention}%</span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                {setShadowRetention && (
                  <input
                    type="range"
                    min="0"
                    max="100"
                    step="5"
                    value={shadowRetention}
                    onChange={(e) => setShadowRetention(parseInt(e.target.value))}
                    onPointerUp={(e) => {
                      const val = parseInt((e.target as HTMLInputElement).value);
                      if (onCommitSliderChange) onCommitSliderChange({ shadowRetention: val });
                    }}
                    onTouchEnd={(e) => {
                      const val = parseInt((e.target as HTMLInputElement).value);
                      if (onCommitSliderChange) onCommitSliderChange({ shadowRetention: val });
                    }}
                    onKeyUp={(e) => {
                      const val = parseInt((e.target as HTMLInputElement).value);
                      if (onCommitSliderChange) onCommitSliderChange({ shadowRetention: val });
                    }}
                    style={{ flex: 1, accentColor: '#a78bfa' }}
                    title="Optical Unmixing: Bóc tách lọc sạch màu nền khỏi các lớp vải lụa mỏng bay phất phới, tơ lụa, khói mờ và bóng đổ hốc mắt, giữ nguyên độ trong suốt tự nhiên không bị ám màu nền"
                  />
                )}
                {setShadowRetention && (
                  <div style={{ display: 'flex', gap: 3 }}>
                    {[
                      { val: 0, label: 'Tắt' },
                      { val: 50, label: '50%' },
                      { val: 100, label: '100%' },
                    ].map((item) => (
                      <button
                        key={item.val}
                        onClick={() => {
                          setShadowRetention(item.val);
                          if (onCommitSliderChange) onCommitSliderChange({ shadowRetention: item.val });
                        }}
                        style={{
                          height: 22,
                          padding: '0 6px',
                          fontSize: 9,
                          fontWeight: 600,
                          background: shadowRetention === item.val ? '#7c3aed' : 'rgba(255,255,255,0.1)',
                          color: shadowRetention === item.val ? '#ffffff' : '#cbd5e1',
                          border: 'none',
                          borderRadius: 3,
                          cursor: 'pointer',
                          boxSizing: 'border-box',
                        }}
                      >
                        {item.label}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* Stroke / Thêm Viền Nét Theo Màu */}
            <div style={{ background: 'rgba(0,0,0,0.35)', padding: 8, borderRadius: 6, border: '1.5px solid rgba(56, 189, 248, 0.3)', display: 'flex', flexDirection: 'column', gap: 6 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontSize: 10, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
                  <Palette size={13} color="#38bdf8" /> 🎨 Thêm viền nét theo màu (Stroke):
                </span>
                <span style={{ fontSize: 10, color: '#4ade80', fontWeight: 700 }}>{strokeWidth}px</span>
              </div>

              {/* Color Presets */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, flexWrap: 'wrap' }}>
                <span style={{ fontSize: 9, color: '#cbd5e1', fontWeight: 500 }}>Màu viền:</span>
                {setStrokeColorHex && (
                  <input
                    type="color"
                    value={strokeColorHex}
                    onChange={(e) => {
                      setStrokeColorHex(e.target.value);
                      if (onCommitSliderChange && strokeWidth > 0) onCommitSliderChange({ strokeColorHex: e.target.value });
                    }}
                    style={{ width: 26, height: 22, padding: 0, border: 'none', borderRadius: 3, cursor: 'pointer', background: 'transparent' }}
                    title="Chọn màu viền tùy ý"
                  />
                )}
                {setStrokeColorHex && (
                  <div style={{ display: 'flex', gap: 3 }}>
                    {[
                      { hex: '#000000', label: '🖤 Đen' },
                      { hex: '#ffffff', label: '🤍 Trắng' },
                      { hex: '#2b1810', label: '🤎 Nâu' },
                    ].map((item) => (
                      <button
                        key={item.hex}
                        onClick={() => { setStrokeColorHex(item.hex); if (onCommitSliderChange && strokeWidth > 0) onCommitSliderChange({ strokeColorHex: item.hex }); }}
                        style={{
                          height: 22,
                          padding: '0 6px',
                          fontSize: 9,
                          fontWeight: 600,
                          background: strokeColorHex === item.hex ? '#0284c7' : 'rgba(0,0,0,0.6)',
                          color: '#fff',
                          border: strokeColorHex === item.hex ? '1px solid #38bdf8' : '1px solid #475569',
                          borderRadius: 3,
                          cursor: 'pointer',
                          boxSizing: 'border-box',
                        }}
                      >
                        {item.label}
                      </button>
                    ))}
                  </div>
                )}
              </div>

              {/* Stroke Width Slider */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                {setStrokeWidth && (
                  <input
                    type="range"
                    min="0"
                    max="12"
                    step="1"
                    value={strokeWidth}
                    onChange={(e) => setStrokeWidth(parseInt(e.target.value, 10))}
                    onPointerUp={(e) => {
                      const val = parseInt((e.target as HTMLInputElement).value, 10);
                      if (onCommitSliderChange) onCommitSliderChange({ strokeWidth: val, strokeColorHex });
                    }}
                    onTouchEnd={(e) => {
                      const val = parseInt((e.target as HTMLInputElement).value, 10);
                      if (onCommitSliderChange) onCommitSliderChange({ strokeWidth: val, strokeColorHex });
                    }}
                    onKeyUp={(e) => {
                      const val = parseInt((e.target as HTMLInputElement).value, 10);
                      if (onCommitSliderChange) onCommitSliderChange({ strokeWidth: val, strokeColorHex });
                    }}
                    style={{ flex: 1, accentColor: '#38bdf8' }}
                  />
                )}
                {setStrokeWidth && (
                  <div style={{ display: 'flex', gap: 3 }}>
                    {[0, 1, 2, 4].map((val) => (
                      <button
                        key={val}
                        onClick={() => { setStrokeWidth(val); if (onCommitSliderChange) onCommitSliderChange({ strokeWidth: val, strokeColorHex }); }}
                        style={{
                          height: 22,
                          padding: '0 6px',
                          fontSize: 9,
                          fontWeight: 600,
                          background: strokeWidth === val ? '#0284c7' : 'rgba(255,255,255,0.1)',
                          color: strokeWidth === val ? '#fff' : '#94a3b8',
                          border: 'none',
                          borderRadius: 3,
                          cursor: 'pointer',
                          boxSizing: 'border-box',
                        }}
                      >
                        {val}px
                      </button>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* Quick Action Button for Tab 1 - Synchronized height: 34px */}
            <button
              onClick={() => onAutoSliceAndAssemble()}
              disabled={isProcessing}
              style={{
                width: '100%',
                height: 34,
                fontSize: 11,
                fontWeight: 700,
                borderRadius: 6,
                background: 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)',
                color: '#ffffff',
                border: '1px solid rgba(56, 189, 248, 0.4)',
                cursor: isProcessing ? 'not-allowed' : 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 5,
                boxShadow: '0 2px 10px rgba(2, 132, 199, 0.35)',
                boxSizing: 'border-box',
                letterSpacing: '0.1px',
              }}
              title="Áp dụng tách màu nền theo các thông số màu sắc ở tab này"
            >
              <Sparkles size={14} />
              ⚡ Áp dụng tách màu nền ngay
            </button>
          </div>
        ) : bgCleanupSubTab === 'despeckle' ? (
          /* ---------------- SUB-TAB 2: KHỬ RÁC & KHỬ VIỀN SƯỢNG (DEFRINGE & EDGE CLEANUP) ---------------- */
          <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
            {/* Cleanup Mode Selector Chips */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.15fr 1.15fr 1fr', gap: 4 }}>
              {[
                { id: 'all', label: '⚡ Tất cả' },
                { id: 'defringe', label: '🎨 Khử màu' },
                { id: 'smooth', label: '✨ Mịn viền' },
                { id: 'despeckle', label: '🧹 Lọc bụi' },
              ].map((m) => (
                <button
                  key={m.id}
                  onClick={() => {
                    if (setCleanupMode) setCleanupMode(m.id as any);
                    if (onCommitSliderChange) onCommitSliderChange({ cleanupMode: m.id as any });
                  }}
                  style={{
                    height: 26,
                    fontSize: 9,
                    fontWeight: 600,
                    borderRadius: 4,
                    border: cleanupMode === m.id ? '1.5px solid #10b981' : '1px solid rgba(255,255,255,0.08)',
                    background: cleanupMode === m.id ? 'rgba(16, 185, 129, 0.25)' : 'rgba(0,0,0,0.3)',
                    color: cleanupMode === m.id ? '#4ade80' : '#94a3b8',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    boxSizing: 'border-box',
                  }}
                >
                  {m.label}
                </button>
              ))}
            </div>

            {/* SECTION 1: KHỬ MÀU VIỀN BÁM (COLOR DEFRINGE & DESPILL) */}
            {(cleanupMode === 'all' || cleanupMode === 'defringe') && (
              <div style={{ background: 'rgba(15, 23, 42, 0.65)', padding: 8, borderRadius: 7, border: '1px solid rgba(16, 185, 129, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontSize: 10, color: '#e2e8f0', fontWeight: 600, display: 'flex', alignItems: 'center', gap: 4 }}>
                    🎨 Màu viền rác cần khử:
                  </span>
                  {/* Fringe Color Swatch Badge */}
                  <div style={{ display: 'flex', alignItems: 'center', gap: 4, padding: '2px 6px', background: '#090e1a', borderRadius: 4, border: '1px solid rgba(255,255,255,0.15)' }}>
                    <div
                      style={{
                        width: 12,
                        height: 12,
                        borderRadius: 2,
                        background: fringeColorType === 'chroma_green' ? '#00ff00' : fringeColorType === 'pure_white' ? '#ffffff' : fringeColorType === 'pure_black' ? '#000000' : fringeColorHex,
                        border: '1px solid rgba(255,255,255,0.4)',
                      }}
                    />
                    <span style={{ fontSize: 9.5, fontFamily: 'monospace', fontWeight: 700, color: '#4ade80' }}>
                      {fringeColorType === 'chroma_green' ? '#00FF00' : fringeColorType === 'pure_white' ? '#FFFFFF' : fringeColorType === 'pure_black' ? '#000000' : fringeColorHex.toUpperCase()}
                    </span>
                  </div>
                </div>

                {/* Fringe Presets Grid - Synchronized height: 28px */}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: 4 }}>
                  {[
                    { id: 'chroma_green', label: 'Xanh lá', color: '#22c55e' },
                    { id: 'pure_white', label: 'Trắng', color: '#ffffff' },
                    { id: 'pure_black', label: 'Đen', color: '#000000' },
                    { id: 'custom', label: 'Tùy chọn', color: '#38bdf8' },
                  ].map((p) => (
                    <button
                      key={p.id}
                      onClick={() => {
                        if (setFringeColorType) setFringeColorType(p.id as any);
                        if (onCommitSliderChange) onCommitSliderChange({ fringeColorType: p.id as any, fringeColorHex });
                      }}
                      style={{
                        height: 28,
                        fontSize: 9.5,
                        fontWeight: 600,
                        borderRadius: 5,
                        border: fringeColorType === p.id ? '1.5px solid #10b981' : '1px solid rgba(255,255,255,0.1)',
                        background: fringeColorType === p.id ? 'rgba(16, 185, 129, 0.25)' : 'rgba(0,0,0,0.3)',
                        color: fringeColorType === p.id ? '#4ade80' : '#94a3b8',
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        gap: 3,
                        boxSizing: 'border-box',
                      }}
                    >
                      <span style={{ width: 7, height: 7, borderRadius: '50%', background: p.color, border: p.id === 'pure_white' ? '1px solid #666' : 'none' }} />
                      {p.label}
                    </button>
                  ))}
                </div>

                {/* Eyedropper & Custom Hex Input for Fringe */}
                <div style={{ display: 'flex', gap: 5, alignItems: 'center', background: 'rgba(0,0,0,0.35)', padding: 5, borderRadius: 5, border: '1px solid rgba(255,255,255,0.06)' }}>
                  <button
                    onClick={() => {
                      if (setEyedropperTarget) setEyedropperTarget('fringe');
                      if (setIsEyedropperActive) setIsEyedropperActive(!isEyedropperActive);
                    }}
                    style={{
                      flex: 1,
                      height: 30,
                      fontSize: 10,
                      fontWeight: 600,
                      borderRadius: 4,
                      background: isEyedropperActive && eyedropperTarget === 'fringe'
                        ? 'linear-gradient(135deg, #10b981 0%, #059669 100%)'
                        : 'linear-gradient(135deg, rgba(16,185,129,0.2) 0%, rgba(5,150,105,0.2) 100%)',
                      color: isEyedropperActive && eyedropperTarget === 'fringe' ? '#ffffff' : '#4ade80',
                      border: isEyedropperActive && eyedropperTarget === 'fringe' ? '1.5px solid #34d399' : '1px solid rgba(16,185,129,0.4)',
                      boxShadow: isEyedropperActive && eyedropperTarget === 'fringe' ? '0 0 10px rgba(16,185,129,0.5)' : 'none',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      gap: 5,
                      boxSizing: 'border-box',
                    }}
                    title="Hút màu trực tiếp tại mép pixel bị sượng/sạn trên ảnh"
                  >
                    <Pipette size={13} />
                    {isEyedropperActive && eyedropperTarget === 'fringe' ? '🎯 Đang hút màu viền...' : '💧 Hút màu viền rác'}
                  </button>

                  <input
                    type="color"
                    value={fringeColorType === 'chroma_green' ? '#00ff00' : fringeColorType === 'pure_white' ? '#ffffff' : fringeColorType === 'pure_black' ? '#000000' : fringeColorHex}
                    onChange={(e) => {
                      if (setFringeColorType) setFringeColorType('custom');
                      if (setFringeColorHex) setFringeColorHex(e.target.value);
                      if (onCommitSliderChange) onCommitSliderChange({ fringeColorType: 'custom', fringeColorHex: e.target.value });
                    }}
                    style={{ width: 30, height: 30, padding: 0, border: 'none', borderRadius: 4, cursor: 'pointer', background: 'transparent' }}
                    title="Bảng chọn mã màu rác"
                  />

                  <input
                    type="text"
                    value={fringeColorHex}
                    onChange={(e) => {
                      if (setFringeColorType) setFringeColorType('custom');
                      if (setFringeColorHex) setFringeColorHex(e.target.value);
                      if (onCommitSliderChange && e.target.value.length === 7) {
                        onCommitSliderChange({ fringeColorType: 'custom', fringeColorHex: e.target.value });
                      }
                    }}
                    style={{ width: 68, height: 30, padding: '0 5px', fontSize: 10, background: '#090e1a', color: '#4ade80', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4, fontFamily: 'monospace', textAlign: 'center', fontWeight: 600 }}
                    placeholder="#00FF00"
                  />
                </div>

                {/* Defringe Strength Slider */}
                <div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#cbd5e1', marginBottom: 2 }}>
                    <span style={{ fontWeight: 600, color: '#4ade80' }}>⚡ Độ mạnh khử viền (Defringe):</span>
                    <span style={{ color: '#4ade80', fontWeight: 700 }}>{defringeStrength}%</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                    <input
                      type="range"
                      min="0"
                      max="100"
                      value={defringeStrength}
                      onChange={(e) => setDefringeStrength && setDefringeStrength(parseInt(e.target.value))}
                      onPointerUp={(e) => {
                        const val = parseInt((e.target as HTMLInputElement).value);
                        if (onCommitSliderChange) onCommitSliderChange({ defringeStrength: val });
                      }}
                      onTouchEnd={(e) => {
                        const val = parseInt((e.target as HTMLInputElement).value);
                        if (onCommitSliderChange) onCommitSliderChange({ defringeStrength: val });
                      }}
                      style={{ flex: 1, accentColor: '#4ade80' }}
                    />
                    <div style={{ display: 'flex', gap: 2 }}>
                      {[30, 60, 90].map((v) => (
                        <button
                          key={v}
                          onClick={() => {
                            if (setDefringeStrength) setDefringeStrength(v);
                            if (onCommitSliderChange) onCommitSliderChange({ defringeStrength: v });
                          }}
                          style={{ height: 20, padding: '0 5px', fontSize: 8.5, fontWeight: 600, background: defringeStrength === v ? '#10b981' : 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}
                        >
                          {v}%
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* SECTION 2: GỌT & LÀM MỊN VIỀN SƯỢNG (EDGE CHOKE & SMOOTH) */}
            {(cleanupMode === 'all' || cleanupMode === 'smooth') && (
              <div style={{ background: 'rgba(15, 23, 42, 0.65)', padding: 8, borderRadius: 7, border: '1px solid rgba(56, 189, 248, 0.25)', display: 'flex', flexDirection: 'column', gap: 6 }}>
                {/* Edge Choke Slider */}
                <div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#cbd5e1', marginBottom: 2 }}>
                    <span style={{ fontWeight: 600, color: '#38bdf8' }} title="Lấn vào mép trong để xén sạch răng cưa hoặc viền màu bám dính">
                      ✂️ Gọt lùi viền sượng (Edge Choke):
                    </span>
                    <span style={{ color: '#38bdf8', fontWeight: 700 }}>{edgeChoke}px</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                    <input
                      type="range"
                      min="0"
                      max="5"
                      step="1"
                      value={edgeChoke}
                      onChange={(e) => setEdgeChoke && setEdgeChoke(parseInt(e.target.value))}
                      onPointerUp={(e) => {
                        const val = parseInt((e.target as HTMLInputElement).value);
                        if (onCommitSliderChange) onCommitSliderChange({ edgeChoke: val });
                      }}
                      onTouchEnd={(e) => {
                        const val = parseInt((e.target as HTMLInputElement).value);
                        if (onCommitSliderChange) onCommitSliderChange({ edgeChoke: val });
                      }}
                      style={{ flex: 1, accentColor: '#38bdf8' }}
                    />
                    <div style={{ display: 'flex', gap: 2 }}>
                      {[0, 1, 2].map((v) => (
                        <button
                          key={v}
                          onClick={() => {
                            if (setEdgeChoke) setEdgeChoke(v);
                            if (onCommitSliderChange) onCommitSliderChange({ edgeChoke: v });
                          }}
                          style={{ height: 20, padding: '0 6px', fontSize: 8.5, fontWeight: 600, background: edgeChoke === v ? '#0284c7' : 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}
                        >
                          {v}px
                        </button>
                      ))}
                    </div>
                  </div>
                </div>

                {/* Edge Smooth Slider */}
                <div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#cbd5e1', marginBottom: 2 }}>
                    <span style={{ fontWeight: 600, color: '#38bdf8' }} title="Khử bậc thang pixel, tạo đường bao viền mềm mại tự nhiên">
                      ✨ Làm mịn & Khử răng cưa viền:
                    </span>
                    <span style={{ color: '#38bdf8', fontWeight: 700 }}>{edgeSmooth}px</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                    <input
                      type="range"
                      min="0"
                      max="10"
                      step="1"
                      value={edgeSmooth}
                      onChange={(e) => setEdgeSmooth && setEdgeSmooth(parseInt(e.target.value))}
                      onPointerUp={(e) => {
                        const val = parseInt((e.target as HTMLInputElement).value);
                        if (onCommitSliderChange) onCommitSliderChange({ edgeSmooth: val });
                      }}
                      onTouchEnd={(e) => {
                        const val = parseInt((e.target as HTMLInputElement).value);
                        if (onCommitSliderChange) onCommitSliderChange({ edgeSmooth: val });
                      }}
                      style={{ flex: 1, accentColor: '#38bdf8' }}
                    />
                    <div style={{ display: 'flex', gap: 2 }}>
                      {[0, 2, 5].map((v) => (
                        <button
                          key={v}
                          onClick={() => {
                            if (setEdgeSmooth) setEdgeSmooth(v);
                            if (onCommitSliderChange) onCommitSliderChange({ edgeSmooth: v });
                          }}
                          style={{ height: 20, padding: '0 6px', fontSize: 8.5, fontWeight: 600, background: edgeSmooth === v ? '#0284c7' : 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}
                        >
                          {v}px
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* SECTION 3: LỌC BỤI & ĐỐM VỤN (NOISE & ISLAND FILTER) */}
            {(cleanupMode === 'all' || cleanupMode === 'despeckle') && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
                {/* Despeckle Size */}
                <div style={{ background: 'rgba(0,0,0,0.25)', padding: 7, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#cbd5e1', marginBottom: 2 }}>
                    <span style={{ fontWeight: 600, color: '#4ade80' }}>🧹 Khử đốm rác nhỏ (Despeckle):</span>
                    <span style={{ color: '#4ade80', fontWeight: 700 }}>{despeckleSize}px</span>
                  </div>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    value={despeckleSize}
                    onChange={(e) => setDespeckleSize(parseInt(e.target.value))}
                    onPointerUp={(e) => {
                      const val = parseInt((e.target as HTMLInputElement).value);
                      if (onCommitSliderChange) onCommitSliderChange({ despeckleSize: val });
                    }}
                    onTouchEnd={(e) => {
                      const val = parseInt((e.target as HTMLInputElement).value);
                      if (onCommitSliderChange) onCommitSliderChange({ despeckleSize: val });
                    }}
                    style={{ width: '100%', accentColor: '#4ade80' }}
                  />
                </div>

                {/* White Speckle Sensitivity */}
                <div style={{ background: 'rgba(0,0,0,0.25)', padding: 7, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#cbd5e1', marginBottom: 2 }}>
                    <span style={{ fontWeight: 600, color: '#4ade80' }}>✨ Khử hạt bụi sáng:</span>
                    <span style={{ color: '#4ade80', fontWeight: 700 }}>{whiteSpeckleSensitivity}%</span>
                  </div>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    value={whiteSpeckleSensitivity}
                    onChange={(e) => setWhiteSpeckleSensitivity(parseInt(e.target.value))}
                    onPointerUp={(e) => {
                      const val = parseInt((e.target as HTMLInputElement).value);
                      if (onCommitSliderChange) onCommitSliderChange({ whiteSpeckleSensitivity: val });
                    }}
                    onTouchEnd={(e) => {
                      const val = parseInt((e.target as HTMLInputElement).value);
                      if (onCommitSliderChange) onCommitSliderChange({ whiteSpeckleSensitivity: val });
                    }}
                    style={{ width: '100%', accentColor: '#4ade80' }}
                  />
                </div>

                {/* Keep Largest Island Checkbox */}
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 9.5, color: '#cbd5e1', cursor: 'pointer', background: 'rgba(0,0,0,0.3)', padding: 6, borderRadius: 5, border: '1px solid rgba(255,255,255,0.06)' }}>
                  <input
                    type="checkbox"
                    checked={keepLargestIslandOnly}
                    onChange={(e) => {
                      setKeepLargestIslandOnly(e.target.checked);
                      if (onCommitSliderChange) onCommitSliderChange({ keepLargestIslandOnly: e.target.checked });
                    }}
                    style={{ accentColor: '#4ade80' }}
                  />
                  🏝️ Chỉ giữ cụm linh kiện lớn nhất (Xóa sạch bụi phụ)
                </label>
              </div>
            )}

            {/* Quick Action Button - Synchronized height: 34px */}
            <button
              onClick={() => {
                if (onRunDespeckleOnly) onRunDespeckleOnly();
                else onAutoSliceAndAssemble();
              }}
              disabled={isProcessing}
              style={{
                width: '100%',
                height: 34,
                fontSize: 11,
                fontWeight: 700,
                borderRadius: 6,
                background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)',
                color: '#ffffff',
                border: '1px solid rgba(52, 211, 153, 0.4)',
                cursor: isProcessing ? 'not-allowed' : 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 5,
                boxShadow: '0 2px 10px rgba(16, 185, 129, 0.35)',
                boxSizing: 'border-box',
                letterSpacing: '0.1px',
              }}
              title="Áp dụng ngay bộ lọc khử màu bám, gọt viền và làm mượt răng cưa"
            >
              <Sparkles size={14} />
              🧹 Áp dụng khử rác & Làm mượt viền ngay
            </button>
          </div>
        ) : bgCleanupSubTab === 'ai_matting' ? (
          /* ---------------- SUB-TAB 3: AI MATTING (GPU) ---------------- */
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {/* Server Status Header */}
            <div
              style={{
                background: aiServerStatus === 'online' ? 'rgba(34, 197, 94, 0.15)' : 'rgba(239, 68, 68, 0.15)',
                border: aiServerStatus === 'online' ? '1px solid #22c55e' : '1px solid #ef4444',
                padding: '6px 8px',
                borderRadius: 6,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: 9.5, fontWeight: 600, color: aiServerStatus === 'online' ? '#4ade80' : '#f87171' }}>
                <span style={{ width: 8, height: 8, borderRadius: '50%', background: aiServerStatus === 'online' ? '#22c55e' : '#ef4444', display: 'inline-block', boxShadow: aiServerStatus === 'online' ? '0 0 8px #22c55e' : 'none' }}></span>
                {aiServerStatus === 'online' ? 'GPU CUDA sẵn sàng (RTX 3060)' : 'Chưa khởi động Server AI Local'}
              </div>
              <span style={{ fontSize: 8.5, color: '#94a3b8' }}>:5000</span>
            </div>

            {/* Model Selection */}
            <div>
              <div style={{ fontSize: 9.5, color: '#c084fc', marginBottom: 4, fontWeight: 600, display: 'flex', alignItems: 'center', gap: 4 }}>
                <Sparkles size={12} /> Chọn Model AI chuyên dụng:
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                {[
                  { id: 'isnet-anime', name: '🌸 ISNet-Anime', desc: 'Chuyên bóc tách Anime, 2D Sprite & Lineart', tag: '⚡ 0.15s' },
                  { id: 'birefnet-general', name: '🌟 BiRefNet General', desc: 'SOTA 2025 - Chuẩn từng sợi tóc & viền mềm', tag: '💎 Chuẩn nét' },
                  { id: 'u2net', name: '⚡ U2Net Standard', desc: 'Bóc tách nền tổng quát cân bằng', tag: '⚡ 0.15s' },
                  { id: 'birefnet-portrait', name: '👑 BiRefNet Portrait', desc: 'Chuyên chân dung, mái tóc & trang phục', tag: 'Chân Dung' },
                ].map((m) => (
                  <div
                    key={m.id}
                    onClick={() => setAiModel && setAiModel(m.id)}
                    style={{
                      background: aiModel === m.id ? 'rgba(168, 85, 247, 0.25)' : 'rgba(0,0,0,0.3)',
                      border: aiModel === m.id ? '1.5px solid #a855f7' : '1px solid rgba(255,255,255,0.06)',
                      borderRadius: 5,
                      padding: '5px 7px',
                      cursor: 'pointer',
                      display: 'flex',
                      flexDirection: 'column',
                      gap: 2,
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <span style={{ fontSize: 10, fontWeight: 600, color: aiModel === m.id ? '#f3e8ff' : '#cbd5e1' }}>{m.name}</span>
                      <span style={{ fontSize: 8, padding: '1px 4px', borderRadius: 3, background: aiModel === m.id ? '#9333ea' : 'rgba(255,255,255,0.1)', color: '#fff' }}>{m.tag}</span>
                    </div>
                    <div style={{ fontSize: 8.5, color: aiModel === m.id ? '#d8b4fe' : '#64748b' }}>{m.desc}</div>
                  </div>
                ))}
              </div>
            </div>

            {/* Scope Selection - Synchronized height: 30px */}
            <div>
              <div style={{ fontSize: 9.5, color: '#94a3b8', marginBottom: 4, fontWeight: 600 }}>Phạm vi xử lý:</div>
              <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr 1fr', gap: 5 }}>
                <button
                  onClick={() => setAiScope && setAiScope('full_image')}
                  style={{
                    height: 30,
                    fontSize: 9.5,
                    fontWeight: 600,
                    borderRadius: 5,
                    border: aiScope === 'full_image' ? '1.5px solid #a855f7' : '1px solid rgba(255,255,255,0.1)',
                    background: aiScope === 'full_image' ? 'rgba(168, 85, 247, 0.3)' : 'rgba(0,0,0,0.3)',
                    color: aiScope === 'full_image' ? '#f3e8ff' : '#cbd5e1',
                    cursor: 'pointer',
                    boxSizing: 'border-box',
                  }}
                >
                  🖼️ Toàn bộ ảnh
                </button>
                <button
                  onClick={() => setAiScope && setAiScope('all')}
                  style={{
                    height: 30,
                    fontSize: 9.5,
                    fontWeight: 600,
                    borderRadius: 5,
                    border: aiScope === 'all' ? '1.5px solid #a855f7' : '1px solid rgba(255,255,255,0.1)',
                    background: aiScope === 'all' ? 'rgba(168, 85, 247, 0.25)' : 'rgba(0,0,0,0.3)',
                    color: aiScope === 'all' ? '#f3e8ff' : '#94a3b8',
                    cursor: 'pointer',
                    boxSizing: 'border-box',
                  }}
                >
                  🧩 Từng ô ({totalCellCount})
                </button>
                <button
                  onClick={() => setAiScope && setAiScope('selected')}
                  style={{
                    height: 30,
                    fontSize: 9.5,
                    fontWeight: 600,
                    borderRadius: 5,
                    border: aiScope === 'selected' ? '1.5px solid #a855f7' : '1px solid rgba(255,255,255,0.1)',
                    background: aiScope === 'selected' ? 'rgba(168, 85, 247, 0.25)' : 'rgba(0,0,0,0.3)',
                    color: aiScope === 'selected' ? '#f3e8ff' : '#94a3b8',
                    cursor: 'pointer',
                    boxSizing: 'border-box',
                  }}
                >
                  🎯 Ô đang chọn
                </button>
              </div>
            </div>

            {/* Run AI Button - Synchronized height: 36px */}
            <button
              onClick={onRunAIMatting}
              disabled={isAIRunning}
              style={{
                width: '100%',
                height: 36,
                fontSize: 11,
                fontWeight: 700,
                borderRadius: 6,
                background: isAIRunning
                  ? 'rgba(255,255,255,0.1)'
                  : 'linear-gradient(135deg, #9333ea 0%, #d946ef 100%)',
                color: '#ffffff',
                border: '1px solid rgba(255,255,255,0.3)',
                cursor: isAIRunning ? 'not-allowed' : 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 6,
                boxShadow: '0 3px 12px rgba(147, 51, 234, 0.45)',
                boxSizing: 'border-box',
                letterSpacing: '0.1px',
              }}
            >
              {isAIRunning ? (
                <>
                  <RefreshCw size={14} className="animate-spin" /> Đang chạy Model AI GPU...
                </>
              ) : (
                <>
                  <Sparkles size={14} /> 🚀 Tách nền bằng Model AI (GPU)
                </>
              )}
            </button>

            {/* Quick guide to start server */}
            {aiServerStatus !== 'online' && (
              <div style={{ background: 'rgba(0,0,0,0.4)', padding: 7, borderRadius: 5, border: '1px dashed #a855f7', fontSize: 8.5, color: '#d8b4fe' }}>
                <div style={{ fontWeight: 600, marginBottom: 3, color: '#f3e8ff' }}>💡 Cách chạy Server AI trên máy:</div>
                <div>Chạy file <code style={{ background: '#1e1b4b', padding: '1px 4px', borderRadius: 3, color: '#38bdf8' }}>run_ai_matting_server.bat</code> trong thư mục dự án.</div>
              </div>
            )}
          </div>
        ) : null}
      </div>

      {/* ========================================================
          CARD 3: HÀNH ĐỘNG CHÍNH & LẮP RÁP 3D (ACTION BAR)
         ======================================================== */}
      <div
        style={{
          background: 'linear-gradient(180deg, rgba(24, 34, 53, 0.7) 0%, rgba(15, 23, 42, 0.8) 100%)',
          borderRadius: 8,
          border: '1px solid rgba(56, 189, 248, 0.25)',
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 7,
          boxShadow: '0 4px 12px rgba(0,0,0,0.3)',
        }}
      >
        <div style={{ fontSize: 11.5, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6, borderBottom: '1px solid rgba(255,255,255,0.06)', paddingBottom: 6, letterSpacing: '0.2px' }}>
          <Sparkles size={14} color="#38bdf8" /> 3. Xuất bản & Lắp ráp 3D
        </div>

        {/* Hero Button - Synchronized height: 40px */}
        <button
          onClick={onAutoSliceAndAssemble}
          disabled={isProcessing}
          style={{
            width: '100%',
            height: 40,
            fontSize: 12,
            fontWeight: 700,
            borderRadius: 8,
            background: isProcessing
              ? 'rgba(255,255,255,0.1)'
              : 'linear-gradient(135deg, #0284c7 0%, #2563eb 50%, #7c3aed 100%)',
            color: '#ffffff',
            border: '1px solid rgba(255,255,255,0.25)',
            cursor: isProcessing ? 'not-allowed' : 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 7,
            boxShadow: '0 4px 16px rgba(37, 99, 235, 0.45)',
            boxSizing: 'border-box',
            letterSpacing: '0.2px',
          }}
        >
          {isProcessing ? (
            <>
              <RefreshCw size={15} className="animate-spin" /> Đang bóc tách từng ô...
            </>
          ) : assemblySuccess ? (
            <>
              <Check size={15} /> ✓ Đã tách ({slicedCount}/{totalCellCount} ô) & Lắp 3D!
            </>
          ) : (
            <>
              <Sparkles size={15} /> ⚡ Bóc tách & Lắp ráp 3D tự động
            </>
          )}
        </button>

        {/* Action Row 2 (Save Kit & Catalog Modal) - Synchronized height: 32px */}
        <div style={{ display: 'grid', gridTemplateColumns: slicedCount > 0 ? '1fr 1fr' : '1fr', gap: 6 }}>
          {slicedCount > 0 && (
            <button
              onClick={onOpenSaveKitModal}
              style={{
                height: 32,
                fontSize: 10.5,
                fontWeight: 600,
                borderRadius: 6,
                background: 'linear-gradient(135deg, #8b5cf6 0%, #6d28d9 100%)',
                color: '#ffffff',
                border: 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 5,
                boxShadow: '0 2px 10px rgba(139, 92, 246, 0.35)',
                boxSizing: 'border-box',
              }}
            >
              <Save size={13} /> 💾 Lưu bộ Kit ({slicedCount})
            </button>
          )}

          {onOpenCatalogModal && (
            <button
              onClick={onOpenCatalogModal}
              style={{
                height: 32,
                fontSize: 10.5,
                fontWeight: 500,
                borderRadius: 6,
                background: 'rgba(255,255,255,0.06)',
                color: '#cbd5e1',
                border: '1px solid rgba(255,255,255,0.12)',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 5,
                boxSizing: 'border-box',
              }}
            >
              📦 Mở kho linh kiện (Catalog)
            </button>
          )}
        </div>

        {/* Commit and Lock as New Base Image (if sliced) */}
        {onApplyAsNewBaseImage && slicedCount > 0 && (
          <button
            onClick={onApplyAsNewBaseImage}
            style={{
              width: '100%',
              height: 32,
              fontSize: 10.5,
              fontWeight: 700,
              borderRadius: 6,
              background: 'linear-gradient(135deg, #0d9488 0%, #059669 100%)',
              color: '#ffffff',
              border: '1px solid #2dd4bf',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
              boxShadow: '0 2px 10px rgba(13, 148, 136, 0.35)',
              boxSizing: 'border-box',
              letterSpacing: '0.1px',
            }}
            title="Lưu kết quả bóc tách hiện tại thành ảnh gốc mới để tiếp tục xử lý các màu nền/chi tiết khác mà không bị áp dụng lại các bộ lọc cũ"
          >
            💾 Xác nhận & Lưu làm mốc gốc mới (Commit Base)
          </button>
        )}
      </div>
    </div>
  );
};
