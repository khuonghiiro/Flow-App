import React, { useRef, useEffect } from 'react';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import {
  Loader,
  Grid,
  Box,
  MousePointerClick,
  Move3D,
  Film,
  Activity,
  FastForward,
  Zap,
  Play,
  Pause,
  Eye,
  EyeOff,
} from 'lucide-react';
import { Interactive3DPartSelector, SelectedPartInfo } from '../workbench/Interactive3DPartSelector';

export interface Character3DViewportProps {
  canvasContainerRef: React.RefObject<HTMLDivElement | null>;
  previewSceneRef: React.MutableRefObject<THREE.Scene | null>;
  previewCameraRef: React.MutableRefObject<THREE.PerspectiveCamera | null>;
  previewRendererRef: React.MutableRefObject<THREE.WebGLRenderer | null>;
  previewControlsRef: React.MutableRefObject<OrbitControls | null>;
  floorGridRef: React.MutableRefObject<THREE.GridHelper | null>;
  showFloorGrid: boolean;
  onToggleFloorGrid: () => void;
  showWireframe: boolean;
  onToggleWireframe: () => void;
  viewportMode: 'orbit' | 'select';
  onToggleViewportMode: () => void;
  onCanvasClick: (e: React.MouseEvent<HTMLDivElement>) => void;
  isPreviewLoading: boolean;
  // Animation & Skeleton Controller props
  hasBones: boolean;
  totalBonesCount: number;
  showSkeletonHelper: boolean;
  onToggleSkeletonHelper: () => void;
  availableAnimations: string[];
  selectedAnimClip: string;
  onSelectAnimationClip: (clip: string) => void;
  activePose: string;
  onSelectPose: (pose: string) => void;
  isPlayingAnim: boolean;
  isPosePlaying: boolean;
  onTogglePlayPause: () => void;
  animSpeed: number;
  onChangeAnimSpeed: (speed: number) => void;
  isRigged?: boolean;
}

