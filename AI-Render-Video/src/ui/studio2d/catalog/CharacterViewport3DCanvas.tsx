import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Box, Compass, ZoomIn, ZoomOut, Maximize2 } from 'lucide-react';
import { Character2DAssembly } from '../../../types/scene2d';
import {
  ThreeMultiAngleBillboardEngine,
  AngleDetectionResult,
} from '../../../core/engine2d/ThreeMultiAngleBillboardEngine';
import {
  Canvas2DPuppetEngine,
  PuppetAnimationState,
} from '../../../core/engine2d/Canvas2DPuppetEngine';

interface CharacterViewport3DCanvasProps {
  assembly: Character2DAssembly;
}

export const CharacterViewport3DCanvas: React.FC<CharacterViewport3DCanvasProps> = ({
  assembly,
}) => {
  const [viewportMode, setViewportMode] = useState<'3d_turntable' | '2d_canvas'>('3d_turntable');
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

  const threeContainerRef = useRef<HTMLDivElement>(null);
  const threeEngineRef = useRef<ThreeMultiAngleBillboardEngine | null>(null);
  const canvas2dRef = useRef<HTMLCanvasElement>(null);
  const canvas2dEngineRef = useRef<Canvas2DPuppetEngine>(new Canvas2DPuppetEngine());
  const animTimeRef = useRef<number>(0);

  // Initialize Three.js 3D Billboard Engine
  useEffect(() => {
    if (viewportMode === '3d_turntable' && threeContainerRef.current) {
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
      if (viewportMode !== '3d_turntable' && threeEngineRef.current) {
        threeEngineRef.current.dispose();
        threeEngineRef.current = null;
      }
    };
  }, [viewportMode]);

  // Update assembly in 3D engine when assembly changes
  useEffect(() => {
    if (threeEngineRef.current && viewportMode === '3d_turntable') {
      threeEngineRef.current.setAssembly(assembly);
    }
  }, [assembly, viewportMode]);

  // Render 2D Canvas when in 2d_canvas mode
  const render2D = useCallback(() => {
    if (viewportMode !== '2d_canvas' || !canvas2dRef.current) return;
    const canvas = canvas2dRef.current;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    canvas.width = canvas.clientWidth;
    canvas.height = canvas.clientHeight;
    animTimeRef.current += 0.02;

    const animState: PuppetAnimationState = {
      mode: 'breathe',
      time: animTimeRef.current,
      isBlinking: Math.floor(animTimeRef.current * 1.5) % 8 === 0,
      isTalking: false,
      slashProgress: 0,
      shakeOffset: [0, 0],
      zoomFactor: 1.0,
    };

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = '#080c14';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    ctx.save();
    const centerX = canvas.width / 2 + panOffset[0];
    const centerY = canvas.height * 0.5 + panOffset[1];

    canvas2dEngineRef.current.renderCharacter(
      ctx,
      assembly,
      animState,
      centerX,
      centerY,
      zoomLevel * (assembly.base_scale || 1.0),
      null
    );
    ctx.restore();
  }, [viewportMode, assembly, zoomLevel, panOffset]);

  useEffect(() => {
    let animId: number;
    const loop = () => {
      render2D();
      animId = requestAnimationFrame(loop);
    };
    if (viewportMode === '2d_canvas') {
      animId = requestAnimationFrame(loop);
    }
    return () => cancelAnimationFrame(animId);
  }, [viewportMode, render2D]);

  const handleAngleJump = (deg: number) => {
    setTurntableAngle(deg);
    if (threeEngineRef.current) {
      threeEngineRef.current.jumpToAngle(deg);
    }
  };

  return (
    <div
      style={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        minHeight: 0,
        overflow: 'hidden',
      }}
    >
      {/* Top Bar Switcher & Mode Tools */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          background: 'rgba(15, 23, 42, 0.8)',
          padding: '4px 8px',
          borderRadius: 6,
          border: '1px solid rgba(255, 255, 255, 0.08)',
        }}
      >
        <div style={{ display: 'flex', gap: 3, background: 'rgba(0,0,0,0.4)', padding: 2, borderRadius: 5 }}>
          <button
            onClick={() => setViewportMode('3d_turntable')}
            style={{
              padding: '3px 9px',
              fontSize: 10.5,
              fontWeight: 700,
              borderRadius: 4,
              border: 'none',
              background: viewportMode === '3d_turntable' ? '#0284c7' : 'transparent',
              color: viewportMode === '3d_turntable' ? '#fff' : '#94a3b8',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            <Box size={12} /> Không Gian 3D Đa Góc
          </button>
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
        </div>

        {viewportMode === '3d_turntable' ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <span style={{ fontSize: 9.5, color: '#94a3b8' }}>Góc nhanh:</span>
            {[
              { label: '0° Chính diện', deg: 0 },
              { label: '45° 3/4', deg: 45 },
              { label: '90° Ngang', deg: 90 },
              { label: '180° Sau lưng', deg: 180 },
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
        ) : (
          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
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
        )}
      </div>

      {/* Main Viewport Container */}
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
        {viewportMode === '3d_turntable' ? (
          <div ref={threeContainerRef} style={{ width: '100%', height: '100%' }} />
        ) : (
          <canvas ref={canvas2dRef} style={{ width: '100%', height: '100%', display: 'block' }} />
        )}

        {/* Direction Badge */}
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
      </div>

      {/* 360° Turntable Angle Slider */}
      {viewportMode === '3d_turntable' && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            background: 'rgba(15, 23, 42, 0.8)',
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
