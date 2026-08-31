// =========================================================================================
// AI NOTICE: Refer to README.md and .agents/skills/flowmy-standards/SKILL.md before editing.
// Central Environment & Service Endpoint Configuration
// =========================================================================================

export interface AppEnvConfig {
  aiMattingUrl: string;
  sidecarUrl: string;
  ffmpegPath: string;
}

/**
 * Global Environment Configuration loaded from Vite .env
 */
export const ENV_CONFIG: AppEnvConfig = {
  aiMattingUrl: (import.meta.env.VITE_AI_MATTING_URL as string) || 'http://127.0.0.1:5000',
  sidecarUrl: (import.meta.env.VITE_SIDECAR_URL as string) || 'http://127.0.0.1:5050',
  ffmpegPath: (import.meta.env.VITE_FFMPEG_PATH as string) || 'ffmpeg',
};

/**
 * Returns formatted API endpoint URL
 */
export function getAIMattingApiUrl(path: string): string {
  const base = ENV_CONFIG.aiMattingUrl.replace(/\/+$/, '');
  const cleanPath = path.startsWith('/') ? path : `/${path}`;
  return `${base}${cleanPath}`;
}

/**
 * Returns formatted Sidecar API endpoint URL
 */
export function getSidecarApiUrl(path: string): string {
  const base = ENV_CONFIG.sidecarUrl.replace(/\/+$/, '');
  const cleanPath = path.startsWith('/') ? path : `/${path}`;
  return `${base}${cleanPath}`;
}
