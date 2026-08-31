// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { useState, useCallback, useEffect } from 'react';

export interface UseSlicerEyedropperManagerProps {
  hasExplicitlySliced: boolean;
  handleAutoSliceAndAssemble: (overrides?: any) => void;
  setSmoothColorType: (type: 'black' | 'white' | 'auto' | 'custom') => void;
  setSmoothColorHex: (hex: string) => void;
  setFringeColorType: (type: 'chroma_green' | 'pure_white' | 'pure_black' | 'custom') => void;
  setFringeColorHex: (hex: string) => void;
  setKeyColorType: (type: 'chroma_green' | 'pure_white' | 'custom') => void;
  setKeyColorHex: (hex: string) => void;
  showToast?: (msg: string, type?: 'undo' | 'redo') => void;
}

export function useSlicerEyedropperManager({
  hasExplicitlySliced,
  handleAutoSliceAndAssemble,
  setSmoothColorType,
  setSmoothColorHex,
  setFringeColorType,
  setFringeColorHex,
  setKeyColorType,
  setKeyColorHex,
  showToast,
}: UseSlicerEyedropperManagerProps) {
  const [isEyedropperActive, setIsEyedropperActive] = useState<boolean>(false);
  const [eyedropperTarget, setEyedropperTarget] = useState<'chroma' | 'fringe' | 'smooth'>('chroma');
  const [eyedropperHoverColor, setEyedropperHoverColor] = useState<{
    hex: string;
    r: number;
    g: number;
    b: number;
    x: number;
    y: number;
  } | null>(null);

  // Keyboard shortcut: Escape cancels eyedropper mode
  useEffect(() => {
    if (!isEyedropperActive) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' || e.code === 'Escape') {
        setIsEyedropperActive(false);
        setEyedropperTarget('chroma');
        setEyedropperHoverColor(null);
        showToast?.('Đã huỷ chế độ hút màu', 'undo');
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [isEyedropperActive, showToast]);

  const handlePickColor = useCallback(
    (hex: string) => {
      if (eyedropperTarget === 'smooth') {
        setSmoothColorType('custom');
        setSmoothColorHex(hex);
        if (hasExplicitlySliced) handleAutoSliceAndAssemble({ smoothColorType: 'custom', smoothColorHex: hex });
        showToast?.(`✓ Đã hút màu làm mịn: ${hex.toUpperCase()}`, 'redo');
      } else if (eyedropperTarget === 'fringe') {
        setFringeColorType('custom');
        setFringeColorHex(hex);
        if (hasExplicitlySliced) handleAutoSliceAndAssemble({ fringeColorType: 'custom', fringeColorHex: hex });
        showToast?.(`✓ Đã hút màu viền rác: ${hex.toUpperCase()}`, 'redo');
      } else {
        setKeyColorType('custom');
        setKeyColorHex(hex);
        if (hasExplicitlySliced) handleAutoSliceAndAssemble({ keyColorType: 'custom', keyColorHex: hex });
        showToast?.(`✓ Đã hút màu nền: ${hex.toUpperCase()}`, 'redo');
      }
      setIsEyedropperActive(false);
      setEyedropperTarget('chroma');
      setEyedropperHoverColor(null);
    },
    [
      eyedropperTarget,
      setSmoothColorType,
      setSmoothColorHex,
      hasExplicitlySliced,
      handleAutoSliceAndAssemble,
      setFringeColorType,
      setFringeColorHex,
      setKeyColorType,
      setKeyColorHex,
      showToast,
    ]
  );

  const handleHoverColor = useCallback(
    (data: { hex: string; r: number; g: number; b: number; x: number; y: number } | null) => {
      setEyedropperHoverColor(data);
    },
    []
  );

  const handleToggleEyedropper = useCallback(
    (target: 'chroma' | 'fringe' | 'smooth' = 'chroma') => {
      if (isEyedropperActive && eyedropperTarget === target) {
        setIsEyedropperActive(false);
        setEyedropperTarget('chroma');
        setEyedropperHoverColor(null);
      } else {
        setIsEyedropperActive(true);
        setEyedropperTarget(target);
      }
    },
    [isEyedropperActive, eyedropperTarget]
  );

  return {
    isEyedropperActive,
    setIsEyedropperActive,
    eyedropperTarget,
    setEyedropperTarget,
    eyedropperHoverColor,
    setEyedropperHoverColor,
    handlePickColor,
    handleHoverColor,
    handleToggleEyedropper,
  };
}
