import React from 'react';
import { Sparkles, RefreshCw, Check, Save, Package, Film, Layers } from 'lucide-react';

export interface SlicerGalleryActionCardProps {
  isProcessing?: boolean;
  isBatchProcessing?: boolean;
  assemblySuccess?: boolean;
  slicedCount?: number;
  totalCellCount?: number;
  checkedCount?: number;
  onAutoSliceAndAssemble?: () => void;
  onBatchSeparateChecked?: () => Promise<void>;
  onOpenCatalogModal?: () => void;
  onOpenSaveKitModal?: () => void;
  onTransferToAnimationSlicer?: () => void;
  onSwitchToAssemblyTab?: () => void;
}

export const SlicerGalleryActionCard: React.FC<SlicerGalleryActionCardProps> = ({
  isProcessing = false,
  isBatchProcessing = false,
  assemblySuccess = false,
  slicedCount = 0,
  totalCellCount = 0,
  checkedCount = 0,
  onAutoSliceAndAssemble,
  onBatchSeparateChecked,
  onOpenCatalogModal,
  onOpenSaveKitModal,
  onTransferToAnimationSlicer,
  onSwitchToAssemblyTab,
}) => {
  const isBusy = isProcessing || isBatchProcessing;

  const handleSeparateClick = () => {
    if (isBusy) return;
    if (checkedCount > 1 && onBatchSeparateChecked) {
      onBatchSeparateChecked();
    } else if (onAutoSliceAndAssemble) {
      onAutoSliceAndAssemble();
    }
  };

  return (
    <div
      style={{
        background: 'linear-gradient(180deg, rgba(30, 41, 59, 0.75) 0%, rgba(15, 23, 42, 0.95) 100%)',
        borderRadius: 8,
        border: '1.5px solid rgba(56, 189, 248, 0.3)',
        padding: 8,
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        boxShadow: '0 4px 16px rgba(0, 0, 0, 0.5), 0 0 10px rgba(56, 189, 248, 0.1)',
        boxSizing: 'border-box',
        flexShrink: 0,
      }}
    >
      {/* 1. Header: Quick Actions Label */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          paddingBottom: 4,
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
        }}
      >
        <span style={{ fontSize: 10, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
          <Sparkles size={12} color="#38bdf8" /> BẢNG THAO TÁC XỬ LÝ
        </span>
        {slicedCount > 0 && (
          <span style={{ fontSize: 9, fontWeight: 700, color: '#4ade80', background: 'rgba(34,197,94,0.15)', padding: '1px 5px', borderRadius: 3 }}>
            {slicedCount} ô đã cắt
          </span>
        )}
      </div>

      {/* 2. Primary Hero Button: Tách Nền & Bóc Tách */}
      <button
        onClick={handleSeparateClick}
        disabled={isBusy}
        style={{
          width: '100%',
          height: 34,
          fontSize: 11,
          fontWeight: 700,
          borderRadius: 6,
          background: isBusy
            ? 'rgba(255, 255, 255, 0.1)'
            : checkedCount > 1
            ? 'linear-gradient(135deg, #7c3aed 0%, #0284c7 100%)'
            : 'linear-gradient(135deg, #0284c7 0%, #2563eb 50%, #7c3aed 100%)',
          color: '#ffffff',
          border: '1px solid rgba(255, 255, 255, 0.25)',
          cursor: isBusy ? 'not-allowed' : 'pointer',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 6,
          boxShadow: isBusy ? 'none' : '0 3px 12px rgba(37, 99, 235, 0.4)',
          transition: 'all 0.15s ease',
        }}
      >
        {isBatchProcessing ? (
          <>
            <RefreshCw size={13} className="animate-spin" /> Đang tách ({checkedCount} ảnh)...
          </>
        ) : isProcessing ? (
          <>
            <RefreshCw size={13} className="animate-spin" /> Đang bóc tách từng ô...
          </>
        ) : checkedCount > 1 ? (
          <>
            <Sparkles size={13} /> Tách Nền {checkedCount} Ảnh Đang Chọn
          </>
        ) : assemblySuccess ? (
          <>
            <Check size={13} /> ✓ Đã bóc tách ({slicedCount}/{totalCellCount} ô)
          </>
        ) : (
          <>
            <Sparkles size={13} /> ⚡ Tách Nền & Bóc Tách Lưới
          </>
        )}
      </button>

      {/* 3. Secondary Button: Kho Linh Kiện */}
      {onOpenCatalogModal && (
        <button
          onClick={onOpenCatalogModal}
          style={{
            height: 27,
            fontSize: 10,
            fontWeight: 700,
            borderRadius: 4,
            background: 'rgba(255, 255, 255, 0.08)',
            color: '#e2e8f0',
            border: '1px solid rgba(255, 255, 255, 0.15)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 5,
            transition: 'all 0.15s ease',
          }}
          title="Mở kho linh kiện sprite sheet"
        >
          <Package size={12} color="#38bdf8" /> 📦 Kho Linh Kiện Sprite Sheet
        </button>
      )}

      {/* 4. Action: Chuyển Sang Tab Hoạt Ảnh (Tab 1.2) hoặc Tab Ghép (Tab 1.3) */}
      {onTransferToAnimationSlicer && (
        <button
          onClick={onTransferToAnimationSlicer}
          style={{
            height: 28,
            fontSize: 9.5,
            fontWeight: 700,
            borderRadius: 5,
            background: 'linear-gradient(135deg, rgba(2, 132, 199, 0.35), rgba(168, 85, 247, 0.35))',
            color: '#38bdf8',
            border: '1px solid rgba(56, 189, 248, 0.5)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 5,
            boxShadow: '0 2px 8px rgba(56, 189, 248, 0.2)',
            transition: 'all 0.15s ease',
          }}
          title="Chuyển các ô đã cắt hoặc ảnh hiện tại sang Tab 1.2 để ghép hoạt ảnh chuyển động"
        >
          <Film size={11} color="#38bdf8" /> Chuyển Sang Hoạt Ảnh (Tab 1.2)
        </button>
      )}

      {onSwitchToAssemblyTab && (
        <button
          onClick={onSwitchToAssemblyTab}
          style={{
            height: 26,
            fontSize: 9.5,
            fontWeight: 600,
            borderRadius: 5,
            background: 'rgba(255, 255, 255, 0.05)',
            color: '#a5b4fc',
            border: '1px solid rgba(165, 180, 252, 0.25)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 4,
          }}
          title="Chuyển sang Tab Ghép 3D"
        >
          <Layers size={11} color="#a5b4fc" /> Chuyển Sang Tab Ghép 3D
        </button>
      )}
    </div>
  );
};
