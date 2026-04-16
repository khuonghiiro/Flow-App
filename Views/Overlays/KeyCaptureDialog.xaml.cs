using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace FlowMy.Views.Overlays
{
    public partial class KeyCaptureDialog : Window
    {
        private bool _isClosing;
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

        public string? CapturedKeyText { get; private set; }

        public KeyCaptureDialog()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Closed += OnClosed;

            PreviewKeyDown += OnPreviewKeyDown;

            // Click outside / lose focus => close without selecting
            Deactivated += (_, __) =>
            {
                if (_isClosing) return;
                if (DialogResult == true) return; // already accepted

                // Mark closing early so we don't re-enter on Deactivated/Activate messages.
                _isClosing = true;
                try { DialogResult = false; } catch { }
                try { Close(); } catch { }
            };

            Closing += (_, __) => _isClosing = true;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Activate();
                Focus();
                Keyboard.Focus(this);
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
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                try
                {
                    var vkCode = Marshal.ReadInt32(lParam);
                    // Don't block Win keys here - let them through to OnPreviewKeyDown
                    // WndProc will block the Start menu
                }
                catch { }
            }

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
            if (key == Key.None)
                return;

            // Always handle to prevent system actions
            e.Handled = true;

            // Esc cancels without selecting.
            if (key == Key.Escape)
            {
                if (_isClosing) return;
                _isClosing = true;
                CapturedKeyText = null;
                try { DialogResult = false; } catch { }
                try { Close(); } catch { }
                return;
            }

            // Allow Win keys to be captured (but still block Start menu via hook)
            // Win key is captured as "LWin" or "RWin"

            if (_isClosing) return;
            CapturedKeyText = key.ToString();
            _isClosing = true;
            try { DialogResult = true; } catch { }
            try { Close(); } catch { }
        }
    }
}

