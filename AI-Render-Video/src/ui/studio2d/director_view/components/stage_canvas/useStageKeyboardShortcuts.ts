// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import { useEffect, useRef, useState } from 'react';
import { Director2DProject, MultiAngleDirectorShot } from '../../../../../types/studio2d_director';

interface UseStageKeyboardShortcutsParams {
  project: Director2DProject;
  activeShot: MultiAngleDirectorShot;
  selectedActorId: string | null;
  selectedPropId?: string | null;
  onUpdateActorScale?: (actorId: string, scale: number) => void;
  onUpdateActorRotation?: (actorId: string, rotDeg: number) => void;
  onUpdateActorFacingAngle?: (actorId: string, angleDeg: number, flipX?: boolean) => void;
  onUpdateActorFlipX?: (actorId: string, flipX: boolean) => void;
  onUpdatePropScale?: (propId: string, scale: number) => void;
  onUpdatePropRotation?: (propId: string, rotDeg: number) => void;
  onUpdatePropFlipX?: (propId: string, flipX: boolean) => void;
  onUpdateActorZIndex?: (actorId: string, delta: number) => void;
  onUpdatePropZIndex?: (propId: string, delta: number) => void;
  onUpdateCameraFrame?: (width: number, height: number) => void;
}

export function useStageKeyboardShortcuts({
  project,
  activeShot,
  selectedActorId,
  selectedPropId,
  onUpdateActorScale,
  onUpdateActorRotation,
  onUpdateActorFacingAngle,
  onUpdateActorFlipX,
  onUpdatePropScale,
  onUpdatePropRotation,
  onUpdatePropFlipX,
  onUpdateActorZIndex,
  onUpdatePropZIndex,
  onUpdateCameraFrame,
}: UseStageKeyboardShortcutsParams) {
  const [zToast, setZToast] = useState<{ text: string; time: number } | null>(null);

  // Stable references
  const projectRef = useRef(project);
  projectRef.current = project;
  const activeShotRef = useRef(activeShot);
  activeShotRef.current = activeShot;
  const selectedActorIdRef = useRef(selectedActorId);
  selectedActorIdRef.current = selectedActorId;
  const selectedPropIdRef = useRef(selectedPropId);
  selectedPropIdRef.current = selectedPropId;
  const onUpdateActorScaleRef = useRef(onUpdateActorScale);
  onUpdateActorScaleRef.current = onUpdateActorScale;
  const onUpdateActorRotationRef = useRef(onUpdateActorRotation);
  onUpdateActorRotationRef.current = onUpdateActorRotation;
  const onUpdateActorFacingAngleRef = useRef(onUpdateActorFacingAngle);
  onUpdateActorFacingAngleRef.current = onUpdateActorFacingAngle;
  const onUpdateActorFlipXRef = useRef(onUpdateActorFlipX);
  onUpdateActorFlipXRef.current = onUpdateActorFlipX;
  const onUpdatePropScaleRef = useRef(onUpdatePropScale);
  onUpdatePropScaleRef.current = onUpdatePropScale;
  const onUpdatePropRotationRef = useRef(onUpdatePropRotation);
  onUpdatePropRotationRef.current = onUpdatePropRotation;
  const onUpdatePropFlipXRef = useRef(onUpdatePropFlipX);
  onUpdatePropFlipXRef.current = onUpdatePropFlipX;
  const onUpdateActorZIndexRef = useRef(onUpdateActorZIndex);
  onUpdateActorZIndexRef.current = onUpdateActorZIndex;
  const onUpdatePropZIndexRef = useRef(onUpdatePropZIndex);
  onUpdatePropZIndexRef.current = onUpdatePropZIndex;
  const onUpdateCameraFrameRef = useRef(onUpdateCameraFrame);
  onUpdateCameraFrameRef.current = onUpdateCameraFrame;

  const repeatHoldTimerRef = useRef<number | null>(null);

  useEffect(() => {
    const stepScale = (delta: number) => {
      const curActorId = selectedActorIdRef.current;
      const curPropId = selectedPropIdRef.current;
      const curShot = activeShotRef.current;
      const curProj = projectRef.current;

      if (curActorId && onUpdateActorScaleRef.current && curShot) {
        const curScale = curShot.actors[curActorId]?.scale ?? 1.6;
        const newScale = Math.max(0.2, Math.min(5.0, Number((curScale + delta).toFixed(2))));
        onUpdateActorScaleRef.current(curActorId, newScale);
        setZToast({
          text: `🔍 Tỉ lệ (Scale): ${newScale.toFixed(2)}x ${delta > 0 ? '🔼' : '🔽'}`,
          time: Date.now(),
        });
      } else if (curPropId && onUpdatePropScaleRef.current && curProj) {
        const curProp = curProj.props.find((p) => p.id === curPropId);
        const curScale = curProp?.scale[0] ?? 1.0;
        const newScale = Math.max(0.2, Math.min(5.0, Number((curScale + delta).toFixed(2))));
        onUpdatePropScaleRef.current(curPropId, newScale);
        setZToast({
          text: `🔍 Tỉ lệ (Scale): ${newScale.toFixed(2)}x ${delta > 0 ? '🔼' : '🔽'}`,
          time: Date.now(),
        });
      } else if (onUpdateCameraFrameRef.current && curShot) {
        const curCamW = curShot.camera.frameWidth || 720;
        const deltaW = delta > 0 ? 30 : -30;
        const newW = Math.max(320, Math.min(1800, curCamW + deltaW));
        const newH = Math.round((newW * 9) / 16);
        onUpdateCameraFrameRef.current(newW, newH);
        setZToast({
          text: `📹 Khung Camera 16:9: ${newW}×${newH} ${delta > 0 ? '🔍 Phóng to' : '🔎 Thu nhỏ'}`,
          time: Date.now(),
        });
      }
    };

    const stepRotate = (deltaDeg: number) => {
      const curActorId = selectedActorIdRef.current;
      const curPropId = selectedPropIdRef.current;
      const curShot = activeShotRef.current;
      const curProj = projectRef.current;

      if (curActorId && onUpdateActorRotationRef.current && curShot) {
        const curRot = curShot.actors[curActorId]?.rotation ?? 0;
        let newRot = Math.round(curRot + deltaDeg);
        if (newRot > 180) newRot -= 360;
        if (newRot < -180) newRot += 360;
        onUpdateActorRotationRef.current(curActorId, newRot);
        setZToast({
          text: `🔄 Góc xoay: ${newRot}° ${deltaDeg > 0 ? '↷' : '↶'}`,
          time: Date.now(),
        });
      } else if (curPropId && onUpdatePropRotationRef.current && curProj) {
        const curProp = curProj.props.find((p) => p.id === curPropId);
        const curRot = curProp?.rotation ?? 0;
        let newRot = Math.round(curRot + deltaDeg);
        if (newRot > 180) newRot -= 360;
        if (newRot < -180) newRot += 360;
        onUpdatePropRotationRef.current(curPropId, newRot);
        setZToast({
          text: `🔄 Góc xoay: ${newRot}° ${deltaDeg > 0 ? '↷' : '↶'}`,
          time: Date.now(),
        });
      }
    };

    const setFacingAngle = (angleDeg: number, flipX: boolean, label: string) => {
      const curActorId = selectedActorIdRef.current;
      const curPropId = selectedPropIdRef.current;
      const curProj = projectRef.current;

      if (curActorId && onUpdateActorFacingAngleRef.current) {
        onUpdateActorFacingAngleRef.current(curActorId, angleDeg, flipX);
        setZToast({
          text: flipX ? `🪞 Lật ngược ảnh: ${label} (FlipX)` : `👤 Hướng nhìn ảnh: ${label} (${angleDeg}°)`,
          time: Date.now(),
        });
      } else if (curPropId && onUpdatePropFlipXRef.current && curProj) {
        const curProp = curProj.props.find((p) => p.id === curPropId);
        const newFlip = flipX !== undefined ? flipX : !(curProp?.flipX ?? false);
        onUpdatePropFlipXRef.current(curPropId, newFlip);
        setZToast({
          text: newFlip ? '🪞 Đã lật ngang đạo cụ (FlipX)' : '🌲 Đã khôi phục hướng gốc đạo cụ',
          time: Date.now(),
        });
      }
    };

    const handleKeyDown = (e: KeyboardEvent) => {
      if (['INPUT', 'TEXTAREA', 'SELECT'].includes((e.target as HTMLElement)?.tagName)) return;

      const code = e.code;
      const key = e.key;

      // ─── NUMERIC ANGLE & FLIP SHORTCUTS (0..9 & Shift + 0..9) ─────────
      // 0: Chính diện (Front 0°) / Shift+0: Lật ngược ảnh chính diện (FlipX)
      if (code === 'Digit0' || code === 'Numpad0' || key === '0' || key === ')') {
        e.preventDefault();
        setFacingAngle(0, e.shiftKey, 'Chính diện Front 0°');
        return;
      }

      // 1: Chéo trước phải (Front-Right 45°) / Shift+1: Lật ngược chéo trước trái
      if (code === 'Digit1' || code === 'Numpad1' || key === '1' || key === '!') {
        e.preventDefault();
        setFacingAngle(45, e.shiftKey, e.shiftKey ? 'Lật ngược Chéo trước phải' : 'Chéo trước phải 45°');
        return;
      }

      // 2: Ngang phải (Side-Right 90°) / Shift+2: Lật ngược ngang phải (thành nhìn sang trái)
      if (code === 'Digit2' || code === 'Numpad2' || key === '2' || key === '@') {
        e.preventDefault();
        setFacingAngle(90, e.shiftKey, e.shiftKey ? 'Lật ngược sang trái' : 'Ngang phải 90°');
        return;
      }

      // 3: Chéo sau phải (Back-Right 135°) / Shift+3: Lật ngược chéo sau phải
      if (code === 'Digit3' || code === 'Numpad3' || key === '3' || key === '#') {
        e.preventDefault();
        setFacingAngle(135, e.shiftKey, e.shiftKey ? 'Lật ngược Chéo sau phải' : 'Chéo sau phải 135°');
        return;
      }

      // 4: Ngang trái (Side-Left 270°) / Shift+4: Lật ngược ngang trái (thành nhìn sang phải)
      if (code === 'Digit4' || code === 'Numpad4' || key === '4' || key === '$') {
        e.preventDefault();
        setFacingAngle(270, e.shiftKey, e.shiftKey ? 'Lật ngược sang phải' : 'Ngang trái 270°');
        return;
      }

      // 5: Quay lưng (Back 180°) / Shift+5: Lật ngược ảnh quay lưng
      if (code === 'Digit5' || code === 'Numpad5' || key === '5' || key === '%') {
        e.preventDefault();
        setFacingAngle(180, e.shiftKey, e.shiftKey ? 'Lật ngược Quay lưng' : 'Quay lưng 180°');
        return;
      }

      // 6: Chéo sau trái (Back-Left 225°) / Shift+6: Lật ngược chéo sau trái
      if (code === 'Digit6' || code === 'Numpad6' || key === '6' || key === '^') {
        e.preventDefault();
        setFacingAngle(225, e.shiftKey, e.shiftKey ? 'Lật ngược Chéo sau trái' : 'Chéo sau trái 225°');
        return;
      }

      // 7: Chéo trước trái (Front-Left 315°) / Shift+7: Lật ngược chéo trước trái
      if (code === 'Digit7' || code === 'Numpad7' || key === '7' || key === '&') {
        e.preventDefault();
        setFacingAngle(315, e.shiftKey, e.shiftKey ? 'Lật ngược Chéo trước trái' : 'Chéo trước trái 315°');
        return;
      }

      // 8: Chính diện (Front 0°) / Shift+8: Lật ngược ảnh
      if (code === 'Digit8' || code === 'Numpad8' || key === '8' || key === '*') {
        e.preventDefault();
        setFacingAngle(0, e.shiftKey, 'Chính diện Front 0°');
        return;
      }

      // 9: Quay lưng (Back 180°) / Shift+9: Lật ngược ảnh
      if (code === 'Digit9' || code === 'Numpad9' || key === '9' || key === '(') {
        e.preventDefault();
        setFacingAngle(180, e.shiftKey, 'Quay lưng Back 180°');
        return;
      }

      // Reset Rotation & Flip with 'R'
      if (key.toLowerCase() === 'r') {
        e.preventDefault();
        const curActorId = selectedActorIdRef.current;
        const curPropId = selectedPropIdRef.current;
        if (curActorId) {
          onUpdateActorFacingAngleRef.current?.(curActorId, 0, false);
          onUpdateActorRotationRef.current?.(curActorId, 0);
          setZToast({ text: '🔄 Khôi phục góc chính diện 0° & Xoay 0°', time: Date.now() });
        } else if (curPropId) {
          onUpdatePropFlipXRef.current?.(curPropId, false);
          onUpdatePropRotationRef.current?.(curPropId, 0);
          setZToast({ text: '🔄 Khôi phục góc gốc & Xoay 0°', time: Date.now() });
        }
        return;
      }

      // Rotation Continuous Shortcuts [ / ]
      const isBracketLeft = key === '[' || code === 'BracketLeft';
      const isBracketRight = key === ']' || code === 'BracketRight';
      if (isBracketLeft || isBracketRight) {
        e.preventDefault();
        const delta = isBracketRight ? (e.shiftKey ? 15 : 3) : (e.shiftKey ? -15 : -3);
        if (!repeatHoldTimerRef.current) {
          stepRotate(delta);
          repeatHoldTimerRef.current = window.setInterval(() => {
            stepRotate(delta);
          }, 45);
        }
        return;
      }

      const isPlus = key === '+' || key === '=' || code === 'NumpadAdd' || code === 'Equal';
      const isMinus = key === '-' || key === '_' || code === 'NumpadSubtract' || code === 'Minus';

      if (!isPlus && !isMinus) return;

      if (e.shiftKey) {
        // Continuous Scaling Mode
        e.preventDefault();
        const delta = isPlus ? 0.05 : -0.05;

        if (!repeatHoldTimerRef.current) {
          stepScale(delta);
          repeatHoldTimerRef.current = window.setInterval(() => {
            stepScale(delta);
          }, 45);
        }
      } else {
        // Single Step Z-Index Mode
        const curActorId = selectedActorIdRef.current;
        const curPropId = selectedPropIdRef.current;
        const curShot = activeShotRef.current;
        const curProj = projectRef.current;

        if (isPlus) {
          if (curActorId && onUpdateActorZIndexRef.current && curShot) {
            onUpdateActorZIndexRef.current(curActorId, 1);
            const curZ = (curShot.actors[curActorId]?.zIndex || 10) + 1;
            setZToast({ text: `Lớp hiển thị (Z-Index): ${curZ} ⬆️`, time: Date.now() });
          } else if (curPropId && onUpdatePropZIndexRef.current && curProj) {
            onUpdatePropZIndexRef.current(curPropId, 1);
            const curProp = curProj.props.find((p) => p.id === curPropId);
            const curZ = (curProp?.zIndex || 5) + 1;
            setZToast({ text: `Lớp hiển thị (Z-Index): ${curZ} ⬆️`, time: Date.now() });
          }
        } else if (isMinus) {
          if (curActorId && onUpdateActorZIndexRef.current && curShot) {
            onUpdateActorZIndexRef.current(curActorId, -1);
            const curZ = Math.max(1, (curShot.actors[curActorId]?.zIndex || 10) - 1);
            setZToast({ text: `Lớp hiển thị (Z-Index): ${curZ} ⬇️`, time: Date.now() });
          } else if (curPropId && onUpdatePropZIndexRef.current && curProj) {
            onUpdatePropZIndexRef.current(curPropId, -1);
            const curProp = curProj.props.find((p) => p.id === curPropId);
            const curZ = Math.max(1, (curProp?.zIndex || 5) - 1);
            setZToast({ text: `Lớp hiển thị (Z-Index): ${curZ} ⬇️`, time: Date.now() });
          }
        }
      }
    };

    const handleKeyUp = (e: KeyboardEvent) => {
      const isPlus = e.key === '+' || e.key === '=' || e.code === 'NumpadAdd' || e.code === 'Equal';
      const isMinus = e.key === '-' || e.key === '_' || e.code === 'NumpadSubtract' || e.code === 'Minus';
      const isBracket = e.key === '[' || e.key === ']' || e.code === 'BracketLeft' || e.code === 'BracketRight';
      if (isPlus || isMinus || isBracket || e.key === 'Shift') {
        if (repeatHoldTimerRef.current) {
          clearInterval(repeatHoldTimerRef.current);
          repeatHoldTimerRef.current = null;
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
      if (repeatHoldTimerRef.current) clearInterval(repeatHoldTimerRef.current);
    };
  }, []);

  return { zToast };
}
