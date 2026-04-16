using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using FlowMy.Services.Interaction;

namespace FlowMy.Services.Rendering
{
    /// <summary>
    /// Utility to clip WebView2 HWND regions so that WPF overlay elements
    /// (toolbar, left sidebar, minimap, etc.) render on top of WebView2.
    /// 
    /// WebView2 is an HwndHost — its Win32 HWND always paints on top of WPF content
    /// (the classic "airspace" issue). This helper uses SetWindowRgn to cut away
    /// portions of the WebView2 window that overlap with overlay UI elements,
    /// making it appear as though the WPF elements are rendered on top.
    /// </summary>
    public static class WebView2AirspaceClipper
    {
        #region Win32 Interop

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("gdi32.dll")]
        private static extern int CombineRgn(IntPtr hrgnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int fnCombineMode);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        private const int RGN_DIFF = 4; // Subtract region

        #endregion

        // Cache overlay elements per host để tránh walk visual tree mỗi lần gọi
        private static readonly Dictionary<IWorkflowEditorHost, List<FrameworkElement>> _overlayCache = new();

        // Cache region rect cuối cùng cho mỗi HWND để skip SetWindowRgn nếu không đổi
        // Value: (wvWidth, wvHeight, subtractRects-hashcode, needsClipping)
        private static readonly Dictionary<IntPtr, (int w, int h, int hash, bool clip)> _lastRegionCache = new();

        /// <summary>
        /// Xóa cache overlay khi cần (ví dụ khi window layout thay đổi).
        /// </summary>
        public static void InvalidateOverlayCache(IWorkflowEditorHost host)
        {
            _overlayCache.Remove(host);
        }

