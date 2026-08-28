import React, { useState, useEffect, useRef } from 'react';
import {
  Download,
  Upload,
  Play,
  RotateCcw,
  Sparkles,
  Layers,
  Film,
  FileCode,
  Check,
  Compass,
  Puzzle,
  Sliders,
  Image as ImageIcon,
  Trees,
} from 'lucide-react';
import {
  Director2DProject,
  MultiAngleDirectorShot,
  Actor2DProfile,
  ScenePropItem,
} from '../../../types/studio2d_director';
import {
  DEFAULT_2D_DIRECTOR_PROJECT,
  exportProjectToJson,
  importProjectFromJson,
} from './utils/jsonProjectHelper';
import { ActorAngleSlotManager } from './components/ActorAngleSlotManager';
import { LayerStackingAssembler } from './components/LayerStackingAssembler';
import { ScenePropManager } from './components/ScenePropManager';
import { Stage2DCanvas } from './components/Stage2DCanvas';
import { ShotSequencerPanel } from './components/ShotSequencerPanel';
import { MotionTrajectoryEditor } from './components/MotionTrajectoryEditor';
import { Timeline2DScrubber } from './components/Timeline2DScrubber';

export const MultiAngle2DStudioView: React.FC = () => {
  const [project, setProject] = useState<Director2DProject>(() => {
    const saved = localStorage.getItem('flowmy_2d_director_project');
    if (saved) {
      try {
        return importProjectFromJson(saved);
      } catch (e) {
        // fallback
      }
    }
    return DEFAULT_2D_DIRECTOR_PROJECT;
  });

  const [activeShotId, setActiveShotId] = useState<string>(
    project.shots[0]?.id || 'shot_1'
  );
  const [selectedActorId, setSelectedActorId] = useState<string>(
    project.actors[0]?.id || 'char_tieu_dao'
  );
  const [selectedPartId, setSelectedPartId] = useState<string | null>(null);
  const [selectedPropId, setSelectedPropId] = useState<string | null>(
    project.props?.[0]?.id || 'prop_truc_1'
  );
  const [rightTab, setRightTab] = useState<'props' | 'layers' | 'sprites' | 'motion'>('props');
  const [currentTime, setCurrentTime] = useState<number>(0);
  const [isPlaying, setIsPlaying] = useState<boolean>(false);
  const [saveToast, setSaveToast] = useState<string | null>(null);

  // Auto-save to localStorage
  useEffect(() => {
    localStorage.setItem('flowmy_2d_director_project', JSON.stringify(project));
  }, [project]);

  // Compute total duration & active shot based on currentTime
  const totalDuration = project.shots.reduce((s, shot) => s + shot.durationSeconds, 0);

  // Find active shot and local shot progress (0..1)
  let accumulated = 0;
  let currentShot = project.shots[0] || null;
  let shotProgress = 0;

  for (const shot of project.shots) {
    if (currentTime >= accumulated && currentTime <= accumulated + shot.durationSeconds) {
      currentShot = shot;
      shotProgress = (currentTime - accumulated) / (shot.durationSeconds || 1);
      break;
    }
    accumulated += shot.durationSeconds;
  }

  if (!currentShot && project.shots.length > 0) {
    currentShot = project.shots[project.shots.length - 1];
    shotProgress = 1;
  }

  useEffect(() => {
    if (currentShot && currentShot.id !== activeShotId) {
      setActiveShotId(currentShot.id);
    }
  }, [currentShot?.id]);

  // 60 FPS Playback Loop
  useEffect(() => {
    let animFrame: number;
    let lastTime = performance.now();

    const loop = (now: number) => {
      const delta = (now - lastTime) / 1000;
      lastTime = now;

      if (isPlaying) {
        setCurrentTime((prev) => {
          const next = prev + delta;
          if (next >= totalDuration) {
            return 0;
          }
          return next;
        });
      }
      animFrame = requestAnimationFrame(loop);
    };

    animFrame = requestAnimationFrame(loop);
    return () => cancelAnimationFrame(animFrame);
  }, [isPlaying, totalDuration]);

  // Export JSON Handler
  const handleExportJson = () => {
    exportProjectToJson(project);
    setSaveToast('Đã xuất file JSON thành công!');
    setTimeout(() => setSaveToast(null), 3000);
  };

  // Import JSON Handler
  const handleImportJson = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (event) => {
      try {
        const text = event.target?.result as string;
        const imported = importProjectFromJson(text);
        setProject(imported);
        if (imported.shots.length > 0) {
          setActiveShotId(imported.shots[0].id);
          setCurrentTime(0);
        }
        setSaveToast('Đã nhập dữ liệu dự án từ JSON thành công!');
        setTimeout(() => setSaveToast(null), 3000);
      } catch (err: any) {
        alert(err.message);
      }
    };
    reader.readAsText(file);
    e.target.value = '';
  };

  const handleUpdateCameraAngle = (yawDeg: number, pitchDeg?: number) => {
    if (!currentShot) return;
    const updated = project.shots.map((s) =>
      s.id === currentShot.id
        ? {
            ...s,
            camera: {
              ...s.camera,
              angleStart: yawDeg,
              angleEnd: yawDeg,
              ...(pitchDeg !== undefined ? { pitchStart: pitchDeg, pitchEnd: pitchDeg } : {}),
            },
          }
        : s
    );
    setProject({ ...project, shots: updated });
  };

  const activeActor = project.actors.find((a) => a.id === selectedActorId) || project.actors[0];

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        width: '100%',
        background: '#070b14',
        color: '#f8fafc',
        overflow: 'hidden',
      }}
    >
      {/* ─── 2D SUB-HEADER ACTION BAR ─────────────────────────────────── */}
      <div
        style={{
          height: 44,
          padding: '0 16px',
          background: 'rgba(15, 23, 42, 0.95)',
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexShrink: 0,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <span style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8' }}>
            🎬 Xưởng Ghép Ảnh, Cây Cối & Hoạt Ảnh 2.5D:
          </span>
          <span style={{ fontSize: 11, color: '#94a3b8' }}>{project.title}</span>
        </div>

        {/* Action Buttons: JSON Export, JSON Import, Reset */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {saveToast && (
            <span style={{ fontSize: 11, color: '#4ade80', fontWeight: 600, display: 'flex', alignItems: 'center', gap: 4 }}>
              <Check size={13} /> {saveToast}
            </span>
          )}

          {/* Import JSON Button */}
          <label
            title="Nhập file JSON dự án đã lưu để chạy lại"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '4px 10px',
              borderRadius: 6,
              background: 'rgba(255, 255, 255, 0.05)',
              border: '1px solid rgba(255, 255, 255, 0.12)',
              color: '#e2e8f0',
              fontSize: 11,
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            <Upload size={12} /> Nhập File JSON
            <input type="file" accept=".json" style={{ display: 'none' }} onChange={handleImportJson} />
          </label>

          {/* Export JSON Button */}
          <button
            onClick={handleExportJson}
            title="Xuất toàn bộ cấu hình dự án hoạt ảnh ra file JSON"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 5,
              padding: '4px 10px',
              borderRadius: 6,
              background: 'rgba(56, 189, 248, 0.15)',
              border: '1px solid rgba(56, 189, 248, 0.3)',
              color: '#38bdf8',
              fontSize: 11,
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            <Download size={12} /> Xuất File JSON
          </button>

          {/* Reset Demo Button */}
          <button
            onClick={() => {
              if (confirm('Khôi phục lại dự án hoạt ảnh mẫu đối thoại phản bác ban đầu?')) {
                setProject(DEFAULT_2D_DIRECTOR_PROJECT);
                setActiveShotId(DEFAULT_2D_DIRECTOR_PROJECT.shots[0].id);
                setCurrentTime(0);
              }
            }}
            title="Khôi phục dự án hoạt ảnh mẫu"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 4,
              padding: '4px 8px',
              borderRadius: 6,
              background: 'rgba(255, 255, 255, 0.03)',
              border: '1px solid rgba(255, 255, 255, 0.08)',
              color: '#94a3b8',
              fontSize: 11,
              cursor: 'pointer',
            }}
          >
            <RotateCcw size={11} /> Mẫu Chuẩn
          </button>
        </div>
      </div>

      {/* ─── 3-COLUMN STUDIO WORKSPACE ───────────────────────────────── */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: '310px 1fr 370px',
          gap: 10,
          padding: 10,
          flex: 1,
          minHeight: 0,
          overflow: 'hidden',
        }}
      >
        {/* LEFT COLUMN: Shot Sequencer & Dialogue Panel */}
        <div style={{ height: '100%', overflow: 'hidden' }}>
          <ShotSequencerPanel
            project={project}
            activeShotId={activeShotId}
            onSelectShot={(id) => {
              setActiveShotId(id);
              let acc = 0;
              for (const s of project.shots) {
                if (s.id === id) {
                  setCurrentTime(acc);
                  break;
                }
                acc += s.durationSeconds;
              }
            }}
            onUpdateProject={setProject}
          />
        </div>

        {/* CENTER COLUMN: Visual Interactive Stage & Timeline */}
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: 10,
            height: '100%',
            overflow: 'hidden',
          }}
        >
          {/* Interactive 2.5D Stage Canvas with direct 360° drag and HUD */}
          <div style={{ flex: 1, minHeight: 0 }}>
            {currentShot && (
              <Stage2DCanvas
                project={project}
                activeShot={currentShot}
                shotProgress={shotProgress}
                isPlaying={isPlaying}
                selectedActorId={selectedActorId}
                selectedPartId={selectedPartId}
                selectedPropId={selectedPropId}
                onSelectActor={setSelectedActorId}
                onSelectPart={setSelectedPartId}
                onSelectProp={setSelectedPropId}
                onUpdateCameraAngle={handleUpdateCameraAngle}
              />
            )}
          </div>

          {/* Bottom Timeline Scrubber */}
          <div style={{ flexShrink: 0 }}>
            <Timeline2DScrubber
              project={project}
              currentTime={currentTime}
              isPlaying={isPlaying}
              onTogglePlay={() => setIsPlaying(!isPlaying)}
              onSeek={setCurrentTime}
              activeShotId={activeShotId}
              onSelectShot={(id) => {
                setActiveShotId(id);
                let acc = 0;
                for (const s of project.shots) {
                  if (s.id === id) {
                    setCurrentTime(acc);
                    break;
                  }
                  acc += s.durationSeconds;
                }
              }}
            />
          </div>
        </div>

        {/* RIGHT COLUMN: Scene Props / Layer Stacking / Multi-Angle Sprites / Motion Editor */}
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: 8,
            height: '100%',
            overflow: 'hidden',
          }}
        >
          {/* Top Segmented Tab Switcher */}
          <div
            style={{
              display: 'flex',
              background: 'rgba(2, 6, 23, 0.8)',
              padding: 3,
              borderRadius: 8,
              border: '1px solid rgba(255, 255, 255, 0.1)',
              flexShrink: 0,
              gap: 2,
            }}
          >
            <button
              onClick={() => setRightTab('props')}
              style={{
                flex: 1,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 3,
                padding: '6px 2px',
                borderRadius: 6,
                fontSize: 9.5,
                fontWeight: rightTab === 'props' ? 700 : 500,
                background: rightTab === 'props' ? 'linear-gradient(135deg, #10b981, #059669)' : 'transparent',
                color: rightTab === 'props' ? '#fff' : '#94a3b8',
                border: 'none',
                cursor: 'pointer',
                transition: 'all 0.15s',
              }}
            >
              <Trees size={11} /> Cây Cối & Đạo Cụ
            </button>
            <button
              onClick={() => setRightTab('layers')}
              style={{
                flex: 1,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 3,
                padding: '6px 2px',
                borderRadius: 6,
                fontSize: 9.5,
                fontWeight: rightTab === 'layers' ? 700 : 500,
                background: rightTab === 'layers' ? 'linear-gradient(135deg, #0284c7, #38bdf8)' : 'transparent',
                color: rightTab === 'layers' ? '#fff' : '#94a3b8',
                border: 'none',
                cursor: 'pointer',
                transition: 'all 0.15s',
              }}
            >
              <Puzzle size={11} /> Ghép Lớp
            </button>
            <button
              onClick={() => setRightTab('sprites')}
              style={{
                flex: 1,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 3,
                padding: '6px 2px',
                borderRadius: 6,
                fontSize: 9.5,
                fontWeight: rightTab === 'sprites' ? 700 : 500,
                background: rightTab === 'sprites' ? 'linear-gradient(135deg, #a855f7, #c084fc)' : 'transparent',
                color: rightTab === 'sprites' ? '#fff' : '#94a3b8',
                border: 'none',
                cursor: 'pointer',
                transition: 'all 0.15s',
              }}
            >
              <ImageIcon size={11} /> 8 Góc
            </button>
            <button
              onClick={() => setRightTab('motion')}
              style={{
                flex: 1,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 3,
                padding: '6px 2px',
                borderRadius: 6,
                fontSize: 9.5,
                fontWeight: rightTab === 'motion' ? 700 : 500,
                background: rightTab === 'motion' ? 'linear-gradient(135deg, #f59e0b, #fbbf24)' : 'transparent',
                color: rightTab === 'motion' ? '#000' : '#94a3b8',
                border: 'none',
                cursor: 'pointer',
                transition: 'all 0.15s',
              }}
            >
              <Sliders size={11} /> Zoom & Động Tác
            </button>
          </div>

          {/* Tab Content */}
          <div style={{ flex: 1, minHeight: 0, overflowY: 'auto' }}>
            {rightTab === 'props' && (
              <ScenePropManager
                props={project.props || []}
                selectedPropId={selectedPropId}
                onSelectProp={setSelectedPropId}
                onUpdateProps={(updated) => setProject({ ...project, props: updated })}
              />
            )}

            {rightTab === 'layers' && activeActor && (
              <LayerStackingAssembler
                actor={activeActor}
                selectedPartId={selectedPartId}
                onSelectPart={setSelectedPartId}
                onUpdateActor={(updated) => {
                  const acts = project.actors.map((a) => (a.id === updated.id ? updated : a));
                  setProject({ ...project, actors: acts });
                }}
              />
            )}

            {rightTab === 'sprites' && (
              <ActorAngleSlotManager
                actors={project.actors}
                selectedActorId={selectedActorId}
                onSelectActor={setSelectedActorId}
                onUpdateActor={(updated) => {
                  const acts = project.actors.map((a) => (a.id === updated.id ? updated : a));
                  setProject({ ...project, actors: acts });
                }}
              />
            )}

            {rightTab === 'motion' && currentShot && (
              <MotionTrajectoryEditor
                activeShot={currentShot}
                actors={project.actors}
                selectedActorId={selectedActorId}
                onUpdateShot={(updates) => {
                  const updated = project.shots.map((s) => (s.id === currentShot.id ? { ...s, ...updates } : s));
                  setProject({ ...project, shots: updated });
                }}
              />
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
