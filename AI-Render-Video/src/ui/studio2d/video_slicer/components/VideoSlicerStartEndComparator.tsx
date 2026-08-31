// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Slicer Start & End Frame Thumbnail Cards (High-Resolution Aspect-Ratio Fit)
// =========================================================================================
import React, { useState, useEffect, useRef } from 'react';
import { MapPin, Eye } from 'lucide-react';
import { VideoCropBBox } from '../../../../types/video_slicer';

export interface FrameCardProps {
  type: 'start' | 'end';
  timestamp: number;
  thumbUrl: string | null;
  aspectRatio: number;
  onSeek: (t: number) => void;
  maxHeight?: number;
  maxWidth?: number;
}

export const VideoSlicerFrameCard: React.FC<FrameCardProps> = ({
  type,
  timestamp,
  thumbUrl,
  aspectRatio,
  onSeek,
  maxHeight = 400,
  maxWidth = 240,
}) => {
  const isStart = type === 'start';
  const borderColor = isStart ? '#0284c7' : '#dc2626';
  const badgeGradient = isStart
    ? 'linear-gradient(135deg, #0284c7, #0369a1)'
    : 'linear-gradient(135deg, #dc2626, #b91c1c)';

  return (
    <div
      onClick={() => onSeek(timestamp)}
      title={`Bấm để xem video tại mốc ${isStart ? 'Start' : 'End'} (${timestamp.toFixed(2)}s)`}
      style={{
        display: 'flex',
        flexDirection: 'column',
        background: '#070a12',
        border: `2px solid ${borderColor}`,
        borderRadius: 8,
        overflow: 'hidden',
        boxShadow: `0 4px 18px ${isStart ? 'rgba(2, 132, 199, 0.3)' : 'rgba(220, 38, 38, 0.3)'}`,
        cursor: 'pointer',
        maxHeight,
        maxWidth,
        flexShrink: 0,
        transition: 'transform 0.15s ease, box-shadow 0.15s ease',
      }}
      onMouseEnter={(e) => {
        e.currentTarget.style.transform = 'scale(1.02)';
      }}
      onMouseLeave={(e) => {
        e.currentTarget.style.transform = 'scale(1.0)';
      }}
    >
      {/* Top Header Badge */}
      <div
        style={{
          background: badgeGradient,
          color: '#fff',
          padding: '3px 8px',
          fontSize: 10,
          fontWeight: 800,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 6,
        }}
      >
        <span style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          <MapPin size={10} />
          {isStart ? 'GHIM START' : 'GHIM END'}
        </span>
        <span style={{ fontFamily: 'monospace', fontSize: 10, color: '#fef08a' }}>
          {timestamp.toFixed(2)}s
        </span>
      </div>

      {/* Frame Image Container with Proper Aspect-Ratio Fit */}
      <div
        style={{
          flex: 1,
          minHeight: 80,
          background: '#000',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          position: 'relative',
          overflow: 'hidden',
          padding: 2,
        }}
      >
        {thumbUrl ? (
          <img
            src={thumbUrl}
            alt={isStart ? 'Start Frame' : 'End Frame'}
            style={{
              maxWidth: '100%',
              maxHeight: maxHeight - 30,
              objectFit: 'contain',
              display: 'block',
              borderRadius: 4,
            }}
          />
        ) : (
          <div style={{ fontSize: 9, color: '#64748b', padding: 20 }}>Đang nạp ảnh...</div>
        )}

        <div
          style={{
            position: 'absolute',
            bottom: 4,
            right: 4,
            background: 'rgba(0, 0, 0, 0.7)',
            borderRadius: 3,
            padding: '2px 4px',
            fontSize: 8,
            color: '#94a3b8',
            display: 'flex',
            alignItems: 'center',
            gap: 2,
          }}
        >
          <Eye size={8} /> Xem
        </div>
      </div>
    </div>
  );
};

/**
 * Helper hook to capture high-res thumbnails for start & end timestamps
 */
