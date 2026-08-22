import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  Box,
  Compass,
  ZoomIn,
  ZoomOut,
  Maximize2,
  Wand2,
  Move,
  Sparkles,
} from 'lucide-react';
import {
  Character2DAssembly,
  Character2DPartType,
  Character2DPartConfig,
} from '../../../types/scene2d';
import {
  ThreeMultiAngleBillboardEngine,
  AngleDetectionResult,
} from '../../../core/engine2d/ThreeMultiAngleBillboardEngine';
import {
  Canvas2DPuppetEngine,
  PuppetAnimationState,
} from '../../../core/engine2d/Canvas2DPuppetEngine';

interface AssemblerMainViewportProps {
  assembly: Character2DAssembly;
  selectedSlot: Character2DPartType;
  onChangeAssembly: (updated: Character2DAssembly) => void;
  animMode: 'idle' | 'breathe' | 'talk' | 'combat_slash';
  isPlaying: boolean;
  isBlinking: boolean;
  isTalking: boolean;
  onAutoAlignAnatomy: () => void;
}

export const AssemblerMainViewport: React.FC<AssemblerMainViewportProps> = ({
  assembly,
  selectedSlot,
  onChangeAssembly,
  animMode,
  isPlaying,
  isBlinking,
  isTalking,
  onAutoAlignAnatomy,
}) => {
  const [viewportMode, setViewportMode] = useState<'3d_multi_angle' | '2d_canvas'>('2d_canvas');
  const [turntableAngle, setTurntableAngle] = useState<number>(0);
  const [angleInfo, setAngleInfo] = useState<AngleDetectionResult>({
    angleDeg: 0,
    discreteAngle: 'front',
    angleLabel: 'Chính diện (0°)',
    compassDirection: 'S',
  });

  // 2D Canvas Zoom & Pan
  const [zoomLevel, setZoomLevel] = useState<number>(0.85);
  const [panOffset, setPanOffset] = useState<[number, number]>([0, 10]);
  const [isDraggingPart, setIsDraggingPart] = useState<boolean>(false);
  const [isPanningCanvas, setIsPanningCanvas] = useState<boolean>(false);

  const dragStartRef = useRef<{
    mouseX: number;
    mouseY: number;
    initialOffset: [number, number];
    initialPan: [number, number];
  }>({
    mouseX: 0,
    mouseY: 0,
    initialOffset: [0, 0],
    initialPan: [0, 0],
  });

  const canvas2dRef = useRef<HTMLCanvasElement>(null);
  const threeContainerRef = useRef<HTMLDivElement>(null);
  const canvasEngineRef = useRef<Canvas2DPuppetEngine>(new Canvas2DPuppetEngine());
  const threeEngineRef = useRef<ThreeMultiAngleBillboardEngine | null>(null);
  const animTimeRef = useRef<number>(0);
  const slashProgressRef = useRef<number>(0);

  // Initialize Three.js 3D Engine
  useEffect(() => {
    if (viewportMode === '3d_multi_angle' && threeContainerRef.current) {
      if (!threeEngineRef.current) {
        threeEngineRef.current = new ThreeMultiAngleBillboardEngine(
          threeContainerRef.current,
          (res: AngleDetectionResult) => {
            setAngleInfo(res);
            setTurntableAngle(res.angleDeg);
          }
        );
      }
      threeEngineRef.current.setAssembly(assembly);
    }
    return () => {
      if (viewportMode !== '3d_multi_angle' && threeEngineRef.current) {
        threeEngineRef.current.dispose();
        threeEngineRef.current = null;
      }
    };
  }, [viewportMode]);

  // Sync assembly updates to 3D engine
  useEffect(() => {
    if (threeEngineRef.current && viewportMode === '3d_multi_angle') {
      threeEngineRef.current.setAssembly(assembly);
    }
  }, [assembly, viewportMode]);

  // 2D Render Loop
  const renderCanvas = useCallback(() => {
    if (viewportMode !== '2d_canvas' || !canvas2dRef.current) return;
    const canvas = canvas2dRef.current;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    if (isPlaying) animTimeRef.current += 0.025;

    const blinkCycle = Math.floor(animTimeRef.current * 1.5) % 8 === 0;
    const shouldBlink = isBlinking || blinkCycle;

    if (animMode === 'combat_slash') {
      slashProgressRef.current = (slashProgressRef.current + 0.02) % 1.0;
    } else {
      slashProgressRef.current = 0;
    }

    const animState: PuppetAnimationState = {
      mode: animMode,
      time: animTimeRef.current,
      isBlinking: shouldBlink,
      isTalking: isTalking || animMode === 'talk',
      slashProgress: slashProgressRef.current,
      shakeOffset: [0, 0],
      zoomFactor: 1.0,
    };

    // Canvas Background
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = '#080c14';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Faint grid
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.03)';
    ctx.lineWidth = 1;
    const gridSize = 40;
    for (let x = 0; x < canvas.width; x += gridSize) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, canvas.height);
      ctx.stroke();
    }
    for (let y = 0; y < canvas.height; y += gridSize) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(canvas.width, y);
      ctx.stroke();
    }

    // Ground stage line
    const groundY = canvas.height * 0.52 + panOffset[1] + 150 * zoomLevel * (assembly.base_scale || 1);
    ctx.strokeStyle = 'rgba(56, 189, 248, 0.2)';
    ctx.lineWidth = 1.5;
    ctx.setLineDash([6, 6]);
    ctx.beginPath();
    ctx.moveTo(canvas.width * 0.15, groundY);
    ctx.lineTo(canvas.width * 0.85, groundY);
    ctx.stroke();
    ctx.setLineDash([]);

    // Draw Character
    ctx.save();
    const centerX = canvas.width / 2 + panOffset[0];
    const centerY = canvas.height * 0.5 + panOffset[1];

    canvasEngineRef.current.renderCharacter(
      ctx,
      assembly,
      animState,
      centerX,
      centerY,
      zoomLevel * (assembly.base_scale || 1.0),
      selectedSlot
    );
    ctx.restore();
  }, [viewportMode, isPlaying, isBlinking, isTalking, animMode, assembly, panOffset, zoomLevel, selectedSlot]);

  useEffect(() => {
    let animId: number;
    const loop = () => {
      renderCanvas();
      animId = requestAnimationFrame(loop);
    };
    if (viewportMode === '2d_canvas') {
      animId = requestAnimationFrame(loop);
    }
    return () => cancelAnimationFrame(animId);
  }, [viewportMode, renderCanvas]);

  // Mouse Drag to Move Selected Part or Pan Canvas
  const handleMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (viewportMode !== '2d_canvas') return;
    const partCfg = assembly.parts[selectedSlot] || { offset: [0, 0] };
    dragStartRef.current = {
      mouseX: e.clientX,
      mouseY: e.clientY,
      initialOffset: [...(partCfg.offset || [0, 0])],
      initialPan: [...panOffset],
    };

    if (e.shiftKey || e.button === 1) {
      setIsPanningCanvas(true);
    } else {
      setIsDraggingPart(true);
    }
  };

  const handleMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (!isDraggingPart && !isPanningCanvas) return;
    const dx = e.clientX - dragStartRef.current.mouseX;
    const dy = e.clientY - dragStartRef.current.mouseY;

    if (isPanningCanvas) {
      setPanOffset([
        dragStartRef.current.initialPan[0] + dx,
        dragStartRef.current.initialPan[1] + dy,
      ]);
    } else if (isDraggingPart) {
      const scaleAdj = zoomLevel * (assembly.base_scale || 1.0);
      const newOffsetX = Math.round(dragStartRef.current.initialOffset[0] + dx / scaleAdj);
      const newOffsetY = Math.round(dragStartRef.current.initialOffset[1] + dy / scaleAdj);

      const curPart = assembly.parts[selectedSlot] || {};
      onChangeAssembly({
        ...assembly,
        parts: {
          ...assembly.parts,
          [selectedSlot]: {
            ...curPart,
            offset: [newOffsetX, newOffsetY],
          },
        },
      });
    }
  };

  const handleMouseUp = () => {
    setIsDraggingPart(false);
    setIsPanningCanvas(false);
  };

  const handleAngleJump = (deg: number) => {
    setTurntableAngle(deg);
    if (threeEngineRef.current) {
      threeEngineRef.current.jumpToAngle(deg);
    }
  };

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 6, minHeight: 0, overflow: 'hidden' }}>
      {/* Top Viewport Navigation Bar */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          background: 'rgba(15, 23, 42, 0.85)',
          padding: '4px 8px',
          borderRadius: 6,
          border: '1px solid rgba(255, 255, 255, 0.08)',
        }}
      >
        {/* Mode switcher tabs */}
        <div style={{ display: 'flex', gap: 3, background: 'rgba(0,0,0,0.4)', padding: 2, borderRadius: 5 }}>
          <button
            onClick={() => setViewportMode('2d_canvas')}
            style={{
              padding: '3px 9px',
              fontSize: 10.5,
              fontWeight: 700,
              borderRadius: 4,
              border: 'none',
              background: viewportMode === '2d_canvas' ? '#0284c7' : 'transparent',
              color: viewportMode === '2d_canvas' ? '#fff' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            🎨 Bàn Canvas 2D
          </button>

          <button
            onClick={() => setViewportMode('3d_multi_angle')}
            style={{
              padding: '3px 9px',
              fontSize: 10.5,
              fontWeight: 700,
              borderRadius: 4,
              border: 'none',
              background: viewportMode === '3d_multi_angle' ? '#0284c7' : 'transparent',
              color: viewportMode === '3d_multi_angle' ? '#fff' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <Box size={12} /> Không Gian 3D Đa Góc
          </button>
        </div>

        {/* Viewport Tools */}
        {viewportMode === '2d_canvas' ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <button
              onClick={onAutoAlignAnatomy}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 3,
                padding: '2px 7px',
                fontSize: 9.5,
                fontWeight: 700,
                borderRadius: 4,
                background: 'rgba(56, 189, 248, 0.15)',
                color: '#38bdf8',
                border: '1px solid rgba(56, 189, 248, 0.3)',
                cursor: 'pointer',
              }}
              title="Tự động căn chuẩn giải phẫu toàn bộ các khớp nối"
            >
              <Wand2 size={11} /> Căn Chuẩn
            </button>
            <button
              onClick={() => setZoomLevel((z) => Math.max(0.3, z - 0.1))}
              style={{ padding: '2px 5px', fontSize: 10, borderRadius: 4, background: 'rgba(255,255,255,0.06)', color: '#e2e8f0', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
            >
              <ZoomOut size={11} />
            </button>
            <span style={{ fontSize: 9.5, color: '#38bdf8', minWidth: 32, textAlign: 'center' }}>
              {Math.round(zoomLevel * 100)}%
            </span>
            <button
              onClick={() => setZoomLevel((z) => Math.min(2.5, z + 0.1))}
              style={{ padding: '2px 5px', fontSize: 10, borderRadius: 4, background: 'rgba(255,255,255,0.06)', color: '#e2e8f0', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
            >
              <ZoomIn size={11} />
            </button>
            <button
              onClick={() => {
                setZoomLevel(0.85);
                setPanOffset([0, 10]);
              }}
              style={{ padding: '2px 6px', fontSize: 9.5, borderRadius: 4, background: 'rgba(255,255,255,0.06)', color: '#e2e8f0', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
            >
              <Maximize2 size={11} /> Căn Giữa
            </button>
          </div>
        ) : (
          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <span style={{ fontSize: 9.5, color: '#94a3b8' }}>Góc nhanh:</span>
            {[
              { label: '0° Thẳng', deg: 0 },
              { label: '45° 3/4', deg: 45 },
              { label: '90° Ngang', deg: 90 },
              { label: '180° Sau', deg: 180 },
            ].map((btn) => (
              <button
                key={btn.deg}
                onClick={() => handleAngleJump(btn.deg)}
                style={{
                  padding: '2px 6px',
                  fontSize: 9.5,
                  borderRadius: 4,
                  background: 'rgba(255,255,255,0.06)',
                  color: '#e2e8f0',
                  border: '1px solid rgba(255,255,255,0.1)',
                  cursor: 'pointer',
                }}
              >
                {btn.label}
              </button>
            ))}
          </div>
        )}
      </div>

      {/* Main Screen Container */}
      <div
        style={{
          flex: 1,
          borderRadius: 8,
          overflow: 'hidden',
          border: '1px solid rgba(56, 189, 248, 0.3)',
          background: '#040711',
          position: 'relative',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        {viewportMode === '2d_canvas' ? (
          <canvas
            ref={canvas2dRef}
            width={600}
            height={480}
            onMouseDown={handleMouseDown}
            onMouseMove={handleMouseMove}
            onMouseUp={handleMouseUp}
            onMouseLeave={handleMouseUp}
            style={{
              width: '100%',
              height: '100%',
              display: 'block',
              cursor: isDraggingPart ? 'grabbing' : isPanningCanvas ? 'move' : 'grab',
            }}
          />
        ) : (
          <div ref={threeContainerRef} style={{ width: '100%', height: '100%' }} />
        )}

        {/* Floating Hint Overlay */}
        <div
          style={{
            position: 'absolute',
            bottom: 8,
            left: 8,
            padding: '3px 8px',
            borderRadius: 4,
            background: 'rgba(0,0,0,0.7)',
            fontSize: 9,
            color: '#64748b',
          }}
        >
          {viewportMode === '2d_canvas'
            ? '💡 Bấm giữ chuột để kéo di chuyển linh kiện đang chọn | Shift + Kéo để dịch Canvas'
            : '💡 Kéo chuột để xoay nhân vật 360°'}
        </div>

        {viewportMode === '3d_multi_angle' && (
          <div
            style={{
              position: 'absolute',
              top: 8,
              right: 8,
              padding: '2px 8px',
              borderRadius: 4,
              background: 'rgba(0, 0, 0, 0.75)',
              border: '1px solid rgba(56, 189, 248, 0.4)',
              color: '#38bdf8',
              fontSize: 9.5,
              fontWeight: 700,
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <Compass size={12} /> {angleInfo.compassDirection} • {angleInfo.angleLabel}
          </div>
        )}
      </div>

      {/* 360 Turntable Slider */}
      {viewportMode === '3d_multi_angle' && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            background: 'rgba(15, 23, 42, 0.85)',
            padding: '4px 10px',
            borderRadius: 6,
            border: '1px solid rgba(255, 255, 255, 0.08)',
          }}
        >
          <span style={{ fontSize: 9.5, color: '#94a3b8', whiteSpace: 'nowrap' }}>Xoay Camera 360°:</span>
          <input
            type="range"
            min="0"
            max="360"
            value={turntableAngle}
            onChange={(e) => handleAngleJump(parseInt(e.target.value))}
            style={{ flex: 1 }}
          />
          <span style={{ fontSize: 9.5, color: '#38bdf8', fontWeight: 700, minWidth: 32 }}>
            {turntableAngle}°
          </span>
        </div>
      )}
    </div>
  );
};
