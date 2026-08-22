import React, { useState, useEffect } from 'react';
import {
  Clapperboard,
  Play,
  Pause,
  Plus,
  Trash2,
  Camera,
  Swords,
  MessageSquare,
  Sparkles,
  Volume2,
  Code,
  Copy,
  Check,
} from 'lucide-react';
import { Scene2DConfig, Scene2DShot } from '../../types/scene2d';

const SAMPLE_2D_SCENE: Scene2DConfig = {
  scene_id: 'scene_2d_tu_tien_combat',
  title: 'Tiêu Dao Kiếm Khách Quyết Đấu',
  map_id: 'map_truc_lam_tu_tien',
  duration_seconds: 12.0,
  shots: [
    {
      id: 'shot_1',
      time_start: 0.0,
      time_end: 3.5,
      shot_type: 'medium_shot',
      camera_zoom: 1.0,
      camera_offset: [0, 0],
      transition_in: 'none',
      actors: {
        actor_a: {
          actor_id: 'char_kiem_khach_tieu_dao',
          position: [-100, 0],
          animation: 'talk',
          expression: 'neutral',
          mouth_talk_cycle: true,
        },
      },
      speaker_name: 'Tiêu Dao',
      subtitle_text: 'Dám xâm phạm cấm địa Trúc Lâm, nhận một kiếm này của ta!',
      sfx_sound: 'asset_2ds/am_thanh/sfx_combat/rut_kiem.mp3',
    },
    {
      id: 'shot_2',
      time_start: 3.5,
      time_end: 7.0,
      shot_type: 'jump_cut_closeup',
      camera_zoom: 1.4,
      camera_offset: [-50, -30],
      camera_shake: { intensity: 0.8, duration: 0.4 },
      transition_in: 'jump_cut',
      vfx_overlay: 'asset_2ds/hieu_ung/chem_kiem/kiem_khi_xanh.png',
      actors: {
        actor_a: {
          actor_id: 'char_kiem_khach_tieu_dao',
          position: [-50, 0],
          animation: 'combat_slash',
          expression: 'angry',
          weapon_visible: true,
        },
      },
      sfx_sound: 'asset_2ds/am_thanh/sfx_combat/chem_kiem_no.mp3',
    },
    {
      id: 'shot_3',
      time_start: 7.0,
      time_end: 10.0,
      shot_type: 'closeup',
      camera_zoom: 1.5,
      camera_offset: [80, -20],
      camera_shake: { intensity: 0.3, duration: 0.2 },
      actors: {
        actor_b: {
          actor_id: 'char_doi_thu',
          position: [80, 0],
          animation: 'shocked',
          expression: 'shocked',
        },
      },
      speaker_name: 'Đối Thủ',
      subtitle_text: 'Kiếm khí cường đại quá... Không thể nào!',
    },
    {
      id: 'shot_4',
      time_start: 10.0,
      time_end: 12.0,
      shot_type: 'wide_shot',
      camera_zoom: 0.85,
      camera_offset: [0, 0],
      transition_in: 'flash_white',
      actors: {},
      subtitle_text: '[Cảnh chuyển sang Đại Điện Tiên Môn]',
    },
  ],
};

