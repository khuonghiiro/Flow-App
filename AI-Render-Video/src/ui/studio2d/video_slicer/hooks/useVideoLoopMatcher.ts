// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Video Loop Matcher Custom Hook (Customizable Search Duration)
// =========================================================================================
import { useState, useRef, useCallback } from 'react';
import { VideoCropBBox } from '../../../../types/video_slicer';
import { findBestLoopEndFrame } from '../utils/videoLoopMatcher';

export function useVideoLoopMatcher() {
  const [isAutoFindEnd, setIsAutoFindEnd] = useState<boolean>(true);
  const [maxSearchDuration, setMaxSearchDuration] = useState<number>(3.0);
  const [isSearchingEnd, setIsSearchingEnd] = useState<boolean>(false);
  const [searchProgress, setSearchProgress] = useState<number>(0);
  const [searchStatusText, setSearchStatusText] = useState<string>('');

  const isCancelledRef = useRef<boolean>(false);

  /**
   * Stops/cancels ongoing search immediately
   */
  const handleStopSearch = useCallback(() => {
    isCancelledRef.current = true;
    setIsSearchingEnd(false);
    setSearchStatusText('Đã dừng quét tìm.');
  }, []);

  /**
   * Executes the loop search for the best matching end frame within user-configured seconds
   */
  const handleTriggerSearchEnd = useCallback(
    async (
      videoSourceUrl: string,
      startTime: number,
      duration: number,
      bbox: VideoCropBBox | null,
      onFoundEnd: (endSec: number) => void,
      customMaxSearchSec?: number
    ) => {
      if (!videoSourceUrl || isSearchingEnd) return;

      const searchLimit = customMaxSearchSec ?? maxSearchDuration ?? 3.0;

      isCancelledRef.current = false;
      setIsSearchingEnd(true);
      setSearchProgress(0);
      setSearchStatusText(`Đang quét tìm frame khớp vòng lặp (${searchLimit}s)...`);

      try {
        const result = await findBestLoopEndFrame(videoSourceUrl, {
          startTime,
          videoDuration: duration,
          maxSearchSeconds: searchLimit,
          stepSeconds: 0.04,
          bbox,
          shouldCancel: () => isCancelledRef.current,
          onProgress: (currentSec, pct) => {
            setSearchProgress(pct);
            setSearchStatusText(`Đang quét ${(currentSec - startTime).toFixed(1)}s / ${searchLimit}s (${pct}%)...`);
          },
        });

        if (!isCancelledRef.current && result.bestTimestamp > startTime) {
          onFoundEnd(result.bestTimestamp);
          const matchPercent = Math.round(result.bestSimilarity * 100);
          setSearchStatusText(`✓ Đã tìm thấy Ghim End khớp loop ${matchPercent}% tại ${result.bestTimestamp}s!`);
        }
      } catch (err: any) {
        setSearchStatusText(`Lỗi quét frame: ${err.message}`);
      } finally {
        setIsSearchingEnd(false);
      }
    },
    [isSearchingEnd, maxSearchDuration]
  );

  /**
   * Automatically triggers search when user releases the Start Pin (if toggle is active)
   */
  const handleStartPinReleased = useCallback(
    (
      videoSourceUrl: string,
      newStartTime: number,
      duration: number,
      bbox: VideoCropBBox | null,
      onFoundEnd: (endSec: number) => void
    ) => {
      if (isAutoFindEnd) {
        handleTriggerSearchEnd(videoSourceUrl, newStartTime, duration, bbox, onFoundEnd);
      }
    },
    [isAutoFindEnd, handleTriggerSearchEnd]
  );

  return {
    isAutoFindEnd,
    setIsAutoFindEnd,
    maxSearchDuration,
    setMaxSearchDuration,
    isSearchingEnd,
    searchProgress,
    searchStatusText,
    handleTriggerSearchEnd,
    handleStopSearch,
    handleStartPinReleased,
  };
}
