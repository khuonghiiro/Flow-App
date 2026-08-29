// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { useState, useCallback, useEffect, useRef } from 'react';
import { AnimationSliceFrame } from '../../../../types/animation_slicer';

interface HistorySnapshot {
  timestamp: number;
  label: string;
  frames: AnimationSliceFrame[];
  frameOrder: number[];
}

const MAX_HISTORY = 30;

export const useAnimationHistory = (
  initialFrames: AnimationSliceFrame[],
  initialOrder: number[],
  onRestore: (frames: AnimationSliceFrame[], order: number[], label: string) => void
) => {
  const [past, setPast] = useState<HistorySnapshot[]>([]);
  const [future, setFuture] = useState<HistorySnapshot[]>([]);
  const isInternalUpdateRef = useRef<boolean>(false);

  // Push new snapshot to history
  const pushSnapshot = useCallback(
    (frames: AnimationSliceFrame[], frameOrder: number[], label: string = 'Chỉnh sửa') => {
      if (isInternalUpdateRef.current) {
        isInternalUpdateRef.current = false;
        return;
      }
      setPast((prev) => {
        const snapshot: HistorySnapshot = {
          timestamp: Date.now(),
          label,
          frames: JSON.parse(JSON.stringify(frames)),
          frameOrder: [...frameOrder],
        };
        const next = [...prev, snapshot];
        if (next.length > MAX_HISTORY) next.shift();
        return next;
      });
      setFuture([]);
    },
    []
  );

  // Undo (Ctrl+Z)
  const undo = useCallback(
    (currentFrames: AnimationSliceFrame[], currentOrder: number[]): boolean => {
      if (past.length === 0) return false;
      const previous = past[past.length - 1];
      const newPast = past.slice(0, past.length - 1);

      // Save current state into future
      const currentSnapshot: HistorySnapshot = {
        timestamp: Date.now(),
        label: 'Hiện tại',
        frames: JSON.parse(JSON.stringify(currentFrames)),
        frameOrder: [...currentOrder],
      };
      setFuture((prev) => [currentSnapshot, ...prev]);
      setPast(newPast);

      isInternalUpdateRef.current = true;
      onRestore(previous.frames, previous.frameOrder, `Hoàn tác: ${previous.label}`);
      return true;
    },
    [past, onRestore]
  );

  // Redo (Ctrl+Y / Ctrl+Shift+Z)
  const redo = useCallback(
    (currentFrames: AnimationSliceFrame[], currentOrder: number[]): boolean => {
      if (future.length === 0) return false;
      const next = future[0];
      const newFuture = future.slice(1);

      const currentSnapshot: HistorySnapshot = {
        timestamp: Date.now(),
        label: 'Hiện tại',
        frames: JSON.parse(JSON.stringify(currentFrames)),
        frameOrder: [...currentOrder],
      };
      setPast((prev) => [...prev, currentSnapshot]);
      setFuture(newFuture);

      isInternalUpdateRef.current = true;
      onRestore(next.frames, next.frameOrder, `Làm lại: ${next.label}`);
      return true;
    },
    [future, onRestore]
  );

  return {
    canUndo: past.length > 0,
    canRedo: future.length > 0,
    undo,
    redo,
    pushSnapshot,
  };
};
