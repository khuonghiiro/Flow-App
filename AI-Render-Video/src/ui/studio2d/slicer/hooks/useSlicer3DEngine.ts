import { useState, useRef, useEffect } from 'react';
import { Character2DAssembly } from '../../../../types/scene2d';
import { ThreeMultiAngleBillboardEngine, AngleDetectionResult } from '../../../../core/engine2d/ThreeMultiAngleBillboardEngine';

export interface UseSlicer3DEngineProps {
  currentAssembly: Character2DAssembly;
}

export function useSlicer3DEngine({ currentAssembly }: UseSlicer3DEngineProps) {
  const [activeAngleInfo, setActiveAngleInfo] = useState<AngleDetectionResult>({
    angleDeg: 0,
    discreteAngle: 'front',
    angleLabel: 'Chính diện (Front 0°)',
    compassDirection: 'S',
  });
  const [turntableAngle, setTurntableAngle] = useState<number>(0);

  const threeContainerRef = useRef<HTMLDivElement>(null);
  const threeEngineRef = useRef<ThreeMultiAngleBillboardEngine | null>(null);

  // Initialize 3D Engine
  useEffect(() => {
    if (threeContainerRef.current && !threeEngineRef.current) {
      threeEngineRef.current = new ThreeMultiAngleBillboardEngine(threeContainerRef.current, (res: AngleDetectionResult) => {
        setActiveAngleInfo(res);
        setTurntableAngle(res.angleDeg);
      });
      if (currentAssembly) threeEngineRef.current.setAssembly(currentAssembly);
    }
    return () => {
      if (threeEngineRef.current) {
        threeEngineRef.current.dispose();
        threeEngineRef.current = null;
      }
    };
  }, [currentAssembly]);

  useEffect(() => {
    if (threeEngineRef.current && currentAssembly) threeEngineRef.current.setAssembly(currentAssembly);
  }, [currentAssembly]);

  return {
    activeAngleInfo,
    setActiveAngleInfo,
    turntableAngle,
    setTurntableAngle,
    threeContainerRef,
    threeEngineRef,
  };
}
