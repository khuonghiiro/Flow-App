// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video AI Background Removal Custom Hook
// =========================================================================================
import { useState, useCallback } from 'react';
import { VideoSliceFrame } from '../../../../types/video_slicer';
import { getAIMattingApiUrl } from '../../../../core/config/envConfig';

export interface UseVideoAIMattingProps {
  frames: VideoSliceFrame[];
  setFrames: React.Dispatch<React.SetStateAction<VideoSliceFrame[]>>;
  aiModel?: string;
}

export function useVideoAIMatting({
  frames,
  setFrames,
  aiModel = 'birefnet-general',
}: UseVideoAIMattingProps) {
  const [isAIMattingRunning, setIsAIMattingRunning] = useState<boolean>(false);
  const [mattingProgress, setMattingProgress] = useState<number>(0);
  const [mattingStatusText, setMattingStatusText] = useState<string>('');

  /**
   * Runs AI Background Removal on all current video frames
   */
  const handleRunBatchAIMatting = useCallback(async () => {
    if (frames.length === 0) return;

    // Check server health
    try {
      const ping = await fetch(getAIMattingApiUrl('/api/status'));
      if (!ping.ok) throw new Error('offline');
    } catch {
      alert('Chưa kết nối được Server AI! Vui lòng khởi động file run_ai_matting_server.bat');
      return;
    }

    setIsAIMattingRunning(true);
    setMattingProgress(0);
    setMattingStatusText(`Đang tách nền ${frames.length} frame bằng AI model '${aiModel}'...`);

    try {
      const imagesToProcess = frames.map((f) => f.originalDataUrl);

      // Attempt fast Batch endpoint
      const batchRes = await fetch(getAIMattingApiUrl('/api/video/remove-bg-batch'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          images: imagesToProcess,
          model: aiModel,
        }),
      });

      if (batchRes.ok) {
        const data = await batchRes.json();
        if (data.success && data.results && data.results.length === frames.length) {
          setFrames((prev) =>
            prev.map((f, idx) => ({
              ...f,
              transparentDataUrl: data.results[idx] || f.transparentDataUrl,
            }))
          );
          setMattingProgress(100);
          setMattingStatusText(`✓ Đã tách nền AI thành công cho toàn bộ ${frames.length} frame!`);
          return;
        }
      }

      // Sequential fallback if batch fails
      const updatedFrames = [...frames];
      for (let i = 0; i < frames.length; i++) {
        setMattingStatusText(`Đang tách nền AI frame ${i + 1}/${frames.length}...`);
        const res = await fetch(getAIMattingApiUrl('/api/remove-bg'), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            image: frames[i].originalDataUrl,
            model: aiModel,
          }),
        });
        const resData = await res.json();
        if (resData.success && resData.result) {
          updatedFrames[i] = {
            ...updatedFrames[i],
            transparentDataUrl: resData.result,
          };
        }
        setMattingProgress(Math.round(((i + 1) / frames.length) * 100));
      }

      setFrames(updatedFrames);
      setMattingStatusText(`✓ Đã hoàn tất tách nền AI cho ${frames.length} frame!`);
    } catch (err: any) {
      alert(`Lỗi khi tách nền AI video: ${err.message}`);
    } finally {
      setIsAIMattingRunning(false);
    }
  }, [frames, setFrames, aiModel]);

  return {
    isAIMattingRunning,
    mattingProgress,
    mattingStatusText,
    handleRunBatchAIMatting,
  };
}
