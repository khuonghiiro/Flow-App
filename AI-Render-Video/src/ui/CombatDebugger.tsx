import React from 'react';
import { Swords, Zap, Activity, ShieldAlert } from 'lucide-react';
import { MasterSceneConfig } from '../types/scene';

interface CombatDebuggerProps {
  scene: MasterSceneConfig;
  currentTime: number;
  onSeekToImpact: (time: number) => void;
}

export const CombatDebugger: React.FC<CombatDebuggerProps> = ({
  scene,
  currentTime,
  onSeekToImpact,
}) => {
  const combatActions = scene.actors.flatMap((a) =>
    (a.tracks.combat_actions || []).map((action) => ({
      attackerId: a.id,
      attackerName: a.name,
      ...action,
    }))
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <span style={{ fontSize: 13, fontWeight: 700, color: '#f8fafc', display: 'flex', alignItems: 'center', gap: 6 }}>
          <Swords size={15} color="#f43f5e" /> Combat Sync Monitor
        </span>
        <span style={{ fontSize: 11, color: '#94a3b8' }}>Frame-Accurate</span>
      </div>

      {combatActions.map((cb, idx) => {
        const isWindup = currentTime >= cb.start_time && currentTime < cb.impact_time;
        const isImpact = Math.abs(currentTime - cb.impact_time) < 0.2;
        const isRecovery = currentTime >= cb.impact_time && currentTime <= cb.impact_time + 1.5;

        return (
          <div
            key={idx}
            className="ui-card"
            style={{
              borderColor: isImpact ? '#f43f5e' : undefined,
              boxShadow: isImpact ? '0 0 15px rgba(244, 63, 94, 0.4)' : undefined,
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <strong style={{ fontSize: 12, color: '#f8fafc' }}>{cb.anim}</strong>
              <button
                className="btn-secondary"
                style={{ padding: '2px 6px', fontSize: 10 }}
                onClick={() => onSeekToImpact(cb.impact_time)}
              >
                Nhảy đến va chạm ({cb.impact_time}s)
              </button>
            </div>

            <div style={{ fontSize: 11, color: '#94a3b8', display: 'flex', flexDirection: 'column', gap: 4 }}>
              <div>⚔️ Tấn công: <strong style={{ color: '#eab308' }}>{cb.attackerName}</strong></div>
              <div>🎯 Mục tiêu: <strong style={{ color: '#a855f7' }}>{cb.target.actor_id}</strong></div>
              <div>💥 Phản lực lùi: <strong>{cb.target.knockback_distance}m</strong> ({cb.target.reaction_anim})</div>
              <div>📳 Rung Camera: <strong>{cb.target.screen_shake.intensity} intensity</strong></div>
            </div>

            {/* Combat Phase Meter */}
            <div style={{ display: 'flex', gap: 4, marginTop: 4 }}>
              <div
                style={{
                  flex: 1,
                  padding: 4,
                  borderRadius: 4,
                  fontSize: 10,
                  textAlign: 'center',
                  fontWeight: 600,
                  background: isWindup ? '#eab308' : 'rgba(255, 255, 255, 0.05)',
                  color: isWindup ? '#000000' : '#64748b',
                }}
              >
                1. Lấy đà & VFX
              </div>
              <div
                style={{
                  flex: 1,
                  padding: 4,
                  borderRadius: 4,
                  fontSize: 10,
                  textAlign: 'center',
                  fontWeight: 600,
                  background: isImpact ? '#f43f5e' : 'rgba(255, 255, 255, 0.05)',
                  color: isImpact ? '#ffffff' : '#64748b',
                }}
              >
                2. Va Chạm (Hit)
              </div>
              <div
                style={{
                  flex: 1,
                  padding: 4,
                  borderRadius: 4,
                  fontSize: 10,
                  textAlign: 'center',
                  fontWeight: 600,
                  background: isRecovery ? '#3b82f6' : 'rgba(255, 255, 255, 0.05)',
                  color: isRecovery ? '#ffffff' : '#64748b',
                }}
              >
                3. Bay lùi & Đau
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
};