export const Character3DViewport: React.FC<Character3DViewportProps> = ({
  canvasContainerRef,
  previewSceneRef,
  previewCameraRef,
  previewRendererRef,
  previewControlsRef,
  floorGridRef,
  showFloorGrid,
  onToggleFloorGrid,
  showWireframe,
  onToggleWireframe,
  viewportMode,
  onToggleViewportMode,
  onCanvasClick,
  isPreviewLoading,
  hasBones,
  totalBonesCount,
  showSkeletonHelper,
  onToggleSkeletonHelper,
  availableAnimations,
  selectedAnimClip,
  onSelectAnimationClip,
  activePose,
  onSelectPose,
  isPlayingAnim,
  isPosePlaying,
  onTogglePlayPause,
  animSpeed,
  onChangeAnimSpeed,
  isRigged = false,
}) => {
  const isCurrentlyPlaying = availableAnimations.length > 0 ? isPlayingAnim : isPosePlaying;

  return (
    <div
      style={{
        flex: '0 0 40%',
        width: '40%',
        maxWidth: '40%',
        display: 'flex',
        flexDirection: 'column',
        borderRight: '1px solid rgba(255,255,255,0.08)',
        background: '#070b14',
        position: 'relative',
      }}
    >
      {/* 3D Viewport Top Toolbar */}
      <div
        style={{
          padding: '8px 12px',
          background: 'rgba(15, 23, 42, 0.7)',
          borderBottom: '1px solid rgba(255,255,255,0.08)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span style={{ fontSize: 12, fontWeight: 700, color: '#f8fafc' }}>
            3D Character Viewport
          </span>
          <span style={{ fontSize: 10, color: '#64748b', background: 'rgba(255,255,255,0.05)', padding: '2px 6px', borderRadius: 4 }}>
            PBR Real-time
          </span>
        </div>

        {/* Viewport Control Buttons */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          {/* Interaction Mode Toggle */}
          <button
            onClick={onToggleViewportMode}
            title={viewportMode === 'orbit' ? 'Đang ở Chế độ Xoay Camera. Nhấn để bật Chạm Chọn Chi Tiết' : 'Đang ở Chế độ Chạm Chọn Chi Tiết. Nhấn để bật Xoay Camera'}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '4px 10px',
              fontSize: 11,
              fontWeight: 700,
              borderRadius: 6,
              background: viewportMode === 'select' ? 'linear-gradient(135deg, #a855f7, #7c3aed)' : 'rgba(255,255,255,0.06)',
              border: viewportMode === 'select' ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.12)',
              color: viewportMode === 'select' ? '#ffffff' : '#cbd5e1',
              cursor: 'pointer',
              boxShadow: viewportMode === 'select' ? '0 0 10px rgba(168, 85, 247, 0.4)' : 'none',
              transition: 'all 0.15s ease',
            }}
          >
            {viewportMode === 'select' ? <MousePointerClick size={13} /> : <Move3D size={13} />}
            <span>{viewportMode === 'select' ? 'Chạm Chọn Đổi Màu' : 'Xoay 3D (360°)'}</span>
          </button>

          {/* Floor Grid Toggle */}
          <button
            onClick={onToggleFloorGrid}
            title="Bật/Tắt Lưới Sàn Tọa Độ 3D"
            style={{
              padding: '4px 8px',
              background: showFloorGrid ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.06)',
              border: showFloorGrid ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
              color: showFloorGrid ? '#38bdf8' : '#94a3b8',
              borderRadius: 6,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              fontSize: 11,
            }}
          >
            <Grid size={13} />
          </button>

          {/* Wireframe Toggle */}
          <button
            onClick={onToggleWireframe}
            title="Bật/Tắt Khung Lưới Đa Giác (Wireframe)"
            style={{
              padding: '4px 8px',
              background: showWireframe ? 'rgba(168, 85, 247, 0.25)' : 'rgba(255,255,255,0.06)',
              border: showWireframe ? '1px solid #c084fc' : '1px solid rgba(255,255,255,0.1)',
              color: showWireframe ? '#c084fc' : '#94a3b8',
              borderRadius: 6,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              fontSize: 11,
            }}
          >
            <Box size={13} />
          </button>
        </div>
      </div>

      {/* 3D WebGL Canvas Container */}
      <div
        onClick={onCanvasClick}
        style={{
          flex: 1,
          position: 'relative',
          width: '100%',
          minHeight: 0,
          overflow: 'hidden',
          cursor: viewportMode === 'select' ? 'pointer' : 'default',
        }}
      >
        <div ref={canvasContainerRef as any} style={{ width: '100%', height: '100%' }} />

        {/* Loading Spinner Overlay */}
        {isPreviewLoading && (
          <div
            style={{
              position: 'absolute',
              inset: 0,
              background: 'rgba(10, 15, 29, 0.75)',
              backdropFilter: 'blur(4px)',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 8,
              color: '#38bdf8',
              fontWeight: 700,
              fontSize: 12,
              zIndex: 30,
            }}
          >
            <Loader size={26} className="animate-spin" />
            <span>Đang tải mô hình 3D & chuẩn hóa giải phẫu...</span>
          </div>
        )}

        {/* Interaction Mode Floating Hint */}
        <div
          style={{
            position: 'absolute',
            top: 8,
            left: 8,
            fontSize: 10,
            color: viewportMode === 'select' ? '#c084fc' : 'rgba(255,255,255,0.65)',
            pointerEvents: 'none',
            background: viewportMode === 'select' ? 'rgba(88, 28, 135, 0.65)' : 'rgba(0,0,0,0.55)',
            border: viewportMode === 'select' ? '1px solid rgba(168, 85, 247, 0.4)' : '1px solid rgba(255,255,255,0.1)',
            backdropFilter: 'blur(4px)',
            padding: '4px 10px',
            borderRadius: 6,
            fontWeight: 600,
            display: 'flex',
            alignItems: 'center',
            gap: 5,
            zIndex: 20,
          }}
        >
          {viewportMode === 'select' ? (
            <>
              <MousePointerClick size={12} />
              <span>Chế độ Chạm Chọn: Click trực tiếp vào Áo, Quần, Giày, Tóc... để đổi màu & chất liệu ở cột bên phải</span>
            </>
          ) : (
            <>
              <Move3D size={12} />
              <span>Chế độ Xoay 3D: Chuột trái xoay 360° | Cuộn chuột để Zoom</span>
            </>
          )}
        </div>
      </div>

      {/* Floating Animation & Skeleton Controller Bar (Available for Columbina 743 bones, embedded animations, or auto-rig) */}
      {(hasBones || availableAnimations.length > 0 || isRigged) && (
        <div
          style={{
            position: 'absolute',
            bottom: 12,
            left: 12,
            right: 12,
            background: 'rgba(15, 23, 42, 0.90)',
            backdropFilter: 'blur(10px)',
            padding: '8px 12px',
            borderRadius: 10,
            border: '1px solid rgba(56, 189, 248, 0.25)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 8,
            zIndex: 25,
            boxShadow: '0 6px 20px rgba(0,0,0,0.5)',
          }}
        >
          {/* Left: Skeleton Bones Toggle */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <button
              onClick={onToggleSkeletonHelper}
              title={showSkeletonHelper ? 'Ẩn bộ khung xương 3D' : 'Hiện bộ khung xương 3D'}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 5,
                padding: '4px 10px',
                fontSize: 11,
                fontWeight: 700,
                borderRadius: 6,
                background: showSkeletonHelper ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.06)',
                border: showSkeletonHelper ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.12)',
                color: showSkeletonHelper ? '#38bdf8' : '#cbd5e1',
                cursor: 'pointer',
                transition: 'all 0.15s ease',
              }}
            >
              <Activity size={13} />
              <span>{showSkeletonHelper ? 'Ẩn Khung Xương' : 'Hiện Khung Xương'}</span>
              {totalBonesCount > 0 && (
                <span style={{ fontSize: 9, opacity: 0.8, background: 'rgba(0,0,0,0.3)', padding: '1px 5px', borderRadius: 4 }}>
                  {totalBonesCount} Khớp
                </span>
              )}
            </button>
          </div>

          {/* Center: Animation / Pose Selector */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flex: 1, justifyContent: 'center' }}>
            {availableAnimations.length > 0 ? (
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <Film size={13} color="#f59e0b" />
                <select
                  value={selectedAnimClip}
                  onChange={(e) => onSelectAnimationClip(e.target.value)}
                  style={{
                    background: '#1e293b',
                    border: '1px solid rgba(245, 158, 11, 0.4)',
                    color: '#fbbf24',
                    padding: '4px 8px',
                    borderRadius: 6,
                    fontSize: 11,
                    fontWeight: 600,
                    outline: 'none',
                    cursor: 'pointer',
                  }}
                >
                  {availableAnimations.map((name) => (
                    <option key={name} value={name}>🎬 {name}</option>
                  ))}
                </select>
              </div>
            ) : (
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <Zap size={13} color="#a855f7" />
                <select
                  value={activePose}
                  onChange={(e) => onSelectPose(e.target.value)}
                  style={{
                    background: '#1e293b',
                    border: '1px solid rgba(168, 85, 247, 0.4)',
                    color: '#c084fc',
                    padding: '4px 8px',
                    borderRadius: 6,
                    fontSize: 11,
                    fontWeight: 600,
                    outline: 'none',
                    cursor: 'pointer',
                  }}
                >
                  <option value="walk">🚶 Dáng Đi Tự Nhiên (Walk Cycle)</option>
                  <option value="slash">⚔️ Xuất Chiêu Kiếm Pháp (Slash)</option>
                  <option value="defend">🛡️ Thế Thủ Võ Thuật (Guard)</option>
                  <option value="wave">👋 Vẫy Tay Chào (Wave)</option>
                  <option value="sit">🪑 Tư Thế Ngồi (Sit)</option>
                  <option value="t_pose">🤸 T-Pose (Chuẩn)</option>
                </select>
              </div>
            )}

            {/* Play / Pause Toggle Button */}
            <button
              onClick={onTogglePlayPause}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 5,
                padding: '4px 12px',
                fontSize: 11,
                fontWeight: 700,
                borderRadius: 6,
                background: isCurrentlyPlaying ? 'rgba(239, 68, 68, 0.25)' : 'rgba(34, 197, 94, 0.25)',
                border: isCurrentlyPlaying ? '1px solid #ef4444' : '1px solid #22c55e',
                color: isCurrentlyPlaying ? '#f87171' : '#4ade80',
                cursor: 'pointer',
                transition: 'all 0.15s ease',
              }}
            >
              {isCurrentlyPlaying ? (
                <>
                  <Pause size={12} /> Tạm Dừng
                </>
              ) : (
                <>
                  <Play size={12} /> Chạy Động Tác
                </>
              )}
            </button>
          </div>

          {/* Right: Playback Speed */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <FastForward size={12} color="#94a3b8" />
            <select
              value={animSpeed}
              onChange={(e) => onChangeAnimSpeed(parseFloat(e.target.value))}
              style={{
                background: '#1e293b',
                border: '1px solid rgba(255,255,255,0.15)',
                color: '#f8fafc',
                padding: '3px 6px',
                borderRadius: 6,
                fontSize: 10,
                outline: 'none',
                cursor: 'pointer',
              }}
            >
              <option value="0.5">0.5x</option>
              <option value="1.0">1.0x (Chuẩn)</option>
              <option value="1.5">1.5x</option>
              <option value="2.0">2.0x</option>
            </select>
          </div>
        </div>
      )}
    </div>
  );
};
