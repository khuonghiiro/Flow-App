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
import { TimelinePropTrack } from './timeline/TimelinePropTrack';
import { TimelineCameraTrack } from './timeline/TimelineCameraTrack';
import { TimelineContextMenu } from './timeline/TimelineContextMenu';
import { TimelineActionModal } from './timeline/TimelineActionModal';
import { TimelineDialogueModal } from './timeline/TimelineDialogueModal';
import { TimelinePropGrowthModal } from './timeline/TimelinePropGrowthModal';
import { TimelineTotalDurationModal } from './timeline/TimelineTotalDurationModal';
import { TimelineVisibilityModal } from './timeline/TimelineVisibilityModal';

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
  selectedPropId?: string | null;
  onSelectProp?: (propId: string) => void;
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
  selectedPropId,
  onSelectProp,
}) => {
  const [isExpanded, setIsExpanded] = useState(false);
  const [hoverTime, setHoverTime] = useState<number | null>(null);
  const [hoverX, setHoverX] = useState<number | null>(null);
  const [isDurationModalOpen, setIsDurationModalOpen] = useState(false);
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

  // Modal & Context Menu States
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

  const [visibilityModal, setVisibilityModal] = useState<{
    isOpen: boolean;
    type: 'actor' | 'prop';
    id: string;
    title: string;
    from: number;
    to: number;
  } | null>(null);

  const [contextMenu, setContextMenu] = useState<{
    x: number;
    y: number;
    actorId: string;
    actorName: string;
    shotId: string;
    time: number;
  } | null>(null);

  // Mouse move over tracks container
  const handleContainerMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!containerRef.current || totalDuration <= 0) return;
    const rect = containerRef.current.getBoundingClientRect();
    const trackLeft = rect.left + 140;
    const trackWidth = rect.width - 140;
    if (trackWidth <= 0) return;

    const clickX = e.clientX - trackLeft;
    if (clickX < 0) {
      setHoverTime(null);
      setHoverX(null);
      return;
    }

    const pct = Math.max(0, Math.min(1, clickX / trackWidth));
    setHoverTime(pct * totalDuration);
    setHoverX(e.clientX - rect.left);
  };

  const handleContainerMouseLeave = () => {
    setHoverTime(null);
    setHoverX(null);
  };

  const handleTrackClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!containerRef.current || totalDuration <= 0) return;
    const rect = containerRef.current.getBoundingClientRect();
    const trackLeft = rect.left + 140;
    const trackWidth = rect.width - 140;
    if (trackWidth <= 0) return;
    const clickX = e.clientX - trackLeft;
    const pct = Math.max(0, Math.min(1, clickX / trackWidth));
    onSeek(pct * totalDuration);
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

  // 4. Apply Visibility Time Window
  const handleApplyVisibility = (from: number, to: number) => {
    if (!visibilityModal || !onUpdateProject) return;
    const { type, id } = visibilityModal;

    if (type === 'actor') {
      const updatedShots = project.shots.map((s) => {
        const curActor = s.actors[id] || {
          actorId: id,
          worldFacingAngle: 0,
          positionStart: [0, 50],
          positionEnd: [0, 50],
          scale: 1.6,
          zIndex: 10,
          actionPose: 'idle_breathe' as ActionPoseType,
        };
        return { ...s, actors: { ...s.actors, [id]: { ...curActor, visibleFrom: from, visibleTo: to } } };
      });
      onUpdateProject({ ...project, shots: updatedShots });
    } else if (type === 'prop') {
      const updatedProps = (project.props || []).map((p) =>
        p.id === id ? { ...p, visibleFrom: from, visibleTo: to } : p
      );
      onUpdateProject({ ...project, props: updatedProps });
    }
    setVisibilityModal(null);
  };

  // 5. Update Total Duration
  const handleApplyTotalDuration = (newTotal: number) => {
    if (!onUpdateProject || project.shots.length === 0) return;
    const ratio = newTotal / Math.max(1, totalDuration);
    const updatedShots = project.shots.map((s) => ({
      ...s,
      durationSeconds: Math.max(0.5, Number((s.durationSeconds * ratio).toFixed(2))),
    }));
    onUpdateProject({ ...project, shots: updatedShots });
    setIsDurationModalOpen(false);
  };

  // 6. Quick Extend Duration
  const handleExtendDuration = (secondsToAdd: number) => {
    if (!onUpdateProject || project.shots.length === 0) return;
    const updated = project.shots.map((s, idx) =>
      idx === project.shots.length - 1 ? { ...s, durationSeconds: s.durationSeconds + secondsToAdd } : s
    );
    onUpdateProject({ ...project, shots: updated });
  };

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
        onOpenTotalDurationModal={() => setIsDurationModalOpen(true)}
        onExtendDuration={handleExtendDuration}
      />

      {/* 2. Scrollable Multi-Track Lanes Container */}
      <div
        ref={containerRef}
        onMouseMove={handleContainerMouseMove}
        onMouseLeave={handleContainerMouseLeave}
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
        {/* Ruler */}
        <TimelineTrackRuler currentTime={currentTime} totalDuration={totalDuration} onSeek={onSeek} />

        {/* Actor Tracks */}
        {project.actors.map((actor) => (
          <TimelineActorTrack
            key={actor.id}
            actor={actor}
            selectedActorId={selectedActorId}
            activeShotId={activeShotId}
            currentTime={currentTime}
            totalDuration={totalDuration}
            shotTimeline={shotTimeline}
            onSelectActor={(id) => {
              onSelectActor?.(id);
              onSelectProp?.('');
            }}
            onSelectShot={onSelectShot}
            onTrackClick={handleTrackClick}
            onOpenActionModal={(actorId, shotId, time, currentPose) =>
              setActionModal({ isOpen: true, actorId, targetShotId: shotId, targetTime: time, currentPose })
            }
            onOpenVisibilityModal={(actorId, actorName, from, to) =>
              setVisibilityModal({ isOpen: true, type: 'actor', id: actorId, title: actorName, from, to })
            }
            onOpenContextMenu={(e, actorId, actorName, shotId, time) =>
              setContextMenu({ x: e.clientX, y: e.clientY, actorId, actorName, shotId, time })
            }
          />
        ))}

        {/* Props & Flora Track */}
        <TimelinePropTrack
          props={project.props}
          selectedPropId={selectedPropId}
          currentTime={currentTime}
          totalDuration={totalDuration}
          shotTimeline={shotTimeline}
          onSelectShot={onSelectShot}
          onSelectProp={(id) => {
            onSelectProp?.(id);
            onSelectActor?.('');
          }}
          onTrackClick={handleTrackClick}
          onOpenPropModal={(propId, shotId, time, currentStage) =>
            setPropModal({ isOpen: true, propId, targetShotId: shotId, targetTime: time, currentStage })
          }
          onOpenVisibilityModal={(propId, propName, from, to) =>
            setVisibilityModal({ isOpen: true, type: 'prop', id: propId, title: propName, from, to })
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

        {/* ─── GHOST HOVER GUIDE LINE (FOLLOWS MOUSE) ────────────────────── */}
        {hoverTime !== null && hoverX !== null && Math.abs(hoverTime - currentTime) > 0.05 && (
          <div
            style={{
              position: 'absolute',
              top: 0,
              bottom: 0,
              left: hoverX,
              width: 1,
              background: 'rgba(244, 63, 94, 0.45)',
              pointerEvents: 'none',
              zIndex: 30,
            }}
          >
            <div
              style={{
                position: 'absolute',
                top: 1,
                left: 4,
                background: 'rgba(15, 23, 42, 0.95)',
                color: '#fda4af',
                padding: '1px 5px',
                borderRadius: 3,
                border: '1px solid rgba(244, 63, 94, 0.4)',
                fontSize: 8.5,
                fontWeight: 700,
                fontFamily: 'monospace',
                whiteSpace: 'nowrap',
              }}
            >
              {hoverTime.toFixed(2)}s
            </div>
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

      {/* 4. Right-Click Context Menu */}
      {contextMenu && (
        <TimelineContextMenu
          menuState={contextMenu}
          onClose={() => setContextMenu(null)}
          onOpenActionModal={() => {
            const shot = project.shots.find((s) => s.id === contextMenu.shotId);
            const curPose = shot?.actors[contextMenu.actorId]?.actionPose || 'idle_breathe';
            setActionModal({
              isOpen: true,
              actorId: contextMenu.actorId,
              targetShotId: contextMenu.shotId,
              targetTime: contextMenu.time,
              currentPose: curPose,
            });
          }}
          onOpenDialogueModal={() => {
            const shot = project.shots.find((s) => s.id === contextMenu.shotId);
            setDialogueModal({
              isOpen: true,
              targetShotId: contextMenu.shotId,
              targetTime: contextMenu.time,
              text: shot?.dialogueText || '',
              speakerId: shot?.speakerActorId || contextMenu.actorId,
            });
          }}
          onOpenDurationModal={() => setIsDurationModalOpen(true)}
        />
      )}

      {/* 5. Modals */}
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

      {visibilityModal && (
        <TimelineVisibilityModal
          isOpen={visibilityModal.isOpen}
          title={visibilityModal.title}
          totalDuration={totalDuration}
          currentTime={currentTime}
          initialFrom={visibilityModal.from}
          initialTo={visibilityModal.to}
          onClose={() => setVisibilityModal(null)}
          onApply={handleApplyVisibility}
        />
      )}

      {isDurationModalOpen && (
        <TimelineTotalDurationModal
          isOpen={isDurationModalOpen}
          currentDuration={totalDuration}
          onClose={() => setIsDurationModalOpen(false)}
          onApply={handleApplyTotalDuration}
        />
      )}
    </div>
  );
};
