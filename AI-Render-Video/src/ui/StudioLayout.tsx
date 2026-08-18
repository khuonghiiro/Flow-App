import React, { useState } from 'react';
import { Film, MessageSquare, Swords, Bot, Map, Layers, Download, Sparkles, Settings } from 'lucide-react';
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
  onUpdateScene: (updated: MasterSceneConfig) => void;
  onExportVideo: () => void;
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
  onUpdateScene,
  onExportVideo,
  isExporting,
  exportProgressMsg,
}) => {
  const [leftTab, setLeftTab] = useState<'dialogue' | 'combat'>('dialogue');
  const [rightTab, setRightTab] = useState<'director' | 'radar'>('director');
  const [showCC, setShowCC] = useState(true);
  const [showDialogueModal, setShowDialogueModal] = useState(false);

  return (
    <div className="studio-container">
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

        <div className="header-actions">
          <button
            className="btn-secondary"
            onClick={() => setShowDialogueModal(true)}
          >
            <MessageSquare size={14} color="#eab308" /> Quản Lý Thoại (TTS)
          </button>

          <button
            className="btn-primary"
            onClick={onExportVideo}
            disabled={isExporting}
          >
            <Download size={14} />
            {isExporting ? exportProgressMsg || 'Đang Xuất Video...' : 'Xuất Video MP4 (GPU)'}
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
                onInspectDialogue={onInspectDialogue}
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
            onToggleCC={() => setShowCC(!showCC)}
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

          <div className="sidebar-content">
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
