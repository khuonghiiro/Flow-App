import React, { useState } from 'react';
import { X, Volume2, Sparkles, Download, FileText, CheckCircle2 } from 'lucide-react';
import { MasterSceneConfig, DialogueManifestItem } from '../types/scene';
import { TTSBatchGenerator } from '../core/audio_tts/TTSBatchGenerator';
import { AudioAutoFiller } from '../core/audio_tts/AudioAutoFiller';
import { SubtitleSRTExporter } from '../core/subtitles/SubtitleSRTExporter';
import { WebSpeechPreviewer } from '../core/audio_tts/WebSpeechPreviewer';

interface DialogueEditorModalProps {
  scene: MasterSceneConfig;
  isOpen: boolean;
  onClose: () => void;
  onUpdateScene: (updated: MasterSceneConfig) => void;
}

export const DialogueEditorModal: React.FC<DialogueEditorModalProps> = ({
  scene,
  isOpen,
  onClose,
  onUpdateScene,
}) => {
  const [isRenderingTTS, setIsRenderingTTS] = useState(false);
  const [progressMsg, setProgressMsg] = useState('');

  if (!isOpen) return null;

  const handleBatchRenderTTS = async () => {
    setIsRenderingTTS(true);
    setProgressMsg('Đang khởi tạo Web Audio Synthesis & phân tích Viseme...');

    try {
      const results = await TTSBatchGenerator.batchGenerate(
        scene.dialogues_manifest || [],
        (current, total) => {
          setProgressMsg(`Đang sinh file âm thanh (${current}/${total})...`);
        }
      );

      const updated = AudioAutoFiller.autoFillSceneAudio(scene, results);
      onUpdateScene(updated);
      setProgressMsg('Đã render hoàn tất và điền audio_path tự động!');
    } catch (err) {
      console.error(err);
      setProgressMsg('Có lỗi trong quá trình render TTS.');
    } finally {
      setIsRenderingTTS(false);
    }
  };

  const handleExportSRT = () => {
    const srt = SubtitleSRTExporter.exportToSRT(scene);
    SubtitleSRTExporter.downloadFile(`${scene.scene_id}_subtitles.srt`, srt);
  };

  const handleExportVTT = () => {
    const vtt = SubtitleSRTExporter.exportToVTT(scene);
    SubtitleSRTExporter.downloadFile(`${scene.scene_id}_subtitles.vtt`, vtt, 'text/vtt');
  };

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <FileText size={18} color="#818cf8" />
            <h3 style={{ fontSize: 16, fontWeight: 700, color: '#f8fafc' }}>
              Quản Lý Lời Thoại & TTS Pipeline
            </h3>
          </div>
          <button className="btn-icon" onClick={onClose}>
            <X size={16} />
          </button>
        </div>

        <div className="modal-body">
          <div style={{ fontSize: 12, color: '#94a3b8', lineHeight: 1.5 }}>
            Theo quy tắc thiết kế, kịch bản thoại khởi tạo với <code>audio_path: null</code>. Bạn có thể bấm nút <strong>Render TTS Tự Động</strong> để sinh file âm thanh, đo độ dài thực tế và điền path vào Master JSON.
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            {(scene.dialogues_manifest || []).map((item: DialogueManifestItem) => (
              <div key={item.line_id} className="ui-card" style={{ padding: 12 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <span
                      style={{
                        width: 8,
                        height: 8,
                        borderRadius: '50%',
                        backgroundColor: item.speaker_color || '#eab308',
                      }}
                    />
                    <strong style={{ fontSize: 12, color: item.speaker_color }}>
                      {item.speaker_name}
                    </strong>
                    <span style={{ fontSize: 11, color: '#64748b' }}>({item.line_id})</span>
                  </div>

                  <span
                    style={{
                      fontSize: 11,
                      padding: '2px 6px',
                      borderRadius: 4,
                      background: item.status === 'ready' ? 'rgba(16, 185, 129, 0.15)' : 'rgba(234, 179, 8, 0.15)',
                      color: item.status === 'ready' ? '#10b981' : '#eab308',
                      fontWeight: 600,
                    }}
                  >
                    {item.status === 'ready' ? '✓ Đã có Audio' : '⏳ Chờ TTS'}
                  </span>
                </div>

                <div style={{ fontSize: 13, color: '#f1f5f9' }}>{item.text}</div>

                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 11, color: '#94a3b8' }}>
                  <div>
                    Audio Path: <code style={{ color: item.audio_path ? '#38bdf8' : '#eab308' }}>
                      {item.audio_path || 'null (chưa gán)'}
                    </code>
                  </div>
                  <button
                    className="btn-secondary"
                    style={{ padding: '2px 8px', fontSize: 11 }}
                    onClick={() => WebSpeechPreviewer.preview(item)}
                  >
                    <Volume2 size={12} /> Đọc Thử
                  </button>
                </div>
              </div>
            ))}
          </div>

          {progressMsg && (
            <div
              style={{
                fontSize: 12,
                padding: '8px 12px',
                borderRadius: 6,
                background: 'rgba(99, 102, 241, 0.12)',
                color: '#818cf8',
                border: '1px solid rgba(99, 102, 241, 0.3)',
              }}
            >
              {progressMsg}
            </div>
          )}
        </div>

        <div className="modal-footer" style={{ justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn-secondary" onClick={handleExportSRT}>
              <Download size={13} /> Xuất .SRT
            </button>
            <button className="btn-secondary" onClick={handleExportVTT}>
              <Download size={13} /> Xuất .VTT
            </button>
          </div>

          <button
            className="btn-primary"
            onClick={handleBatchRenderTTS}
            disabled={isRenderingTTS}
          >
            <Sparkles size={14} /> {isRenderingTTS ? 'Đang Render...' : 'Render TTS Tự Động'}
          </button>
        </div>
      </div>
    </div>
  );
};
