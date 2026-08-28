import React from 'react';
import { Layers, Eye, Table, Scissors, Target, Grid, Undo2, Redo2, ZoomIn, ZoomOut } from 'lucide-react';
import { GridCategoryDefinition } from '../../../../core/assets/GridSliceRegistry';

interface SlicerCanvasTopBarProps {
  currentCategory: GridCategoryDefinition;
  hasImage: boolean;
  loadedImage: HTMLImageElement | null;
  checkedCount: number;
  zoom: number;
  setZoom: (z: number | ((prev: number) => number)) => void;
  setPanOffset: (pos: { x: number; y: number }) => void;
  onAutoFitToViewport: () => void;
  onOpenGridTablePicker?: () => void;
  isSingleImageMode?: boolean;
  onToggleSingleImageMode?: () => void;
  isDirectBBoxCropActive?: boolean;
  onToggleDirectBBoxCrop?: () => void;
  onAutoFitGrid?: () => void;
  onResetUniformGrid?: () => void;
  canUndo?: boolean;
  canRedo?: boolean;
  onUndo?: () => void;
  onRedo?: () => void;
  checkerTheme?: 'dark' | 'light';
  onToggleCheckerTheme?: () => void;
  previewDisplayMode: 'transparent' | 'original';
  onModeClick: (mode: 'transparent' | 'original') => void;
}

