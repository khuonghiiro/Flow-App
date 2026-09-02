// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// High-Quality Animated GIF Exporter (Backend High-Res + Client Download)
// =========================================================================================
import { getAIMattingApiUrl } from '../../../../core/config/envConfig';

/**
 * Downloads a data URL as a file in the browser
 */
export function downloadDataUrl(dataUrl: string, filename: string): void {
  const link = document.createElement('a');
  link.href = dataUrl;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
}

/**
 * Exports an array of frame image URLs to an Animated GIF file
 */
export async function exportFramesToAnimatedGif(
  frameUrls: string[],
  fps: number = 12,
  customFilename?: string
): Promise<{ success: boolean; count: number; filename: string }> {
  if (!frameUrls || frameUrls.length === 0) {
    throw new Error('Danh sách khung hình trống, không thể xuất GIF');
  }

  const apiUrl = getAIMattingApiUrl('/api/video/export-gif');
  const filename = customFilename || `animation_${Date.now()}.gif`;

  const response = await fetch(apiUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      frames: frameUrls,
      fps: Math.max(1, fps),
    }),
  });

  if (!response.ok) {
    const errJson = await response.json().catch(() => ({}));
    throw new Error(errJson.error || 'Máy chủ không thể tạo file GIF');
  }

  const result = await response.json();
  if (!result.gif_data_url) {
    throw new Error('Không nhận được dữ liệu GIF từ máy chủ');
  }

  downloadDataUrl(result.gif_data_url, filename);

  return {
    success: true,
    count: frameUrls.length,
    filename,
  };
}
