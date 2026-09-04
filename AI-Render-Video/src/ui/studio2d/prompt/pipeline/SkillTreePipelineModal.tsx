import React, { useState, useEffect, useRef, useCallback } from 'react';
import {
  Play,
  Square,
  RefreshCw,
  CheckCircle2,
  AlertCircle,
  Clock,
  Cpu,
  Layers,
  Film,
  Image as ImageIcon,
  X,
  ExternalLink,
  RotateCw,
} from 'lucide-react';
import { PromptCustomizerValues } from '../types';
import { getFlowKitApiUrl } from '../../../../core/config/envConfig';

interface AngleInfo {
  media_id: string | null;
  url: string | null;
  status: 'PENDING' | 'RUNNING' | 'COMPLETED' | 'ERROR';
}

interface WorkerSlot {
  slot: number;
  task_id: string | null;
  name: string;
  status: 'IDLE' | 'GENERATING' | 'COMPLETED' | 'RETRYING';
  elapsed_s: number;
}

interface ActionTask {
  id: string;
  type: string;
  angle: string;
  name: string;
  status: 'PENDING' | 'RUNNING' | 'COMPLETED' | 'ERROR';
  retries: number;
  output_url: string | null;
  error: string | null;
}

interface PipelineStatusData {
  id: string;
  status: 'IDLE' | 'RUNNING' | 'COMPLETED' | 'ERROR' | 'CANCELLED';
  stage: 'INIT' | 'STAGE_1_ROOT' | 'STAGE_2_ANGLES' | 'STAGE_3_ACTIONS' | 'COMPLETED' | 'FAILED' | 'CANCELLED';
  project_id: string;
  error_message: string | null;
  angles: Record<string, AngleInfo>;
  slots: WorkerSlot[];
  tasks: ActionTask[];
}

interface SkillTreePipelineModalProps {
  isOpen: boolean;
  onClose: () => void;
  customizerValues: PromptCustomizerValues;
}

const ANGLE_LABELS: Record<string, { label: string; desc: string }> = {
  '0': { label: '0° Chính diện', desc: 'Ảnh Master Gốc (Identity Root)' },
  '45': { label: '45° Nghiêng trái', desc: 'Khóa theo Master 0°' },
  '90': { label: '90° Nhìn ngang', desc: 'Khóa theo Master 0°' },
  '135': { label: '135° Lưng phải', desc: 'Khóa theo Master 0°' },
  '180': { label: '180° Sau lưng', desc: 'Khóa theo Master 0°' },
};