export const ActionSequence2DDirector: React.FC = () => {
  const [sceneConfig, setSceneConfig] = useState<Scene2DConfig>(SAMPLE_2D_SCENE);
  const [activeShotId, setActiveShotId] = useState<string>('shot_1');
  const [currentTime, setCurrentTime] = useState<number>(0);
  const [isPlaying, setIsPlaying] = useState<boolean>(false);
  const [showJsonCode, setShowJsonCode] = useState<boolean>(false);
  const [copied, setCopied] = useState<boolean>(false);

  // Playback timer loop
  useEffect(() => {
    let interval: any;
    if (isPlaying) {
      interval = setInterval(() => {
        setCurrentTime((prev) => {
          const next = prev + 0.1;
          if (next > sceneConfig.duration_seconds) {
            return 0;
          }
          return next;
        });
      }, 100);
    }
    return () => clearInterval(interval);
  }, [isPlaying, sceneConfig.duration_seconds]);

  // Sync active shot to current playback time
  useEffect(() => {
    const shot = sceneConfig.shots.find((s) => currentTime >= s.time_start && currentTime < s.time_end);
    if (shot) setActiveShotId(shot.id);
  }, [currentTime, sceneConfig.shots]);

  const activeShot = sceneConfig.shots.find((s) => s.id === activeShotId) || sceneConfig.shots[0];

  const handleCopyJson = () => {
    navigator.clipboard.writeText(JSON.stringify(sceneConfig, null, 2));
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '320px 1fr', gap: 16, height: '100%', overflow: 'hidden' }}>
      {/* ─── LEFT: Storyboard Shots List ──────────────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, background: 'rgba(15, 23, 42, 0.7)', padding: 12, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', overflowY: 'auto' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Clapperboard size={13} /> DANH SÁCH CẢNH QUAY (SHOTS)
          </div>
          <span style={{ fontSize: 10, color: '#94a3b8' }}>{sceneConfig.shots.length} shots ({sceneConfig.duration_seconds}s)</span>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {sceneConfig.shots.map((shot, idx) => {
            const isSelected = shot.id === activeShotId;
            return (
              <div
                key={shot.id}
                onClick={() => {
                  setActiveShotId(shot.id);
                  setCurrentTime(shot.time_start);
                }}
                style={{
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 4,
                  padding: '8px 10px',
                  borderRadius: 6,
                  background: isSelected ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.02)',
                  border: isSelected ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.04)',
                  cursor: 'pointer',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontSize: 11, fontWeight: 700, color: isSelected ? '#38bdf8' : '#e2e8f0' }}>
                    Shot #{idx + 1}: {shot.shot_type}
                  </span>
                  <span style={{ fontSize: 9, color: '#64748b' }}>{shot.time_start.toFixed(1)}s - {shot.time_end.toFixed(1)}s</span>
                </div>
                {shot.subtitle_text && (
                  <div style={{ fontSize: 10, color: '#94a3b8', fontStyle: 'italic', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                    {shot.speaker_name ? `[${shot.speaker_name}]: ` : ''}{shot.subtitle_text}
                  </div>
                )}
              </div>
            );
          })}
        </div>

        {/* JSON toggle */}
        <button
          onClick={() => setShowJsonCode(!showJsonCode)}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 6,
            padding: '8px',
            borderRadius: 6,
            background: 'rgba(255,255,255,0.05)',
            border: '1px solid rgba(255,255,255,0.1)',
            color: '#e2e8f0',
            fontSize: 11,
            cursor: 'pointer',
            marginTop: 'auto',
          }}
        >
          <Code size={13} /> {showJsonCode ? 'Ẩn Cấu Trúc JSON' : 'Xem Cấu Trúc JSON Chuẩn AI'}
        </button>
      </div>

      {/* ─── RIGHT: Shot Detail & Director Controls ────────────────── */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12, background: 'rgba(15, 23, 42, 0.7)', padding: 16, borderRadius: 8, border: '1px solid rgba(255,255,255,0.08)', overflowY: 'auto' }}>
        {showJsonCode ? (
          /* JSON Schema View */
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10, height: '100%' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ fontSize: 12, fontWeight: 700, color: '#4ade80' }}>Cấu Trúc Dữ Liệu Kịch Bản JSON:</span>
              <button
                onClick={handleCopyJson}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                  padding: '4px 10px',
                  borderRadius: 4,
                  background: copied ? '#22c55e' : '#0284c7',
                  color: '#fff',
                  fontSize: 11,
                  border: 'none',
                  cursor: 'pointer',
                }}
              >
                {copied ? <Check size={12} /> : <Copy size={12} />} {copied ? 'Đã Sao Chép!' : 'Sao Chép JSON'}
              </button>
            </div>
            <pre style={{ flex: 1, background: '#090d16', padding: 12, borderRadius: 6, fontSize: 11, color: '#38bdf8', overflow: 'auto', margin: 0 }}>
              {JSON.stringify(sceneConfig, null, 2)}
            </pre>
          </div>
        ) : (
          /* Visual Shot Inspector */
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            {/* Playback Scrubber */}
            <div style={{ background: '#090d16', padding: 12, borderRadius: 6, border: '1px solid rgba(255,255,255,0.1)' }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <button
                    onClick={() => setIsPlaying(!isPlaying)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 4,
                      padding: '5px 12px',
                      borderRadius: 5,
                      background: isPlaying ? '#0284c7' : 'rgba(255,255,255,0.1)',
                      color: '#fff',
                      fontSize: 11,
                      fontWeight: 700,
                      border: 'none',
                      cursor: 'pointer',
                    }}
                  >
                    {isPlaying ? <Pause size={12} /> : <Play size={12} />}
                    {isPlaying ? 'Dừng Diễn Hoạt' : 'Chạy Kịch Bản'}
                  </button>
                  <span style={{ fontSize: 11, color: '#38bdf8', fontWeight: 600 }}>
                    Thời gian: {currentTime.toFixed(1)}s / {sceneConfig.duration_seconds}s
                  </span>
                </div>
                <span style={{ fontSize: 10, background: 'rgba(168, 85, 247, 0.2)', color: '#c084fc', padding: '2px 8px', borderRadius: 10 }}>
                  Đang chạy: {activeShot.shot_type}
                </span>
              </div>

              <input
                type="range"
                min="0"
                max={sceneConfig.duration_seconds * 10}
                value={Math.round(currentTime * 10)}
                onChange={(e) => setCurrentTime(parseInt(e.target.value) / 10)}
                style={{ width: '100%' }}
              />
            </div>

            {/* Current Shot Information Card */}
            <div style={{ background: 'rgba(255,255,255,0.02)', padding: 14, borderRadius: 6, border: '1px solid rgba(255,255,255,0.06)' }}>
              <div style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8', marginBottom: 8, display: 'flex', alignItems: 'center', gap: 6 }}>
                <Camera size={14} /> CHI TIẾT GÓC MÁY & DIỄN HOẠT TRONG SHOT
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <div>
                  <div style={{ fontSize: 10, color: '#94a3b8' }}>Kiểu góc máy (Shot Type):</div>
                  <div style={{ fontSize: 12, fontWeight: 600, color: '#fff' }}>{activeShot.shot_type} (Zoom: {activeShot.camera_zoom}x)</div>
                </div>

                <div>
                  <div style={{ fontSize: 10, color: '#94a3b8' }}>Rung lắc camera (Shake):</div>
                  <div style={{ fontSize: 12, fontWeight: 600, color: activeShot.camera_shake ? '#ef4444' : '#64748b' }}>
                    {activeShot.camera_shake ? `Cường độ ${activeShot.camera_shake.intensity * 100}% (${activeShot.camera_shake.duration}s)` : 'Không có'}
                  </div>
                </div>

                <div>
                  <div style={{ fontSize: 10, color: '#94a3b8' }}>Hiệu ứng kỹ năng (VFX):</div>
                  <div style={{ fontSize: 11, color: activeShot.vfx_overlay ? '#38bdf8' : '#64748b' }}>
                    {activeShot.vfx_overlay || 'Không có'}
                  </div>
                </div>

                <div>
                  <div style={{ fontSize: 10, color: '#94a3b8' }}>Âm thanh SFX:</div>
                  <div style={{ fontSize: 11, color: activeShot.sfx_sound ? '#fbbf24' : '#64748b' }}>
                    {activeShot.sfx_sound || 'Không có'}
                  </div>
                </div>
              </div>

              {activeShot.subtitle_text && (
                <div style={{ marginTop: 12, background: 'rgba(0,0,0,0.4)', padding: 10, borderRadius: 6, borderLeft: '3px solid #38bdf8' }}>
                  <div style={{ fontSize: 10, color: '#38bdf8', fontWeight: 700 }}>
                    {activeShot.speaker_name ? `Lời thoại - ${activeShot.speaker_name}:` : 'Lời thoại / Dẫn truyện:'}
                  </div>
                  <div style={{ fontSize: 12, color: '#fff', marginTop: 2 }}>
                    "{activeShot.subtitle_text}"
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
