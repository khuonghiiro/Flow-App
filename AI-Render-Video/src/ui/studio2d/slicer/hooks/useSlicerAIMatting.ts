import { useState, useCallback } from 'react';

export interface UseSlicerAIMattingProps {
  loadedImageRef: React.MutableRefObject<HTMLImageElement | null>;
  aiModel: string;
  setUserUploadedImageUrl: (url: string) => void;
  onAutoSliceAndAssemble: () => void;
}

export function useSlicerAIMatting({
  loadedImageRef,
  aiModel,
  setUserUploadedImageUrl,
  onAutoSliceAndAssemble,
}: UseSlicerAIMattingProps) {
  const [isAIRunning, setIsAIRunning] = useState<boolean>(false);

  const handleRunAIMatting = useCallback(async () => {
    const img = loadedImageRef.current;
    if (!img) return;

    try {
      const ping = await fetch('http://127.0.0.1:5000/api/status');
      if (!ping.ok) throw new Error('offline');
    } catch {
      alert('Chưa kết nối được Server AI! Vui lòng khởi động file run_ai_matting_server.bat');
      return;
    }

    setIsAIRunning(true);
    try {
      const fullCanvas = document.createElement('canvas');
      fullCanvas.width = img.width;
      fullCanvas.height = img.height;
      const fCtx = fullCanvas.getContext('2d');
      if (!fCtx) throw new Error('Không thể tạo canvas');
      fCtx.drawImage(img, 0, 0);

      const response = await fetch('http://127.0.0.1:5000/api/remove-bg', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          image: fullCanvas.toDataURL('image/png'),
          model: aiModel,
          alpha_matting: false,
        }),
      });
      const data = await response.json();
      if (!data.success || !data.result) throw new Error(data.error || 'Lỗi server');

      const cleanFullImg = new Image();
      await new Promise<void>((resolve, reject) => {
        cleanFullImg.onload = () => resolve();
        cleanFullImg.onerror = reject;
        cleanFullImg.src = data.result;
      });

      loadedImageRef.current = cleanFullImg;
      setUserUploadedImageUrl(data.result);
      onAutoSliceAndAssemble();
    } catch (err: any) {
      alert('Lỗi AI Matting: ' + err.message);
    } finally {
      setIsAIRunning(false);
    }
  }, [loadedImageRef, aiModel, setUserUploadedImageUrl, onAutoSliceAndAssemble]);

  return {
    isAIRunning,
    handleRunAIMatting,
  };
}
