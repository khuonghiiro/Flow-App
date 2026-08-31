// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// =========================================================================================
import React, { useState, useEffect, useMemo } from 'react';
import {
  FolderOpen,
  X,
  Search,
  Trash2,
  Play,
  Layers,
  Sparkles,
  Calendar,
  Clock,
} from 'lucide-react';
import { AnimationSequenceConfig } from '../../../../types/animation_slicer';
import {
  loadAllSavedAnimationSequences,
  deleteSavedAnimationSequence,
} from '../utils/animationPoseRegistry';

interface AnimationSequenceLoadModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSelectSequence: (sequence: AnimationSequenceConfig) => void;
}

export const AnimationSequenceLoadModal: React.FC<AnimationSequenceLoadModalProps> = ({
  isOpen,
  onClose,
  onSelectSequence,
}) => {
  const [sequences, setSequences] = useState<AnimationSequenceConfig[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedAngleFilter, setSelectedAngleFilter] = useState<number | 'all'>('all');

  // Reload saved sequences whenever modal opens
  useEffect(() => {
    if (isOpen) {
      const list = loadAllSavedAnimationSequences();
      setSequences(list);
    }
  }, [isOpen]);

  const handleDelete = (e: React.MouseEvent, id: string) => {
    e.stopPropagation();
    if (window.confirm('Bạn có chắc muốn xóa hoạt ảnh đã lưu này không?')) {
      const updated = deleteSavedAnimationSequence(id);
      setSequences(updated);
    }
  };

  const filteredSequences = useMemo(() => {
    return sequences.filter((seq) => {
      const matchesSearch =
        searchTerm.trim() === '' ||
        seq.poseName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        seq.poseId.toLowerCase().includes(searchTerm.toLowerCase()) ||
        seq.angleId.toLowerCase().includes(searchTerm.toLowerCase()) ||
        seq.folderSlug.toLowerCase().includes(searchTerm.toLowerCase());

      const matchesAngle =
        selectedAngleFilter === 'all' || seq.angleDeg === selectedAngleFilter;

      return matchesSearch && matchesAngle;
    });
  }, [sequences, searchTerm, selectedAngleFilter]);

  if (!isOpen) return null;

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 10000,
        background: 'rgba(0, 0, 0, 0.78)',
        backdropFilter: 'blur(6px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 16,
      }}
      onClick={onClose}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          width: '100%',
          maxWidth: 820,
          maxHeight: '85vh',
          background: 'linear-gradient(180deg, #0f172a 0%, #090d16 100%)',
          border: '1px solid rgba(56, 189, 248, 0.3)',
          borderRadius: 12,
          boxShadow: '0 20px 50px rgba(0,0,0,0.8), 0 0 30px rgba(56, 189, 248, 0.15)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
          color: '#f8fafc',
        }}
      >
        {/* Modal Header */}
        <div
          style={{
            padding: '12px 18px',
            borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            background: 'rgba(15, 23, 42, 0.95)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div
              style={{
                width: 32,
                height: 32,
                borderRadius: 8,
                background: 'rgba(56, 189, 248, 0.15)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#38bdf8',
              }}
            >
              <FolderOpen size={18} />
            </div>
            <div>
              <div style={{ fontSize: 13, fontWeight: 800, color: '#38bdf8', letterSpacing: '0.3px' }}>
                DANH SÁCH HOẠT ẢNH ĐÃ TẠO & ĐÃ LƯU
              </div>
              <div style={{ fontSize: 10.5, color: '#94a3b8' }}>
                Chọn một hoạt ảnh để nạp và hiển thị toàn bộ chuỗi frame lên sân khấu làm việc
              </div>
            </div>
          </div>

          <button
            onClick={onClose}
            style={{
              background: 'rgba(255, 255, 255, 0.05)',
              border: '1px solid rgba(255, 255, 255, 0.1)',
              color: '#94a3b8',
              borderRadius: 6,
              width: 28,
              height: 28,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
            }}
          >
            <X size={15} />
          </button>
        </div>

        {/* Search & Filter Bar */}
        <div
          style={{
            padding: '10px 18px',
            background: 'rgba(2, 6, 23, 0.5)',
            borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
            display: 'flex',
            gap: 10,
            flexWrap: 'wrap',
            alignItems: 'center',
          }}
        >
          {/* Search Box */}
          <div style={{ position: 'relative', flex: '1 1 240px', minWidth: 200 }}>
            <Search
              size={14}
              style={{
                position: 'absolute',
                left: 10,
                top: '50%',
                transform: 'translateY(-50%)',
                color: '#64748b',
              }}
            />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="Tìm theo tên động tác (chém kiếm, đả tọa, chạy...)..."
              style={{
                width: '100%',
                height: 32,
                padding: '4px 10px 4px 30px',
                fontSize: 11.5,
                background: 'rgba(15, 23, 42, 0.8)',
                border: '1px solid rgba(56, 189, 248, 0.25)',
                borderRadius: 6,
                color: '#f8fafc',
                outline: 'none',
              }}
            />
          </div>

          {/* Angle Filter Chips */}
          <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
            {['all', 0, 45, 90, 135, 180, 225, 270, 315].map((angle) => {
              const isActive = selectedAngleFilter === angle;
              return (
                <button
                  key={String(angle)}
                  onClick={() => setSelectedAngleFilter(angle as number | 'all')}
                  style={{
                    padding: '4px 8px',
                    borderRadius: 5,
                    fontSize: 10,
                    fontWeight: 700,
                    cursor: 'pointer',
                    background: isActive ? 'rgba(56, 189, 248, 0.25)' : 'rgba(255,255,255,0.04)',
                    border: isActive ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.08)',
                    color: isActive ? '#38bdf8' : '#94a3b8',
                  }}
                >
                  {angle === 'all' ? 'Tất cả góc' : `${angle}°`}
                </button>
              );
            })}
          </div>
        </div>

        {/* Modal Content / List of Animation Sequences */}
        <div
          style={{
            flex: 1,
            overflowY: 'auto',
            padding: 18,
            display: 'flex',
            flexDirection: 'column',
            gap: 12,
          }}
        >
          {filteredSequences.length === 0 ? (
            <div
              style={{
                textAlign: 'center',
                padding: '40px 20px',
                color: '#64748b',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: 10,
              }}
            >
              <Sparkles size={32} style={{ color: '#38bdf8', opacity: 0.5 }} />
              <div style={{ fontSize: 13, fontWeight: 700, color: '#e2e8f0' }}>
                {sequences.length === 0
                  ? 'Chưa có hoạt ảnh nào được lưu vào danh sách'
                  : 'Không tìm thấy hoạt ảnh phù hợp với bộ lọc'}
              </div>
              <div style={{ fontSize: 11, maxWidth: 420 }}>
                {sequences.length === 0
                  ? 'Sau khi cắt và chỉnh sửa các khung hình ở Tab 1.2, bấm nút "Lưu Động Tác" để lưu lại hoạt ảnh vào danh mục dự án của bạn!'
                  : 'Hãy thử đổi từ khóa tìm kiếm hoặc bấm "Tất cả góc".'}
              </div>
            </div>
          ) : (
            filteredSequences.map((seq) => {
              const frameList = seq.frames || [];
              const totalDurationSec = frameList.reduce((acc, f) => acc + (f.durationMs || 500), 0) / 1000;

              return (
                <div
                  key={seq.id}
                  onClick={() => {
                    onSelectSequence(seq);
                    onClose();
                  }}
                  style={{
                    background: 'rgba(15, 23, 42, 0.75)',
                    border: '1px solid rgba(255, 255, 255, 0.08)',
                    borderRadius: 10,
                    padding: 12,
                    display: 'flex',
                    flexDirection: 'column',
                    gap: 10,
                    cursor: 'pointer',
                    transition: 'all 0.15s ease',
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.borderColor = 'rgba(56, 189, 248, 0.5)';
                    e.currentTarget.style.background = 'rgba(15, 23, 42, 0.95)';
                    e.currentTarget.style.transform = 'translateY(-1px)';
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.borderColor = 'rgba(255, 255, 255, 0.08)';
                    e.currentTarget.style.background = 'rgba(15, 23, 42, 0.75)';
                    e.currentTarget.style.transform = 'none';
                  }}
                >
                  {/* Card Header */}
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span style={{ fontSize: 13, fontWeight: 800, color: '#f8fafc' }}>
                        {seq.poseName}
                      </span>
                      <span
                        style={{
                          fontSize: 10,
                          fontWeight: 700,
                          padding: '2px 7px',
                          borderRadius: 4,
                          background: 'rgba(56, 189, 248, 0.15)',
                          color: '#38bdf8',
                          border: '1px solid rgba(56, 189, 248, 0.3)',
                        }}
                      >
                        🎯 Góc {seq.angleDeg}° ({seq.angleId})
                      </span>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <span style={{ fontSize: 9.5, color: '#64748b', display: 'flex', alignItems: 'center', gap: 4 }}>
                        <Calendar size={11} /> {seq.createdAt ? new Date(seq.createdAt).toLocaleDateString('vi-VN') : 'Gần đây'}
                      </span>
                      <button
                        onClick={(e) => handleDelete(e, seq.id)}
                        title="Xóa hoạt ảnh này"
                        style={{
                          background: 'rgba(239, 68, 68, 0.15)',
                          border: '1px solid rgba(239, 68, 68, 0.3)',
                          color: '#f87171',
                          borderRadius: 5,
                          padding: '3px 6px',
                          cursor: 'pointer',
                          display: 'flex',
                          alignItems: 'center',
                        }}
                      >
                        <Trash2 size={12} />
                      </button>
                    </div>
                  </div>

                  {/* Card Body: Info & Mini Filmstrip Thumbnails */}
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
                    {/* Mini Thumbnails Strip */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, overflowX: 'auto', padding: '2px 0' }}>
                      {frameList.slice(0, 8).map((f, fIdx) => (
                        <div
                          key={f.id || fIdx}
                          style={{
                            width: 48,
                            height: 48,
                            borderRadius: 6,
                            background: '#040711',
                            border: '1px solid rgba(255,255,255,0.08)',
                            overflow: 'hidden',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            flexShrink: 0,
                          }}
                        >
                          <img
                            src={f.transparentDataUrl || f.originalDataUrl}
                            alt={`Frame ${fIdx + 1}`}
                            style={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }}
                          />
                        </div>
                      ))}
                      {frameList.length > 8 && (
                        <span style={{ fontSize: 10, color: '#94a3b8', fontWeight: 600 }}>
                          +{frameList.length - 8}
                        </span>
                      )}
                    </div>

                    {/* Stats & Load Action Button */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexShrink: 0 }}>
                      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', fontSize: 10, color: '#94a3b8' }}>
                        <span style={{ color: '#4ade80', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 3 }}>
                          <Layers size={11} /> {frameList.length} Frames
                        </span>
                        <span style={{ display: 'flex', alignItems: 'center', gap: 3 }}>
                          <Clock size={11} /> {totalDurationSec.toFixed(2)}s • {seq.fps || 8} FPS
                        </span>
                      </div>

                      <button
                        onClick={() => {
                          onSelectSequence(seq);
                          onClose();
                        }}
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: 5,
                          padding: '6px 14px',
                          borderRadius: 6,
                          background: 'linear-gradient(135deg, #0284c7, #38bdf8)',
                          border: 'none',
                          color: '#ffffff',
                          fontSize: 11,
                          fontWeight: 700,
                          cursor: 'pointer',
                          boxShadow: '0 2px 10px rgba(56, 189, 248, 0.35)',
                        }}
                      >
                        <Play size={12} fill="#ffffff" /> Nạp Hoạt Ảnh
                      </button>
                    </div>
                  </div>
                </div>
              );
            })
          )}
        </div>
      </div>
    </div>
  );
};
