import React, { useRef, useEffect, useState } from 'react';
import * as THREE from 'three';
import { 
  Camera, Zap, RotateCcw, Compass, MessageSquare, Eye, EyeOff, 
  Move, RotateCw, Maximize2, Globe, X, Target, Keyboard, ChevronDown, ChevronUp, Trash2
} from 'lucide-react';
import { ThreeRenderer } from '../core/engine/ThreeRenderer';
import { SubtitleOverlay } from './SubtitleOverlay';
import { ActiveSubtitle } from '../core/subtitles/SubtitleSynchronizer';
import { SubtitlesConfig, MasterSceneConfig } from '../types/scene';
import { SelectedSceneObject } from './TransformInspector';

interface ProjectedMarker {
  id: string;
  name: string;
  category: 'actor' | 'prop';
  x: number;
  y: number;
  pos: [number, number, number];
  rot: [number, number, number];
  scale: number;
  isObstacle?: boolean;
}

interface ViewportCanvasProps {
  renderer: ThreeRenderer | null;
  fps: number;
  activeSubtitle: ActiveSubtitle | null;
  subtitlesConfig?: SubtitlesConfig | null;
  scene?: MasterSceneConfig;
  showCC: boolean;
  isInspecting?: boolean;
  isFreeCam?: boolean;
  isLoadingMap?: boolean;
  selectedObjectId?: string | null;
  selectedObjectName?: string | null;
  gizmoMode?: 'translate' | 'rotate' | 'scale';
  gizmoSpace?: 'world' | 'local';
  onChangeGizmoMode?: (mode: 'translate' | 'rotate' | 'scale') => void;
  onToggleGizmoSpace?: () => void;
  onSelectObject?: (obj: SelectedSceneObject | null) => void;
  onDeselectObject?: () => void;
  onDeleteProp?: (propId: string) => void;
  onToggleCC: () => void;
  onToggleFreeCam?: () => void;
  onResetCamera?: () => void;
}

