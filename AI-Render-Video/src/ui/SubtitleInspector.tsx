import React from 'react';
import { Eye, Volume2, Clock, CheckCircle2, AlertCircle } from 'lucide-react';
import { MasterSceneConfig, DialogueManifestItem } from '../types/scene';
import { WebSpeechPreviewer } from '../core/audio_tts/WebSpeechPreviewer';

interface SubtitleInspectorProps {
  scene: MasterSceneConfig;
  currentTime: number;
  onInspectDialogue: (dialogue: DialogueManifestItem) => void;
  onPreviewSpeech: (dialogue: DialogueManifestItem) => void;
}

export const SubtitleInspector: React.FC<SubtitleInspectorProps> = ({
  scene,
  currentTime,
  onInspectDialogue,
  onPreviewSpeech,
}) => {
  const dialogues = scene.dialogues_manifest || [];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <span style={{ fontSize: 13, fontWeight: 700, color: '#f8fafc' }}>
          Danh Sách Thoại ({dialogues.length})
        </span>
        <span style={{ fontSize: 11, color: '#94a3b8' }}>Bấm để soi Face Close-Up</span>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {dialogues.map((dlg) => {
          const duration = dlg.actual_duration || dlg.estimated_duration || 3.0;
          const isActive = currentTime >= dlg.start_time && currentTime <= dlg.start_time + duration;

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
                  className="btn-secondary"
                  style={{ padding: '3px 8px', fontSize: 11 }}
                  onClick={(e) => {
                    e.stopPropagation();
                    onInspectDialogue(dlg);
                  }}
                >
                  <Eye size={12} /> Soi Khẩu Hình
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
