using FlowMy.Models;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media.Animation;

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

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLong(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLong(IntPtr hwnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_EXSTYLE        = -20;
        private const int WS_EX_TRANSPARENT  = 0x00000020;
        private const int WS_EX_NOACTIVATE   = 0x08000000;
        private const int WS_EX_LAYERED      = 0x00080000;
        private const uint SWP_NOMOVE        = 0x0002;
        private const uint SWP_NOSIZE        = 0x0001;
        private const uint SWP_NOZORDER      = 0x0004;
        private const uint SWP_FRAMECHANGED  = 0x0020;

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
        private const int WH_MOUSE_LL = 14;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_MOUSEWHEEL = 0x020A;

        private const uint VK_CONTROL = 0x11;
        private const uint VK_MENU = 0x12;
        private const uint VK_SHIFT = 0x10;
        private const uint VK_ESCAPE = 0x1B;
        private const uint VK_CAPITAL = 0x14; // CapsLock
        private const uint VK_LCONTROL = 0xA2;
        private const uint VK_RCONTROL = 0xA3;
        private const uint VK_LMENU = 0xA4;
        private const uint VK_RMENU = 0xA5;
        private const uint VK_LSHIFT = 0xA0;
        private const uint VK_RSHIFT = 0xA1;

        // Click marker colors
        private static readonly Color ColorLeftClick = Color.FromRgb(0x22, 0x99, 0xFF); // blue
        private static readonly Color ColorRightClick = Color.FromRgb(0xFF, 0x33, 0x33); // red
        private static readonly Color ColorShiftLeftClick = Color.FromRgb(0xFF, 0xA5, 0x00); // orange
        private static readonly Color ColorScroll = Color.FromRgb(0x44, 0xDD, 0x88); // green

        private const int MarkerRadius = 12; // px radius — nhỏ gọn, không che khuất

        // ─── Screen color sampling ────────────────────────────────────────────────

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        /// <summary>
        /// Sample the screen pixel at (screenX, screenY) and return a contrasting color
        /// for the trail so it's always visible regardless of background.
        /// Light background → dark trail; dark background → light trail.
        /// </summary>
        private static Color GetContrastingTrailColor(int screenX, int screenY)
        {
            try
            {
                IntPtr hdc = GetDC(IntPtr.Zero); // screen DC
                uint pixel = GetPixel(hdc, screenX, screenY);
                ReleaseDC(IntPtr.Zero, hdc);

                byte r = (byte)(pixel & 0xFF);
                byte g = (byte)((pixel >> 8) & 0xFF);
                byte b = (byte)((pixel >> 16) & 0xFF);

                // Perceived luminance (ITU-R BT.709)
                double lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;

                if (lum > 160)
                {
                    // Light background → use dark vivid color (deep blue)
                    return Color.FromArgb(220, 0x00, 0x44, 0xCC);
                }
                else if (lum > 80)
                {
                    // Mid-tone → use bright yellow-green
                    return Color.FromArgb(220, 0xFF, 0xEE, 0x00);
                }
                else
                {
                    // Dark background → use bright white-cyan
                    return Color.FromArgb(220, 0xCC, 0xFF, 0xFF);
                }
            }
            catch
            {
                // Fallback: bright yellow always visible
                return Color.FromArgb(200, 0xFF, 0xEE, 0x00);
            }
        }

        // ─── Delegates ───────────────────────────────────────────────────────────

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private readonly LowLevelKeyboardProc _keyboardProc;
        private readonly LowLevelMouseProc _mouseProc;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private IntPtr _mouseHook = IntPtr.Zero;

        // ─── State ───────────────────────────────────────────────────────────────

        private OverlayState _state = OverlayState.Idle;
        private readonly List<MacroAction> _actions = new();
        private int _sequenceCounter = 0;
        private long _lastActionTs = 0;   // timestamp of previous action (for delta display)

        // Mouse move throttle
        private int _lastMoveX = int.MinValue;
        private int _lastMoveY = int.MinValue;
        private const int MoveThresholdPx = 5;

        // ESC rapid-press detection (3 presses within 1.5s = cancel/stop)
        private int _escPressCount = 0;
        private long _escFirstPressTs = 0;
        private const int EscRequiredCount = 3;
        private const long EscWindowMs = 1500; // 1.5 giây

        // Alt double-press detection (Ctrl + Alt + Alt = toggle recording)
        private int _altPressCount = 0;
        private long _altFirstPressTs = 0;
        private const int AltRequiredCount = 2;
        private const long AltWindowMs = 500; // 0.5 giây

        // Shift double-press detection (Ctrl + CapsLock + CapsLock = thao tác thực)
        private int _capsLockPressCount = 0;
        private long _capsLockFirstPressTs = 0;
        private const int CapsLockRequiredCount = 2;
        private const long CapsLockWindowMs = 500; // 0.5 giây

        // Key hold tracking (prevent multiple markers for same key hold)
        private readonly HashSet<uint> _keysCurrentlyHeld = new();

        // Modifier hold timing for combo label: key → press timestamp
        private readonly Dictionary<uint, long> _modifierHoldStart = new();

        // Marker visual elements tracking to persist on screen while held
        private readonly Dictionary<uint, UIElement[]> _modifierMarkers = new();

        // Real-action mode: when true, mouse hook skips recording so clicks pass through unrecorded
        // Toggled by Ctrl+CapsLock+CapsLock
        private volatile bool _realActionMode = false;

        // Manual Ctrl tracking — GetAsyncKeyState is unreliable inside low-level hook callbacks
        private volatile bool _ctrlHeldInHook = false;

        // Drag-hold tracking (left button held down)
        private bool _isDragging = false;
        private int _dragStartX, _dragStartY;
        private int _dragStartSeq;
        private System.Windows.Point _dragStartCanvas;
        private Line? _dragLine;
        private Ellipse? _dragStartDot;
        private long _mouseDownTs = 0; // timestamp of last LButtonDown (ms)
        private const long DragMinHoldMs = 200; // minimum hold to show MouseUp marker

        // Trail polyline
        private Polyline? _trailPolyline;
        // Track ALL trail polylines so we can fade them all out on stop
        private readonly List<Polyline> _allTrailPolylines = new();
        private readonly bool _showMouseTrail;
        // Drag trail (separate polyline while dragging, distinct color)
        private Polyline? _dragTrailPolyline;

        // Timer
        private readonly DispatcherTimer _timer = new();
        private DateTime _recordingStartDateTime;

        // Target App Mode
        private MacroExecutionMode _executionMode;
        private string _targetProcess;
        private string _targetTitle;
        private IntPtr _targetHwnd = IntPtr.Zero;

        // ─── Public result ────────────────────────────────────────────────────────

        public string? RecordedJson { get; private set; }
        public bool HasData => !string.IsNullOrEmpty(RecordedJson);

        // ─── Constructor ─────────────────────────────────────────────────────────

        public MacroRecorderOverlay(bool showMouseTrail, MacroExecutionMode executionMode = MacroExecutionMode.Free, string targetProcess = "", string targetTitle = "")
        {
            InitializeComponent();

            _showMouseTrail = showMouseTrail;
            _executionMode = executionMode;
            _targetProcess = targetProcess;
            _targetTitle = targetTitle;

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
            if (_executionMode == MacroExecutionMode.TargetApp && !string.IsNullOrEmpty(_targetProcess))
            {
                var windows = FlowMy.Helpers.WindowHelper.GetActiveWindows();
                var match = windows.FirstOrDefault(w => w.Title == _targetTitle && w.ProcessName == _targetProcess);
                if (match != null)
                {
                    _targetHwnd = match.Handle;
                    InstructionText.Text = $"Chờ ghi thao tác... (Chỉ định: {_targetProcess})";
                }
                else
                {
                    MessageBox.Show($"Không tìm thấy ứng dụng đích '{_targetTitle}' ({_targetProcess}). Sẽ chuyển về chế độ Tự do toàn màn hình.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _executionMode = MacroExecutionMode.Free;
                }
            }

            using var proc = System.Diagnostics.Process.GetCurrentProcess();
            using var module = proc.MainModule;
            var hMod = GetModuleHandle(module?.ModuleName ?? string.Empty);

            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);

            UpdateUI();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _timer.Stop();
            if (_keyboardHook != IntPtr.Zero) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
            if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
        }

        // ─── Timer ────────────────────────────────────────────────────────────────

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _recordingStartDateTime;
            TimerText.Text = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
        }

        // ─── Click-through ────────────────────────────────────────────────────────

        /// <summary>
        /// Cập nhật vị trí chuột ảo (VirtualCursor) trên canvas theo tọa độ màn hình.
        /// Chuột ảo chỉ hiển thị khi đang ghi (RECORDING state).
        /// </summary>
        private void UpdateVirtualCursor(int screenX, int screenY)
        {
            if (_state != OverlayState.Recording)
            {
                VirtualCursor.Visibility = Visibility.Collapsed;
                return;
            }
            var pt = ScreenToCanvas(screenX, screenY);
            VirtualCursor.Visibility = Visibility.Visible;
            Canvas.SetLeft(VirtualCursor, pt.X);
            Canvas.SetTop(VirtualCursor, pt.Y);
        }

        /// <summary>
        /// Bật/tắt chế độ click-through:
        ///   enable=true  → WS_EX_TRANSPARENT + ẩn HitTestRect → click xuyên qua overlay
        ///   enable=false → xóa WS_EX_TRANSPARENT + hiện HitTestRect → overlay nhận input bình thường
        /// Phải gọi trên UI thread.
        /// </summary>
        private void SetClickThroughMode(bool enable)
        {
            // 1. Toggle HitTestRect — khi ẩn, WPF không capture mouse nữa
            HitTestRect.IsHitTestVisible = !enable;
            HitTestRect.Fill = enable
                ? System.Windows.Media.Brushes.Transparent
                : new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromArgb(1, 0, 0, 0));

            // 2. Apply/remove WS_EX_TRANSPARENT at Win32 level
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            IntPtr cur = GetWindowLong(hwnd, GWL_EXSTYLE);
            long style = cur.ToInt64();
            if (enable)
                style |= WS_EX_TRANSPARENT;
            else
                style &= ~(long)WS_EX_TRANSPARENT;

            SetWindowLong(hwnd, GWL_EXSTYLE, new IntPtr(style));
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

            // 3. Update UI feedback
            if (enable)
            {
                InstructionText.Text = "⚡ Thao tác thực — click xuyên overlay — Ctrl+CapsLock+CapsLock để lưu & đóng";
                StatusIcon.Text = "⚡";
            }
            else
            {
                if (_state == OverlayState.Recording)
                {
                    InstructionText.Text = "Đang ghi... Nhấn Ctrl+Alt+Alt để dừng";
                    StatusIcon.Text = "🔴";
                }
                else
                {
                    InstructionText.Text = "Nhấn Ctrl+Alt+Alt để bắt đầu ghi | Ctrl+CapsLock+CapsLock để ghi thao tác thực";
                    StatusIcon.Text = "⏺";
                }
            }
        }

        // ─── SendInput P/Invoke (for real-action mode) ───────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private const uint INPUT_MOUSE    = 0;
        private const uint MOUSEEVENTF_LEFTDOWN   = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP     = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN  = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP    = 0x0010;
        private const uint MOUSEEVENTF_ABSOLUTE   = 0x8000;
        private const uint MOUSEEVENTF_MOVE       = 0x0001;
        private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

        // Extra info tag to identify our own injected events — skip them in the hook
        private static readonly IntPtr OurExtraInfo = new IntPtr(0x464C4F57); // "FLOW"

        /// <summary>
        /// Inject a real mouse click at (screenX, screenY) using SendInput.
        /// The injected event is tagged with OurExtraInfo so the hook ignores it.
        /// </summary>
        private static void InjectMouseClick(int screenX, int screenY, bool rightButton, bool down)
        {
            // Convert screen coords to absolute (0–65535 range)
            int screenW = (int)SystemParameters.PrimaryScreenWidth;
            int screenH = (int)SystemParameters.PrimaryScreenHeight;
            int absX = (int)((screenX * 65535.0) / screenW);
            int absY = (int)((screenY * 65535.0) / screenH);

            uint flags = down
                ? (rightButton ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN)
                : (rightButton ? MOUSEEVENTF_RIGHTUP   : MOUSEEVENTF_LEFTUP);

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                u = new INPUTUNION
                {
                    mi = new MOUSEINPUT
                    {
                        dx = absX,
                        dy = absY,
                        mouseData = 0,
                        dwFlags = flags | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE | MOUSEEVENTF_VIRTUALDESK,
                        time = 0,
                        dwExtraInfo = OurExtraInfo
                    }
                }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var vk = kb.vkCode;

                bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                bool isKeyUp   = wParam == (IntPtr)0x0101 || wParam == (IntPtr)0x0105; // WM_KEYUP / WM_SYSKEYUP

                // ── Track Ctrl state manually (GetAsyncKeyState unreliable in hook thread) ──
                if (vk is VK_CONTROL or VK_LCONTROL or VK_RCONTROL)
                {
                    if (isKeyDown)
                    {
                        _ctrlHeldInHook = true;
                    }
                    else if (isKeyUp)
                    {
                        _ctrlHeldInHook = false;
                        // Reset ALT counter on Ctrl release, but NOT CapsLock counter
                        // because user releases Ctrl between the two Ctrl+CapsLock combos
                        _altPressCount = 0;
                        _altFirstPressTs = 0;
                    }
                }

                if (isKeyDown)
                {
                    // Use manually tracked Ctrl state — reliable in hook context
                    bool ctrlDown  = _ctrlHeldInHook;
                    bool altDown   = (GetKeyState((int)VK_MENU)  & 0x8000) != 0;
                    bool shiftDown = (GetKeyState((int)VK_SHIFT) & 0x8000) != 0;

                    bool isModifier = vk is VK_CONTROL or VK_LCONTROL or VK_RCONTROL
                                         or VK_MENU    or VK_LMENU    or VK_RMENU
                                         or VK_SHIFT   or VK_LSHIFT   or VK_RSHIFT
                                         or VK_CAPITAL;

                    // ── Hotkey: Ctrl + Alt + Alt → toggle recording ───────────────
                    if (ctrlDown && (vk is VK_MENU or VK_LMENU or VK_RMENU))
                    {
                        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        if (_altPressCount > 0 && (now - _altFirstPressTs) > AltWindowMs)
                        {
                            _altPressCount = 0;
                            _altFirstPressTs = 0;
                        }
                        if (_altPressCount == 0) _altFirstPressTs = now;
                        _altPressCount++;

                        if (_altPressCount >= AltRequiredCount)
                        {
                            _altPressCount = 0;
                            _altFirstPressTs = 0;
                            Dispatcher.BeginInvoke(ToggleRecording);
                            return (IntPtr)1;
                        }
                        // First Alt press while Ctrl held — still record it as a modifier if recording
                        // (fall through to modifier recording block below)
                    }

                    // ── Standalone Alt (no Ctrl) or first Alt+Ctrl during recording ──
                    // Falls through to the modifier recording block at the bottom

                    // ── Hotkey: Ctrl + CapsLock + CapsLock → toggle real-action recording ──
                    if (ctrlDown && vk == VK_CAPITAL && !_modifierHoldStart.ContainsKey(vk))
                    {
                        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        // Only apply double-tap logic if we are NOT in the regular overlay mode
                        if (!(_state == OverlayState.Recording && !_realActionMode))
                        {
                            if (_capsLockPressCount > 0 && (now - _capsLockFirstPressTs) > CapsLockWindowMs)
                            {
                                _capsLockPressCount = 0;
                            }
                            if (_capsLockPressCount == 0) _capsLockFirstPressTs = now;
                            _capsLockPressCount++;

                            if (_capsLockPressCount >= CapsLockRequiredCount)
                            {
                                _capsLockPressCount = 0;
                                _capsLockFirstPressTs = 0;

                                if (!_realActionMode)
                                {
                                    _realActionMode = true;
                                    _state = OverlayState.Recording;
                                    Dispatcher.BeginInvoke(() =>
                                    {
                                        StartRecording();
                                        SetClickThroughMode(true);
                                    });
                                }
                                else
                                {
                                    _realActionMode = false;
                                    Dispatcher.BeginInvoke(() =>
                                    {
                                        SetClickThroughMode(false);
                                        if (_state == OverlayState.Recording)
                                            StopRecording(save: true);
                                    });
                                }
                                return (IntPtr)1; // Consume the 2nd key completely
                            }
                        }
                    }
                    // We let the first press or single press fall through down to the modifier block so it renders visually!

                    if (vk == VK_ESCAPE)
                    {
                        Dispatcher.BeginInvoke(HandleEsc);
                        return (IntPtr)1;
                    }

                    if (_state == OverlayState.Recording && !isModifier)
                    {
                        // Key hold detection: only record first press, not repeat
                        if (_keysCurrentlyHeld.Contains(vk))
                            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                        _keysCurrentlyHeld.Add(vk);

                        GetCursorPos(out POINT pt);
                        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        double delta = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                        _lastActionTs = ts;

                        var keyName = GetKeyName(vk);

                        // Build full combo key string including held modifiers
                        // e.g. "Ctrl+C", "Shift+Alt+F4", or just "A" if no modifiers
                        string comboKey = BuildComboKeyString(keyName);

                        // Build combo timing label for visual display
                        string? comboLabel = BuildComboLabel(ts, keyName);

                        var coords = GetActionCoords(pt.X, pt.Y);
                        _actions.Add(new MacroAction
                        {
                            SequenceNumber = ++_sequenceCounter,
                            Type = "KeyPress",
                            Timestamp = ts,
                            X = coords.X,
                            Y = coords.Y,
                            Key = comboKey  // store full combo e.g. "Ctrl+C"
                        });
                        int seqKey = _sequenceCounter;
                        Dispatcher.BeginInvoke(() =>
                        {
                            DrawKeyPress(pt.X, pt.Y, comboKey, seqKey, delta, comboLabel);
                            UpdateActionCount();
                        });
                    }
                    else if (_state == OverlayState.Recording && isModifier)
                    {
                        // Track modifier hold start time for combo label timing
                        // Also draw a visual marker so user can see modifier was registered
                        if (!_modifierHoldStart.ContainsKey(vk))
                        {
                            long modTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                            _modifierHoldStart[vk] = modTs;
                            UpdateHeldKeysUI();

                            string modName = vk switch
                            {
                                VK_CONTROL or VK_LCONTROL or VK_RCONTROL => "Ctrl",
                                VK_MENU    or VK_LMENU    or VK_RMENU    => "Alt",
                                VK_SHIFT   or VK_LSHIFT   or VK_RSHIFT   => "Shift",
                                VK_CAPITAL => "Caps",
                                _          => GetKeyName(vk)
                            };
                            GetCursorPos(out POINT modPt);
                            Dispatcher.BeginInvoke(() =>
                            {
                                var pt2 = ScreenToCanvas(modPt.X, modPt.Y);
                                var color = Color.FromArgb(220, 50, 205, 50); // Lime Green (Xanh lá)
                                int r2 = MarkerRadius; // 12, so 24x24

                                var diamond = new Border
                                {
                                    Width = r2 * 2 + 8,
                                    Height = r2 * 2 + 8,
                                    Background = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B)),
                                    BorderBrush = Brushes.White,
                                    BorderThickness = new Thickness(1.5),
                                    RenderTransform = new RotateTransform(45),
                                    RenderTransformOrigin = new Point(0.5, 0.5),
                                    IsHitTestVisible = false
                                };
                                Canvas.SetLeft(diamond, pt2.X - diamond.Width / 2);
                                Canvas.SetTop(diamond, pt2.Y - diamond.Height / 2);
                                DrawingCanvas.Children.Add(diamond);

                                var container2 = new Grid { Width = r2 * 2 + 8, Height = r2 * 2 + 8, IsHitTestVisible = false };
                                container2.Children.Add(new TextBlock
                                {
                                    Text = modName, 
                                    FontSize = 9.5, 
                                    FontWeight = FontWeights.Bold,
                                    Foreground = Brushes.White,
                                    HorizontalAlignment = HorizontalAlignment.Center,
                                    VerticalAlignment = VerticalAlignment.Center,
                                    IsHitTestVisible = false
                                });
                                // Keep text container upright
                                Canvas.SetLeft(container2, pt2.X - container2.Width / 2);
                                Canvas.SetTop(container2, pt2.Y - container2.Height / 2);
                                DrawingCanvas.Children.Add(container2);

                                // Save elements instead of fading to persist while held
                                _modifierMarkers[vk] = new UIElement[] { diamond, container2 };
                            });
                        }
                    }
                }
                else if (isKeyUp)
                {
                    _keysCurrentlyHeld.Remove(vk);
                    // Clear modifier hold timing on release
                    _modifierHoldStart.Remove(vk);
                    
                    if (_modifierMarkers.TryGetValue(vk, out var elems))
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            foreach (var e in elems)
                                DrawingCanvas.Children.Remove(e);
                        });
                        _modifierMarkers.Remove(vk);
                    }

                    UpdateHeldKeysUI();
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
                int x = ms.pt.X;
                int y = ms.pt.Y;
                long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Skip events we injected ourselves (real-action mode re-injection)
                if (ms.dwExtraInfo == OurExtraInfo)
                    return CallNextHookEx(_mouseHook, nCode, wParam, lParam);

                // Real-action mode: overlay is minimized, record normally — no special handling needed

                if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    bool shiftHeld = (GetAsyncKeyState((int)VK_SHIFT)   & 0x8000) != 0;
                    bool ctrlHeld  = (GetAsyncKeyState((int)VK_CONTROL) & 0x8000) != 0;
                    bool altHeld   = (GetAsyncKeyState((int)VK_MENU)    & 0x8000) != 0;

                    double delta = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                    _lastActionTs = ts;

                    // Build display label for visual feedback
                    string button = "Left";
                    var coords = GetActionCoords(x, y);
                    _actions.Add(new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "MouseDown",
                        Timestamp = ts,
                        X = coords.X, Y = coords.Y,
                        Button = button,
                        ShiftHeld = shiftHeld,
                        CtrlHeld  = ctrlHeld,
                        AltHeld   = altHeld
                    });
                    int seq = _sequenceCounter;
                    string displayButton = BuildModifierLabel(button, shiftHeld, ctrlHeld, altHeld);
                    _mouseDownTs = ts;
                    Dispatcher.BeginInvoke(() =>
                    {
                        DrawClick(x, y, displayButton, seq, delta);
                        // Start drag-hold tracking
                        _isDragging = true;
                        _dragStartX = x; _dragStartY = y;
                        _dragStartSeq = seq;
                        _dragStartCanvas = ScreenToCanvas(x, y);

                        // Drag trail: solid thin blue line — distinct from white dashed move trail
                        _dragTrailPolyline = new Polyline
                        {
                            Stroke = new SolidColorBrush(Color.FromArgb(200, 0x00, 0xBB, 0xFF)),
                            StrokeThickness = 1.5,
                            IsHitTestVisible = false
                        };
                        _dragTrailPolyline.Points.Add(_dragStartCanvas);
                        DrawingCanvas.Children.Add(_dragTrailPolyline);

                        // No separate drag start dot — the DrawClick circle already marks the position
                        _dragStartDot = null;
                        _dragLine = null;
                        UpdateActionCount();
                    });
                }
                else if (wParam == (IntPtr)WM_LBUTTONUP && _isDragging)
                {
                    double delta = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                    _lastActionTs = ts;

                    bool wasHeld = (ts - _mouseDownTs) >= DragMinHoldMs;

                    if (!wasHeld)
                    {
                        // Quick click (< 200ms) — retroactively convert MouseDown → MouseClick
                        // and don't add a MouseUp action
                        var lastDown = _actions.FindLast(a => a.Type == "MouseDown" && a.Button == "Left");
                        if (lastDown != null)
                            lastDown.Type = "MouseClick";

                        Dispatcher.BeginInvoke(() =>
                        {
                            _isDragging = false;
                            if (_dragTrailPolyline != null)
                            {
                                // Remove the drag trail — it was just a click, not a drag
                                DrawingCanvas.Children.Remove(_dragTrailPolyline);
                                _dragTrailPolyline = null;
                            }
                            _dragLine = null;
                            UpdateActionCount();
                        });
                    }
                    else
                    {
                        // Real drag — keep MouseDown + add MouseUp
                        var coords = GetActionCoords(x, y);
                        _actions.Add(new MacroAction
                        {
                            SequenceNumber = ++_sequenceCounter,
                            Type = "MouseUp",
                            Timestamp = ts,
                            X = coords.X,
                            Y = coords.Y,
                            Button = "Left"
                        });
                        int seq = _sequenceCounter;
                        Dispatcher.BeginInvoke(() =>
                        {
                            _isDragging = false;
                            if (_dragTrailPolyline != null)
                            {
                                _dragTrailPolyline.Points.Add(ScreenToCanvas(x, y));
                                _dragTrailPolyline = null;
                            }
                            _dragLine = null;
                            DrawMouseUp(x, y, seq, delta);
                            UpdateActionCount();
                        });
                    }
                }
                else if (wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    bool shiftHeld = (GetAsyncKeyState((int)VK_SHIFT)   & 0x8000) != 0;
                    bool ctrlHeld  = (GetAsyncKeyState((int)VK_CONTROL) & 0x8000) != 0;
                    bool altHeld   = (GetAsyncKeyState((int)VK_MENU)    & 0x8000) != 0;
                    double delta = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                    _lastActionTs = ts;

                    var coords = GetActionCoords(x, y);
                    _actions.Add(new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "MouseClick",
                        Timestamp = ts,
                        X = coords.X, Y = coords.Y,
                        Button = "Right",
                        ShiftHeld = shiftHeld,
                        CtrlHeld  = ctrlHeld,
                        AltHeld   = altHeld
                    });
                    int seqRight = _sequenceCounter;
                    string displayRight = BuildModifierLabel("Right", shiftHeld, ctrlHeld, altHeld);
                    Dispatcher.BeginInvoke(() =>
                    {
                        DrawClick(x, y, displayRight, seqRight, delta);
                        UpdateActionCount();
                    });
                }
                else if (wParam == (IntPtr)WM_MOUSEWHEEL)
                {
                    // High word of mouseData = wheel delta (positive = up, negative = down)
                    int wheelDelta = (short)((ms.mouseData >> 16) & 0xFFFF);
                    int notches = wheelDelta / 120; // 120 units per notch

                    double delta = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                    _lastActionTs = ts;

                    var coords = GetActionCoords(x, y);
                    _actions.Add(new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "MouseScroll",
                        Timestamp = ts,
                        X = coords.X,
                        Y = coords.Y,
                        ScrollDelta = notches
                    });
                    int seqScroll = _sequenceCounter;
                    Dispatcher.BeginInvoke(() =>
                    {
                        DrawScroll(x, y, notches, seqScroll, delta);
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

                        var coords = GetActionCoords(x, y);
                        _actions.Add(new MacroAction
                        {
                            SequenceNumber = ++_sequenceCounter,
                            Type = "MouseMove",
                            Timestamp = ts,
                            X = coords.X,
                            Y = coords.Y
                        });
                        Dispatcher.BeginInvoke(() =>
                        {
                            var pt = ScreenToCanvas(x, y);
                            if (_isDragging)
                            {
                                // While dragging: add to drag trail (cyan), not normal trail
                                _dragTrailPolyline?.Points.Add(pt);
                            }
                            else
                            {
                                AddTrailPoint(x, y);
                            }
                            UpdateVirtualCursor(x, y);
                            UpdateActionCount();
                        });
                    }
                    else
                    {
                        // Below threshold — still update virtual cursor smoothly
                        Dispatcher.BeginInvoke(() => UpdateVirtualCursor(x, y));
                    }
                }
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        // ─── State machine ────────────────────────────────────────────────────────

        private void ToggleRecording()
        {
            if (_state == OverlayState.Idle) StartRecording();
            else if (_state == OverlayState.Recording) StopRecording(save: true);
        }

        private void StartRecording()
        {
            _state = OverlayState.Recording;
            _actions.Clear();
            _sequenceCounter = 0;
            _lastActionTs = 0;
            _lastMoveX = int.MinValue;
            _lastMoveY = int.MinValue;
            _escPressCount = 0;
            _escFirstPressTs = 0;
            _altPressCount = 0;
            _altFirstPressTs = 0;
            _capsLockPressCount = 0;
            _capsLockFirstPressTs = 0;
            _keysCurrentlyHeld.Clear();
            _modifierHoldStart.Clear();
            _allTrailPolylines.Clear();
            _recordingStartDateTime = DateTime.Now;

            // Show virtual cursor and recording border
            VirtualCursor.Visibility = Visibility.Visible;
            RecordingBorder.Visibility = Visibility.Visible;

            // Normal mouse-move trail: adaptive color — only shown when user opted in
            // Initial color will be updated per-point via GetContrastingTrailColor
            if (_showMouseTrail)
            {
                _trailPolyline = new Polyline
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(220, 0xFF, 0xEE, 0x00)), // initial yellow
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    IsHitTestVisible = false
                };
                DrawingCanvas.Children.Add(_trailPolyline);
                _allTrailPolylines.Add(_trailPolyline);
            }

            _timer.Start();
            UpdateUI();
        }

        private void StopRecording(bool save)
        {
            _timer.Stop();
            _realActionMode = false;
            // Ensure click-through is off when stopping
            SetClickThroughMode(false);

            // Hide virtual cursor and recording border
            VirtualCursor.Visibility = Visibility.Collapsed;
            RecordingBorder.Visibility = Visibility.Collapsed;

            // Fade out all trail polylines (dashed move trail + drag trails)
            if (_allTrailPolylines.Count > 0)
            {
                var trailsToFade = _allTrailPolylines.ToArray();
                _allTrailPolylines.Clear();
                _trailPolyline = null;
                FadeOutAndRemove(0, 250, trailsToFade);
            }
            if (_dragTrailPolyline != null)
            {
                FadeOutAndRemove(0, 250, _dragTrailPolyline);
                _dragTrailPolyline = null;
            }

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
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Reset counter if outside the time window
            if (_escPressCount > 0 && (now - _escFirstPressTs) > EscWindowMs)
            {
                _escPressCount = 0;
                _escFirstPressTs = 0;
            }

            if (_escPressCount == 0)
                _escFirstPressTs = now;

            _escPressCount++;

            if (_state == OverlayState.Recording)
            {
                if (_escPressCount >= 2)
                {
                    // 2 rapid presses < 1.5s → save & close, remove the last ESC action
                    _escPressCount = 0;
                    var lastEsc = _actions.FindLast(a =>
                        a.Type == "KeyPress" &&
                        string.Equals(a.Key, "Escape", StringComparison.OrdinalIgnoreCase));
                    if (lastEsc != null)
                        _actions.Remove(lastEsc);
                    StopRecording(save: _actions.Count >= 1);
                }
                else
                {
                    // First press → record ESC as a key action, show marker
                    // Don't save yet — wait to see if user presses ESC again within 1.5s
                    GetCursorPos(out POINT pt);
                    long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    double delta = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                    _lastActionTs = ts;

                    var coords = GetActionCoords(pt.X, pt.Y);
                    _actions.Add(new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "KeyPress",
                        Timestamp = ts,
                        X = coords.X, Y = coords.Y,
                        Key = "Escape"
                    });
                    int seqEsc = _sequenceCounter;
                    Dispatcher.BeginInvoke(() =>
                    {
                        DrawKeyPress(pt.X, pt.Y, "Esc", seqEsc, delta);
                        UpdateActionCount();
                        EscHintText.Text = "ESC × 2 nhanh để lưu & thoát";
                        EscHintText.Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0xFF, 0xCC, 0x00));
                    });

                    // After 1.5s with no second press → reset counter, restore hint
                    var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(EscWindowMs) };
                    resetTimer.Tick += (_, _) =>
                    {
                        resetTimer.Stop();
                        _escPressCount = 0;
                        _escFirstPressTs = 0;
                        if (_state == OverlayState.Recording)
                        {
                            EscHintText.Text = "ESC = ghi phím Esc | ESC × 2 nhanh để lưu & thoát";
                            EscHintText.Foreground = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF));
                        }
                    };
                    resetTimer.Start();
                }
            }
            else
            {
                // Idle state — single ESC cancels
                _escPressCount = 0;
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
                    InstructionText.Text = "Nhấn Ctrl+Alt+Alt để bắt đầu ghi (overlay) | Ctrl+CapsLock+CapsLock để ghi thao tác thực";
                    TimerPanel.Visibility = Visibility.Collapsed;
                    ActionCountPanel.Visibility = Visibility.Collapsed;
                    EscHintText.Text = "ESC để hủy";
                    break;

                case OverlayState.Recording:
                    StatusIcon.Text = "🔴";
                    InstructionText.Text = "Đang ghi... Nhấn Ctrl+Alt+Alt để dừng";
                    TimerPanel.Visibility = Visibility.Visible;
                    ActionCountPanel.Visibility = Visibility.Visible;
                    TimerText.Text = "00:00";
                    ActionCountText.Text = "0 thao tác";
                    EscHintText.Text = "ESC = ghi phím Esc | ESC × 2 nhanh để lưu & thoát";
                    break;
            }
        }

        private void UpdateActionCount() =>
            ActionCountText.Text = $"{_actions.Count} thao tác";

        private void UpdateHeldKeysUI()
        {
            Dispatcher.BeginInvoke(() =>
            {
                var heldMods = _keysCurrentlyHeld
                    .Where(vk => vk is VK_CONTROL or VK_LCONTROL or VK_RCONTROL
                                    or VK_MENU or VK_LMENU or VK_RMENU
                                    or VK_SHIFT or VK_LSHIFT or VK_RSHIFT
                                    or VK_CAPITAL)
                    .Select(vk => vk switch
                    {
                        VK_CONTROL or VK_LCONTROL or VK_RCONTROL => "Ctrl",
                        VK_MENU or VK_LMENU or VK_RMENU => "Alt",
                        VK_SHIFT or VK_LSHIFT or VK_RSHIFT => "Shift",
                        VK_CAPITAL => "Caps",
                        _ => GetKeyName(vk)
                    })
                    .Distinct()
                    .ToList();

                var pnl = this.FindName("HeldKeysPanel") as Border;
                var stk = this.FindName("HeldKeysStack") as StackPanel;

                if (heldMods.Count > 0)
                {
                    if (pnl != null) pnl.Visibility = Visibility.Visible;
                    if (stk != null)
                    {
                        stk.Children.Clear();
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
                            stk.Children.Add(b);
                        }
                    }
                }
                else
                {
                    if (pnl != null) pnl.Visibility = Visibility.Collapsed;
                    if (stk != null) stk.Children.Clear();
                }
            });
        }

        // ─── Visual feedback ──────────────────────────────────────────────────────

        /// <summary>
        /// Helper chung: vẽ marker hình tròn — label loại thao tác ở tâm, seq badge trên đỉnh, delta bên dưới.
        /// centerFontSize: font size cho label tâm (default 13, dùng 8 cho text dài như "Chuột trái")
        /// </summary>
        private void DrawMarker(System.Windows.Point pt, Color fillColor, string centerText,
                                int seq, double deltaSeconds, bool hollow = false, double centerFontSize = 13)
        {
            int r = MarkerRadius;

            // The main marker border that adapts its width
            var mainMarker = new Border
            {
                Background = hollow 
                    ? new SolidColorBrush(Color.FromArgb(55, fillColor.R, fillColor.G, fillColor.B))
                    : new SolidColorBrush(Color.FromArgb(225, fillColor.R, fillColor.G, fillColor.B)),
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

            // Outer glow — tight, adapts to width
            var glow = new Border
            {
                Width = cw + 6,
                Height = ch + 6,
                CornerRadius = new CornerRadius(r + 3),
                Background = new SolidColorBrush(Color.FromArgb(50, fillColor.R, fillColor.G, fillColor.B)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(glow, pt.X - cw / 2.0 - 3);
            Canvas.SetTop(glow,  pt.Y - ch / 2.0 - 3);
            DrawingCanvas.Children.Add(glow);

            Canvas.SetLeft(mainMarker, pt.X - cw / 2.0);
            Canvas.SetTop(mainMarker,  pt.Y - ch / 2.0);
            DrawingCanvas.Children.Add(mainMarker);

            // Collect all elements for fade-out
            var allElements = new List<UIElement> { glow, mainMarker };

            // Action type badge adjacent to the circle (sleek pill shape)
            if (!string.IsNullOrEmpty(centerText) && centerText != "+")
            {
                var actionBadge = new Border
                {
                    Background   = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20)),
                    BorderBrush  = new SolidColorBrush(Color.FromArgb(180, fillColor.R, fillColor.G, fillColor.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding      = new Thickness(6, 2, 6, 2),
                    Child        = new TextBlock
                    {
                        Text       = centerText,
                        FontSize   = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White,
                        IsHitTestVisible = false
                    },
                    IsHitTestVisible = false
                };
                actionBadge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(actionBadge, pt.X + cw / 2.0 + 6);
                Canvas.SetTop(actionBadge,  pt.Y - actionBadge.DesiredSize.Height / 2.0);
                DrawingCanvas.Children.Add(actionBadge);
                allElements.Add(actionBadge);
            }

            // Delta badge below circle
            if (deltaSeconds > 0)
            {
                var deltaBorder = new Border
                {
                    Background   = new SolidColorBrush(Color.FromArgb(200, 20, 20, 20)),
                    CornerRadius = new CornerRadius(4),
                    Padding      = new Thickness(4, 1, 4, 1),
                    Child        = new TextBlock
                    {
                        Text       = $"+{deltaSeconds:F2}s",
                        FontSize   = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 225, 80))
                    },
                    IsHitTestVisible = false
                };
                deltaBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(deltaBorder, pt.X - deltaBorder.DesiredSize.Width / 2.0);
                Canvas.SetTop(deltaBorder,  pt.Y + ch / 2.0 + 3);
                DrawingCanvas.Children.Add(deltaBorder);
                allElements.Add(deltaBorder);
            }

            // Auto-fade: stay fully visible for 1s, then fade out over 0.3s and remove
            FadeOutAndRemove(1000, 300, allElements.ToArray());
        }

        /// <summary>
        /// Fade out elements after <paramref name="holdMs"/> ms, animating over <paramref name="fadeMs"/> ms,
        /// then remove them from DrawingCanvas.
        /// </summary>
        private void FadeOutAndRemove(int holdMs, int fadeMs, params UIElement[] elements)
        {
            var holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(holdMs) };
            holdTimer.Tick += (_, _) =>
            {
                holdTimer.Stop();
                var anim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(fadeMs));
                anim.Completed += (_, _) =>
                {
                    foreach (var el in elements)
                    {
                        if (DrawingCanvas.Children.Contains(el))
                            DrawingCanvas.Children.Remove(el);
                    }
                };
                // Animate all elements together
                foreach (var el in elements)
                    el.BeginAnimation(UIElement.OpacityProperty, anim);
            };
            holdTimer.Start();
        }

        private void DrawClick(int screenX, int screenY, string button, int seq, double deltaSeconds)
        {
            var pt = ScreenToCanvas(screenX, screenY);
            bool isRight = button == "Right" || button == "R" || button.EndsWith("+R");
            Color fillColor = isRight ? ColorRightClick : ColorLeftClick;
            // Always show "+" in center — color already distinguishes left/right
            DrawMarker(pt, fillColor, "+", seq, deltaSeconds, centerFontSize: 14);
        }

        private void DrawScroll(int screenX, int screenY, int notches, int seq, double deltaSeconds)
            => DrawMarker(ScreenToCanvas(screenX, screenY), ColorScroll,
                          notches >= 0 ? "↑" : "↓", seq, deltaSeconds);

        private void DrawKeyPress(int screenX, int screenY, string keyName, int seq, double deltaSeconds,
                                   string? comboLabel = null)
        {
            var pt = ScreenToCanvas(screenX, screenY);
            bool isCombo = keyName.Contains('+');

            // Combo keys: orange/yellow — visually distinct from single keys (purple)
            Color fillColor = isCombo
                ? Color.FromRgb(0xFF, 0xAA, 0x00)   // orange for combos
                : Color.FromRgb(0xAA, 0x88, 0xFF);  // purple for single keys

            int r = isCombo ? MarkerRadius + 4 : MarkerRadius; // slightly larger for combos

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

            var allKeyElements = new List<UIElement> { glow, mainMarker };

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
                    Text = isCombo ? keyName.Replace("+", " + ") : keyName,
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
            allKeyElements.Add(actionBadge);

            // Delta badge below circle
            double badgeTop = pt.Y + ch / 2.0 + 3;
            if (deltaSeconds > 0)
            {
                var deltaBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(200, 20, 20, 20)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(4, 1, 4, 1),
                    Child = new TextBlock
                    {
                        Text = $"+{deltaSeconds:F2}s",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 225, 80))
                    },
                    IsHitTestVisible = false
                };
                deltaBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(deltaBorder, pt.X - deltaBorder.DesiredSize.Width / 2);
                Canvas.SetTop(deltaBorder, badgeTop);
                DrawingCanvas.Children.Add(deltaBorder);
                allKeyElements.Add(deltaBorder);
                badgeTop += deltaBorder.DesiredSize.Height + 2;
            }

            // Combo timing label below delta
            if (!string.IsNullOrEmpty(comboLabel))
            {
                var comboBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(210, 30, 20, 0)),
                    CornerRadius = new CornerRadius(4),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(150, fillColor.R, fillColor.G, fillColor.B)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(5, 2, 5, 2),
                    Child = new TextBlock
                    {
                        Text = comboLabel,
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0xDD, 0x88)),
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 220
                    },
                    IsHitTestVisible = false
                };
                comboBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(comboBorder, pt.X - comboBorder.DesiredSize.Width / 2);
                Canvas.SetTop(comboBorder, badgeTop);
                DrawingCanvas.Children.Add(comboBorder);
                allKeyElements.Add(comboBorder);
            }

            // Auto-fade after 1s
            FadeOutAndRemove(1000, 300, allKeyElements.ToArray());
        }

        private void DrawMouseUp(int screenX, int screenY, int seq, double deltaSeconds)
            => DrawMarker(ScreenToCanvas(screenX, screenY),
                          Color.FromRgb(0xFF, 0xA5, 0x00), "↑L", seq, deltaSeconds, hollow: true);

        // ── OLD DrawClick body removed — kept only for compiler: dummy block ──────
        private void _DrawClick_OLD(int screenX, int screenY, string button, int seq, double deltaSeconds)
        {
            var pt = ScreenToCanvas(screenX, screenY);
            Color fillColor = button switch { _ => ColorLeftClick };
            string label = "?";
            int r = MarkerRadius;

            // Outer glow ring
            var glow = new Ellipse
            {
                Width = (r + 6) * 2,
                Height = (r + 6) * 2,
                Fill = new SolidColorBrush(Color.FromArgb(40, fillColor.R, fillColor.G, fillColor.B)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(glow, pt.X - (r + 6));
            Canvas.SetTop(glow, pt.Y - (r + 6));
            DrawingCanvas.Children.Add(glow);

            // Main filled circle
            var circle = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Fill = new SolidColorBrush(Color.FromArgb(230, fillColor.R, fillColor.G, fillColor.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(circle, pt.X - r);
            Canvas.SetTop(circle, pt.Y - r);
            DrawingCanvas.Children.Add(circle);

            // Label at center — seq number + button type
            var centerLabel = new TextBlock
            {
                Text = $"{seq}\n{label}",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                LineHeight = 13,
                IsHitTestVisible = false
            };
            centerLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(centerLabel, pt.X - centerLabel.DesiredSize.Width / 2);
            Canvas.SetTop(centerLabel, pt.Y - centerLabel.DesiredSize.Height / 2);
            DrawingCanvas.Children.Add(centerLabel);

            // Delta time badge below circle
            if (deltaSeconds > 0)
            {
                var delta = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(200, 20, 20, 20)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(4, 1, 4, 1),
                    Child = new TextBlock
                    {
                        Text = $"+{deltaSeconds:F2}s",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 230, 100))
                    },
                    IsHitTestVisible = false
                };
                delta.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(delta, pt.X - delta.DesiredSize.Width / 2);
                Canvas.SetTop(delta, pt.Y + r + 3);
                DrawingCanvas.Children.Add(delta);
            }
        }

        private void AddTrailPoint(int screenX, int screenY)
        {
            if (!_showMouseTrail || _trailPolyline == null) return;
            var pt = ScreenToCanvas(screenX, screenY);

            // Sample pixel color at current position and update trail stroke if contrast changed
            var contrastColor = GetContrastingTrailColor(screenX, screenY);
            var currentBrush = _trailPolyline.Stroke as SolidColorBrush;
            if (currentBrush == null || currentBrush.Color != contrastColor)
            {
                // Start a new polyline segment with the new color
                _trailPolyline = new Polyline
                {
                    Stroke = new SolidColorBrush(contrastColor),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    IsHitTestVisible = false
                };
                DrawingCanvas.Children.Add(_trailPolyline);
                _allTrailPolylines.Add(_trailPolyline);
            }

            _trailPolyline.Points.Add(pt);
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
            0x25 => "←",
            0x26 => "↑",
            0x27 => "→",
            0x28 => "↓",
            0x2C => "PrtSc",
            0x2D => "Insert",
            0x2E => "Delete",
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),
            >= 0x60 and <= 0x69 => $"Num{vk - 0x60}",
            0x6A => "Num*",
            0x6B => "Num+",
            0x6D => "Num-",
            0x6E => "Num.",
            0x6F => "Num/",
            >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",
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
            _ => $"VK_{vk:X2}"
        };

        /// <summary>
        /// Tạo label hiển thị cho click kèm modifier: "Ctrl+L", "Shift+Alt+R", v.v.
        /// Dùng "+" làm separator, "L"/"R" cho Left/Right click.
        /// </summary>
        private static string BuildModifierLabel(string button, bool shift, bool ctrl, bool alt)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (ctrl)  parts.Add("Ctrl");
            if (alt)   parts.Add("Alt");
            if (shift) parts.Add("Shift");
            parts.Add(button == "Right" ? "R" : "L");
            return string.Join("+", parts);
        }

        /// <summary>
        /// Tạo combo key string để lưu vào MacroAction.Key: "Ctrl+C", "Shift+Alt+F4", "A", v.v.
        /// Dùng trạng thái modifier hiện tại (reliable hơn _modifierHoldStart).
        /// </summary>
        private string BuildComboKeyString(string mainKey)
        {
            var parts = new List<string>();

            // Use live key state — more reliable than _modifierHoldStart
            // _ctrlHeldInHook is manually tracked; GetKeyState for Shift/Alt
            if (_ctrlHeldInHook)
                parts.Add("Ctrl");
            if ((GetKeyState((int)VK_MENU) & 0x8000) != 0)
                parts.Add("Alt");
            if ((GetKeyState((int)VK_SHIFT) & 0x8000) != 0)
                parts.Add("Shift");

            parts.Add(mainKey);
            return string.Join("+", parts);
        }

        /// <summary>
        /// Tạo combo label với thời gian giữ từng modifier: "Ctrl (3.12s) + Shift (1.2s) + B (0.1s)"
        /// Trả về null nếu không có modifier nào đang được giữ.
        /// </summary>
        private string? BuildComboLabel(long nowMs, string mainKey)
        {
            var modMap = new (uint vk, string name)[]
            {
                (VK_CONTROL,  "Ctrl"),
                (VK_LCONTROL, "Ctrl"),
                (VK_RCONTROL, "Ctrl"),
                (VK_MENU,     "Alt"),
                (VK_LMENU,    "Alt"),
                (VK_RMENU,    "Alt"),
                (VK_SHIFT,    "Shift"),
                (VK_LSHIFT,   "Shift"),
                (VK_RSHIFT,   "Shift"),
            };

            var parts = new List<string>();
            var seen  = new HashSet<string>();

            foreach (var (vk, name) in modMap)
            {
                if (_modifierHoldStart.TryGetValue(vk, out long startMs) && seen.Add(name))
                {
                    double held = (nowMs - startMs) / 1000.0;
                    parts.Add($"{name} ({held:F2}s)");
                }
            }

            if (parts.Count == 0) return null;

            parts.Add(mainKey);
            return string.Join(" + ", parts);
        }

        private (int X, int Y) GetActionCoords(int screenX, int screenY)
        {
            // Always store screen coordinates in MacroAction.
            // The executor is responsible for converting to client coords at playback time.
            return (screenX, screenY);
        }
    }
}
