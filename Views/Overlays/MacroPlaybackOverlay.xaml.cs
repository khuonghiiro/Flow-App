using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Lightweight fullscreen overlay shown during MacroRecorderNode playback.
    /// Non-interactive (IsHitTestVisible=False) — just visual feedback.
    /// Call UpdateProgress() from the executor to update the display.
    /// Call Close() when playback finishes.
    /// </summary>
    public partial class MacroPlaybackOverlay : Window
    {
        // Trail polyline for mouse move
        private Polyline? _trailPolyline;

        // Colors matching the recording overlay
        private static readonly Color ColorLeftClick      = Color.FromRgb(0x22, 0x99, 0xFF);
        private static readonly Color ColorRightClick     = Color.FromRgb(0xFF, 0x33, 0x33);
        private static readonly Color ColorShiftLeftClick = Color.FromRgb(0xFF, 0xA5, 0x00);
        private static readonly Color ColorScroll         = Color.FromRgb(0x44, 0xDD, 0x88);
        private const int MarkerRadius = 14;

        public MacroPlaybackOverlay()
        {
            InitializeComponent();

            // Create trail polyline
            _trailPolyline = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromArgb(140, 100, 200, 255)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false
            };
            Loaded += (_, _) => DrawingCanvas.Children.Add(_trailPolyline);
        }

        // ─── Public API called from executor ─────────────────────────────────────

        /// <summary>Update cycle and action progress labels.</summary>
        public void UpdateProgress(int cycle, int totalCycles, int actionIndex, int totalActions)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ProgressText.Text = totalCycles > 1
                    ? $"Chu kỳ {cycle}/{totalCycles}"
                    : "Phát lại";
                ActionText.Text = $"Thao tác {actionIndex} / {totalActions}";
            });
        }

        /// <summary>Draw a click marker at screen coordinates.</summary>
        public void DrawClick(int screenX, int screenY, string button, int seq)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var pt = ScreenToCanvas(screenX, screenY);

                Color fillColor = button switch
                {
                    "Left"      => ColorLeftClick,
                    "Right"     => ColorRightClick,
                    "ShiftLeft" => ColorShiftLeftClick,
                    _           => ColorLeftClick
                };
                string lbl = button switch
                {
                    "Left"      => "L",
                    "Right"     => "R",
                    "ShiftLeft" => "⇧L",
                    _           => "?"
                };

                // Circle
                var circle = new Ellipse
                {
                    Width  = MarkerRadius * 2,
                    Height = MarkerRadius * 2,
                    Fill   = new SolidColorBrush(Color.FromArgb(200, fillColor.R, fillColor.G, fillColor.B)),
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(circle, pt.X - MarkerRadius);
                Canvas.SetTop(circle,  pt.Y - MarkerRadius);
                DrawingCanvas.Children.Add(circle);

                // "+" horizontal
                var hBar = new Rectangle { Width = MarkerRadius, Height = 2.5, Fill = Brushes.White, IsHitTestVisible = false };
                Canvas.SetLeft(hBar, pt.X - MarkerRadius / 2.0);
                Canvas.SetTop(hBar,  pt.Y - 1.25);
                DrawingCanvas.Children.Add(hBar);

                // "+" vertical
                var vBar = new Rectangle { Width = 2.5, Height = MarkerRadius, Fill = Brushes.White, IsHitTestVisible = false };
                Canvas.SetLeft(vBar, pt.X - 1.25);
                Canvas.SetTop(vBar,  pt.Y - MarkerRadius / 2.0);
                DrawingCanvas.Children.Add(vBar);

                // Label pill
                var pill = new Border
                {
                    Background   = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20)),
                    CornerRadius = new CornerRadius(4),
                    Padding      = new Thickness(5, 2, 5, 2),
                    Child        = new TextBlock
                    {
                        Text       = $"[{seq}] {lbl}",
                        FontSize   = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White
                    },
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(pill, pt.X + MarkerRadius + 3);
                Canvas.SetTop(pill,  pt.Y - MarkerRadius);
                DrawingCanvas.Children.Add(pill);

                // Fade out after 1.5 s
                FadeOutAndRemove(circle, hBar, vBar, pill);
            });
        }

        /// <summary>Draw a scroll indicator at screen coordinates.</summary>
        public void DrawScroll(int screenX, int screenY, int notches, int seq)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var pt = ScreenToCanvas(screenX, screenY);
                string arrow = notches >= 0 ? "▲" : "▼";

                var pill = new Border
                {
                    Background   = new SolidColorBrush(Color.FromArgb(200, ColorScroll.R, ColorScroll.G, ColorScroll.B)),
                    CornerRadius = new CornerRadius(6),
                    Padding      = new Thickness(6, 3, 6, 3),
                    Child        = new TextBlock
                    {
                        Text       = $"[{seq}] {arrow} {Math.Abs(notches)}",
                        FontSize   = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White
                    },
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(pill, pt.X + 10);
                Canvas.SetTop(pill,  pt.Y - 12);
                DrawingCanvas.Children.Add(pill);

                FadeOutAndRemove(pill);
            });
        }

        /// <summary>Draw a key press label at screen coordinates.</summary>
        public void DrawKeyPress(int screenX, int screenY, string keyName, int seq)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var pt = ScreenToCanvas(screenX, screenY);

                var pill = new Border
                {
                    Background   = new SolidColorBrush(Color.FromArgb(210, 30, 30, 30)),
                    CornerRadius = new CornerRadius(4),
                    Padding      = new Thickness(5, 2, 5, 2),
                    Child        = new TextBlock
                    {
                        Text       = $"[{seq}] {keyName}",
                        FontSize   = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    },
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(pill, pt.X + 10);
                Canvas.SetTop(pill,  pt.Y - 10);
                DrawingCanvas.Children.Add(pill);

                FadeOutAndRemove(pill);
            });
        }

        /// <summary>Add a point to the mouse trail.</summary>
        public void AddTrailPoint(int screenX, int screenY)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_trailPolyline == null) return;
                _trailPolyline.Points.Add(ScreenToCanvas(screenX, screenY));
            });
        }

        /// <summary>Clear all visuals (called between cycles).</summary>
        public void ClearVisuals()
        {
            Dispatcher.BeginInvoke(() =>
            {
                DrawingCanvas.Children.Clear();
                _trailPolyline = new Polyline
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(140, 100, 200, 255)),
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    IsHitTestVisible = false
                };
                DrawingCanvas.Children.Add(_trailPolyline);
            });
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private System.Windows.Point ScreenToCanvas(int screenX, int screenY)
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
                return source.CompositionTarget.TransformFromDevice
                             .Transform(new System.Windows.Point(screenX, screenY));
            return new System.Windows.Point(screenX, screenY);
        }

        /// <summary>Fade out elements after 1.5 s and remove from canvas.</summary>
        private void FadeOutAndRemove(params UIElement[] elements)
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                foreach (var el in elements)
                {
                    if (DrawingCanvas.Children.Contains(el))
                        DrawingCanvas.Children.Remove(el);
                }
            };
            timer.Start();
        }
    }
}
