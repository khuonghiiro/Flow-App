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
                // Đưa app lên trước nếu được cấu hình (chỉ áp dụng khi không chụp workflow canvas)
                if (!node.CaptureWorkflowCanvas && !string.IsNullOrWhiteSpace(node.TargetProcessName))
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
                    ownerWindow.Show();
                    ownerWindow.Activate();

                    System.Windows.Controls.Canvas? canvas = null;
                    if (ownerWindow is FlowMy.Services.Interaction.IWorkflowEditorHost host)
                        canvas = host.WorkflowCanvas;
                    else if (Application.Current.MainWindow is FlowMy.Services.Interaction.IWorkflowEditorHost hostMain)
                        canvas = hostMain.WorkflowCanvas;
                    else
                        canvas = Application.Current.Windows.OfType<FlowMy.Services.Interaction.IWorkflowEditorHost>().FirstOrDefault()?.WorkflowCanvas;

                    if (node.CaptureWorkflowCanvas && canvas != null && canvas.IsVisible)
                    {
                        try
                        {
                            Point p1 = canvas.PointFromScreen(new Point(overlay.CaptureX, overlay.CaptureY));
                            Point p2 = canvas.PointFromScreen(new Point(overlay.CaptureX + overlay.CaptureWidth, overlay.CaptureY + overlay.CaptureHeight));

                            int cX = (int)Math.Round(Math.Min(p1.X, p2.X));
                            int cY = (int)Math.Round(Math.Min(p1.Y, p2.Y));
                            int cW = (int)Math.Round(Math.Abs(p2.X - p1.X));
                            int cH = (int)Math.Round(Math.Abs(p2.Y - p1.Y));

                            if (cW > 0 && cH > 0)
                            {
                                node.CaptureX = cX;
                                node.CaptureY = cY;
                                node.CaptureWidth = cW;
                                node.CaptureHeight = cH;
                                node.CapturedImage = FlowMy.Services.Workflow.NodeExecutors.TextScanNodeExecutor.RenderCanvasRegion(canvas, cX, cY, cW, cH);
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ScreenCaptureHelper] Convert screen to canvas coords error: {ex.Message}");
                        }
                    }

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
                if (!ownerWindow.IsVisible)
                {
                    ownerWindow.Show();
                    ownerWindow.Activate();
                }
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
