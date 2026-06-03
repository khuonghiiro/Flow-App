using System;
using System.Runtime.InteropServices;
using System.Threading;
using InputInterceptorNS;

namespace FlowMy.Helpers
{
    /// <summary>
    /// Kernel-level input injection sử dụng Interception driver (InputInterceptorNS).
    /// Hoạt động với MỌI app (game, browser, UWP...) mà không cần app active.
    ///
    /// YÊU CẦU: Driver phải được cài — gọi EnsureDriverInstalled() khi app khởi động.
    /// Cần quyền admin để cài driver lần đầu, sau đó user thường có thể dùng bình thường.
    /// </summary>
    public static class InterceptionInputHelper
    {
        private static bool _initialized = false;
        private static bool _available = false;
        private static readonly object _lock = new();
        private static bool _promptInProgress = false; // tránh hiện dialog nhiều lần cùng lúc

        // Shared hooks — khởi tạo một lần, tái sử dụng
        private static KeyboardHook? _keyboardHook;
        private static MouseHook? _mouseHook;

        // ── Driver install ────────────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra + khởi tạo driver và hooks. Gọi một lần khi app start.
        /// Trả về true nếu sẵn sàng.
        /// </summary>
        public static bool EnsureDriverInstalled()
        {
            lock (_lock)
            {
                if (_initialized) return _available;
                _initialized = true;

                try
                {
                    // Kiểm tra driver đã cài chưa
                    bool installed = InputInterceptor.CheckDriverInstalled();
                    if (!installed)
                    {
                        // Driver chưa có — KHÔNG tự cài, không UAC.
                        // User phải chủ động gọi PromptAndInstallDriver() từ UI.
                        System.Diagnostics.Debug.WriteLine("[InterceptionInput] Driver chưa cài — cần gọi PromptAndInstallDriver() từ UI.");
                        _available = false;
                        return false;
                    }

                    // Driver đã có → khởi tạo context và hooks
                    bool initOk = InputInterceptor.Initialize();
                    System.Diagnostics.Debug.WriteLine($"[InterceptionInput] Initialize: {initOk}");
                    if (!initOk)
                    {
                        _available = false;
                        return false;
                    }

                    _keyboardHook = new KeyboardHook();
                    _mouseHook = new MouseHook();

                    _available = (_keyboardHook.IsInitialized || _keyboardHook.CanSimulateInput)
                              && (_mouseHook.IsInitialized || _mouseHook.CanSimulateInput);

                    System.Diagnostics.Debug.WriteLine($"[InterceptionInput] Available: {_available}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InterceptionInput] Init error: {ex.Message}");
                    _available = false;
                }

                return _available;
            }
        }

        /// <summary>
        /// Hiện dialog hỏi user có muốn cài Interception driver không, rồi cài với UAC.
        /// Gọi từ UI thread khi user bật Background Mode lần đầu.
        /// Trả về true nếu driver đã sẵn sàng sau khi hoàn tất.
        /// </summary>
        public static bool PromptAndInstallDriver(System.Windows.Window? ownerWindow = null)
        {
            // Tránh hiện dialog nhiều lần khi user di chuột liên tục qua ComboBox
            if (_promptInProgress) return false;

            if (IsDriverInstalled())
            {
                if (!_initialized) { Reset(); }
                return EnsureDriverInstalled();
            }

            // Đảm bảo chạy trên UI thread
            if (System.Windows.Application.Current?.Dispatcher != null
                && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(
                    () => PromptAndInstallDriver(ownerWindow));
            }

            _promptInProgress = true;
            try
            {
                return PromptInstallCore();
            }
            finally
            {
                _promptInProgress = false;
            }
        }

