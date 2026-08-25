import React from 'react';
import { Sparkles, RefreshCw, Check, Save, Scissors } from 'lucide-react';
import { ChromaProcessOptions } from '../../../../core/utils/ChromaDespeckleProcessor';

export interface SlicerAssemblyActionCardProps {
  isProcessing: boolean;
  assemblySuccess: boolean;
  slicedCount: number;
  totalCellCount: number;
  paddingInset?: number;
  setPaddingInset?: (inset: number) => void;
  enableSmartCrop?: boolean;
  setEnableSmartCrop?: (enable: boolean) => void;
  smartCropPadding?: number;
  setSmartCropPadding?: (pad: number) => void;
  onCommitSliderChange?: (overrides?: Partial<ChromaProcessOptions>) => void;
  onAutoSliceAndAssemble: () => void;
  onOpenSaveKitModal?: () => void;
  onOpenCatalogModal?: () => void;
  onApplyAsNewBaseImage?: () => void;
  onOpenSmartCrop?: () => void;
}

export const SlicerAssemblyActionCard: React.FC<SlicerAssemblyActionCardProps> = ({
  isProcessing,
  assemblySuccess,
  slicedCount,
  totalCellCount,
  paddingInset = 0,
  setPaddingInset,
  enableSmartCrop = false,
  setEnableSmartCrop,
  smartCropPadding = 2,
  setSmartCropPadding,
  onCommitSliderChange,
  onAutoSliceAndAssemble,
  onOpenSaveKitModal,
  onOpenCatalogModal,
  onApplyAsNewBaseImage,
  onOpenSmartCrop,
}) => {
  return (
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

      {/* Padding Inset Control */}
      {setPaddingInset && (
        <div style={{ background: 'rgba(0,0,0,0.3)', padding: 7, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#cbd5e1', marginBottom: 3 }}>
            <span style={{ fontWeight: 600, color: '#38bdf8' }}>✂️ Thu viền ô (Padding Inset):</span>
            <span style={{ color: '#4ade80', fontWeight: 700 }}>{paddingInset}px</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
            <input
              type="range"
              min="0"
              max="40"
              step="1"
              value={paddingInset}
              onChange={(e) => setPaddingInset(parseInt(e.target.value, 10))}
              onPointerUp={() => onCommitSliderChange && onCommitSliderChange()}
              style={{ flex: 1, accentColor: '#38bdf8' }}
            />
            <div style={{ display: 'flex', gap: 2 }}>
              {[0, 4, 8].map((val) => (
                <button
                  key={val}
                  onClick={() => { setPaddingInset(val); if (onCommitSliderChange) onCommitSliderChange(); }}
                  style={{
                    height: 20,
                    padding: '0 5px',
                    fontSize: 8.5,
                    fontWeight: 600,
                    background: paddingInset === val ? '#0284c7' : 'rgba(255,255,255,0.1)',
                    color: paddingInset === val ? '#ffffff' : '#cbd5e1',
                    border: 'none',
                    borderRadius: 3,
                    cursor: 'pointer',
                  }}
                >
                  {val}px
                </button>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Smart Auto-Trim Bounding Box & Padding Slider */}
      {setEnableSmartCrop && setSmartCropPadding && (
        <div
          style={{
            background: enableSmartCrop ? 'rgba(147, 51, 234, 0.12)' : 'rgba(0,0,0,0.3)',
            padding: 7,
            borderRadius: 6,
            border: enableSmartCrop ? '1px solid rgba(192, 132, 252, 0.4)' : '1px solid rgba(255,255,255,0.06)',
            display: 'flex',
            flexDirection: 'column',
            gap: 5,
            transition: 'all 0.2s ease',
          }}
        >
          {/* Toggle Header */}
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <label
              style={{
                fontSize: 10,
                fontWeight: 700,
                color: enableSmartCrop ? '#c084fc' : '#94a3b8',
                display: 'flex',
                alignItems: 'center',
                gap: 5,
                cursor: 'pointer',
              }}
            >
              <input
                type="checkbox"
                checked={enableSmartCrop}
                onChange={(e) => {
                  setEnableSmartCrop(e.target.checked);
                  if (onCommitSliderChange) onCommitSliderChange();
                }}
                style={{ accentColor: '#a855f7', cursor: 'pointer' }}
              />
              <Scissors size={12} /> Cắt Bounding Box Pixel
            </label>
            <span
              style={{
                fontSize: 8.5,
                fontWeight: 700,
                padding: '1px 5px',
                borderRadius: 3,
                background: enableSmartCrop ? 'rgba(168, 85, 247, 0.25)' : 'rgba(255,255,255,0.08)',
                color: enableSmartCrop ? '#e9d5ff' : '#64748b',
                border: enableSmartCrop ? '1px solid rgba(192, 132, 252, 0.4)' : 'none',
              }}
            >
              {enableSmartCrop ? 'ĐANG BẬT' : 'TẮT'}
            </span>
          </div>

          {/* Padding Slider (Active when enabled) */}
          {enableSmartCrop && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 2 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 9.5, color: '#cbd5e1' }}>
                <span style={{ color: '#c084fc', fontWeight: 600 }}>Khoảng đệm biên (Padding):</span>
                <span style={{ color: '#4ade80', fontWeight: 700 }}>+{smartCropPadding}px (thụt lùi xa biên)</span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
                <input
                  type="range"
                  min="0"
                  max="30"
                  step="1"
                  value={smartCropPadding}
                  onChange={(e) => setSmartCropPadding(parseInt(e.target.value, 10))}
                  onPointerUp={() => onCommitSliderChange && onCommitSliderChange()}
                  style={{ flex: 1, accentColor: '#a855f7' }}
                />
                <div style={{ display: 'flex', gap: 2 }}>
                  {[0, 2, 4, 8, 12].map((val) => (
                    <button
                      key={val}
                      onClick={() => {
                        setSmartCropPadding(val);
                        if (onCommitSliderChange) onCommitSliderChange();
                      }}
                      style={{
                        height: 19,
                        padding: '0 4px',
                        fontSize: 8.5,
                        fontWeight: 600,
                        background: smartCropPadding === val ? '#9333ea' : 'rgba(255,255,255,0.08)',
                        color: smartCropPadding === val ? '#ffffff' : '#cbd5e1',
                        border: 'none',
                        borderRadius: 3,
                        cursor: 'pointer',
                      }}
                    >
                      {val === 0 ? '0px' : `+${val}px`}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Hero Button */}
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

      {/* Save Kit & Catalog Modal */}
      <div style={{ display: 'grid', gridTemplateColumns: slicedCount > 0 ? '1fr 1fr' : '1fr', gap: 6 }}>
        {slicedCount > 0 && onOpenSaveKitModal && (
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
            }}
          >
            📦 Mở kho linh kiện (Catalog)
          </button>
        )}

        {onOpenSmartCrop && (
          <button
            onClick={onOpenSmartCrop}
            style={{
              height: 32,
              fontSize: 10.5,
              fontWeight: 700,
              borderRadius: 6,
              background: 'linear-gradient(135deg, #9333ea 0%, #7e22ce 100%)',
              color: '#ffffff',
              border: '1px solid #c084fc',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 5,
              boxShadow: '0 2px 10px rgba(147, 51, 234, 0.35)',
            }}
            title="Tự động quét tìm viền pixel và cắt gọt kèm khoảng đệm Slider để lưu vào kho"
          >
            <Scissors size={13} /> ✂️ Cắt Bounding Box & Lưu Kho
          </button>
        )}
      </div>

      {/* Commit and Lock as New Base Image */}
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
          }}
          title="Lưu kết quả bóc tách hiện tại thành ảnh gốc mới"
        >
          💾 Xác nhận & Lưu làm mốc gốc mới (Commit Base)
        </button>
      )}
    </div>
  );
};
