using System;
using System.Collections.Generic;
using FlowMy.Views;
using FlowMy.Helpers;

namespace FlowMy.Services
{
    /// <summary>
    /// Quản lý embedded window overlays cho automation.
    /// 
    /// Use cases:
    /// 1. Auto-embed target window khi run flow
    /// 2. Keep window at original position & size
    /// 3. Don't interrupt user's current work
    /// 4. Auto-cleanup khi flow completes
    /// </summary>
    public class EmbeddedWindowOverlayManager
    {
        private static EmbeddedWindowOverlayManager? _instance;
        public static EmbeddedWindowOverlayManager Instance => _instance ??= new EmbeddedWindowOverlayManager();

        private readonly Dictionary<IntPtr, EmbeddedWindowOverlay> _activeOverlays = new();

        private EmbeddedWindowOverlayManager() { }

        /// <summary>
        /// Embed window vào overlay tại vị trí gốc của nó.
        /// Returns overlay window để caller có thể control.
        /// </summary>
        public EmbeddedWindowOverlay? EmbedWindowInOverlay(IntPtr windowHandle, string windowTitle = "")
        {
            if (windowHandle == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("[OverlayManager] Invalid window handle");
                return null;
            }

            // Check if already embedded
            if (_activeOverlays.ContainsKey(windowHandle))
            {
                System.Diagnostics.Debug.WriteLine($"[OverlayManager] Window already embedded: {windowHandle}");
                return _activeOverlays[windowHandle];
            }

            // Check if can embed
            if (!WindowHostHelper.CanEmbedWindow(windowHandle))
            {
                System.Diagnostics.Debug.WriteLine($"[OverlayManager] Cannot embed system window: {windowHandle}");
                return null;
            }

            try
            {
                // Create overlay on UI thread
                EmbeddedWindowOverlay? overlay = null;

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    overlay = new EmbeddedWindowOverlay();
                    
                    bool success = overlay.EmbedWindowAtOriginalPosition(windowHandle, windowTitle);

                    if (success)
                    {
                        _activeOverlays[windowHandle] = overlay;
                        System.Diagnostics.Debug.WriteLine($"[OverlayManager] ✅ Created overlay for {windowTitle}");

                        // Auto-remove from dict when overlay closes
                        overlay.Closed += (s, e) =>
                        {
                            _activeOverlays.Remove(windowHandle);
                            System.Diagnostics.Debug.WriteLine($"[OverlayManager] Overlay closed, removed from tracking");
                        };
                    }
                    else
                    {
                        overlay = null;
                    }
                });

                return overlay;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OverlayManager] Error creating overlay: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Unembed và close overlay cho window.
        /// </summary>
        public void CloseOverlay(IntPtr windowHandle)
        {
            if (_activeOverlays.TryGetValue(windowHandle, out var overlay))
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    overlay.UnembedAndClose();
                    overlay.Close();
                });

                _activeOverlays.Remove(windowHandle);
            }
        }

        /// <summary>
        /// Get overlay for window (if exists).
        /// </summary>
        public EmbeddedWindowOverlay? GetOverlay(IntPtr windowHandle)
        {
            return _activeOverlays.TryGetValue(windowHandle, out var overlay) ? overlay : null;
        }

        /// <summary>
        /// Close all active overlays.
        /// </summary>
        public void CloseAllOverlays()
        {
            var handles = new List<IntPtr>(_activeOverlays.Keys);
            foreach (var handle in handles)
            {
                CloseOverlay(handle);
            }
        }

        /// <summary>
        /// Check if window is currently embedded in overlay.
        /// </summary>
        public bool IsEmbedded(IntPtr windowHandle)
        {
            return _activeOverlays.ContainsKey(windowHandle);
        }
    }
}
