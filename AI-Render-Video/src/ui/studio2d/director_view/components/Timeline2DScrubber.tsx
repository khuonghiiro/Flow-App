// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React, { useState, useRef } from 'react';
import {
  Director2DProject,
  MultiAngleDirectorShot,
  ActionPoseType,
  PropGrowthStage,
} from '../../../../types/studio2d_director';
import { findShotAtTime } from './timeline/timelineConstants';
import { TimelineHeaderToolbar } from './timeline/TimelineHeaderToolbar';
import { TimelineTrackRuler } from './timeline/TimelineTrackRuler';
import { TimelineActorTrack } from './timeline/TimelineActorTrack';
import { TimelineDialogueTrack } from './timeline/TimelineDialogueTrack';
import { TimelinePropTrack } from './timeline/TimelinePropTrack';
import { TimelineCameraTrack } from './timeline/TimelineCameraTrack';
import { TimelineActionModal } from './timeline/TimelineActionModal';
import { TimelineDialogueModal } from './timeline/TimelineDialogueModal';
import { TimelinePropGrowthModal } from './timeline/TimelinePropGrowthModal';
import { TimelineShotDurationModal } from './timeline/TimelineShotDurationModal';

export interface Timeline2DScrubberProps {
  project: Director2DProject;
  currentTime: number;
  isPlaying: boolean;
  onTogglePlay: () => void;
  onSeek: (time: number) => void;
  activeShotId: string;
  onSelectShot: (shotId: string) => void;
  onUpdateProject?: (updated: Director2DProject) => void;
  selectedActorId?: string | null;
  onSelectActor?: (actorId: string) => void;
}

