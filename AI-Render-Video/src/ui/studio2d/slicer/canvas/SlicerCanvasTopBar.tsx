import React from 'react';
import { Layers, Eye, Table, Scissors, Target, Grid, Undo2, Redo2, ZoomIn, ZoomOut, Eraser, Square } from 'lucide-react';
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

  // Eraser tools
  eraserMode?: 'off' | 'brush' | 'box';
  setEraserMode?: (m: 'off' | 'brush' | 'box') => void;
  eraserBrushSize?: number;
  setEraserBrushSize?: (s: number) => void;

  // Grid Divider Sync Controls
  dividerSyncMode?: 'all' | 'single';
  onToggleDividerSyncMode?: () => void;
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
  eraserMode = 'off',
  setEraserMode,
  eraserBrushSize = 20,
  setEraserBrushSize,
  dividerSyncMode = 'single',
  onToggleDividerSyncMode,
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

        {/* 🎯 Toggle Chỉnh Lưới: Riêng 1 ảnh VS Toàn bộ ảnh */}
        {onToggleDividerSyncMode && !isSingleImageMode && currentCategory && currentCategory.id !== 'single_full_image' && currentCategory.cols > 1 && (
          <button
            onClick={onToggleDividerSyncMode}
            style={{
              padding: '3px 8px',
              fontSize: 10,
              fontWeight: 700,
              borderRadius: 4,
              background:
                dividerSyncMode === 'single'
                  ? 'linear-gradient(135deg, rgba(99, 102, 241, 0.35), rgba(168, 85, 247, 0.35))'
                  : 'linear-gradient(135deg, rgba(2, 132, 199, 0.35), rgba(56, 189, 248, 0.35))',
              color: dividerSyncMode === 'single' ? '#a5b4fc' : '#38bdf8',
              border: dividerSyncMode === 'single' ? '1.5px solid #818cf8' : '1.5px solid #38bdf8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              boxShadow:
                dividerSyncMode === 'single'
                  ? '0 0 8px rgba(129, 140, 248, 0.3)'
                  : '0 0 8px rgba(56, 189, 248, 0.3)',
              transition: 'all 0.15s ease',
            }}
            title={
              dividerSyncMode === 'single'
                ? 'Đang ở chế độ: Chỉnh lưới RIÊNG TỪNG ẢNH (Không ảnh hưởng các ảnh khác) - Nhấp để chuyển sang ĐỒNG BỘ TOÀN BỘ ẢNH'
                : 'Đang ở chế độ: Chỉnh lưới ĐỒNG BỘ TOÀN BỘ ẢNH - Nhấp để chuyển sang RIÊNG TỪNG ẢNH'
            }
          >
            <span>{dividerSyncMode === 'single' ? '🎯 Lưới: Riêng 1 ảnh' : '🌐 Lưới: Toàn bộ ảnh'}</span>
          </button>
        )}

        {/* 🧹 Direct Pixel Eraser Brush Toggle */}
        {setEraserMode && (
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, background: 'rgba(0,0,0,0.35)', padding: '2px 5px', borderRadius: 6, border: '1px solid rgba(255,255,255,0.08)' }}>
            <button
              onClick={() => setEraserMode(eraserMode === 'brush' ? 'off' : 'brush')}
              style={{
                padding: '3px 8px',
                fontSize: 10,
                fontWeight: 700,
                borderRadius: 4,
                background: eraserMode === 'brush'
                  ? 'linear-gradient(135deg, rgba(245, 158, 11, 0.4), rgba(217, 119, 6, 0.4))'
                  : 'rgba(255, 255, 255, 0.06)',
                color: eraserMode === 'brush' ? '#fbbf24' : '#cbd5e1',
                border: eraserMode === 'brush' ? '1.5px solid #f59e0b' : '1px solid rgba(255, 255, 255, 0.1)',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                boxShadow: eraserMode === 'brush' ? '0 0 10px rgba(245, 158, 11, 0.4)' : 'none',
              }}
              title="Bật cọ tẩy pixel trực tiếp trên khung ảnh hoặc các thẻ ảnh trong lưới"
            >
              <Eraser size={12} />
              <span>{eraserMode === 'brush' ? '🧹 Cọ Tẩy: BẬT' : '🧹 Cọ Tẩy'}</span>
            </button>

            <button
              onClick={() => setEraserMode(eraserMode === 'box' ? 'off' : 'box')}
              style={{
                padding: '3px 8px',
                fontSize: 10,
                fontWeight: 700,
                borderRadius: 4,
                background: eraserMode === 'box'
                  ? 'linear-gradient(135deg, rgba(239, 68, 68, 0.4), rgba(220, 38, 38, 0.4))'
                  : 'rgba(255, 255, 255, 0.06)',
                color: eraserMode === 'box' ? '#fca5a5' : '#cbd5e1',
                border: eraserMode === 'box' ? '1.5px solid #ef4444' : '1px solid rgba(255, 255, 255, 0.1)',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                boxShadow: eraserMode === 'box' ? '0 0 10px rgba(239, 68, 68, 0.4)' : 'none',
              }}
              title="Kéo chuột tạo vùng chọn hình chữ nhật để xóa toàn bộ pixel bên trong"
            >
              <Square size={12} />
              <span>{eraserMode === 'box' ? '📦 Chọn Xóa: BẬT' : '📦 Chọn Xóa'}</span>
            </button>

            {eraserMode === 'brush' && setEraserBrushSize && (
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, paddingLeft: 4, borderLeft: '1px solid rgba(255,255,255,0.1)' }}>
                <span style={{ fontSize: 9.5, color: '#f59e0b', fontWeight: 700 }}>Cỡ cọ:</span>
                <input
                  type="range"
                  min="4"
                  max="100"
                  step="2"
                  value={eraserBrushSize}
                  onChange={(e) => setEraserBrushSize(parseInt(e.target.value, 10))}
                  style={{ width: 60, accentColor: '#f59e0b' }}
                />
                <span style={{ fontSize: 9.5, color: '#fde68a', fontWeight: 700, minWidth: 26 }}>{eraserBrushSize}px</span>
                <div style={{ display: 'flex', gap: 2 }}>
                  {[8, 16, 24, 40].map((sz) => (
                    <button
                      key={sz}
                      onClick={() => setEraserBrushSize(sz)}
                      style={{
                        padding: '1px 4px',
                        fontSize: 8.5,
                        fontWeight: 600,
                        background: eraserBrushSize === sz ? '#f59e0b' : 'rgba(255,255,255,0.08)',
                        color: eraserBrushSize === sz ? '#000' : '#cbd5e1',
                        border: 'none',
                        borderRadius: 3,
                        cursor: 'pointer',
                      }}
                    >
                      {sz}
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>
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
