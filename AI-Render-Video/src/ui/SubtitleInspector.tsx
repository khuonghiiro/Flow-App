import React from 'react';
import { Eye, EyeOff, Volume2, Clock, CheckCircle2, AlertCircle, Camera, Sparkles, RotateCcw } from 'lucide-react';
import { MasterSceneConfig, DialogueManifestItem } from '../types/scene';
import { WebSpeechPreviewer } from '../core/audio_tts/WebSpeechPreviewer';
import { InspectCameraAngle } from '../core/camera/CameraFraming';

interface SubtitleInspectorProps {
  scene: MasterSceneConfig;
  currentTime: number;
  inspectAngle: InspectCameraAngle;
  inspectingActorId: string | null;
  onChangeInspectAngle: (angle: InspectCameraAngle) => void;
  onInspectDialogue: (dialogue: DialogueManifestItem) => void;
  onResetCamera: () => void;
  onPreviewSpeech: (dialogue: DialogueManifestItem) => void;
}

export const SubtitleInspector: React.FC<SubtitleInspectorProps> = ({
  scene,
  currentTime,
  inspectAngle,
  inspectingActorId,
  onChangeInspectAngle,
  onInspectDialogue,
  onResetCamera,
  onPreviewSpeech,
}) => {
  const dialogues = scene.dialogues_manifest || [];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <span style={{ fontSize: 13, fontWeight: 700, color: '#f8fafc' }}>
          Danh Sách Thoại ({dialogues.length})
        </span>

        {inspectingActorId ? (
          <button
            className="btn-secondary active"
            style={{
              padding: '3px 8px',
              fontSize: 10,
              backgroundColor: 'rgba(239, 68, 68, 0.2)',
              borderColor: '#f87171',
              color: '#fca5a5',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
            onClick={onResetCamera}
            title="Khôi phục góc quay đạo diễn ban đầu"
          >
            <RotateCcw size={11} /> Khôi Phục Cam
          </button>
        ) : (
          <span style={{ fontSize: 11, color: '#94a3b8' }}>Bấm để soi Face Close-Up</span>
        )}
      </div>

      {/* Multi-angle Inspect Selector */}
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          gap: 6,
          background: 'rgba(15, 23, 42, 0.65)',
          padding: 8,
          borderRadius: 8,
          border: '1px solid var(--border-glow)',
        }}
      >
        <div style={{ fontSize: 11, fontWeight: 600, color: '#38bdf8', display: 'flex', alignItems: 'center', gap: 4 }}>
          <Camera size={12} /> Góc Soi Khẩu Hình:
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 4 }}>
          {[
            { id: 'front', label: '🎯 Chính Diện' },
            { id: 'three_quarter', label: '📐 Nghiêng 40°' },
            { id: 'side', label: '➡️ Ngang 90°' },
            { id: 'low_angle', label: '🔼 Góc Ngước' },
          ].map((item) => (
            <button
              key={item.id}
              className={`btn-secondary ${inspectAngle === item.id ? 'active' : ''}`}
              style={{
                padding: '4px 6px',
                fontSize: 10,
                backgroundColor: inspectAngle === item.id ? 'rgba(56, 189, 248, 0.25)' : undefined,
                borderColor: inspectAngle === item.id ? '#38bdf8' : undefined,
                color: inspectAngle === item.id ? '#38bdf8' : '#cbd5e1',
                fontWeight: inspectAngle === item.id ? 700 : 400,
              }}
              onClick={() => onChangeInspectAngle(item.id as InspectCameraAngle)}
            >
              {item.label}
            </button>
          ))}
        </div>
        <div style={{ fontSize: 9.5, color: '#94a3b8', display: 'flex', alignItems: 'center', gap: 3 }}>
          <Sparkles size={10} color="#10b981" /> Tự làm trong suốt tán cây (Genshin X-Ray) khi bị che
        </div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {dialogues.map((dlg) => {
          const duration = dlg.actual_duration || dlg.estimated_duration || 3.0;
          const isActive = currentTime >= dlg.start_time && currentTime <= dlg.start_time + duration;
          const isBeingInspected = inspectingActorId === dlg.speaker_id;

          return (
            <div
              key={dlg.line_id}
              className={`dialogue-item ${isActive ? 'active' : ''}`}
              onClick={() => onInspectDialogue(dlg)}
            >
              <div className="dialogue-header">
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span
                    style={{
                      width: 8,
                      height: 8,
                      borderRadius: '50%',
                      backgroundColor: dlg.speaker_color || '#eab308',
                    }}
                  />
                  <span style={{ fontSize: 12, fontWeight: 700, color: dlg.speaker_color }}>
                    {dlg.speaker_name}
                  </span>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span style={{ fontSize: 11, color: '#94a3b8', display: 'flex', alignItems: 'center', gap: 3 }}>
                    <Clock size={11} /> {dlg.start_time.toFixed(1)}s
                  </span>
                  {dlg.status === 'ready' ? (
                    <CheckCircle2 size={13} color="#10b981" />
                  ) : (
                    <AlertCircle size={13} color="#eab308" />
                  )}
                </div>
              </div>

              <div className="dialogue-text">{dlg.text}</div>

              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  marginTop: 4,
                }}
              >
                <button
                  className={`btn-secondary ${isBeingInspected ? 'active' : ''}`}
                  style={{
                    padding: '3px 8px',
                    fontSize: 11,
                    backgroundColor: isBeingInspected ? 'rgba(56, 189, 248, 0.3)' : undefined,
                    borderColor: isBeingInspected ? '#38bdf8' : undefined,
                    color: isBeingInspected ? '#38bdf8' : undefined,
                    fontWeight: isBeingInspected ? 700 : 400,
                  }}
                  onClick={(e) => {
                    e.stopPropagation();
                    onInspectDialogue(dlg);
                  }}
                >
                  {isBeingInspected ? (
                    <>
                      <EyeOff size={12} /> Bỏ Soi (Khôi Phục)
                    </>
                  ) : (
                    <>
                      <Eye size={12} /> Soi Khẩu Hình
                    </>
                  )}
                </button>

                <button
                  className="btn-secondary"
                  style={{ padding: '3px 8px', fontSize: 11 }}
                  onClick={(e) => {
                    e.stopPropagation();
                    onPreviewSpeech(dlg);
                    WebSpeechPreviewer.preview(dlg);
                  }}
                >
                  <Volume2 size={12} /> Nghe Thử
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
