// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { useState, useCallback } from 'react';

export interface UseSlicerEyedropperManagerProps {
  hasExplicitlySliced: boolean;
  handleAutoSliceAndAssemble: (overrides?: any) => void;
  setSmoothColorType: (type: 'matte' | 'custom') => void;
  setSmoothColorHex: (hex: string) => void;
  setFringeColorType: (type: 'auto' | 'custom') => void;
  setFringeColorHex: (hex: string) => void;
  setKeyColorType: (type: 'green' | 'blue' | 'white' | 'black' | 'custom') => void;
  setKeyColorHex: (hex: string) => void;
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
}: UseSlicerEyedropperManagerProps) {
  const [isEyedropperActive, setIsEyedropperActive] = useState<boolean>(false);
  const [eyedropperTarget, setEyedropperTarget] = useState<'chroma' | 'fringe' | 'smooth' | null>(null);
  const [eyedropperHoverColor, setEyedropperHoverColor] = useState<{
    hex: string;
    r: number;
    g: number;
    b: number;
    x: number;
    y: number;
  } | null>(null);

  const handlePickColor = useCallback(
    (hex: string) => {
      if (eyedropperTarget === 'smooth') {
        setSmoothColorType('custom');
        setSmoothColorHex(hex);
        if (hasExplicitlySliced) handleAutoSliceAndAssemble({ smoothColorType: 'custom', smoothColorHex: hex });
      } else if (eyedropperTarget === 'fringe') {
        setFringeColorType('custom');
        setFringeColorHex(hex);
        if (hasExplicitlySliced) handleAutoSliceAndAssemble({ fringeColorType: 'custom', fringeColorHex: hex });
      } else {
        setKeyColorType('custom');
        setKeyColorHex(hex);
        if (hasExplicitlySliced) handleAutoSliceAndAssemble({ keyColorType: 'custom', keyColorHex: hex });
      }
      setIsEyedropperActive(false);
      setEyedropperTarget(null);
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
    ]
  );

  const handleHoverColor = useCallback(
    (data: { hex: string; r: number; g: number; b: number; x: number; y: number } | null) => {
      setEyedropperHoverColor(data);
    },
    []
  );

  const handleToggleEyedropper = useCallback(
    (target: 'chroma' | 'fringe' | 'smooth') => {
      if (isEyedropperActive && eyedropperTarget === target) {
        setIsEyedropperActive(false);
        setEyedropperTarget(null);
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
