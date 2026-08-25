import React from 'react';
import {
  Eraser,
  Square,
  Wand2,
  Hand,
  Feather,
  ZoomIn,
  ZoomOut,
  Undo2,
  Redo2,
  Droplet,
} from 'lucide-react';

export interface EraserToolbarProps {
  tool: 'brush' | 'rect_erase' | 'magic_wand' | 'pan';
  setTool: (tool: 'brush' | 'rect_erase' | 'magic_wand' | 'pan') => void;
  brushSize: number;
  setBrushSize: (size: number) => void;
  hardness: number;
  setHardness: (h: number) => void;
  opacity: number;
  setOpacity: (o: number) => void;
  flow: number;
  setFlow: (f: number) => void;
  magicTolerance: number;
  setMagicTolerance: (tol: number) => void;
  zoom: number;
  setZoom: (z: number) => void;
  onResetZoom100: () => void;
  onFitZoom: () => void;
  undoCount: number;
  redoCount: number;
  onUndo: () => void;
  onRedo: () => void;
  onAutoDespeckle: () => void;
  onDespillGreen: () => void;
}

export const EraserToolbar: React.FC<EraserToolbarProps> = ({
  tool,
  setTool,
  brushSize,
  setBrushSize,
  hardness,
  setHardness,
  opacity,
  setOpacity,
  flow,
  setFlow,
  magicTolerance,
  setMagicTolerance,
  zoom,
  setZoom,
  onResetZoom100,
  onFitZoom,
  undoCount,
  redoCount,
  onUndo,
  onRedo,
  onAutoDespeckle,
  onDespillGreen,
}) => {
  return (
    <div
      style={{
        padding: '8px 16px',
        background: '#080d1a',
        borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
        display: 'flex',
        alignItems: 'center',
        gap: 10,
        flexWrap: 'wrap',
      }}
    >
      {/* Tool Selectors */}
      <div style={{ display: 'flex', background: 'rgba(0,0,0,0.4)', padding: 3, borderRadius: 6, border: '1px solid rgba(255,255,255,0.08)', gap: 2 }}>
        <button
          onClick={() => setTool('brush')}
          style={{
            padding: '5px 10px',
            fontSize: 11,
            fontWeight: 600,
            borderRadius: 4,
            border: 'none',
            background: tool === 'brush' ? '#0284c7' : 'transparent',
            color: tool === 'brush' ? '#fff' : '#94a3b8',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: 5,
          }}
          title="Cọ Tẩy Photoshop (Bấm giữ và quét để xóa mờ mịn)"
        >
          <Eraser size={13} /> 🖌️ Cọ Tẩy (E)
        </button>

        <button
          onClick={() => setTool('rect_erase')}
          style={{
            padding: '5px 10px',
            fontSize: 11,
            fontWeight: 600,
            borderRadius: 4,
            border: 'none',
            background: tool === 'rect_erase' ? '#0284c7' : 'transparent',
            color: tool === 'rect_erase' ? '#fff' : '#94a3b8',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: 5,
          }}
          title="Xóa Theo Vùng Chữ Nhật"
        >
          <Square size={13} /> 🔲 Vùng Chọn Xóa
        </button>

        <button
          onClick={() => setTool('magic_wand')}
          style={{
            padding: '5px 10px',
            fontSize: 11,
            fontWeight: 600,
            borderRadius: 4,
            border: 'none',
            background: tool === 'magic_wand' ? '#0284c7' : 'transparent',
            color: tool === 'magic_wand' ? '#fff' : '#94a3b8',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: 5,
          }}
          title="Đũa Thần Tẩy Màu (Wand)"
        >
          <Wand2 size={13} /> 🪄 Tẩy Cụm Màu
        </button>

        <button
          onClick={() => setTool('pan')}
          style={{
            padding: '5px 10px',
            fontSize: 11,
            fontWeight: 600,
            borderRadius: 4,
            border: 'none',
            background: tool === 'pan' ? '#0284c7' : 'transparent',
            color: tool === 'pan' ? '#fff' : '#94a3b8',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: 5,
          }}
          title="Di Chuyển Khung (Pan)"
        >
          <Hand size={13} /> ✋ Kéo Canvas
        </button>
      </div>

      {/* Quick Brush Preset Buttons */}
      {tool === 'brush' && (
        <div style={{ display: 'flex', background: 'rgba(0,0,0,0.4)', padding: 3, borderRadius: 6, border: '1px solid rgba(255,255,255,0.08)', gap: 2 }}>
          <button
            onClick={() => {
              setHardness(0);
              setOpacity(50);
              setFlow(80);
            }}
            style={{
              padding: '5px 8px',
              fontSize: 10,
              fontWeight: hardness === 0 ? 700 : 400,
              borderRadius: 4,
              border: 'none',
              background: hardness === 0 ? '#0284c7' : 'transparent',
              color: hardness === 0 ? '#fff' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 3,
            }}
            title="Cọ Mềm Photoshop (Soft Round 0% Hardness): Xóa mịn màng viền tóc"
          >
            <Feather size={12} /> 🪶 Cọ Mềm (Soft 0%)
          </button>

          <button
            onClick={() => {
              setHardness(50);
              setOpacity(80);
              setFlow(90);
            }}
            style={{
              padding: '5px 8px',
              fontSize: 10,
              fontWeight: hardness === 50 ? 700 : 400,
              borderRadius: 4,
              border: 'none',
              background: hardness === 50 ? '#0284c7' : 'transparent',
              color: hardness === 50 ? '#fff' : '#94a3b8',
              cursor: 'pointer',
            }}
            title="Cọ Vừa (Medium 50% Hardness)"
          >
            🌓 Cọ Vừa (50%)
          </button>

          <button
            onClick={() => {
              setHardness(100);
              setOpacity(100);
              setFlow(100);
            }}
            style={{
              padding: '5px 8px',
              fontSize: 10,
              fontWeight: hardness === 100 ? 700 : 400,
              borderRadius: 4,
              border: 'none',
              background: hardness === 100 ? '#0284c7' : 'transparent',
              color: hardness === 100 ? '#fff' : '#94a3b8',
              cursor: 'pointer',
            }}
            title="Cọ Sắc Nét (Hard Round 100% Hardness)"
          >
            ⚡ Cọ Cứng (100%)
          </button>
        </div>
      )}

      {/* Adobe Photoshop Precision Sliders */}
      {tool === 'brush' && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, background: 'rgba(255,255,255,0.03)', padding: '3px 8px', borderRadius: 6, flexWrap: 'wrap' }}>
          {/* Size */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <span style={{ fontSize: 10.5, color: '#94a3b8' }}>Size: <b>{brushSize}px</b></span>
            <input
              type="range"
              min="1"
              max="100"
              value={brushSize}
              onChange={(e) => setBrushSize(parseInt(e.target.value))}
              style={{ width: 60, cursor: 'pointer' }}
            />
          </div>

          {/* Hardness */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, borderLeft: '1px solid rgba(255,255,255,0.1)', paddingLeft: 6 }}>
            <span style={{ fontSize: 10.5, color: '#38bdf8' }}>Hardness: <b>{hardness}%</b></span>
            <input
              type="range"
              min="0"
              max="100"
              value={hardness}
              onChange={(e) => setHardness(parseInt(e.target.value))}
              style={{ width: 55, cursor: 'pointer' }}
              title="Độ cứng cọ (0% = Siêu mềm mịn Gaussian, 100% = Sắc nét)"
            />
          </div>

          {/* Opacity */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, borderLeft: '1px solid rgba(255,255,255,0.1)', paddingLeft: 6 }}>
            <span style={{ fontSize: 10.5, color: '#4ade80' }}>Opacity: <b>{opacity}%</b></span>
            <input
              type="range"
              min="5"
              max="100"
              value={opacity}
              onChange={(e) => setOpacity(parseInt(e.target.value))}
              style={{ width: 55, cursor: 'pointer' }}
              title="Độ mờ tối đa trong 1 lần quét chuột (Ví dụ 50% sẽ không bao giờ bị xóa trắng khi kéo)"
            />
          </div>

          {/* Flow */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, borderLeft: '1px solid rgba(255,255,255,0.1)', paddingLeft: 6 }}>
            <span style={{ fontSize: 10.5, color: '#94a3b8' }}>Flow: <b>{flow}%</b></span>
            <input
              type="range"
              min="5"
              max="100"
              value={flow}
              onChange={(e) => setFlow(parseInt(e.target.value))}
              style={{ width: 50, cursor: 'pointer' }}
              title="Tốc độ ra lực xóa (Flow rate)"
            />
          </div>
        </div>
      )}

      {/* Magic Wand Tolerance */}
      {tool === 'magic_wand' && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, background: 'rgba(255,255,255,0.03)', padding: '3px 8px', borderRadius: 6 }}>
          <span style={{ fontSize: 11, color: '#94a3b8' }}>Độ nhạy màu: <b>{magicTolerance}%</b></span>
          <input
            type="range"
            min="5"
            max="80"
            value={magicTolerance}
            onChange={(e) => setMagicTolerance(parseInt(e.target.value))}
            style={{ width: 80, cursor: 'pointer' }}
          />
        </div>
      )}

      {/* Zoom Controls */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 5, background: 'rgba(255,255,255,0.03)', padding: '3px 8px', borderRadius: 6 }}>
        <button
          onClick={() => setZoom(Math.max(0.5, Math.round((zoom / 1.25) * 10) / 10))}
          style={{ padding: '3px 6px', borderRadius: 4, background: 'rgba(255,255,255,0.08)', color: '#fff', border: 'none', cursor: 'pointer' }}
          title="Thu nhỏ (-)"
        >
          <ZoomOut size={13} />
        </button>
        <span style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', minWidth: 42, textAlign: 'center', fontFamily: 'monospace' }}>
          {Math.round(zoom * 100)}%
        </span>
        <button
          onClick={() => setZoom(Math.min(32, Math.round((zoom * 1.25) * 10) / 10))}
          style={{ padding: '3px 6px', borderRadius: 4, background: 'rgba(255,255,255,0.08)', color: '#fff', border: 'none', cursor: 'pointer' }}
          title="Phóng to (+)"
        >
          <ZoomIn size={13} />
        </button>
        <button
          onClick={onResetZoom100}
          style={{ padding: '2px 6px', fontSize: 10, fontWeight: 600, borderRadius: 4, background: 'rgba(255,255,255,0.06)', color: '#94a3b8', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
          title="Khôi phục kích thước 100%"
        >
          100%
        </button>
        <button
          onClick={onFitZoom}
          style={{ padding: '2px 6px', fontSize: 10, fontWeight: 700, borderRadius: 4, background: 'rgba(56, 189, 248, 0.15)', color: '#38bdf8', border: '1px solid rgba(56, 189, 248, 0.3)', cursor: 'pointer' }}
          title="Vừa khít màn hình"
        >
          Fit
        </button>
      </div>

      {/* Undo / Redo & One-Click Despeckle */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 4, marginLeft: 'auto' }}>
        <button
          onClick={onUndo}
          disabled={undoCount <= 1}
          style={{
            padding: '4px 7px',
            fontSize: 10.5,
            borderRadius: 4,
            background: undoCount > 1 ? 'rgba(255,255,255,0.1)' : 'rgba(255,255,255,0.02)',
            color: undoCount > 1 ? '#f8fafc' : '#475569',
            border: 'none',
            cursor: undoCount > 1 ? 'pointer' : 'not-allowed',
            display: 'flex',
            alignItems: 'center',
            gap: 4,
          }}
          title="Hoàn tác (Ctrl+Z)"
        >
          <Undo2 size={12} /> Undo
        </button>

        <button
          onClick={onRedo}
          disabled={redoCount === 0}
          style={{
            padding: '4px 7px',
            fontSize: 10.5,
            borderRadius: 4,
            background: redoCount > 0 ? 'rgba(255,255,255,0.1)' : 'rgba(255,255,255,0.02)',
            color: redoCount > 0 ? '#f8fafc' : '#475569',
            border: 'none',
            cursor: redoCount > 0 ? 'pointer' : 'not-allowed',
            display: 'flex',
            alignItems: 'center',
            gap: 4,
          }}
          title="Làm lại (Ctrl+Y)"
        >
          <Redo2 size={12} /> Redo
        </button>

        <button
          onClick={onAutoDespeckle}
          style={{
            padding: '4px 9px',
            fontSize: 10.5,
            fontWeight: 600,
            borderRadius: 4,
            background: 'rgba(56, 189, 248, 0.15)',
            color: '#38bdf8',
            border: '1px solid rgba(56, 189, 248, 0.3)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: 4,
          }}
          title="Tự động quét và xóa sạch các hạt đốm trắng li ti trong ô này"
        >
          <Wand2 size={12} /> Auto-Xóa Cặn rác
        </button>

        <button
          onClick={onDespillGreen}
          style={{
            padding: '4px 9px',
            fontSize: 10.5,
            fontWeight: 600,
            borderRadius: 4,
            background: 'rgba(34, 197, 94, 0.15)',
            color: '#4ade80',
            border: '1px solid rgba(34, 197, 94, 0.3)',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: 5,
          }}
          title="Khử viền xanh (Despill Green) - Khắc phục hắt sáng xanh vào tóc"
        >
          <Droplet size={12} /> Khử Viền Xanh
        </button>
      </div>
    </div>
  );
};
