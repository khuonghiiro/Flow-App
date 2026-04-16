using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FlowMy.Models;

namespace FlowMy.Services.Interaction
{
    /// <summary>
    /// Dialog có tác vụ async khi đóng (vd. HtmlUiNode). NodeDialogManager gọi trước Close() để hủy tải / đóng thật.
    /// </summary>
    public interface INodeDialogForceClose
    {
        void NotifyHostForceClose();
    }

    /// <summary>
    /// Service quản lý dialog của nodes. Chỉ cho phép 1 dialog mở tại 1 thời điểm.
    /// </summary>
    public sealed class NodeDialogManager
    {
        private Window? _currentDialog;
        private WorkflowNode? _currentNode;

        /// <summary>
        /// Thời điểm (UTC) dialog vừa được đóng gần nhất.
        /// Dùng để chặn trường hợp mouse-up "rơi" xuống panel/canvas bên dưới.
        /// </summary>
        public DateTime LastDialogClosedUtc { get; private set; } = DateTime.MinValue;

        /// <summary>
        /// Mở dialog cho node. Nếu đã có dialog mở, đóng dialog cũ trước.
        /// </summary>
        public void OpenDialog(WorkflowNode node, Window dialog, IWorkflowEditorHost host)
        {
            // Đảm bảo không còn mouse capture sót lại từ resize/drag node
            // (thường gây scroll/pan "nhảy" sau khi đóng dialog).
            try { Mouse.Capture(null); } catch { }

            // Đóng dialog hiện tại nếu có
            CloseCurrentDialog();

            // Deselect node để tránh node nhảy đến vị trí chuột khi click vào canvas/dialog
            if (host.ViewModel != null)
            {
                host.ViewModel.SelectedNode = null;
            }

            _currentNode = node;
            _currentDialog = dialog;

            // Áp dụng UI scale factor để dialog có kích thước phù hợp với màn hình hiện tại
            try
            {
                if (Application.Current.Resources["UIScaleFactor"] is double uiScale && Math.Abs(uiScale - 1.0) > 0.01)
                {
                    dialog.LayoutTransform = new ScaleTransform(uiScale, uiScale);
                }
            }
            catch { /* best-effort */ }

            // Đặt vị trí dialog ở phía phải màn hình sau khi dialog đã được load
            dialog.Loaded += (s, e) =>
            {
                PositionDialog(dialog);
                // Focus vào dialog ngay khi load để node không còn focus
                try
                {
                    dialog.Activate();
                    dialog.Focus();
                    Keyboard.Focus(dialog);
                }
                catch { }
            };

            // Xử lý đóng dialog
            dialog.Closed += (s, e) =>
            {
                if (_currentDialog == dialog)
                {
                    // Cập nhật mốc để chặn việc mouse-up "rơi" xuống UI gây pan/nhảy
                    LastDialogClosedUtc = DateTime.UtcNow;
                    _currentDialog = null;
                    _currentNode = null;
                }
            };

            // Ngăn dialog bị đóng khi mất focus (chỉ đóng khi click vào canvas hoặc node khác)
            dialog.Deactivated += (s, e) =>
            {
                // Không làm gì - để dialog vẫn mở
            };

            dialog.Show();
            
            // Nếu dialog đã được load rồi, đặt vị trí ngay và focus vào dialog
            if (dialog.IsLoaded)
            {
                PositionDialog(dialog);
                // Focus vào dialog ngay để node không còn focus
                try
                {
                    dialog.Activate();
                    dialog.Focus();
                    Keyboard.Focus(dialog);
                }
                catch { }
            }
            else
            {
                // Nếu chưa load, focus vào dialog sau khi Show()
                try
                {
                    dialog.Activate();
                    dialog.Focus();
                    Keyboard.Focus(dialog);
                }
                catch { }
            }
        }

