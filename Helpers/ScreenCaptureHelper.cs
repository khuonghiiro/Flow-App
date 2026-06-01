using FlowMy.Models.Nodes;
using FlowMy.Views.Overlays;
using System.Windows;

namespace FlowMy.Helpers
{
    /// <summary>
    /// Helper class cho screen capture logic - dùng chung giữa UI control và workflow executor
    /// </summary>
    public static class ScreenCaptureHelper
    {
        /// <summary>
        /// Thực hiện chụp màn hình với overlay dialog cho TextScanNode
        /// </summary>
        public static bool CaptureForTextScanNode(TextScanNode node, Window ownerWindow)
        {
            if (node == null || ownerWindow == null) return false;

            ownerWindow.Hide();
            try
            {
                // Đưa app lên trước nếu được cấu hình
                if (!string.IsNullOrWhiteSpace(node.TargetProcessName))
                {
                    var windows = WindowHelper.GetActiveWindows();
                    var match = windows.FirstOrDefault(wnd =>
                        wnd.ProcessName == node.TargetProcessName && wnd.Title == node.TargetWindowTitle)
                        ?? windows.FirstOrDefault(wnd => wnd.ProcessName == node.TargetProcessName);

                    if (match != null)
                        WindowHelper.BringToFront(match.Handle);
                }

                System.Threading.Thread.Sleep(150);

                var overlay = new ScreenCaptureOverlay();
                if (overlay.ShowDialog() == true)
                {
                    node.CaptureX = overlay.CaptureX;
                    node.CaptureY = overlay.CaptureY;
                    node.CaptureWidth = overlay.CaptureWidth;
                    node.CaptureHeight = overlay.CaptureHeight;
                    node.CapturedImage = overlay.CapturedImage;
                    return true;
                }
                return false;
            }
            finally
            {
                ownerWindow.Show();
                ownerWindow.Activate();
            }
        }

        /// <summary>
        /// Thực hiện chụp màn hình với overlay dialog cho ScreenCaptureNode
        /// </summary>
        public static bool CaptureForScreenCaptureNode(ScreenCaptureNode node, Window ownerWindow)
        {
            if (node == null || ownerWindow == null) return false;

            ownerWindow.Hide();
            try
            {
                // Đưa app lên trước nếu được cấu hình
                if (!string.IsNullOrWhiteSpace(node.TargetProcessName))
                {
                    var windows = WindowHelper.GetActiveWindows();
                    var match = windows.FirstOrDefault(wnd =>
                        wnd.ProcessName == node.TargetProcessName && wnd.Title == node.TargetWindowTitle)
                        ?? windows.FirstOrDefault(wnd => wnd.ProcessName == node.TargetProcessName);

                    if (match != null)
                        WindowHelper.BringToFront(match.Handle);
                }

                System.Threading.Thread.Sleep(150);

                var overlay = new ScreenCaptureOverlay();
                if (overlay.ShowDialog() == true)
                {
                    node.CaptureX = overlay.CaptureX;
                    node.CaptureY = overlay.CaptureY;
                    node.CaptureWidth = overlay.CaptureWidth;
                    node.CaptureHeight = overlay.CaptureHeight;
                    node.CapturedImage = overlay.CapturedImage;
                    return true;
                }
                return false;
            }
            finally
            {
                ownerWindow.Show();
                ownerWindow.Activate();
            }
        }
    }
}
