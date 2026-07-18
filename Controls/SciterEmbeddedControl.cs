using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using EmptyFlow.SciterAPI;
using FlowMy.Helpers;
using FlowMy.Models.Nodes;
using FlowMy.Views.Overlays;

namespace FlowMy.Controls
{
    public class SciterEmbeddedControl : HwndHost
    {
        private readonly DynamicUiNode _node;
        private SciterAPIHost? _host;
        private IntPtr _sciterWindow = IntPtr.Zero;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        private const int GWL_STYLE = -16;
        private const int WS_CHILD = 0x40000000;
        private const int WS_POPUP = unchecked((int)0x80000000);

        public SciterEmbeddedControl(DynamicUiNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            var sciterFolder = SciterWindowHelper.GetSciterFolder();
            _host = new SciterAPIHost(sciterFolder);
            _host.EnableDebugMode();
            _host.EnableFeatures();

            int width = (int)ActualWidth;
            int height = (int)ActualHeight;
            if (width <= 0) width = 300;
            if (height <= 0) height = 200;

            // Create Sciter as a child window - using positional parameters to avoid named args mismatch
            _sciterWindow = _host.CreateWindow(
                width,
                height,
                0,
                0,
                WindowsFlags.Child,
                false,
                null,
                null,
                hwndParent.Handle,
                false
            );

            if (_sciterWindow != IntPtr.Zero)
            {
                // Force WS_CHILD style and set parent HWND via Win32 API to satisfy WPF's HwndHost requirement
                SetParent(_sciterWindow, hwndParent.Handle);
                int style = GetWindowLong(_sciterWindow, GWL_STYLE);
                style = (style & ~WS_POPUP) | WS_CHILD;
                SetWindowLong(_sciterWindow, GWL_STYLE, style);
            }

            // Register interop event handler
            var eventHandler = new DynamicUiEventHandler(_sciterWindow, _host, (methodName, args) =>
            {
                if (methodName == "submitForm" || methodName == "updateValue")
                {
                    if (args.Count > 0)
                    {
                        var dataVal = args[0];
                        if (dataVal.IsMap)
                        {
                            var mapItems = _host.GetMapItems(ref dataVal);
                            foreach (var kvp in mapItems)
                            {
                                var key = kvp.Key;
                                var val = kvp.Value;
                                object? value = null;

                                if (val.IsBoolean) value = val.ToBoolean() == true;
                                else if (val.IsInteger) value = _host.GetValueInt32(ref val);
                                else if (val.IsFloat) value = _host.GetValueDouble(ref val);
                                else value = _host.GetValueString(ref val);

                                _node.ResolvedOutputs[key] = value;
                            }
                        }
                    }
                }
                else if (methodName == "getPrefilledValue")
                {
                    if (args.Count > 0)
                    {
                        var keyVal = args[0];
                        var key = _host.GetValueString(ref keyVal);
                        
                        if (_node.ResolvedOutputs.TryGetValue(key, out var resolved) && resolved != null)
                        {
                            return _host.CreateValue(resolved.ToString());
                        }

                        var field = _node.Fields.FirstOrDefault(f => f.Key == key);
                        if (field != null)
                        {
                            return _host.CreateValue(field.DefaultValue ?? "");
                        }
                    }
                    return _host.CreateNullValue();
                }

                return null;
            });

            _host.AddWindowEventHandler(eventHandler);

            UpdateContent();

            return new HandleRef(this, _sciterWindow);
        }

        public void UpdateOutputsFromDom(string paramsCode, Dictionary<string, object?> resolvedOutputs)
        {
            if (_host == null || _sciterWindow == IntPtr.Zero || string.IsNullOrWhiteSpace(paramsCode)) return;

            var lines = paramsCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var rawLine in lines)
            {
                var line = rawLine?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line.StartsWith("#")) continue;

                string[] parts;
                if (line.Contains(":"))
                    parts = line.Split(new[] { ':' }, 2);
                else if (line.Contains("="))
                    parts = line.Split(new[] { '=' }, 2);
                else
                    continue;

                if (parts.Length != 2) continue;
                var key = parts[0].Trim();
                var selector = parts[1].Trim();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(selector)) continue;

                var jsSelector = selector.Replace("\\", "\\\\").Replace("\"", "\\\"");
                var script = $@"(function() {{
  try {{
    var el = document.querySelector(""{jsSelector}"");
    if (!el) return null;
    if (typeof el.value !== 'undefined') return el.value;
    if (el.textContent) return el.textContent;
    return null;
  }} catch (e) {{
    return null;
  }}
}})();";

                var resultVal = _host.CreateNullValue();
                bool ok = _host.ExecuteWindowEval(_sciterWindow, script, out resultVal);
                if (ok)
                {
                    var valueStr = _host.GetValueString(ref resultVal);
                    resolvedOutputs[key] = valueStr ?? "";
                }
            }
        }

        public void UpdateContent()
        {
            if (_host == null || _sciterWindow == IntPtr.Zero) return;

            var combinedHtml = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        {_node.CssCode}
    </style>
</head>
<body>
    {_node.HtmlCode}
    <script type=""module"">
        {_node.JsCode}
    </script>
</body>
</html>";

            _host.LoadHtml(combinedHtml, _sciterWindow);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            if (_host != null && _sciterWindow != IntPtr.Zero)
            {
                try
                {
                    _host.CloseWindow(_sciterWindow);
                }
                catch { }
                _sciterWindow = IntPtr.Zero;
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (_host != null && _sciterWindow != IntPtr.Zero)
            {
                int width = (int)sizeInfo.NewSize.Width;
                int height = (int)sizeInfo.NewSize.Height;
                MoveWindow(_sciterWindow, 0, 0, width, height, true);
            }
        }
    }
}
