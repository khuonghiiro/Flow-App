using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using FlowMy.Models;

namespace FlowMy.Views.Overlays
{
    public enum OverlayState { Idle, Recording, Done, Cancelled }

    public partial class MacroRecorderOverlay : Window
    {
        // ─── P/Invoke ────────────────────────────────────────────────────────────

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode, scanCode, flags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData, flags, time;
            public IntPtr dwExtraInfo;
        }

        // ─── Constants ───────────────────────────────────────────────────────────

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL    = 14;

        private const int WM_KEYDOWN    = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEMOVE   = 0x0200;
        private const int WM_MOUSEWHEEL  = 0x020A;

        private const uint VK_CONTROL  = 0x11;
        private const uint VK_MENU     = 0x12;
        private const uint VK_SHIFT    = 0x10;
        private const uint VK_ESCAPE   = 0x1B;
        private const uint VK_LCONTROL = 0xA2;
        private const uint VK_RCONTROL = 0xA3;
        private const uint VK_LMENU    = 0xA4;
        private const uint VK_RMENU    = 0xA5;
        private const uint VK_LSHIFT   = 0xA0;
        private const uint VK_RSHIFT   = 0xA1;

        // Click marker colors
        private static readonly Color ColorLeftClick      = Color.FromRgb(0x22, 0x99, 0xFF); // blue
        private static readonly Color ColorRightClick     = Color.FromRgb(0xFF, 0x33, 0x33); // red
        private static readonly Color ColorShiftLeftClick = Color.FromRgb(0xFF, 0xA5, 0x00); // orange
        private static readonly Color ColorScroll         = Color.FromRgb(0x44, 0xDD, 0x88); // green

        private const int MarkerRadius = 14; // px radius of click circle

        // ─── Delegates ───────────────────────────────────────────────────────────

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private readonly LowLevelKeyboardProc _keyboardProc;
        private readonly LowLevelMouseProc    _mouseProc;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private IntPtr _mouseHook    = IntPtr.Zero;

        // ─── State ───────────────────────────────────────────────────────────────

        private OverlayState _state = OverlayState.Idle;
        private readonly List<MacroAction> _actions = new();
        private int  _sequenceCounter  = 0;
        private long _lastActionTs     = 0;   // timestamp of previous action (for delta display)

        // Mouse move throttle
        private int _lastMoveX = int.MinValue;
        private int _lastMoveY = int.MinValue;
        private const int MoveThresholdPx = 5;

        // Trail polyline
        private Polyline? _trailPolyline;

        // Timer
        private readonly DispatcherTimer _timer = new();
        private DateTime _recordingStartDateTime;

        // ─── Public result ────────────────────────────────────────────────────────

        public string? RecordedJson { get; private set; }
        public bool HasData => !string.IsNullOrEmpty(RecordedJson);

        // ─── Constructor ─────────────────────────────────────────────────────────

