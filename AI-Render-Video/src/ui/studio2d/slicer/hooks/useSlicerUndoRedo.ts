import { useState, useCallback } from 'react';
import { Character2DAssembly } from '../../../../types/scene2d';

export interface SlicerHistorySnapshot {
  timestamp: number;
  label?: string;
  userUploadedImageUrl: string | null;
  selectedCatId: string;
  keyColorType: 'chroma_green' | 'pure_white' | 'custom';
  keyColorHex: string;
  isolationMode: 'all' | 'outer_only';
  tolerance: number;
  feather: number;
  shadowRetention: number;
  strokeWidth: number;
  strokeColorHex: string;
  bgCleanupSubTab: 'chroma' | 'despeckle' | 'ai_matting';
  cleanupMode: 'all' | 'defringe' | 'smooth' | 'despeckle';
  fringeColorType: 'chroma_green' | 'pure_white' | 'pure_black' | 'custom';
  fringeColorHex: string;
  defringeStrength: number;
  edgeChoke: number;
  edgeSmooth: number;
  smoothColorType?: 'black' | 'white' | 'auto' | 'custom';
  smoothColorHex?: string;
  despeckleSize: number;
  whiteSpeckleSensitivity: number;
  keepLargestIslandOnly: boolean;
  paddingInset: number;
  colDividers: number[];
  rowDividers: number[];
  slicedResults: [string, string][];
  previewDisplayMode: 'transparent' | 'original';
  hasExplicitlySliced: boolean;
  currentAssembly: Character2DAssembly;
}

export interface UseSlicerUndoRedoProps {
  getCurrentSnapshot: (label?: string) => SlicerHistorySnapshot;
  applySnapshot: (snap: SlicerHistorySnapshot) => void;
}

export function useSlicerUndoRedo({ getCurrentSnapshot, applySnapshot }: UseSlicerUndoRedoProps) {
  const [undoStack, setUndoStack] = useState<SlicerHistorySnapshot[]>([]);
  const [redoStack, setRedoStack] = useState<SlicerHistorySnapshot[]>([]);
  const [historyToast, setHistoryToast] = useState<{ message: string; type: 'undo' | 'redo' } | null>(null);

  const showToast = useCallback((message: string, type: 'undo' | 'redo') => {
    setHistoryToast({ message, type });
    setTimeout(() => setHistoryToast(null), 2500);
  }, []);

  const pushUndoState = useCallback(
    (label?: string) => {
      const snap = getCurrentSnapshot(label);
      setUndoStack((prev) => {
        const next = [...prev, snap];
        if (next.length > 30) next.shift();
        return next;
      });
      setRedoStack([]);
    },
    [getCurrentSnapshot]
  );

  const handleUndo = useCallback(() => {
    if (undoStack.length === 0) return;
    const currentSnap = getCurrentSnapshot('Current');
    const targetSnap = undoStack[undoStack.length - 1];

    setRedoStack((r) => [...r, currentSnap]);
    setUndoStack((u) => u.slice(0, u.length - 1));

    applySnapshot(targetSnap);
    showToast(`↩️ Đã hoàn tác: ${targetSnap.label || 'Thay đổi trước'} (Ctrl+Z)`, 'undo');
  }, [undoStack, getCurrentSnapshot, applySnapshot, showToast]);

  const handleRedo = useCallback(() => {
    if (redoStack.length === 0) return;
    const currentSnap = getCurrentSnapshot('Current');
    const targetSnap = redoStack[redoStack.length - 1];

    setUndoStack((u) => [...u, currentSnap]);
    setRedoStack((r) => r.slice(0, r.length - 1));

    applySnapshot(targetSnap);
    showToast(`↪️ Đã làm lại: ${targetSnap.label || 'Thao tác tiếp theo'} (Ctrl+Y)`, 'redo');
  }, [redoStack, getCurrentSnapshot, applySnapshot, showToast]);

  return {
    undoStack,
    redoStack,
    historyToast,
    showToast,
    pushUndoState,
    handleUndo,
    handleRedo,
  };
}
