import React, { useState, useRef } from 'react';
import { Film, MessageSquare, Swords, Bot, Map, Layers, Download, Sparkles, Settings, Clapperboard, FolderUp } from 'lucide-react';
import { MasterSceneConfig, DialogueManifestItem } from '../types/scene';
import { ThreeRenderer } from '../core/engine/ThreeRenderer';
import { ActiveSubtitle } from '../core/subtitles/SubtitleSynchronizer';
import { VRMAvatar } from '../core/actors/VRMAvatar';
import { NavMeshManager } from '../core/navigation/NavMeshManager';
import { ViewportCanvas } from './ViewportCanvas';
import { TimelineScrubber } from './TimelineScrubber';
import { SubtitleInspector } from './SubtitleInspector';
import { CombatDebugger } from './CombatDebugger';
import { MapRadarView } from './MapRadarView';
import { AIChatDirector } from './AIChatDirector';
import { DialogueEditorModal } from './DialogueEditorModal';
import { sampleScenes } from '../samples/villageClashScene';
import { InspectCameraAngle } from '../core/camera/CameraFraming';

interface StudioLayoutProps {
  scene: MasterSceneConfig;
  renderer: ThreeRenderer | null;
  fps: number;
  currentTime: number;
  isPlaying: boolean;
  playbackRate: number;
  isLooping: boolean;
  activeSubtitle: ActiveSubtitle | null;
  actors: Map<string, VRMAvatar>;
  navMesh: NavMeshManager;
  onTogglePlay: () => void;
  onSeek: (time: number) => void;
  onToggleLoop: () => void;
  onChangePlaybackRate: (rate: number) => void;
  onInspectDialogue: (dlg: DialogueManifestItem) => void;
  onPreviewSpeech: (dlg: DialogueManifestItem) => void;
  inspectAngle: InspectCameraAngle;
  inspectingActorId: string | null;
  onChangeInspectAngle: (angle: InspectCameraAngle) => void;
  onResetCamera: () => void;
  isFreeCam?: boolean;
  isLoadingMap?: boolean;
  onToggleFreeCam?: () => void;
  onUpdateScene: (updated: MasterSceneConfig) => void;
  onImportCustomMap?: (file: File) => void;
  onExportVideo: (fps?: number) => void;
  isExporting: boolean;
  exportProgressMsg: string;
}

