// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React, { useRef, useEffect, useState } from 'react';
import { ZoomIn, ZoomOut, RotateCcw, Move, Grid } from 'lucide-react';

interface AnimationSlicerCanvasProps {
  image: HTMLImageElement | null;
  sliceMode: 'column' | 'grid';
  colDividers: number[];
  rowDividers: number[];
  selectedFrameIndex: number | null;
  onSelectFrameIndex: (index: number) => void;
  onUpdateColDividers: (dividers: number[]) => void;
  onUpdateRowDividers: (dividers: number[]) => void;
}

export const AnimationSlicerCanvas: React.FC<AnimationSlicerCanvasProps> = ({
  image,
  sliceMode,
  colDividers,
  rowDividers,
  selectedFrameIndex,
  onSelectFrameIndex,
  onUpdateColDividers,
  onUpdateRowDividers,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [zoom, setZoom] = useState<number>(1.0);
  const [pan, setPan] = useState<{ x: number; y: number }>({ x: 0, y: 0 });

  const isDraggingDividerRef = useRef<{ type: 'col' | 'row'; index: number } | null>(null);
  const isPanningRef = useRef<boolean>(false);
  const lastMousePosRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 });

  // Main Canvas Render
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const w = canvas.width;
    const h = canvas.height;

    ctx.clearRect(0, 0, w, h);

    // Checkerboard Background
    const sq = 12;
    for (let x = 0; x < w; x += sq) {
      for (let y = 0; y < h; y += sq) {
        ctx.fillStyle = (Math.floor(x / sq) + Math.floor(y / sq)) % 2 === 0 ? '#0f172a' : '#1e293b';
        ctx.fillRect(x, y, sq, sq);
      }
    }

    if (!image) {
      ctx.fillStyle = '#64748b';
      ctx.font = '12px sans-serif';
      ctx.textAlign = 'center';
      ctx.fillText('Chưa tải ảnh lên. Vui lòng chọn ảnh Sprite Sheet ở cột bên trái.', w / 2, h / 2);
      return;
    }

    ctx.save();
    ctx.translate(w / 2 + pan.x, h / 2 + pan.y);
    ctx.scale(zoom, zoom);
    ctx.translate(-image.width / 2, -image.height / 2);

    // Draw Source Image
    ctx.drawImage(image, 0, 0);

    const imgW = image.width;
    const imgH = image.height;

    // Draw Column Dividers
    colDividers.forEach((normX, idx) => {
      const posX = normX * imgW;
      ctx.strokeStyle = '#38bdf8';
      ctx.lineWidth = 2;
      ctx.setLineDash([6, 4]);
      ctx.beginPath();
      ctx.moveTo(posX, 0);
      ctx.lineTo(posX, imgH);
      ctx.stroke();
      ctx.setLineDash([]);

      // Top/Bottom Handle Pin
      ctx.fillStyle = '#38bdf8';
      ctx.fillRect(posX - 4, -8, 8, 8);
      ctx.fillRect(posX - 4, imgH, 8, 8);
    });

    // Draw Row Dividers if in Grid Mode
    if (sliceMode === 'grid') {
      rowDividers.forEach((normY) => {
        const posY = normY * imgH;
        ctx.strokeStyle = '#c084fc';
        ctx.lineWidth = 2;
        ctx.setLineDash([6, 4]);
        ctx.beginPath();
        ctx.moveTo(0, posY);
        ctx.lineTo(imgW, posY);
        ctx.stroke();
        ctx.setLineDash([]);

        ctx.fillStyle = '#c084fc';
        ctx.fillRect(-8, posY - 4, 8, 8);
        ctx.fillRect(imgW, posY - 4, 8, 8);
      });
    }

    // Draw Frame Index Badges (F1, F2, F3...)
    const fullCols = [0, ...colDividers, 1];
    const fullRows = sliceMode === 'grid' ? [0, ...rowDividers, 1] : [0, 1];
    let frameIdx = 0;

    for (let r = 0; r < fullRows.length - 1; r++) {
      for (let c = 0; c < fullCols.length - 1; c++) {
        const x1 = fullCols[c] * imgW;
        const x2 = fullCols[c + 1] * imgW;
        const y1 = fullRows[r] * imgH;
        const y2 = fullRows[r + 1] * imgH;

        const isSelected = selectedFrameIndex === frameIdx;

        // Frame Border Highlight
        if (isSelected) {
          ctx.strokeStyle = '#4ade80';
          ctx.lineWidth = 3;
          ctx.strokeRect(x1 + 2, y1 + 2, x2 - x1 - 4, y2 - y1 - 4);
        }

        // Frame Badge
        ctx.fillStyle = isSelected ? 'rgba(34, 197, 94, 0.9)' : 'rgba(15, 23, 42, 0.85)';
        ctx.fillRect(x1 + 6, y1 + 6, 40, 20);
        ctx.strokeStyle = isSelected ? '#4ade80' : 'rgba(255,255,255,0.2)';
        ctx.lineWidth = 1;
        ctx.strokeRect(x1 + 6, y1 + 6, 40, 20);

        ctx.fillStyle = '#ffffff';
        ctx.font = 'bold 11px sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(`F${frameIdx + 1}`, x1 + 26, y1 + 20);

        frameIdx++;
      }
    }

    ctx.restore();
  }, [image, sliceMode, colDividers, rowDividers, selectedFrameIndex, zoom, pan]);

  // Mouse Interaction: Drag Dividers or Pan Canvas
  const handleMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (!image) return;
    const canvas = canvasRef.current;
    if (!canvas) return;

    const rect = canvas.getBoundingClientRect();
    const clientX = e.clientX - rect.left;
    const clientY = e.clientY - rect.top;

    lastMousePosRef.current = { x: e.clientX, y: e.clientY };

    // Test divider hit
    const canvasCenterX = canvas.width / 2 + pan.x;
    const canvasCenterY = canvas.height / 2 + pan.y;

    const imgTopLeftX = canvasCenterX - (image.width / 2) * zoom;
    const imgTopLeftY = canvasCenterY - (image.height / 2) * zoom;

    // Check col dividers
    for (let i = 0; i < colDividers.length; i++) {
      const dividerScreenX = imgTopLeftX + colDividers[i] * image.width * zoom;
      if (Math.abs(clientX - dividerScreenX) <= 8) {
        isDraggingDividerRef.current = { type: 'col', index: i };
        return;
      }
    }

    // Check row dividers
    if (sliceMode === 'grid') {
      for (let i = 0; i < rowDividers.length; i++) {
        const dividerScreenY = imgTopLeftY + rowDividers[i] * image.height * zoom;
        if (Math.abs(clientY - dividerScreenY) <= 8) {
          isDraggingDividerRef.current = { type: 'row', index: i };
          return;
        }
      }
    }

    isPanningRef.current = true;
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (!image) return;
    const dx = e.clientX - lastMousePosRef.current.x;
    const dy = e.clientY - lastMousePosRef.current.y;
    lastMousePosRef.current = { x: e.clientX, y: e.clientY };

    if (isDraggingDividerRef.current) {
      const canvas = canvasRef.current;
      if (!canvas) return;
      const imgW = image.width * zoom;
      const imgH = image.height * zoom;

      if (isDraggingDividerRef.current.type === 'col') {
        const normDx = dx / imgW;
        const newCols = [...colDividers];
        const idx = isDraggingDividerRef.current.index;
        newCols[idx] = Math.max(0.05, Math.min(0.95, newCols[idx] + normDx));
        onUpdateColDividers(newCols);
      } else if (isDraggingDividerRef.current.type === 'row') {
        const normDy = dy / imgH;
        const newRows = [...rowDividers];
        const idx = isDraggingDividerRef.current.index;
        newRows[idx] = Math.max(0.05, Math.min(0.95, newRows[idx] + normDy));
        onUpdateRowDividers(newRows);
      }
      return;
    }

    if (isPanningRef.current) {
      setPan((p) => ({ x: p.x + dx, y: p.y + dy }));
    }
  };

  const handleMouseUp = () => {
    isDraggingDividerRef.current = null;
    isPanningRef.current = false;
  };

  return (
    <div
      style={{
        position: 'relative',
        width: '100%',
        height: '100%',
        background: '#020617',
        borderRadius: 8,
        overflow: 'hidden',
        border: '1px solid rgba(255,255,255,0.08)',
      }}
    >
      <canvas
        ref={canvasRef}
        width={960}
        height={540}
        onMouseDown={handleMouseDown}
        onMouseMove={handleMouseMove}
        onMouseUp={handleMouseUp}
        onMouseLeave={handleMouseUp}
        onWheel={(e) => {
          e.preventDefault();
          const zoomFactor = e.deltaY < 0 ? 1.1 : 0.9;
          setZoom((z) => Math.max(0.25, Math.min(4.0, z * zoomFactor)));
        }}
        style={{
          width: '100%',
          height: '100%',
          objectFit: 'contain',
          cursor: isDraggingDividerRef.current ? 'col-resize' : isPanningRef.current ? 'grabbing' : 'crosshair',
        }}
      />

      {/* Top Floating Zoom & Reset Toolbar */}
      <div
        style={{
          position: 'absolute',
          top: 10,
          right: 10,
          display: 'flex',
          alignItems: 'center',
          gap: 4,
          background: 'rgba(15, 23, 42, 0.92)',
          backdropFilter: 'blur(10px)',
          border: '1px solid rgba(255,255,255,0.1)',
          borderRadius: 6,
          padding: '4px 6px',
        }}
      >
        <button
          onClick={() => setZoom((z) => Math.max(0.25, z * 0.85))}
          title="Thu nhỏ"
          style={{ background: 'none', border: 'none', color: '#cbd5e1', cursor: 'pointer', padding: 2 }}
        >
          <ZoomOut size={13} />
        </button>

        <button
          onClick={() => {
            setZoom(1.0);
            setPan({ x: 0, y: 0 });
          }}
          title="Tỉ lệ 100%"
          style={{
            padding: '2px 5px',
            fontSize: 9.5,
            fontWeight: 700,
            color: '#38bdf8',
            background: 'none',
            border: 'none',
            cursor: 'pointer',
          }}
        >
          {Math.round(zoom * 100)}%
        </button>

        <button
          onClick={() => setZoom((z) => Math.min(4.0, z * 1.15))}
          title="Phóng to"
          style={{ background: 'none', border: 'none', color: '#cbd5e1', cursor: 'pointer', padding: 2 }}
        >
          <ZoomIn size={13} />
        </button>
      </div>
    </div>
  );
};
