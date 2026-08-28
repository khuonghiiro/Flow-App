import React from 'react';
import { Compass, RotateCw } from 'lucide-react';
import { STANDARD_8_ANGLES, StandardHorizontalAngle, AngleInfo } from '../../../../types/studio2d_director';

interface AngleCompassWheelProps {
  currentAngle: number; // 0..360
  onChangeAngle: (deg: number) => void;
  title?: string;
  size?: number;
}

export const AngleCompassWheel: React.FC<AngleCompassWheelProps> = ({
  currentAngle,
  onChangeAngle,
  title = '🎥 GÓC QUAY CAMERA (360°)',
  size = 140,
}) => {
  const normalizedDeg = ((currentAngle % 360) + 360) % 360;

  // Closest standard angle
  const closestStandard = STANDARD_8_ANGLES.reduce((prev, curr) => {
    return Math.abs(curr.deg - normalizedDeg) < Math.abs(prev.deg - normalizedDeg) ? curr : prev;
  });

  return (
    <div
      style={{
        background: 'rgba(15, 23, 42, 0.85)',
        border: '1px solid rgba(255, 255, 255, 0.08)',
        borderRadius: 8,
        padding: 10,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 8,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', width: '100%' }}>
        <span style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
          <Compass size={13} /> {title}
        </span>
        <span style={{ fontSize: 11, fontWeight: 700, color: '#4ade80', background: 'rgba(34,197,94,0.15)', padding: '1px 6px', borderRadius: 4 }}>
          {normalizedDeg.toFixed(0)}° ({closestStandard.labelVi.split(' ')[0]})
        </span>
      </div>

      {/* Rotary Visual Compass Wheel */}
      <div
        style={{
          position: 'relative',
          width: size,
          height: size,
          borderRadius: '50%',
          background: 'radial-gradient(circle, #090d16 0%, #030712 100%)',
          border: '2px solid rgba(56, 189, 248, 0.3)',
          boxShadow: '0 0 15px rgba(56, 189, 248, 0.15) inset',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          userSelect: 'none',
        }}
      >
        {/* Center Pivot Indicator */}
        <div
          style={{
            position: 'absolute',
            width: 14,
            height: 14,
            borderRadius: '50%',
            background: '#38bdf8',
            boxShadow: '0 0 8px #38bdf8',
            zIndex: 10,
          }}
        />

        {/* Needle Pointer */}
        <div
          style={{
            position: 'absolute',
            width: 3,
            height: size / 2 - 10,
            background: 'linear-gradient(to top, #38bdf8, #ec4899)',
            borderRadius: 2,
            bottom: size / 2,
            left: 'calc(50% - 1.5px)',
            transformOrigin: 'bottom center',
            transform: `rotate(${normalizedDeg}deg)`,
            transition: 'transform 0.1s ease-out',
            zIndex: 5,
            boxShadow: '0 0 6px #ec4899',
          }}
        />

        {/* 8 Angle Nodes around circle */}
        {STANDARD_8_ANGLES.map((ang) => {
          const rad = ((ang.deg - 90) * Math.PI) / 180;
          const r = size / 2 - 18;
          const x = size / 2 + r * Math.cos(rad) - 12;
          const y = size / 2 + r * Math.sin(rad) - 12;
          const isSelected = Math.abs(ang.deg - normalizedDeg) < 22.5;

          return (
            <button
              key={ang.id}
              onClick={() => onChangeAngle(ang.deg)}
              title={`${ang.labelVi} (${ang.deg}°)`}
              style={{
                position: 'absolute',
                left: x,
                top: y,
                width: 24,
                height: 24,
                borderRadius: '50%',
                background: isSelected ? '#38bdf8' : 'rgba(30, 41, 59, 0.9)',
                border: isSelected ? '2px solid #ffffff' : '1px solid rgba(255,255,255,0.15)',
                color: isSelected ? '#030712' : '#94a3b8',
                fontSize: 9,
                fontWeight: 700,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                zIndex: 8,
                boxShadow: isSelected ? '0 0 10px #38bdf8' : 'none',
                transition: 'all 0.15s',
              }}
            >
              {ang.deg}
            </button>
          );
        })}
      </div>

      {/* Quick 8-Angle Button Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 4, width: '100%' }}>
        {STANDARD_8_ANGLES.map((ang) => {
          const isSelected = Math.abs(ang.deg - normalizedDeg) < 22.5;
          return (
            <button
              key={ang.id}
              onClick={() => onChangeAngle(ang.deg)}
              style={{
                padding: '4px 2px',
                fontSize: 9.5,
                fontWeight: isSelected ? 700 : 500,
                borderRadius: 4,
                background: isSelected ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.03)',
                border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.06)',
                color: isSelected ? '#38bdf8' : '#cbd5e1',
                cursor: 'pointer',
                textAlign: 'center',
                whiteSpace: 'nowrap',
              }}
            >
              {ang.compass} {ang.deg}°
            </button>
          );
        })}
      </div>
    </div>
  );
};
