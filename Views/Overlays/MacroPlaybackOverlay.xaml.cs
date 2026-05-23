using FlowMy.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Lightweight fullscreen overlay shown during MacroRecorderNode playback.
    /// Click-through (WS_EX_TRANSPARENT) — input passes through to the window below.
    /// Call UpdateProgress() from the executor to update the display.
    /// Call Close() when playback finishes.
    /// </summary>
    public partial class MacroPlaybackOverlay : Window
    {
        // ─── Click-through P/Invoke ───────────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        private const int GWL_EXSTYLE       = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED     = 0x00080000;
        private const uint LWA_ALPHA        = 0x00000002;
        // Trail polyline for mouse move
        private Polyline? _trailPolyline;

        // Ghost mode: map sequenceNumber → list of canvas elements to remove when action executes
        private readonly Dictionary<int, List<UIElement>> _ghostMarkers = new();

        // Colors matching the recording overlay
        private static readonly Color ColorLeftClick      = Color.FromRgb(0x22, 0x99, 0xFF);
        private static readonly Color ColorRightClick     = Color.FromRgb(0xFF, 0x33, 0x33);
        private static readonly Color ColorShiftLeftClick = Color.FromRgb(0xFF, 0xA5, 0x00);
        private static readonly Color ColorScroll         = Color.FromRgb(0x44, 0xDD, 0x88);
        private static readonly Color ColorKeyPress       = Color.FromRgb(0xAA, 0x88, 0xFF);
        private const int MarkerRadius = 18;

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
            Loaded += (_, _) =>
            {
                DrawingCanvas.Children.Add(_trailPolyline);
                // Make window click-through so input reaches the app below
                MakeClickThrough();
            };
        }

        /// <summary>
        /// Apply WS_EX_TRANSPARENT | WS_EX_LAYERED so all mouse/keyboard input
        /// passes through this overlay to whatever window is underneath.
        /// SetLayeredWindowAttributes with LWA_ALPHA=255 keeps full visual opacity
        /// while still allowing input pass-through via WS_EX_TRANSPARENT.
        /// </summary>
        private void MakeClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            // Must set LAYERED first, then TRANSPARENT
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            // LWA_ALPHA=255 = fully opaque visually, but WS_EX_TRANSPARENT passes all input through
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
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
                int r = MarkerRadius;

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

                // Outer glow
                var glow = new Ellipse
                {
                    Width  = (r + 6) * 2,
                    Height = (r + 6) * 2,
                    Fill   = new SolidColorBrush(Color.FromArgb(50, fillColor.R, fillColor.G, fillColor.B)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(glow, pt.X - (r + 6));
                Canvas.SetTop(glow,  pt.Y - (r + 6));
                DrawingCanvas.Children.Add(glow);

                // Main circle
                var circle = new Ellipse
                {
                    Width  = r * 2,
                    Height = r * 2,
                    Fill   = new SolidColorBrush(Color.FromArgb(230, fillColor.R, fillColor.G, fillColor.B)),
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(circle, pt.X - r);
                Canvas.SetTop(circle,  pt.Y - r);
                DrawingCanvas.Children.Add(circle);

                // Center label
                var centerLabel = new TextBlock
                {
                    Text       = $"{seq}\n{lbl}",
                    FontSize   = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    LineHeight = 13,
                    IsHitTestVisible = false
                };
                centerLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(centerLabel, pt.X - centerLabel.DesiredSize.Width / 2);
                Canvas.SetTop(centerLabel,  pt.Y - centerLabel.DesiredSize.Height / 2);
                DrawingCanvas.Children.Add(centerLabel);

                FadeOutAndRemove(glow, circle, centerLabel);
            });
        }

        /// <summary>Draw a scroll indicator at screen coordinates.</summary>
        public void DrawScroll(int screenX, int screenY, int notches, int seq)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var pt = ScreenToCanvas(screenX, screenY);
                int r = MarkerRadius;
                string arrow = notches >= 0 ? "↑" : "↓";

                // Outer glow
                var glow = new Ellipse
                {
                    Width  = (r + 6) * 2,
                    Height = (r + 6) * 2,
                    Fill   = new SolidColorBrush(Color.FromArgb(50, ColorScroll.R, ColorScroll.G, ColorScroll.B)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(glow, pt.X - (r + 6));
                Canvas.SetTop(glow,  pt.Y - (r + 6));
                DrawingCanvas.Children.Add(glow);

                // Main circle
                var circle = new Ellipse
                {
                    Width  = r * 2,
                    Height = r * 2,
                    Fill   = new SolidColorBrush(Color.FromArgb(220, ColorScroll.R, ColorScroll.G, ColorScroll.B)),
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(circle, pt.X - r);
                Canvas.SetTop(circle,  pt.Y - r);
                DrawingCanvas.Children.Add(circle);

                // Center label
                var centerLabel = new TextBlock
                {
                    Text       = $"{seq}\n{arrow}",
                    FontSize   = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    LineHeight = 13,
                    IsHitTestVisible = false
                };
                centerLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(centerLabel, pt.X - centerLabel.DesiredSize.Width / 2);
                Canvas.SetTop(centerLabel,  pt.Y - centerLabel.DesiredSize.Height / 2);
                DrawingCanvas.Children.Add(centerLabel);

                FadeOutAndRemove(glow, circle, centerLabel);
            });
        }

        /// <summary>Draw a key press label at screen coordinates.</summary>
        public void DrawKeyPress(int screenX, int screenY, string keyName, int seq)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var pt = ScreenToCanvas(screenX, screenY);
                int r = MarkerRadius;

                // Outer glow
                var glow = new Ellipse
                {
                    Width  = (r + 6) * 2,
                    Height = (r + 6) * 2,
                    Fill   = new SolidColorBrush(Color.FromArgb(40, ColorKeyPress.R, ColorKeyPress.G, ColorKeyPress.B)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(glow, pt.X - (r + 6));
                Canvas.SetTop(glow,  pt.Y - (r + 6));
                DrawingCanvas.Children.Add(glow);

                // Main circle
                var circle = new Ellipse
                {
                    Width  = r * 2,
                    Height = r * 2,
                    Fill   = new SolidColorBrush(Color.FromArgb(210, ColorKeyPress.R, ColorKeyPress.G, ColorKeyPress.B)),
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(circle, pt.X - r);
                Canvas.SetTop(circle,  pt.Y - r);
                DrawingCanvas.Children.Add(circle);

                // Center label
                string displayKey = keyName.Length > 4 ? keyName[..4] : keyName;
                var centerLabel = new TextBlock
                {
                    Text       = $"{seq}\n{displayKey}",
                    FontSize   = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    LineHeight = 12,
                    IsHitTestVisible = false
                };
                centerLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(centerLabel, pt.X - centerLabel.DesiredSize.Width / 2);
                Canvas.SetTop(centerLabel,  pt.Y - centerLabel.DesiredSize.Height / 2);
                DrawingCanvas.Children.Add(centerLabel);

                FadeOutAndRemove(glow, circle, centerLabel);
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
                _ghostMarkers.Clear();
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

        // ─── Ghost mode API ───────────────────────────────────────────────────────

        /// <summary>
        /// Ghost mode: pre-draw all action markers at once (dimmed/ghost style).
        /// As each action executes, call RemoveGhostMarker(seq) to remove it.
        /// Must be called on UI thread.
        /// </summary>
        public void PreDrawGhostMarkers(IReadOnlyList<MacroAction> actions)
        {
            _ghostMarkers.Clear();

            // ── Pass 1: draw mouse-move trail ─────────────────────────────────────
            var ghostTrail = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromArgb(60, 200, 200, 200)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 4 },
                IsHitTestVisible = false
            };
            DrawingCanvas.Children.Add(ghostTrail);

            foreach (var action in actions)
            {
                if (action.Type is "MouseMove" or "MouseDown" or "MouseUp" or "MouseClick")
                    ghostTrail.Points.Add(ScreenToCanvas(action.X, action.Y));
            }

            // ── Pass 2: connector lines between click/key actions (numbered flow) ─
            // Collect only "significant" actions (not MouseMove) in order
            var significant = new List<MacroAction>();
            foreach (var a in actions)
            {
                if (a.Type is "MouseClick" or "MouseDown" or "MouseUp" or "KeyPress" or "MouseScroll")
                    significant.Add(a);
            }

            for (int i = 0; i < significant.Count - 1; i++)
            {
                var from = ScreenToCanvas(significant[i].X, significant[i].Y);
                var to   = ScreenToCanvas(significant[i + 1].X, significant[i + 1].Y);

                // Only draw connector if points are far enough apart
                double dist = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
                if (dist < 8) continue;

                var connector = new Line
                {
                    X1 = from.X, Y1 = from.Y,
                    X2 = to.X,   Y2 = to.Y,
                    Stroke = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 5 },
                    IsHitTestVisible = false
                };
                DrawingCanvas.Children.Add(connector);
                // Connectors are background — not tracked in _ghostMarkers (stay visible throughout)
            }

            // ── Pass 3: draw ghost markers for each significant action ─────────────
            foreach (var action in actions)
            {
                var elements = new List<UIElement>();

                switch (action.Type)
                {
                    case "MouseClick":
                    case "MouseDown":
                        DrawGhostClick(action.X, action.Y, action.Button ?? "Left", action.SequenceNumber, elements);
                        break;
                    case "MouseUp":
                        DrawGhostMouseUp(action.X, action.Y, action.SequenceNumber, elements);
                        break;
                    case "KeyPress":
                        if (!string.IsNullOrWhiteSpace(action.Key))
                            DrawGhostKeyPress(action.X, action.Y, action.Key, action.SequenceNumber, elements);
                        break;
                    case "MouseScroll":
                        DrawGhostScroll(action.X, action.Y, action.ScrollDelta, action.SequenceNumber, elements);
                        break;
                }

                if (elements.Count > 0)
                    _ghostMarkers[action.SequenceNumber] = elements;
            }
        }

        /// <summary>
        /// Ghost mode: remove the pre-drawn marker for the given sequence number
        /// (called when that action is about to execute).
        /// </summary>
        public void RemoveGhostMarker(int sequenceNumber)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!_ghostMarkers.TryGetValue(sequenceNumber, out var elements)) return;
                _ghostMarkers.Remove(sequenceNumber);
                foreach (var el in elements)
                {
                    // Fade out quickly instead of instant remove
                    var anim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(300));
                    anim.Completed += (_, _) =>
                    {
                        if (DrawingCanvas.Children.Contains(el))
                            DrawingCanvas.Children.Remove(el);
                    };
                    el.BeginAnimation(UIElement.OpacityProperty, anim);
                }
            });
        }

        // ─── Ghost drawing helpers ────────────────────────────────────────────────

        private void DrawGhostClick(int screenX, int screenY, string button, int seq, List<UIElement> elements)
        {
            var pt = ScreenToCanvas(screenX, screenY);
            Color fillColor = button switch
            {
                "Left"      => ColorLeftClick,
                "Right"     => ColorRightClick,
                "ShiftLeft" => ColorShiftLeftClick,
                _           => ColorLeftClick
            };
            string label = button switch
            {
                "Left"      => "L",
                "Right"     => "R",
                "ShiftLeft" => "⇧L",
                _           => "?"
            };

            // Ghost circle — dashed border, semi-transparent fill
            var circle = new Ellipse
            {
                Width  = MarkerRadius * 2,
                Height = MarkerRadius * 2,
                Fill   = new SolidColorBrush(Color.FromArgb(50, fillColor.R, fillColor.G, fillColor.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(180, fillColor.R, fillColor.G, fillColor.B)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(circle, pt.X - MarkerRadius);
            Canvas.SetTop(circle,  pt.Y - MarkerRadius);
            DrawingCanvas.Children.Add(circle);
            elements.Add(circle);

            // "+" cross — horizontal bar (ghost, semi-transparent)
            var hBar = new Rectangle
            {
                Width  = MarkerRadius,
                Height = 2,
                Fill   = new SolidColorBrush(Color.FromArgb(160, fillColor.R, fillColor.G, fillColor.B)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(hBar, pt.X - MarkerRadius / 2.0);
            Canvas.SetTop(hBar,  pt.Y - 1);
            DrawingCanvas.Children.Add(hBar);
            elements.Add(hBar);

            // "+" cross — vertical bar
            var vBar = new Rectangle
            {
                Width  = 2,
                Height = MarkerRadius,
                Fill   = new SolidColorBrush(Color.FromArgb(160, fillColor.R, fillColor.G, fillColor.B)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(vBar, pt.X - 1);
            Canvas.SetTop(vBar,  pt.Y - MarkerRadius / 2.0);
            DrawingCanvas.Children.Add(vBar);
            elements.Add(vBar);

            // Sequence label pill
            var pill = new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(120, 10, 10, 10)),
                CornerRadius = new CornerRadius(4),
                BorderBrush  = new SolidColorBrush(Color.FromArgb(100, fillColor.R, fillColor.G, fillColor.B)),
                BorderThickness = new Thickness(1),
                Padding      = new Thickness(4, 1, 4, 1),
                Child        = new TextBlock
                {
                    Text       = $"[{seq}] {label}",
                    FontSize   = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
                },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(pill, pt.X + MarkerRadius + 2);
            Canvas.SetTop(pill,  pt.Y - MarkerRadius);
            DrawingCanvas.Children.Add(pill);
            elements.Add(pill);
        }

        private void DrawGhostMouseUp(int screenX, int screenY, int seq, List<UIElement> elements)
        {
            var pt = ScreenToCanvas(screenX, screenY);
            // Dashed open circle — orange, marks drag release point
            var circle = new Ellipse
            {
                Width  = MarkerRadius * 2,
                Height = MarkerRadius * 2,
                Fill   = new SolidColorBrush(Color.FromArgb(25, 0xFF, 0xA5, 0x00)),
                Stroke = new SolidColorBrush(Color.FromArgb(150, 0xFF, 0xA5, 0x00)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(circle, pt.X - MarkerRadius);
            Canvas.SetTop(circle,  pt.Y - MarkerRadius);
            DrawingCanvas.Children.Add(circle);
            elements.Add(circle);

            // Small upward arrow indicator
            var arrow = new TextBlock
            {
                Text       = "↑",
                FontSize   = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(160, 0xFF, 0xA5, 0x00)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(arrow, pt.X - 5);
            Canvas.SetTop(arrow,  pt.Y - 7);
            DrawingCanvas.Children.Add(arrow);
            elements.Add(arrow);

            var pill = new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(100, 10, 10, 10)),
                CornerRadius = new CornerRadius(4),
                BorderBrush  = new SolidColorBrush(Color.FromArgb(100, 0xFF, 0xA5, 0x00)),
                BorderThickness = new Thickness(1),
                Padding      = new Thickness(4, 1, 4, 1),
                Child        = new TextBlock
                {
                    Text       = $"[{seq}] ↑L",
                    FontSize   = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
                },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(pill, pt.X + MarkerRadius + 2);
            Canvas.SetTop(pill,  pt.Y - MarkerRadius);
            DrawingCanvas.Children.Add(pill);
            elements.Add(pill);
        }

        private void DrawGhostKeyPress(int screenX, int screenY, string keyName, int seq, List<UIElement> elements)
        {
            var pt = ScreenToCanvas(screenX, screenY);
            var pill = new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(80, 30, 30, 60)),
                CornerRadius = new CornerRadius(4),
                BorderBrush  = new SolidColorBrush(Color.FromArgb(120, ColorKeyPress.R, ColorKeyPress.G, ColorKeyPress.B)),
                BorderThickness = new Thickness(1),
                Padding      = new Thickness(4, 1, 4, 1),
                Child        = new TextBlock
                {
                    Text       = $"[{seq}] {keyName}",
                    FontSize   = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255))
                },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(pill, pt.X + 8);
            Canvas.SetTop(pill,  pt.Y - 8);
            DrawingCanvas.Children.Add(pill);
            elements.Add(pill);
        }

        private void DrawGhostScroll(int screenX, int screenY, int notches, int seq, List<UIElement> elements)
        {
            var pt = ScreenToCanvas(screenX, screenY);
            string arrow = notches >= 0 ? "▲" : "▼";
            var pill = new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(80, ColorScroll.R, ColorScroll.G, ColorScroll.B)),
                CornerRadius = new CornerRadius(6),
                Padding      = new Thickness(5, 2, 5, 2),
                Child        = new TextBlock
                {
                    Text       = $"[{seq}] {arrow} {Math.Abs(notches)}",
                    FontSize   = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255))
                },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(pill, pt.X + 10);
            Canvas.SetTop(pill,  pt.Y - 10);
            DrawingCanvas.Children.Add(pill);
            elements.Add(pill);
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
                Interval = TimeSpan.FromMilliseconds(1200)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                foreach (var el in elements)
                {
                    if (!DrawingCanvas.Children.Contains(el)) continue;
                    var anim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(400));
                    anim.Completed += (_, _) =>
                    {
                        if (DrawingCanvas.Children.Contains(el))
                            DrawingCanvas.Children.Remove(el);
                    };
                    el.BeginAnimation(UIElement.OpacityProperty, anim);
                }
            };
            timer.Start();
        }
    }
}
