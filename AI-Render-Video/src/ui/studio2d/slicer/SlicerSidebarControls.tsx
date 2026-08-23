import React from 'react';
import {
  Scissors,
  Sparkles,
  Upload,
  Layers,
  Save,
  Check,
  RefreshCw,
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
  bgCleanupSubTab: 'chroma' | 'despeckle';
  setBgCleanupSubTab: (tab: 'chroma' | 'despeckle') => void;
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
  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        background: 'rgba(15, 23, 42, 0.7)',
        padding: 10,
        borderRadius: 8,
        border: '1px solid rgba(255,255,255,0.08)',
        overflowY: 'auto',
      }}
    >
      {/* 1. Category Selector */}
      <div style={{ background: 'rgba(0,0,0,0.3)', padding: 8, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
        <div style={{ fontSize: 10.5, fontWeight: 700, color: '#38bdf8', marginBottom: 4, display: 'flex', alignItems: 'center', gap: 5 }}>
          <Layers size={13} /> 1. Bảng Linh Kiện Cần Cắt:
        </div>
        <select
          value={selectedCatId}
          onChange={(e) => onSelectCatId(e.target.value)}
          style={{
            width: '100%',
            padding: '6px 8px',
            fontSize: 10.5,
            background: '#0f172a',
            color: '#38bdf8',
            border: '1px solid #0284c7',
            borderRadius: 5,
            fontWeight: 600,
          }}
        >
          {GRID_CATEGORY_DEFINITIONS.map((cat) => (
            <option key={cat.id} value={cat.id}>
              {cat.label}
            </option>
          ))}
        </select>
      </div>

      {/* Padding Inset: Thu Viền Mép Ô */}
      <div style={{ background: 'rgba(0,0,0,0.3)', padding: 8, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 2 }}>
          <span style={{ fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
            ✂️ Thu Nhỏ Viền Ô (Padding Inset):
          </span>
          <span style={{ color: '#4ade80', fontWeight: 700 }}>{paddingInset} px</span>
        </div>
        <div style={{ fontSize: 8.5, color: '#94a3b8', marginBottom: 4, lineHeight: 1.25 }}>
          Tự động thụt lùi 4 mép cắt vào trong để loại bỏ triệt để viền kẻ ô, khung đen hoặc khoảng trống thừa.
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          {setPaddingInset && (
            <input
              type="range"
              min="0"
              max="50"
              step="1"
              value={paddingInset}
              onChange={(e) => {
                const val = parseInt(e.target.value, 10);
                setPaddingInset(val);
              }}
              onPointerUp={() => {
                if (onCommitSliderChange) onCommitSliderChange();
              }}
              onTouchEnd={() => {
                if (onCommitSliderChange) onCommitSliderChange();
              }}
              onKeyUp={() => {
                if (onCommitSliderChange) onCommitSliderChange();
              }}
              style={{ flex: 1 }}
            />
          )}
          {setPaddingInset && (
            <>
              <button onClick={() => { setPaddingInset(0); if (onCommitSliderChange) onCommitSliderChange(); }} style={{ padding: '2px 4px', fontSize: 9, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 3, cursor: 'pointer' }}>0</button>
              <button onClick={() => { setPaddingInset(4); if (onCommitSliderChange) onCommitSliderChange(); }} style={{ padding: '2px 4px', fontSize: 9, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>4px</button>
              <button onClick={() => { setPaddingInset(8); if (onCommitSliderChange) onCommitSliderChange(); }} style={{ padding: '2px 4px', fontSize: 9, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>8px</button>
              <button onClick={() => { setPaddingInset(12); if (onCommitSliderChange) onCommitSliderChange(); }} style={{ padding: '2px 4px', fontSize: 9, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>12px</button>
            </>
          )}
        </div>
      </div>

      {/* 2. Image Source Controls */}
      <div style={{ background: 'rgba(0,0,0,0.3)', padding: 8, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
        <div style={{ fontSize: 10.5, fontWeight: 700, color: '#38bdf8', marginBottom: 4, display: 'flex', alignItems: 'center', gap: 5 }}>
          <Upload size={13} /> 2. Ảnh Nguồn Sprite Sheet:
        </div>

        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          onChange={onFileUpload}
          style={{ display: 'none' }}
        />

        <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
          <button
            onClick={() => fileInputRef.current?.click()}
            style={{
              width: '100%',
              padding: '6px 8px',
              fontSize: 10.5,
              fontWeight: 600,
              borderRadius: 5,
              background: '#0284c7',
              color: '#ffffff',
              border: 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 5,
            }}
          >
            <Upload size={12} /> {userUploadedImageUrl ? 'Tải Ảnh Khác' : 'Tải Ảnh Lên Cắt'}
          </button>

          <div style={{ display: 'flex', gap: 4 }}>
            <button
              onClick={() => onResetToDemoImage('chibi')}
              style={{
                flex: 1,
                padding: '5px 4px',
                fontSize: 9.5,
                fontWeight: 700,
                borderRadius: 5,
                background: 'linear-gradient(135deg, rgba(16, 185, 129, 0.25), rgba(5, 150, 105, 0.25))',
                color: '#6ee7b7',
                border: '1px solid rgba(16, 185, 129, 0.4)',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 3,
              }}
              title="Dùng ảnh mẫu bóc tách tóc Chibi 4x5"
            >
              🌟 Mẫu Chibi (4x5)
            </button>

            <button
              onClick={() => onResetToDemoImage('default')}
              style={{
                flex: 1,
                padding: '5px 4px',
                fontSize: 9.5,
                fontWeight: 600,
                borderRadius: 5,
                background: 'rgba(255,255,255,0.05)',
                color: '#94a3b8',
                border: '1px solid rgba(255,255,255,0.1)',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 3,
              }}
              title="Dùng ảnh mẫu tóc Kiếm Khách"
            >
              🗡️ Mẫu Kiếm Khách
            </button>
          </div>
          {/* Apply Current Result as New Base Image */}
          {slicedCount > 0 && onApplyAsNewBaseImage && (
            <button
              onClick={onApplyAsNewBaseImage}
              style={{
                width: '100%',
                padding: '6px 8px',
                fontSize: 10,
                fontWeight: 700,
                borderRadius: 5,
                background: 'linear-gradient(135deg, #0d9488 0%, #059669 100%)',
                color: '#ffffff',
                border: '1px solid #14b8a6',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 5,
                boxShadow: '0 2px 8px rgba(13, 148, 136, 0.35)',
              }}
              title="Ghi đè kết quả đã cắt/tách nền này thành Ảnh Gốc Mới để tiếp tục bóc tách và lọc màu trên ảnh đã xử lý"
            >
              📌 Áp Dụng Làm Ảnh Gốc Mới (Apply Base)
            </button>
          )}
        </div>
      </div>

      {/* 3. Chroma Key & Despeckle Cleanup Section */}
      <div style={{ background: 'rgba(34, 197, 94, 0.06)', padding: 8, borderRadius: 6, border: '1px solid rgba(34, 197, 94, 0.2)', display: 'flex', flexDirection: 'column', gap: 6 }}>
        <div style={{ fontSize: 10.5, fontWeight: 700, color: '#4ade80', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Scissors size={13} /> 3. Tách Nền & Khử Đốm Rác:
        </div>
        {/* Toggle Chế Độ Xử Lý Tiếp (Lọc màu bổ sung / Multi-pass Keying) */}
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: 4,
            background: isCumulativeProcessing ? 'rgba(234, 179, 8, 0.12)' : 'rgba(0,0,0,0.25)',
            padding: '6px 8px',
            borderRadius: 6,
            border: isCumulativeProcessing ? '1px solid #eab308' : '1px solid rgba(255,255,255,0.08)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: 9.5, fontWeight: 700, color: isCumulativeProcessing ? '#facc15' : '#94a3b8', display: 'flex', alignItems: 'center', gap: 4 }}>
              ⚡ Xử Lý Tiếp (Giữ kết quả cũ):
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
                  color: isCumulativeProcessing ? '#090d16' : '#cbd5e1',
                  cursor: 'pointer',
                  transition: 'all 0.2s',
                  boxShadow: isCumulativeProcessing ? '0 0 10px rgba(234, 179, 8, 0.4)' : 'none',
                }}
              >
                {isCumulativeProcessing ? '✓ BẬT' : '○ TẮT'}
              </button>
            )}
          </div>
          <div style={{ fontSize: 8.5, color: isCumulativeProcessing ? '#fde047' : '#64748b', lineHeight: 1.25 }}>
            {isCumulativeProcessing
              ? '✓ Bật xử lý tiếp: Bạn có thể chọn màu khác (vd: trắng/xanh/custom) để bóc tách thêm trên ảnh đã cắt, không bị reset về ảnh gốc!'
              : '○ Đang tắt: Mỗi lần bấm bóc tách sẽ làm mới lại từ ảnh gốc.'}
          </div>
          <div style={{ display: 'flex', gap: 4, marginTop: 2 }}>
            {onApplyAsNewBaseImage && slicedCount > 0 && (
              <button
                onClick={onApplyAsNewBaseImage}
                style={{
                  flex: 1,
                  padding: '3px 6px',
                  fontSize: 9,
                  fontWeight: 700,
                  borderRadius: 4,
                  border: '1px solid #14b8a6',
                  background: 'rgba(13, 148, 136, 0.25)',
                  color: '#2dd4bf',
                  cursor: 'pointer',
                }}
                title="Ghi đè kết quả hiện tại thành ảnh gốc mới"
              >
                📌 Lưu đè thành Ảnh Gốc Mới
              </button>
            )}
            {isCumulativeProcessing && onResetToRawSlices && (
              <button
                onClick={onResetToRawSlices}
                style={{
                  flex: 1,
                  padding: '3px 6px',
                  fontSize: 9,
                  fontWeight: 600,
                  borderRadius: 4,
                  border: '1px dashed rgba(234, 179, 8, 0.5)',
                  background: 'transparent',
                  color: '#facc15',
                  cursor: 'pointer',
                }}
              >
                ↺ Quay lại ảnh gốc
              </button>
            )}
          </div>
        </div>


        {/* Sub-tab Switcher */}
        <div style={{ display: 'flex', gap: 4, background: 'rgba(0,0,0,0.35)', padding: 2, borderRadius: 5, border: '1px solid rgba(255,255,255,0.06)' }}>
          <button
            onClick={() => setBgCleanupSubTab('chroma')}
            style={{
              flex: 1,
              padding: '4px',
              fontSize: 9.5,
              fontWeight: 700,
              borderRadius: 4,
              border: 'none',
              background: bgCleanupSubTab === 'chroma' ? '#0284c7' : 'transparent',
              color: bgCleanupSubTab === 'chroma' ? '#ffffff' : '#94a3b8',
              cursor: 'pointer',
            }}
          >
            1. Tách Màu
          </button>

          <button
            onClick={() => setBgCleanupSubTab('despeckle')}
            style={{
              flex: 1,
              padding: '4px',
              fontSize: 9.5,
              fontWeight: 700,
              borderRadius: 4,
              border: 'none',
              background: bgCleanupSubTab === 'despeckle' ? '#0284c7' : 'transparent',
              color: bgCleanupSubTab === 'despeckle' ? '#ffffff' : '#94a3b8',
              cursor: 'pointer',
            }}
          >
            2. Khử Rác
          </button>
        </div>

        {bgCleanupSubTab === 'chroma' ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {/* Target Color Selector */}
            <div>
              <div style={{ fontSize: 9.5, color: '#94a3b8', marginBottom: 3, fontWeight: 600 }}>Màu Nền Cần Tách:</div>
              <div style={{ display: 'flex', gap: 4 }}>
                <button
                  onClick={() => {
                    setKeyColorType('chroma_green');
                    if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'chroma_green' });
                  }}
                  style={{
                    flex: 1,
                    padding: '4px 3px',
                    fontSize: 9,
                    fontWeight: 700,
                    borderRadius: 4,
                    border: keyColorType === 'chroma_green' ? '1.5px solid #22c55e' : '1px solid rgba(255,255,255,0.1)',
                    background: keyColorType === 'chroma_green' ? 'rgba(34, 197, 94, 0.2)' : 'rgba(0,0,0,0.3)',
                    color: keyColorType === 'chroma_green' ? '#4ade80' : '#94a3b8',
                    cursor: 'pointer',
                    whiteSpace: 'nowrap',
                  }}
                >
                  🟢 Xanh
                </button>
                <button
                  onClick={() => {
                    setKeyColorType('pure_white');
                    if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'pure_white' });
                  }}
                  style={{
                    flex: 1,
                    padding: '4px 3px',
                    fontSize: 9,
                    fontWeight: 700,
                    borderRadius: 4,
                    border: keyColorType === 'pure_white' ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    background: keyColorType === 'pure_white' ? 'rgba(56, 189, 248, 0.2)' : 'rgba(0,0,0,0.3)',
                    color: keyColorType === 'pure_white' ? '#38bdf8' : '#94a3b8',
                    cursor: 'pointer',
                    whiteSpace: 'nowrap',
                  }}
                >
                  ⚪ Trắng
                </button>
                <button
                  onClick={() => {
                    setKeyColorType('custom');
                    if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'custom' });
                  }}
                  style={{
                    flex: 1,
                    padding: '4px 3px',
                    fontSize: 9,
                    fontWeight: 700,
                    borderRadius: 4,
                    border: keyColorType === 'custom' ? '1.5px solid #a855f7' : '1px solid rgba(255,255,255,0.1)',
                    background: keyColorType === 'custom' ? 'rgba(168, 85, 247, 0.2)' : 'rgba(0,0,0,0.3)',
                    color: keyColorType === 'custom' ? '#c084fc' : '#94a3b8',
                    cursor: 'pointer',
                    whiteSpace: 'nowrap',
                  }}
                >
                  🎨 Tự Chọn
                </button>
              </div>

              {keyColorType === 'custom' && (
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 4 }}>
                  <input
                    type="color"
                    value={keyColorHex}
                    onChange={(e) => {
                      setKeyColorHex(e.target.value);
                      if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'custom', keyColorHex: e.target.value });
                    }}
                    style={{ width: 28, height: 22, padding: 0, border: 'none', background: 'transparent', cursor: 'pointer' }}
                  />
                  <input
                    type="text"
                    value={keyColorHex}
                    onChange={(e) => {
                      setKeyColorHex(e.target.value);
                    }}
                    onBlur={() => {
                      if (onCommitSliderChange) onCommitSliderChange({ keyColorType: 'custom', keyColorHex });
                    }}
                    style={{ flex: 1, padding: '2px 5px', fontSize: 9.5, background: '#090d16', color: '#fff', border: '1px solid #a855f7', borderRadius: 4 }}
                  />
                </div>
              )}
            </div>

            {/* Mode: Tách Toàn Bộ vs Chỉ Tách Viền Ngoài */}
            <div>
              <div style={{ fontSize: 9.5, color: '#94a3b8', marginBottom: 3, fontWeight: 600 }}>Chế Độ Bóc Tách:</div>
              <div style={{ display: 'flex', gap: 4 }}>
                <button
                  onClick={() => {
                    setIsolationMode('all');
                    if (onCommitSliderChange) onCommitSliderChange({ isolationMode: 'all' });
                  }}
                  style={{
                    flex: 1,
                    padding: '4px 2px',
                    fontSize: 9,
                    fontWeight: 700,
                    borderRadius: 4,
                    border: isolationMode === 'all' ? '1.5px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                    background: isolationMode === 'all' ? 'rgba(56, 189, 248, 0.2)' : 'rgba(0,0,0,0.3)',
                    color: isolationMode === 'all' ? '#38bdf8' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  🌐 Tách Toàn Bộ
                </button>
                <button
                  onClick={() => {
                    setIsolationMode('outer_only');
                    if (onCommitSliderChange) onCommitSliderChange({ isolationMode: 'outer_only' });
                  }}
                  style={{
                    flex: 1,
                    padding: '4px 2px',
                    fontSize: 9,
                    fontWeight: 700,
                    borderRadius: 4,
                    border: isolationMode === 'outer_only' ? '1.5px solid #eab308' : '1px solid rgba(255,255,255,0.1)',
                    background: isolationMode === 'outer_only' ? 'rgba(234, 179, 8, 0.2)' : 'rgba(0,0,0,0.3)',
                    color: isolationMode === 'outer_only' ? '#facc15' : '#94a3b8',
                    cursor: 'pointer',
                  }}
                >
                  🔲 Tách Viền Ngoài
                </button>
              </div>
            </div>

            {/* Tolerance Slider */}
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                <span>Độ nhạy màu (Tolerance):</span>
                <span style={{ color: '#38bdf8', fontWeight: 700 }}>{tolerance}</span>
              </div>
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
                style={{ width: '100%' }}
              />
            </div>

                                    {/* Thêm Viền Theo Màu Đã Chọn (Edge Stroke / Outline) */}
            <div style={{ background: 'rgba(0,0,0,0.3)', padding: 7, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)', display: 'flex', flexDirection: 'column', gap: 5 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontSize: 9.5, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
                  🎨 Thêm Viền Theo Màu (Stroke):
                </span>
                <span style={{ fontSize: 9.5, color: '#4ade80', fontWeight: 700 }}>{strokeWidth}px</span>
              </div>
              <div style={{ fontSize: 8.5, color: '#94a3b8', lineHeight: 1.25 }}>
                Tạo một đường viền nét mịn bao quanh hình dựa theo màu bạn chọn bên dưới.
              </div>

              {/* Color picker & quick color presets */}
              <div style={{ display: 'flex', alignItems: 'center', gap: 5, flexWrap: 'wrap' }}>
                <span style={{ fontSize: 9, color: '#cbd5e1' }}>Màu viền:</span>
                {setStrokeColorHex && (
                  <input
                    type="color"
                    value={strokeColorHex}
                    onChange={(e) => {
                      setStrokeColorHex(e.target.value);
                      if (onCommitSliderChange && strokeWidth > 0) onCommitSliderChange({ strokeColorHex: e.target.value });
                    }}
                    style={{ width: 24, height: 20, border: 'none', borderRadius: 3, cursor: 'pointer', background: 'transparent' }}
                    title="Chọn màu viền tùy thích"
                  />
                )}
                {setStrokeColorHex && (
                  <>
                    <button onClick={() => { setStrokeColorHex('#000000'); if (onCommitSliderChange && strokeWidth > 0) onCommitSliderChange({ strokeColorHex: '#000000' }); }} style={{ padding: '2px 5px', fontSize: 8.5, background: strokeColorHex === '#000000' ? '#0284c7' : 'rgba(0,0,0,0.5)', color: '#fff', border: '1px solid #475569', borderRadius: 3, cursor: 'pointer' }}>🖤 Đen</button>
                    <button onClick={() => { setStrokeColorHex('#ffffff'); if (onCommitSliderChange && strokeWidth > 0) onCommitSliderChange({ strokeColorHex: '#ffffff' }); }} style={{ padding: '2px 5px', fontSize: 8.5, background: strokeColorHex === '#ffffff' ? '#0284c7' : 'rgba(255,255,255,0.2)', color: '#fff', border: '1px solid #475569', borderRadius: 3, cursor: 'pointer' }}>🤍 Trắng</button>
                    <button onClick={() => { setStrokeColorHex('#2b1810'); if (onCommitSliderChange && strokeWidth > 0) onCommitSliderChange({ strokeColorHex: '#2b1810' }); }} style={{ padding: '2px 5px', fontSize: 8.5, background: strokeColorHex === '#2b1810' ? '#0284c7' : '#2b1810', color: '#fff', border: '1px solid #475569', borderRadius: 3, cursor: 'pointer' }}>🤎 Nâu Nét</button>
                    <button onClick={() => { setStrokeColorHex(keyColorHex); if (onCommitSliderChange && strokeWidth > 0) onCommitSliderChange({ strokeColorHex: keyColorHex }); }} style={{ padding: '2px 5px', fontSize: 8.5, background: strokeColorHex === keyColorHex ? '#0284c7' : 'rgba(255,255,255,0.1)', color: '#fff', border: '1px solid #475569', borderRadius: 3, cursor: 'pointer' }}>✨ Màu Nền</button>
                  </>
                )}
              </div>

              {/* Slider for stroke width */}
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
                    <button onClick={() => { setStrokeWidth(0); if (onCommitSliderChange) onCommitSliderChange({ strokeWidth: 0 }); }} style={{ padding: '2px 4px', fontSize: 9, background: 'rgba(255,255,255,0.1)', color: '#94a3b8', border: 'none', borderRadius: 3, cursor: 'pointer' }}>0</button>
                    <button onClick={() => { setStrokeWidth(1); if (onCommitSliderChange) onCommitSliderChange({ strokeWidth: 1, strokeColorHex }); }} style={{ padding: '2px 4px', fontSize: 9, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>1px</button>
                    <button onClick={() => { setStrokeWidth(2); if (onCommitSliderChange) onCommitSliderChange({ strokeWidth: 2, strokeColorHex }); }} style={{ padding: '2px 4px', fontSize: 9, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>2px</button>
                    <button onClick={() => { setStrokeWidth(4); if (onCommitSliderChange) onCommitSliderChange({ strokeWidth: 4, strokeColorHex }); }} style={{ padding: '2px 4px', fontSize: 9, background: 'rgba(255,255,255,0.1)', color: '#fff', border: 'none', borderRadius: 3, cursor: 'pointer' }}>4px</button>
                  </>
                )}
              </div>
            </div>

            {/* Feather Slider */}
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                <span>Làm mềm viền (Feather):</span>
                <span style={{ color: '#38bdf8', fontWeight: 700 }}>{feather}px</span>
              </div>
              <input
                type="range"
                min="0"
                max="20"
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
                style={{ width: '100%' }}
              />
            </div>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {/* Despeckle Size Slider */}
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                <span>🧹 Kích thước hạt rác:</span>
                <span style={{ color: '#38bdf8', fontWeight: 700 }}>{despeckleSize}px</span>
              </div>
              <input
                type="range"
                min="0"
                max="150"
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
                onKeyUp={(e) => {
                  const val = parseInt((e.target as HTMLInputElement).value);
                  if (onCommitSliderChange) onCommitSliderChange({ despeckleSize: val });
                }}
                style={{ width: '100%' }}
              />
            </div>

            {/* White Speckle Sensitivity */}
            <div style={{ fontSize: 9.5, color: '#94a3b8' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                <span>⚪ Khử đốm trắng:</span>
                <span style={{ color: '#38bdf8', fontWeight: 700 }}>{whiteSpeckleSensitivity}%</span>
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
                onKeyUp={(e) => {
                  const val = parseInt((e.target as HTMLInputElement).value);
                  if (onCommitSliderChange) onCommitSliderChange({ whiteSpeckleSensitivity: val });
                }}
                style={{ width: '100%' }}
              />
            </div>

            {/* Keep Largest Island Only Toggle */}
            <button
              onClick={() => {
                const nextVal = !keepLargestIslandOnly;
                setKeepLargestIslandOnly(nextVal);
                if (onCommitSliderChange) onCommitSliderChange({ keepLargestIslandOnly: nextVal });
              }}
              style={{
                width: '100%',
                padding: '5px 8px',
                fontSize: 9.5,
                fontWeight: 600,
                borderRadius: 4,
                border: keepLargestIslandOnly ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                background: keepLargestIslandOnly ? 'rgba(56, 189, 248, 0.2)' : 'rgba(0,0,0,0.3)',
                color: keepLargestIslandOnly ? '#38bdf8' : '#94a3b8',
                cursor: 'pointer',
                textAlign: 'left',
              }}
            >
              {keepLargestIslandOnly ? '✓ Đang bật: Chỉ giữ lại mảng lớn nhất' : '○ Chỉ giữ lại mảng lớn nhất (Tự lọc rác)'}
            </button>
          </div>
        )}
      </div>

      {/* 4. Action Buttons */}
      <button
        onClick={onAutoSliceAndAssemble}
        disabled={isProcessing}
        style={{
          width: '100%',
          padding: '10px 12px',
          fontSize: 11.5,
          fontWeight: 700,
          borderRadius: 6,
          background: assemblySuccess
            ? 'linear-gradient(135deg, #10b981 0%, #059669 100%)'
            : 'linear-gradient(135deg, #0284c7 0%, #2563eb 100%)',
          color: '#ffffff',
          border: 'none',
          cursor: isProcessing ? 'wait' : 'pointer',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 6,
          boxShadow: '0 4px 14px rgba(2, 132, 199, 0.4)',
        }}
      >
        {isProcessing ? (
          <>
            <RefreshCw size={14} className="animate-spin" /> Đang bóc tách...
          </>
        ) : assemblySuccess ? (
          <>
            <Check size={14} /> ✓ Đã Bóc Tách & Lắp 3D!
          </>
        ) : (
          <>
            <Sparkles size={14} /> ⚡ Tách Nền & Lắp Ráp 3D
          </>
        )}
      </button>

      {/* Save Kit & Open Catalog Buttons */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
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
            <Save size={13} /> 💾 Lưu Bộ Linh Kiện ({slicedCount}/{totalCellCount})
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
