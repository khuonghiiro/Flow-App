using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FlowMy.Services.Interaction
{
    /// <summary>
    /// Service để gửi keyboard input (simulate key press/hotkey press).
    /// </summary>
    public class KeyboardInputService
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint INPUT_KEYBOARD = 1;
        private const uint MAPVK_VK_TO_VSC = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// Gửi một phím đơn (key press).
        /// </summary>
        public void SendKeyPress(string keyText, int repeatCount = 1, int delayMs = 50)
        {
            SendKeyPress(keyText, repeatCount, delayMs, IntPtr.Zero, FlowMy.Helpers.BackgroundInputHelper.InputMode.Auto);
        }

        public void SendKeyPress(string keyText, int repeatCount, int delayMs,
                               IntPtr targetHwnd, FlowMy.Helpers.BackgroundInputHelper.InputMode mode)
        {
            if (string.IsNullOrWhiteSpace(keyText)) return;
            if (repeatCount < 1) repeatCount = 1;
            if (delayMs < 0) delayMs = 0;

            // Use BackgroundInputHelper if targetHwnd is provided and mode is not ForegroundActivation
            if (targetHwnd != IntPtr.Zero && mode != FlowMy.Helpers.BackgroundInputHelper.InputMode.ForegroundActivation)
            {
                var vk = KeyInterop.VirtualKeyFromKey(ParseKey(keyText) ?? Key.None);
                if (vk != 0)
                {
                    FlowMy.Helpers.BackgroundInputHelper.SendKey(targetHwnd, (ushort)vk, mode);
                    return;
                }
            }

            var key = ParseKey(keyText);
            if (key == null)
            {
                // Log or handle parsing failure - keyText might not be a valid Key enum name
                System.Diagnostics.Debug.WriteLine($"Failed to parse key: {keyText}");
                return;
            }

            for (int i = 0; i < repeatCount; i++)
            {
                // Send keydown and keyup in a single batch for better reliability
                SendKeyPressBatch(key.Value);
                
                if (i < repeatCount - 1)
                {
                    Thread.Sleep(delayMs); // Delay between repeats
                }
            }
        }

        /// <summary>
        /// Gửi phím đơn (bất đồng bộ) với CancellationToken.
        /// Có hỗ trợ giữ phím và chạy nền cho giữ phím.
        /// </summary>
        public async Task SendKeyPressAsync(string keyText, int repeatCount, double delayMs, double holdDurationMs, bool isHoldAsync,
                               IntPtr targetHwnd, FlowMy.Helpers.BackgroundInputHelper.InputMode mode, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(keyText)) return;
            if (repeatCount < 1) repeatCount = 1;
            if (delayMs < 0) delayMs = 0;

            var key = ParseKey(keyText);
            var vk = key.HasValue ? KeyInterop.VirtualKeyFromKey(key.Value) : 0;
            bool isModifier = vk == 0x10 || vk == 0xA0 || vk == 0xA1 || // Shift
                              vk == 0x11 || vk == 0xA2 || vk == 0xA3 || // Ctrl
                              vk == 0x12 || vk == 0xA4 || vk == 0xA5 || // Alt
                              vk == 0x5B || vk == 0x5C;                 // Win

            for (int i = 0; i < repeatCount; i++)
            {
                if (ct.IsCancellationRequested) break;

                // Use BackgroundInputHelper if targetHwnd is provided and mode is not ForegroundActivation
                if (targetHwnd != IntPtr.Zero && mode != FlowMy.Helpers.BackgroundInputHelper.InputMode.ForegroundActivation && vk != 0)
                {
                    if (holdDurationMs > 0)
                    {
                        if (isHoldAsync)
                        {
                            _ = Task.Run(async () =>
                            {
                                await SimulateHoldAsync(() => FlowMy.Helpers.BackgroundInputHelper.SendKeyDown(targetHwnd, (ushort)vk, mode), (int)holdDurationMs, isModifier, ct);
                                FlowMy.Helpers.BackgroundInputHelper.SendKeyUp(targetHwnd, (ushort)vk, mode);
                            }, ct);
                        }
                        else
                        {
                            await SimulateHoldAsync(() => FlowMy.Helpers.BackgroundInputHelper.SendKeyDown(targetHwnd, (ushort)vk, mode), (int)holdDurationMs, isModifier, ct);
                            FlowMy.Helpers.BackgroundInputHelper.SendKeyUp(targetHwnd, (ushort)vk, mode);
                        }
                    }
                    else
                    {
                        FlowMy.Helpers.BackgroundInputHelper.SendKey(targetHwnd, (ushort)vk, mode);
                    }
                }
                else if (key.HasValue)
                {
                    if (holdDurationMs > 0)
                    {
                        if (isHoldAsync)
                        {
                            _ = Task.Run(async () =>
                            {
                                await SimulateHoldAsync(() => SendKeyDown(key.Value), (int)holdDurationMs, isModifier, ct);
                                SendKeyUp(key.Value);
                            }, ct);
                        }
                        else
                        {
                            await SimulateHoldAsync(() => SendKeyDown(key.Value), (int)holdDurationMs, isModifier, ct);
                            SendKeyUp(key.Value);
                        }
                    }
                    else
                    {
                        // Send keydown and keyup in a single batch for better reliability
                        SendKeyPressBatch(key.Value);
                    }
                }
                
                if (i < repeatCount - 1)
                {
                    try
                    {
                        await Task.Delay((int)delayMs, ct).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task SimulateHoldAsync(Action keyDownAction, int holdDurationMs, bool isModifier, CancellationToken ct)
        {
            if (holdDurationMs <= 0) return;

            // Nhấn xuống lần đầu
            keyDownAction();

            // Nếu là phím Modifier (Ctrl, Alt, Shift, Win) -> Không cần giả lập auto-repeat của hệ điều hành
            if (isModifier)
            {
                try { await Task.Delay(holdDurationMs, ct).ConfigureAwait(false); } catch (TaskCanceledException) { }
                return;
            }

            // Nếu thời gian giữ quá ngắn, chỉ cần chờ rồi kết thúc
            if (holdDurationMs <= 30)
            {
                try { await Task.Delay(holdDurationMs, ct).ConfigureAwait(false); } catch (TaskCanceledException) { }
                return;
            }

            var startTime = DateTime.UtcNow;
            
            // Thời gian chờ trước khi bắt đầu lặp lại phím (auto-repeat delay, thường là 500ms)
            int initialDelay = Math.Min(500, holdDurationMs);
            try { await Task.Delay(initialDelay, ct).ConfigureAwait(false); } catch (TaskCanceledException) { return; }

            int elapsed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            // Bắt đầu lặp lại phím (auto-repeat rate, thường khoảng 30ms/lần)
            while (elapsed < holdDurationMs && !ct.IsCancellationRequested)
            {
                keyDownAction();
                
                int remaining = holdDurationMs - elapsed;
                int waitTime = Math.Min(30, remaining);
                if (waitTime <= 0) break;

                try { await Task.Delay(waitTime, ct).ConfigureAwait(false); } catch (TaskCanceledException) { break; }
                elapsed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            }
        }

        /// <summary>
        /// Kiểm tra xem virtual key có phải là phím multimedia/đặc biệt không thể gửi bằng SendInput.
        /// </summary>
        private bool IsUnsupportedKey(int vk)
        {
            // Multimedia keys và một số phím đặc biệt không thể gửi bằng SendInput
            return vk == 0xAD || // VK_VOLUME_MUTE
                   vk == 0xAE || // VK_VOLUME_DOWN
                   vk == 0xAF || // VK_VOLUME_UP
                   vk == 0xB0 || // VK_MEDIA_NEXT_TRACK
                   vk == 0xB1 || // VK_MEDIA_PREV_TRACK
                   vk == 0xB2 || // VK_MEDIA_STOP
                   vk == 0xB3 || // VK_MEDIA_PLAY_PAUSE
                   vk == 0xB4 || // VK_LAUNCH_MAIL
                   vk == 0xB5 || // VK_LAUNCH_MEDIA_SELECT
                   vk == 0xB6 || // VK_LAUNCH_APP1
                   vk == 0xB7;   // VK_LAUNCH_APP2
        }

        /// <summary>
        /// Gửi key press (keydown + keyup) với delay nhỏ giữa chúng để đảm bảo ứng dụng nhận được.
        /// </summary>
        private void SendKeyPressBatch(Key key)
        {
            var vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get virtual key for: {key}");
                return;
            }

            // Check if this is an unsupported key (multimedia keys)
            if (IsUnsupportedKey(vk))
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Key {key} (VK={vk}) is a multimedia/special key. Using keybd_event fallback.");
                // Try using keybd_event as fallback for multimedia keys
                try
                {
                    keybd_event((byte)vk, 0, 0, IntPtr.Zero); // Key down
                    Thread.Sleep(30);
                    keybd_event((byte)vk, 0, 2, IntPtr.Zero); // Key up (2 = KEYEVENTF_KEYUP)
                    Thread.Sleep(10);
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"keybd_event also failed for VK={vk}: {ex.Message}");
                    return;
                }
            }

            // Get scan code - if 0, we'll use virtual key only
            var scan = MapVirtualKey((uint)vk, MAPVK_VK_TO_VSC);
            
            // Determine if this is an extended key (right Alt, right Ctrl, arrow keys, etc.)
            bool isExtended = IsExtendedKey(vk);
            uint extendedFlag = isExtended ? KEYEVENTF_EXTENDEDKEY : 0;

            var inputSize = Marshal.SizeOf(typeof(INPUT));

            // If scan code is 0 or invalid, use virtual key only (set scan to 0)
            // Some keys like volume keys may not have valid scan codes
            ushort scanCode = (scan > 0 && scan <= 0xFF) ? (ushort)scan : (ushort)0;

            // Send key down
            var keyDown = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)vk,
                        wScan = scanCode,
                        dwFlags = KEYEVENTF_KEYDOWN | extendedFlag,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            System.Diagnostics.Debug.WriteLine($"Sending key down: vk={vk}, scan={scanCode}, flags={keyDown.U.ki.dwFlags}, size={inputSize}");
            var resultDown = SendInput(1, new[] { keyDown }, inputSize);
            if (resultDown != 1)
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"SendKeyDown failed: error code: {error}, vk={vk}, scan={scanCode}");
                // Try with scan code = 0 if it failed
                if (scanCode != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Retrying with scan=0");
                    keyDown.U.ki.wScan = 0;
                    resultDown = SendInput(1, new[] { keyDown }, inputSize);
                    if (resultDown != 1)
                    {
                        error = Marshal.GetLastWin32Error();
                        System.Diagnostics.Debug.WriteLine($"SendKeyDown retry failed: error code: {error}. Trying keybd_event fallback.");
                        // Fallback to keybd_event
                        try
                        {
                            keybd_event((byte)vk, 0, 0, IntPtr.Zero);
                            Thread.Sleep(30);
                            keybd_event((byte)vk, 0, 2, IntPtr.Zero);
                            Thread.Sleep(10);
                            return;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"keybd_event fallback also failed: {ex.Message}");
                            return;
                        }
                    }
                }
            }

            // Small delay between keydown and keyup (some apps need this)
            Thread.Sleep(30);

            // Send key up
            var keyUp = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)vk,
                        wScan = scanCode,
                        dwFlags = KEYEVENTF_KEYUP | extendedFlag,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            System.Diagnostics.Debug.WriteLine($"Sending key up: vk={vk}, scan={scanCode}, flags={keyUp.U.ki.dwFlags}, size={inputSize}");
            var resultUp = SendInput(1, new[] { keyUp }, inputSize);
            if (resultUp != 1)
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"SendKeyUp failed: error code: {error}, vk={vk}, scan={scanCode}");
                // Try with scan code = 0 if it failed
                if (scanCode != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Retrying with scan=0");
                    keyUp.U.ki.wScan = 0;
                    resultUp = SendInput(1, new[] { keyUp }, inputSize);
                    if (resultUp != 1)
                    {
                        error = Marshal.GetLastWin32Error();
                        System.Diagnostics.Debug.WriteLine($"SendKeyUp retry failed: error code: {error}. Trying keybd_event fallback.");
                        // Fallback to keybd_event
                        try
                        {
                            keybd_event((byte)vk, 0, 2, IntPtr.Zero);
                            Thread.Sleep(10);
                            return;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"keybd_event fallback also failed: {ex.Message}");
                            return;
                        }
                    }
                }
            }

            // Small delay after keyup to ensure the event is processed
            Thread.Sleep(10);
        }

        /// <summary>
        /// Kiểm tra xem key có phải là extended key không (cần flag KEYEVENTF_EXTENDEDKEY).
        /// </summary>
        private bool IsExtendedKey(int vk)
        {
            // Extended keys include: right Alt, right Ctrl, arrow keys, numpad Enter, etc.
            return vk == 0x5D || // Right Windows key
                   vk == 0xA5 || // Right Alt
                   vk == 0xA4 || // Left Alt (sometimes)
                   vk == 0xA3 || // Right Ctrl
                   vk == 0xA2 || // Left Ctrl (sometimes)
                   vk == 0x2D || // Insert
                   vk == 0x2E || // Delete
                   vk == 0x21 || // Page Up
                   vk == 0x22 || // Page Down
                   vk == 0x23 || // End
                   vk == 0x24 || // Home
                   vk == 0x25 || // Left Arrow
                   vk == 0x26 || // Up Arrow
                   vk == 0x27 || // Right Arrow
                   vk == 0x28 || // Down Arrow
                   vk == 0x2C || // Print Screen
                   vk == 0x2D || // Insert
                   vk == 0x2E || // Delete
                   (vk >= 0x60 && vk <= 0x6F) || // Numpad keys
                   vk == 0x6C; // Numpad Enter
        }

        /// <summary>
        /// Gửi tổ hợp phím (hotkey press). Hỗ trợ nhiều phím như Ctrl+H+L+O+...
        /// </summary>
        public void SendHotkeyPress(string hotkeyText, int repeatCount = 1, int delayMs = 50)
        {
            SendHotkeyPress(hotkeyText, repeatCount, delayMs, IntPtr.Zero, FlowMy.Helpers.BackgroundInputHelper.InputMode.Auto);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        public void SendHotkeyPress(string hotkeyText, int repeatCount, int delayMs,
                                  IntPtr targetHwnd, FlowMy.Helpers.BackgroundInputHelper.InputMode mode)
        {
            if (string.IsNullOrWhiteSpace(hotkeyText)) return;
            if (repeatCount < 1) repeatCount = 1;
            if (delayMs < 0) delayMs = 0;

            var keys = ParseHotkey(hotkeyText);
            if (keys == null || (keys.Modifiers.Count == 0 && keys.MainKeys.Count == 0))
            {
                // Log or handle parsing failure
                System.Diagnostics.Debug.WriteLine($"Failed to parse hotkey: {hotkeyText}");
                return;
            }

            // Intercept Win+L to lock workstation (Windows blocks synthetic Win+L for security)
            if (keys.Modifiers.Count == 1 && (keys.Modifiers[0] == Key.LWin || keys.Modifiers[0] == Key.RWin) &&
                keys.MainKeys.Count == 1 && keys.MainKeys[0] == Key.L)
            {
                LockWorkStation();
                return;
            }

            // Use BackgroundInputHelper if targetHwnd is provided and mode is not ForegroundActivation
            if (targetHwnd != IntPtr.Zero && mode != FlowMy.Helpers.BackgroundInputHelper.InputMode.ForegroundActivation)
            {
                // Gửi từng phím trong hotkey: hold modifiers → press main keys → release modifiers
                for (int i = 0; i < repeatCount; i++)
                {
                    // Nhấn modifier keys (WM_KEYDOWN)
                    foreach (var key in keys.Modifiers)
                    {
                        var vk = (ushort)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
                        if (vk != 0) SendModifierKeyDirectMessage(targetHwnd, vk, down: true);
                    }
                    // Nhấn + thả main keys
                    foreach (var key in keys.MainKeys)
                    {
                        var vk = (ushort)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
                        if (vk != 0) FlowMy.Helpers.BackgroundInputHelper.SendKey(targetHwnd, vk, mode);
                    }
                    // Thả modifier keys (WM_KEYUP, thứ tự ngược)
                    for (int j = keys.Modifiers.Count - 1; j >= 0; j--)
                    {
                        var vk = (ushort)System.Windows.Input.KeyInterop.VirtualKeyFromKey(keys.Modifiers[j]);
                        if (vk != 0) SendModifierKeyDirectMessage(targetHwnd, vk, down: false);
                    }
                    if (i < repeatCount - 1)
                        Thread.Sleep(delayMs);
                }
                return;
            }

            for (int i = 0; i < repeatCount; i++)
            {
                // Press all modifier keys first
                foreach (var key in keys.Modifiers)
                {
                    SendKeyDown(key);
                }

                // Press main keys
                foreach (var key in keys.MainKeys)
                {
                    SendKeyDown(key);
                    Thread.Sleep(10);
                    SendKeyUp(key);
                }

                // Release all modifier keys (in reverse order)
                for (int j = keys.Modifiers.Count - 1; j >= 0; j--)
                {
                    SendKeyUp(keys.Modifiers[j]);
                }

                if (i < repeatCount - 1)
                {
                    Thread.Sleep(delayMs); // Delay between repeats
                }
            }
        }

        private void SendKeyDown(Key key)
        {
            var vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0) return;

            // Check if this is Win key - use keybd_event for better compatibility
            if (vk == 0x5B || vk == 0x5C) // VK_LWIN or VK_RWIN
            {
                try
                {
                    keybd_event((byte)vk, 0, 0, IntPtr.Zero);
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"keybd_event failed for Win key VK={vk}: {ex.Message}");
                    return;
                }
            }

            var scan = MapVirtualKey((uint)vk, MAPVK_VK_TO_VSC);
            bool isExtended = IsExtendedKey(vk);
            
            // Validate scan code - if scan equals vk or is invalid, use 0
            ushort scanCode = 0;
            if (scan > 0 && scan <= 0xFF && scan != vk)
            {
                scanCode = (ushort)scan;
            }
            
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)vk,
                        wScan = scanCode,
                        dwFlags = KEYEVENTF_KEYDOWN | (isExtended ? KEYEVENTF_EXTENDEDKEY : 0),
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            var result = SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
            if (result != 1)
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"SendKeyDown failed: error code: {error}, vk={vk}, scan={scanCode}");
                // Try with scan = 0 if it failed
                if (scanCode != 0)
                {
                    input.U.ki.wScan = 0;
                    result = SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
                    if (result != 1)
                    {
                        error = Marshal.GetLastWin32Error();
                        System.Diagnostics.Debug.WriteLine($"SendKeyDown retry failed: error code: {error}. Trying keybd_event fallback.");
                        // Fallback to keybd_event
                        try
                        {
                            keybd_event((byte)vk, 0, 0, IntPtr.Zero);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"keybd_event fallback also failed: {ex.Message}");
                        }
                    }
                }
            }
        }

        private void SendKeyUp(Key key)
        {
            var vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0) return;

            // Check if this is Win key - use keybd_event for better compatibility
            if (vk == 0x5B || vk == 0x5C) // VK_LWIN or VK_RWIN
            {
                try
                {
                    keybd_event((byte)vk, 0, 2, IntPtr.Zero); // 2 = KEYEVENTF_KEYUP
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"keybd_event failed for Win key VK={vk}: {ex.Message}");
                    return;
                }
            }

            var scan = MapVirtualKey((uint)vk, MAPVK_VK_TO_VSC);
            bool isExtended = IsExtendedKey(vk);
            
            // Validate scan code - if scan equals vk or is invalid, use 0
            ushort scanCode = 0;
            if (scan > 0 && scan <= 0xFF && scan != vk)
            {
                scanCode = (ushort)scan;
            }
            
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)vk,
                        wScan = scanCode,
                        dwFlags = KEYEVENTF_KEYUP | (isExtended ? KEYEVENTF_EXTENDEDKEY : 0),
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            var result = SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
            if (result != 1)
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"SendKeyUp failed: error code: {error}, vk={vk}, scan={scanCode}");
                // Try with scan = 0 if it failed
                if (scanCode != 0)
                {
                    input.U.ki.wScan = 0;
                    result = SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
                    if (result != 1)
                    {
                        error = Marshal.GetLastWin32Error();
                        System.Diagnostics.Debug.WriteLine($"SendKeyUp retry failed: error code: {error}. Trying keybd_event fallback.");
                        // Fallback to keybd_event
                        try
                        {
                            keybd_event((byte)vk, 0, 2, IntPtr.Zero);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"keybd_event fallback also failed: {ex.Message}");
                        }
                    }
                }
            }
        }

        private Key? ParseKey(string keyText)
        {
            if (string.IsNullOrWhiteSpace(keyText)) return null;

            var trimmed = keyText.Trim();

            // Try to parse as Key enum (case-insensitive)
            if (Enum.TryParse<Key>(trimmed, true, out var key) && key != Key.None)
            {
                return key;
            }

            // Try common aliases
            var normalized = trimmed.ToLowerInvariant();
            switch (normalized)
            {
                case "space":
                    return Key.Space;
                case "enter":
                case "return":
                    return Key.Enter;
                case "tab":
                    return Key.Tab;
                case "backspace":
                    return Key.Back;
                case "delete":
                case "del":
                    return Key.Delete;
                case "escape":
                case "esc":
                    return Key.Escape;
                case "up":
                case "↑":
                    return Key.Up;
                case "down":
                case "↓":
                    return Key.Down;
                case "left":
                case "←":
                    return Key.Left;
                case "right":
                case "→":
                    return Key.Right;
                case "home":
                    return Key.Home;
                case "end":
                    return Key.End;
                case "pageup":
                case "pgup":
                    return Key.PageUp;
                case "pagedown":
                case "pgdn":
                    return Key.PageDown;
                case "prtsc":
                    return Key.PrintScreen;
                case "insert":
                    return Key.Insert;
                case "capslock":
                    return Key.CapsLock;
                case "pause":
                    return Key.Pause;
                case "numlock":
                    return Key.NumLock;
                case "scrolllock":
                    return Key.Scroll;
            }

            // Handle Num0–Num9 (numpad)
            if (normalized.StartsWith("num") && normalized.Length == 4 &&
                char.IsDigit(normalized[3]))
            {
                int digit = normalized[3] - '0';
                return (Key)(Key.NumPad0 + digit);
            }

            // Handle Num* Num+ Num- Num. Num/
            switch (normalized)
            {
                case "num*": return Key.Multiply;
                case "num+": return Key.Add;
                case "num-": return Key.Subtract;
                case "num.": return Key.Decimal;
                case "num/": return Key.Divide;
            }

            // Handle F1–F24
            if (normalized.StartsWith("f") && int.TryParse(normalized[1..], out int fNum) &&
                fNum >= 1 && fNum <= 24)
            {
                return (Key)(Key.F1 + fNum - 1);
            }

            // Handle VK_XX hex codes from GetKeyName fallback
            if (normalized.StartsWith("vk_") && int.TryParse(normalized[3..], System.Globalization.NumberStyles.HexNumber, null, out int vkHex))
            {
                var k = KeyInterop.KeyFromVirtualKey(vkHex);
                if (k != Key.None) return k;
            }

            return null;
        }

        private ParsedHotkey? ParseHotkey(string hotkeyText)
        {
            if (string.IsNullOrWhiteSpace(hotkeyText)) return null;

            var parts = hotkeyText.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0) return null;

            var modifiers = new List<Key>();
            var mainKeys = new List<Key>();

            foreach (var part in parts)
            {
                var normalized = part.Trim();
                if (string.IsNullOrWhiteSpace(normalized)) continue;

                // Check if it's a modifier first
                var modifier = ParseModifier(normalized);
                if (modifier.HasValue)
                {
                    modifiers.Add(modifier.Value);
                }
                else
                {
                    // Try to parse as main key using Key enum
                    if (Enum.TryParse<Key>(normalized, true, out var key) && key != Key.None)
                    {
                        // Don't add modifier keys as main keys
                        if (!IsModifierKeyEnum(key))
                        {
                            mainKeys.Add(key);
                        }
                    }
                    else
                    {
                        // Try common aliases for main keys
                        var keyAlias = ParseKeyAlias(normalized);
                        if (keyAlias.HasValue && !IsModifierKeyEnum(keyAlias.Value))
                        {
                            mainKeys.Add(keyAlias.Value);
                        }
                    }
                }
            }

            // Allow hotkey with only modifiers (e.g., Ctrl+Alt) or only main keys
            // But at least one key must be present
            if (mainKeys.Count == 0 && modifiers.Count == 0) return null;

            return new ParsedHotkey
            {
                Modifiers = modifiers,
                MainKeys = mainKeys
            };
        }

        private bool IsModifierKeyEnum(Key key)
        {
            return key is Key.LeftCtrl or Key.RightCtrl
                or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift
                or Key.LWin or Key.RWin
                or Key.System;
        }

        private Key? ParseKeyAlias(string alias)
        {
            var normalized = alias.ToLowerInvariant();
            return normalized switch
            {
                "space" => Key.Space,
                "enter" or "return" => Key.Enter,
                "tab" => Key.Tab,
                "backspace" => Key.Back,
                "delete" or "del" => Key.Delete,
                "escape" or "esc" => Key.Escape,
                "up" => Key.Up,
                "down" => Key.Down,
                "left" => Key.Left,
                "right" => Key.Right,
                "home" => Key.Home,
                "end" => Key.End,
                "pageup" or "pgup" => Key.PageUp,
                "pagedown" or "pgdn" => Key.PageDown,
                _ => null
            };
        }

        private Key? ParseModifier(string modifierText)
        {
            var normalized = modifierText.Trim();
            return normalized.ToLowerInvariant() switch
            {
                "ctrl" or "control" => Key.LeftCtrl,
                "alt" => Key.LeftAlt,
                "shift" => Key.LeftShift,
                "win" or "windows" => Key.LWin,
                _ => null
            };
        }

        /// <summary>
        /// Gửi WM_KEYDOWN / WM_KEYUP cho modifier key đến window nền mà không cần focus.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private static void SendModifierKeyDirectMessage(IntPtr hWnd, ushort vk, bool down)
        {
            const uint WM_KEYDOWN = 0x0100;
            const uint WM_KEYUP   = 0x0101;
            uint scan = MapVirtualKey(vk, 0);
            // lParam cho KEYDOWN: repeat=1, scan, extended=0, context=0, prev=0, transition=0
            // lParam cho KEYUP:   repeat=1, scan, extended=0, context=0, prev=1, transition=1
            IntPtr lParamDown = (IntPtr)((scan << 16) | 1);
            IntPtr lParamUp   = (IntPtr)((1 << 31) | (1 << 30) | (scan << 16) | 1);
            PostMessage(hWnd, down ? WM_KEYDOWN : WM_KEYUP, (IntPtr)vk, down ? lParamDown : lParamUp);
        }

        private class ParsedHotkey
        {
            public List<Key> Modifiers { get; set; } = new();
            public List<Key> MainKeys { get; set; } = new();
        }

        /// <summary>
        /// Giải phóng tất cả các modifier keys để tránh bị kẹt phím.
        /// </summary>
        public void ReleaseAllModifiers()
        {
            var modifiers = new[]
            {
                Key.LeftCtrl, Key.RightCtrl,
                Key.LeftAlt, Key.RightAlt,
                Key.LeftShift, Key.RightShift,
                Key.LWin, Key.RWin
            };

            foreach (var mod in modifiers)
            {
                SendKeyUp(mod);
            }
        }
    }
}

