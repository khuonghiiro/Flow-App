import React, { useState, useRef } from 'react';
import { Film, MessageSquare, Swords, Bot, Map, Layers, Download, Sparkles, Settings, Clapperboard, FolderUp, Loader, FolderOpen, Maximize2, Minimize2, Move, Lightbulb, Wrench } from 'lucide-react';
import { MasterSceneConfig, DialogueManifestItem, EnvironmentOverride } from '../types/scene';
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
import { WeatherControlPanel } from './WeatherControlPanel';
import { AssetBrowserPanel } from './AssetBrowserPanel';
import { LightingStudioPanel } from './LightingStudioPanel';
import { CharacterWorkbenchPanel } from './CharacterWorkbenchPanel';
import { TransformInspector, SelectedSceneObject } from './TransformInspector';
import { sampleScenes, sceneCategories } from '../core/scenes/SceneRegistry';
import { InspectCameraAngle } from '../core/camera/CameraFraming';
import { MapPresetManager } from '../core/maps/MapPresetManager';
import { PlacedProp } from '../types/map_preset';
import { getSavedViewportSettings, saveViewportSetting } from '../core/storage/ViewportSettingsStorage';

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
  onImportCustomMap?: (files: FileList | File[]) => void;
  onExportVideo: (fps?: number) => void;
  isExporting: boolean;
  exportProgressMsg: string;
  envOverride: EnvironmentOverride;
  onUpdateEnvOverride: (override: EnvironmentOverride) => void;
  onPlaceProp?: (prop: any) => void;
  onSelectMap?: (mapId: string) => void;
  onSelectAvatar?: (actorId: string, vrmUrl: string) => void;
  onPlayAnimationPreview?: (animName: string) => void;
  selectedObject?: SelectedSceneObject | null;
  onSelectObject?: (obj: SelectedSceneObject | null) => void;
  onUpdateTransform?: (updated: SelectedSceneObject) => void;
  onDeleteProp?: (propId: string) => void;
  onDuplicateProp?: (prop: PlacedProp) => void;
  onFocusObject?: (position: [number, number, number]) => void;
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
  envOverride,
  onUpdateEnvOverride,
  onPlaceProp,
  onSelectMap,
  onSelectAvatar,
  onPlayAnimationPreview,
  selectedObject,
  onSelectObject,
  onUpdateTransform,
  onDeleteProp,
  onDuplicateProp,
  onFocusObject,
}) => {
  const [leftTab, setLeftTab] = useState<'dialogue' | 'combat' | 'weather'>('dialogue');
  const [rightTab, setRightTab] = useState<'director' | 'radar' | 'inspector'>('inspector');
  const [bottomTab, setBottomTab] = useState<'timeline' | 'assets' | 'lighting' | 'workbench'>('timeline');
  const [isBottomMaximized, setIsBottomMaximized] = useState<boolean>(false);
  const [showCC, setShowCC] = useState(() => getSavedViewportSettings().showCC);
  const [showDialogueModal, setShowDialogueModal] = useState(false);
  const [showWorkbenchModal, setShowWorkbenchModal] = useState(false);
  const [exportFps, setExportFps] = useState<number>(120);
  const [gizmoMode, setGizmoMode] = useState<'translate' | 'rotate' | 'scale'>('translate');
  const [gizmoSpace, setGizmoSpace] = useState<'world' | 'local'>('world');

  const handleChangeGizmoMode = (mode: 'translate' | 'rotate' | 'scale') => {
    setGizmoMode(mode);
    if (renderer && renderer.gizmo) {
      renderer.gizmo.setMode(mode);
    }
  };

  const handleToggleGizmoSpace = () => {
    const next = gizmoSpace === 'world' ? 'local' : 'world';
    setGizmoSpace(next);
    if (renderer && renderer.gizmo) {
      renderer.gizmo.setSpace(next);
    }
  };
  
  const [selectedCategory, setSelectedCategory] = useState<string>(() => {
    return sceneCategories.find(c => c.scenes.some(s => s.scene_id === scene.scene_id))?.id || sceneCategories[0]?.id || '';
  });

  React.useEffect(() => {
    const cat = sceneCategories.find(c => c.scenes.some(s => s.scene_id === scene.scene_id))?.id;
    if (cat) setSelectedCategory(cat);
  }, [scene.scene_id]);

  const mapInputRef = useRef<HTMLInputElement>(null);
  const folderInputRef = useRef<HTMLInputElement>(null);

  const handleMapFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files && files.length > 0 && onImportCustomMap) {
      onImportCustomMap(files);
    }
    e.target.value = '';
  };

  return (
    <div className="studio-container">
      {/* Hidden Folder Map Input */}
      <input
        type="file"
        ref={folderInputRef}
        {...({ webkitdirectory: '', directory: '' } as any)}
        multiple
        style={{ display: 'none' }}
        onChange={handleMapFileChange}
      />

      {/* Hidden Single File Map Input */}
      <input
        type="file"
        ref={mapInputRef}
        accept=".glb,.gltf,.bin,.png,.jpg,.jpeg,.webp"
        multiple
        style={{ display: 'none' }}
        onChange={handleMapFileChange}
      />

      {/* Sleek Studio Header */}
      <header
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          height: 48,
          padding: '0 16px',
          background: 'rgba(11, 15, 25, 0.85)',
          backdropFilter: 'blur(12px)',
          borderBottom: '1px solid rgba(255,255,255,0.08)',
          zIndex: 100,
          flexShrink: 0,
        }}
      >
        {/* Brand */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: 28,
              height: 28,
              background: 'linear-gradient(135deg, #38bdf8, #a855f7)',
              borderRadius: 6,
              color: 'white',
              boxShadow: '0 0 10px rgba(56, 189, 248, 0.4)',
            }}
          >
            <Film size={16} />
          </div>
          <div>
            <div style={{ fontSize: 14, fontWeight: 700, color: '#f8fafc', lineHeight: 1.1 }}>FlowMy AI Studio</div>
            <div style={{ fontSize: 10, color: '#64748b' }}>WebCodecs GPU Render</div>
          </div>
        </div>

        {/* Center: Scene Selection */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, background: 'rgba(255,255,255,0.03)', padding: '4px 12px', borderRadius: 20, border: '1px solid rgba(255,255,255,0.05)' }}>
          <span style={{ fontSize: 11, fontWeight: 600, color: '#94a3b8' }}>Mẫu cảnh:</span>
          
          <select
            style={{
              padding: '2px 8px',
              fontSize: 12,
              borderRadius: 6,
              background: 'rgba(15, 20, 36, 0.9)',
              border: '1px solid rgba(255,255,255,0.1)',
              color: '#ffffff',
              cursor: 'pointer',
              outline: 'none',
            }}
            value={selectedCategory}
            onChange={(e) => {
              const catId = e.target.value;
              setSelectedCategory(catId);
              const cat = sceneCategories.find((c) => c.id === catId);
              if (cat && cat.scenes.length > 0) {
                onUpdateScene(cat.scenes[0]);
              }
            }}
          >
            {sceneCategories.map((c) => (
              <option key={c.id} value={c.id}>
                📁 {c.title}
              </option>
            ))}
          </select>

          <select
            style={{
              padding: '2px 8px',
              fontSize: 12,
              borderRadius: 6,
              background: 'rgba(15, 20, 36, 0.9)',
              border: '1px solid rgba(255,255,255,0.1)',
              color: '#ffffff',
              cursor: 'pointer',
              outline: 'none',
            }}
            value={scene.scene_id}
            onChange={(e) => {
              const selected = sampleScenes.find((s) => s.scene_id === e.target.value);
              if (selected) {
                onUpdateScene(selected);
              }
            }}
          >
            {sceneCategories.find((c) => c.id === selectedCategory)?.scenes.map((s) => (
              <option key={s.scene_id} value={s.scene_id}>
                {s.title || s.scene_id}
              </option>
            ))}
          </select>
        </div>

        {/* Right: Actions */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div style={{ display: 'flex', gap: 6 }}>
            <button
              onClick={() => folderInputRef.current?.click()}
              title="Import Folder (.gltf + .bin + textures)"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '4px 10px',
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 6,
                border: '1px solid rgba(56, 189, 248, 0.3)',
                background: 'rgba(56, 189, 248, 0.1)',
                color: '#38bdf8',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              <FolderUp size={12} /> Folder
            </button>
            <button
              onClick={() => mapInputRef.current?.click()}
              title="Import File .glb"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '4px 10px',
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 6,
                border: '1px solid rgba(168, 85, 247, 0.3)',
                background: 'rgba(168, 85, 247, 0.1)',
                color: '#c084fc',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              <Layers size={12} /> GLB
            </button>
            <button
              onClick={() => setShowWorkbenchModal(true)}
              title="Mở Xưởng Lắp Ghép Nhân Vật, Auto-Rig & Thiết Kế Map 3D"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '4px 12px',
                fontSize: 11,
                fontWeight: 700,
                borderRadius: 6,
                border: '1px solid rgba(245, 158, 11, 0.4)',
                background: 'linear-gradient(135deg, rgba(245, 158, 11, 0.2), rgba(217, 119, 6, 0.1))',
                color: '#fbbf24',
                cursor: 'pointer',
                transition: 'all 0.2s',
                boxShadow: '0 2px 8px rgba(245, 158, 11, 0.2)',
              }}
            >
              <Wrench size={12} /> 🛠️ Xưởng 3D & Auto-Rig
            </button>
            <button
              onClick={() => {
                const mapId = prompt('Nhập mã định danh Map ID (ví dụ: my_custom_village):', `custom_map_${Date.now().toString().slice(-4)}`);
                if (!mapId) return;
                const name = prompt('Nhập tên hiển thị của Map:', 'Bản Đồ Tùy Chỉnh') || mapId;
                const desc = prompt('Nhập mô tả bối cảnh (để AI đọc hiểu):', 'Bản đồ tùy chỉnh gồm các vật thể và điểm xuất hiện.') || '';
                
                const placedProps = scene.environment.placed_props || [
                  { id: 'tree_main', asset_path: 'props/nature/tree_sakura.glb', position: [4, 0, -3] as [number, number, number], type: 'nature' as const },
                  { id: 'bench_rest', asset_path: 'props/furniture/chair_wooden.glb', position: [-4, 0, -2] as [number, number, number], type: 'furniture' as const, smart_socket: { socket_type: 'sit' as const } },
                  { id: 'farm_plot', asset_path: 'props/tools/farm_plot.glb', position: [0, 0, -5] as [number, number, number], type: 'nature' as const, smart_socket: { socket_type: 'harvest' as const } }
                ];

                const preset = MapPresetManager.createPresetFromScene(scene, placedProps, mapId, name, desc);
                MapPresetManager.downloadPresetJson(preset);
                alert(`✅ Đã xuất tệp cấu hình ${mapId}.json!\n\nHãy lưu file này vào thư mục assets/maps/presets/ rồi chạy _scan_assets.bat để AI tự động nhận diện và tái sử dụng bản đồ này.`);
              }}
              title="Lưu cấu hình bản đồ hiện tại (Map Preset JSON)"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '4px 10px',
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 6,
                border: '1px solid rgba(34, 197, 94, 0.3)',
                background: 'rgba(34, 197, 94, 0.1)',
                color: '#4ade80',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              <Map size={12} /> Lưu Map
            </button>
          </div>

          <div style={{ width: 1, height: 24, background: 'rgba(255,255,255,0.1)' }}></div>


          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <button
              onClick={() => setShowDialogueModal(true)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '4px 10px',
                fontSize: 11,
                fontWeight: 600,
                borderRadius: 6,
                border: '1px solid rgba(234, 179, 8, 0.3)',
                background: 'rgba(234, 179, 8, 0.1)',
                color: '#eab308',
                cursor: 'pointer',
                transition: 'all 0.2s',
              }}
            >
              <MessageSquare size={12} /> TTS
            </button>

            <div style={{ width: 1, height: 24, background: 'rgba(255,255,255,0.1)' }}></div>
            
            <select
              style={{
                padding: '4px 8px',
                fontSize: 11,
                borderRadius: 6,
                background: 'rgba(15, 20, 36, 0.9)',
                border: '1px solid rgba(255,255,255,0.1)',
                color: '#38bdf8',
                fontWeight: 600,
                cursor: 'pointer',
                outline: 'none',
              }}
              value={exportFps}
              onChange={(e) => setExportFps(parseInt(e.target.value))}
            >
              <option value={30}>30 FPS</option>
              <option value={60}>60 FPS</option>
              <option value={120}>120 FPS</option>
            </select>

            <button
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '4px 12px',
                fontSize: 12,
                fontWeight: 600,
                borderRadius: 6,
                background: 'linear-gradient(135deg, #38bdf8, #2563eb)',
                color: '#ffffff',
                border: 'none',
                cursor: 'pointer',
                opacity: isExporting ? 0.7 : 1,
              }}
              onClick={() => onExportVideo(exportFps)}
              disabled={isExporting}
            >
              {isExporting ? <Loader className="spin" size={14} /> : <Download size={14} />}
              {isExporting ? 'Đang xuất...' : 'Xuất MP4'}
            </button>
          </div>
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
            <button
              className={`sidebar-tab-btn ${leftTab === 'weather' ? 'active' : ''}`}
              onClick={() => setLeftTab('weather')}
            >
              <Sparkles size={14} /> Thời Tiết
            </button>
          </div>

          <div className="sidebar-content">
            {leftTab === 'dialogue' && (
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
            )}
            {leftTab === 'combat' && (
              <CombatDebugger
                scene={scene}
                currentTime={currentTime}
                onSeekToImpact={(impactTime) => onSeek(impactTime)}
              />
            )}
            {leftTab === 'weather' && (
              <WeatherControlPanel 
                override={envOverride} 
                onChange={onUpdateEnvOverride} 
              />
            )}
          </div>
        </aside>

        {/* Central Viewport & Multi-Track Timeline / Asset Browser */}
        <main className="studio-center">
          <ViewportCanvas
            renderer={renderer}
            fps={fps}
            activeSubtitle={activeSubtitle}
            subtitlesConfig={scene.subtitles_config}
            scene={scene}
            showCC={showCC}
            isInspecting={!!inspectingActorId}
            isFreeCam={isFreeCam}
            isLoadingMap={isLoadingMap}
            selectedObjectId={selectedObject?.id || null}
            selectedObjectName={selectedObject?.name || null}
            gizmoMode={gizmoMode}
            gizmoSpace={gizmoSpace}
            onChangeGizmoMode={handleChangeGizmoMode}
            onToggleGizmoSpace={handleToggleGizmoSpace}
            onSelectObject={onSelectObject}
            onDeselectObject={() => onSelectObject?.(null)}
            onDeleteProp={onDeleteProp}
            onToggleCC={() => {
              const next = !showCC;
              setShowCC(next);
              saveViewportSetting('showCC', next);
            }}
            onToggleFreeCam={onToggleFreeCam}
            onResetCamera={onResetCamera}
          />

          {/* Unity-Style Bottom Dock Panel */}
          <div className={`studio-bottom-dock ${isBottomMaximized ? 'maximized' : ''}`}>
            <div className="bottom-dock-tabs">
              <div className="dock-tabs-left">
                <button
                  className={`dock-tab-btn ${bottomTab === 'timeline' ? 'active' : ''}`}
                  onClick={() => setBottomTab('timeline')}
                >
                  <Film size={13} /> Timeline & Hoạt Cảnh
                </button>
                <button
                  className={`dock-tab-btn ${bottomTab === 'assets' ? 'active' : ''}`}
                  onClick={() => setBottomTab('assets')}
                >
                  <FolderOpen size={13} /> Project Assets (Thư Mục Tài Nguyên)
                </button>
                <button
                  className={`dock-tab-btn ${bottomTab === 'lighting' ? 'active' : ''}`}
                  onClick={() => setBottomTab('lighting')}
                >
                  <Lightbulb size={13} /> Studio Ánh Sáng & Nguồn Sáng 3D
                </button>
                <button
                  className={`dock-tab-btn ${bottomTab === 'workbench' ? 'active' : ''}`}
                  onClick={() => setBottomTab('workbench')}
                >
                  <Wrench size={13} /> 🛠️ Xưởng Nhân Vật & Auto-Rig
                </button>
              </div>

              <div className="dock-tabs-right">
                <button
                  className="dock-btn"
                  title={isBottomMaximized ? 'Thu nhỏ về chuẩn' : 'Mở rộng khung làm việc'}
                  onClick={() => setIsBottomMaximized(!isBottomMaximized)}
                >
                  {isBottomMaximized ? <Minimize2 size={12} /> : <Maximize2 size={12} />}
                  {isBottomMaximized ? 'Thu Nhỏ' : 'Mở Rộng'}
                </button>
              </div>
            </div>

            <div className="bottom-dock-content">
              {bottomTab === 'timeline' ? (
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
              ) : bottomTab === 'assets' ? (
                <AssetBrowserPanel
                  onPlaceProp={(prop) => onPlaceProp?.(prop)}
                  onSelectMap={(mapId) => onSelectMap?.(mapId)}
                  onSelectAvatar={(actorId, vrmUrl) => onSelectAvatar?.(actorId, vrmUrl)}
                  onPlayAnimationPreview={onPlayAnimationPreview}
                  onImportCustomFiles={onImportCustomMap}
                  actorsList={scene.actors.map((a) => ({ id: a.id, name: a.name || a.id }))}
                  isMaximized={isBottomMaximized}
                  onToggleMaximize={() => setIsBottomMaximized(!isBottomMaximized)}
                />
              ) : bottomTab === 'lighting' ? (
                <LightingStudioPanel
                  scene={scene}
                  onUpdateScene={onUpdateScene}
                  selectedObjectId={selectedObject?.id}
                  onFocusObject={onFocusObject}
                />
              ) : (
                <CharacterWorkbenchPanel
                  scene={scene}
                  onUpdateScene={onUpdateScene}
                  onSelectAvatar={onSelectAvatar}
                  onSelectMap={onSelectMap}
                />
              )}
            </div>
          </div>
        </main>

        {/* Right Sidebar: AI Director & Radar & Transform Inspector */}
        <aside className="studio-sidebar right">
          <div className="sidebar-tabs">
            <button
              className={`sidebar-tab-btn ${rightTab === 'inspector' ? 'active' : ''}`}
              onClick={() => setRightTab('inspector')}
            >
              <Move size={13} /> Transform (XYZ)
            </button>
            <button
              className={`sidebar-tab-btn ${rightTab === 'director' ? 'active' : ''}`}
              onClick={() => setRightTab('director')}
            >
              <Bot size={13} /> AI Đạo Diễn
            </button>
            <button
              className={`sidebar-tab-btn ${rightTab === 'radar' ? 'active' : ''}`}
              onClick={() => setRightTab('radar')}
            >
              <Map size={13} /> 2D Radar
            </button>
          </div>

          <div className="sidebar-content" style={{ display: 'flex', flexDirection: 'column', height: 'calc(100% - 45px)' }}>
            {rightTab === 'inspector' ? (
              <TransformInspector
                scene={scene}
                selectedObject={selectedObject || null}
                onSelectObject={(obj) => onSelectObject?.(obj)}
                onUpdateTransform={(updated) => onUpdateTransform?.(updated)}
                onDeleteProp={(propId) => onDeleteProp?.(propId)}
                onDuplicateProp={(prop) => onDuplicateProp?.(prop)}
                onFocusObject={(pos) => onFocusObject?.(pos)}
              />
            ) : rightTab === 'director' ? (
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

      {/* 3D Character Workbench & Auto-Rig Studio Full Modal Window */}
      {showWorkbenchModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            zIndex: 9999,
            background: 'rgba(2, 6, 23, 0.85)',
            backdropFilter: 'blur(12px)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            padding: 20,
          }}
        >
          <div
            style={{
              width: '88vw',
              maxWidth: '1240px',
              height: '84vh',
              maxHeight: '820px',
              background: '#090d16',
              borderRadius: 14,
              border: '1px solid rgba(56, 189, 248, 0.3)',
              boxShadow: '0 25px 60px -15px rgba(0, 0, 0, 0.95), 0 0 30px rgba(56, 189, 248, 0.15)',
              overflow: 'hidden',
              display: 'flex',
              flexDirection: 'column',
            }}
          >
            <CharacterWorkbenchPanel
              scene={scene}
              onUpdateScene={onUpdateScene}
              onSelectAvatar={onSelectAvatar}
              onSelectMap={onSelectMap}
              onClose={() => setShowWorkbenchModal(false)}
              isModal={true}
            />
          </div>
        </div>
      )}
    </div>
  );
};