        private static bool PromptInstallCore()
        {
            // Dùng Topmost invisible window làm owner để MessageBox luôn nổi trên cùng
            System.Windows.Window MakeTopmostOwner()
            {
                var w = new System.Windows.Window
                {
                    Width = 0, Height = 0,
                    WindowStyle = System.Windows.WindowStyle.None,
                    ShowInTaskbar = false,
                    Topmost = true,
                    Opacity = 0,
                    AllowsTransparency = true,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
                };
                w.Show();
                return w;
            }

            var confirm = MakeTopmostOwner();

            var result = System.Windows.MessageBox.Show(
                owner: confirm,
                messageBoxText:
                    "Background Mode cần cài Interception Driver để gửi input đến app không active.\n\n" +
                    "Driver này hoạt động ở kernel-level, tương tự các tool macro chuyên nghiệp.\n" +
                    "Cần xác nhận quyền Admin (UAC) một lần duy nhất.\n\n" +
                    "Cài driver ngay?",
                caption: "Cần cài Interception Driver",
                button: System.Windows.MessageBoxButton.YesNo,
                icon: System.Windows.MessageBoxImage.Question);

            confirm.Close();

            if (result != System.Windows.MessageBoxResult.Yes)
                return false;

            // Spawn elevated process để cài driver
            bool installed = TryInstallWithUac();

            if (!installed)
            {
                var failOwner = MakeTopmostOwner();
                System.Windows.MessageBox.Show(
                    owner: failOwner,
                    messageBoxText:
                        "Không cài được driver.\n\n" +
                        "Bạn có thể thử chạy FlowMy với quyền Administrator một lần để cài.\n\n" +
                        "Background Mode sẽ dùng PostMessage fallback (ít tương thích hơn).",
                    caption: "Cài driver thất bại",
                    button: System.Windows.MessageBoxButton.OK,
                    icon: System.Windows.MessageBoxImage.Warning);
                failOwner.Close();
                return false;
            }

            Reset();
            bool ok = EnsureDriverInstalled();

            var resultOwner = MakeTopmostOwner();

            if (ok)
            {
                System.Windows.MessageBox.Show(
                    owner: resultOwner,
                    messageBoxText: "Interception Driver đã được cài thành công.\nBackground Mode sẵn sàng sử dụng.",
                    caption: "Thành công",
                    button: System.Windows.MessageBoxButton.OK,
                    icon: System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    owner: resultOwner,
                    messageBoxText:
                        "Driver đã cài nhưng chưa kích hoạt được.\n" +
                        "Vui lòng khởi động lại máy tính để driver có hiệu lực.",
                    caption: "Cần khởi động lại",
                    button: System.Windows.MessageBoxButton.OK,
                    icon: System.Windows.MessageBoxImage.Information);
                resultOwner.Close();
                return true;
            }

            resultOwner.Close();
            return ok;
        }
        /// App tự gọi lại chính mình với argument --install-interception.
        /// </summary>
        private static bool TryInstallWithUac()
        {
            try
            {
                // Thử cài thẳng trước (nếu đang chạy admin)
                if (InputInterceptor.CheckAdministratorRights())
                {
                    return InputInterceptor.InstallDriver();
                }

                // Spawn elevated process để cài
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return false;

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName  = exePath,
                    Arguments = "--install-interception-driver",
                    Verb      = "runas", // UAC elevation
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) return false;

                proc.WaitForExit(30_000); // chờ tối đa 30s
                return proc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InterceptionInput] TryInstallWithUac error: {ex.Message}");
                return false;
            }
        }

        /// <summary>Trả về true nếu Interception driver đã sẵn sàng.</summary>
        public static bool IsAvailable()
        {
            if (!_initialized) EnsureDriverInstalled();
            return _available;
        }

        /// <summary>
        /// Reset trạng thái để thử khởi tạo lại (dùng sau khi cài driver thành công).
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                try { _keyboardHook?.Dispose(); } catch { }
                try { _mouseHook?.Dispose(); }    catch { }
                try { InputInterceptor.Dispose(); } catch { }
                _keyboardHook = null;
                _mouseHook = null;
                _available = false;
                _initialized = false;
            }
        }

        /// <summary>
        /// Kiểm tra driver đã cài chưa mà không khởi tạo.
        /// </summary>
        public static bool IsDriverInstalled()
        {
            try { return InputInterceptor.CheckDriverInstalled(); }
            catch { return false; }
        }

        /// <summary>
        /// Kiểm tra process hiện tại có quyền admin không.
        /// </summary>
        public static bool HasAdminRights()
        {
            try { return InputInterceptor.CheckAdministratorRights(); }
            catch { return false; }
        }

        // ── Keyboard ─────────────────────────────────────────────────────────

        /// <summary>
        /// Nhấn 1 phím đơn ở kernel level — không cần app active.
        /// </summary>
        public static bool SendKey(ushort vkCode, int delayBetweenMs = 20)
        {
            if (!IsAvailable() || _keyboardHook == null) return false;
            try
            {
                var kc = (KeyCode)vkCode;
                bool ok = _keyboardHook.SimulateKeyPress(kc, 1);
                if (delayBetweenMs > 0) Thread.Sleep(delayBetweenMs);
                return ok;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InterceptionInput] SendKey error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Nhấn tổ hợp phím "Ctrl+C", "Alt+F4", v.v. ở kernel level.
        /// </summary>
        public static bool SendHotkey(string hotkeyText, int repeatCount = 1, int delayMs = 50)
        {
            if (!IsAvailable() || _keyboardHook == null) return false;
            if (string.IsNullOrWhiteSpace(hotkeyText)) return false;

            try
            {
                // SimulateInput nhận chuỗi trực tiếp như "Ctrl+C"
                bool ok = _keyboardHook.SimulateInput(hotkeyText, repeatCount, delayMs);
                return ok;
            }
            catch (Exception ex)
            {
                // Fallback: parse thủ công
                System.Diagnostics.Debug.WriteLine($"[InterceptionInput] SimulateInput failed ({ex.Message}), trying manual parse...");
                return SendHotkeyManual(hotkeyText, repeatCount, delayMs);
            }
        }

        private static bool SendHotkeyManual(string hotkeyText, int repeatCount, int delayMs)
        {
            if (_keyboardHook == null) return false;
            var parts = hotkeyText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var modifiers = new System.Collections.Generic.List<KeyCode>();
            var mainKeys = new System.Collections.Generic.List<KeyCode>();

            foreach (var part in parts)
            {
                var kc = ParseKeyCode(part);
                if (kc == null) continue;
                if (IsModifier(kc.Value)) modifiers.Add(kc.Value);
                else mainKeys.Add(kc.Value);
            }

            for (int i = 0; i < repeatCount; i++)
            {
                foreach (var mod in modifiers)
                    _keyboardHook.SimulateKeyDown(mod);

                foreach (var key in mainKeys)
                {
                    _keyboardHook.SimulateKeyDown(key);
                    Thread.Sleep(10);
                    _keyboardHook.SimulateKeyUp(key);
                }

                for (int j = modifiers.Count - 1; j >= 0; j--)
                    _keyboardHook.SimulateKeyUp(modifiers[j]);

                if (i < repeatCount - 1) Thread.Sleep(delayMs);
            }
            return true;
        }

        // ── Mouse ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Di chuyển và click tại tọa độ màn hình — kernel level.
        /// </summary>
        public static bool SendMouseClick(int screenX, int screenY, string button = "Left")
        {
            if (!IsAvailable() || _mouseHook == null) return false;
            try
            {
                _mouseHook.SetCursorPosition(screenX, screenY, true);
                Thread.Sleep(10);

                bool ok = button switch
                {
                    "Right"  => _mouseHook.SimulateRightButtonClick(1),
                    "Middle" => _mouseHook.SimulateMiddleButtonClick(1),
                    _        => _mouseHook.SimulateLeftButtonClick(1)
                };
                return ok;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InterceptionInput] SendMouseClick error: {ex.Message}");
                return false;
            }
        }

        /// <summary>Nhấn chuột xuống — kernel level.</summary>
        public static bool SendMouseDown(int screenX, int screenY, string button = "Left")
        {
            if (!IsAvailable() || _mouseHook == null) return false;
            try
            {
                _mouseHook.SetCursorPosition(screenX, screenY, true);
                Thread.Sleep(10);

                return button switch
                {
                    "Right"  => _mouseHook.SimulateRightButtonDown(),
                    "Middle" => _mouseHook.SimulateMiddleButtonDown(),
                    _        => _mouseHook.SimulateLeftButtonDown()
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InterceptionInput] SendMouseDown error: {ex.Message}");
                return false;
            }
        }

        /// <summary>Thả chuột — kernel level.</summary>
        public static bool SendMouseUp(int screenX, int screenY, string button = "Left")
        {
            if (!IsAvailable() || _mouseHook == null) return false;
            try
            {
                _mouseHook.SetCursorPosition(screenX, screenY, true);
                Thread.Sleep(10);

                return button switch
                {
                    "Right"  => _mouseHook.SimulateRightButtonUp(),
                    "Middle" => _mouseHook.SimulateMiddleButtonUp(),
                    _        => _mouseHook.SimulateLeftButtonUp()
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InterceptionInput] SendMouseUp error: {ex.Message}");
                return false;
            }
        }

        /// <summary>Scroll chuột — kernel level. delta > 0 = lên, < 0 = xuống.</summary>
        public static bool SendMouseScroll(int screenX, int screenY, int delta)
        {
            if (!IsAvailable() || _mouseHook == null) return false;
            try
            {
                _mouseHook.SetCursorPosition(screenX, screenY, true);
                Thread.Sleep(10);

                short amount = (short)Math.Abs(Math.Min(Math.Max(delta, short.MinValue), short.MaxValue));
                return delta > 0
                    ? _mouseHook.SimulateScrollUp(amount)
                    : _mouseHook.SimulateScrollDown(amount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InterceptionInput] SendMouseScroll error: {ex.Message}");
                return false;
            }
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        /// <summary>Giải phóng hooks khi app đóng.</summary>
        public static void Dispose()
        {
            try { _keyboardHook?.Dispose(); } catch { }
            try { _mouseHook?.Dispose(); }    catch { }
            try { InputInterceptor.Dispose(); } catch { }
            _keyboardHook = null;
            _mouseHook = null;
            _available = false;
            _initialized = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static KeyCode? ParseKeyCode(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "ctrl":
                case "control": return KeyCode.Control;
                case "alt":     return KeyCode.Alt;
                case "shift":   return KeyCode.LeftShift;
                case "win":
                case "windows": return KeyCode.LeftWindowsKey;
                case "enter":   return KeyCode.Enter;
                case "space":   return KeyCode.Space;
                case "tab":     return KeyCode.Tab;
                case "escape":
                case "esc":     return KeyCode.Escape;
                case "backspace": return KeyCode.Backspace;
                case "delete":
                case "del":     return KeyCode.Delete;
                case "up":      return KeyCode.Up;
                case "down":    return KeyCode.Down;
                case "left":    return KeyCode.Left;
                case "right":   return KeyCode.Right;
                case "home":    return KeyCode.Home;
                case "end":     return KeyCode.End;
                case "pageup":  return KeyCode.PageUp;
                case "pagedown": return KeyCode.PageDown;
            }

            if (Enum.TryParse<KeyCode>(name, true, out var kc))
                return kc;

            // Single char
            if (name.Length == 1)
            {
                short vkScan = VkKeyScan(name[0]);
                if (vkScan != -1)
                {
                    int vk = vkScan & 0xFF;
                    if (Enum.IsDefined(typeof(KeyCode), vk))
                        return (KeyCode)vk;
                }
            }

            return null;
        }

        private static bool IsModifier(KeyCode kc) =>
            kc is KeyCode.Control
               or KeyCode.Alt
               or KeyCode.LeftShift    or KeyCode.RightShift
               or KeyCode.LeftWindowsKey or KeyCode.RightWindowsKey;

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);
    }
}
