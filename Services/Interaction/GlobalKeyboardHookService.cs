using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace FlowMy.Services.Interaction
{
    /// <summary>
    /// Global low-level keyboard hook (WH_KEYBOARD_LL) để chờ phím ngay cả khi app không focus.
    /// </summary>
    public sealed class GlobalKeyboardHookService : IDisposable
    {
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
        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;
        private const int VK_MENU = 0x12; // Alt
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private readonly object _gate = new();
        private readonly Dictionary<Guid, Waiter> _waiters = new();
        private readonly LowLevelKeyboardProc _proc;

        private IntPtr _hook = IntPtr.Zero;
        private bool _installed;
        private bool _disposed;
        private int _suppressCount;

        /// <summary>
        /// Fired on the UI thread when the user physically presses ESC (VK_ESCAPE = 0x1B).
        /// Only fires when notifications are not suppressed.
        /// </summary>
        public event Action? EscapePressed;

        public GlobalKeyboardHookService()
        {
            _proc = HookCallback;
        }

        public Task<string> WaitForKeyPressAsync(string? expectedKeyText, CancellationToken cancellationToken)
        {
            EnsureInstalled();

            var expected = Normalize(expectedKeyText);
            var id = Guid.NewGuid();
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            CancellationTokenRegistration ctr = default;
            if (cancellationToken.CanBeCanceled)
            {
                ctr = cancellationToken.Register(() =>
                {
                    Waiter? w = null;
                    lock (_gate)
                    {
                        if (_waiters.TryGetValue(id, out w))
                        {
                            _waiters.Remove(id);
                        }
                    }

                    try { w?.Cancellation.Dispose(); } catch { }
                    w?.Tcs.TrySetCanceled(cancellationToken);
                });
            }

            lock (_gate)
            {
                ThrowIfDisposed();
                _waiters[id] = new Waiter(WaitMode.Key, expected, tcs, ctr);
            }

            return tcs.Task;
        }

        public Task<string> WaitForHotkeyAsync(string? expectedHotkeyText, CancellationToken cancellationToken)
        {
            EnsureInstalled();

            var expected = Normalize(expectedHotkeyText);
            var id = Guid.NewGuid();
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            CancellationTokenRegistration ctr = default;
            if (cancellationToken.CanBeCanceled)
            {
                ctr = cancellationToken.Register(() =>
                {
                    Waiter? w = null;
                    lock (_gate)
                    {
                        if (_waiters.TryGetValue(id, out w))
                        {
                            _waiters.Remove(id);
                        }
                    }

                    try { w?.Cancellation.Dispose(); } catch { }
                    w?.Tcs.TrySetCanceled(cancellationToken);
                });
            }

            lock (_gate)
            {
                ThrowIfDisposed();
                _waiters[id] = new Waiter(WaitMode.Hotkey, expected, tcs, ctr);
            }

            return tcs.Task;
        }

        /// <summary>
        /// Tạm thời suppress việc notify waiters (để tránh đang chọn hotkey mà kích hoạt event workflow).
        /// </summary>
        public IDisposable BeginSuppressNotificationsScope()
        {
            ThrowIfDisposed();
            Interlocked.Increment(ref _suppressCount);
            return new SuppressScope(this);
        }

        private void EnsureInstalled()
        {
            ThrowIfDisposed();
            if (_installed) return;

            void InstallInternal()
            {
                if (_installed) return;
                if (_hook != IntPtr.Zero) return;

                using var curProcess = Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule;
                var moduleName = curModule?.ModuleName ?? string.Empty;

                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(moduleName), 0);
                _installed = _hook != IntPtr.Zero;
            }

            var app = Application.Current;
            if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.Invoke(InstallInternal);
            }
            else
            {
                InstallInternal();
            }

            // Best-effort: if hook fails, still allow app to run.
            // WaitForKeyPressAsync will never complete in that case, so we throw early.
            if (!_installed)
            {
                throw new InvalidOperationException("Không thể cài đặt global keyboard hook (WH_KEYBOARD_LL).");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                if (Volatile.Read(ref _suppressCount) > 0)
                {
                    return CallNextHookEx(_hook, nCode, wParam, lParam);
                }

                try
                {
                    // KBDLLHOOKSTRUCT.vkCode is the first 4 bytes
                    var vkCode = Marshal.ReadInt32(lParam);

                    // Fire EscapePressed event when user physically presses ESC
                    if (vkCode == 0x1B) // VK_ESCAPE
                    {
                        var handler = EscapePressed;
                        if (handler != null)
                        {
                            var app = Application.Current;
                            if (app?.Dispatcher != null)
                                app.Dispatcher.BeginInvoke(handler);
                            else
                                handler();
                        }
                    }

                    var key = KeyInterop.KeyFromVirtualKey(vkCode);
                    if (key != Key.None)
                    {
                        var pressedKeyText = key.ToString();
                        var pressedHotkeyText = BuildHotkeyText(key);
                        Notify(pressedKeyText, pressedHotkeyText);
                    }
                }
                catch
                {
                    // ignore hook exceptions
                }
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private void Notify(string pressedKeyText, string pressedHotkeyText)
        {
            List<WaiterToComplete>? hits = null;
            lock (_gate)
            {
                if (_waiters.Count == 0) return;

                foreach (var kv in _waiters)
                {
                    var waiter = kv.Value;
                    var expected = waiter.ExpectedText;
                    var candidate = waiter.Mode == WaitMode.Hotkey ? pressedHotkeyText : pressedKeyText;
                    if (string.IsNullOrWhiteSpace(expected) ||
                        string.Equals(expected, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        hits ??= new List<WaiterToComplete>();
                        hits.Add(new WaiterToComplete(kv.Key, waiter, candidate));
                    }
                }

                if (hits != null)
                {
                    foreach (var h in hits)
                    {
                        _waiters.Remove(h.Id);
                    }
                }
            }

            if (hits == null) return;
            foreach (var h in hits)
            {
                try { h.Waiter.Cancellation.Dispose(); } catch { }
                h.Waiter.Tcs.TrySetResult(h.ResultText);
            }
        }

        private static string Normalize(string? keyText)
        {
            return (keyText ?? string.Empty).Trim();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GlobalKeyboardHookService));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                lock (_gate)
                {
                    foreach (var kv in _waiters.Values)
                    {
                        try { kv.Cancellation.Dispose(); } catch { }
                        kv.Tcs.TrySetCanceled();
                    }
                    _waiters.Clear();
                }
            }
            catch { }

            try
            {
                if (_hook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hook);
                    _hook = IntPtr.Zero;
                }
            }
            catch { }
        }

        private readonly record struct WaiterToComplete(Guid Id, Waiter Waiter, string ResultText);

        private enum WaitMode
        {
            Key,
            Hotkey
        }

        private sealed class Waiter
        {
            public Waiter(WaitMode mode, string expectedText, TaskCompletionSource<string> tcs, CancellationTokenRegistration cancellation)
            {
                Mode = mode;
                ExpectedText = expectedText;
                Tcs = tcs;
                Cancellation = cancellation;
            }

            public WaitMode Mode { get; }
            public string ExpectedText { get; }
            public TaskCompletionSource<string> Tcs { get; }
            public CancellationTokenRegistration Cancellation { get; }
        }

        private sealed class SuppressScope : IDisposable
        {
            private GlobalKeyboardHookService? _svc;
            public SuppressScope(GlobalKeyboardHookService svc) => _svc = svc;
            public void Dispose()
            {
                var s = Interlocked.Exchange(ref _svc, null);
                if (s == null) return;
                Interlocked.Decrement(ref s._suppressCount);
            }
        }

        private static bool IsDown(int vk)
        {
            try { return (GetKeyState(vk) & 0x8000) != 0; } catch { return false; }
        }

        private static bool IsModifierKey(Key key)
        {
            return key is Key.LeftCtrl or Key.RightCtrl
                or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift
                or Key.LWin or Key.RWin;
        }

        private static string BuildHotkeyText(Key mainKey)
        {
            // Only consider combos where a non-modifier key is pressed.
            if (IsModifierKey(mainKey))
                return mainKey.ToString();

            var parts = new List<string>(5);
            if (IsDown(VK_CONTROL)) parts.Add("Ctrl");
            if (IsDown(VK_MENU)) parts.Add("Alt");
            if (IsDown(VK_SHIFT)) parts.Add("Shift");
            if (IsDown(VK_LWIN) || IsDown(VK_RWIN)) parts.Add("Win");
            parts.Add(mainKey.ToString());
            return string.Join("+", parts);
        }
    }
}

