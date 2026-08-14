using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EmptyFlow.SciterAPI;
using FlowMy.Models.Nodes;
using FlowMy.Helpers;

namespace FlowMy.Views.Overlays
{
    public class DynamicUiPopupWindow
    {
        private readonly DynamicUiNode _node;
        private readonly Dictionary<string, string> _prefilledValues;
        private readonly TaskCompletionSource<bool> _tcs = new();
        private SciterAPIHost? _host;
        private IntPtr _window = IntPtr.Zero;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_POPUP = unchecked((int)0x80000000);

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        public Dictionary<string, object?> SubmittedValues { get; } = new();

        public Task<bool> WaitForSubmitAsync() => _tcs.Task;

        public void Close()
        {
            if (_host != null && _window != IntPtr.Zero)
            {
                try
                {
                    _host.CloseWindow(_window);
                }
                catch { }
            }
        }

        public DynamicUiPopupWindow(DynamicUiNode node, Dictionary<string, string> prefilledValues)
        {
            _node = node;
            _prefilledValues = prefilledValues ?? new Dictionary<string, string>();
        }

        public void Show()
        {
            var sciterFolder = SciterWindowHelper.GetSciterFolder();

            // Run Sciter event loop in an STA thread to prevent blocking WPF application thread
            var thread = new Thread(() => RunSciterLoop(sciterFolder));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void RunSciterLoop(string sciterFolder)
        {
            try
            {
                var host = new SciterAPIHost(sciterFolder);
                _host = host;
                host.EnableDebugMode();
                host.EnableFeatures();

                int width = (int)_node.WindowWidth;
                int height = (int)_node.WindowHeight;
                int x = 100;
                int y = 100;

                try
                {
                    var mousePoint = System.Windows.Forms.Control.MousePosition;
                    var screen = System.Windows.Forms.Screen.FromPoint(mousePoint);
                    var bounds = screen.WorkingArea;

                    x = mousePoint.X - (width / 2);
                    y = mousePoint.Y - 50;

                    if (x < bounds.Left) x = bounds.Left;
                    if (x + width > bounds.Right) x = bounds.Right - width;
                    if (y < bounds.Top) y = bounds.Top;
                    if (y + height > bounds.Bottom) y = bounds.Bottom - height - 10;
                }
                catch
                {
                    // Fallback to coordinates
                }

                // Create the Sciter Win32 window (Popup flag removes basic default window border during creation)
                var window = host.CreateWindow(width: width, height: height, x: x, y: y, flags: WindowsFlags.Popup, asMain: false);
                _window = window;

                if (window != IntPtr.Zero)
                {
                    const int WS_VISIBLE = 0x10000000;
                    const int WS_CLIPSIBLINGS = 0x04000000;
                    
                    int style = WS_POPUP | WS_VISIBLE | WS_CLIPSIBLINGS;
                    SetWindowLong(window, GWL_STYLE, style);

                    // Strip extended borders
                    SetWindowLong(window, GWL_EXSTYLE, 0);

                    // Force windows manager to apply styles instantly
                    SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, 
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                }
                
                host.SetWindowCaption(window, _node.Title ?? "Dynamic Form");
                host.SetWindowResizable(window, false);

                // Register event handler
                var eventHandler = new DynamicUiEventHandler(window, host, (methodName, args) =>
                {
                    if (methodName == "submitForm")
                    {
                        if (args.Count > 0)
                        {
                            var dataVal = args[0];
                            if (dataVal.IsMap)
                            {
                                var mapItems = host.GetMapItems(ref dataVal);
                                foreach (var kvp in mapItems)
                                {
                                    var key = kvp.Key;
                                    var val = kvp.Value;

                                    if (val.IsBoolean)
                                    {
                                        SubmittedValues[key] = val.ToBoolean() == true;
                                    }
                                    else if (val.IsInteger)
                                    {
                                        SubmittedValues[key] = host.GetValueInt32(ref val);
                                    }
                                    else if (val.IsFloat)
                                    {
                                        SubmittedValues[key] = host.GetValueDouble(ref val);
                                    }
                                    else
                                    {
                                        SubmittedValues[key] = host.GetValueString(ref val);
                                    }
                                }
                            }
                        }

                        _tcs.TrySetResult(true);
                        host.CloseWindow(window);
                    }
                    else if (methodName == "cancelForm")
                    {
                        _tcs.TrySetResult(false);
                        host.CloseWindow(window);
                    }
                    else if (methodName == "getPrefilledValue")
                    {
                        if (args.Count > 0)
                        {
                            var keyVal = args[0];
                            var key = host.GetValueString(ref keyVal);
                            if (_prefilledValues.TryGetValue(key, out var prefill) && prefill != null)
                            {
                                return host.CreateValue(prefill);
                            }
                        }
                        return host.CreateNullValue();
                    }

                    return null;
                });

                host.AddWindowEventHandler(eventHandler);

                // Combine HTML, CSS, and JS into a single loadable document
                var combinedHtml = $@"<!DOCTYPE html>
<html window-frame=""none"">
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

                host.LoadHtml(combinedHtml, window);
                host.Process(); // Blocks until window is closed
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sciter Error: {ex}");
                _tcs.TrySetResult(false);
            }
            finally
            {
                // Ensure task completion
                _tcs.TrySetResult(false);
            }
        }
    }

    public class DynamicUiEventHandler : SciterEventHandler
    {
        private readonly Func<string, List<SciterValue>, SciterValue?> _handler;

        public DynamicUiEventHandler(IntPtr window, SciterAPIHost host, Func<string, List<SciterValue>, SciterValue?> handler)
            : base(window, host, SciterEventHandlerMode.Window)
        {
            _handler = handler;
        }

        public override (SciterValue? value, bool handled) ScriptMethodCall(string methodName, IEnumerable<SciterValue> args)
        {
            var result = _handler(methodName, args.ToList());
            if (result != null)
            {
                return (result, true);
            }
            return (null, true);
        }
    }
}
