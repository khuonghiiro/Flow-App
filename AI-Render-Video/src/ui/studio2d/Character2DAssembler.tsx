import React, { useState, useRef, useEffect, useCallback } from 'react';
import {
  Layers,
  Play,
  Pause,
  RotateCcw,
  Sparkles,
  Save,
  Sliders,
  Compass,
  Box,
  Image as ImageIcon,
  ZoomIn,
  ZoomOut,
  Maximize2,
  Move,
  Wand2,
} from 'lucide-react';
import {
  Character2DAssembly,
  Character2DPartType,
  Character2DPartConfig,
  Character2DAngle,
} from '../../types/scene2d';
import {
  PART_HIERARCHY_CONFIG,
  generateDemoPartSvg,
} from '../../core/assets/Asset2DRegistry';
import {
  Canvas2DPuppetEngine,
  PuppetAnimationState,
} from '../../core/engine2d/Canvas2DPuppetEngine';
import {
  ThreeMultiAngleBillboardEngine,
  AngleDetectionResult,
} from '../../core/engine2d/ThreeMultiAngleBillboardEngine';

interface Character2DAssemblerProps {
  assembly: Character2DAssembly;
  onChangeAssembly: (updated: Character2DAssembly) => void;
  onSaveCharacter?: (saved: Character2DAssembly) => void;
}

