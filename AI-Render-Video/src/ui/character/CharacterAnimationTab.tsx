import React, { useRef, useState } from 'react';
import {
  Film,
  Play,
  Pause,
  Upload,
  Activity,
  FastForward,
  Layers,
  Sparkles,
  Zap,
  CheckCircle,
  Eye,
  EyeOff,
  FolderOpen,
} from 'lucide-react';
import { CharacterCategory } from '../CharacterAssetRegistry';
import { NativeFileDialogHelper } from '../../utils/NativeFileDialogHelper';

export interface CharacterAnimationTabProps {
  availableCategories: CharacterCategory[];
  availableAnimations: string[];
  selectedAnimClip: string;
  onSelectAnimationClip: (clipName: string) => void;
  isPlayingAnim: boolean;
  onTogglePlayPause: () => void;
  animSpeed: number;
  onChangeAnimSpeed: (speed: number) => void;
  activePose: string;
  onSelectPose: (pose: string) => void;
  isPosePlaying: boolean;
  poseProgress: number;
  showSkeletonHelper: boolean;
  onToggleSkeletonHelper: () => void;
  hasBones: boolean;
  totalBonesCount: number;
  currentModelPath?: string;
  onSelectModel: (path: string) => void;
}

export const CharacterAnimationTab: React.FC<CharacterAnimationTabProps> = ({
  availableCategories,
  availableAnimations,
  selectedAnimClip,
  onSelectAnimationClip,
  isPlayingAnim,
  onTogglePlayPause,
  animSpeed,
  onChangeAnimSpeed,
  activePose,
  onSelectPose,
  isPosePlaying,
  poseProgress,
  showSkeletonHelper,
  onToggleSkeletonHelper,
  hasBones,
  totalBonesCount,
  currentModelPath = '',
  onSelectModel,
}) => {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [importedFileName, setImportedFileName] = useState<string>('');

  const hasEmbeddedClips = availableAnimations.length > 0;
  const isCurrentlyPlaying = hasEmbeddedClips ? isPlayingAnim : isPosePlaying;

  const bodies = availableCategories.find((c) => c.id === 'than_co_ban')?.items || [];

  const handleFastImport = async () => {
    const files = await NativeFileDialogHelper.pickFiles({
      description: '3D Character Models',
      extensions: ['.glb', '.gltf', '.fbx', '.vrm'],
      multiple: false,
    });
    if (files.length > 0) {
      const file = files[0];
      const objectUrl = `${URL.createObjectURL(file)}#${encodeURIComponent(file.name)}`;
      setImportedFileName(file.name);
      onSelectModel(objectUrl);
    }
  };

  const PROCEDURAL_POSES = [
    { id: 'walk', label: 'Dáng Đi Tự Nhiên', icon: '🚶', desc: 'Chu kỳ bước đi Humanoid chuẩn nhịp tay đối xứng' },
    { id: 'slash', label: 'Xuất Chiêu Kiếm Pháp', icon: '⚔️', desc: 'Hoạt cảnh vung kiếm chém ngang uy lực & xoay thân' },
    { id: 'defend', label: 'Thế Thủ Võ Thuật', icon: '🛡️', desc: 'Hạ trọng tâm, giơ tay thủ thế phòng ngự' },
    { id: 'wave', label: 'Vẫy Tay Chào', icon: '👋', desc: 'Giơ tay cao vẫy chào thân thiện' },
    { id: 'sit', label: 'Tư Thế Ngồi Ghế', icon: '🪑', desc: 'Gập gối 90° ngồi thư giãn' },
    { id: 't_pose', label: 'T-Pose (Khung Chuẩn)', icon: '🤸', desc: 'Tư thế đứng chữ T chuẩn để gắn xương' },
  ];

  return (
    <div style={{ flex: 1, padding: 18, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 16 }}>
      {/* Header Info */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: 16, fontWeight: 800, color: '#f59e0b', letterSpacing: '0.2px' }}>
              🎬 Animation & Chuyển Động Studio
            </span>
            <span style={{ fontSize: 10, background: 'rgba(245, 158, 11, 0.15)', color: '#fbbf24', padding: '2px 8px', borderRadius: 12, fontWeight: 700, border: '1px solid rgba(245, 158, 11, 0.3)' }}>
              Real-time 60 FPS
            </span>
          </div>
          <p style={{ margin: '4px 0 0 0', fontSize: 11, color: '#94a3b8' }}>
            Duyệt & phát các animation tích hợp sẵn trong model hoặc thư viện chuyển động chuẩn giải phẫu.
          </p>
        </div>

        {/* Skeleton Bones Badge */}
        <button
          onClick={onToggleSkeletonHelper}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            padding: '6px 12px',
            fontSize: 11,
            fontWeight: 700,
            borderRadius: 8,
            background: showSkeletonHelper ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.05)',
            border: showSkeletonHelper ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.12)',
            color: showSkeletonHelper ? '#38bdf8' : '#94a3b8',
            cursor: 'pointer',
            transition: 'all 0.2s ease',
          }}
        >
          {showSkeletonHelper ? <Eye size={14} /> : <EyeOff size={14} />}
          <span>{showSkeletonHelper ? 'Ẩn Khung Xương' : 'Hiện Khung Xương'}</span>
          {totalBonesCount > 0 && (
            <span style={{ fontSize: 10, background: 'rgba(0,0,0,0.35)', padding: '1px 6px', borderRadius: 6, color: '#38bdf8' }}>
              {totalBonesCount} Khớp
            </span>
          )}
        </button>
      </div>

      {/* 1. Model Selector & Local File Import Card */}
      <div style={{ background: 'rgba(255,255,255,0.03)', padding: 14, borderRadius: 10, border: '1px solid rgba(255,255,255,0.08)', display: 'flex', flexDirection: 'column', gap: 10 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <label style={{ fontWeight: 700, color: '#f1f5f9', fontSize: 12, display: 'flex', alignItems: 'center', gap: 6 }}>
            <Layers size={15} color="#38bdf8" />
            <span>Chọn Hoặc Nhập Mô Hình 3D Cần Test Animation</span>
          </label>

          {importedFileName && (
            <span style={{ fontSize: 10, background: 'rgba(34, 197, 94, 0.2)', color: '#4ade80', padding: '2px 8px', borderRadius: 6, fontWeight: 700 }}>
              ✅ Đã nạp: {importedFileName}
            </span>
          )}
        </div>

        <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
          {/* Dropdown Library Models */}
          <select
            value={currentModelPath}
            onChange={(e) => {
              setImportedFileName('');
              onSelectModel(e.target.value);
            }}
            style={{
              flex: 1,
              padding: '8px 12px',
              background: '#0f172a',
              color: '#f8fafc',
              border: '1px solid rgba(255,255,255,0.15)',
              borderRadius: 8,
              fontSize: 12,
              fontWeight: 600,
              outline: 'none',
              cursor: 'pointer',
            }}
          >
            {bodies.map((item) => (
              <option key={item.path} value={item.path}>
                {item.name} ({item.path})
              </option>
            ))}
            {currentModelPath.startsWith('blob:') && (
              <option value={currentModelPath}>
                📁 File Đã Nhập: {importedFileName || 'Mô hình tùy chỉnh'}
              </option>
            )}
          </select>

          {/* Fast Import File Button */}
          <button
            onClick={handleFastImport}
            title="Tải tệp mô hình 3D (.glb, .gltf, .fbx, .vrm) từ máy tính của bạn để kiểm thử chuyển động"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '8px 14px',
              background: 'linear-gradient(135deg, #0284c7, #0369a1)',
              color: '#ffffff',
              border: 'none',
              borderRadius: 8,
              fontSize: 11,
              fontWeight: 700,
              cursor: 'pointer',
              whiteSpace: 'nowrap',
              boxShadow: '0 2px 8px rgba(2, 132, 199, 0.3)',
              transition: 'all 0.15s ease',
            }}
          >
            <Upload size={14} />
            <span>Nhập File 3D</span>
          </button>
        </div>

        {/* Model Status Subtext */}
        <div style={{ fontSize: 10, color: hasBones ? '#34d399' : '#94a3b8', display: 'flex', alignItems: 'center', gap: 4, marginTop: 2 }}>
          {hasBones ? (
            <>
              <CheckCircle size={11} color="#34d399" />
              <span>Phát hiện {totalBonesCount} Khớp Xương SkinnedMesh (Sẵn sàng chạy Animation & Chuyển Động)</span>
            </>
          ) : (
            <span>Mô hình tĩnh (Static Mesh) — Không phát hiện khung xương. Bạn có thể sang tab Auto-Rig để gắn xương tự động.</span>
          )}
        </div>
      </div>

      {/* 2. Embedded Animation Clips Section (If present in model) */}
      {hasEmbeddedClips && (
        <div style={{ background: 'rgba(245, 158, 11, 0.05)', padding: 14, borderRadius: 10, border: '1px solid rgba(245, 158, 11, 0.2)' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 10 }}>
            <Film size={14} color="#f59e0b" />
            <span style={{ fontSize: 12, fontWeight: 700, color: '#fbbf24' }}>
              Danh Sách Animation Tích Hợp Sẵn Trong File ({availableAnimations.length} Động Tác)
            </span>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))', gap: 8 }}>
            {availableAnimations.map((clipName) => {
              const isSelected = selectedAnimClip === clipName;
              return (
                <button
                  key={clipName}
                  onClick={() => onSelectAnimationClip(clipName)}
                  style={{
                    padding: '8px 12px',
                    fontSize: 11,
                    fontWeight: isSelected ? 700 : 500,
                    borderRadius: 8,
                    background: isSelected ? 'rgba(245, 158, 11, 0.25)' : 'rgba(255,255,255,0.04)',
                    border: isSelected ? '1px solid #f59e0b' : '1px solid rgba(255,255,255,0.08)',
                    color: isSelected ? '#fbbf24' : '#cbd5e1',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    textAlign: 'left',
                    transition: 'all 0.15s ease',
                  }}
                >
                  <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>🎬 {clipName}</span>
                  {isSelected && isPlayingAnim && (
                    <span style={{ width: 6, height: 6, borderRadius: '50%', background: '#fbbf24', boxShadow: '0 0 6px #fbbf24' }} />
                  )}
                </button>
              );
            })}
          </div>
        </div>
      )}

      {/* 3. Skeletal Motion Library (For all models with bones / Auto-Rig) */}
      <div style={{ background: 'rgba(255,255,255,0.03)', padding: 14, borderRadius: 10, border: '1px solid rgba(255,255,255,0.06)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 10 }}>
          <Zap size={14} color="#a855f7" />
          <span style={{ fontSize: 12, fontWeight: 700, color: '#c084fc' }}>
            Thư Viện Động Tác Chuyển Động Xương (Motion Library)
          </span>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: 8 }}>
          {PROCEDURAL_POSES.map((pose) => {
            const isSelected = activePose === pose.id;
            return (
              <button
                key={pose.id}
                onClick={() => onSelectPose(pose.id)}
                style={{
                  padding: '10px 12px',
                  borderRadius: 8,
                  background: isSelected ? 'rgba(168, 85, 247, 0.22)' : 'rgba(255,255,255,0.03)',
                  border: isSelected ? '1px solid #a855f7' : '1px solid rgba(255,255,255,0.08)',
                  color: isSelected ? '#e9d5ff' : '#cbd5e1',
                  cursor: 'pointer',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 3,
                  textAlign: 'left',
                  transition: 'all 0.15s ease',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', width: '100%' }}>
                  <span style={{ fontSize: 12, fontWeight: 700 }}>{pose.icon} {pose.label}</span>
                  {isSelected && isPosePlaying && (
                    <span style={{ width: 6, height: 6, borderRadius: '50%', background: '#a855f7', boxShadow: '0 0 6px #a855f7' }} />
                  )}
                </div>
                <span style={{ fontSize: 10, color: '#94a3b8', lineHeight: 1.3 }}>{pose.desc}</span>
              </button>
            );
          })}
        </div>
      </div>

      {/* 4. Timeline Master Controller Bar */}
      <div style={{ background: 'rgba(15, 23, 42, 0.95)', padding: 14, borderRadius: 10, border: '1px solid rgba(56, 189, 248, 0.3)', display: 'flex', flexDirection: 'column', gap: 12 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span style={{ fontSize: 12, fontWeight: 700, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 5 }}>
            <Activity size={14} /> Bộ Điều Khiển Phát Animation
          </span>

          {/* Speed Selector */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <FastForward size={13} color="#94a3b8" />
            <span style={{ fontSize: 11, color: '#94a3b8' }}>Tốc độ:</span>
            <select
              value={animSpeed}
              onChange={(e) => onChangeAnimSpeed(parseFloat(e.target.value))}
              style={{
                background: '#1e293b',
                border: '1px solid rgba(255,255,255,0.15)',
                color: '#f8fafc',
                padding: '3px 8px',
                borderRadius: 6,
                fontSize: 11,
                fontWeight: 600,
                outline: 'none',
                cursor: 'pointer',
              }}
            >
              <option value="0.25">0.25x</option>
              <option value="0.5">0.5x (Chậm)</option>
              <option value="1.0">1.0x (Chuẩn)</option>
              <option value="1.25">1.25x</option>
              <option value="1.5">1.5x (Nhanh)</option>
              <option value="2.0">2.0x</option>
            </select>
          </div>
        </div>

        {/* Scrubber Progress Track */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div style={{ flex: 1, height: 6, background: 'rgba(255,255,255,0.1)', borderRadius: 3, overflow: 'hidden', position: 'relative' }}>
            <div
              id="character-anim-progress-bar"
              style={{
                height: '100%',
                width: '0%',
                background: 'linear-gradient(90deg, #38bdf8, #a855f7)',
                transition: 'width 0.05s linear',
              }}
            />
          </div>
          <span
            id="character-anim-progress-text"
            style={{ fontSize: 11, color: '#94a3b8', minWidth: 35, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}
          >
            0%
          </span>
        </div>

        {/* Master Play / Pause Button */}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 12 }}>
          <button
            onClick={onTogglePlayPause}
            style={{
              padding: '8px 24px',
              fontSize: 12,
              fontWeight: 800,
              borderRadius: 8,
              background: isCurrentlyPlaying ? 'linear-gradient(135deg, #ef4444, #dc2626)' : 'linear-gradient(135deg, #10b981, #059669)',
              color: '#ffffff',
              border: 'none',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              boxShadow: isCurrentlyPlaying ? '0 4px 14px rgba(239, 68, 68, 0.4)' : '0 4px 14px rgba(16, 185, 129, 0.4)',
              transition: 'all 0.15s ease',
            }}
          >
            {isCurrentlyPlaying ? (
              <>
                <Pause size={15} /> TẠM DỪNG
              </>
            ) : (
              <>
                <Play size={15} /> CHẠY ĐỘNG TÁC
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};
