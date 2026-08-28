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
  const [isDragOver, setIsDragOver] = useState(false);

  const hasEmbeddedClips = availableAnimations.length > 0;
  const isCurrentlyPlaying = hasEmbeddedClips ? isPlayingAnim : isPosePlaying;

  const bodies = availableCategories.find((c) => c.id === 'than_co_ban')?.items || [];

  const VALID_3D_EXTS = ['.glb', '.gltf', '.fbx', '.vrm'];

  const importFile = (file: File) => {
    const ext = file.name.toLowerCase().substring(file.name.lastIndexOf('.'));
    if (!VALID_3D_EXTS.includes(ext)) return;
    const objectUrl = `${URL.createObjectURL(file)}#${encodeURIComponent(file.name)}`;
    setImportedFileName(file.name);
    onSelectModel(objectUrl);
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    importFile(file);
    e.target.value = '';
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragOver(false);
    const file = e.dataTransfer.files?.[0];
    if (file) importFile(file);
  };

  const handleDragOver = (e: React.DragEvent) => { e.preventDefault(); setIsDragOver(true); };
  const handleDragLeave = () => setIsDragOver(false);

  const PROCEDURAL_POSES = [
    { id: 'idle', label: 'Dáng Đứng Thở Sống Động', icon: '🧘', desc: 'Thở tự nhiên, chuyển trọng tâm chân mềm mại' },
    { id: 'walk', label: 'Dáng Đi Chuẩn Điện Ảnh', icon: '🚶', desc: 'Chu kỳ bước đi AAA với nhún hông & cuộn bàn chân' },
    { id: 'run', label: 'Chạy Nhanh Hành Động', icon: '🏃', desc: 'Sải bước chạy tốc độ cao, gập gối mạnh & nghiêng thân' },
    { id: 'slash', label: 'Xuất Chiêu Kiếm Pháp', icon: '⚔️', desc: 'Tích lực chém kiếm 4 giai đoạn uy lực & xoay thân' },
    { id: 'cast_spell', label: 'Niệm Chú Pháp Thuật', icon: '✨', desc: 'Tập trung ma lực, nâng tay vòng cung & ngửa thân' },
    { id: 'defend', label: 'Thế Thủ Võ Thuật', icon: '🛡️', desc: 'Hạ trọng tâm, hai tay thủ thế hộ thân vững chãi' },
    { id: 'dance', label: 'Vũ Đạo Nhịp Điệu', icon: '💃', desc: 'Lắc hông, đánh nhịp vai & sóng tay sôi động' },
    { id: 'wave', label: 'Vẫy Tay Thân Thiện', icon: '👋', desc: 'Giơ tay cao vẫy chào tự nhiên & nghiêng đầu' },
    { id: 'sit', label: 'Tư Thế Ngồi Thư Giãn', icon: '🪑', desc: 'Gập gối 90° chuẩn ngồi ghế nghỉ ngơi' },
    { id: 't_pose', label: 'T-Pose (Khung Chuẩn)', icon: '🤸', desc: 'Tư thế đứng chữ T chuẩn để căn chỉnh' },
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

          {/* Drag & Drop Zone + Click-to-Browse (bypasses slow Windows Explorer dialog) */}
          <div
            onDrop={handleDrop}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onClick={() => fileInputRef.current?.click()}
            title="Kéo thả file .glb / .gltf / .fbx / .vrm vào đây, hoặc click để chọn"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '8px 14px',
              background: isDragOver
                ? 'linear-gradient(135deg, #059669, #047857)'
                : 'linear-gradient(135deg, #0284c7, #0369a1)',
              color: '#ffffff',
              border: isDragOver ? '2px dashed #34d399' : '2px solid transparent',
              borderRadius: 8,
              fontSize: 11,
              fontWeight: 700,
              cursor: 'pointer',
              whiteSpace: 'nowrap',
              boxShadow: isDragOver
                ? '0 2px 12px rgba(5, 150, 105, 0.5)'
                : '0 2px 8px rgba(2, 132, 199, 0.3)',
              transition: 'all 0.15s ease',
              userSelect: 'none',
            }}
          >
            <Upload size={14} />
            <span>{isDragOver ? 'Thả file vào đây!' : 'Nhập File 3D'}</span>
            <input
              ref={fileInputRef}
              type="file"
              style={{ display: 'none' }}
              onChange={handleFileChange}
            />
          </div>
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

      {/* 2. Embedded Animation Clips Section */}
      <div
        style={{
          background: hasEmbeddedClips ? 'rgba(245, 158, 11, 0.05)' : 'rgba(255, 255, 255, 0.02)',
          padding: 14,
          borderRadius: 10,
          border: hasEmbeddedClips ? '1px solid rgba(245, 158, 11, 0.25)' : '1px solid rgba(255, 255, 255, 0.06)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <Film size={14} color={hasEmbeddedClips ? '#f59e0b' : '#94a3b8'} />
            <span style={{ fontSize: 12, fontWeight: 700, color: hasEmbeddedClips ? '#fbbf24' : '#e2e8f0' }}>
              1. Animation Tích Hợp Sẵn Trong File Model ({availableAnimations.length} Clips)
            </span>
          </div>
          {hasEmbeddedClips ? (
            <span style={{ fontSize: 10, background: 'rgba(245, 158, 11, 0.2)', color: '#fbbf24', padding: '2px 8px', borderRadius: 6, fontWeight: 700 }}>
              Đang khả dụng
            </span>
          ) : (
            <span style={{ fontSize: 10, background: 'rgba(255,255,255,0.06)', color: '#94a3b8', padding: '2px 8px', borderRadius: 6, fontWeight: 600 }}>
              0 clip nhúng
            </span>
          )}
        </div>

        {hasEmbeddedClips ? (
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
        ) : (
          <div style={{ fontSize: 11, color: '#94a3b8', lineHeight: 1.5, background: 'rgba(0,0,0,0.2)', padding: '10px 12px', borderRadius: 8, border: '1px dashed rgba(255,255,255,0.1)' }}>
            ℹ️ Model hiện tại (ví dụ: Columbina / Base Body) là <strong>file 3D tĩnh / T-Pose</strong> không chứa animation nhúng sẵn bên trong file. Hệ thống đang tự động kích hoạt <strong>Thư Viện Chuyển Động Xương</strong> ở mục 2 bên dưới. Bạn có thể kéo thả thêm file <code>.glb</code> / <code>.fbx</code> có animation từ Mixamo vào để hiện danh sách clip tại đây.
          </div>
        )}
      </div>

      {/* 3. Skeletal Motion Library (For all models with bones / Auto-Rig) */}
      <div style={{ background: 'rgba(255,255,255,0.03)', padding: 14, borderRadius: 10, border: '1px solid rgba(255,255,255,0.06)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 10 }}>
          <Zap size={14} color="#a855f7" />
          <span style={{ fontSize: 12, fontWeight: 700, color: '#c084fc' }}>
            2. Thư Viện Động Tác Chuyển Động Xương (Motion Library)
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
              value={String(animSpeed)}
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
              <option value="1">1.0x (Chuẩn)</option>
              <option value="1.25">1.25x</option>
              <option value="1.5">1.5x (Nhanh)</option>
              <option value="2">2.0x</option>
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
              fontWeight: 700,
              fontFamily: 'Inter, system-ui, sans-serif',
              letterSpacing: '0.3px',
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
                <Pause size={15} /> Tạm Dừng
              </>
            ) : (
              <>
                <Play size={15} /> Chạy Động Tác
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};
