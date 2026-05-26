using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        // DPI scale so với 96 dpi chuẩn
        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;

        public int CaptureX { get; private set; }
        public int CaptureY { get; private set; }
        public int CaptureWidth { get; private set; }
        public int CaptureHeight { get; private set; }
        public BitmapImage? CapturedImage { get; private set; }

        // P/Invoke để lấy tọa độ window thực tế (physical pixel) từ OS
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public ScreenCaptureOverlay()
        {
            InitializeComponent();

            Left   = SystemParameters.VirtualScreenLeft;
            Top    = SystemParameters.VirtualScreenTop;
            Width  = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            Loaded += (_, _) => ReadDpiScale();
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

        // ── Mouse events ──────────────────────────────────────────────────
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            _startPoint = e.GetPosition(this);
            _isSelecting = true;

            SelectionRectangle.Visibility = Visibility.Visible;
            InfoPanel.Visibility          = Visibility.Visible;

            Canvas.SetLeft(SelectionRectangle, _startPoint.X);
            Canvas.SetTop(SelectionRectangle,  _startPoint.Y);
            SelectionRectangle.Width  = 0;
            SelectionRectangle.Height = 0;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(this);
            UpdateCrosshair(p.X, p.Y);

            if (!_isSelecting) return;

            var x = Math.Min(_startPoint.X, p.X);
            var y = Math.Min(_startPoint.Y, p.Y);
            var w = Math.Abs(p.X - _startPoint.X);
            var h = Math.Abs(p.Y - _startPoint.Y);

            Canvas.SetLeft(SelectionRectangle, x);
            Canvas.SetTop(SelectionRectangle,  y);
            SelectionRectangle.Width  = w;
            SelectionRectangle.Height = h;

            InfoText.Text = $"{(int)w} × {(int)h} px";

            double infoTop = y - 34;
            if (infoTop < 4) infoTop = y + 8;
            Canvas.SetLeft(InfoPanel, x);
            Canvas.SetTop(InfoPanel,  infoTop);
        }

        private async void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;
            _isSelecting = false;

            var p = e.GetPosition(this);
            var x = Math.Min(_startPoint.X, p.X);
            var y = Math.Min(_startPoint.Y, p.Y);
            var w = Math.Abs(p.X - _startPoint.X);
            var h = Math.Abs(p.Y - _startPoint.Y);

            if (w < 10 || h < 10)
            {
                SelectionRectangle.Visibility = Visibility.Collapsed;
                InfoPanel.Visibility          = Visibility.Collapsed;
                return;
            }

            // ── Tính tọa độ physical pixel ────────────────────────────────
            // Lấy vị trí thực tế của window từ OS (physical pixel, không bị ảnh hưởng bởi DPI quirks)
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int winPhysLeft = 0, winPhysTop = 0;
            if (GetWindowRect(hwnd, out var wr))
            {
                winPhysLeft = wr.Left;
                winPhysTop  = wr.Top;
            }
            else
            {
                // Fallback nếu P/Invoke thất bại
                winPhysLeft = (int)Math.Round(Left * _dpiScaleX);
                winPhysTop  = (int)Math.Round(Top  * _dpiScaleY);
            }

            int physX = winPhysLeft + (int)Math.Round(x * _dpiScaleX);
            int physY = winPhysTop  + (int)Math.Round(y * _dpiScaleY);
            int physW = (int)Math.Round(w * _dpiScaleX);
            int physH = (int)Math.Round(h * _dpiScaleY);

            // Lưu tọa độ logical để node hiển thị
            CaptureX      = (int)Math.Round(x + Left);
            CaptureY      = (int)Math.Round(y + Top);
            CaptureWidth  = (int)Math.Round(w);
            CaptureHeight = (int)Math.Round(h);

            // ── Ẩn hoàn toàn window trước khi chụp ───────────────────────
            // Dùng Opacity=0 thay vì Hide() để window vẫn còn là dialog (DialogResult vẫn set được)
            // IsHitTestVisible=false để không nhận mouse event trong lúc chờ
            Opacity = 0;
            IsHitTestVisible = false;

            // Chờ đủ lâu để DWM composite lại màn hình phía sau (2 frame @ 60fps ≈ 33ms, dùng 80ms cho chắc)
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
                using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
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