export const Timeline2DScrubber: React.FC<Timeline2DScrubberProps> = ({
  project,
  currentTime,
  isPlaying,
  onTogglePlay,
  onSeek,
  activeShotId,
  onSelectShot,
  onUpdateProject,
  selectedActorId,
  onSelectActor,
}) => {
  const [isExpanded, setIsExpanded] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  // Compute total duration and shot boundaries
  const totalDuration = project.shots.reduce((sum, s) => sum + s.durationSeconds, 0);
  let accumulatedTime = 0;
  const shotTimeline = project.shots.map((shot) => {
    const start = accumulatedTime;
    const end = accumulatedTime + shot.durationSeconds;
    accumulatedTime = end;
    return { shot, start, end };
  });

  // Modal States
  const [actionModal, setActionModal] = useState<{
    isOpen: boolean;
    actorId: string;
    targetShotId: string;
    targetTime: number;
    currentPose: ActionPoseType;
  } | null>(null);

  const [dialogueModal, setDialogueModal] = useState<{
    isOpen: boolean;
    targetShotId: string;
    targetTime: number;
    text: string;
    speakerId: string;
  } | null>(null);

  const [propModal, setPropModal] = useState<{
    isOpen: boolean;
    propId: string;
    targetShotId: string;
    targetTime: number;
    currentStage: PropGrowthStage;
  } | null>(null);

  const [shotDurationModal, setShotDurationModal] = useState<{
    isOpen: boolean;
    shotId: string;
    duration: number;
    title: string;
  } | null>(null);

  // Seek time calculation from mouse event on tracks container
  const seekFromClientX = (clientX: number) => {
    if (!containerRef.current || totalDuration <= 0) return;
    const rect = containerRef.current.getBoundingClientRect();
    const trackLeft = rect.left + 140; // 140px header offset
    const trackWidth = rect.width - 140;
    if (trackWidth <= 0) return;
    const clickX = clientX - trackLeft;
    const pct = Math.max(0, Math.min(1, clickX / trackWidth));
    onSeek(pct * totalDuration);
  };

  const handleTrackClick = (e: React.MouseEvent<HTMLDivElement>) => {
    seekFromClientX(e.clientX);
  };

  // Playhead Needle Drag & Hold Scrubbing Handler
  const handleNeedleMouseDown = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();

    const handleMouseMove = (moveEvent: MouseEvent) => {
      seekFromClientX(moveEvent.clientX);
    };

    const handleMouseUp = () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('mouseup', handleMouseUp);
    };

    window.addEventListener('mousemove', handleMouseMove);
    window.addEventListener('mouseup', handleMouseUp);
  };

  // 1. Apply Action
  const handleApplyAction = (pose: ActionPoseType) => {
    if (!actionModal || !onUpdateProject) return;
    const { actorId, targetShotId } = actionModal;
    const updatedShots = project.shots.map((s) => {
      if (s.id === targetShotId) {
        const curActor = s.actors[actorId] || {
          actorId,
          worldFacingAngle: 0,
          positionStart: [0, 50],
          positionEnd: [0, 50],
          scale: 1.6,
          zIndex: 10,
          actionPose: 'idle_breathe' as ActionPoseType,
        };
        return { ...s, actors: { ...s.actors, [actorId]: { ...curActor, actionPose: pose } } };
      }
      return s;
    });
    onUpdateProject({ ...project, shots: updatedShots });
    setActionModal(null);
  };

  // 2. Apply Dialogue
  const handleApplyDialogue = (dialogueText: string, speakerId: string) => {
    if (!dialogueModal || !onUpdateProject) return;
    const { targetShotId } = dialogueModal;
    const updatedShots = project.shots.map((s) =>
      s.id === targetShotId
        ? { ...s, dialogueText, speakerActorId: speakerId || s.speakerActorId || project.actors[0]?.id }
        : s
    );
    onUpdateProject({ ...project, shots: updatedShots });
    setDialogueModal(null);
  };

  // 3. Apply Prop Growth
  const handleApplyPropGrowth = (stage: PropGrowthStage) => {
    if (!propModal || !onUpdateProject) return;
    const { propId, targetShotId } = propModal;
    const baseProp = project.props?.find((p) => p.id === propId);
    if (!baseProp) return;

    const updatedShots = project.shots.map((s) => {
      if (s.id === targetShotId) {
        const curProps = s.props || {};
        const existing = curProps[propId] || baseProp;
        return { ...s, props: { ...curProps, [propId]: { ...existing, growthStage: stage } } };
      }
      return s;
    });
    onUpdateProject({ ...project, shots: updatedShots });
    setPropModal(null);
  };

  // 4. Add New Shot
  const handleAddNewShot = (duration: number = 3.0) => {
    if (!onUpdateProject) return;
    const lastShot = project.shots[project.shots.length - 1];
    const newIndex = project.shots.length + 1;
    const newShot: MultiAngleDirectorShot = {
      id: `shot_${Date.now().toString().slice(-6)}`,
      title: `Shot ${newIndex} (${duration}s)`,
      durationSeconds: duration,
      camera: lastShot ? { ...lastShot.camera } : { angleStart: 0, angleEnd: 0, zoomStart: 1.1, zoomEnd: 1.2, panStart: [0, 0], panEnd: [0, 0] },
      actors: lastShot
        ? JSON.parse(JSON.stringify(lastShot.actors))
        : {
            [project.actors[0]?.id || 'char_1']: {
              actorId: project.actors[0]?.id || 'char_1',
              worldFacingAngle: 0,
              positionStart: [-100, 40],
              positionEnd: [-100, 40],
              scale: 1.6,
              zIndex: 10,
              actionPose: 'talk_dialogue',
            },
          },
      speakerActorId: project.actors[0]?.id,
      dialogueText: '',
    };
    onUpdateProject({ ...project, shots: [...project.shots, newShot] });
    onSelectShot(newShot.id);
  };

  // 5. Update Duration
  const handleUpdateDuration = (shotId: string, duration: number) => {
    if (!onUpdateProject) return;
    const dur = Math.max(0.5, Math.min(60, duration));
    const updated = project.shots.map((s) => (s.id === shotId ? { ...s, durationSeconds: dur } : s));
    onUpdateProject({ ...project, shots: updated });
    setShotDurationModal(null);
  };

  const activeShotObj = project.shots.find((s) => s.id === activeShotId) || project.shots[0];

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 6,
        background: 'linear-gradient(180deg, rgba(15, 23, 42, 0.98) 0%, rgba(9, 13, 22, 0.99) 100%)',
        border: '1px solid rgba(56, 189, 248, 0.3)',
        borderRadius: 10,
        padding: '8px 12px',
        boxShadow: '0 10px 35px rgba(0, 0, 0, 0.8), 0 0 25px rgba(56, 189, 248, 0.08)',
        position: 'relative',
        transition: 'all 0.25s ease',
      }}
    >
      {/* 1. Header Toolbar */}
      <TimelineHeaderToolbar
        project={project}
        currentTime={currentTime}
        totalDuration={totalDuration}
        isPlaying={isPlaying}
        activeShotObj={activeShotObj}
        selectedActorId={selectedActorId}
        isExpanded={isExpanded}
        onTogglePlay={onTogglePlay}
        onSeek={onSeek}
        onToggleExpand={() => setIsExpanded(!isExpanded)}
        onOpenActionModal={() => {
          if (!selectedActorId) return;
          const { shot } = findShotAtTime(project, currentTime, totalDuration);
          const curPose = shot.actors[selectedActorId]?.actionPose || 'idle_breathe';
          setActionModal({ isOpen: true, actorId: selectedActorId, targetShotId: shot.id, targetTime: currentTime, currentPose: curPose });
        }}
        onOpenDialogueModal={() => {
          const { shot } = findShotAtTime(project, currentTime, totalDuration);
          setDialogueModal({
            isOpen: true,
            targetShotId: shot.id,
            targetTime: currentTime,
            text: shot.dialogueText || '',
            speakerId: shot.speakerActorId || selectedActorId || project.actors[0]?.id,
          });
        }}
        onOpenDurationModal={() => {
          if (activeShotObj) {
            setShotDurationModal({ isOpen: true, shotId: activeShotObj.id, duration: activeShotObj.durationSeconds, title: activeShotObj.title });
          }
        }}
        onAddNewShot={handleAddNewShot}
      />

      {/* 2. Scrollable Multi-Track Lanes Container */}
      <div
        ref={containerRef}
        style={{
          display: 'flex',
          flexDirection: 'column',
          gap: 3,
          background: 'rgba(2, 6, 23, 0.94)',
          borderRadius: 8,
          border: '1px solid rgba(255, 255, 255, 0.12)',
          padding: '4px 6px',
          position: 'relative',
          maxHeight: isExpanded ? 360 : 175,
          overflowY: 'auto',
          overflowX: 'hidden',
          transition: 'max-height 0.25s ease',
        }}
      >
        {/* Ruler with sub-second precision and drag seeking */}
        <TimelineTrackRuler totalDuration={totalDuration} onSeek={onSeek} />

        {/* Actor Tracks */}
        {project.actors.map((actor) => (
          <TimelineActorTrack
            key={actor.id}
            actor={actor}
            selectedActorId={selectedActorId}
            currentTime={currentTime}
            totalDuration={totalDuration}
            shotTimeline={shotTimeline}
            onSelectActor={onSelectActor}
            onSelectShot={onSelectShot}
            onTrackClick={handleTrackClick}
            onOpenActionModal={(actorId, shotId, time, currentPose) =>
              setActionModal({ isOpen: true, actorId, targetShotId: shotId, targetTime: time, currentPose })
            }
          />
        ))}

        {/* Dialogue Track */}
        <TimelineDialogueTrack
          actors={project.actors}
          currentTime={currentTime}
          totalDuration={totalDuration}
          shotTimeline={shotTimeline}
          onSelectShot={onSelectShot}
          onTrackClick={handleTrackClick}
          onOpenDialogueModal={(shotId, time, text, speakerId) =>
            setDialogueModal({ isOpen: true, targetShotId: shotId, targetTime: time, text, speakerId })
          }
        />

        {/* Props & Flora Track */}
        <TimelinePropTrack
          props={project.props}
          currentTime={currentTime}
          totalDuration={totalDuration}
          shotTimeline={shotTimeline}
          onSelectShot={onSelectShot}
          onTrackClick={handleTrackClick}
          onOpenPropModal={(propId, shotId, time, currentStage) =>
            setPropModal({ isOpen: true, propId, targetShotId: shotId, targetTime: time, currentStage })
          }
        />

        {/* Camera Track */}
        <TimelineCameraTrack
          activeShotId={activeShotId}
          totalDuration={totalDuration}
          shotTimeline={shotTimeline}
          onSelectShot={onSelectShot}
          onSeek={onSeek}
          onTrackClick={handleTrackClick}
        />

        {/* ─── DRAGGABLE GLOWING PLAYHEAD NEEDLE ──────────────────────── */}
        {totalDuration > 0 && (
          <div
            onMouseDown={handleNeedleMouseDown}
            style={{
              position: 'absolute',
              top: 0,
              bottom: 0,
              left: `calc(140px + (100% - 140px) * ${currentTime / totalDuration})`,
              width: 14,
              transform: 'translateX(-50%)',
              cursor: 'ew-resize',
              zIndex: 40,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
            }}
            title={`Con trỏ phát: ${currentTime.toFixed(2)}s. Nhấn giữ và kéo để tua thời gian.`}
          >
            {/* Top Diamond Grab Handle */}
            <div
              style={{
                width: 14,
                height: 14,
                background: 'linear-gradient(135deg, #38bdf8, #0284c7)',
                transform: 'rotate(45deg)',
                boxShadow: '0 0 10px #38bdf8, 0 0 16px rgba(56, 189, 248, 0.8)',
                border: '1.5px solid #fff',
                cursor: 'ew-resize',
                marginTop: 2,
              }}
            />

            {/* Glowing Vertical Needle Line */}
            <div
              style={{
                width: 2.5,
                flex: 1,
                background: '#38bdf8',
                boxShadow: '0 0 10px #38bdf8, 0 0 18px rgba(56, 189, 248, 0.95)',
                pointerEvents: 'none',
              }}
            />
          </div>
        )}
      </div>

      {/* 3. Range Slider */}
      <input
        type="range"
        min="0"
        max={totalDuration || 1}
        step="0.02"
        value={currentTime}
        onChange={(e) => onSeek(parseFloat(e.target.value))}
        style={{ width: '100%', height: 5, accentColor: '#38bdf8', cursor: 'pointer', margin: 0 }}
      />

      {/* 4. Modals */}
      {actionModal && (
        <TimelineActionModal
          modalState={actionModal}
          project={project}
          onClose={() => setActionModal(null)}
          onApplyAction={handleApplyAction}
        />
      )}

      {dialogueModal && (
        <TimelineDialogueModal
          modalState={dialogueModal}
          project={project}
          onClose={() => setDialogueModal(null)}
          onChangeText={(text) => setDialogueModal({ ...dialogueModal, text })}
          onChangeSpeaker={(speakerId) => setDialogueModal({ ...dialogueModal, speakerId })}
          onApplyDialogue={handleApplyDialogue}
        />
      )}

      {propModal && (
        <TimelinePropGrowthModal
          modalState={propModal}
          project={project}
          onClose={() => setPropModal(null)}
          onApplyPropGrowth={handleApplyPropGrowth}
        />
      )}

      {shotDurationModal && (
        <TimelineShotDurationModal
          modalState={shotDurationModal}
          onClose={() => setShotDurationModal(null)}
          onChangeDuration={(dur) => setShotDurationModal({ ...shotDurationModal, duration: dur })}
          onApplyDuration={handleUpdateDuration}
        />
      )}
    </div>
  );
};