export const SlicerCanvasTopBar: React.FC<SlicerCanvasTopBarProps> = ({
  currentCategory,
  hasImage,
  loadedImage,
  checkedCount,
  zoom,
  setZoom,
  setPanOffset,
  onAutoFitToViewport,
  onOpenGridTablePicker,
  isSingleImageMode,
  onToggleSingleImageMode,
  isDirectBBoxCropActive,
  onToggleDirectBBoxCrop,
  onAutoFitGrid,
  onResetUniformGrid,
  canUndo,
  canRedo,
  onUndo,
  onRedo,
  checkerTheme,
  onToggleCheckerTheme,
  previewDisplayMode,
  onModeClick,
}) => {
  return (
    <div
      style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: 6,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
        <div
          style={{
            fontSize: 11,
            fontWeight: 700,
            color: '#38bdf8',
            display: 'flex',
            alignItems: 'center',
            gap: 5,
          }}
        >
          <Layers size={13} />{' '}
          {currentCategory.id === 'single_full_image'
            ? 'Khung cắt: Ảnh đơn (Đã tắt lưới)'
            : `Khung lưới cắt (${currentCategory.rows} hàng × ${currentCategory.cols} cột)`}
        </div>

        {loadedImage && (
          <div
            style={{
              fontSize: 10,
              fontWeight: 700,
              color: '#4ade80',
              background: 'rgba(34, 197, 94, 0.15)',
              border: '1px solid rgba(34, 197, 94, 0.3)',
              borderRadius: 4,
              padding: '2px 7px',
              fontFamily: 'monospace',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
            title="Kích thước thực của ảnh đang chỉnh sửa"
          >
            <span>
              📐 {loadedImage.naturalWidth || loadedImage.width}×
              {loadedImage.naturalHeight || loadedImage.height}px
            </span>
          </div>
        )}

        {hasImage && checkedCount <= 1 && (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 2,
              background: 'rgba(0,0,0,0.3)',
              padding: 1,
              borderRadius: 4,
              border: '1px solid rgba(255,255,255,0.06)',
            }}
          >
            <button
              onClick={() => {
                setZoom(1.0);
                setPanOffset({ x: 0, y: 0 });
              }}
              style={{
                padding: '2px 6px',
                fontSize: 9.5,
                fontWeight: 600,
                borderRadius: 3,
                background: Math.abs(zoom - 1.0) < 0.05 ? '#0284c7' : 'transparent',
                color: Math.abs(zoom - 1.0) < 0.05 ? '#ffffff' : '#94a3b8',
                border: 'none',
                cursor: 'pointer',
              }}
              title="Xem kích thước thật 100% (1:1)"
            >
              100%
            </button>
            <button
              onClick={onAutoFitToViewport}
              style={{
                padding: '2px 6px',
                fontSize: 9.5,
                fontWeight: 600,
                borderRadius: 3,
                background: 'transparent',
                color: '#94a3b8',
                border: 'none',
                cursor: 'pointer',
              }}
              title="Căn vừa khung nhìn"
            >
              🔍 Fit ({Math.round(zoom * 100)}%)
            </button>
          </div>
        )}

        {onOpenGridTablePicker && (
          <button
            onClick={onOpenGridTablePicker}
            style={{
              padding: '3px 8px',
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 4,
              background:
                'linear-gradient(135deg, rgba(2, 132, 199, 0.4), rgba(56, 189, 248, 0.4))',
              color: '#38bdf8',
              border: '1.5px solid rgba(56, 189, 248, 0.6)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              boxShadow: '0 0 8px rgba(56, 189, 248, 0.25)',
              transition: 'all 0.15s ease',
            }}
            title="Mở bảng chọn ma trận dòng × cột kiểu bảng Word"
          >
            <Table size={12} /> Ma Trận Lưới...
          </button>
        )}

        {onToggleSingleImageMode && (
          <button
            onClick={onToggleSingleImageMode}
            style={{
              padding: '3px 8px',
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 4,
              background: isSingleImageMode
                ? 'linear-gradient(135deg, rgba(34, 197, 94, 0.3), rgba(16, 185, 129, 0.3))'
                : 'rgba(255, 255, 255, 0.08)',
              color: isSingleImageMode ? '#4ade80' : '#94a3b8',
              border: isSingleImageMode
                ? '1.5px solid #22c55e'
                : '1px solid rgba(255, 255, 255, 0.15)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              boxShadow: isSingleImageMode ? '0 0 8px rgba(34, 197, 94, 0.3)' : 'none',
              transition: 'all 0.15s ease',
            }}
            title={
              isSingleImageMode
                ? 'Đang bật Ảnh Đơn (Tắt khung lưới) - Nhấp để chuyển sang Ảnh Lưới theo ma trận'
                : 'Đang bật Ảnh Lưới - Nhấp để chuyển sang Ảnh Đơn'
            }
          >
            <span>{isSingleImageMode ? '🖼️ Ảnh Đơn: BẬT' : '🔲 Ảnh Lưới: BẬT'}</span>
          </button>
        )}

        {onToggleDirectBBoxCrop && (
          <button
            onClick={onToggleDirectBBoxCrop}
            disabled={!hasImage && checkedCount === 0}
            style={{
              padding: '3px 8px',
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 4,
              background: isDirectBBoxCropActive
                ? 'linear-gradient(135deg, rgba(168, 85, 247, 0.4), rgba(147, 51, 234, 0.4))'
                : 'rgba(255, 255, 255, 0.08)',
              color: isDirectBBoxCropActive
                ? '#d8b4fe'
                : hasImage || checkedCount > 0
                ? '#cbd5e1'
                : '#475569',
              border: isDirectBBoxCropActive
                ? '1.5px solid #c084fc'
                : '1px solid rgba(255, 255, 255, 0.15)',
              cursor: hasImage || checkedCount > 0 ? 'pointer' : 'not-allowed',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              boxShadow: isDirectBBoxCropActive ? '0 0 10px rgba(192, 132, 252, 0.4)' : 'none',
              transition: 'all 0.15s ease',
            }}
            title={
              isDirectBBoxCropActive
                ? 'Đang bật chế độ Cắt Bounding Box - Nhấp để tắt'
                : 'Bật chế độ Cắt Bounding Box tự động trực tiếp trên khung cắt'
            }
          >
            <Scissors size={12} />
            <span>{isDirectBBoxCropActive ? '✂️ BBox: BẬT' : '✂️ BBox: TẮT'}</span>
          </button>
        )}
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
        {currentCategory.id !== 'single_full_image' && (
          <div style={{ display: 'flex', gap: 4 }}>
            <button
              onClick={onAutoFitGrid}
              disabled={!hasImage}
              style={{
                padding: '4px 9px',
                fontSize: 10,
                fontWeight: 700,
                borderRadius: 4,
                background:
                  'linear-gradient(135deg, rgba(2, 132, 199, 0.3), rgba(139, 92, 246, 0.3))',
                color: hasImage ? '#38bdf8' : '#475569',
                border: '1px solid rgba(56, 189, 248, 0.35)',
                cursor: hasImage ? 'pointer' : 'not-allowed',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                boxShadow: hasImage ? '0 0 8px rgba(56, 189, 248, 0.2)' : 'none',
                transition: 'all 0.15s ease',
              }}
              title="Tự động quét và căn chỉnh các đường lưới ôm khớp khít từng linh kiện theo ảnh AI"
            >
              <Target size={11} /> Tự Căn Khung (Auto-Fit)
            </button>

            <button
              onClick={onResetUniformGrid}
              disabled={!hasImage}
              style={{
                padding: '4px 8px',
                fontSize: 10,
                fontWeight: 600,
                borderRadius: 4,
                background: 'rgba(255, 255, 255, 0.05)',
                color: hasImage ? '#94a3b8' : '#475569',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                cursor: hasImage ? 'pointer' : 'not-allowed',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
              }}
              title="Đặt lại các ô lưới chia đều nhau theo chiều ngang và dọc"
            >
              <Grid size={11} /> 📐 Lưới Đều
            </button>
          </div>
        )}

        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 2,
            background: 'rgba(0,0,0,0.4)',
            padding: '2px 4px',
            borderRadius: 5,
            border: '1px solid rgba(255,255,255,0.06)',
          }}
        >
          <button
            onClick={onUndo}
            disabled={!canUndo}
            style={{
              padding: '3px 7px',
              borderRadius: 4,
              background: canUndo ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.04)',
              color: canUndo ? '#38bdf8' : '#475569',
              border: canUndo ? '1px solid rgba(56, 189, 248, 0.3)' : '1px solid transparent',
              cursor: canUndo ? 'pointer' : 'not-allowed',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              fontSize: 10,
              fontWeight: 600,
              transition: 'all 0.15s ease',
            }}
            title="Hoàn tác thao tác trước (Ctrl + Z)"
          >
            <Undo2 size={12} /> Hoàn tác
          </button>
          <button
            onClick={onRedo}
            disabled={!canRedo}
            style={{
              padding: '3px 7px',
              borderRadius: 4,
              background: canRedo ? 'rgba(56, 189, 248, 0.15)' : 'rgba(255,255,255,0.04)',
              color: canRedo ? '#38bdf8' : '#475569',
              border: canRedo ? '1px solid rgba(56, 189, 248, 0.3)' : '1px solid transparent',
              cursor: canRedo ? 'pointer' : 'not-allowed',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              fontSize: 10,
              fontWeight: 600,
              transition: 'all 0.15s ease',
            }}
            title="Làm lại thao tác vừa hoàn tác (Ctrl + Y)"
          >
            <Redo2 size={12} /> Làm lại
          </button>
        </div>

        {checkedCount <= 1 && (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              background: 'rgba(0,0,0,0.4)',
              padding: '2px 6px',
              borderRadius: 5,
              border: '1px solid rgba(255,255,255,0.06)',
            }}
          >
            <button
              onClick={() => {
                const newZ = Math.max(0.05, Math.round((zoom / 1.2) * 100) / 100);
                setZoom(newZ);
              }}
              disabled={!hasImage}
              style={{
                padding: '3px 6px',
                borderRadius: 4,
                background: 'rgba(255,255,255,0.08)',
                color: hasImage ? '#fff' : '#475569',
                border: 'none',
                cursor: hasImage ? 'pointer' : 'not-allowed',
                display: 'flex',
                alignItems: 'center',
              }}
              title="Thu nhỏ (-)"
            >
              <ZoomOut size={12} />
            </button>
            <span
              style={{
                fontSize: 10.5,
                fontWeight: 700,
                color: '#38bdf8',
                minWidth: 40,
                textAlign: 'center',
                fontFamily: 'monospace',
              }}
            >
              {Math.round(zoom * 100)}%
            </span>
            <button
              onClick={() => {
                const newZ = Math.min(8, Math.round((zoom * 1.2) * 100) / 100);
                setZoom(newZ);
              }}
              disabled={!hasImage}
              style={{
                padding: '3px 6px',
                borderRadius: 4,
                background: 'rgba(255,255,255,0.08)',
                color: hasImage ? '#fff' : '#475569',
                border: 'none',
                cursor: hasImage ? 'pointer' : 'not-allowed',
                display: 'flex',
                alignItems: 'center',
              }}
              title="Phóng to (+)"
            >
              <ZoomIn size={12} />
            </button>
            <button
              onClick={() => {
                setZoom(1);
                setPanOffset({ x: 0, y: 0 });
              }}
              disabled={!hasImage}
              style={{
                padding: '2px 6px',
                fontSize: 9.5,
                fontWeight: 600,
                borderRadius: 4,
                background: 'rgba(255,255,255,0.06)',
                color: hasImage ? '#94a3b8' : '#475569',
                border: '1px solid rgba(255,255,255,0.08)',
                cursor: hasImage ? 'pointer' : 'not-allowed',
              }}
              title="Khôi phục kích thước 100% (1:1)"
            >
              100%
            </button>
            <button
              onClick={onAutoFitToViewport}
              disabled={!hasImage}
              style={{
                padding: '2px 7px',
                fontSize: 9.5,
                fontWeight: 700,
                borderRadius: 4,
                background: 'rgba(56, 189, 248, 0.15)',
                color: hasImage ? '#38bdf8' : '#475569',
                border: '1px solid rgba(56, 189, 248, 0.3)',
                cursor: hasImage ? 'pointer' : 'not-allowed',
              }}
              title="Tự động căn giữa và co giãn vừa khít khung nhìn"
            >
              Fit
            </button>
          </div>
        )}

        {onToggleCheckerTheme && (
          <button
            onClick={onToggleCheckerTheme}
            style={{
              padding: '4px 9px',
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 4,
              background:
                checkerTheme === 'light'
                  ? 'linear-gradient(135deg, #ffffff, #e2e8f0)'
                  : 'rgba(255, 255, 255, 0.08)',
              color: checkerTheme === 'light' ? '#0f172a' : '#cbd5e1',
              border:
                checkerTheme === 'light'
                  ? '1.5px solid #38bdf8'
                  : '1px solid rgba(255, 255, 255, 0.15)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              boxShadow: checkerTheme === 'light' ? '0 0 10px rgba(56, 189, 248, 0.5)' : 'none',
              transition: 'all 0.15s ease',
            }}
            title={
              checkerTheme === 'light'
                ? 'Đang bật Caro Sáng (Trắng/Xám nhạt) để soi rõ chi tiết tối. Nhấp để chuyển sang Caro Tối'
                : 'Đang bật Caro Tối (Đen/Xanh đậm). Nhấp để chuyển sang Caro Sáng'
            }
          >
            <span>{checkerTheme === 'light' ? '🏁 Caro Sáng (Bật)' : '🏁 Caro Tối (Bật)'}</span>
          </button>
        )}

        <div
          style={{
            display: 'flex',
            gap: 3,
            background: 'rgba(0,0,0,0.4)',
            padding: 2,
            borderRadius: 5,
          }}
        >
          <button
            onClick={() => onModeClick('transparent')}
            disabled={!hasImage && checkedCount === 0}
            style={{
              padding: '4px 9px',
              fontSize: 10,
              fontWeight: 600,
              borderRadius: 4,
              background: previewDisplayMode === 'transparent' ? '#0284c7' : 'transparent',
              color:
                hasImage || checkedCount > 0
                  ? previewDisplayMode === 'transparent'
                    ? '#ffffff'
                    : '#94a3b8'
                  : '#475569',
              border:
                previewDisplayMode === 'transparent'
                  ? '1px solid #38bdf8'
                  : '1px solid transparent',
              cursor: hasImage || checkedCount > 0 ? 'pointer' : 'not-allowed',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              boxShadow:
                previewDisplayMode === 'transparent' ? '0 0 8px rgba(56,189,248,0.3)' : 'none',
              transition: 'all 0.15s ease',
            }}
            title="Xem kết quả sau khi đã bóc tách nền (trong suốt)"
          >
            <Layers size={12} /> 🏁 Đã tách nền
          </button>

          <button
            onClick={() => onModeClick('original')}
            disabled={!hasImage && checkedCount === 0}
            style={{
              padding: '4px 9px',
              fontSize: 10,
              fontWeight: 600,
              borderRadius: 4,
              background:
                previewDisplayMode === 'original' && (hasImage || checkedCount > 0)
                  ? '#0284c7'
                  : 'transparent',
              color:
                hasImage || checkedCount > 0
                  ? previewDisplayMode === 'original'
                    ? '#ffffff'
                    : '#94a3b8'
                  : '#475569',
              border:
                previewDisplayMode === 'original' && (hasImage || checkedCount > 0)
                  ? '1px solid #38bdf8'
                  : '1px solid transparent',
              cursor: hasImage || checkedCount > 0 ? 'pointer' : 'not-allowed',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              boxShadow:
                previewDisplayMode === 'original' && (hasImage || checkedCount > 0)
                  ? '0 0 8px rgba(56,189,248,0.3)'
                  : 'none',
              transition: 'all 0.15s ease',
            }}
            title="Xem bức ảnh gốc ban đầu"
          >
            <Eye size={12} /> 👁️ Ảnh gốc
          </button>
        </div>
      </div>
    </div>
  );
};
