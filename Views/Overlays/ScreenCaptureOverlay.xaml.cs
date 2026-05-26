using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.Overlays
{
    public partial class ScreenCaptureOverlay : Window
    {
        // Độ dài mỗi nhánh crosshair (px logical)
        private const double CrosshairArm = 20;
        private const double CrosshairGap = 5;

        private System.Windows.Point _startPoint;
        private bool _isSelecting;

        // Vùng chọn hiện tại (logical px)
        private double _selX, _selY, _selW, _selH;

        // DPI scale so với 96 dpi chuẩn
        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;

        public int CaptureX { get; private set; }
        public int CaptureY { get; private set; }
        public int CaptureWidth { get; private set; }
        public int CaptureHeight { get; private set; }
        public BitmapImage? CapturedImage { get; private set; }

        // P/Invoke để lấy tọa độ window thực tế (physical pixel)
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        public ScreenCaptureOverlay()
        {
            InitializeComponent();

            Left   = SystemParameters.VirtualScreenLeft;
            Top    = SystemParameters.VirtualScreenTop;
            Width  = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ReadDpiScale();

            // Gán kích thước nền toàn màn hình cho geometry đục lỗ
            FullScreenGeometry.Rect = new Rect(0, 0, ActualWidth, ActualHeight);

            // Lỗ ban đầu rỗng (Rect.Empty → không đục gì)
            HoleGeometry.Rect = Rect.Empty;
        }

        private void ReadDpiScale()
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                _dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                _dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            }
        }

        // ── Crosshair ngắn quanh con trỏ ─────────────────────────────────
        private void UpdateCrosshair(double cx, double cy)
        {
            CH_Left.X1  = cx - CrosshairArm - CrosshairGap; CH_Left.Y1  = cy;
            CH_Left.X2  = cx - CrosshairGap;                CH_Left.Y2  = cy;
            CH_Right.X1 = cx + CrosshairGap;                CH_Right.Y1 = cy;
            CH_Right.X2 = cx + CrosshairArm + CrosshairGap; CH_Right.Y2 = cy;
            CH_Up.X1    = cx; CH_Up.Y1    = cy - CrosshairArm - CrosshairGap;
            CH_Up.X2    = cx; CH_Up.Y2    = cy - CrosshairGap;
            CH_Down.X1  = cx; CH_Down.Y1  = cy + CrosshairGap;
            CH_Down.X2  = cx; CH_Down.Y2  = cy + CrosshairArm + CrosshairGap;
            Canvas.SetLeft(CH_Dot, cx - 2);
            Canvas.SetTop(CH_Dot,  cy - 2);
        }

        // ── Cập nhật "lỗ sáng" trên lớp phủ tối ─────────────────────────
        private void UpdateHole(double x, double y, double w, double h)
        {
            HoleGeometry.Rect = w > 0 && h > 0
                ? new Rect(x, y, w, h)
                : Rect.Empty;
        }

        // ── Mouse events ──────────────────────────────────────────────────
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            _startPoint  = e.GetPosition(this);
            _isSelecting = true;

            // Reset lỗ và viền
            HoleGeometry.Rect = Rect.Empty;
            SelectionBorder.Visibility = Visibility.Collapsed;
            InfoPanel.Visibility       = Visibility.Collapsed;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(this);
            UpdateCrosshair(p.X, p.Y);

            if (!_isSelecting) return;

            _selX = Math.Min(_startPoint.X, p.X);
            _selY = Math.Min(_startPoint.Y, p.Y);
            _selW = Math.Abs(p.X - _startPoint.X);
            _selH = Math.Abs(p.Y - _startPoint.Y);

            // Đục lỗ sáng theo vùng đang kéo
            UpdateHole(_selX, _selY, _selW, _selH);

            // Cập nhật viền xanh
            Canvas.SetLeft(SelectionBorder, _selX);
            Canvas.SetTop(SelectionBorder,  _selY);
            SelectionBorder.Width  = _selW;
            SelectionBorder.Height = _selH;
            SelectionBorder.Visibility = Visibility.Visible;

            // Info panel kích thước
            InfoText.Text = $"{(int)_selW} × {(int)_selH} px";
            double infoTop = _selY - 34;
            if (infoTop < 4) infoTop = _selY + 8;
            Canvas.SetLeft(InfoPanel, _selX);
            Canvas.SetTop(InfoPanel,  infoTop);
            InfoPanel.Visibility = Visibility.Visible;
        }

        private async void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;

            if (_selW < 10 || _selH < 10)
            {
                // Vùng quá nhỏ → reset
                HoleGeometry.Rect          = Rect.Empty;
                SelectionBorder.Visibility = Visibility.Collapsed;
                InfoPanel.Visibility       = Visibility.Collapsed;
                return;
            }

            // ── Tính tọa độ physical pixel ────────────────────────────────
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int winPhysLeft = 0, winPhysTop = 0;
            if (GetWindowRect(hwnd, out var wr))
            {
                winPhysLeft = wr.Left;
                winPhysTop  = wr.Top;
            }
            else
            {
                winPhysLeft = (int)Math.Round(Left * _dpiScaleX);
                winPhysTop  = (int)Math.Round(Top  * _dpiScaleY);
            }

            int physX = winPhysLeft + (int)Math.Round(_selX * _dpiScaleX);
            int physY = winPhysTop  + (int)Math.Round(_selY * _dpiScaleY);
            int physW = (int)Math.Round(_selW * _dpiScaleX);
            int physH = (int)Math.Round(_selH * _dpiScaleY);

            // Lưu tọa độ logical để node hiển thị
            CaptureX      = (int)Math.Round(_selX + Left);
            CaptureY      = (int)Math.Round(_selY + Top);
            CaptureWidth  = (int)Math.Round(_selW);
            CaptureHeight = (int)Math.Round(_selH);

            // ── Ẩn toàn bộ overlay trước khi chụp (giống Snipping Tool) ──
            // Ẩn hết các lớp UI: dim, viền, crosshair, hint
            DimPath.Visibility        = Visibility.Collapsed;
            SelectionCanvas.Visibility = Visibility.Collapsed;
            CrosshairCanvas.Visibility = Visibility.Collapsed;
            HintPanel.Visibility       = Visibility.Collapsed;

            // Chờ DWM composite lại màn hình thực phía sau (2 frame @ 60fps ≈ 33ms, dùng 80ms cho chắc)
            await Task.Delay(80);

            CapturedImage = CaptureScreenRegion(physX, physY, physW, physH);

            DialogResult = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        // ── Chụp màn hình theo physical pixel ────────────────────────────
        private static BitmapImage? CaptureScreenRegion(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0) return null;
            try
            {
                using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bitmap);
                g.CopyFromScreen(x, y, 0, 0,
                    new System.Drawing.Size(width, height),
                    CopyPixelOperation.SourceCopy);

                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                var img = new BitmapImage();
                img.BeginInit();
                img.StreamSource  = ms;
                img.CacheOption   = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenCapture] Error: {ex.Message}");
                return null;
            }
        }
    }
}
