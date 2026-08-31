// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Loop Matcher Custom Hook (Manual Scan with Best Match Selection)
// =========================================================================================
import { useState, useCallback, useRef } from 'react';
import { VideoCropBBox } from '../../../../types/video_slicer';
import { findBestLoopEndFrame } from '../utils/videoLoopMatcher';

export function useVideoLoopMatcher() {
  const [isScanByBBox, setIsScanByBBox] = useState<boolean>(false);
  const [maxSearchDuration, setMaxSearchDuration] = useState<number>(3.0);
  const [isSearchingEnd, setIsSearchingEnd] = useState<boolean>(false);
  const [searchProgress, setSearchProgress] = useState<number>(0);
  const [searchStatusText, setSearchStatusText] = useState<string>('');

  const cancelSearchRef = useRef<boolean>(false);

  /**
   * Stops any currently running frame search immediately
   */
  const handleStopSearch = useCallback(() => {
    cancelSearchRef.current = true;
    setIsSearchingEnd(false);
    setSearchStatusText('Đã dừng tìm kiếm.');
  }, []);

  /**
   * Executes frame-by-frame similarity search starting from startTime
   * Picks the frame with highest similarity percentage upon completion
   */
  const handleTriggerSearchEnd = useCallback(
    async (
      videoSourceUrl: string,
      startTime: number,
      videoDuration: number,
      videoCropBBox: VideoCropBBox | null,
      onFoundEndFrame: (endTime: number) => void,
      customDuration?: number
    ) => {
      if (!videoSourceUrl || isSearchingEnd) return;

      cancelSearchRef.current = false;
      setIsSearchingEnd(true);
      setSearchProgress(0);

      const targetDuration = customDuration || maxSearchDuration || 3.0;
      setSearchStatusText(
        isScanByBBox && videoCropBBox
          ? `Đang quét theo BBox chi tiết (+${targetDuration}s)...`
          : `Đang quét từng frame (+${targetDuration}s)...`
      );

      try {
        const activeSearchBBox = isScanByBBox ? videoCropBBox : null;
        const result = await findBestLoopEndFrame(videoSourceUrl, {
          startTime,
          videoDuration,
          maxSearchSeconds: targetDuration,
          stepSeconds: 0.04,
          bbox: activeSearchBBox,
          shouldCancel: () => cancelSearchRef.current,
          onProgress: (sec, prog) => {
            setSearchProgress(prog);
            setSearchStatusText(
              isScanByBBox && videoCropBBox
                ? `Quét BBox: ${sec.toFixed(2)}s (${prog}%)`
                : `Quét frame: ${sec.toFixed(2)}s (${prog}%)`
            );
          },
        });

        if (!cancelSearchRef.current && result.bestTimestamp > startTime) {
          onFoundEndFrame(result.bestTimestamp);
          const matchPercent = Math.round(result.bestSimilarity * 100);
          setSearchStatusText(
            `✓ Đã chốt frame khớp cao nhất (${matchPercent}%) tại ${result.bestTimestamp.toFixed(2)}s!`
          );
          setTimeout(() => setSearchStatusText(''), 3500);
        }
      } catch (err: any) {
        setSearchStatusText(`Lỗi: ${err.message}`);
      } finally {
        setIsSearchingEnd(false);
      }
    },
    [isSearchingEnd, maxSearchDuration, isScanByBBox]
  );

  return {
    isScanByBBox,
    setIsScanByBBox,
    maxSearchDuration,
    setMaxSearchDuration,
    isSearchingEnd,
    searchProgress,
    searchStatusText,
    handleTriggerSearchEnd,
    handleStopSearch,
  };
}