export function useStartEndThumbnails(
  videoSourceUrl: string,
  startTime: number,
  endTime: number,
  activeBBox: VideoCropBBox | null
) {
  const [startThumb, setStartThumb] = useState<string | null>(null);
  const [endThumb, setEndThumb] = useState<string | null>(null);
  const [similarityScore, setSimilarityScore] = useState<number | null>(null);
  const hiddenVideoRef = useRef<HTMLVideoElement | null>(null);

  useEffect(() => {
    const video = document.createElement('video');
    video.muted = true;
    video.playsInline = true;
    video.preload = 'auto';
    video.src = videoSourceUrl;
    hiddenVideoRef.current = video;

    return () => {
      video.src = '';
      video.remove();
      hiddenVideoRef.current = null;
    };
  }, [videoSourceUrl]);

  useEffect(() => {
    let isCancelled = false;

    const timer = setTimeout(async () => {
      const video = hiddenVideoRef.current;
      if (!video || !videoSourceUrl) return;

      const capture = (targetTime: number): Promise<{ url: string; buffer: Uint8ClampedArray | null }> => {
        return new Promise((resolve) => {
          let timeoutId: any;
          const onSeeked = () => {
            video.removeEventListener('seeked', onSeeked);
            clearTimeout(timeoutId);

            const vw = video.videoWidth || 640;
            const vh = video.videoHeight || 480;

            let sx = 0, sy = 0, sw = vw, sh = vh;
            if (activeBBox && activeBBox.width > 10 && activeBBox.height > 10) {
              sx = Math.max(0, Math.min(activeBBox.x, vw - 10));
              sy = Math.max(0, Math.min(activeBBox.y, vh - 10));
              sw = Math.min(activeBBox.width, vw - sx);
              sh = Math.min(activeBBox.height, vh - sy);
            }

            const canvas = document.createElement('canvas');
            // Max dimension 320 for high-resolution crisp thumbnail
            const maxDim = 320;
            const scale = Math.min(1, maxDim / Math.max(sw, sh));
            canvas.width = Math.round(sw * scale);
            canvas.height = Math.round(sh * scale);

            const ctx = canvas.getContext('2d');
            if (!ctx) {
              resolve({ url: '', buffer: null });
              return;
            }

            ctx.drawImage(video, sx, sy, sw, sh, 0, 0, canvas.width, canvas.height);
            const url = canvas.toDataURL('image/jpeg', 0.9);
            const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            resolve({ url, buffer: imgData.data });
          };

          video.addEventListener('seeked', onSeeked, { once: true });
          video.currentTime = targetTime;

          timeoutId = setTimeout(() => {
            video.removeEventListener('seeked', onSeeked);
            resolve({ url: '', buffer: null });
          }, 200);
        });
      };

      try {
        const [startRes, endRes] = await Promise.all([
          capture(startTime),
          capture(endTime),
        ]);

        if (!isCancelled) {
          setStartThumb(startRes.url);
          setEndThumb(endRes.url);

          if (startRes.buffer && endRes.buffer) {
            let totalDiff = 0;
            const compareLen = Math.min(startRes.buffer.length, endRes.buffer.length);
            const pixels = compareLen / 4;

            for (let i = 0; i < compareLen; i += 4) {
              const dr = Math.abs(startRes.buffer[i] - endRes.buffer[i]);
              const dg = Math.abs(startRes.buffer[i + 1] - endRes.buffer[i + 1]);
              const db = Math.abs(startRes.buffer[i + 2] - endRes.buffer[i + 2]);
              totalDiff += (dr + dg + db) / 3;
            }

            const avgDiff = totalDiff / pixels;
            const sim = Math.max(0, Math.round((1 - avgDiff / 255) * 100));
            setSimilarityScore(sim);
          }
        }
      } catch (err) {
        console.warn('Error extracting start/end thumbnails:', err);
      }
    }, 80);

    return () => {
      isCancelled = true;
      clearTimeout(timer);
    };
  }, [videoSourceUrl, startTime, endTime, activeBBox]);

  return { startThumb, endThumb, similarityScore };
}