        public MacroRecorderOverlay()
        {
            InitializeComponent();

            _keyboardProc = KeyboardHookCallback;
            _mouseProc    = MouseHookCallback;

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        // ─── Window events ────────────────────────────────────────────────────────

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            using var proc   = System.Diagnostics.Process.GetCurrentProcess();
            using var module = proc.MainModule;
            var hMod = GetModuleHandle(module?.ModuleName ?? string.Empty);

            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            _mouseHook    = SetWindowsHookEx(WH_MOUSE_LL,    _mouseProc,    hMod, 0);

            UpdateUI();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _timer.Stop();
            if (_keyboardHook != IntPtr.Zero) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
            if (_mouseHook    != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook);    _mouseHook    = IntPtr.Zero; }
        }

        // ─── Timer ────────────────────────────────────────────────────────────────

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _recordingStartDateTime;
            TimerText.Text = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
        }

        // ─── Keyboard hook ────────────────────────────────────────────────────────

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var vk = kb.vkCode;

                bool ctrlDown  = (GetAsyncKeyState((int)VK_CONTROL) & 0x8000) != 0;
                bool altDown   = (GetAsyncKeyState((int)VK_MENU)    & 0x8000) != 0;
                bool shiftDown = (GetAsyncKeyState((int)VK_SHIFT)   & 0x8000) != 0;

                bool isModifier = vk is VK_CONTROL or VK_LCONTROL or VK_RCONTROL
                                     or VK_MENU    or VK_LMENU    or VK_RMENU
                                     or VK_SHIFT   or VK_LSHIFT   or VK_RSHIFT;

                if (isModifier)
                {
                    bool ctrlNow  = ctrlDown  || vk is VK_CONTROL or VK_LCONTROL or VK_RCONTROL;
                    bool altNow   = altDown   || vk is VK_MENU    or VK_LMENU    or VK_RMENU;
                    bool shiftNow = shiftDown || vk is VK_SHIFT   or VK_LSHIFT   or VK_RSHIFT;

                    if (ctrlNow && altNow && shiftNow)
                    {
                        Dispatcher.Invoke(ToggleRecording);
                        return (IntPtr)1;
                    }
                }

                if (vk == VK_ESCAPE)
                {
                    Dispatcher.Invoke(HandleEsc);
                    return (IntPtr)1;
                }

                if (_state == OverlayState.Recording && !isModifier)
                {
                    GetCursorPos(out POINT pt);
                    long ts      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    double delta = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                    _lastActionTs = ts;

                    var keyName = GetKeyName(vk);
                    var action  = new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type      = "KeyPress",
                        Timestamp = ts,
                        X = pt.X, Y = pt.Y,
                        Key = keyName
                    };
                    _actions.Add(action);
                    Dispatcher.Invoke(() =>
                    {
                        DrawKeyPress(pt.X, pt.Y, keyName, _sequenceCounter, delta);
                        UpdateActionCount();
                    });
                }
            }

            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        // ─── Mouse hook ───────────────────────────────────────────────────────────

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _state == OverlayState.Recording)
            {
                var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int x  = ms.pt.X;
                int y  = ms.pt.Y;
                long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    bool shiftHeld = (GetAsyncKeyState((int)VK_SHIFT) & 0x8000) != 0;
                    string button  = shiftHeld ? "ShiftLeft" : "Left";

                    double delta  = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                    _lastActionTs = ts;

                    _actions.Add(new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "MouseClick", Timestamp = ts,
                        X = x, Y = y, Button = button
                    });
                    Dispatcher.Invoke(() =>
                    {
                        DrawClick(x, y, button, _sequenceCounter, delta);
                        UpdateActionCount();
                    });
                }
                else if (wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    double delta  = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                    _lastActionTs = ts;

                    _actions.Add(new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "MouseClick", Timestamp = ts,
                        X = x, Y = y, Button = "Right"
                    });
                    Dispatcher.Invoke(() =>
                    {
                        DrawClick(x, y, "Right", _sequenceCounter, delta);
                        UpdateActionCount();
                    });
                }
                else if (wParam == (IntPtr)WM_MOUSEWHEEL)
                {
                    // High word of mouseData = wheel delta (positive = up, negative = down)
                    int wheelDelta = (short)((ms.mouseData >> 16) & 0xFFFF);
                    int notches    = wheelDelta / 120; // 120 units per notch

                    double delta  = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                    _lastActionTs = ts;

                    _actions.Add(new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "MouseScroll", Timestamp = ts,
                        X = x, Y = y, ScrollDelta = notches
                    });
                    Dispatcher.Invoke(() =>
                    {
                        DrawScroll(x, y, notches, _sequenceCounter, delta);
                        UpdateActionCount();
                    });
                }
                else if (wParam == (IntPtr)WM_MOUSEMOVE)
                {
                    int dx = x - _lastMoveX;
                    int dy = y - _lastMoveY;
                    if (_lastMoveX == int.MinValue || Math.Sqrt(dx * dx + dy * dy) > MoveThresholdPx)
                    {
                        _lastMoveX = x;
                        _lastMoveY = y;

                        _actions.Add(new MacroAction
                        {
                            SequenceNumber = ++_sequenceCounter,
                            Type = "MouseMove", Timestamp = ts,
                            X = x, Y = y
                        });
                        Dispatcher.Invoke(() =>
                        {
                            AddTrailPoint(x, y);
                            UpdateActionCount();
                        });
                    }
                }
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        // ─── State machine ────────────────────────────────────────────────────────

        private void ToggleRecording()
        {
            if (_state == OverlayState.Idle)         StartRecording();
            else if (_state == OverlayState.Recording) StopRecording(save: true);
        }

        private void StartRecording()
        {
            _state = OverlayState.Recording;
            _actions.Clear();
            _sequenceCounter = 0;
            _lastActionTs    = 0;
            _lastMoveX = int.MinValue;
            _lastMoveY = int.MinValue;
            _recordingStartDateTime = DateTime.Now;

            _trailPolyline = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromArgb(160, 255, 200, 0)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                IsHitTestVisible = false
            };
            DrawingCanvas.Children.Add(_trailPolyline);

            _timer.Start();
            UpdateUI();
        }

        private void StopRecording(bool save)
        {
            _timer.Stop();

            if (save && _actions.Count > 0)
            {
                _state = OverlayState.Done;
                RecordedJson = JsonSerializer.Serialize(_actions, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });
            }
            else
            {
                _state = OverlayState.Cancelled;
                RecordedJson = null;
            }

            Close();
        }

        private void HandleEsc()
        {
            if (_state == OverlayState.Recording)
                StopRecording(save: _actions.Count >= 1);
            else
            {
                _state = OverlayState.Cancelled;
                RecordedJson = null;
                Close();
            }
        }

        // ─── UI update ────────────────────────────────────────────────────────────

        private void UpdateUI()
        {
            switch (_state)
            {
                case OverlayState.Idle:
                    StatusIcon.Text      = "⏺";
                    InstructionText.Text = "Nhấn giữ tổ hợp phím Ctrl+Alt+Shift để bắt đầu ghi lại thao tác";
                    TimerPanel.Visibility       = Visibility.Collapsed;
                    ActionCountPanel.Visibility = Visibility.Collapsed;
                    EscHintText.Text = "ESC để hủy";
                    break;

                case OverlayState.Recording:
                    StatusIcon.Text      = "🔴";
                    InstructionText.Text = "Đang ghi... Nhấn Ctrl+Alt+Shift để dừng";
                    TimerPanel.Visibility       = Visibility.Visible;
                    ActionCountPanel.Visibility = Visibility.Visible;
                    TimerText.Text       = "00:00";
                    ActionCountText.Text = "0 thao tác";
                    EscHintText.Text     = "ESC để lưu và thoát";
                    break;
            }
        }

        private void UpdateActionCount() =>
            ActionCountText.Text = $"{_actions.Count} thao tác";

        // ─── Visual feedback ──────────────────────────────────────────────────────

        /// <summary>
        /// Vẽ marker click: hình tròn lớn với dấu + bên trong, nhãn số thứ tự và delta giây.
        /// </summary>
        private void DrawClick(int screenX, int screenY, string button, int seq, double deltaSeconds)
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

            // Outer circle
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

            // "+" cross — horizontal bar
            var hBar = new Rectangle
            {
                Width  = MarkerRadius,
                Height = 2.5,
                Fill   = Brushes.White,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(hBar, pt.X - MarkerRadius / 2.0);
            Canvas.SetTop(hBar,  pt.Y - 1.25);
            DrawingCanvas.Children.Add(hBar);

            // "+" cross — vertical bar
            var vBar = new Rectangle
            {
                Width  = 2.5,
                Height = MarkerRadius,
                Fill   = Brushes.White,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(vBar, pt.X - 1.25);
            Canvas.SetTop(vBar,  pt.Y - MarkerRadius / 2.0);
            DrawingCanvas.Children.Add(vBar);

            // Sequence + button label (top-right of circle)
            var seqLabel = new TextBlock
            {
                Text       = $"[{seq}] {label}",
                FontSize   = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(seqLabel, pt.X + MarkerRadius + 3);
            Canvas.SetTop(seqLabel,  pt.Y - MarkerRadius);
            DrawingCanvas.Children.Add(seqLabel);

            // Delta time label (below sequence label, only if > 0)
            if (deltaSeconds > 0)
            {
                var deltaLabel = new TextBlock
                {
                    Text       = $"+{deltaSeconds:F2}s",
                    FontSize   = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 180)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(deltaLabel, pt.X + MarkerRadius + 3);
                Canvas.SetTop(deltaLabel,  pt.Y - MarkerRadius + 14);
                DrawingCanvas.Children.Add(deltaLabel);
            }
        }

        /// <summary>
        /// Vẽ marker scroll: mũi tên lên/xuống với delta giây.
        /// </summary>
        private void DrawScroll(int screenX, int screenY, int notches, int seq, double deltaSeconds)
        {
            var pt = ScreenToCanvas(screenX, screenY);

            bool scrollUp = notches >= 0;
            string arrow  = scrollUp ? "▲" : "▼";

            var border = new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(200,
                    ColorScroll.R, ColorScroll.G, ColorScroll.B)),
                CornerRadius = new CornerRadius(6),
                Padding      = new Thickness(6, 3, 6, 3),
                IsHitTestVisible = false
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text       = $"[{seq}] {arrow} {Math.Abs(notches)}",
                FontSize   = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            if (deltaSeconds > 0)
            {
                sp.Children.Add(new TextBlock
                {
                    Text       = $"  +{deltaSeconds:F2}s",
                    FontSize   = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 180)),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            border.Child = sp;

            Canvas.SetLeft(border, pt.X + 10);
            Canvas.SetTop(border,  pt.Y - 12);
            DrawingCanvas.Children.Add(border);
        }

        /// <summary>
        /// Vẽ label phím tại vị trí chuột.
        /// </summary>
        private void DrawKeyPress(int screenX, int screenY, string keyName, int seq, double deltaSeconds)
        {
            var pt = ScreenToCanvas(screenX, screenY);

            var sp = new StackPanel { Orientation = Orientation.Vertical };
            sp.Children.Add(new TextBlock
            {
                Text       = $"[{seq}] {keyName}",
                FontSize   = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            if (deltaSeconds > 0)
            {
                sp.Children.Add(new TextBlock
                {
                    Text       = $"+{deltaSeconds:F2}s",
                    FontSize   = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 180))
                });
            }

            var border = new Border
            {
                Background   = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)),
                CornerRadius = new CornerRadius(4),
                Padding      = new Thickness(5, 2, 5, 2),
                Child        = sp,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(border, pt.X + 10);
            Canvas.SetTop(border,  pt.Y - 10);
            DrawingCanvas.Children.Add(border);
        }

        private void AddTrailPoint(int screenX, int screenY)
        {
            if (_trailPolyline == null) return;
            _trailPolyline.Points.Add(ScreenToCanvas(screenX, screenY));
        }

        private System.Windows.Point ScreenToCanvas(int screenX, int screenY)
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
                return source.CompositionTarget.TransformFromDevice
                             .Transform(new System.Windows.Point(screenX, screenY));
            return new System.Windows.Point(screenX, screenY);
        }

        // ─── Key name helper ──────────────────────────────────────────────────────

        private static string GetKeyName(uint vk) => vk switch
        {
            0x08 => "Backspace", 0x09 => "Tab",    0x0D => "Enter",
            0x13 => "Pause",     0x14 => "CapsLock",0x1B => "Escape",
            0x20 => "Space",     0x21 => "PageUp",  0x22 => "PageDown",
            0x23 => "End",       0x24 => "Home",
            0x25 => "←",         0x26 => "↑",       0x27 => "→",  0x28 => "↓",
            0x2C => "PrtSc",     0x2D => "Insert",  0x2E => "Delete",
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),
            >= 0x60 and <= 0x69 => $"Num{vk - 0x60}",
            0x6A => "Num*", 0x6B => "Num+", 0x6D => "Num-", 0x6E => "Num.", 0x6F => "Num/",
            >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",
            0x90 => "NumLock", 0x91 => "ScrollLock",
            0xBA => ";",  0xBB => "=",  0xBC => ",",  0xBD => "-",
            0xBE => ".",  0xBF => "/",  0xC0 => "`",
            0xDB => "[",  0xDC => "\\", 0xDD => "]",  0xDE => "'",
            _ => $"VK_{vk:X2}"
        };
    }
}
