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
  onResetToDemoImage: (key?: 'default' | 'chibi') => void;
  fileInputRef: React.RefObject<HTMLInputElement>;
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
  fileInputRef,
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
        background: '#090e1a',
        padding: 12,
        borderRadius: 10,
        border: '1px solid rgba(255,255,255,0.08)',
        overflowY: 'auto',
        color: '#e2e8f0',
      }}
    >
      {/* ========================================================
          CARD 1: BẢNG LINH KIỆN CẦN CẮT (GRID CATEGORY & IMAGE)
         ======================================================== */}
      <div
        style={{
          background: 'linear-gradient(180deg, rgba(30, 41, 59, 0.7) 0%, rgba(15, 23, 42, 0.8) 100%)',
          borderRadius: 8,
          border: '1px solid rgba(56, 189, 248, 0.25)',
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 8,
          boxShadow: '0 4px 12px rgba(0,0,0,0.25)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ fontSize: 11, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Layers size={14} color="#38bdf8" /> 1. CHẾ ĐỘ XỬ LÝ ẢNH
          </div>
          <span
            style={{
              fontSize: 9,
              fontWeight: 700,
              padding: '2px 6px',
              borderRadius: 4,
              background: selectedCatId === 'single_full_image' ? 'rgba(34, 197, 94, 0.2)' : 'rgba(56, 189, 248, 0.15)',
              color: selectedCatId === 'single_full_image' ? '#4ade80' : '#38bdf8',
              border: selectedCatId === 'single_full_image' ? '1px solid #22c55e' : '1px solid rgba(56, 189, 248, 0.3)',
            }}
          >
            {selectedCatId === 'single_full_image' ? '🖼️ 1 Ảnh Đơn Hoàn Chỉnh' : `${currentCat.rows} Hàng × ${currentCat.cols} Cột`}
          </span>
        </div>

        {/* Nút Bật / Tắt Khung Lưới (Toggle Single Image vs Grid Slicer) */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 5 }}>
          <button
            onClick={() => onSelectCatId('single_full_image')}
            style={{
              padding: '7px 4px',
              fontSize: 10,
              fontWeight: 800,
              borderRadius: 6,
              border: selectedCatId === 'single_full_image' ? '1.5px solid #22c55e' : '1px solid rgba(255,255,255,0.1)',
              background: selectedCatId === 'single_full_image' ? 'linear-gradient(135deg, #15803d, #166534)' : 'rgba(0,0,0,0.3)',
              color: selectedCatId === 'single_full_image' ? '#ffffff' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              boxShadow: selectedCatId === 'single_full_image' ? '0 2px 8px rgba(34, 197, 94, 0.4)' : 'none',
            }}
            title="Tắt khung lưới, xử lý toàn bộ bức ảnh tải lên như 1 vật thể hoàn chỉnh"
          >
            🖼️ ẢNH ĐƠN (TẮT LƯỚI)
          </button>
          <button
            onClick={() => {
              if (selectedCatId === 'single_full_image') {
                onSelectCatId('hair_multi_angle_grid');
              }
            }}
            style={{
              padding: '7px 4px',
              fontSize: 10,
              fontWeight: 800,
              borderRadius: 6,
              border: selectedCatId !== 'single_full_image' ? '1.5px solid #0284c7' : '1px solid rgba(255,255,255,0.1)',
              background: selectedCatId !== 'single_full_image' ? 'linear-gradient(135deg, #0284c7, #0369a1)' : 'rgba(0,0,0,0.3)',
              color: selectedCatId !== 'single_full_image' ? '#ffffff' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 4,
              boxShadow: selectedCatId !== 'single_full_image' ? '0 2px 8px rgba(2, 132, 199, 0.4)' : 'none',
            }}
            title="Bật khung lưới để cắt ảnh Sprite Sheet thành nhiều ô"
          >
            🔲 CẮT LƯỚI SPRITE
          </button>
        </div>

        {/* Dropdown Category Selector */}
        <select
          value={selectedCatId}
          onChange={(e) => onSelectCatId(e.target.value)}
          style={{
            width: '100%',
            padding: '7px 9px',
            fontSize: 11,
            background: '#0b1329',
            color: '#38bdf8',
            border: '1.5px solid #0284c7',
            borderRadius: 6,
            fontWeight: 700,
            cursor: 'pointer',
            outline: 'none',
          }}
        >
          {GRID_CATEGORY_DEFINITIONS.map((cat) => (
            <option key={cat.id} value={cat.id}>
              {cat.label} ({cat.rows}x{cat.cols})
            </option>
          ))}
        </select>

        {/* Image Source & Upload Controls */}
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          onChange={onFileUpload}
          style={{ display: 'none' }}
        />

        <div style={{ display: 'flex', flexDirection: 'column', gap: 5, marginTop: 2 }}>
          <button
            onClick={() => fileInputRef.current?.click()}
            style={{
              width: '100%',
              padding: '7px 10px',
              fontSize: 11,
              fontWeight: 700,
              borderRadius: 6,
              background: 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)',
              color: '#ffffff',
              border: 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
              boxShadow: '0 2px 8px rgba(2, 132, 199, 0.35)',
            }}
          >
            <Upload size={13} /> {userUploadedImageUrl ? '📤 Tải Ảnh Khác' : '📤 Tải Ảnh Sprite Sheet Lên'}
          </button>

          <div style={{ display: 'flex', gap: 4 }}>
            <button
              onClick={() => onResetToDemoImage('chibi')}
              style={{
                flex: 1,
                padding: '4px 6px',
                fontSize: 9.5,
                fontWeight: 700,
                borderRadius: 4,
                background: 'rgba(16, 185, 129, 0.15)',
                color: '#6ee7b7',
                border: '1px solid rgba(16, 185, 129, 0.35)',
                cursor: 'pointer',
              }}
              title="Dùng ảnh mẫu bóc tách tóc Chibi 4x5"
            >
              🌟 Mẫu Chibi 4x5
            </button>

            <button
              onClick={() => onResetToDemoImage('default')}
              style={{
                flex: 1,
                padding: '4px 6px',
                fontSize: 9.5,
                fontWeight: 600,
                borderRadius: 4,
                background: 'rgba(255,255,255,0.06)',
                color: '#cbd5e1',
                border: '1px solid rgba(255,255,255,0.1)',
                cursor: 'pointer',
              }}
              title="Dùng ảnh mẫu tóc Kiếm Khách"
            >
              🗡️ Mẫu Kiếm Khách
            </button>
          </div>
        </div>

        {/* Padding Inset (Thu lùi mép viền khung ô) */}
        <div style={{ background: 'rgba(0,0,0,0.3)', padding: 7, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)', marginTop: 2 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
            <span style={{ fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
              ✂️ Thu Viền Ô (Padding Inset):
            </span>
            <span style={{ color: '#4ade80', fontWeight: 700 }}>{paddingInset}px</span>
          </div>
          <div style={{ fontSize: 8.5, color: '#94a3b8', marginBottom: 4 }}>
            Thụt lùi 4 mép cắt vào trong để xén bỏ viền kẻ đen / khung ô của AI.
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
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
                style={{ flex: 1 }}
              />
            )}
            {setPaddingInset && (
              <>
                <button onClick={() => { setPaddingInset(0); if (onCommitSliderChange) onCommitSliderChange(); }} style={{ padding: '2px 4px', fontSize: 9, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 3, cursor: 'pointer' }}>0</button>
                <button onClick={() => { setPaddingInset(4); if (onCommitSliderChange) onCommitSliderChange(); }} style={{ padding: '2px 4px', fontSize: 9, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>4px</button>
                <button onClick={() => { setPaddingInset(8); if (onCommitSliderChange) onCommitSliderChange(); }} style={{ padding: '2px 4px', fontSize: 9, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>8px</button>
              </>
            )}
          </div>
        </div>
      </div>

      {/* ========================================================
          CARD 2: TÁCH NỀN & TINH CHỈNH ĐƯỜNG VIỀN (CHROMA & EDGES)
         ======================================================== */}
      <div
        style={{
          background: 'linear-gradient(180deg, rgba(30, 41, 59, 0.7) 0%, rgba(15, 23, 42, 0.8) 100%)',
          borderRadius: 8,
          border: '1px solid rgba(34, 197, 94, 0.25)',
          padding: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 8,
          boxShadow: '0 4px 12px rgba(0,0,0,0.25)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ fontSize: 11, fontWeight: 800, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Scissors size={14} color="#4ade80" /> 2. TÁCH NỀN & TINH CHỈNH VIỀN
          </div>
        </div>

        {/* Workflow Toolbar: Xử Lý Tiếp & Áp Dụng Làm Ảnh Gốc */}
        <div
          style={{
            background: isCumulativeProcessing ? 'rgba(234, 179, 8, 0.12)' : 'rgba(0,0,0,0.3)',
            padding: 7,
            borderRadius: 6,
            border: isCumulativeProcessing ? '1.5px solid #eab308' : '1px solid rgba(255,255,255,0.08)',
            display: 'flex',
            flexDirection: 'column',
            gap: 5,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: 9.5, fontWeight: 700, color: isCumulativeProcessing ? '#facc15' : '#cbd5e1', display: 'flex', alignItems: 'center', gap: 4 }}>
              ⚡ Chế Độ Xử Lý Tiếp:
            </span>
            {setIsCumulativeProcessing && (
              <button
                onClick={() => setIsCumulativeProcessing(!isCumulativeProcessing)}
                style={{
                  padding: '3px 8px',
                  fontSize: 9.5,
                  fontWeight: 700,
                  borderRadius: 4,
                  border: 'none',
                  background: isCumulativeProcessing ? '#eab308' : 'rgba(255,255,255,0.1)',
                  color: isCumulativeProcessing ? '#0f172a' : '#94a3b8',
                  cursor: 'pointer',
                  boxShadow: isCumulativeProcessing ? '0 0 10px rgba(234, 179, 8, 0.4)' : 'none',
                }}
              >
                {isCumulativeProcessing ? '✓ ĐANG BẬT' : '○ TẮT'}
              </button>
            )}
          </div>
          <div style={{ fontSize: 8.5, color: isCumulativeProcessing ? '#fef08a' : '#64748b', lineHeight: 1.25 }}>
            {isCumulativeProcessing
              ? '✓ Bật xử lý tiếp: Giữ nguyên kết quả cắt trước, cho phép chọn màu khác để bóc tách thêm nhiều tầng.'
              : '○ Đang tắt: Mỗi lần bóc tách sẽ làm sạch mới lại từ ảnh gốc.'}
          </div>

          {onApplyAsNewBaseImage && slicedCount > 0 && (
            <button
              onClick={onApplyAsNewBaseImage}
              style={{
                width: '100%',
                padding: '5px 8px',
                fontSize: 9.5,
                fontWeight: 700,
                borderRadius: 4,
                background: 'linear-gradient(135deg, #0d9488 0%, #059669 100%)',
                color: '#ffffff',
                border: '1px solid #14b8a6',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 5,
              }}
              title="Lưu đè kết quả đã cắt thành ảnh gốc mới"
            >
              📌 Áp Dụng Làm Ảnh Gốc Mới (Apply Base)
            </button>
          )}
        </div>

        {/* Color & Cleanup Sub-tabs */}
        <div style={{ display: 'flex', gap: 3, background: 'rgba(0,0,0,0.4)', padding: 2, borderRadius: 5 }}>
          <button
            onClick={() => setBgCleanupSubTab('chroma')}
            style={{
              flex: 1,
              padding: '5px 2px',
              fontSize: 9.5,
              fontWeight: 700,
              borderRadius: 4,
              border: 'none',
              background: bgCleanupSubTab === 'chroma' ? '#0284c7' : 'transparent',
              color: bgCleanupSubTab === 'chroma' ? '#ffffff' : '#94a3b8',
              cursor: 'pointer',
            }}
          >
            🎨 1. Tách Màu
          </button>
          <button
            onClick={() => setBgCleanupSubTab('despeckle')}
            style={{
              flex: 1,
              padding: '5px 2px',
              fontSize: 9.5,
              fontWeight: 700,
              borderRadius: 4,
              border: 'none',
              background: bgCleanupSubTab === 'despeckle' ? '#0284c7' : 'transparent',
              color: bgCleanupSubTab === 'despeckle' ? '#ffffff' : '#94a3b8',
              cursor: 'pointer',
            }}
          >
            🧹 2. Khử Rác
          </button>
          <button
            onClick={() => setBgCleanupSubTab('ai_matting')}
            style={{
              flex: 1.2,
              padding: '5px 2px',
              fontSize: 9.5,
              fontWeight: 700,
              borderRadius: 4,
              border: 'none',
              background: bgCleanupSubTab === 'ai_matting' ? 'linear-gradient(135deg, #8b5cf6, #d946ef)' : 'transparent',
              color: bgCleanupSubTab === 'ai_matting' ? '#ffffff' : '#c084fc',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 3,
            }}
          >
            🤖 3. AI Tách Nền (GPU)
          </button>
        </div>

        {bgCleanupSubTab === 'chroma' ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {/* Color Selection */}
            <div>
              <div style={{ fontSize: 9.5, color: '#94a3b8', marginBottom: 4, fontWeight: 700 }}>Chọn Màu Cần Tách:</div>
              <div style={{ display: 'flex', gap: 4 }}>
                <button
                  onClick={() => {
                    setKeyColorType('chroma_green');
                    if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'chroma_green' });
                  }}
                  style={{
                    flex: 1,
                    padding: '5px 3px',
                    fontSize: 9.5,
                    fontWeight: 700,
                    borderRadius: 4,
                    border: keyColorType === 'chroma_green' ? '1.5px solid #22c55e' : '1px solid rgba(255,255,255,0.1)',
                    background: keyColorType === 'chroma_green' ? 'rgba(34, 197, 94, 0.25)' : 'rgba(0,0,0,0.3)',
                    color: keyColorType === 'chroma_green' ? '#4ade80' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  🟢 Xanh Lá
                </button>
                <button
                  onClick={() => {
                    setKeyColorType('pure_white');
                    if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'pure_white' });
                  }}
                  style={{
                    flex: 1,
                    padding: '5px 3px',
                    fontSize: 9.5,
                    fontWeight: 700,
                    borderRadius: 4,
                    border: keyColorType === 'pure_white' ? '1.5px solid #ffffff' : '1px solid rgba(255,255,255,0.1)',
                    background: keyColorType === 'pure_white' ? 'rgba(255,255,255,0.2)' : 'rgba(0,0,0,0.3)',
                    color: keyColorType === 'pure_white' ? '#ffffff' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  ⚪ Trắng
                </button>
                <button
                  onClick={() => {
                    setKeyColorType('custom');
                    if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'custom', keyColorHex });
                  }}
                  style={{
                    flex: 1,
                    padding: '5px 3px',
                    fontSize: 9.5,
                    fontWeight: 700,
                    borderRadius: 4,
                    border: keyColorType === 'custom' ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    background: keyColorType === 'custom' ? 'rgba(56, 189, 248, 0.25)' : 'rgba(0,0,0,0.3)',
                    color: keyColorType === 'custom' ? '#38bdf8' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  🎨 Tự Chọn
                </button>
              </div>

              {keyColorType === 'custom' && (
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 5, background: 'rgba(0,0,0,0.3)', padding: 5, borderRadius: 5 }}>
                  <input
                    type="color"
                    value={keyColorHex}
                    onChange={(e) => {
                      setKeyColorHex(e.target.value);
                      if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'custom', keyColorHex: e.target.value });
                    }}
                    style={{ width: 28, height: 22, border: 'none', borderRadius: 4, cursor: 'pointer', background: 'transparent' }}
                  />
                  <input
                    type="text"
                    value={keyColorHex}
                    onChange={(e) => {
                      setKeyColorHex(e.target.value);
                      if (onCommitSliderChange && e.target.value.length === 7) {
                        onCommitSliderChange({ keyColorType: 'custom', keyColorHex: e.target.value });
                      }
                    }}
                    style={{ flex: 1, padding: '3px 6px', fontSize: 10, background: '#090e1a', color: '#38bdf8', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 4, fontFamily: 'monospace' }}
                  />
                </div>
              )}
            </div>

            {/* Nút Tách Nền Siêu Tốc BFS (Chạy Trực Tiếp Không Cần Server) */}
            <button
              onClick={onRunFastBFSMatting}
              disabled={isProcessing}
              style={{
                width: '100%',
                padding: '9px 10px',
                fontSize: 11,
                fontWeight: 800,
                borderRadius: 6,
                background: 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)',
                color: '#ffffff',
                border: '1px solid rgba(56, 189, 248, 0.5)',
                cursor: isProcessing ? 'not-allowed' : 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 6,
                boxShadow: '0 3px 10px rgba(2, 132, 199, 0.4)',
              }}
              title="Tách sạch nền 100% bằng thuật toán BFS loang từ viền, bảo vệ tròng trắng mắt và quần áo trắng, không cần bật Server Python"
            >
              <Sparkles size={14} />
              ⚡ TÁCH NỀN SMART BFS (TRỰC TIẾP TRÊN TRÌNH DUYỆT)
            </button>

            {/* Isolation Mode */}
            <div>
              <div style={{ fontSize: 9.5, color: '#94a3b8', marginBottom: 4, fontWeight: 700 }}>Phạm Vi Bóc Tách:</div>
              <div style={{ display: 'flex', gap: 4 }}>
                <button
                  onClick={() => {
                    setIsolationMode('outer_only');
                    if (onCommitSliderChange) onCommitSliderChange({ isolationMode: 'outer_only' });
                  }}
                  style={{
                    flex: 1,
                    padding: '5px 3px',
                    fontSize: 9.5,
                    fontWeight: 700,
                    borderRadius: 4,
                    border: isolationMode === 'outer_only' ? '1.5px solid #eab308' : '1px solid rgba(255,255,255,0.1)',
                    background: isolationMode === 'outer_only' ? 'rgba(234, 179, 8, 0.25)' : 'rgba(0,0,0,0.3)',
                    color: isolationMode === 'outer_only' ? '#facc15' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                  title="Chỉ tách nền viền ngoài, bảo vệ điểm sáng bóng/phụ kiện bên trong"
                >
                  🔲 Tách Viền Ngoài (Bảo vệ thân)
                </button>
                <button
                  onClick={() => {
                    setIsolationMode('all');
                    if (onCommitSliderChange) onCommitSliderChange({ isolationMode: 'all' });
                  }}
                  style={{
                    flex: 1,
                    padding: '5px 3px',
                    fontSize: 9.5,
                    fontWeight: 700,
                    borderRadius: 4,
                    border: isolationMode === 'all' ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    background: isolationMode === 'all' ? 'rgba(56, 189, 248, 0.25)' : 'rgba(0,0,0,0.3)',
                    color: isolationMode === 'all' ? '#38bdf8' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                  title="Tách toàn bộ mọi khoảng màu trùng khớp"
                >
                  🌐 Tách Toàn Bộ
                </button>
              </div>
            </div>

            {/* Tolerance Slider */}
            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 7, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
                <span style={{ fontWeight: 700, color: '#38bdf8' }}>🎯 Độ Nhạy Tách Màu (Tolerance):</span>
                <span style={{ color: '#38bdf8', fontWeight: 800 }}>{tolerance}</span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                <input
                  type="range"
                  min="5"
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
                  style={{ flex: 1 }}
                />
                <button onClick={() => { setTolerance(25); if (onCommitSliderChange) onCommitSliderChange({ tolerance: 25 }); }} style={{ padding: '2px 4px', fontSize: 8.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>25</button>
                <button onClick={() => { setTolerance(38); if (onCommitSliderChange) onCommitSliderChange({ tolerance: 38 }); }} style={{ padding: '2px 4px', fontSize: 8.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>38</button>
                <button onClick={() => { setTolerance(55); if (onCommitSliderChange) onCommitSliderChange({ tolerance: 55 }); }} style={{ padding: '2px 4px', fontSize: 8.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>55</button>
              </div>
            </div>

            {/* Feather Slider */}
            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 7, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
                <span style={{ fontWeight: 700, color: '#38bdf8' }}>🪶 Làm Mềm Viền (Feather):</span>
                <span style={{ color: '#38bdf8', fontWeight: 800 }}>{feather}px</span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
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
                  style={{ flex: 1 }}
                />
                <button onClick={() => { setFeather(0); if (onCommitSliderChange) onCommitSliderChange({ feather: 0 }); }} style={{ padding: '2px 4px', fontSize: 8.5, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 3, cursor: 'pointer' }}>0</button>
                <button onClick={() => { setFeather(1); if (onCommitSliderChange) onCommitSliderChange({ feather: 1 }); }} style={{ padding: '2px 4px', fontSize: 8.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>1px</button>
                <button onClick={() => { setFeather(2); if (onCommitSliderChange) onCommitSliderChange({ feather: 2 }); }} style={{ padding: '2px 4px', fontSize: 8.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>2px</button>
              </div>
            </div>

            {/* Stroke / Thêm Viền Bao Quanh Theo Màu */}
            <div style={{ background: 'rgba(0,0,0,0.35)', padding: 8, borderRadius: 6, border: '1.5px solid rgba(56, 189, 248, 0.3)', display: 'flex', flexDirection: 'column', gap: 6 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontSize: 10, fontWeight: 800, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
                  <Palette size={13} color="#38bdf8" /> 🎨 THÊM VIỀN NÉT THEO MÀU (STROKE):
                </span>
                <span style={{ fontSize: 10, color: '#4ade80', fontWeight: 800 }}>{strokeWidth}px</span>
              </div>
              <div style={{ fontSize: 8.5, color: '#94a3b8', lineHeight: 1.25 }}>
                Đắp thêm một đường viền nét mịn ra ngoài theo màu bạn chọn bên dưới.
              </div>

              {/* Color Presets */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, flexWrap: 'wrap' }}>
                <span style={{ fontSize: 9, color: '#cbd5e1', fontWeight: 600 }}>Màu viền:</span>
                {setStrokeColorHex && (
                  <input
                    type="color"
                    value={strokeColorHex}
                    onChange={(e) => {
                      setStrokeColorHex(e.target.value);
                      if (onCommitSliderChange && strokeWidth > 0) onCommitSliderChange({ strokeColorHex: e.target.value });
                    }}
                    style={{ width: 24, height: 20, border: 'none', borderRadius: 3, cursor: 'pointer', background: 'transparent' }}
                    title="Chọn màu viền tùy ý"
                  />
                )}
                {setStrokeColorHex && (
                  <>
                    <button onClick={() => { setStrokeColorHex('#000000'); if (onCommitSliderChange && strokeWidth > 0) onCommitSliderChange({ strokeColorHex: '#000000' }); }} style={{ padding: '2px 5px', fontSize: 8.5, background: strokeColorHex === '#000000' ? '#0284c7' : 'rgba(0,0,0,0.6)', color: '#fff', border: '1px solid #475569', borderRadius: 3, cursor: 'pointer' }}>🖤 Đen</button>
                    <button onClick={() => { setStrokeColorHex('#ffffff'); if (onCommitSliderChange && strokeWidth > 0) onCommitSliderChange({ strokeColorHex: '#ffffff' }); }} style={{ padding: '2px 5px', fontSize: 8.5, background: strokeColorHex === '#ffffff' ? '#0284c7' : 'rgba(255,255,255,0.2)', color: '#fff', border: '1px solid #475569', borderRadius: 3, cursor: 'pointer' }}>🤍 Trắng</button>
                    <button onClick={() => { setStrokeColorHex('#2b1810'); if (onCommitSliderChange && strokeWidth > 0) onCommitSliderChange({ strokeColorHex: '#2b1810' }); }} style={{ padding: '2px 5px', fontSize: 8.5, background: strokeColorHex === '#2b1810' ? '#0284c7' : '#2b1810', color: '#fff', border: '1px solid #475569', borderRadius: 3, cursor: 'pointer' }}>🤎 Nâu Nét</button>
                    <button onClick={() => { setStrokeColorHex(keyColorHex); if (onCommitSliderChange && strokeWidth > 0) onCommitSliderChange({ strokeColorHex: keyColorHex }); }} style={{ padding: '2px 5px', fontSize: 8.5, background: strokeColorHex === keyColorHex ? '#0284c7' : 'rgba(255,255,255,0.1)', color: '#fff', border: '1px solid #475569', borderRadius: 3, cursor: 'pointer' }}>✨ Màu Nền</button>
                  </>
                )}
              </div>

              {/* Stroke Width Slider */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
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
                    style={{ flex: 1 }}
                  />
                )}
                {setStrokeWidth && (
                  <>
                    <button onClick={() => { setStrokeWidth(0); if (onCommitSliderChange) onCommitSliderChange({ strokeWidth: 0 }); }} style={{ padding: '2px 4px', fontSize: 8.5, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 3, cursor: 'pointer' }}>0</button>
                    <button onClick={() => { setStrokeWidth(1); if (onCommitSliderChange) onCommitSliderChange({ strokeWidth: 1, strokeColorHex }); }} style={{ padding: '2px 4px', fontSize: 8.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>1px</button>
                    <button onClick={() => { setStrokeWidth(2); if (onCommitSliderChange) onCommitSliderChange({ strokeWidth: 2, strokeColorHex }); }} style={{ padding: '2px 4px', fontSize: 8.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>2px</button>
                    <button onClick={() => { setStrokeWidth(4); if (onCommitSliderChange) onCommitSliderChange({ strokeWidth: 4, strokeColorHex }); }} style={{ padding: '2px 4px', fontSize: 8.5, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>4px</button>
                  </>
                )}
              </div>
            </div>
          </div>
        ) : bgCleanupSubTab === 'despeckle' ? (
          /* Sub-tab 2: Despeckle & Noise Filtering */
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 7, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
                <span style={{ fontWeight: 700, color: '#38bdf8' }}>🧹 Khử Đốm Rác Nhỏ (Despeckle):</span>
                <span style={{ color: '#38bdf8', fontWeight: 800 }}>{despeckleSize}px</span>
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
                style={{ width: '100%' }}
              />
            </div>

            <div style={{ background: 'rgba(0,0,0,0.25)', padding: 7, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
                <span style={{ fontWeight: 700, color: '#38bdf8' }}>✨ Khử Hạt Bụi Trắng:</span>
                <span style={{ color: '#38bdf8', fontWeight: 800 }}>{whiteSpeckleSensitivity}%</span>
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
                style={{ width: '100%' }}
              />
            </div>

            <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 10, color: '#cbd5e1', cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={keepLargestIslandOnly}
                onChange={(e) => {
                  setKeepLargestIslandOnly(e.target.checked);
                  if (onCommitSliderChange) onCommitSliderChange({ keepLargestIslandOnly: e.target.checked });
                }}
              />
              🏝️ Chỉ giữ cụm linh kiện lớn nhất (Xóa sạch bụi phụ)
            </label>
          </div>
        ) : bgCleanupSubTab === 'ai_matting' ? (
          /* Sub-tab 3: AI Matting (BiRefNet / RTX 3060) */
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
              <div style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: 9.5, fontWeight: 700, color: aiServerStatus === 'online' ? '#4ade80' : '#f87171' }}>
                <span style={{ width: 8, height: 8, borderRadius: '50%', background: aiServerStatus === 'online' ? '#22c55e' : '#ef4444', display: 'inline-block', boxShadow: aiServerStatus === 'online' ? '0 0 8px #22c55e' : 'none' }}></span>
                {aiServerStatus === 'online' ? 'GPU CUDA Sẵn Sàng (RTX 3060)' : 'Chưa Khởi Động Server AI Local'}
              </div>
              <span style={{ fontSize: 8.5, color: '#94a3b8' }}>:5000</span>
            </div>

            {/* Local Environment & Model Path Info Badge */}
            <div style={{ background: 'rgba(0,0,0,0.3)', padding: '5px 8px', borderRadius: 5, border: '1px solid rgba(255,255,255,0.06)', display: 'flex', flexDirection: 'column', gap: 2 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5 }}>
                <span style={{ color: '#94a3b8' }}>📁 Môi trường Python:</span>
                <span style={{ color: '#38bdf8', fontWeight: 700 }}>.venv (Cục bộ dự án)</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 8.5 }}>
                <span style={{ color: '#94a3b8' }}>🧠 Thư mục Model:</span>
                <span style={{ color: '#4ade80', fontWeight: 700 }}>./models/ai_matting/</span>
              </div>
            </div>

            {/* Model Selection */}
            <div>
              <div style={{ fontSize: 9.5, color: '#c084fc', marginBottom: 4, fontWeight: 700, display: 'flex', alignItems: 'center', gap: 4 }}>
                <Sparkles size={12} /> Chọn Model AI Chuyên Dụng:
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                {[
                  { id: 'isnet-anime', name: '🌸 ISNet-Anime', desc: 'Chuyên bóc tách Anime, 2D Sprite & Lineart (Nhanh nhất 0.15s)', tag: '⚡ Siêu Nhanh • 0.15s' },
                  { id: 'birefnet-general', name: '🌟 BiRefNet General', desc: 'SOTA 2025 - Chuẩn từng sợi tóc & viền mềm mại', tag: '💎 Chi Tiết Cực Cao' },
                  { id: 'u2net', name: '⚡ U2Net Standard', desc: 'Bóc tách nền tổng quát cân bằng', tag: '⚡ 0.15s' },
                  { id: 'birefnet-portrait', name: '👑 BiRefNet Portrait', desc: 'Chuyên chân dung, mái tóc & trang phục tinh xảo', tag: 'Chân Dung' },
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
                      <span style={{ fontSize: 10, fontWeight: 700, color: aiModel === m.id ? '#f3e8ff' : '#cbd5e1' }}>{m.name}</span>
                      <span style={{ fontSize: 8, padding: '1px 4px', borderRadius: 3, background: aiModel === m.id ? '#9333ea' : 'rgba(255,255,255,0.1)', color: '#fff' }}>{m.tag}</span>
                    </div>
                    <div style={{ fontSize: 8.5, color: aiModel === m.id ? '#d8b4fe' : '#64748b' }}>{m.desc}</div>
                  </div>
                ))}
              </div>
            </div>

            {/* Scope Selection */}
            <div>
              <div style={{ fontSize: 9.5, color: '#94a3b8', marginBottom: 4, fontWeight: 700 }}>Phạm Vi Xử Lý:</div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                <button
                  onClick={() => setAiScope && setAiScope('full_image')}
                  style={{
                    width: '100%',
                    padding: '6px 8px',
                    fontSize: 10,
                    fontWeight: 800,
                    borderRadius: 5,
                    border: aiScope === 'full_image' ? '1.5px solid #a855f7' : '1px solid rgba(255,255,255,0.1)',
                    background: aiScope === 'full_image' ? 'rgba(168, 85, 247, 0.3)' : 'rgba(0,0,0,0.3)',
                    color: aiScope === 'full_image' ? '#f3e8ff' : '#cbd5e1',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                  }}
                >
                  <span style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                    🖼️ Toàn Bộ Ảnh Gốc (Full Image)
                  </span>
                  <span style={{ fontSize: 8.5, padding: '1px 5px', borderRadius: 3, background: '#9333ea', color: '#fff' }}>⚡ 1 Lần • Nhanh nhất</span>
                </button>
                <div style={{ display: 'flex', gap: 4 }}>
                  <button
                    onClick={() => setAiScope && setAiScope('all')}
                    style={{
                      flex: 1,
                      padding: '5px 3px',
                      fontSize: 9,
                      fontWeight: 700,
                      borderRadius: 4,
                      border: aiScope === 'all' ? '1.5px solid #a855f7' : '1px solid rgba(255,255,255,0.1)',
                      background: aiScope === 'all' ? 'rgba(168, 85, 247, 0.25)' : 'rgba(0,0,0,0.3)',
                      color: aiScope === 'all' ? '#f3e8ff' : '#94a3b8',
                      cursor: 'pointer',
                    }}
                  >
                    🧩 Từng Ô ({totalCellCount} Ô)
                  </button>
                  <button
                    onClick={() => setAiScope && setAiScope('selected')}
                    style={{
                      flex: 1,
                      padding: '5px 3px',
                      fontSize: 9,
                      fontWeight: 700,
                      borderRadius: 4,
                      border: aiScope === 'selected' ? '1.5px solid #a855f7' : '1px solid rgba(255,255,255,0.1)',
                      background: aiScope === 'selected' ? 'rgba(168, 85, 247, 0.25)' : 'rgba(0,0,0,0.3)',
                      color: aiScope === 'selected' ? '#f3e8ff' : '#94a3b8',
                      cursor: 'pointer',
                    }}
                  >
                    🎯 Ô Đang Chọn
                  </button>
                </div>
              </div>
            </div>

            {/* Nút Tách Nền Smart BFS Client-Side (Thay thế khi không muốn bật Server) */}
            <div style={{ background: 'rgba(56, 189, 248, 0.08)', padding: '7px 8px', borderRadius: 6, border: '1px solid rgba(56, 189, 248, 0.3)', display: 'flex', flexDirection: 'column', gap: 5 }}>
              <div style={{ fontSize: 9.5, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
                <Sparkles size={12} /> ⚡ Chạy Ngay Trên Trình Duyệt (Không Cần Bật Server):
              </div>
              <div style={{ fontSize: 8.5, color: '#94a3b8', lineHeight: 1.4 }}>
                Thuật toán loang từ viền ngoài vào trong (Perimeter BFS), bảo vệ 100% tròng trắng mắt & răng không bị thủng, xử lý trong 0.01s.
              </div>
              <button
                onClick={onRunFastBFSMatting}
                disabled={isProcessing}
                style={{
                  width: '100%',
                  padding: '7px 10px',
                  fontSize: 10.5,
                  fontWeight: 800,
                  borderRadius: 5,
                  background: 'linear-gradient(135deg, #0284c7, #02537e)',
                  color: '#fff',
                  border: '1px solid #38bdf8',
                  cursor: isProcessing ? 'not-allowed' : 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 5,
                }}
              >
                ⚡ Bóc Tách BFS Trực Tiếp (0.01s • 0% VRAM)
              </button>
            </div>

            {/* Run AI Button */}
            <button
              onClick={onRunAIMatting}
              disabled={isAIRunning}
              style={{
                width: '100%',
                padding: '9px 12px',
                fontSize: 11,
                fontWeight: 800,
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
                marginTop: 2,
              }}
            >
              {isAIRunning ? (
                <>
                  <RefreshCw size={14} className="animate-spin" /> Đang chạy Model AI GPU...
                </>
              ) : (
                <>
                  <Sparkles size={14} /> 🚀 TÁCH NỀN BẰNG MODEL AI (GPU)
                </>
              )}
            </button>

            {/* Quick guide to start server */}
            {aiServerStatus !== 'online' && (
              <div style={{ background: 'rgba(0,0,0,0.4)', padding: 7, borderRadius: 5, border: '1px dashed #a855f7', fontSize: 8.5, color: '#d8b4fe' }}>
                <div style={{ fontWeight: 700, marginBottom: 3, color: '#f3e8ff' }}>💡 Cách chạy Server AI trên máy:</div>
                <div>1. Nhấp đúp vào file: <code style={{ background: '#1e1b4b', padding: '1px 4px', borderRadius: 3, color: '#38bdf8' }}>run_ai_matting_server.bat</code> trong thư mục dự án.</div>
                <div>2. Hoặc chạy lệnh terminal: <code style={{ background: '#1e1b4b', padding: '1px 4px', borderRadius: 3, color: '#38bdf8' }}>python server_ai_matting.py</code></div>
              </div>
            )}
          </div>
        ) : null}
      </div>

      {/* ========================================================
          CARD 3: HÀNH ĐỘNG CHÍNH & LẮP RÁP 3D (HERO BUTTONS)
         ======================================================== */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        <button
          onClick={onAutoSliceAndAssemble}
          disabled={isProcessing}
          style={{
            width: '100%',
            padding: '10px 14px',
            fontSize: 12,
            fontWeight: 800,
            borderRadius: 8,
            background: isProcessing
              ? 'rgba(255,255,255,0.1)'
              : 'linear-gradient(135deg, #0284c7 0%, #2563eb 50%, #7c3aed 100%)',
            color: '#ffffff',
            border: '1px solid rgba(255,255,255,0.2)',
            cursor: isProcessing ? 'not-allowed' : 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 7,
            boxShadow: '0 4px 16px rgba(37, 99, 235, 0.45)',
          }}
        >
          {isProcessing ? (
            <>
              <RefreshCw size={15} className="animate-spin" /> Đang bóc tách từng ô...
            </>
          ) : assemblySuccess ? (
            <>
              <Check size={15} /> ✓ Đã Tách ({slicedCount}/{totalCellCount} Ô) & Lắp 3D!
            </>
          ) : (
            <>
              <Sparkles size={15} /> ⚡ BÓC TÁCH & LẮP RÁP 3D TỰ ĐỘNG
            </>
          )}
        </button>

        {slicedCount > 0 && (
          <button
            onClick={onOpenSaveKitModal}
            style={{
              width: '100%',
              padding: '8px 10px',
              fontSize: 11,
              fontWeight: 700,
              borderRadius: 6,
              background: 'linear-gradient(135deg, #8b5cf6 0%, #6d28d9 100%)',
              color: '#ffffff',
              border: 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
              boxShadow: '0 2px 10px rgba(139, 92, 246, 0.35)',
            }}
          >
            <Save size={13} /> 💾 Lưu Bộ Kit Linh Kiện ({slicedCount}/{totalCellCount})
          </button>
        )}

        {onOpenCatalogModal && (
          <button
            onClick={onOpenCatalogModal}
            style={{
              width: '100%',
              padding: '7px 10px',
              fontSize: 10.5,
              fontWeight: 600,
              borderRadius: 6,
              background: 'rgba(255,255,255,0.06)',
              color: '#cbd5e1',
              border: '1px solid rgba(255,255,255,0.12)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
            }}
          >
            📦 Mở Kho Tài Nguyên Đã Lưu (Catalog)
          </button>
        )}
      </div>
    </div>
  );
};
