/**
 * Adaptive Chunk Prefetcher
 * 
 * Detects device capability and prefetches lazy-loaded chunks
 * during idle time on powerful devices. Weak devices skip prefetching
 * to save memory and bandwidth.
 * 
 * Detection uses:
 * - navigator.hardwareConcurrency (CPU cores)
 * - navigator.deviceMemory (RAM in GB, Chrome-only)
 * - navigator.connection.effectiveType (network speed)
 */

interface DeviceProfile {
  cores: number;
  memoryGB: number | null;
  connectionType: string | null;
  isPowerful: boolean;
}

/** Detect device capability */
export function detectDeviceProfile(): DeviceProfile {
  const cores = navigator.hardwareConcurrency || 2;
  const memoryGB = (navigator as any).deviceMemory ?? null;
  const connection = (navigator as any).connection;
  const connectionType = connection?.effectiveType ?? null;

  // Device is "powerful" if:
  // - Has >= 4 CPU cores AND
  // - Has >= 4GB RAM (or RAM detection unavailable, assume modern desktop) AND
  // - Not on slow network (2g/slow-2g)
  const hasEnoughCores = cores >= 4;
  const hasEnoughMemory = memoryGB === null || memoryGB >= 4;
  const hasGoodNetwork = connectionType === null || !['slow-2g', '2g'].includes(connectionType);

  const isPowerful = hasEnoughCores && hasEnoughMemory && hasGoodNetwork;

  return { cores, memoryGB, connectionType, isPowerful };
}

/**
 * Prefetch all lazy-loaded chunks during browser idle time.
 * Only runs on powerful devices. Uses requestIdleCallback to avoid
 * blocking the main thread.
 * 
 * @param delayMs - Minimum delay after call before starting prefetch (default: 2000ms)
 */
export function prefetchLazyChunks(delayMs = 2000): void {
  const profile = detectDeviceProfile();

  if (!profile.isPowerful) {
    console.log(
      `[Prefetch] Skipped — device profile: ${profile.cores} cores, ` +
      `${profile.memoryGB ?? '?'}GB RAM, ${profile.connectionType ?? 'unknown'} network`
    );
    return;
  }

  console.log(
    `[Prefetch] Powerful device detected (${profile.cores} cores, ` +
    `${profile.memoryGB ?? '?'}GB RAM). Will prefetch lazy chunks after ${delayMs}ms.`
  );

  // Wait for the app to fully settle before prefetching
  setTimeout(() => {
    const schedule = typeof requestIdleCallback === 'function'
      ? requestIdleCallback
      : (cb: () => void) => setTimeout(cb, 100);

    // ─── StudioLayout panels ───
    const studioPanels = [
      () => import('../../ui/SubtitleInspector'),
      () => import('../../ui/CombatDebugger'),
      () => import('../../ui/MapRadarView'),
      () => import('../../ui/AIChatDirector'),
      () => import('../../ui/DialogueEditorModal'),
      () => import('../../ui/WeatherControlPanel'),
      () => import('../../ui/AssetBrowserPanel'),
      () => import('../../ui/LightingStudioPanel'),
      () => import('../../ui/CharacterWorkbenchPanel'),
      () => import('../../ui/TransformInspector'),
      () => import('../../ui/studio2d/Studio2DWorkbenchModal'),
    ];

    // ─── Studio2D sub-tabs ───
    const studio2DTabs = [
      () => import('../../ui/studio2d/ImageSegmenterCropper'),
      () => import('../../ui/studio2d/AIPromptGenerator2D'),
      () => import('../../ui/studio2d/Character2DAssembler'),
      () => import('../../ui/studio2d/AutoGridSlicer3DAssembler'),
      () => import('../../ui/studio2d/Map2DAssembler'),
      () => import('../../ui/studio2d/ActionSequence2DDirector'),
      () => import('../../ui/studio2d/MultiAngleRigAssembler'),
      () => import('../../ui/studio2d/vectorizer/ImageToSvgVectorizerTab'),
      () => import('../../ui/studio2d/agent/AIAntigravityDecomposerPanel'),
      () => import('../../ui/studio2d/detail/DetailPartAssemblerTab'),
    ];

    const allChunks = [...studioPanels, ...studio2DTabs];
    let loaded = 0;

    // Load chunks one at a time during idle periods to avoid flooding
    function loadNext() {
      if (loaded >= allChunks.length) {
        console.log(`[Prefetch] Complete — ${loaded} chunks prefetched.`);
        return;
      }

      schedule(() => {
        allChunks[loaded]()
          .then(() => {
            loaded++;
            loadNext();
          })
          .catch(() => {
            // Silently skip failed prefetch — chunk will load on-demand
            loaded++;
            loadNext();
          });
      });
    }

    loadNext();
  }, delayMs);
}
