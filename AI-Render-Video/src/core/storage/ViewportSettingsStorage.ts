export interface ViewportSettings {
  showUI: boolean;
  showControlsGuide: boolean;
  showCoordinates: boolean;
  showCC: boolean;
  isFreeCam: boolean;
  showNavWidget: boolean;
}

const STORAGE_KEY = 'studio_viewport_settings_v1';

const DEFAULT_SETTINGS: ViewportSettings = {
  showUI: true,
  showControlsGuide: true,
  showCoordinates: false,
  showCC: true,
  isFreeCam: false,
  showNavWidget: true,
};

export const getSavedViewportSettings = (): ViewportSettings => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw);
      return { ...DEFAULT_SETTINGS, ...parsed };
    }
  } catch (e) {
    console.warn('Failed to load viewport settings from localStorage', e);
  }
  return { ...DEFAULT_SETTINGS };
};

export const saveViewportSetting = <K extends keyof ViewportSettings>(
  key: K,
  value: ViewportSettings[K]
): void => {
  try {
    const current = getSavedViewportSettings();
    current[key] = value;
    localStorage.setItem(STORAGE_KEY, JSON.stringify(current));
  } catch (e) {
    console.warn('Failed to save viewport setting to localStorage', e);
  }
};
