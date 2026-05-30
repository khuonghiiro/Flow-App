using FlowMy.Models;
using FlowMy.Models.Nodes;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Overlay hiển thị viền sáng màn hình với cấu hình màu, độ dày, hiệu ứng.
    /// Có thể hiển thị fullscreen hoặc over target window.
    /// Click-through (WS_EX_TRANSPARENT) — input passes through.
    /// </summary>
    public partial class BorderHighlightOverlay : Window
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
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        // ─── Target-mode positioning ──────────────────────────────────────────────
        private bool _isTargetMode = false;
        private IntPtr _targetHwnd = IntPtr.Zero;
        private DispatcherTimer? _repositionTimer;

        // ─── Prepare target mode before showing ────────────────────────────────────
        public void PrepareForTargetMode()
        {
            _isTargetMode = true;
        }

        // ─── Animation ─────────────────────────────────────────────────────────────
        private Storyboard? _animationStoryboard;

        // TaskCompletionSource to signal when the overlay is fully loaded
        private readonly TaskCompletionSource _loadedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WhenLoaded => _loadedTcs.Task;

        public BorderHighlightOverlay()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                if (!_isTargetMode)
                    WindowState = WindowState.Maximized;
                MakeClickThrough();
                _loadedTcs.TrySetResult();
            };
        }

        /// <summary>
        /// Cấu hình overlay với các tham số từ BorderHighlightNode.
        /// </summary>
        public void Configure(string borderColorHex, int borderThickness, int gradientSize, 
                               double opacity, BorderEffectType effectType)
        {
            Dispatcher.BeginInvoke(() =>
            {
                // Parse color
                var color = ParseColor(borderColorHex);

                // Set outer border
                OuterBorder.BorderBrush = new SolidColorBrush(color);
                OuterBorder.BorderThickness = new Thickness(borderThickness);

                // Set gradient size
                TopGradient.Height = gradientSize;
                BottomGradient.Height = gradientSize;
                LeftGradient.Width = gradientSize;
                RightGradient.Width = gradientSize;

                // Set opacity
                GlowBorder.Opacity = opacity;

                // Set gradient brushes
                var gradientBrush = CreateGradientBrush(color, gradientSize);
                TopGradient.Fill = gradientBrush;
                BottomGradient.Fill = CreateGradientBrush(color, gradientSize, true);
                LeftGradient.Fill = CreateGradientBrush(color, gradientSize, false, true);
                RightGradient.Fill = CreateGradientBrush(color, gradientSize, false, true, true);

                // Apply effect animation
                ApplyEffectAnimation(effectType);
            });
        }

        /// <summary>
        /// Định vị overlay over target window.
        /// </summary>
        public void PositionOverTarget(IntPtr targetHwnd)
        {
            if (targetHwnd == IntPtr.Zero) return;

            _isTargetMode = true;
            _targetHwnd = targetHwnd;

            if (!ApplyClientRect(targetHwnd)) return;

            StartRepositionTimer();
        }

        private bool ApplyClientRect(IntPtr hwnd)
        {
            if (!GetClientRect(hwnd, out RECT cr)) return false;
            if (cr.Right <= 0 || cr.Bottom <= 0) return false;

            var ptOrigin = new POINT { X = 0, Y = 0 };
            ClientToScreen(hwnd, ref ptOrigin);

            double scaleX = 1.0, scaleY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            Left = ptOrigin.X / scaleX;
            Top = ptOrigin.Y / scaleY;
            Width = cr.Right / scaleX;
            Height = cr.Bottom / scaleY;
            return true;
        }

        private void StartRepositionTimer()
        {
            _repositionTimer?.Stop();
            _repositionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _repositionTimer.Tick += (_, _) =>
            {
                // Check if window is still valid and window is not closed
                if (_targetHwnd == IntPtr.Zero) return;

                // Check if overlay window is still open
                if (!IsLoaded) return;

                // Check if target window is the foreground window
                IntPtr foregroundHwnd = GetForegroundWindow();
                bool isTargetActive = (foregroundHwnd == _targetHwnd);

                // Debug output
                System.Diagnostics.Debug.WriteLine($"[BorderHighlight] Foreground: {foregroundHwnd}, Target: {_targetHwnd}, IsTargetActive: {isTargetActive}, IsVisible: {IsVisible}, IsLoaded: {IsLoaded}");

                RefreshPosition();

                // Only show overlay when target window is active
                if (isTargetActive)
                {
                    if (!IsVisible)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BorderHighlight] Showing overlay");
                        Show();
                    }
                }
                else
                {
                    if (IsVisible)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BorderHighlight] Hiding overlay");
                        Hide();
                    }
                }
            };
            _repositionTimer.Start();
            Closed += (_, _) =>
            {
                _repositionTimer?.Stop();
                System.Diagnostics.Debug.WriteLine($"[BorderHighlight] Timer stopped on window closed");
            };
        }

        public void StopRepositionTimer()
        {
            _repositionTimer?.Stop();
            System.Diagnostics.Debug.WriteLine($"[BorderHighlight] Timer stopped explicitly");
        }

        private void RefreshPosition()
        {
            if (_targetHwnd == IntPtr.Zero) return;
            ApplyClientRect(_targetHwnd);
        }

        private void MakeClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT);
        }

        private Color ParseColor(string hex)
        {
            try
            {
                if (hex.StartsWith("#"))
                {
                    return (Color)ColorConverter.ConvertFromString(hex);
                }
                return Colors.Cyan;
            }
            catch
            {
                return Colors.Cyan;
            }
        }

        private LinearGradientBrush CreateGradientBrush(Color color, int size, bool reverse = false, 
                                                         bool horizontal = false, bool reverseHorizontal = false)
        {
            var brush = new LinearGradientBrush();
            
            if (horizontal)
            {
                brush.StartPoint = reverseHorizontal ? new System.Windows.Point(1, 0) : new System.Windows.Point(0, 0);
                brush.EndPoint = reverseHorizontal ? new System.Windows.Point(0, 0) : new System.Windows.Point(1, 0);
            }
            else
            {
                brush.StartPoint = reverse ? new System.Windows.Point(0, 1) : new System.Windows.Point(0, 0);
                brush.EndPoint = reverse ? new System.Windows.Point(0, 0) : new System.Windows.Point(0, 1);
            }

            var color1 = Color.FromArgb((byte)(color.A * 0.5), color.R, color.G, color.B);
            var color2 = Color.FromArgb((byte)(color.A * 0.13), color.R, color.G, color.B);
            var color3 = Color.FromArgb(0, color.R, color.G, color.B);

            brush.GradientStops.Add(new GradientStop(color1, 0.0));
            brush.GradientStops.Add(new GradientStop(color2, 0.4));
            brush.GradientStops.Add(new GradientStop(color3, 1.0));

            return brush;
        }

        private void ApplyEffectAnimation(BorderEffectType effectType)
        {
            _animationStoryboard?.Stop();
            _animationStoryboard = null;

            switch (effectType)
            {
                case BorderEffectType.Pulse:
                    _animationStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
                    var pulseAnim = new DoubleAnimation
                    {
                        From = 0.3,
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(1.0),
                        AutoReverse = true
                    };
                    Storyboard.SetTarget(pulseAnim, GlowBorder);
                    Storyboard.SetTargetProperty(pulseAnim, new PropertyPath("Opacity"));
                    _animationStoryboard.Children.Add(pulseAnim);
                    _animationStoryboard.Begin();
                    break;

                case BorderEffectType.Glow:
                    _animationStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
                    var glowAnim = new DoubleAnimation
                    {
                        From = 0.5,
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(0.5),
                        AutoReverse = true
                    };
                    Storyboard.SetTarget(glowAnim, OuterBorder);
                    Storyboard.SetTargetProperty(glowAnim, new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Opacity)"));
                    _animationStoryboard.Children.Add(glowAnim);
                    _animationStoryboard.Begin();
                    break;

                case BorderEffectType.Rainbow:
                    // Rainbow effect - cycle through multiple colors
                    _animationStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
                    
                    // Animate through rainbow colors
                    var colors = new[] { Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Blue, Colors.Indigo, Colors.Violet };
                    var totalDuration = TimeSpan.FromSeconds(7); // 1 second per color
                    
                    for (int i = 0; i < colors.Length; i++)
                    {
                        var colorAnim = new ColorAnimation
                        {
                            From = colors[i],
                            To = colors[(i + 1) % colors.Length],
                            Duration = TimeSpan.FromSeconds(1),
                            BeginTime = TimeSpan.FromSeconds(i)
                        };
                        
                        // Animate OuterBorder
                        Storyboard.SetTarget(colorAnim, OuterBorder);
                        Storyboard.SetTargetProperty(colorAnim, new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Color)"));
                        _animationStoryboard.Children.Add(colorAnim);
                    }
                    
                    _animationStoryboard.Begin();
                    break;

                case BorderEffectType.None:
                default:
                    // No animation
                    break;
            }
        }

        public void StopAnimation()
        {
            _animationStoryboard?.Stop();
            _animationStoryboard = null;
            StopRepositionTimer();
        }
    }
}