export const Character2DAssembler: React.FC<Character2DAssemblerProps> = ({
  assembly,
  onChangeAssembly,
  onSaveCharacter,
}) => {
  const [viewportMode, setViewportMode] = useState<'3d_multi_angle' | '2d_canvas'>('2d_canvas');
  const [selectedSlot, setSelectedSlot] = useState<Character2DPartType>('dau');
  const [animMode, setAnimMode] = useState<'idle' | 'breathe' | 'talk' | 'combat_slash'>('breathe');
  const [isPlaying, setIsPlaying] = useState<boolean>(true);
  const [isBlinking, setIsBlinking] = useState<boolean>(false);
  const [isTalking, setIsTalking] = useState<boolean>(false);
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [saveSuccessMsg, setSaveSuccessMsg] = useState<string | null>(null);

  // 2D Canvas Viewport Zoom & Pan Navigation
  const [zoomLevel, setZoomLevel] = useState<number>(0.85);
  const [panOffset, setPanOffset] = useState<[number, number]>([0, 10]);
  const [isDraggingPart, setIsDraggingPart] = useState<boolean>(false);
  const [isPanningCanvas, setIsPanningCanvas] = useState<boolean>(false);
  const dragStartRef = useRef<{ mouseX: number; mouseY: number; initialOffset: [number, number]; initialPan: [number, number] }>({
    mouseX: 0,
    mouseY: 0,
    initialOffset: [0, 0],
    initialPan: [0, 0],
  });

  // 3D Engine & Angle Detection
  const [angleInfo, setAngleInfo] = useState<AngleDetectionResult>({
    angleDeg: 0,
    discreteAngle: 'front',
    angleLabel: 'Chính Diện (0°)',
    compassDirection: 'N',
  });

  const canvas2dRef = useRef<HTMLCanvasElement>(null);
  const threeContainerRef = useRef<HTMLDivElement>(null);
  const canvasEngineRef = useRef<Canvas2DPuppetEngine>(new Canvas2DPuppetEngine());
  const threeEngineRef = useRef<ThreeMultiAngleBillboardEngine | null>(null);
  const animTimeRef = useRef<number>(0);
  const animFrameIdRef = useRef<number | null>(null);
  const slashProgressRef = useRef<number>(0);

  // Selected part config
  const selectedPart = assembly.parts[selectedSlot] || {
    path: '',
    offset: PART_HIERARCHY_CONFIG[selectedSlot]?.defaultOffset || [0, 0],
    scale: [1, 1],
    rotation: 0,
    pivot: PART_HIERARCHY_CONFIG[selectedSlot]?.defaultPivot || [0.5, 0.5],
    flipX: false,
    flipY: false,
    z_index: PART_HIERARCHY_CONFIG[selectedSlot]?.defaultZ || 5,
    z_depth_3d: PART_HIERARCHY_CONFIG[selectedSlot]?.defaultZDepth3D || 0,
    opacity: 1,
  };

  const updatePartConfig = (updates: Partial<Character2DPartConfig>) => {
    onChangeAssembly({
      ...assembly,
      parts: {
        ...assembly.parts,
        [selectedSlot]: {
          ...selectedPart,
          ...updates,
        },
      },
    });
  };

  // ─── 1. Initialize Three.js 3D Engine ──────────────────────────
  useEffect(() => {
    if (viewportMode === '3d_multi_angle' && threeContainerRef.current) {
      if (!threeEngineRef.current) {
        threeEngineRef.current = new ThreeMultiAngleBillboardEngine(
          threeContainerRef.current,
          (res) => setAngleInfo(res)
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

  // ─── 2. 2D Canvas Interactive Render Loop ──────────────────────
  const renderCanvasLoop = useCallback(() => {
    if (viewportMode === '2d_canvas') {
      const canvas = canvas2dRef.current;
      if (!canvas) return;
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

      // Clear & Background
      ctx.clearRect(0, 0, canvas.width, canvas.height);
      ctx.fillStyle = '#080c14';
      ctx.fillRect(0, 0, canvas.width, canvas.height);

      // Draw faint background grid
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

      // Draw ground stage guide line
      const groundY = canvas.height * 0.52 + panOffset[1] + (150 * zoomLevel * (assembly.base_scale || 1));
      ctx.strokeStyle = 'rgba(56, 189, 248, 0.2)';
      ctx.lineWidth = 1.5;
      ctx.setLineDash([6, 6]);
      ctx.beginPath();
      ctx.moveTo(canvas.width * 0.15, groundY);
      ctx.lineTo(canvas.width * 0.85, groundY);
      ctx.stroke();
      ctx.setLineDash([]);

      // Render Character with Zoom & Pan transformations
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
    }

    animFrameIdRef.current = requestAnimationFrame(renderCanvasLoop);
  }, [assembly, selectedSlot, animMode, isPlaying, isBlinking, isTalking, viewportMode, zoomLevel, panOffset]);

  useEffect(() => {
    animFrameIdRef.current = requestAnimationFrame(renderCanvasLoop);
    return () => {
      if (animFrameIdRef.current) cancelAnimationFrame(animFrameIdRef.current);
    };
  }, [renderCanvasLoop]);

  // ─── 3. Mouse Zoom, Pan & Drag to Move Parts ───────────────────
  const handleCanvasWheel = (e: React.WheelEvent<HTMLCanvasElement>) => {
    e.preventDefault();
    const zoomDelta = e.deltaY < 0 ? 0.08 : -0.08;
    setZoomLevel((prev) => Math.min(Math.max(0.3, prev + zoomDelta), 2.5));
  };

  const handleCanvasMouseDown = (e: React.MouseEvent<HTMLCanvasElement>) => {
    const isMiddleOrAlt = e.button === 1 || e.button === 2 || e.altKey || e.shiftKey;
    dragStartRef.current = {
      mouseX: e.clientX,
      mouseY: e.clientY,
      initialOffset: [...(selectedPart.offset || [0, 0])] as [number, number],
      initialPan: [...panOffset] as [number, number],
    };

    if (isMiddleOrAlt) {
      setIsPanningCanvas(true);
    } else {
      setIsDraggingPart(true);
    }
  };

  const handleCanvasMouseMove = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (isPanningCanvas) {
      const dx = e.clientX - dragStartRef.current.mouseX;
      const dy = e.clientY - dragStartRef.current.mouseY;
      setPanOffset([
        dragStartRef.current.initialPan[0] + dx,
        dragStartRef.current.initialPan[1] + dy,
      ]);
    } else if (isDraggingPart) {
      const scale = zoomLevel * (assembly.base_scale || 1.0);
      const dx = (e.clientX - dragStartRef.current.mouseX) / scale;
      const dy = (e.clientY - dragStartRef.current.mouseY) / scale;

      updatePartConfig({
        offset: [
          Math.round(dragStartRef.current.initialOffset[0] + dx),
          Math.round(dragStartRef.current.initialOffset[1] + dy),
        ],
      });
    }
  };

  const handleCanvasMouseUp = () => {
    setIsDraggingPart(false);
    setIsPanningCanvas(false);
  };

  // ─── 4. Auto-Align Anatomy Helper ──────────────────────────────
  const handleAutoAlignAnatomy = () => {
    const newParts = { ...assembly.parts };
    for (const [slotKey, config] of Object.entries(newParts)) {
      const slot = slotKey as Character2DPartType;
      const hierarchy = PART_HIERARCHY_CONFIG[slot];
      if (hierarchy && config) {
        newParts[slot] = {
          ...config,
          offset: [...hierarchy.defaultOffset],
          pivot: [...hierarchy.defaultPivot],
          scale: [1, 1],
          rotation: slot === 'canh_tay_trai' ? 12 : slot === 'canh_tay_phai' ? -18 : slot === 'vu_khi' ? -15 : 0,
        };
      }
    }
    onChangeAssembly({
      ...assembly,
      base_scale: 1.0,
      parts: newParts,
    });
    setPanOffset([0, 10]);
    setZoomLevel(0.85);
    setSaveSuccessMsg('⚡ Đã tự động căn chuẩn giải phẫu cơ thể!');
    setTimeout(() => setSaveSuccessMsg(null), 2500);
  };

  // ─── 5. Save Assembly ──────────────────────────────────────────
  const handleSaveAssembly = async () => {
    setIsSaving(true);
    try {
      const response = await fetch('/api/save-2d-character', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: assembly.name || 'nhan_vat_2d',
          profileData: {
            ...assembly,
            preview_image: `asset_2ds/nhan_vat/_lap_rap/${assembly.id || 'char_2d'}.png`,
          },
        }),
      });

      if (response.ok) {
        setSaveSuccessMsg('✅ Đã lưu cấu hình vào asset_2ds/nhan_vat/_lap_rap/!');
      } else {
        downloadJsonAssembly();
        setSaveSuccessMsg('✅ Đã xuất tệp cấu hình JSON!');
      }
    } catch {
      downloadJsonAssembly();
      setSaveSuccessMsg('✅ Đã xuất tệp cấu hình JSON!');
    } finally {
      setIsSaving(false);
      setTimeout(() => setSaveSuccessMsg(null), 3000);
      if (onSaveCharacter) onSaveCharacter(assembly);
    }
  };

  const downloadJsonAssembly = () => {
    const dataStr = 'data:text/json;charset=utf-8,' + encodeURIComponent(JSON.stringify(assembly, null, 2));
    const a = document.createElement('a');
    a.href = dataStr;
    a.download = `${assembly.id || 'character_2d'}.json`;
    a.click();
  };

  const allSlots = Object.keys(PART_HIERARCHY_CONFIG) as Character2DPartType[];

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '250px 1fr 330px', gap: 14, height: '100%', overflow: 'hidden' }}>
      {/* ─── LEFT: Layer / Part Tree ─────────────────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, background: 'rgba(15, 23, 42, 0.7)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', overflowY: 'auto' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Layers size={13} /> LINH KIỆN & LỚP
          </div>
          <button
            onClick={handleAutoAlignAnatomy}
            title="Căn chuẩn lại toàn bộ vị trí giải phẫu chuẩn"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '3px 8px',
              fontSize: 10,
              borderRadius: 4,
              background: 'rgba(56, 189, 248, 0.15)',
              color: '#38bdf8',
              border: '1px solid rgba(56, 189, 248, 0.3)',
              cursor: 'pointer',
            }}
          >
            <Wand2 size={11} /> Căn Chuẩn
          </button>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          {allSlots.map((slot) => {
            const hasPart = Boolean(assembly.parts[slot]?.path);
            const isSelected = slot === selectedSlot;

            return (
              <div
                key={slot}
                onClick={() => setSelectedSlot(slot)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '6px 10px',
                  borderRadius: 6,
                  fontSize: 11,
                  background: isSelected ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.02)',
                  border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.04)',
                  color: isSelected ? '#ffffff' : hasPart ? '#e2e8f0' : '#64748b',
                  cursor: 'pointer',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span style={{ width: 8, height: 8, borderRadius: '50%', background: hasPart ? '#22c55e' : '#475569' }} />
                  <span>{PART_HIERARCHY_CONFIG[slot]?.label || slot}</span>
                </div>
                <span style={{ fontSize: 9, color: '#38bdf8' }}>Z:{assembly.parts[slot]?.z_index ?? PART_HIERARCHY_CONFIG[slot]?.defaultZ}</span>
              </div>
            );
          })}
        </div>
      </div>

      {/* ─── CENTER: Canvas Viewport & Navigation ──────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, position: 'relative' }}>
        {/* Top Viewport Mode Switcher & Navigation Tools */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', background: 'rgba(15, 23, 42, 0.8)', padding: '6px 10px', borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)' }}>
          {/* Mode Tabs */}
          <div style={{ display: 'flex', gap: 4, background: 'rgba(0,0,0,0.4)', padding: 3, borderRadius: 6 }}>
            <button
              onClick={() => setViewportMode('2d_canvas')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 5,
                padding: '4px 10px',
                borderRadius: 4,
                fontSize: 11,
                fontWeight: 600,
                border: 'none',
                background: viewportMode === '2d_canvas' ? '#0284c7' : 'transparent',
                color: viewportMode === '2d_canvas' ? '#fff' : '#94a3b8',
                cursor: 'pointer',
              }}
            >
              <ImageIcon size={12} /> Canvas 2D
            </button>
            <button
              onClick={() => setViewportMode('3d_multi_angle')}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 5,
                padding: '4px 10px',
                borderRadius: 4,
                fontSize: 11,
                fontWeight: 600,
                border: 'none',
                background: viewportMode === '3d_multi_angle' ? '#0284c7' : 'transparent',
                color: viewportMode === '3d_multi_angle' ? '#fff' : '#94a3b8',
                cursor: 'pointer',
              }}
            >
              <Box size={12} /> Không Gian 3D Đa Góc
            </button>
          </div>

          {/* 2D Zoom & Viewport Tools */}
          {viewportMode === '2d_canvas' ? (
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <button
                onClick={() => setZoomLevel((z) => Math.max(0.3, z - 0.1))}
                title="Thu nhỏ Canvas"
                style={{ padding: '3px 7px', fontSize: 11, borderRadius: 4, background: 'rgba(255,255,255,0.06)', color: '#e2e8f0', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
              >
                <ZoomOut size={12} />
              </button>
              <span style={{ fontSize: 10, color: '#38bdf8', minWidth: 36, textAlign: 'center' }}>
                {Math.round(zoomLevel * 100)}%
              </span>
              <button
                onClick={() => setZoomLevel((z) => Math.min(2.5, z + 0.1))}
                title="Phóng to Canvas"
                style={{ padding: '3px 7px', fontSize: 11, borderRadius: 4, background: 'rgba(255,255,255,0.06)', color: '#e2e8f0', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
              >
                <ZoomIn size={12} />
              </button>
              <button
                onClick={() => {
                  setZoomLevel(0.85);
                  setPanOffset([0, 10]);
                }}
                title="Căn vừa màn hình & reset vị trí"
                style={{ display: 'flex', alignItems: 'center', gap: 4, padding: '3px 8px', fontSize: 10, borderRadius: 4, background: 'rgba(255,255,255,0.06)', color: '#e2e8f0', border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer' }}
              >
                <Maximize2 size={11} /> Căn Giữa
              </button>
            </div>
          ) : (
            <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
              <span style={{ fontSize: 10, color: '#94a3b8' }}>Góc quay nhanh:</span>
              {[
                { label: '0° Thẳng', deg: 0 },
                { label: '45° 3/4', deg: 45 },
                { label: '90° Ngang', deg: 90 },
                { label: '180° Sau', deg: 180 },
              ].map((btn) => (
                <button
                  key={btn.deg}
                  onClick={() => threeEngineRef.current?.jumpToAngle(btn.deg)}
                  style={{
                    padding: '3px 7px',
                    fontSize: 10,
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

        {/* Viewport Container */}
        <div
          style={{
            flex: 1,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            background: '#080c14',
            borderRadius: 8,
            border: '1px solid rgba(255,255,255,0.08)',
            position: 'relative',
            overflow: 'hidden',
          }}
        >
          {viewportMode === '3d_multi_angle' ? (
            <div ref={threeContainerRef} style={{ width: '100%', height: '100%', cursor: 'grab' }} />
          ) : (
            <canvas
              ref={canvas2dRef}
              width={750}
              height={520}
              onWheel={handleCanvasWheel}
              onMouseDown={handleCanvasMouseDown}
              onMouseMove={handleCanvasMouseMove}
              onMouseUp={handleCanvasMouseUp}
              onMouseLeave={handleCanvasMouseUp}
              style={{
                width: '100%',
                height: '100%',
                cursor: isPanningCanvas ? 'grabbing' : isDraggingPart ? 'move' : 'crosshair',
              }}
            />
          )}

          {/* Interactive Drag Hint Overlay in 2D */}
          {viewportMode === '2d_canvas' && (
            <div
              style={{
                position: 'absolute',
                bottom: 10,
                left: 12,
                fontSize: 10,
                color: '#64748b',
                background: 'rgba(15, 23, 42, 0.75)',
                padding: '4px 10px',
                borderRadius: 6,
                border: '1px solid rgba(255,255,255,0.05)',
                pointerEvents: 'none',
              }}
            >
              🖱️ <b>Kéo chuột trái:</b> Di chuyển linh kiện • <b>Cuộn chuột:</b> Phóng to/thu nhỏ • <b>Shift+Kéo:</b> Pan Canvas
            </div>
          )}

          {/* Floating Angle Radar Indicator in 3D */}
          {viewportMode === '3d_multi_angle' && (
            <div
              style={{
                position: 'absolute',
                top: 12,
                right: 12,
                background: 'rgba(15, 23, 42, 0.85)',
                backdropFilter: 'blur(8px)',
                padding: '6px 12px',
                borderRadius: 20,
                border: '1px solid rgba(56, 189, 248, 0.4)',
                display: 'flex',
                alignItems: 'center',
                gap: 8,
              }}
            >
              <Compass size={14} color="#38bdf8" />
              <div style={{ fontSize: 11, fontWeight: 700, color: '#f8fafc' }}>
                {angleInfo.angleDeg}° • <span style={{ color: '#38bdf8' }}>{angleInfo.angleLabel}</span>
              </div>
            </div>
          )}

          {/* Toast Msg */}
          {saveSuccessMsg && (
            <div style={{ position: 'absolute', top: 12, left: '50%', transform: 'translateX(-50%)', background: 'rgba(34, 197, 94, 0.95)', color: '#fff', fontSize: 11, fontWeight: 700, padding: '6px 14px', borderRadius: 20, boxShadow: '0 4px 12px rgba(0,0,0,0.4)', zIndex: 10 }}>
              {saveSuccessMsg}
            </div>
          )}
        </div>

        {/* Bottom Bar: Action Controls & Save */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', background: 'rgba(15, 23, 42, 0.8)', padding: '8px 14px', borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <button
              onClick={() => setIsPlaying(!isPlaying)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 4,
                padding: '5px 10px',
                borderRadius: 5,
                background: isPlaying ? '#0284c7' : 'rgba(255,255,255,0.1)',
                color: '#fff',
                fontSize: 11,
                fontWeight: 600,
                border: 'none',
                cursor: 'pointer',
              }}
            >
              {isPlaying ? <Pause size={12} /> : <Play size={12} />}
              {isPlaying ? 'Tạm Dừng' : 'Chạy'}
            </button>

            {(['idle', 'breathe', 'talk', 'combat_slash'] as const).map((mode) => (
              <button
                key={mode}
                onClick={() => {
                  setAnimMode(mode);
                  setIsPlaying(true);
                }}
                style={{
                  padding: '5px 10px',
                  borderRadius: 5,
                  fontSize: 11,
                  border: '1px solid rgba(255,255,255,0.1)',
                  background: animMode === mode ? 'rgba(168, 85, 247, 0.3)' : 'rgba(255,255,255,0.03)',
                  borderColor: animMode === mode ? '#a855f7' : 'rgba(255,255,255,0.05)',
                  color: animMode === mode ? '#fff' : '#94a3b8',
                  cursor: 'pointer',
                }}
              >
                {mode === 'breathe' ? '🌬️ Thở Nhẹ' : mode === 'talk' ? '🗣️ Nói Chuyện' : mode === 'combat_slash' ? '⚔️ Tung Chiêu' : 'Đứng Yên'}
              </button>
            ))}
          </div>

          <button
            onClick={handleSaveAssembly}
            disabled={isSaving}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '6px 14px',
              borderRadius: 6,
              background: 'linear-gradient(135deg, #10b981, #059669)',
              color: '#ffffff',
              fontWeight: 700,
              fontSize: 11,
              border: 'none',
              cursor: 'pointer',
              boxShadow: '0 4px 12px rgba(16, 185, 129, 0.3)',
            }}
          >
            <Save size={13} /> {isSaving ? 'Đang Lưu...' : 'Lưu Nhân Vật 2D'}
          </button>
        </div>
      </div>

      {/* ─── RIGHT: Inspector & Transforms ────────────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, background: 'rgba(15, 23, 42, 0.7)', padding: 14, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', overflowY: 'auto' }}>
        <div style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Sliders size={14} /> THUỘC TÍNH & VỊ TRÍ
        </div>

        {/* Global Character Scale & Height */}
        <div style={{ background: 'rgba(56, 189, 248, 0.05)', padding: 10, borderRadius: 6, border: '1px solid rgba(56, 189, 248, 0.2)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, color: '#e2e8f0', marginBottom: 4, fontWeight: 600 }}>
            <span>Tỉ Lệ Chiều Cao Nhân Vật:</span>
            <span style={{ color: '#38bdf8' }}>{Math.round((assembly.base_scale || 1.0) * 100)}%</span>
          </div>
          <input
            type="range"
            min="50"
            max="180"
            value={Math.round((assembly.base_scale || 1.0) * 100)}
            onChange={(e) => onChangeAssembly({ ...assembly, base_scale: parseInt(e.target.value) / 100 })}
            style={{ width: '100%' }}
          />
        </div>

        <div style={{ fontSize: 11, color: '#94a3b8' }}>
          Đang chọn: <b style={{ color: '#fff' }}>{PART_HIERARCHY_CONFIG[selectedSlot]?.label || selectedSlot}</b>
        </div>

        {/* X, Y Offsets */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, background: 'rgba(255,255,255,0.02)', padding: 10, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 2 }}>
              <span>Tọa độ ngang X:</span>
              <span style={{ color: '#38bdf8', fontWeight: 600 }}>{selectedPart.offset?.[0] || 0} px</span>
            </div>
            <input
              type="range"
              min="-200"
              max="200"
              value={selectedPart.offset?.[0] || 0}
              onChange={(e) => updatePartConfig({ offset: [parseInt(e.target.value), selectedPart.offset?.[1] || 0] })}
              style={{ width: '100%' }}
            />
          </div>

          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 2 }}>
              <span>Tọa độ dọc Y:</span>
              <span style={{ color: '#38bdf8', fontWeight: 600 }}>{selectedPart.offset?.[1] || 0} px</span>
            </div>
            <input
              type="range"
              min="-300"
              max="300"
              value={selectedPart.offset?.[1] || 0}
              onChange={(e) => updatePartConfig({ offset: [selectedPart.offset?.[0] || 0, parseInt(e.target.value)] })}
              style={{ width: '100%' }}
            />
          </div>
        </div>

        {/* Rotation & Transforms */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 2 }}>
              <span>Góc xoay (Rotation):</span>
              <span style={{ color: '#38bdf8', fontWeight: 600 }}>{selectedPart.rotation || 0}°</span>
            </div>
            <input
              type="range"
              min="-180"
              max="180"
              value={selectedPart.rotation || 0}
              onChange={(e) => updatePartConfig({ rotation: parseInt(e.target.value) })}
              style={{ width: '100%' }}
            />
          </div>

          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 2 }}>
              <span>Tỉ lệ Scale riêng:</span>
              <span style={{ color: '#38bdf8', fontWeight: 600 }}>{Math.round((selectedPart.scale?.[0] || 1) * 100)}%</span>
            </div>
            <input
              type="range"
              min="20"
              max="200"
              value={Math.round((selectedPart.scale?.[0] || 1) * 100)}
              onChange={(e) => {
                const sc = parseInt(e.target.value) / 100;
                updatePartConfig({ scale: [sc, sc] });
              }}
              style={{ width: '100%' }}
            />
          </div>
        </div>

        {/* 3D Physical Z-Depth */}
        <div style={{ background: 'rgba(255,255,255,0.02)', padding: 10, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: '#94a3b8', marginBottom: 4 }}>
            <span>Độ sâu Z trong 3D:</span>
            <span style={{ color: '#38bdf8', fontWeight: 600 }}>{(selectedPart.z_depth_3d || 0).toFixed(3)}m</span>
          </div>
          <input
            type="range"
            min="-100"
            max="100"
            value={Math.round((selectedPart.z_depth_3d || 0) * 1000)}
            onChange={(e) => updatePartConfig({ z_depth_3d: parseInt(e.target.value) / 1000 })}
            style={{ width: '100%' }}
          />
        </div>

        {/* Multi-Angle Images Slots */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <div style={{ fontSize: 11, fontWeight: 600, color: '#a855f7' }}>
            Ảnh gán theo từng góc quay:
          </div>

          {[
            { angleKey: 'front' as Character2DAngle, label: '0° Góc Thẳng (Front)' },
            { angleKey: 'three_quarter_left' as Character2DAngle, label: '45° Góc Nghiêng 3/4' },
            { angleKey: 'profile_left' as Character2DAngle, label: '90° Góc Ngang (Cằm/Mũi)' },
            { angleKey: 'back' as Character2DAngle, label: '180° Sau Lưng (Back)' },
          ].map((item) => {
            const hasAngleImg = Boolean(selectedPart.angles?.[item.angleKey]);
            return (
              <div
                key={item.angleKey}
                style={{
                  padding: '6px 8px',
                  borderRadius: 6,
                  background: 'rgba(255,255,255,0.02)',
                  border: '1px solid rgba(255,255,255,0.05)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                }}
              >
                <div>
                  <div style={{ fontSize: 10, fontWeight: 600, color: '#e2e8f0' }}>{item.label}</div>
                  <div style={{ fontSize: 9, color: hasAngleImg ? '#22c55e' : '#64748b' }}>
                    {hasAngleImg ? '✓ Đã có ảnh' : 'Dùng mặc định'}
                  </div>
                </div>

                <button
                  onClick={() => {
                    const sampleSvg = generateDemoPartSvg(
                      selectedSlot,
                      assembly.gender || 'nam',
                      item.angleKey === 'profile_left' ? 'profile' : item.angleKey === 'back' ? 'back' : 'front'
                    );
                    updatePartConfig({
                      angles: {
                        ...(selectedPart.angles || {}),
                        [item.angleKey]: sampleSvg,
                      },
                    });
                  }}
                  style={{
                    padding: '3px 8px',
                    fontSize: 9,
                    borderRadius: 4,
                    background: 'rgba(56, 189, 248, 0.1)',
                    color: '#38bdf8',
                    border: '1px solid rgba(56, 189, 248, 0.3)',
                    cursor: 'pointer',
                  }}
                >
                  Nạp Vector
                </button>
              </div>
            );
          })}
        </div>

        {/* Reset Part button */}
        <button
          onClick={() => {
            const hierarchy = PART_HIERARCHY_CONFIG[selectedSlot];
            const defSvg = generateDemoPartSvg(selectedSlot, assembly.gender || 'nam', 'front');
            updatePartConfig({
              path: defSvg,
              offset: hierarchy?.defaultOffset || [0, 0],
              scale: [1, 1],
              rotation: selectedSlot === 'canh_tay_trai' ? 12 : selectedSlot === 'canh_tay_phai' ? -18 : selectedSlot === 'vu_khi' ? -15 : 0,
              flipX: false,
              z_depth_3d: hierarchy?.defaultZDepth3D || 0,
              z_index: hierarchy?.defaultZ || 5,
            });
          }}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 6,
            padding: '8px',
            fontSize: 10,
            borderRadius: 5,
            background: 'rgba(255,255,255,0.03)',
            border: '1px solid rgba(255,255,255,0.1)',
            color: '#94a3b8',
            cursor: 'pointer',
            marginTop: 'auto',
          }}
        >
          <RotateCcw size={12} /> Khôi Phục Về Mẫu Mặc Định
        </button>
      </div>
    </div>
  );
};
