using System;
using System.Windows;
using FlowMy.Services.Interaction;

namespace FlowMy.Services.Rendering
{
    /// <summary>
    /// Clipper helper cho CefSharp.
    /// CefSharp WPF sử dụng OSR (Off-Screen Rendering) theo mặc định nên render trực tiếp
    /// vào Visual Tree của WPF, không bị lỗi Airspace của HwndHost.
    /// File này giữ các method stub để đảm bảo tương thích ngược.
    /// </summary>
    public static class WebView2AirspaceClipper
    {
        public static void InvalidateOverlayCache(IWorkflowEditorHost host)
        {
        }

        public static void UpdateClipping(object? browser, IWorkflowEditorHost host)
        {
        }

        public static void ClearClipping(object? browser)
        {
        }

        public static void ApplyRoundedCorners(object? browser, int cornerRadius)
        {
        }

        public static void UpdateRoundedCorners(object? browser, int cornerRadius)
        {
        }
    }
}
