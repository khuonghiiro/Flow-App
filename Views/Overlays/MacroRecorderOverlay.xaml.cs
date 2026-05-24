using FlowMy.Models;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

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

        private const int MarkerRadius = 18; // px radius — vừa phải, dễ nhìn

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
        private readonly bool _showMouseTrail;
        // Drag trail (separate polyline while dragging, distinct color)
        private Polyline? _dragTrailPolyline;

        // Timer
        private readonly DispatcherTimer _timer = new();
        private DateTime _recordingStartDateTime;

        // ─── Public result ────────────────────────────────────────────────────────

        public string? RecordedJson { get; private set; }
        public bool HasData => !string.IsNullOrEmpty(RecordedJson);

        // ─── Constructor ─────────────────────────────────────────────────────────

        public MacroRecorderOverlay(bool showMouseTrail = false)
        {
            InitializeComponent();

            _showMouseTrail = showMouseTrail;
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
        /// Bật/tắt chế độ thao tác thực:
        ///   enable=true  → ẩn HitTestRect + thêm WS_EX_TRANSPARENT|WS_EX_NOACTIVATE
        ///                   → click đi thẳng qua overlay xuống app bên dưới
        ///   enable=false → hiện lại HitTestRect + xóa các flag đó
        /// </summary>
        private void ApplyRealActionWindowStyle(bool enable)
        {
            // Toggle the hit-test rectangle — when hidden, WPF has nothing to capture mouse
            HitTestRect.IsHitTestVisible = !enable;
            HitTestRect.Fill = enable
                ? System.Windows.Media.Brushes.Transparent   // fully transparent = no hit
                : new System.Windows.Media.SolidColorBrush(
                      System.Windows.Media.Color.FromArgb(1, 0, 0, 0)); // #01000000

            // Also apply/remove WS_EX_TRANSPARENT + WS_EX_NOACTIVATE at Win32 level
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;

            IntPtr cur = GetWindowLong(hwnd, GWL_EXSTYLE);
            long style = cur.ToInt64();
            if (enable)
                style |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
            else
                style &= ~(long)(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);

            SetWindowLong(hwnd, GWL_EXSTYLE, new IntPtr(style));
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
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
                        // Reset hotkey counters when Ctrl is released
                        _altPressCount = 0;
                        _altFirstPressTs = 0;
                        _capsLockPressCount = 0;
                        _capsLockFirstPressTs = 0;
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
                            Dispatcher.Invoke(ToggleRecording);
                            return (IntPtr)1;
                        }
                        return (IntPtr)1; // block first Alt press too
                    }

                    // ── Hotkey: Ctrl + CapsLock + CapsLock → toggle real-action mode ──
                    if (ctrlDown && vk == VK_CAPITAL)
                    {
                        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        if (_capsLockPressCount > 0 && (now - _capsLockFirstPressTs) > CapsLockWindowMs)
                        {
                            _capsLockPressCount = 0;
                            _capsLockFirstPressTs = 0;
                        }
                        if (_capsLockPressCount == 0) _capsLockFirstPressTs = now;
                        _capsLockPressCount++;

                        System.Diagnostics.Debug.WriteLine(
                            $"[MacroRecorder] Ctrl+CapsLock press #{_capsLockPressCount}");

                        if (_capsLockPressCount >= CapsLockRequiredCount)
                        {
                            _capsLockPressCount = 0;
                            _capsLockFirstPressTs = 0;
                            _realActionMode = !_realActionMode;
                            bool mode = _realActionMode;
                            System.Diagnostics.Debug.WriteLine(
                                $"[MacroRecorder] Real-action mode = {mode}");
                            Dispatcher.Invoke(() =>
                            {
                                // Visual feedback only — no window style change needed
                                // (block+reinject approach handles click routing)
                                InstructionText.Text = mode
                                    ? "⚡ Chế độ thao tác thực — nhấn Ctrl+CapsLock+CapsLock để quay lại ghi"
                                    : "Đang ghi... Nhấn Ctrl+Alt+Alt để dừng";
                                StatusIcon.Text = mode ? "⚡" : "🔴";
                            });
                        }
                        // Always pass through so Windows processes CapsLock normally
                        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                    }

                    if (vk == VK_ESCAPE)
                    {
                        Dispatcher.Invoke(HandleEsc);
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
                        _actions.Add(new MacroAction
                        {
                            SequenceNumber = ++_sequenceCounter,
                            Type = "KeyPress",
                            Timestamp = ts,
                            X = pt.X,
                            Y = pt.Y,
                            Key = keyName
                        });
                        Dispatcher.Invoke(() =>
                        {
                            DrawKeyPress(pt.X, pt.Y, keyName, _sequenceCounter, delta);
                            UpdateActionCount();
                        });
                    }
                }
                else if (isKeyUp)
                {
                    _keysCurrentlyHeld.Remove(vk);
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

                // Real-action mode: record the action AND inject a real click to the app below
                if (_realActionMode)
                {
                    if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN
                        || wParam == (IntPtr)WM_LBUTTONUP)
                    {
                        bool isRight = wParam == (IntPtr)WM_RBUTTONDOWN;
                        bool isDown  = wParam != (IntPtr)WM_LBUTTONUP;
                        bool shiftHeld = (GetKeyState((int)VK_SHIFT)   & 0x8000) != 0;
                        bool ctrlHeld  = _ctrlHeldInHook;
                        bool altHeld   = (GetKeyState((int)VK_MENU)    & 0x8000) != 0;

                        double delta = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                        _lastActionTs = ts;

                        string button = isRight ? "Right" : "Left";
                        string actionType = isRight ? "MouseClick"
                                          : isDown  ? "MouseDown" : "MouseUp";

                        _actions.Add(new MacroAction
                        {
                            SequenceNumber = ++_sequenceCounter,
                            Type = actionType,
                            Timestamp = ts,
                            X = x, Y = y,
                            Button = button,
                            ShiftHeld = shiftHeld,
                            CtrlHeld  = ctrlHeld,
                            AltHeld   = altHeld
                        });
                        int seq = _sequenceCounter;
                        string displayButton = BuildModifierLabel(button, shiftHeld, ctrlHeld, altHeld);

                        // Draw marker on overlay
                        Dispatcher.BeginInvoke(() =>
                        {
                            DrawClick(x, y, displayButton, seq, delta);
                            UpdateActionCount();
                        });

                        // Inject the real click so it reaches the app below
                        // Block the original event (return 1) then re-inject via SendInput
                        // so the app below receives it without the overlay intercepting
                        Task.Run(() => InjectMouseClick(x, y, isRight, isDown));
                        return (IntPtr)1; // block original, our injected copy will go through
                    }
                    // For mouse move and scroll — just pass through normally
                    return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
                }

                if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    bool shiftHeld = (GetAsyncKeyState((int)VK_SHIFT)   & 0x8000) != 0;
                    bool ctrlHeld  = (GetAsyncKeyState((int)VK_CONTROL) & 0x8000) != 0;
                    bool altHeld   = (GetAsyncKeyState((int)VK_MENU)    & 0x8000) != 0;

                    double delta = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                    _lastActionTs = ts;

                    // Build display label for visual feedback
                    string button = "Left";
                    _actions.Add(new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "MouseDown",
                        Timestamp = ts,
                        X = x, Y = y,
                        Button = button,
                        ShiftHeld = shiftHeld,
                        CtrlHeld  = ctrlHeld,
                        AltHeld   = altHeld
                    });
                    int seq = _sequenceCounter;
                    string displayButton = BuildModifierLabel(button, shiftHeld, ctrlHeld, altHeld);
                    _mouseDownTs = ts;
                    Dispatcher.Invoke(() =>
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

                        // Small filled dot at drag start
                        _dragStartDot = new Ellipse
                        {
                            Width = 7,
                            Height = 7,
                            Fill = new SolidColorBrush(Color.FromArgb(220, 0x00, 0xBB, 0xFF)),
                            IsHitTestVisible = false
                        };
                        Canvas.SetLeft(_dragStartDot, _dragStartCanvas.X - 3.5);
                        Canvas.SetTop(_dragStartDot, _dragStartCanvas.Y - 3.5);
                        DrawingCanvas.Children.Add(_dragStartDot);

                        // Keep _dragLine for backward compat (not used for drawing now)
                        _dragLine = null;
                        UpdateActionCount();
                    });
                }
                else if (wParam == (IntPtr)WM_LBUTTONUP && _isDragging)
                {
                    double delta = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                    _lastActionTs = ts;

                    _actions.Add(new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "MouseUp",
                        Timestamp = ts,
                        X = x,
                        Y = y,
                        Button = "Left"
                    });
                    int seq = _sequenceCounter;
                    Dispatcher.Invoke(() =>
                    {
                        _isDragging = false;
                        // Finalize drag trail endpoint
                        if (_dragTrailPolyline != null)
                        {
                            _dragTrailPolyline.Points.Add(ScreenToCanvas(x, y));
                            _dragTrailPolyline = null;
                        }
                        _dragLine = null;
                        // Only show MouseUp marker if held long enough (>= 200ms)
                        bool wasHeld = (ts - _mouseDownTs) >= DragMinHoldMs;
                        if (wasHeld)
                            DrawMouseUp(x, y, seq, delta);
                        UpdateActionCount();
                    });
                }
                else if (wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    bool shiftHeld = (GetAsyncKeyState((int)VK_SHIFT)   & 0x8000) != 0;
                    bool ctrlHeld  = (GetAsyncKeyState((int)VK_CONTROL) & 0x8000) != 0;
                    bool altHeld   = (GetAsyncKeyState((int)VK_MENU)    & 0x8000) != 0;
                    double delta = _lastActionTs > 0 ? (ts - _lastActionTs) / 1000.0 : 0;
                    _lastActionTs = ts;

                    _actions.Add(new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "MouseClick",
                        Timestamp = ts,
                        X = x, Y = y,
                        Button = "Right",
                        ShiftHeld = shiftHeld,
                        CtrlHeld  = ctrlHeld,
                        AltHeld   = altHeld
                    });
                    string displayRight = BuildModifierLabel("Right", shiftHeld, ctrlHeld, altHeld);
                    Dispatcher.Invoke(() =>
                    {
                        DrawClick(x, y, displayRight, _sequenceCounter, delta);
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

                    _actions.Add(new MacroAction
                    {
                        SequenceNumber = ++_sequenceCounter,
                        Type = "MouseScroll",
                        Timestamp = ts,
                        X = x,
                        Y = y,
                        ScrollDelta = notches
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
                            Type = "MouseMove",
                            Timestamp = ts,
                            X = x,
                            Y = y
                        });
                        Dispatcher.Invoke(() =>
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
                        Dispatcher.Invoke(() => UpdateVirtualCursor(x, y));
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
            _recordingStartDateTime = DateTime.Now;

            // Show virtual cursor
            VirtualCursor.Visibility = Visibility.Visible;

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
            }

            _timer.Start();
            UpdateUI();
        }

        private void StopRecording(bool save)
        {
            _timer.Stop();
            _realActionMode = false;

            // Hide virtual cursor
            VirtualCursor.Visibility = Visibility.Collapsed;

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
                // Check if any recorded action is an ESC key press
                bool macroHasEsc = _actions.Exists(a => a.Type == "KeyPress" &&
                    string.Equals(a.Key, "Escape", StringComparison.OrdinalIgnoreCase));

                if (macroHasEsc)
                {
                    // Macro contains ESC — need 3 rapid presses to stop
                    if (_escPressCount >= EscRequiredCount)
                    {
                        _escPressCount = 0;
                        StopRecording(save: _actions.Count >= 1);
                    }
                    else
                    {
                        int remaining = EscRequiredCount - _escPressCount;
                        ShowEscWarning(remaining);
                    }
                }
                else
                {
                    // No ESC in macro — single press saves and exits
                    _escPressCount = 0;
                    StopRecording(save: _actions.Count >= 1);
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

        /// <summary>
        /// Hiển thị cảnh báo tạm thời khi user nhấn ESC nhưng chưa đủ 3 lần.
        /// </summary>
        private void ShowEscWarning(int remaining)
        {
            EscHintText.Text = $"⚠ Macro có phím ESC — nhấn ESC thêm {remaining} lần nữa để dừng";
            EscHintText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xFF, 0xCC, 0x00));

            // Reset hint text after 2 seconds
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (_state == OverlayState.Recording)
                {
                    EscHintText.Text = "ESC để lưu và thoát";
                    EscHintText.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF));
                }
            };
            timer.Start();
        }

        // ─── UI update ────────────────────────────────────────────────────────────

        private void UpdateUI()
        {
            switch (_state)
            {
                case OverlayState.Idle:
                    StatusIcon.Text = "⏺";
                    InstructionText.Text = "Nhấn Ctrl+Alt+Alt để bắt đầu ghi (overlay) | Ctrl+CapsLock+CapsLock để thao tác thực";
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
                    EscHintText.Text = "ESC để lưu và thoát";
                    break;
            }
        }

        private void UpdateActionCount() =>
            ActionCountText.Text = $"{_actions.Count} thao tác";

        // ─── Visual feedback ──────────────────────────────────────────────────────

        /// <summary>
        /// Helper chung: vẽ marker hình tròn — label loại thao tác ở tâm, seq badge trên đỉnh, delta bên dưới.
        /// centerFontSize: font size cho label tâm (default 13, dùng 8 cho text dài như "Chuột trái")
        /// </summary>
        private void DrawMarker(System.Windows.Point pt, Color fillColor, string centerText,
                                int seq, double deltaSeconds, bool hollow = false, double centerFontSize = 13)
        {
            int r = MarkerRadius;

            // Outer glow
            var glow = new Ellipse
            {
                Width = (r + 7) * 2,
                Height = (r + 7) * 2,
                Fill = new SolidColorBrush(Color.FromArgb(45, fillColor.R, fillColor.G, fillColor.B)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(glow, pt.X - (r + 7));
            Canvas.SetTop(glow, pt.Y - (r + 7));
            DrawingCanvas.Children.Add(glow);

            // Main circle
            var circle = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Fill = hollow
                    ? new SolidColorBrush(Color.FromArgb(55, fillColor.R, fillColor.G, fillColor.B))
                    : new SolidColorBrush(Color.FromArgb(225, fillColor.R, fillColor.G, fillColor.B)),
                Stroke = Brushes.White,
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(circle, pt.X - r);
            Canvas.SetTop(circle, pt.Y - r);
            DrawingCanvas.Children.Add(circle);

            // Center label — use a fixed-size container so the text is truly centered
            // Width/Height = diameter of circle, positioned so its center aligns with pt
            var centerContainer = new Grid
            {
                Width = r * 2,
                Height = r * 2,
                IsHitTestVisible = false
            };
            var centerTb = new TextBlock
            {
                Text = centerText,
                FontSize = centerFontSize,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                IsHitTestVisible = false
            };
            centerContainer.Children.Add(centerTb);
            Canvas.SetLeft(centerContainer, pt.X - r);
            Canvas.SetTop(centerContainer, pt.Y - r);
            DrawingCanvas.Children.Add(centerContainer);

            // Seq badge — small circle sitting on top of the main circle
            var seqBg = new Ellipse
            {
                Width = 18,
                Height = 18,
                Fill = new SolidColorBrush(Color.FromArgb(240, 20, 20, 20)),
                Stroke = new SolidColorBrush(Color.FromArgb(200, fillColor.R, fillColor.G, fillColor.B)),
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(seqBg, pt.X - 9);
            Canvas.SetTop(seqBg, pt.Y - r - 9);
            DrawingCanvas.Children.Add(seqBg);

            // Seq number — centered inside the badge using a fixed container
            var seqContainer = new Grid
            {
                Width = 18,
                Height = 18,
                IsHitTestVisible = false
            };
            var seqTb = new TextBlock
            {
                Text = seq.ToString(),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            seqContainer.Children.Add(seqTb);
            Canvas.SetLeft(seqContainer, pt.X - 9);
            Canvas.SetTop(seqContainer, pt.Y - r - 9);
            DrawingCanvas.Children.Add(seqContainer);

            // Delta badge below circle
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
                Canvas.SetTop(deltaBorder, pt.Y + r + 4);
                DrawingCanvas.Children.Add(deltaBorder);
            }
        }

        private void DrawClick(int screenX, int screenY, string button, int seq, double deltaSeconds)
        {
            var pt = ScreenToCanvas(screenX, screenY);

            // Detect right-click: button is "Right", "R", or ends with "+R" (e.g. "Ctrl+R")
            bool isRight = button == "Right" || button == "R" || button.EndsWith("+R");
            Color fillColor = isRight ? ColorRightClick : ColorLeftClick;

            // Center label: "Chuột trái" / "Chuột phải" (smaller font to fit circle)
            string label = isRight ? "Chuột\nphải" : "Chuột\ntrái";

            DrawMarker(pt, fillColor, label, seq, deltaSeconds, centerFontSize: 8);
        }

        private void DrawScroll(int screenX, int screenY, int notches, int seq, double deltaSeconds)
            => DrawMarker(ScreenToCanvas(screenX, screenY), ColorScroll,
                          notches >= 0 ? "↑" : "↓", seq, deltaSeconds);

        private void DrawKeyPress(int screenX, int screenY, string keyName, int seq, double deltaSeconds)
        {
            var pt = ScreenToCanvas(screenX, screenY);
            Color fillColor = Color.FromRgb(0xAA, 0x88, 0xFF);
            int r = MarkerRadius;

            bool isModifierOrCombo = keyName.Contains("+") ||
                                     keyName is "Ctrl" or "Shift" or "Alt" or
                                     "Control" or "LCtrl" or "RCtrl" or
                                     "LShift" or "RShift" or "LAlt" or "RAlt";

            // Outer glow
            var glow = new Ellipse
            {
                Width = (r + 7) * 2,
                Height = (r + 7) * 2,
                Fill = new SolidColorBrush(Color.FromArgb(45, fillColor.R, fillColor.G, fillColor.B)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(glow, pt.X - (r + 7));
            Canvas.SetTop(glow, pt.Y - (r + 7));
            DrawingCanvas.Children.Add(glow);

            // Main circle
            var circle = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Fill = new SolidColorBrush(Color.FromArgb(225, fillColor.R, fillColor.G, fillColor.B)),
                Stroke = Brushes.White,
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(circle, pt.X - r);
            Canvas.SetTop(circle, pt.Y - r);
            DrawingCanvas.Children.Add(circle);

            // Center label — "Nhấn X" in center using Grid container for true centering
            string displayKey = keyName.Length > 5 ? keyName[..5] : keyName;
            string centerText = isModifierOrCombo ? displayKey : $"Nhấn\n{displayKey}";
            double fontSize = isModifierOrCombo ? 9 : 8;

            var centerContainer = new Grid
            {
                Width = r * 2,
                Height = r * 2,
                IsHitTestVisible = false
            };
            var centerTb = new TextBlock
            {
                Text = centerText,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                IsHitTestVisible = false
            };
            centerContainer.Children.Add(centerTb);

            if (isModifierOrCombo)
            {
                // Modifier/combo → label above the seq badge (top of marker)
                Canvas.SetLeft(centerContainer, pt.X - r);
                Canvas.SetTop(centerContainer, pt.Y - r - 28);
            }
            else
            {
                // Normal key → centered inside circle
                Canvas.SetLeft(centerContainer, pt.X - r);
                Canvas.SetTop(centerContainer, pt.Y - r);
            }
            DrawingCanvas.Children.Add(centerContainer);

            // Seq badge
            var seqBg = new Ellipse
            {
                Width = 18, Height = 18,
                Fill = new SolidColorBrush(Color.FromArgb(240, 20, 20, 20)),
                Stroke = new SolidColorBrush(Color.FromArgb(200, fillColor.R, fillColor.G, fillColor.B)),
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(seqBg, pt.X - 9);
            Canvas.SetTop(seqBg, pt.Y - r - 9);
            DrawingCanvas.Children.Add(seqBg);

            var seqContainer = new Grid { Width = 18, Height = 18, IsHitTestVisible = false };
            seqContainer.Children.Add(new TextBlock
            {
                Text = seq.ToString(),
                FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            });
            Canvas.SetLeft(seqContainer, pt.X - 9);
            Canvas.SetTop(seqContainer, pt.Y - r - 9);
            DrawingCanvas.Children.Add(seqContainer);

            // Delta badge
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
                Canvas.SetTop(deltaBorder, pt.Y + r + 4);
                DrawingCanvas.Children.Add(deltaBorder);
            }
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
    }
}
