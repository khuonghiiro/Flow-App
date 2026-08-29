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

  // Listen to TopBar events for JSON export, JSON import, and Template reset
  useEffect(() => {
    const handleExportEvent = () => {
      exportProjectToJson(project);
      setSaveToast('Đã xuất file JSON thành công!');
      setTimeout(() => setSaveToast(null), 3000);
    };

    const handleImportTextEvent = (e: any) => {
      try {
        const text = e.detail;
        if (!text) return;
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

    const handleResetEvent = () => {
      setProject(DEFAULT_2D_DIRECTOR_PROJECT);
      if (DEFAULT_2D_DIRECTOR_PROJECT.shots.length > 0) {
        setActiveShotId(DEFAULT_2D_DIRECTOR_PROJECT.shots[0].id);
        setCurrentTime(0);
      }
      setSaveToast('Đã khôi phục dự án mẫu chuẩn!');
      setTimeout(() => setSaveToast(null), 3000);
    };

    window.addEventListener('flowmy:export-2d-project', handleExportEvent);
    window.addEventListener('flowmy:import-2d-project-text', handleImportTextEvent);
    window.addEventListener('flowmy:reset-2d-project', handleResetEvent);

    return () => {
      window.removeEventListener('flowmy:export-2d-project', handleExportEvent);
      window.removeEventListener('flowmy:import-2d-project-text', handleImportTextEvent);
      window.removeEventListener('flowmy:reset-2d-project', handleResetEvent);
    };
  }, [project]);

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

  const handleUpdateActorPosition = (actorId: string, pos: [number, number]) => {
    if (!currentShot) return;
    const updated = project.shots.map((s) => {
      if (s.id === currentShot.id) {
        const curActor = s.actors[actorId] || {
          actorId,
          worldFacingAngle: 0,
          positionStart: pos,
          positionEnd: pos,
          scale: 1.6,
          zIndex: 10,
          actionPose: 'idle_breathe' as ActionPoseType,
        };
        return {
          ...s,
          actors: {
            ...s.actors,
            [actorId]: {
              ...curActor,
              positionStart: pos,
              positionEnd: pos,
            },
          },
        };
      }
      return s;
    });
    setProject({ ...project, shots: updated });
  };

  const handleUpdateActorScale = (actorId: string, scale: number) => {
    if (!currentShot) return;
    const updated = project.shots.map((s) => {
      if (s.id === currentShot.id) {
        const curActor = s.actors[actorId];
        if (!curActor) return s;
        return {
          ...s,
          actors: {
            ...s.actors,
            [actorId]: {
              ...curActor,
              scale: Math.max(0.2, Math.min(5.0, Number(scale.toFixed(2)))),
            },
          },
        };
      }
      return s;
    });
    setProject({ ...project, shots: updated });
  };

  const handleUpdateActorZIndex = (actorId: string, delta: number) => {
    if (!currentShot) return;
    const updated = project.shots.map((s) => {
      if (s.id === currentShot.id) {
        const curActor = s.actors[actorId];
        if (!curActor) return s;
        const newZ = Math.max(1, Math.min(100, (curActor.zIndex || 10) + delta));
        return {
          ...s,
          actors: {
            ...s.actors,
            [actorId]: {
              ...curActor,
              zIndex: newZ,
            },
          },
        };
      }
      return s;
    });
    setProject({ ...project, shots: updated });
  };

  const handleUpdatePropPosition = (propId: string, pos: [number, number]) => {
    const updatedProps = (project.props || []).map((p) =>
      p.id === propId ? { ...p, position: pos } : p
    );
    setProject({ ...project, props: updatedProps });
  };

  const handleUpdatePropScale = (propId: string, scale: number) => {
    const updatedProps = (project.props || []).map((p) =>
      p.id === propId
        ? { ...p, scale: [Number(scale.toFixed(2)), Number(scale.toFixed(2))] as [number, number] }
        : p
    );
    setProject({ ...project, props: updatedProps });
  };

  const handleUpdatePropZIndex = (propId: string, delta: number) => {
    const updatedProps = (project.props || []).map((p) =>
      p.id === propId
        ? { ...p, zIndex: Math.max(1, Math.min(100, (p.zIndex || 5) + delta)) }
        : p
    );
    setProject({ ...project, props: updatedProps });
  };
  const handleUpdateActorRotation = (actorId: string, rotDeg: number) => {
    if (!currentShot) return;
    const updated = project.shots.map((s) => {
      if (s.id === currentShot.id) {
        const curActor = s.actors[actorId];
        if (!curActor) return s;
        return {
          ...s,
          actors: {
            ...s.actors,
            [actorId]: {
              ...curActor,
              rotation: Math.round(rotDeg),
            },
          },
        };
      }
      return s;
    });
    setProject({ ...project, shots: updated });
  };

  const handleUpdateActorFacingAngle = (actorId: string, angleDeg: number, flipX?: boolean) => {
    if (!currentShot) return;
    const updated = project.shots.map((s) => {
      if (s.id === currentShot.id) {
        const curActor = s.actors[actorId];
        if (!curActor) return s;
        return {
          ...s,
          actors: {
            ...s.actors,
            [actorId]: {
              ...curActor,
              worldFacingAngle: Math.round(angleDeg),
              flipX: flipX !== undefined ? flipX : curActor.flipX,
            },
          },
        };
      }
      return s;
    });
    setProject({ ...project, shots: updated });
  };

  const handleUpdateActorFlipX = (actorId: string, flipX: boolean) => {
    if (!currentShot) return;
    const updated = project.shots.map((s) => {
      if (s.id === currentShot.id) {
        const curActor = s.actors[actorId];
        if (!curActor) return s;
        return {
          ...s,
          actors: {
            ...s.actors,
            [actorId]: {
              ...curActor,
              flipX,
            },
          },
        };
      }
      return s;
    });
    setProject({ ...project, shots: updated });
  };

  const handleUpdatePropFlipX = (propId: string, flipX: boolean) => {
    const updatedProps = (project.props || []).map((p) =>
      p.id === propId ? { ...p, flipX } : p
    );
    setProject({ ...project, props: updatedProps });
  };

  const handleUpdatePropRotation = (propId: string, rotDeg: number) => {
    const updatedProps = (project.props || []).map((p) =>
      p.id === propId ? { ...p, rotation: Math.round(rotDeg) } : p
    );
    setProject({ ...project, props: updatedProps });
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
        position: 'relative',
      }}
    >
      {/* Floating Save Toast Notification */}
      {saveToast && (
        <div
          style={{
            position: 'absolute',
            top: 14,
            right: 20,
            zIndex: 9999,
            background: 'rgba(15, 23, 42, 0.95)',
            border: '1px solid rgba(74, 222, 128, 0.5)',
            boxShadow: '0 8px 24px rgba(0, 0, 0, 0.6)',
            color: '#4ade80',
            fontSize: 12,
            fontWeight: 700,
            padding: '8px 16px',
            borderRadius: 8,
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            backdropFilter: 'blur(8px)',
          }}
        >
          <Check size={14} /> {saveToast}
        </div>
      )}

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
                currentTime={currentTime}
                isPlaying={isPlaying}
                selectedActorId={selectedActorId}
                selectedPartId={selectedPartId}
                selectedPropId={selectedPropId}
                onSelectActor={(id) => {
                  setSelectedActorId(id);
                  if (id) {
                    setSelectedPropId(null);
                    setRightTab('motion');
                  }
                }}
                onSelectPart={setSelectedPartId}
                onSelectProp={(id) => {
                  setSelectedPropId(id);
                  if (id) {
                    setSelectedActorId(null);
                    setRightTab('props');
                  }
                }}
                onUpdateCameraAngle={handleUpdateCameraAngle}
                onUpdateActorPosition={handleUpdateActorPosition}
                onUpdateActorScale={handleUpdateActorScale}
                onUpdateActorRotation={handleUpdateActorRotation}
                onUpdateActorFacingAngle={handleUpdateActorFacingAngle}
                onUpdateActorFlipX={handleUpdateActorFlipX}
                onUpdateActorZIndex={handleUpdateActorZIndex}
                onUpdatePropPosition={handleUpdatePropPosition}
                onUpdatePropScale={handleUpdatePropScale}
                onUpdatePropRotation={handleUpdatePropRotation}
                onUpdatePropFlipX={handleUpdatePropFlipX}
                onUpdatePropZIndex={handleUpdatePropZIndex}
              />
            )}
          </div>

          {/* Bottom Multi-Track Timeline Scrubber */}
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
              onUpdateProject={setProject}
              selectedActorId={selectedActorId}
              onSelectActor={(id) => {
                setSelectedActorId(id);
                if (id) {
                  setSelectedPropId(null);
                  setRightTab('motion');
                }
              }}
              selectedPropId={selectedPropId}
              onSelectProp={(id) => {
                setSelectedPropId(id);
                if (id) {
                  setSelectedActorId(null);
                  setRightTab('props');
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
