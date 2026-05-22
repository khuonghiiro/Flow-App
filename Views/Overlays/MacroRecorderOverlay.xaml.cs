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
    /// <summary>
    /// Trạng thái của overlay ghi macro.
    /// </summary>
    public enum OverlayState
    {
        Idle,
        Recording,
        Done,
        Cancelled
    }

    /// <summary>
    /// Overlay toàn màn hình trong suốt dùng để ghi lại thao tác chuột và bàn phím.
    /// </summary>
    public partial class MacroRecorderOverlay : Window
    {
        // ─── P/Invoke declarations ───────────────────────────────────────────────

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // ─── Hook constants ──────────────────────────────────────────────────────

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEMOVE = 0x0200;

        private const uint VK_CONTROL = 0x11;
        private const uint VK_MENU = 0x12;   // Alt
        private const uint VK_SHIFT = 0x10;
        private const uint VK_ESCAPE = 0x1B;
        private const uint VK_LCONTROL = 0xA2;
        private const uint VK_RCONTROL = 0xA3;
        private const uint VK_LMENU = 0xA4;
        private const uint VK_RMENU = 0xA5;
        private const uint VK_LSHIFT = 0xA0;
        private const uint VK_RSHIFT = 0xA1;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        // ─── Delegates ───────────────────────────────────────────────────────────

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        // ─── Hook handles ────────────────────────────────────────────────────────

        private readonly LowLevelKeyboardProc _keyboardProc;
        private readonly LowLevelMouseProc _mouseProc;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private IntPtr _mouseHook = IntPtr.Zero;

        // ─── State ───────────────────────────────────────────────────────────────

        private OverlayState _state = OverlayState.Idle;
        private readonly List<MacroAction> _actions = new();
        private int _sequenceCounter = 0;
        private long _recordingStartTime = 0;

        // Mouse move throttle
        private int _lastMoveX = int.MinValue;
        private int _lastMoveY = int.MinValue;
        private const int MoveThresholdPx = 5;

        // Trail polyline
        private Polyline? _trailPolyline;

        // Timer
        private readonly DispatcherTimer _timer = new();
        private DateTime _recordingStartDateTime;

        // ─── Public result properties ─────────────────────────────────────────────

        /// <summary>
        /// JSON serialized list of MacroAction objects. Null nếu chưa có dữ liệu.
        /// </summary>
        public string? RecordedJson { get; private set; }

        /// <summary>
        /// True nếu có dữ liệu đã ghi.
        /// </summary>
        public bool HasData => RecordedJson != null && RecordedJson.Length > 0;

        // ─── Constructor ─────────────────────────────────────────────────────────

        public MacroRecorderOverlay()
        {
            InitializeComponent();

            _keyboardProc = KeyboardHookCallback;
            _mouseProc = MouseHookCallback;

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        // ─── Window events ────────────────────────────────────────────────────────

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Install hooks
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            var hMod = GetModuleHandle(curModule?.ModuleName ?? string.Empty);

            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);

            UpdateUI();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _timer.Stop();

            if (_keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }
            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
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
                var kbStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var vk = kbStruct.vkCode;

                // Detect Ctrl+Alt+Shift (all three held simultaneously)
                bool ctrlDown = (GetAsyncKeyState((int)VK_CONTROL) & 0x8000) != 0;
                bool altDown = (GetAsyncKeyState((int)VK_MENU) & 0x8000) != 0;
                bool shiftDown = (GetAsyncKeyState((int)VK_SHIFT) & 0x8000) != 0;

                bool isModifierKey = vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL
                                  || vk == VK_MENU || vk == VK_LMENU || vk == VK_RMENU
                                  || vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT;

                // When the current key is one of the modifiers, check if all three are now down
                if (isModifierKey)
                {
                    // Re-check including the key being pressed now
                    bool ctrlNow = ctrlDown || vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL;
                    bool altNow = altDown || vk == VK_MENU || vk == VK_LMENU || vk == VK_RMENU;
                    bool shiftNow = shiftDown || vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT;

                    if (ctrlNow && altNow && shiftNow)
                    {
                        Dispatcher.Invoke(ToggleRecording);
                        return (IntPtr)1; // Consume the event
                    }
                }

                // ESC key
                if (vk == VK_ESCAPE)
                {
                    Dispatcher.Invoke(HandleEsc);
                    return (IntPtr)1;
                }

                // Record key press during recording
                if (_state == OverlayState.Recording && !isModifierKey)
                {
                    GetCursorPos(out POINT pt);
                    var keyName = GetKeyName(vk);
                    var action = new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "KeyPress",
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        X = pt.X,
                        Y = pt.Y,
                        Button = null,
                        Key = keyName
                    };
                    _actions.Add(action);
                    Dispatcher.Invoke(() => DrawKeyPress(pt.X, pt.Y, keyName, _sequenceCounter));
                }
            }

            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        // ─── Mouse hook ───────────────────────────────────────────────────────────

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _state == OverlayState.Recording)
            {
                var msStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int x = msStruct.pt.X;
                int y = msStruct.pt.Y;
                long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    var action = new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "MouseClick",
                        Timestamp = ts,
                        X = x,
                        Y = y,
                        Button = "Left",
                        Key = null
                    };
                    _actions.Add(action);
                    Dispatcher.Invoke(() =>
                    {
                        DrawClick(x, y, isLeft: true, _sequenceCounter);
                        UpdateActionCount();
                    });
                }
                else if (wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    var action = new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "MouseClick",
                        Timestamp = ts,
                        X = x,
                        Y = y,
                        Button = "Right",
                        Key = null
                    };
                    _actions.Add(action);
                    Dispatcher.Invoke(() =>
                    {
                        DrawClick(x, y, isLeft: false, _sequenceCounter);
                        UpdateActionCount();
                    });
                }
                else if (wParam == (IntPtr)WM_MOUSEMOVE)
                {
                    // Throttle: only record if moved > 5px from last recorded position
                    int dx = x - _lastMoveX;
                    int dy = y - _lastMoveY;
                    if (_lastMoveX == int.MinValue || Math.Sqrt(dx * dx + dy * dy) > MoveThresholdPx)
                    {
                        _lastMoveX = x;
                        _lastMoveY = y;

                        var action = new MacroAction
                        {
                            SequenceNumber = ++_sequenceCounter,
                            Type = "MouseMove",
                            Timestamp = ts,
                            X = x,
                            Y = y,
                            Button = null,
                            Key = null
                        };
                        _actions.Add(action);
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
            if (_state == OverlayState.Idle)
            {
                StartRecording();
            }
            else if (_state == OverlayState.Recording)
            {
                StopRecording(save: true);
            }
        }

        private void StartRecording()
        {
            _state = OverlayState.Recording;
            _actions.Clear();
            _sequenceCounter = 0;
            _lastMoveX = int.MinValue;
            _lastMoveY = int.MinValue;
            _recordingStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _recordingStartDateTime = DateTime.Now;

            // Create trail polyline
            _trailPolyline = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 200, 0)),
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
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };
                RecordedJson = JsonSerializer.Serialize(_actions, options);
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
            {
                // Save if ≥1 action, cancel otherwise
                StopRecording(save: _actions.Count >= 1);
            }
            else
            {
                // Idle → cancel
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
                    StatusIcon.Text = "⏺";
                    InstructionText.Text = "Nhấn giữ tổ hợp phím Ctrl+Alt+Shift để bắt đầu ghi lại thao tác";
                    TimerPanel.Visibility = Visibility.Collapsed;
                    ActionCountPanel.Visibility = Visibility.Collapsed;
                    EscHintText.Text = "ESC để hủy";
                    break;

                case OverlayState.Recording:
                    StatusIcon.Text = "🔴";
                    InstructionText.Text = "Đang ghi... Nhấn Ctrl+Alt+Shift để dừng";
                    TimerPanel.Visibility = Visibility.Visible;
                    ActionCountPanel.Visibility = Visibility.Visible;
                    TimerText.Text = "00:00";
                    ActionCountText.Text = "0 thao tác";
                    EscHintText.Text = "ESC để lưu và thoát";
                    break;
            }
        }

        private void UpdateActionCount()
        {
            ActionCountText.Text = $"{_actions.Count} thao tác";
        }

        // ─── Visual feedback ──────────────────────────────────────────────────────

        /// <summary>
        /// Vẽ ellipse click tại tọa độ màn hình (screen coords → canvas coords).
        /// </summary>
        private void DrawClick(int screenX, int screenY, bool isLeft, int seq)
        {
            var pt = ScreenToCanvas(screenX, screenY);

            // Ellipse 12×12
            var ellipse = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = isLeft
                    ? new SolidColorBrush(Color.FromRgb(0x22, 0x99, 0xFF))   // blue
                    : new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33)),  // red
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(ellipse, pt.X - 6);
            Canvas.SetTop(ellipse, pt.Y - 6);
            DrawingCanvas.Children.Add(ellipse);

            // Sequence label
            var label = new TextBlock
            {
                Text = seq.ToString(),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, pt.X + 8);
            Canvas.SetTop(label, pt.Y - 8);
            DrawingCanvas.Children.Add(label);

            UpdateActionCount();
        }

        /// <summary>
        /// Vẽ label phím tại vị trí chuột hiện tại.
        /// </summary>
        private void DrawKeyPress(int screenX, int screenY, string keyName, int seq)
        {
            var pt = ScreenToCanvas(screenX, screenY);

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 2, 5, 2),
                IsHitTestVisible = false
            };
            var tb = new TextBlock
            {
                Text = $"[{seq}] {keyName}",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            border.Child = tb;
            Canvas.SetLeft(border, pt.X + 10);
            Canvas.SetTop(border, pt.Y - 10);
            DrawingCanvas.Children.Add(border);

            UpdateActionCount();
        }

        /// <summary>
        /// Thêm điểm vào trail polyline.
        /// </summary>
        private void AddTrailPoint(int screenX, int screenY)
        {
            if (_trailPolyline == null) return;
            var pt = ScreenToCanvas(screenX, screenY);
            _trailPolyline.Points.Add(pt);
        }

        /// <summary>
        /// Chuyển tọa độ màn hình sang tọa độ canvas (tính đến DPI scaling).
        /// </summary>
        private System.Windows.Point ScreenToCanvas(int screenX, int screenY)
        {
            // PresentationSource để lấy DPI transform
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var transform = source.CompositionTarget.TransformFromDevice;
                return transform.Transform(new System.Windows.Point(screenX, screenY));
            }
            return new System.Windows.Point(screenX, screenY);
        }

        // ─── Key name helper ──────────────────────────────────────────────────────

        private static string GetKeyName(uint vkCode)
        {
            // Map common virtual key codes to readable names
            return vkCode switch
            {
                0x08 => "Backspace",
                0x09 => "Tab",
                0x0D => "Enter",
                0x13 => "Pause",
                0x14 => "CapsLock",
                0x1B => "Escape",
                0x20 => "Space",
                0x21 => "PageUp",
                0x22 => "PageDown",
                0x23 => "End",
                0x24 => "Home",
                0x25 => "Left",
                0x26 => "Up",
                0x27 => "Right",
                0x28 => "Down",
                0x2C => "PrintScreen",
                0x2D => "Insert",
                0x2E => "Delete",
                >= 0x30 and <= 0x39 => ((char)vkCode).ToString(),   // 0-9
                >= 0x41 and <= 0x5A => ((char)vkCode).ToString(),   // A-Z
                >= 0x60 and <= 0x69 => $"Num{vkCode - 0x60}",       // Numpad 0-9
                0x6A => "Num*",
                0x6B => "Num+",
                0x6D => "Num-",
                0x6E => "Num.",
                0x6F => "Num/",
                >= 0x70 and <= 0x87 => $"F{vkCode - 0x6F}",         // F1-F24
                0x90 => "NumLock",
                0x91 => "ScrollLock",
                0xBA => ";",
                0xBB => "=",
                0xBC => ",",
                0xBD => "-",
                0xBE => ".",
                0xBF => "/",
                0xC0 => "`",
                0xDB => "[",
                0xDC => "\\",
                0xDD => "]",
                0xDE => "'",
                _ => $"VK_{vkCode:X2}"
            };
        }
    }
}