        /// <summary>
        /// Updates the clipping region of the WebView2 HWND so that it does not overlap
        /// with overlay UI elements (toolbar, sidebar, minimap panel).
        /// Call this whenever the WebView2 position or the layout changes (zoom, pan, drag, resize).
        /// </summary>
        public static void UpdateClipping(WebView2 webView, IWorkflowEditorHost host)
        {
            try
            {
                if (webView == null || !webView.IsLoaded || webView.Visibility != Visibility.Visible)
                    return;

                var ownerWindow = host.OwnerWindow;
                if (ownerWindow == null || !ownerWindow.IsLoaded)
                    return;

                IntPtr hwnd = GetWebView2Hwnd(webView);
                if (hwnd == IntPtr.Zero)
                    return;

                var webViewTopLeft = webView.PointToScreen(new Point(0, 0));
                var webViewSize = webView.RenderSize;
                var dpiScale = GetDpiScale(webView);
                int wvWidth = (int)(webViewSize.Width * dpiScale.X);
                int wvHeight = (int)(webViewSize.Height * dpiScale.Y);

                if (wvWidth <= 0 || wvHeight <= 0)
                    return;

                // Dùng cache overlay elements thay vì walk visual tree mỗi lần gọi
                if (!_overlayCache.TryGetValue(host, out var overlayElements))
                {
                    overlayElements = CollectOverlayElements(host);
                    _overlayCache[host] = overlayElements;
                }

                // Tính toán các rect cần subtract
                var subtractRects = new System.Collections.Generic.List<(int l, int t, int r, int b)>(overlayElements.Count);
                bool needsClipping = false;

                foreach (var overlay in overlayElements)
                {
                    if (overlay == null || !overlay.IsVisible || overlay.RenderSize.Width <= 0 || overlay.RenderSize.Height <= 0)
                        continue;

                    Point overlayTopLeft;
                    try { overlayTopLeft = overlay.PointToScreen(new Point(0, 0)); }
                    catch { continue; }

                    var overlayWidth = overlay.RenderSize.Width * dpiScale.X;
                    var overlayHeight = overlay.RenderSize.Height * dpiScale.Y;

                    double ixLeft = Math.Max(webViewTopLeft.X, overlayTopLeft.X);
                    double ixTop = Math.Max(webViewTopLeft.Y, overlayTopLeft.Y);
                    double ixRight = Math.Min(webViewTopLeft.X + wvWidth, overlayTopLeft.X + overlayWidth);
                    double ixBottom = Math.Min(webViewTopLeft.Y + wvHeight, overlayTopLeft.Y + overlayHeight);

                    if (ixRight <= ixLeft || ixBottom <= ixTop)
                        continue;

                    int localLeft = (int)(ixLeft - webViewTopLeft.X);
                    int localTop = (int)(ixTop - webViewTopLeft.Y);
                    int localRight = (int)(ixRight - webViewTopLeft.X);
                    int localBottom = (int)(ixBottom - webViewTopLeft.Y);

                    subtractRects.Add((localLeft, localTop, localRight, localBottom));
                    needsClipping = true;
                }

                // Tính hash để kiểm tra region có thay đổi không
                int rectsHash = 0;
                foreach (var r in subtractRects)
                    rectsHash = HashCode.Combine(rectsHash, r.l, r.t, r.r, r.b);

                // Skip SetWindowRgn nếu region giống hệt lần trước — tránh Win32 redraw không cần thiết
                if (_lastRegionCache.TryGetValue(hwnd, out var last) &&
                    last.w == wvWidth && last.h == wvHeight &&
                    last.hash == rectsHash && last.clip == needsClipping)
                {
                    return;
                }
                _lastRegionCache[hwnd] = (wvWidth, wvHeight, rectsHash, needsClipping);

                // Áp dụng region mới
                IntPtr fullRgn = CreateRectRgn(0, 0, wvWidth, wvHeight);
                try
                {
                    foreach (var r in subtractRects)
                    {
                        IntPtr subtractRgn = CreateRectRgn(r.l, r.t, r.r, r.b);
                        try { CombineRgn(fullRgn, fullRgn, subtractRgn, RGN_DIFF); }
                        finally { DeleteObject(subtractRgn); }
                    }

                    if (needsClipping)
                    {
                        // bRedraw=false: để WPF/OS tự schedule redraw, tránh force-redraw ngay lập tức
                        SetWindowRgn(hwnd, fullRgn, false);
                    }
                    else
                    {
                        SetWindowRgn(hwnd, IntPtr.Zero, false);
                        DeleteObject(fullRgn);
                    }
                }
                catch
                {
                    DeleteObject(fullRgn);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2AirspaceClipper error: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears any clipping region on the WebView2 HWND, restoring full visibility.
        /// Call this when the WebView2 is being hidden or disposed.
        /// </summary>
        public static void ClearClipping(WebView2 webView)
        {
            try
            {
                if (webView == null) return;
                IntPtr hwnd = GetWebView2Hwnd(webView);
                if (hwnd == IntPtr.Zero) return;
                SetWindowRgn(hwnd, IntPtr.Zero, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2AirspaceClipper ClearClipping error: {ex.Message}");
            }
        }

        /// <summary>
        /// Collects all overlay FrameworkElements that should clip the WebView2.
        /// These are UI elements that visually sit on top of the canvas area.
        /// </summary>
        private static List<FrameworkElement> CollectOverlayElements(IWorkflowEditorHost host)
        {
            var overlays = new List<FrameworkElement>();
            var ownerWindow = host.OwnerWindow;
            if (ownerWindow == null) return overlays;

            // Strategy: walk up from the WorkflowCanvas to find overlay siblings/ancestors
            // The layout is:
            //   Grid (root)
            //     Row 0: Toolbar Border
            //     Row 1: Grid
            //       Column 0: Left sidebar (node templates)
            //       Column 1: Grid
            //         ScrollViewer (canvas)
            //         MinimapPanel (overlay, bottom-right)

            // 1. Find the toolbar (first Border child of the root Grid at Row 0)
            var rootContent = ownerWindow.Content as FrameworkElement;
            if (rootContent is Grid rootGrid)
            {
                foreach (UIElement child in rootGrid.Children)
                {
                    if (child is Border toolbarBorder && Grid.GetRow(toolbarBorder) == 0)
                    {
                        overlays.Add(toolbarBorder);
                        break;
                    }
                }

                // 2. Find the left sidebar, minimap và các overlay khác trong Grid hàng 1
                foreach (UIElement child in rootGrid.Children)
                {
                    if (child is Grid row1Grid && Grid.GetRow(child) == 1)
                    {
                        foreach (UIElement col1Child in row1Grid.Children)
                        {
                            // Left sidebar: Column 0 Border
                            if (col1Child is Border sidebarBorder && Grid.GetColumn(col1Child) == 0)
                            {
                                overlays.Add(sidebarBorder);
                            }

                            // Canvas area Grid (chứa ScrollViewer, MinimapPanel, Execution overlays)
                            // Trong WorkflowEditorWindow.xaml: cột canvas là Column="2"
                            if (col1Child is Grid canvasGrid && Grid.GetColumn(col1Child) == 2)
                            {
                                // Tìm MinimapPanel và ExecutionFloatingPanel theo tên x:Name trong XAML
                                foreach (UIElement canvasChild in canvasGrid.Children)
                                {
                                    if (canvasChild is FrameworkElement fe)
                                    {
                                        // MinimapPanel (Grid x:Name="MinimapPanel")
                                        if (fe.Name == "MinimapPanel")
                                        {
                                            overlays.Add(fe);
                                        }

                                        // Execution overlay canvas chứa panel nổi hiển thị node đang chạy
                                        if (fe is Canvas overlayCanvas && fe.Name == "ExecutionOverlayCanvas")
                                        {
                                            foreach (UIElement oc in overlayCanvas.Children)
                                            {
                                                if (oc is FrameworkElement floating && floating.Name == "ExecutionFloatingPanel")
                                                {
                                                    overlays.Add(floating);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            }

            return overlays;
        }

        /// <summary>
        /// Gets the Win32 HWND from a WebView2 control.
        /// WebView2 inherits from HwndHost, so we can cast and get the handle.
        /// </summary>
        private static IntPtr GetWebView2Hwnd(WebView2 webView)
        {
            try
            {
                // WebView2 WPF control derives from HwndHost.
                // HwndHost.Handle is a public property.
                if (webView is HwndHost hwndHost)
                {
                    var handle = hwndHost.Handle;
                    if (handle != IntPtr.Zero)
                        return handle;
                }

                // Fallback: find child HWND of the window
                var source = (HwndSource?)PresentationSource.FromVisual(webView);
                if (source != null)
                {
                    return FindChildHwnd(source.Handle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetWebView2Hwnd error: {ex.Message}");
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

        /// <summary>
        /// Finds the first child HWND of a given parent HWND.
        /// WebView2 creates a child window inside the HwndHost container.
        /// </summary>
        private static IntPtr FindChildHwnd(IntPtr parentHwnd)
        {
            if (parentHwnd == IntPtr.Zero) return IntPtr.Zero;
            return FindWindowEx(parentHwnd, IntPtr.Zero, null, null);
        }

        /// <summary>
        /// Gets the DPI scale factor for a given visual element.
        /// </summary>
        private static (double X, double Y) GetDpiScale(Visual visual)
        {
            var source = PresentationSource.FromVisual(visual);
            if (source?.CompositionTarget != null)
            {
                var m = source.CompositionTarget.TransformToDevice;
                return (m.M11, m.M22);
            }
            return (1.0, 1.0);
        }
    }
}
