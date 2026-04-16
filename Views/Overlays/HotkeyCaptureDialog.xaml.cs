using FlowMy.Services.Interaction;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace FlowMy.Views.Overlays
{
    public partial class HotkeyCaptureDialog : Window
    {
        private bool _isClosing;
        private bool _isCapturingWinKey;
        private IDisposable? _suppressScope;
        private readonly System.Collections.Generic.List<string> _capturedKeys = new();
        private ModifierKeys _currentModifiers;
        private HwndSource? _hwndSource;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_KEYMENU = 0xF100;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc? _hookProc;
        private IntPtr _hook = IntPtr.Zero;

        public string? CapturedHotkeyText { get; private set; }
        public string? InitialHotkeyText { get; set; }

        public HotkeyCaptureDialog()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Closed += OnClosed;

            PreviewKeyDown += OnPreviewKeyDown;
            PreviewKeyUp += OnPreviewKeyUp;

            OkButton.Click += (_, __) => AcceptAndClose();
            CancelButton.Click += (_, __) => CancelAndClose();
            ClearButton.Click += (_, __) => ClearHotkey();
            RemoveLastButton.Click += (_, __) => RemoveLastKey();

            // Deactivate handler - but more lenient now with overlay
            Deactivated += (_, __) =>
            {
                if (_isClosing) return;
                if (DialogResult == true) return;
                if (_isCapturingWinKey) return;
            };
        }

        private void OnOverlayClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Cancel when clicking on the overlay backdrop
            if (e.Source == OverlayBackdrop)
            {
                CancelAndClose();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Make window fullscreen for overlay effect
            try
            {
                WindowState = WindowState.Maximized;
                Left = SystemParameters.VirtualScreenLeft;
                Top = SystemParameters.VirtualScreenTop;
                Width = SystemParameters.VirtualScreenWidth;
                Height = SystemParameters.VirtualScreenHeight;
            }
            catch { }

            try
            {
                Activate();
                Focus();
                Keyboard.Focus(this);
            }
            catch { }

            try
            {
                if (!string.IsNullOrWhiteSpace(InitialHotkeyText))
                {
                    UpdateHotkeyText(InitialHotkeyText);
                    // Parse initial hotkey to _capturedKeys
                    if (!string.IsNullOrWhiteSpace(InitialHotkeyText))
                    {
                        var parts = InitialHotkeyText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        _capturedKeys.Clear();
                        _capturedKeys.AddRange(parts);
                    }
                }
            }
            catch { }

            // Suppress global keyboard notifications while configuring hotkey
            try
            {
                var svc = App.Services?.GetService(typeof(GlobalKeyboardHookService)) as GlobalKeyboardHookService;
                _suppressScope = svc?.BeginSuppressNotificationsScope();
            }
            catch { }

            // Install low-level hook to block Win key
            try
            {
                _hookProc = HookCallback;
                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
            }
            catch { }

            // Hook into window messages to block Win key menu
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (_hwndSource != null)
            {
                _hwndSource.AddHook(WndProc);
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            // Remove hook
            if (_hook != IntPtr.Zero)
            {
                try
                {
                    UnhookWindowsHookEx(_hook);
                }
                catch { }
                _hook = IntPtr.Zero;
            }

            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }

            try { _suppressScope?.Dispose(); } catch { }
            _suppressScope = null;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // Don't block Win keys in the hook - let them through to PreviewKeyDown
            // WndProc will handle blocking the Start menu
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Block Win key menu (Start menu)
            if (msg == WM_SYSCOMMAND && (wParam.ToInt32() & 0xFFF0) == SC_KEYMENU)
            {
                handled = true;
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Always handle to avoid bubbling/commands while capturing
            e.Handled = true;

            if (key == Key.None) return;

            // Backspace removes last key
            if (key == Key.Back)
            {
                RemoveLastKey();
                return;
            }

            // Ignore Enter key (used for OK button)
            if (key == Key.Enter || key == Key.Return)
            {
                return;
            }

            // Get key name - normalize modifier keys
            string keyName = NormalizeKeyName(key);

            if (string.IsNullOrWhiteSpace(keyName) || keyName == "None")
                return;

            // Special handling for Win key to prevent Start menu
            if (key == Key.LWin || key == Key.RWin)
            {
                _isCapturingWinKey = true;
                try
                {
                    // Keep dialog active
                    Activate();
                    Focus();

                    if (!_capturedKeys.Contains(keyName))
                    {
                        _capturedKeys.Add(keyName);
                        UpdateHotkeyTextFromCaptured();
                    }
                }
                finally
                {
                    // Reset flag after a short delay
                    System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() => _isCapturingWinKey = false);
                    });
                }
                return;
            }

            // Add ANY key to captured list (including Esc, Ctrl, Alt, Shift, etc.)
            if (!_capturedKeys.Contains(keyName))
            {
                _capturedKeys.Add(keyName);
                UpdateHotkeyTextFromCaptured();
            }
        }

        private string NormalizeKeyName(Key key)
        {
            // Normalize left/right modifier keys to generic names
            return key switch
            {
                Key.LWin or Key.RWin => "Win",
                Key.LeftCtrl or Key.RightCtrl => "Ctrl",
                Key.LeftAlt or Key.RightAlt => "Alt",
                Key.LeftShift or Key.RightShift => "Shift",
                Key.Escape => "Esc",
                _ => key.ToString()
            };
        }

        private void OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            // Update modifiers when keys are released
            _currentModifiers = Keyboard.Modifiers;
        }

        private void UpdateHotkeyTextFromCaptured()
        {
            if (_capturedKeys.Count == 0)
            {
                UpdateHotkeyText("Chưa chọn");
                return;
            }

            var hotkey = string.Join("+", _capturedKeys);
            CapturedHotkeyText = hotkey;
            UpdateHotkeyText(CapturedHotkeyText);
        }

        private void AcceptAndClose()
        {
            if (_isClosing) return;
            _isClosing = true;
            try { DialogResult = true; } catch { }
            try { Close(); } catch { }
        }

        private void CancelAndClose()
        {
            if (_isClosing) return;
            _isClosing = true;
            CapturedHotkeyText = null;
            try { DialogResult = false; } catch { }
            try { Close(); } catch { }
        }

        private void ClearHotkey()
        {
            // Empty string means "explicitly cleared" (so node can clear when OK)
            _capturedKeys.Clear();
            CapturedHotkeyText = string.Empty;
            UpdateHotkeyText("Chưa chọn");
        }

        private void RemoveLastKey()
        {
            if (_capturedKeys.Count > 0)
            {
                _capturedKeys.RemoveAt(_capturedKeys.Count - 1);
                UpdateHotkeyTextFromCaptured();
            }
        }

        private void UpdateHotkeyText(string? text)
        {
            try
            {
                HotkeyText.Text = string.IsNullOrWhiteSpace(text) ? "Chưa chọn" : text;
            }
            catch { }
        }

        private static bool IsModifierKey(Key key)
        {
            return key is Key.LeftCtrl or Key.RightCtrl
                or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift
                or Key.LWin or Key.RWin
                or Key.System;
        }

        private static string PrefixOnly(ModifierKeys mods)
        {
            var p = BuildModifierPrefix(mods);
            return string.IsNullOrWhiteSpace(p) ? "Chưa chọn" : p + "…";
        }

        private static string FormatHotkey(ModifierKeys mods, Key key)
        {
            var main = key.ToString();
            if (string.IsNullOrWhiteSpace(main)) return string.Empty;

            var prefix = BuildModifierPrefix(mods);
            return string.IsNullOrWhiteSpace(prefix) ? main : $"{prefix}+{main}";
        }

        private static string BuildModifierPrefix(ModifierKeys mods)
        {
            // Keep a stable order for comparisons/UX
            var parts = new System.Collections.Generic.List<string>(4);
            if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            return string.Join("+", parts);
        }
    }
}