        private void PositionDialog(Window dialog)
        {
            try
            {
                // Đảm bảo dialog đã được measure và arrange
                if (!dialog.IsLoaded)
                {
                    dialog.Loaded += (s, e) => PositionDialog(dialog);
                    return;
                }

                dialog.UpdateLayout();
                
                // Đặt vị trí dialog ở phía phải màn hình, bottom aligned
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                var workingArea = screen.WorkingArea; // Không bao gồm taskbar
                
                // Lấy kích thước thực tế của dialog hoặc dùng giá trị mặc định
                var dialogWidth = double.IsNaN(dialog.Width) || dialog.Width <= 0 
                    ? (dialog.ActualWidth > 0 ? dialog.ActualWidth : 400)
                    : dialog.Width;

                // Chiều cao mục tiêu: 90% chiều cao khu vực làm việc
                var targetHeight = workingArea.Height * 9.0 / 10.0;

                // Tôn trọng MinHeight và MaxHeight nếu được cấu hình trên dialog
                var minHeight = dialog.MinHeight > 0 ? dialog.MinHeight : 0;
                var maxHeight = double.IsNaN(dialog.MaxHeight) || dialog.MaxHeight <= 0 
                    ? workingArea.Height 
                    : Math.Min(dialog.MaxHeight, workingArea.Height);
                var dialogHeight = Math.Max(minHeight, Math.Min(targetHeight, maxHeight));

                // Tôn trọng MaxWidth nếu được cấu hình
                var maxWidth = double.IsNaN(dialog.MaxWidth) || dialog.MaxWidth <= 0 
                    ? workingArea.Width 
                    : Math.Min(dialog.MaxWidth, workingArea.Width);
                var finalWidth = Math.Min(dialogWidth, maxWidth);

                dialog.WindowStartupLocation = WindowStartupLocation.Manual;
                dialog.Width = finalWidth;
                dialog.Height = dialogHeight;
                
                // Đảm bảo dialog không vượt quá màn hình
                dialog.Left = Math.Max(workingArea.Left, workingArea.Right - finalWidth);
                dialog.Top = Math.Max(workingArea.Top, workingArea.Bottom - dialogHeight); // Bottom aligned, không đè taskbar

                // Đảm bảo dialog không đè lên taskbar và tôn trọng MaxWidth/MaxHeight đã set
                if (double.IsNaN(dialog.MaxHeight) || dialog.MaxHeight <= 0)
                {
                    dialog.MaxHeight = workingArea.Height;
                }
                if (double.IsNaN(dialog.MaxWidth) || dialog.MaxWidth <= 0)
                {
                    dialog.MaxWidth = workingArea.Width;
                }
            }
            catch (Exception ex)
            {
                // Fallback: đặt vị trí mặc định
                try
                {
                    var screen = System.Windows.Forms.Screen.PrimaryScreen;
                    var workingArea = screen.WorkingArea;

                    var targetHeight = workingArea.Height * 6.0 / 10.0;
                    var minHeight = dialog.MinHeight > 0 ? dialog.MinHeight : 0;
                    var dialogHeight = Math.Max(minHeight, targetHeight);

                    dialog.WindowStartupLocation = WindowStartupLocation.Manual;
                    dialog.Width = 400;
                    dialog.Height = dialogHeight;
                    dialog.Left = workingArea.Right - 400;
                    dialog.Top = workingArea.Bottom - dialogHeight; // Bottom aligned
                }
                catch
                {
                    // Nếu vẫn lỗi, để WPF tự xử lý
                }
            }
        }

        /// <summary>
        /// Kiểm tra xem một điểm có nằm trong dialog không.
        /// </summary>
        public bool IsPointInDialog(Point screenPoint)
        {
            if (_currentDialog == null) return false;

            try
            {
                var dialogRect = new Rect(
                    _currentDialog.Left,
                    _currentDialog.Top,
                    _currentDialog.Width,
                    _currentDialog.Height);

                return dialogRect.Contains(screenPoint);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra xem một element có phải là con của dialog không.
        /// </summary>
        public bool IsElementInDialog(DependencyObject? element)
        {
            if (_currentDialog == null || element == null) return false;

            var current = element;
            while (current != null)
            {
                if (current == _currentDialog)
                {
                    return true;
                }
                current = LogicalTreeHelper.GetParent(current) ?? VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        /// <summary>
        /// Đóng dialog hiện tại nếu có.
        /// </summary>
        public void CloseCurrentDialog()
        {
            if (_currentDialog == null) return;

            try { Mouse.Capture(null); } catch { }
            LastDialogClosedUtc = DateTime.UtcNow;

            var dlg = _currentDialog;
            _currentDialog = null;
            _currentNode = null;

            try
            {
                if (dlg is INodeDialogForceClose force)
                    force.NotifyHostForceClose();
                else
                    dlg.Close();
            }
            catch { }
        }

        /// <summary>
        /// Kiểm tra xem có dialog nào đang mở không.
        /// </summary>
        public bool IsDialogOpen => _currentDialog != null;

        /// <summary>
        /// Lấy node đang mở dialog.
        /// </summary>
        public WorkflowNode? CurrentNode => _currentNode;

        /// <summary>
        /// Lấy dialog hiện tại đang mở.
        /// </summary>
        public Window? GetCurrentDialog() => _currentDialog;

        /// <summary>
        /// Update tất cả bindings trong dialog hiện tại nếu là WebNodeDialog.
        /// Được gọi trước khi serialize để đảm bảo giá trị được cập nhật.
        /// </summary>
        public void UpdateAllBindingsIfWebNodeDialog()
        {
            System.Diagnostics.Debug.WriteLine($"UpdateAllBindingsIfWebNodeDialog() called - CurrentDialog is null: {_currentDialog == null}");
            
            if (_currentDialog is Views.Overlays.WebNodeDialog webDialog)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"CurrentDialog is WebNodeDialog, calling UpdateAllBindings()");
                    webDialog.UpdateAllBindings();
                    System.Diagnostics.Debug.WriteLine($"✓ UpdateAllBindings() completed successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error updating bindings in WebNodeDialog: {ex.Message}\n{ex.StackTrace}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"CurrentDialog is not WebNodeDialog (type: {_currentDialog?.GetType().Name ?? "null"})");
            }
        }

        /// <summary>
        /// Refresh UI trong dialog hiện tại nếu là WebNodeDialog.
        /// Được gọi sau khi load workflow để đảm bảo UI hiển thị đúng dữ liệu đã load.
        /// </summary>
        public void RefreshUIIfWebNodeDialog()
        {
            System.Diagnostics.Debug.WriteLine($"RefreshUIIfWebNodeDialog() called - CurrentDialog is null: {_currentDialog == null}");
            
            if (_currentDialog is Views.Overlays.WebNodeDialog webDialog)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"CurrentDialog is WebNodeDialog, calling RefreshUI()");
                    webDialog.RefreshUI();
                    System.Diagnostics.Debug.WriteLine($"✓ RefreshUI() called successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error refreshing UI in WebNodeDialog: {ex.Message}\n{ex.StackTrace}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"CurrentDialog is not WebNodeDialog (type: {_currentDialog?.GetType().Name ?? "null"})");
            }
        }
    }
}

