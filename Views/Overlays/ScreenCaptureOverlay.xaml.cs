using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.Overlays
{
    public partial class ScreenCaptureOverlay : Window
    {
        private System.Windows.Point _startPoint;
        private bool _isSelecting;

        public int CaptureX { get; private set; }
        public int CaptureY { get; private set; }
        public int CaptureWidth { get; private set; }
        public int CaptureHeight { get; private set; }

        // THÊM: Property để lưu ảnh đã chụp
        public BitmapImage? CapturedImage { get; private set; }

        public ScreenCaptureOverlay()
        {
            InitializeComponent();

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _startPoint = e.GetPosition(this);
                _isSelecting = true;

                SelectionRectangle.Visibility = Visibility.Visible;
                InfoPanel.Visibility = Visibility.Visible;

                Canvas.SetLeft(SelectionRectangle, _startPoint.X);
                Canvas.SetTop(SelectionRectangle, _startPoint.Y);
                SelectionRectangle.Width = 0;
                SelectionRectangle.Height = 0;
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;

            var currentPoint = e.GetPosition(this);

            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _startPoint.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Y);

            Canvas.SetLeft(SelectionRectangle, x);
            Canvas.SetTop(SelectionRectangle, y);
            SelectionRectangle.Width = width;
            SelectionRectangle.Height = height;

            InfoText.Text = $"{(int)width} × {(int)height} px";
            Canvas.SetLeft(InfoPanel, x);
            Canvas.SetTop(InfoPanel, y - 30);
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;

            _isSelecting = false;

            var currentPoint = e.GetPosition(this);

            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _startPoint.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Y);

            CaptureX = (int)(x + Left);
            CaptureY = (int)(y + Top);
            CaptureWidth = (int)width;
            CaptureHeight = (int)height;

            if (CaptureWidth >= 10 && CaptureHeight >= 10)
            {
                // THÊM: Chụp ảnh màn hình
                CapturedImage = CaptureScreenRegion(CaptureX, CaptureY, CaptureWidth, CaptureHeight);

                DialogResult = true;
                Close();
            }
            else
            {
                SelectionRectangle.Visibility = Visibility.Collapsed;
                InfoPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        // THÊM: Method chụp màn hình
        private BitmapImage? CaptureScreenRegion(int x, int y, int width, int height)
        {
            try
            {
                using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bitmap);

                g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);

                // Convert sang BitmapImage
                using var memory = new MemoryStream();
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error capturing screen: {ex.Message}");
                return null;
            }
        }
    }
}