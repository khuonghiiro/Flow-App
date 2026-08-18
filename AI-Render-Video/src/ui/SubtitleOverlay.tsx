import React from 'react';
import { ActiveSubtitle } from '../core/subtitles/SubtitleSynchronizer';
import { SubtitlesConfig } from '../types/scene';

interface SubtitleOverlayProps {
  subtitle: ActiveSubtitle | null;
  config: SubtitlesConfig;
  showCC: boolean;
}

export const SubtitleOverlay: React.FC<SubtitleOverlayProps> = ({ subtitle, config, showCC }) => {
  if (!showCC || !config.enable_overlay || !subtitle) {
    return null;
  }

  return (
    <div className="subtitle-overlay-container">
      {config.show_speaker_name && (
        <div className="subtitle-badge" style={{ color: subtitle.speaker_color }}>
          <span
            style={{
              width: 8,
              height: 8,
              borderRadius: '50%',
              backgroundColor: subtitle.speaker_color,
            }}
          />
          {subtitle.speaker_name}
        </div>
      )}
      <div
        className="subtitle-text-box"
        style={{
          fontSize: `${config.font_size || 20}px`,
          color: config.text_color || '#ffffff',
        }}
      >
        {subtitle.text}
      </div>
    </div>
  );
};