export const StudioLayout: React.FC<StudioLayoutProps> = ({
  scene,
  renderer,
  fps,
  currentTime,
  isPlaying,
  playbackRate,
  isLooping,
  activeSubtitle,
  actors,
  navMesh,
  onTogglePlay,
  onSeek,
  onToggleLoop,
  onChangePlaybackRate,
  onInspectDialogue,
  onPreviewSpeech,
  inspectAngle,
  inspectingActorId,
  onChangeInspectAngle,
  onResetCamera,
  isFreeCam,
  isLoadingMap,
  onToggleFreeCam,
  onUpdateScene,
  onImportCustomMap,
  onExportVideo,
  isExporting,
  exportProgressMsg,
}) => {
  const [leftTab, setLeftTab] = useState<'dialogue' | 'combat'>('dialogue');
  const [rightTab, setRightTab] = useState<'director' | 'radar'>('director');
  const [showCC, setShowCC] = useState(true);
  const [showDialogueModal, setShowDialogueModal] = useState(false);
  const [exportFps, setExportFps] = useState<number>(120);
  const mapInputRef = useRef<HTMLInputElement>(null);

  const handleMapFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file && onImportCustomMap) {
      onImportCustomMap(file);
    }
  };

  return (
    <div className="studio-container">
      {/* Hidden Map File Input */}
      <input
        type="file"
        ref={mapInputRef}
        accept=".glb,.gltf"
        style={{ display: 'none' }}
        onChange={handleMapFileChange}
      />

      {/* Studio Header */}
      <header className="studio-header">
        <div className="studio-brand">
          <div className="studio-logo">
            <Film size={20} />
          </div>
          <div>
            <div className="studio-title">AI 3D Animation Studio</div>
            <div style={{ fontSize: 10, color: '#94a3b8' }}>All-in-One Master Orchestrator</div>
          </div>
          <span className="studio-badge">TypeScript + WebCodecs</span>
        </div>

        {/* Scene Presets Selector */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 11, color: '#94a3b8', display: 'flex', alignItems: 'center', gap: 4 }}>
            <Clapperboard size={12} color="#38bdf8" /> Mẫu Cảnh:
          </span>
          <select
            className="form-input"
            style={{
              padding: '4px 10px',
              fontSize: 12,
              borderRadius: 8,
              background: 'rgba(15, 20, 36, 0.9)',
              border: '1px solid var(--border-glow)',
              color: '#ffffff',
              cursor: 'pointer',
            }}
            value={scene.scene_id}
            onChange={(e) => {
              const selected = sampleScenes.find((s) => s.scene_id === e.target.value);
              if (selected) {
                onUpdateScene(selected);
              }
            }}
          >
            {sampleScenes.map((s) => (
              <option key={s.scene_id} value={s.scene_id}>
                {s.title || s.scene_id}
              </option>
            ))}
          </select>
        </div>

        <div className="header-actions">
          {/* Import Map Button */}
          <button
            className="btn-secondary"
            style={{
              borderColor: '#38bdf8',
              color: '#38bdf8',
              background: 'rgba(56, 189, 248, 0.1)',
            }}
            onClick={() => mapInputRef.current?.click()}
            title="Chọn file 3D .glb / .gltf từ máy tính để làm map sân khấu"
          >
            <FolderUp size={14} color="#38bdf8" /> <strong>Import Map 3D (.glb)</strong>
          </button>

          {/* Export FPS Selector */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ fontSize: 11, color: '#94a3b8' }}>FPS Xuất:</span>
            <select
              className="form-input"
              style={{
                padding: '4px 8px',
                fontSize: 11,
                borderRadius: 6,
                background: 'rgba(15, 20, 36, 0.9)',
                border: '1px solid var(--border-glow)',
                color: '#38bdf8',
                fontWeight: 600,
                cursor: 'pointer',
              }}
              value={exportFps}
              onChange={(e) => setExportFps(Number(e.target.value))}
            >
              <option value={30}>30 FPS</option>
              <option value={60}>60 FPS (Mượt)</option>
              <option value={120}>✨ 120 FPS (Siêu Mượt HFR)</option>
            </select>
          </div>

          <button
            className="btn-secondary"
            onClick={() => setShowDialogueModal(true)}
          >
            <MessageSquare size={14} color="#eab308" /> Quản Lý Thoại (TTS)
          </button>

          <button
            className="btn-primary"
            onClick={() => onExportVideo(exportFps)}
            disabled={isExporting}
          >
            <Download size={14} />
            {isExporting ? exportProgressMsg || 'Đang Xuất Video...' : `Xuất MP4 (${exportFps} FPS)`}
          </button>
        </div>
      </header>

      {/* Main Body */}
      <div className="studio-body">
        {/* Left Sidebar: Subtitles Inspector & Combat Debugger */}
        <aside className="studio-sidebar">
          <div className="sidebar-tabs">
            <button
              className={`sidebar-tab-btn ${leftTab === 'dialogue' ? 'active' : ''}`}
              onClick={() => setLeftTab('dialogue')}
            >
              <MessageSquare size={14} /> Phụ Đề & Thoại
            </button>
            <button
              className={`sidebar-tab-btn ${leftTab === 'combat' ? 'active' : ''}`}
              onClick={() => setLeftTab('combat')}
            >
              <Swords size={14} /> Combat Sync
            </button>
          </div>

          <div className="sidebar-content">
            {leftTab === 'dialogue' ? (
              <SubtitleInspector
                scene={scene}
                currentTime={currentTime}
                inspectAngle={inspectAngle}
                inspectingActorId={inspectingActorId}
                onChangeInspectAngle={onChangeInspectAngle}
                onInspectDialogue={onInspectDialogue}
                onResetCamera={onResetCamera}
                onPreviewSpeech={onPreviewSpeech}
              />
            ) : (
              <CombatDebugger
                scene={scene}
                currentTime={currentTime}
                onSeekToImpact={(impactTime) => onSeek(impactTime)}
              />
            )}
          </div>
        </aside>

        {/* Central Viewport & Multi-Track Timeline */}
        <main className="studio-center">
          <ViewportCanvas
            renderer={renderer}
            fps={fps}
            activeSubtitle={activeSubtitle}
            subtitlesConfig={scene.subtitles_config}
            showCC={showCC}
            isInspecting={!!inspectingActorId}
            isFreeCam={isFreeCam}
            isLoadingMap={isLoadingMap}
            onToggleCC={() => setShowCC(!showCC)}
            onToggleFreeCam={onToggleFreeCam}
            onResetCamera={onResetCamera}
          />

          <TimelineScrubber
            scene={scene}
            currentTime={currentTime}
            isPlaying={isPlaying}
            playbackRate={playbackRate}
            isLooping={isLooping}
            onTogglePlay={onTogglePlay}
            onSeek={onSeek}
            onToggleLoop={onToggleLoop}
            onChangePlaybackRate={onChangePlaybackRate}
          />
        </main>

        {/* Right Sidebar: AI Director & Radar */}
        <aside className="studio-sidebar right">
          <div className="sidebar-tabs">
            <button
              className={`sidebar-tab-btn ${rightTab === 'director' ? 'active' : ''}`}
              onClick={() => setRightTab('director')}
            >
              <Bot size={14} /> AI Đạo Diễn
            </button>
            <button
              className={`sidebar-tab-btn ${rightTab === 'radar' ? 'active' : ''}`}
              onClick={() => setRightTab('radar')}
            >
              <Map size={14} /> 2D Radar Map
            </button>
          </div>

          <div className="sidebar-content" style={{ display: 'flex', flexDirection: 'column', height: 'calc(100% - 45px)' }}>
            {rightTab === 'director' ? (
              <AIChatDirector scene={scene} onApplyScene={onUpdateScene} />
            ) : (
              <MapRadarView actors={actors} navMesh={navMesh} />
            )}
          </div>
        </aside>
      </div>

      {/* Dialogue & TTS Manager Modal */}
      <DialogueEditorModal
        scene={scene}
        isOpen={showDialogueModal}
        onClose={() => setShowDialogueModal(false)}
        onUpdateScene={onUpdateScene}
      />
    </div>
  );
};