export const ViewportCanvas: React.FC<ViewportCanvasProps> = ({
  renderer,
  fps,
  activeSubtitle,
  subtitlesConfig,
  scene,
  showCC,
  isInspecting,
  isFreeCam,
  isLoadingMap,
  selectedObjectId,
  selectedObjectName,
  gizmoMode = 'translate',
  gizmoSpace = 'world',
  onChangeGizmoMode,
  onToggleGizmoSpace,
  onSelectObject,
  onDeselectObject,
  onDeleteProp,
  onToggleCC,
  onToggleFreeCam,
  onResetCamera,
}) => {
  const mountRef = useRef<HTMLDivElement>(null);
  const [showUI, setShowUI] = useState(true);
  const [showControlsGuide, setShowControlsGuide] = useState(true);
  const [showCoordinates, setShowCoordinates] = useState(false);
  const [projectedMarkers, setProjectedMarkers] = useState<ProjectedMarker[]>([]);

  useEffect(() => {
    if (!renderer || !mountRef.current) return;
    renderer.mount(mountRef.current);
    renderer.start();

    return () => {
      renderer.unmount();
    };
  }, [renderer]);

  const sceneRef = useRef(scene);
  sceneRef.current = scene;
  const selectedObjectIdRef = useRef(selectedObjectId);
  selectedObjectIdRef.current = selectedObjectId;

  // Keyboard shortcut listener for Unity-style gizmo tool switching (W, E, R, Esc)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ignore if user is currently typing in an input/textarea/select
      const activeTag = (document.activeElement?.tagName || '').toLowerCase();
      if (activeTag === 'input' || activeTag === 'textarea' || activeTag === 'select') {
        return;
      }

      if (e.key === 'w' || e.key === 'W') {
        onChangeGizmoMode?.('translate');
      } else if (e.key === 'e' || e.key === 'E') {
        onChangeGizmoMode?.('rotate');
      } else if (e.key === 'r' || e.key === 'R') {
        onChangeGizmoMode?.('scale');
      } else if (e.key === 'Escape') {
        onDeselectObject?.();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onChangeGizmoMode, onDeselectObject]);

  // Project 3D World Positions to 2D Screen Overlays when showCoordinates is ON
  useEffect(() => {
    if (!renderer || !showCoordinates) {
      setProjectedMarkers([]);
      return;
    }

    const updateProjection = () => {
      if (!mountRef.current || !renderer) return;
      const width = mountRef.current.clientWidth || 800;
      const height = mountRef.current.clientHeight || 450;
      const camera = renderer.camera;
      const activeScene = sceneRef.current;
      const currentSelectedId = selectedObjectIdRef.current;
      if (!activeScene) return;

      const markers: ProjectedMarker[] = [];

      // 1. Project Actors (Hide the marker if this actor is currently selected)
      (activeScene.actors || []).forEach((actor) => {
        if (currentSelectedId === actor.id) return;

        let p: [number, number, number] = actor.spawn_point;
        // Query live world position from 3D avatar
        const actorObj = renderer.scene.getObjectByName(actor.id);
        if (actorObj) {
          const wp = new THREE.Vector3();
          actorObj.getWorldPosition(wp);
          p = [
            parseFloat(wp.x.toFixed(2)),
            parseFloat(wp.y.toFixed(2)),
            parseFloat(wp.z.toFixed(2)),
          ];
        }

        const v = new THREE.Vector3(p[0], p[1] + 1.8, p[2]);
        v.project(camera);

        if (v.z < 1.0) {
          const screenX = (v.x * 0.5 + 0.5) * width;
          const screenY = (-(v.y * 0.5) + 0.5) * height;

          if (screenX >= -50 && screenX <= width + 50 && screenY >= -50 && screenY <= height + 50) {
            markers.push({
              id: actor.id,
              name: `👤 ${actor.name || actor.id}`,
              category: 'actor',
              x: Math.round(screenX),
              y: Math.round(screenY),
              pos: [p[0], p[1], p[2]],
              rot: [0, Math.round(((actor.rotation_y || 0) * 180) / Math.PI), 0],
              scale: 1.0,
            });
          }
        }
      });

      // 2. Project Placed Props (Hide the marker if this prop is currently selected)
      (activeScene.environment.placed_props || []).forEach((prop) => {
        if (currentSelectedId === prop.id) return;

        let p: [number, number, number] = prop.position;
        // Query live world position from 3D object in scene
        const propObj = renderer.scene.getObjectByName(prop.id) || renderer.scene.getObjectByName(`placed_${prop.id.replace('placed_', '')}`);
        if (propObj) {
          const wp = new THREE.Vector3();
          propObj.getWorldPosition(wp);
          p = [
            parseFloat(wp.x.toFixed(2)),
            parseFloat(wp.y.toFixed(2)),
            parseFloat(wp.z.toFixed(2)),
          ];
        }

        const v = new THREE.Vector3(p[0], p[1] + 0.8, p[2]);
        v.project(camera);

        if (v.z < 1.0) {
          const screenX = (v.x * 0.5 + 0.5) * width;
          const screenY = (-(v.y * 0.5) + 0.5) * height;

          if (screenX >= -50 && screenX <= width + 50 && screenY >= -50 && screenY <= height + 50) {
            const rot = prop.rotation || [0, 0, 0];
            markers.push({
              id: prop.id,
              name: `📦 ${prop.id.replace('placed_', '').replace('prop_', '')}`,
              category: 'prop',
              x: Math.round(screenX),
              y: Math.round(screenY),
              pos: [p[0], p[1], p[2]],
              rot: [
                Math.round((rot[0] * 180) / Math.PI),
                Math.round((rot[1] * 180) / Math.PI),
                Math.round((rot[2] * 180) / Math.PI),
              ],
              scale: typeof prop.scale === 'number' ? prop.scale : 1.0,
              isObstacle: prop.is_obstacle,
            });
          }
        }
      });

      setProjectedMarkers(markers);
    };

    renderer.onRender(updateProjection);
    return () => {
      renderer.removeRenderCallback(updateProjection);
    };
  }, [renderer, showCoordinates]);

  return (
    <div className="viewport-wrapper">
      <div ref={mountRef} className="viewport-canvas-container" />

      {/* 3D World Space Coordinate Markers Overlay */}
      {showCoordinates && showUI && (
        <div className="viewport-coordinates-overlay">
          {projectedMarkers.map((marker) => {
            return (
              <div
                key={marker.id}
                className="coordinate-marker-tag"
                style={{
                  left: marker.x,
                  top: marker.y,
                  transform: 'translate(-50%, -100%)',
                }}
                onClick={() => {
                  onSelectObject?.({
                    id: marker.id,
                    name: marker.name,
                    category: marker.category,
                    position: marker.pos,
                    rotation: marker.rot,
                    scale: marker.scale,
                    isObstacle: marker.isObstacle,
                  });
                }}
                title="Bấm để chọn đối tượng và hiện trục tọa độ 3D Gizmo"
              >
                <div className="marker-header">
                  <span className="marker-name">{marker.name}</span>
                </div>
                <div className="marker-coords">
                  <span className="coord-x">X: {marker.pos[0]}</span>
                  <span className="coord-y">Y: {marker.pos[1]}</span>
                  <span className="coord-z">Z: {marker.pos[2]}</span>
                </div>
                <div className="marker-pin-arrow" />
              </div>
            );
          })}
        </div>
      )}

      {/* Loading Map Notification Banner */}
      {isLoadingMap && (
        <div
          style={{
            position: 'absolute',
            top: 54,
            left: '50%',
            transform: 'translateX(-50%)',
            background: 'rgba(15, 23, 42, 0.95)',
            border: '1px solid #38bdf8',
            boxShadow: '0 0 24px rgba(56, 189, 248, 0.45)',
            borderRadius: 24,
            padding: '8px 20px',
            color: '#ffffff',
            fontSize: 12,
            fontWeight: 600,
            display: 'flex',
            alignItems: 'center',
            gap: 10,
            zIndex: 100,
            pointerEvents: 'none',
          }}
        >
          <div
            style={{
              width: 14,
              height: 14,
              border: '2px solid #38bdf8',
              borderTopColor: 'transparent',
              borderRadius: '50%',
              animation: 'spin 0.8s linear infinite',
            }}
          />
          ⏳ Đang nạp Map 3D... Vui lòng đợi trong giây lát
        </div>
      )}

      {/* Top HUD */}
      <div className="viewport-hud" style={{ alignItems: 'flex-start' }}>
        
        {/* Left Side: FPS & Collapsible Controls Guide */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, opacity: showUI ? 1 : 0, transition: 'opacity 0.2s', pointerEvents: showUI ? 'auto' : 'none' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <div className="hud-pill fps-counter" style={{ padding: '2px 8px', fontSize: 10, borderRadius: 12, background: 'rgba(15, 23, 42, 0.6)' }}>
              <Zap size={10} /> {fps} FPS
            </div>

            {/* Guide Collapse Toggle Button */}
            <button
              className="hud-pill-btn"
              style={{
                padding: '2px 8px',
                fontSize: 10,
                borderRadius: 12,
                background: showControlsGuide ? 'rgba(30, 41, 59, 0.8)' : 'rgba(15, 23, 42, 0.6)',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: showControlsGuide ? '#38bdf8' : '#94a3b8',
                cursor: 'pointer',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 4,
              }}
              onClick={() => setShowControlsGuide(!showControlsGuide)}
              title={showControlsGuide ? "Đóng bảng phím tắt" : "Hiện bảng phím tắt"}
            >
              <Keyboard size={11} />
              <span>{showControlsGuide ? 'Ẩn Phím Tắt' : 'Phím Tắt 3D'}</span>
              {showControlsGuide ? <ChevronUp size={11} /> : <ChevronDown size={11} />}
            </button>
          </div>
          
          {/* Controls Guide Box (Collapsible) */}
          {showControlsGuide && (
            <div style={{
              background: 'rgba(15, 23, 42, 0.8)',
              backdropFilter: 'blur(8px)',
              borderRadius: 8,
              padding: '10px 12px',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              fontSize: 10,
              color: '#cbd5e1',
              display: 'flex',
              flexDirection: 'column',
              gap: 6,
              marginTop: 2,
              width: 145,
              animation: 'fadeIn 0.15s ease',
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid rgba(255, 255, 255, 0.08)', paddingBottom: 4 }}>
                <span style={{ color: '#38bdf8', fontWeight: 700, fontSize: 10 }}>ĐIỀU KHIỂN 3D</span>
                <button
                  style={{ background: 'none', border: 'none', color: '#94a3b8', cursor: 'pointer', padding: 0 }}
                  onClick={() => setShowControlsGuide(false)}
                  title="Đóng"
                >
                  <X size={12} />
                </button>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>W, A, S, D</span> <span style={{ opacity: 0.7 }}>Di chuyển</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>Space / Shift</span> <span style={{ opacity: 0.7 }}>Nhảy / Chạy</span>
              </div>
              <div style={{ borderTop: '1px solid rgba(255, 255, 255, 0.08)', margin: '2px 0' }} />
              <div style={{ color: '#a855f7', fontWeight: 700, fontSize: 10 }}>TRỤC GIZMO (UNITY)</div>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span style={{ color: '#ef4444', fontWeight: 600 }}>W</span> <span style={{ opacity: 0.7 }}>Dịch vị trí</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span style={{ color: '#22c55e', fontWeight: 600 }}>E</span> <span style={{ opacity: 0.7 }}>Xoay góc</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span style={{ color: '#38bdf8', fontWeight: 600 }}>R</span> <span style={{ opacity: 0.7 }}>Thu phóng</span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>Esc</span> <span style={{ opacity: 0.7 }}>Bỏ chọn</span>
              </div>
            </div>
          )}
        </div>

        {/* Center: Unity 3D Transform Gizmo Toolbar */}
        <div style={{ flex: 1, display: 'flex', justifyContent: 'center', pointerEvents: 'auto' }}>
          {selectedObjectName && showUI && (
            <div className="viewport-gizmo-toolbar">
              {/* Selected Target Badge */}
              <div className="gizmo-target-badge" title="Đối tượng đang chọn trực tiếp">
                <Target size={12} color="#38bdf8" />
                <span className="target-name">{selectedObjectName}</span>
                <button
                  className="gizmo-deselect-btn"
                  title="Hủy chọn (Esc)"
                  onClick={onDeselectObject}
                >
                  <X size={11} />
                </button>
              </div>

              {/* Gizmo Mode Switcher (Translate W, Rotate E, Scale R) */}
              <div className="gizmo-mode-group">
                <button
                  className={`gizmo-tool-btn ${gizmoMode === 'translate' ? 'active' : ''}`}
                  title="Di chuyển tọa độ (Phím tắt W)"
                  onClick={() => onChangeGizmoMode?.('translate')}
                >
                  <Move size={12} /> Di chuyển (W)
                </button>

                <button
                  className={`gizmo-tool-btn ${gizmoMode === 'rotate' ? 'active' : ''}`}
                  title="Xoay góc hướng (Phím tắt E)"
                  onClick={() => onChangeGizmoMode?.('rotate')}
                >
                  <RotateCw size={12} /> Xoay (E)
                </button>

                <button
                  className={`gizmo-tool-btn ${gizmoMode === 'scale' ? 'active' : ''}`}
                  title="Thu phóng kích thước (Phím tắt R)"
                  onClick={() => onChangeGizmoMode?.('scale')}
                >
                  <Maximize2 size={12} /> Thu phóng (R)
                </button>
              </div>

              {/* Space Toggle (World / Local) */}
              <button
                className="gizmo-space-btn"
                title={`Hệ tọa độ: ${gizmoSpace.toUpperCase()} (Bấm để chuyển đổi)`}
                onClick={onToggleGizmoSpace}
              >
                <Globe size={11} />
                <span>{gizmoSpace === 'world' ? 'World' : 'Local'}</span>
              </button>

              {/* Quick Delete Button for Selected Prop */}
              {selectedObjectId && (selectedObjectId.startsWith('placed_') || selectedObjectId.startsWith('props.')) && onDeleteProp && (
                <button
                  className="gizmo-tool-btn danger"
                  style={{
                    backgroundColor: 'rgba(239, 68, 68, 0.2)',
                    borderColor: 'rgba(239, 68, 68, 0.4)',
                    color: '#f87171',
                  }}
                  title="Xóa đối tượng này khỏi Scene"
                  onClick={() => onDeleteProp(selectedObjectId)}
                >
                  <Trash2 size={12} /> Xóa
                </button>
              )}
            </div>
          )}
        </div>

        {/* Right Actions: Eye Toggle, CC Subtitle, 360 Free Cam, XYZ Coords Overlay, Inspect Reset */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, alignItems: 'flex-end', pointerEvents: 'auto' }}>
          {/* Eye Toggle (Always visible) */}
          <button
            className="btn-secondary"
            style={{
              borderRadius: 8,
              width: 32,
              height: 32,
              padding: 0,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              backgroundColor: 'rgba(30, 41, 59, 0.7)',
              borderColor: '#475569',
              color: showUI ? '#38bdf8' : '#94a3b8',
              cursor: 'pointer',
            }}
            onClick={() => setShowUI(!showUI)}
            title="Ẩn/Hiện Giao Diện"
          >
            {showUI ? <Eye size={16} /> : <EyeOff size={16} />}
          </button>

          {/* Hidden UI Items */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, opacity: showUI ? 1 : 0, pointerEvents: showUI ? 'auto' : 'none', transition: 'opacity 0.2s', alignItems: 'flex-end' }}>
            
            {/* CC Toggle */}
            <button
              id="toggle-subtitles-btn"
              className={`btn-secondary ${showCC ? 'active' : ''}`}
              style={{
                borderRadius: 8,
                width: 32,
                height: 32,
                padding: 0,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                backgroundColor: showCC ? 'rgba(99, 102, 241, 0.8)' : 'rgba(30, 41, 59, 0.7)',
                borderColor: showCC ? '#818cf8' : '#475569',
                color: showCC ? '#ffffff' : '#64748b',
                cursor: 'pointer',
                fontWeight: 700,
                fontSize: 12,
              }}
              onClick={onToggleCC}
              title="Phụ đề"
            >
              CC
            </button>

            {/* 360 Cam Toggle */}
            <button
              id="toggle-free-cam-btn"
              className={isFreeCam ? "btn-primary" : "btn-secondary"}
              style={{
                borderRadius: 8,
                width: 32,
                height: 32,
                padding: 0,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                backgroundColor: isFreeCam ? 'rgba(16, 185, 129, 0.9)' : 'rgba(30, 41, 59, 0.7)',
                borderColor: isFreeCam ? '#34d399' : '#475569',
                color: isFreeCam ? '#ffffff' : '#64748b',
                cursor: 'pointer',
                fontWeight: 700,
                fontSize: 10,
              }}
              onClick={onToggleFreeCam}
              title="Cam tự do (360 độ)"
            >
              360
            </button>

            {/* XYZ Coordinates Overlay Toggle Button (Right Below 360) */}
            <button
              id="toggle-coords-overlay-btn"
              className={showCoordinates ? "btn-primary" : "btn-secondary"}
              style={{
                borderRadius: 8,
                width: 32,
                height: 32,
                padding: 0,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                backgroundColor: showCoordinates ? 'rgba(56, 189, 248, 0.9)' : 'rgba(30, 41, 59, 0.7)',
                borderColor: showCoordinates ? '#38bdf8' : '#475569',
                color: showCoordinates ? '#ffffff' : '#64748b',
                cursor: 'pointer',
                fontWeight: 800,
                fontSize: 9,
                boxShadow: showCoordinates ? '0 0 12px rgba(56, 189, 248, 0.5)' : 'none',
              }}
              onClick={() => setShowCoordinates(!showCoordinates)}
              title={showCoordinates ? "Tắt nhãn tọa độ trên Scene" : "Bật hiển thị nhãn tọa độ XYZ của các đối tượng"}
            >
              XYZ
            </button>
          </div>

          {/* Inspect Mode Reset Button */}
          {showUI && isInspecting && !isFreeCam && (
            <button
              id="reset-inspect-camera-btn"
              className="btn-secondary"
              style={{
                borderRadius: 8,
                padding: '6px 12px',
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                backgroundColor: 'rgba(239, 68, 68, 0.8)',
                borderColor: '#ef4444',
                color: '#ffffff',
                cursor: 'pointer',
                fontWeight: 600,
                fontSize: 11,
              }}
              onClick={onResetCamera}
              title="Thoát chế độ soi thoại"
            >
              <RotateCcw size={12} /> Thoát Soi
            </button>
          )}
        </div>
      </div>

      {/* Subtitles Overlay Layer */}
      {showCC && activeSubtitle && (
        <SubtitleOverlay
          subtitle={activeSubtitle}
          config={subtitlesConfig}
          showCC={showCC}
        />
      )}
    </div>
  );
};