export const SkillTreePipelineModal: React.FC<SkillTreePipelineModalProps> = ({
  isOpen,
  onClose,
  customizerValues,
}) => {
  const [pipelineId, setPipelineId] = useState<string | null>(null);
  const [pipelineData, setPipelineData] = useState<PipelineStatusData | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [errorBanner, setErrorBanner] = useState<string | null>(null);
  const [includeWalk, setIncludeWalk] = useState<boolean>(true);
  const pollTimerRef = useRef<NodeJS.Timeout | null>(null);

  const fetchStatus = useCallback(async (id: string) => {
    try {
      const res = await fetch(getFlowKitApiUrl(`/api/flow/pipeline/status/${id}`));
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data: PipelineStatusData = await res.json();
      setPipelineData(data);
      if (data.status === 'COMPLETED' || data.status === 'ERROR' || data.status === 'CANCELLED') {
        if (pollTimerRef.current) clearInterval(pollTimerRef.current);
      }
    } catch (err: any) {
      console.warn('Poll pipeline status error:', err);
    }
  }, []);

  useEffect(() => {
    if (pipelineId && isOpen) {
      fetchStatus(pipelineId);
      pollTimerRef.current = setInterval(() => fetchStatus(pipelineId), 1500);
    }
    return () => {
      if (pollTimerRef.current) clearInterval(pollTimerRef.current);
    };
  }, [pipelineId, isOpen, fetchStatus]);

  if (!isOpen) return null;

  const handleStartPipeline = async () => {
    setLoading(true);
    setErrorBanner(null);
    try {
      const actions: string[] = [];
      if (includeWalk) actions.push('walk');

      const res = await fetch(getFlowKitApiUrl('/api/flow/pipeline/start'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          customizer: customizerValues,
          actions,
        }),
      });

      if (!res.ok) {
        const errData = await res.json().catch(() => ({}));
        throw new Error(errData.detail || `Khởi tạo thất bại (${res.status})`);
      }

      const json = await res.json();
      setPipelineId(json.pipeline_id);
    } catch (err: any) {
      setErrorBanner(err.message || 'Không thể bắt đầu pipeline');
    } finally {
      setLoading(false);
    }
  };

  const handleCancelPipeline = async () => {
    if (!pipelineId) return;
    try {
      await fetch(getFlowKitApiUrl(`/api/flow/pipeline/cancel/${pipelineId}`), { method: 'POST' });
      fetchStatus(pipelineId);
    } catch (err: any) {
      console.error('Cancel error:', err);
    }
  };

  const isRunning = pipelineData?.status === 'RUNNING';
  const completedTasks = pipelineData?.tasks.filter((t) => t.status === 'COMPLETED') || [];
  const totalTasks = pipelineData?.tasks.length || 0;

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 9999,
        background: 'rgba(0, 0, 0, 0.82)',
        backdropFilter: 'blur(8px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 20,
      }}
    >
      <div
        style={{
          width: '95%',
          maxWidth: 1150,
          maxHeight: '92vh',
          background: 'linear-gradient(180deg, #0f172a 0%, #090d16 100%)',
          border: '1px solid rgba(168, 85, 247, 0.35)',
          borderRadius: 16,
          boxShadow: '0 25px 60px rgba(0,0,0,0.8), 0 0 35px rgba(168, 85, 247, 0.25)',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
          color: '#f8fafc',
        }}
      >
        {/* Header */}
        <div
          style={{
            padding: '16px 24px',
            background: 'linear-gradient(90deg, rgba(79, 70, 229, 0.25), rgba(168, 85, 247, 0.25))',
            borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <div
              style={{
                width: 40,
                height: 40,
                borderRadius: 10,
                background: 'linear-gradient(135deg, #ec4899, #8b5cf6)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                boxShadow: '0 0 16px rgba(236, 72, 153, 0.5)',
              }}
            >
              <Cpu size={22} color="#fff" />
            </div>
            <div>
              <h2 style={{ margin: 0, fontSize: 17, fontWeight: 800, letterSpacing: '0.3px' }}>
                ⚡ Tự Động Sinh Cây Kỹ Năng (Auto Skill Tree Pipeline)
              </h2>
              <p style={{ margin: '2px 0 0', fontSize: 11.5, color: '#94a3b8' }}>
                3 Giai đoạn tuần tự: Master 0° → Khóa 4 góc nhân vật → 5 Slot song song duy trì liên tục (4s Loop)
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            style={{
              background: 'rgba(255, 255, 255, 0.08)',
              border: 'none',
              borderRadius: 8,
              padding: 8,
              cursor: 'pointer',
              color: '#94a3b8',
            }}
          >
            <X size={18} />
          </button>
        </div>

        {/* Action Controls Bar */}
        <div
          style={{
            padding: '12px 24px',
            background: 'rgba(15, 23, 42, 0.6)',
            borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: 12,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={includeWalk}
                disabled={isRunning}
                onChange={(e) => setIncludeWalk(e.target.checked)}
                style={{ accentColor: '#a855f7' }}
              />
              <span style={{ fontWeight: 600 }}>Đi bộ (Walk Cycle 5 góc - 4s Loop)</span>
            </label>
            <span style={{ fontSize: 11, color: '#64748b' }}>
              Nhân vật: <strong style={{ color: '#38bdf8' }}>{customizerValues.characterName}</strong> ({customizerValues.style})
            </span>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            {isRunning ? (
              <button
                onClick={handleCancelPipeline}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 6,
                  padding: '8px 16px',
                  borderRadius: 8,
                  fontSize: 12,
                  fontWeight: 700,
                  cursor: 'pointer',
                  border: 'none',
                  background: 'linear-gradient(135deg, #ef4444, #dc2626)',
                  color: '#fff',
                  boxShadow: '0 0 14px rgba(239, 68, 68, 0.4)',
                }}
              >
                <Square size={14} /> Dừng Khẩn Cấp
              </button>
            ) : (
              <button
                onClick={handleStartPipeline}
                disabled={loading}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 6,
                  padding: '8px 20px',
                  borderRadius: 8,
                  fontSize: 12,
                  fontWeight: 700,
                  cursor: loading ? 'not-allowed' : 'pointer',
                  border: 'none',
                  background: 'linear-gradient(135deg, #8b5cf6, #d946ef)',
                  color: '#fff',
                  boxShadow: '0 0 20px rgba(168, 85, 247, 0.5)',
                  opacity: loading ? 0.6 : 1,
                }}
              >
                {loading ? <RotateCw size={14} className="animate-spin" /> : <Play size={14} />}
                {loading ? 'Đang khởi tạo...' : '⚡ Bắt đầu chạy tự động (5 slots)'}
              </button>
            )}

            {pipelineId && (
              <button
                onClick={() => fetchStatus(pipelineId)}
                title="Làm mới trạng thái"
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  padding: '8px 10px',
                  borderRadius: 8,
                  border: '1px solid rgba(255, 255, 255, 0.1)',
                  background: 'rgba(255, 255, 255, 0.05)',
                  color: '#cbd5e1',
                  cursor: 'pointer',
                }}
              >
                <RefreshCw size={14} />
              </button>
            )}
          </div>
        </div>

        {errorBanner && (
          <div
            style={{
              padding: '8px 24px',
              background: 'rgba(239, 68, 68, 0.15)',
              borderBottom: '1px solid rgba(239, 68, 68, 0.3)',
              color: '#f87171',
              fontSize: 12,
              display: 'flex',
              alignItems: 'center',
              gap: 8,
            }}
          >
            <AlertCircle size={14} /> {errorBanner}
          </div>
        )}

        {/* Scrollable Body */}
        <div style={{ flex: 1, overflowY: 'auto', padding: '18px 24px', display: 'flex', flexDirection: 'column', gap: 20 }}>
          {/* Stage 1 & 2: 5 Angle References */}
          <div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <Layers size={16} color="#38bdf8" />
                <span style={{ fontSize: 13, fontWeight: 700, color: '#e2e8f0' }}>
                  Giai đoạn 1 & 2: 5 Góc Cơ Thể Nhân Vật (Khóa Nhất Quán)
                </span>
              </div>
              <span style={{ fontSize: 11, color: '#94a3b8' }}>
                Stage: <strong style={{ color: '#a855f7' }}>{pipelineData?.stage || 'CHƯA BẮT ĐẦU'}</strong>
              </span>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 10 }}>
              {['0', '45', '90', '135', '180'].map((ang) => {
                const info = pipelineData?.angles?.[ang];
                const meta = ANGLE_LABELS[ang];
                const isMaster = ang === '0';
                return (
                  <div
                    key={ang}
                    style={{
                      background: 'rgba(30, 41, 59, 0.5)',
                      border: isMaster
                        ? '1px solid rgba(56, 189, 248, 0.5)'
                        : '1px solid rgba(255, 255, 255, 0.08)',
                      borderRadius: 10,
                      padding: 10,
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'center',
                      position: 'relative',
                    }}
                  >
                    {isMaster && (
                      <span
                        style={{
                          position: 'absolute',
                          top: 6,
                          right: 6,
                          fontSize: 9,
                          fontWeight: 800,
                          background: '#0284c7',
                          color: '#fff',
                          padding: '1px 5px',
                          borderRadius: 4,
                        }}
                      >
                        MASTER
                      </span>
                    )}

                    <div
                      style={{
                        width: '100%',
                        aspectRatio: '3/4',
                        borderRadius: 6,
                        background: '#00FF00',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        overflow: 'hidden',
                        marginBottom: 8,
                        boxShadow: 'inset 0 0 10px rgba(0,0,0,0.3)',
                      }}
                    >
                      {info?.url ? (
                        <img
                          src={info.url}
                          alt={meta.label}
                          style={{ width: '100%', height: '100%', objectFit: 'contain' }}
                        />
                      ) : (
                        <div style={{ textAlign: 'center', color: '#000', opacity: 0.6 }}>
                          <ImageIcon size={24} />
                          <div style={{ fontSize: 10, fontWeight: 700, marginTop: 4 }}>#00FF00</div>
                        </div>
                      )}
                    </div>

                    <div style={{ fontSize: 11.5, fontWeight: 700, color: '#f8fafc', textAlign: 'center' }}>
                      {meta.label}
                    </div>
                    <div style={{ fontSize: 10, color: '#94a3b8', textAlign: 'center', marginTop: 2 }}>
                      {meta.desc}
                    </div>

                    <div style={{ marginTop: 8 }}>
                      <StatusBadge status={info?.status || 'PENDING'} />
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Stage 3: 5 Concurrent Sliding Slots Monitor */}
          <div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <Cpu size={16} color="#a855f7" />
                <span style={{ fontSize: 13, fontWeight: 700, color: '#e2e8f0' }}>
                  Giai đoạn 3: 5 Slot Song Song Duy Trì Liên Tục (Sliding Window Queue)
                </span>
              </div>
              <span style={{ fontSize: 11, color: '#94a3b8' }}>
                Đã xong: <strong style={{ color: '#10b981' }}>{completedTasks.length}</strong> / {totalTasks}
              </span>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 10 }}>
              {(pipelineData?.slots || [0, 1, 2, 3, 4].map((i) => ({ slot: i, task_id: null, name: 'Chờ lệnh', status: 'IDLE' as const, elapsed_s: 0 }))).map((slot) => {
                const isActive = slot.status === 'GENERATING';
                return (
                  <div
                    key={slot.slot}
                    style={{
                      background: isActive
                        ? 'linear-gradient(180deg, rgba(168, 85, 247, 0.15), rgba(15, 23, 42, 0.8))'
                        : 'rgba(30, 41, 59, 0.35)',
                      border: isActive ? '1px solid #a855f7' : '1px solid rgba(255, 255, 255, 0.08)',
                      borderRadius: 10,
                      padding: 12,
                      display: 'flex',
                      flexDirection: 'column',
                      gap: 6,
                      boxShadow: isActive ? '0 0 16px rgba(168, 85, 247, 0.25)' : 'none',
                      transition: 'all 0.3s ease',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                      <span style={{ fontSize: 11, fontWeight: 800, color: '#38bdf8' }}>SLOT #{slot.slot + 1}</span>
                      <StatusBadge status={slot.status} />
                    </div>

                    <div
                      style={{
                        fontSize: 11,
                        fontWeight: 600,
                        color: slot.name === 'Idle' ? '#64748b' : '#f1f5f9',
                        minHeight: 32,
                        lineHeight: '16px',
                      }}
                    >
                      {slot.name}
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 10, color: '#94a3b8', marginTop: 'auto' }}>
                      <Clock size={11} /> {slot.elapsed_s > 0 ? `${slot.elapsed_s}s` : '--'}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Completed Tasks Output Gallery */}
          {completedTasks.length > 0 && (
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
                <Film size={16} color="#10b981" />
                <span style={{ fontSize: 13, fontWeight: 700, color: '#e2e8f0' }}>
                  Danh Sách Hoạt Ảnh Đã Hoàn Thành (4s Seamless Loops)
                </span>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))', gap: 10 }}>
                {completedTasks.map((task) => (
                  <div
                    key={task.id}
                    style={{
                      background: 'rgba(16, 185, 129, 0.08)',
                      border: '1px solid rgba(16, 185, 129, 0.3)',
                      borderRadius: 8,
                      padding: 10,
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: 8,
                    }}
                  >
                    <div>
                      <div style={{ fontSize: 11.5, fontWeight: 700, color: '#f8fafc' }}>{task.name}</div>
                      <div style={{ fontSize: 10, color: '#6ee7b7' }}>✓ Đã xong (Tự động lặp 4s)</div>
                    </div>
                    {task.output_url && (
                      <a
                        href={task.output_url}
                        target="_blank"
                        rel="noreferrer"
                        style={{
                          padding: '4px 8px',
                          borderRadius: 6,
                          background: 'rgba(16, 185, 129, 0.2)',
                          color: '#34d399',
                          fontSize: 10,
                          fontWeight: 700,
                          textDecoration: 'none',
                          display: 'flex',
                          alignItems: 'center',
                          gap: 4,
                        }}
                      >
                        Xem <ExternalLink size={10} />
                      </a>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

const StatusBadge: React.FC<{ status: string }> = ({ status }) => {
  switch (status) {
    case 'COMPLETED':
      return (
        <span style={{ fontSize: 9.5, fontWeight: 700, padding: '2px 6px', borderRadius: 4, background: 'rgba(16, 185, 129, 0.2)', color: '#34d399', border: '1px solid rgba(16, 185, 129, 0.4)' }}>
          ✓ XONG
        </span>
      );
    case 'RUNNING':
    case 'GENERATING':
      return (
        <span style={{ fontSize: 9.5, fontWeight: 700, padding: '2px 6px', borderRadius: 4, background: 'rgba(168, 85, 247, 0.2)', color: '#c084fc', border: '1px solid rgba(168, 85, 247, 0.4)' }}>
          ⏳ ĐANG TẠO
        </span>
      );
    case 'RETRYING':
      return (
        <span style={{ fontSize: 9.5, fontWeight: 700, padding: '2px 6px', borderRadius: 4, background: 'rgba(245, 158, 11, 0.2)', color: '#fbbf24', border: '1px solid rgba(245, 158, 11, 0.4)' }}>
          🔄 THỬ LẠI
        </span>
      );
    case 'ERROR':
      return (
        <span style={{ fontSize: 9.5, fontWeight: 700, padding: '2px 6px', borderRadius: 4, background: 'rgba(239, 68, 68, 0.2)', color: '#f87171', border: '1px solid rgba(239, 68, 68, 0.4)' }}>
          ✕ LỖI
        </span>
      );
    default:
      return (
        <span style={{ fontSize: 9.5, fontWeight: 600, padding: '2px 6px', borderRadius: 4, background: 'rgba(255, 255, 255, 0.06)', color: '#94a3b8' }}>
          CHỜ
        </span>
      );
  }
};
