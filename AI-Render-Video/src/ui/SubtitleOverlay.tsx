import React from 'react';
import { ActiveSubtitle } from '../core/subtitles/SubtitleSynchronizer';
import { SubtitlesConfig } from '../types/scene';

interface SubtitleOverlayProps {
  subtitle: ActiveSubtitle | null;
  config?: SubtitlesConfig | null;
  showCC: boolean;
}

export const SubtitleOverlay: React.FC<SubtitleOverlayProps> = ({ subtitle, config, showCC }) => {
  const isEnabled = config?.enable_overlay ?? true;
  if (!showCC || !isEnabled || !subtitle) {
    return null;
  }

  const showSpeaker = config?.show_speaker_name ?? true;
  const fontSize = config?.font_size || 20;
  const textColor = config?.text_color || '#ffffff';
  const speakerColor = subtitle.speaker_color || '#38bdf8';

  return (
    <div className="subtitle-overlay-container">
      {showSpeaker && subtitle.speaker_name && (
        <div className="subtitle-badge" style={{ color: speakerColor }}>
          <span
            style={{
              width: 8,
              height: 8,
              borderRadius: '50%',
              backgroundColor: speakerColor,
            }}
          />
          {subtitle.speaker_name}
        </div>
      )}
      <div
        className="subtitle-text-box"
        style={{
          fontSize: `${fontSize}px`,
          color: textColor,
        }}
      >
        {subtitle.text}
      </div>
    </div>
  );
};
