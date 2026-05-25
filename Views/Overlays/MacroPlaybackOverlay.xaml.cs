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
using System.Windows.Threading;

namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Lightweight overlay shown during MacroRecorderNode playback.
    /// In Free mode: fullscreen (WindowState=Maximized).
    /// In TargetApp mode: sized/positioned over the target window via PositionOverTarget().
    /// Click-through (WS_EX_TRANSPARENT) — input passes through to the window below.
    /// </summary>
    public partial class MacroPlaybackOverlay : Window
    {
        // ─── Click-through P/Invoke ───────────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT pt);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        private const int GWL_EXSTYLE       = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        // ─── Target-mode positioning ──────────────────────────────────────────────
        /// <summary>True when overlay is positioned over a specific target window (not fullscreen).</summary>
        private bool _isTargetMode = false;
        /// <summary>Screen-pixel offset of the overlay's top-left corner (used in ScreenToCanvas).</summary>
        private double _overlayScreenLeft = 0;
        private double _overlayScreenTop  = 0;
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
        private const int MarkerRadius = 12;

        // TaskCompletionSource to signal when the overlay is fully loaded and ready
        private readonly TaskCompletionSource _loadedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Awaitable: resolves when the Loaded event has fired and the overlay is fully ready
        /// (DrawingCanvas populated, click-through applied, PresentationSource available).
        /// </summary>
        public Task WhenLoaded => _loadedTcs.Task;

        // ─── Hover-fade timer ─────────────────────────────────────────────────────
        private DispatcherTimer? _hoverTimer;

        public MacroPlaybackOverlay()
        {
            InitializeComponent();

            // Create trail polyline — added to canvas in Loaded to ensure DrawingCanvas is ready
            _trailPolyline = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromArgb(140, 100, 200, 255)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false
            };
            Loaded += (_, _) =>
            {
                // Free mode: go fullscreen
                WindowState = WindowState.Maximized;
                DrawingCanvas.Children.Add(_trailPolyline);
                MakeClickThrough();
                StartHoverFadeTimer();
                _loadedTcs.TrySetResult();
            };
        }

        /// <summary>
        /// Position this overlay exactly over the given target window (TargetApp mode).
        /// Must be called BEFORE Show(), or immediately after Show() before WhenLoaded resolves.
        /// </summary>
        public void PositionOverTarget(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero) return;
            if (!GetWindowRect(targetHwnd, out RECT r)) return;

            _isTargetMode    = true;
            _overlayScreenLeft = r.Left;
            _overlayScreenTop  = r.Top;

            // Convert screen pixels → WPF device-independent units
            // We need the DPI scale; use SystemParameters as a fallback before the window is loaded.
            double dpiX = 96.0, dpiY = 96.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
            }

            Left   = r.Left   / (dpiX / 96.0);
            Top    = r.Top    / (dpiY / 96.0);
            Width  = (r.Right  - r.Left) / (dpiX / 96.0);
            Height = (r.Bottom - r.Top)  / (dpiY / 96.0);
        }

        /// <summary>
        /// Variant of PositionOverTarget that runs on the UI thread and waits for the
        /// window to be loaded so PresentationSource is available for accurate DPI scaling.
        /// Call this after Show() + await WhenLoaded.
        /// </summary>
        public void PositionOverTargetAfterLoad(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero) return;
            if (!GetWindowRect(targetHwnd, out RECT r)) return;

            _isTargetMode      = true;
            _overlayScreenLeft = r.Left;
            _overlayScreenTop  = r.Top;

            double scaleX = 1.0, scaleY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            Left   = r.Left   / scaleX;
            Top    = r.Top    / scaleY;
            Width  = (r.Right  - r.Left) / scaleX;
            Height = (r.Bottom - r.Top)  / scaleY;
        }

        /// <summary>
        /// Apply WS_EX_TRANSPARENT so all mouse/keyboard input passes through this overlay
        /// to whatever window is underneath.
        /// NOTE: Do NOT call SetLayeredWindowAttributes — WPF manages the layered window
        /// internally when AllowsTransparency=True. Overriding it breaks WPF rendering.
        /// WS_EX_TRANSPARENT alone is sufficient for click-through on a WPF transparent window.
        /// </summary>
        private void MakeClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            // Only add WS_EX_TRANSPARENT — WPF already sets WS_EX_LAYERED via AllowsTransparency
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT);
        }

        /// <summary>
        /// Poll real cursor position every 100 ms.
        /// If the cursor is over StatusPanel or ProgressPanel → fade those panels to 20% opacity.
        /// When cursor leaves → restore to 100%.
        /// </summary>
        private void StartHoverFadeTimer()
        {
            _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _hoverTimer.Tick += (_, _) =>
            {
                if (!GetCursorPos(out POINT cur)) return;

                bool overStatus   = IsPointOverElement(StatusPanel,   cur.X, cur.Y);
                bool overProgress = IsPointOverElement(ProgressPanel, cur.X, cur.Y);

                SetPanelOpacity(StatusPanel,   overStatus   ? 0.2 : 1.0);
                SetPanelOpacity(ProgressPanel, overProgress ? 0.2 : 1.0);
            };
            _hoverTimer.Start();

            // Stop timer when window closes
            Closed += (_, _) => _hoverTimer?.Stop();
        }

        /// <summary>Check whether a screen-pixel point (px, py) lies within a FrameworkElement's screen bounds.</summary>
        private bool IsPointOverElement(FrameworkElement el, int px, int py)
        {
            if (!el.IsVisible) return false;
            try
            {
                var topLeft     = el.PointToScreen(new System.Windows.Point(0, 0));
                var bottomRight = el.PointToScreen(new System.Windows.Point(el.ActualWidth, el.ActualHeight));
                return px >= topLeft.X && px <= bottomRight.X
                    && py >= topLeft.Y && py <= bottomRight.Y;
            }
            catch { return false; }
        }

        private static void SetPanelOpacity(UIElement el, double target)
        {
            if (Math.Abs(el.Opacity - target) < 0.01) return;
            var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(150));
            el.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        // ─── Public API called from executor ─────────────────────────────────────

        /// <summary>
        /// Hiển thị countdown "Bắt đầu sau Xs..." trên overlay, minimize main window,
        /// rồi resolve task khi countdown xong để executor bắt đầu phát lại.
        /// </summary>
        public Task ShowCountdownAsync(int seconds, Window? mainWindow)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatcher.BeginInvoke(async () =>
            {
                // Minimize main window để app target lên foreground
                if (mainWindow != null)
                    mainWindow.WindowState = WindowState.Minimized;

                for (int i = seconds; i > 0; i--)
                {
                    StatusText.Text = $"Bắt đầu sau {i}s... (chuyển sang app cần thao tác)";
                    await Task.Delay(1000);
                }
                StatusText.Text = "Đang phát lại thao tác...";
                tcs.TrySetResult();
            });
            return tcs.Task;
        }
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

        /// <summary>
        /// Di chuyển chuột ảo đến tọa độ màn hình (screenX, screenY).
        /// syncBeforeAction=true: dùng Invoke (sync) để cursor hiển thị trước khi action thực thi.
        /// syncBeforeAction=false: dùng BeginInvoke (async) cho MouseMove liên tục, không block executor.
        /// </summary>
        public void MoveVirtualCursor(int screenX, int screenY, bool syncBeforeAction = false)
        {
            void Update()
            {
                var pt = ScreenToCanvas(screenX, screenY);
                VirtualCursor.Visibility = Visibility.Visible;
                Canvas.SetLeft(VirtualCursor, pt.X);
                Canvas.SetTop(VirtualCursor, pt.Y);
                // Reposition floating tooltip relative to cursor, avoiding screen edges
                RepositionHint(pt.X, pt.Y);
            }

            if (syncBeforeAction)
                Dispatcher.Invoke(Update);
            else
                Dispatcher.BeginInvoke(Update);
        }

        /// <summary>
        /// Tính toán vị trí tooltip sát chuột ảo, tự động tránh bị khuất khi gần cạnh màn hình.
        /// Ưu tiên: phải-dưới → trái-dưới → phải-trên → trái-trên
        /// </summary>
        private void RepositionHint(double cursorX, double cursorY)
        {
            if (ActionHintBorder.Visibility != Visibility.Visible) return;

            // Measure hint size (use last desired size if not yet measured)
            ActionHintBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double hw = ActionHintBorder.DesiredSize.Width;
            double hh = ActionHintBorder.DesiredSize.Height;

            double screenW = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
            double screenH = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;

            const double offsetX = 18; // horizontal gap from cursor tip
            const double offsetY = 18; // vertical gap from cursor tip
            const double margin  = 12; // min distance from screen edge

            // Try right-bottom first (default)
            double left = cursorX + offsetX;
            double top  = cursorY + offsetY;

            // Flip horizontal if would overflow right edge
            if (left + hw > screenW - margin)
                left = cursorX - hw - offsetX;

            // Flip vertical if would overflow bottom edge
            if (top + hh > screenH - margin)
                top = cursorY - hh - offsetY;

            // Clamp to screen bounds
            left = Math.Max(margin, Math.Min(left, screenW - hw - margin));
            top  = Math.Max(margin, Math.Min(top,  screenH - hh - margin));

            Canvas.SetLeft(ActionHintBorder, left);
            Canvas.SetTop(ActionHintBorder,  top);
        }

        /// <summary>
        /// Hiển thị hint hành động tại vị trí chuột ảo (ví dụ: "Đang nhấn L", "Giữ L...").
        /// text=null để ẩn hint.
        /// </summary>
        public void ShowActionHint(string? text)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ActionHintText.Text = text ?? "";
                ActionHintBorder.Visibility = string.IsNullOrEmpty(text)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            });
        }

        /// <summary>
        /// Hiển thị thông tin thao tác trên floating tooltip sát chuột ảo.
        /// keyText: phím hoặc tổ hợp phím (ví dụ: "Ctrl+L", "Shift+Alt+R", "L") — null để ẩn
        /// descText: mô tả đang xử lý gì (ví dụ: "Đang nhấn chuột trái", "Giữ chuột phải")
        /// </summary>
        public void ShowRightActionInfo(string? keyText, string? descText)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (string.IsNullOrEmpty(keyText) && string.IsNullOrEmpty(descText))
                {
                    ActionHintBorder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Key line — only show if there's a key text
                    if (!string.IsNullOrEmpty(keyText))
                    {
                        ActionHintKeyText.Text = keyText;
                        ActionHintKeyText.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ActionHintKeyText.Visibility = Visibility.Collapsed;
                    }

                    ActionHintText.Text = descText ?? "";
                    ActionHintBorder.Visibility = Visibility.Visible;

                    // Reposition based on current cursor position
                    double curX = Canvas.GetLeft(VirtualCursor);
                    double curY = Canvas.GetTop(VirtualCursor);
                    if (!double.IsNaN(curX) && !double.IsNaN(curY))
                        RepositionHint(curX, curY);
                }
            });
        }

        public void UpdateHeldModifiers(bool shift, bool ctrl, bool alt)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var heldMods = new List<string>();
                if (ctrl) heldMods.Add("Ctrl");
                if (alt) heldMods.Add("Alt");
                if (shift) heldMods.Add("Shift");

                if (heldMods.Count > 0)
                {
                    HeldKeysPanel.Visibility = Visibility.Visible;
                    HeldKeysStack.Children.Clear();
                    foreach (var name in heldMods)
                    {
                        var b = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(200, 30, 120, 255)),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(8, 4, 8, 4),
                            Margin = new Thickness(0, 0, 4, 0)
                        };
                        b.Child = new TextBlock { Text = name, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 };
                        HeldKeysStack.Children.Add(b);
                    }
                }
                else
                {
                    HeldKeysPanel.Visibility = Visibility.Collapsed;
                    HeldKeysStack.Children.Clear();
                }
            });
        }

        /// <summary>Draw a click marker at screen coordinates.</summary>
        public void DrawClick(int screenX, int screenY, bool isRight, int seq, string label)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var pt = ScreenToCanvas(screenX, screenY);
                int r = MarkerRadius;

                Color fillColor = isRight ? ColorRightClick : ColorLeftClick;

                // The main marker border that adapts its width
                var mainMarker = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(225, fillColor.R, fillColor.G, fillColor.B)),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(r),
                    MinWidth = r * 2,
                    MinHeight = r * 2,
                    Padding = new Thickness(5, 0, 5, 0),
                    Child = new TextBlock
                    {
                        Text = seq.ToString(),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false
                    },
                    IsHitTestVisible = false
                };

                mainMarker.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double cw = Math.Max(r * 2, mainMarker.DesiredSize.Width);
                double ch = r * 2;

                // Outer glow
                var glow = new Border
                {
                    Width = cw + 8,
                    Height = ch + 8,
                    CornerRadius = new CornerRadius(r + 4),
                    Background = new SolidColorBrush(Color.FromArgb(50, fillColor.R, fillColor.G, fillColor.B)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(glow, pt.X - cw / 2.0 - 4);
                Canvas.SetTop(glow, pt.Y - ch / 2.0 - 4);
                DrawingCanvas.Children.Add(glow);

                Canvas.SetLeft(mainMarker, pt.X - cw / 2.0);
                Canvas.SetTop(mainMarker, pt.Y - ch / 2.0);
                DrawingCanvas.Children.Add(mainMarker);

                // Action badge: sleek pill next to circle showing the combo/keys
                var actionBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(180, fillColor.R, fillColor.G, fillColor.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    Child = new TextBlock
                    {
                        Text = label,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White,
                        IsHitTestVisible = false
                    },
                    IsHitTestVisible = false
                };
                actionBadge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(actionBadge, pt.X + cw / 2.0 + 6);
                Canvas.SetTop(actionBadge, pt.Y - actionBadge.DesiredSize.Height / 2.0);
                DrawingCanvas.Children.Add(actionBadge);

                FadeOutAndRemove(glow, mainMarker, actionBadge);
            });
        }

        /// <summary>Draw a scroll indicator at screen coordinates.</summary>
        public void DrawScroll(int screenX, int screenY, int notches, int seq)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var pt = ScreenToCanvas(screenX, screenY);
                int r = MarkerRadius;
                string arrow = notches >= 0 ? "↑ Scroll" : "↓ Scroll";

                var mainMarker = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(220, ColorScroll.R, ColorScroll.G, ColorScroll.B)),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(r),
                    MinWidth = r * 2,
                    MinHeight = r * 2,
                    Padding = new Thickness(5, 0, 5, 0),
                    Child = new TextBlock
                    {
                        Text = seq.ToString(),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false
                    },
                    IsHitTestVisible = false
                };

                mainMarker.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double cw = Math.Max(r * 2, mainMarker.DesiredSize.Width);
                double ch = r * 2;

                var glow = new Border
                {
                    Width = cw + 8,
                    Height = ch + 8,
                    CornerRadius = new CornerRadius(r + 4),
                    Background = new SolidColorBrush(Color.FromArgb(50, ColorScroll.R, ColorScroll.G, ColorScroll.B)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(glow, pt.X - cw / 2.0 - 4);
                Canvas.SetTop(glow, pt.Y - ch / 2.0 - 4);
                DrawingCanvas.Children.Add(glow);

                Canvas.SetLeft(mainMarker, pt.X - cw / 2.0);
                Canvas.SetTop(mainMarker, pt.Y - ch / 2.0);
                DrawingCanvas.Children.Add(mainMarker);

                // Pill
                var actionBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(180, ColorScroll.R, ColorScroll.G, ColorScroll.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    Child = new TextBlock
                    {
                        Text = arrow, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, IsHitTestVisible = false
                    },
                    IsHitTestVisible = false
                };
                actionBadge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(actionBadge, pt.X + cw / 2.0 + 6);
                Canvas.SetTop(actionBadge, pt.Y - actionBadge.DesiredSize.Height / 2.0);
                DrawingCanvas.Children.Add(actionBadge);

                FadeOutAndRemove(glow, mainMarker, actionBadge);
            });
        }

        /// <summary>Draw a key press label at screen coordinates.</summary>
        public void DrawKeyPress(int screenX, int screenY, string keyName, int seq)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var pt = ScreenToCanvas(screenX, screenY);
                int r = MarkerRadius;
                bool isCombo = keyName.Contains('+');

                // Combo = orange, single key = purple
                Color fillColor = isCombo
                    ? Color.FromRgb(0xFF, 0xAA, 0x00)
                    : ColorKeyPress;
                if (isCombo) r = MarkerRadius + 4;

                var mainMarker = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(225, fillColor.R, fillColor.G, fillColor.B)),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(isCombo ? 2 : 1.5),
                    CornerRadius = new CornerRadius(r),
                    MinWidth = r * 2,
                    MinHeight = r * 2,
                    Padding = new Thickness(5, 0, 5, 0),
                    Child = new TextBlock
                    {
                        Text = seq.ToString(),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsHitTestVisible = false
                    },
                    IsHitTestVisible = false
                };

                mainMarker.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double cw = Math.Max(r * 2, mainMarker.DesiredSize.Width);
                double ch = r * 2;

                var glow = new Border
                {
                    Width = cw + 8,
                    Height = ch + 8,
                    CornerRadius = new CornerRadius(r + 4),
                    Background = new SolidColorBrush(Color.FromArgb(50, fillColor.R, fillColor.G, fillColor.B)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(glow, pt.X - cw / 2.0 - 4);
                Canvas.SetTop(glow, pt.Y - ch / 2.0 - 4);
                DrawingCanvas.Children.Add(glow);

                Canvas.SetLeft(mainMarker, pt.X - cw / 2.0);
                Canvas.SetTop(mainMarker, pt.Y - ch / 2.0);
                DrawingCanvas.Children.Add(mainMarker);

                // Pill Badge
                var actionBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(180, fillColor.R, fillColor.G, fillColor.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    Child = new TextBlock
                    {
                        Text = isCombo ? keyName.Replace("+", " + ") : keyName, // Better spacing
                        FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, IsHitTestVisible = false
                    },
                    IsHitTestVisible = false
                };
                actionBadge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(actionBadge, pt.X + cw / 2.0 + 6);
                Canvas.SetTop(actionBadge, pt.Y - actionBadge.DesiredSize.Height / 2.0);
                DrawingCanvas.Children.Add(actionBadge);

                FadeOutAndRemove(glow, mainMarker, actionBadge);
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
                VirtualCursor.Visibility = Visibility.Collapsed;
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

        /// <summary>
        /// Trích xuất phần modifier từ button string.
        /// Ví dụ: "Ctrl+L" → "Ctrl", "Shift+Alt+R" → "Shift+Alt", "L" → "", "R" → ""
        /// </summary>
        private static string ExtractModifierPart(string button)
        {
            if (string.IsNullOrEmpty(button)) return "";
            
            // Tìm dấu "+" cuối cùng
            int lastPlusIndex = button.LastIndexOf('+');
            if (lastPlusIndex < 0) return ""; // không có modifier
            
            // Trả về phần trước dấu "+" cuối cùng
            return button[..lastPlusIndex];
        }

        private System.Windows.Point ScreenToCanvas(int screenX, int screenY)
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                if (_isTargetMode)
                {
                    // In target mode the overlay starts at (_overlayScreenLeft, _overlayScreenTop).
                    // Subtract the overlay origin so coordinates are relative to the overlay's top-left.
                    double scaleX = source.CompositionTarget.TransformToDevice.M11;
                    double scaleY = source.CompositionTarget.TransformToDevice.M22;
                    double relX = screenX - _overlayScreenLeft;
                    double relY = screenY - _overlayScreenTop;
                    // Convert device pixels → WPF DIPs
                    return new System.Windows.Point(relX / scaleX, relY / scaleY);
                }
                // Free / fullscreen mode: standard device→DIP transform
                return source.CompositionTarget.TransformFromDevice
                             .Transform(new System.Windows.Point(screenX, screenY));
            }
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
