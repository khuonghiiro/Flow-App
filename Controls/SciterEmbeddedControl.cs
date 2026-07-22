using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using EmptyFlow.SciterAPI;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Views.Overlays;
using FlowMy.Services.Interaction;
using FlowMy.Services.Workflow;
using FlowMy.Services.Rendering;

namespace FlowMy.Controls
{
    public class SciterEmbeddedControl : HwndHost
    {
        private readonly ISciterNode _node;
        private readonly IWorkflowEditorHost? _editorHost;
        public event EventHandler<System.Collections.Generic.Dictionary<string, object?>>? FormSubmitted;
        public event EventHandler? FormCancelled;
        private SciterAPIHost? _host;
        private IntPtr _sciterWindow = IntPtr.Zero;
        private IntPtr _rootSciterWindow = IntPtr.Zero;
        private double _currentZoom = 1.0;
        private readonly System.Collections.Generic.Dictionary<string, System.Windows.Threading.DispatcherTimer> _autoRefreshTimers = new();
        private System.Windows.Threading.DispatcherTimer? _actionPollTimer;

        private static SciterAPIHost? _globalHost;
        private static readonly object _hostLock = new object();

        private static SciterAPIHost GetHost()
        {
            if (_globalHost == null)
            {
                lock (_hostLock)
                {
                    if (_globalHost == null)
                    {
                        var sciterFolder = SciterWindowHelper.GetSciterFolder();
                        var host = new SciterAPIHost(sciterFolder);
                        host.EnableDebugMode();
                        host.EnableFeatures();
                        _globalHost = host;
                    }
                }
            }
            return _globalHost;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProcDelegate? _subclassWndProc;
        private IntPtr _oldWndProc = IntPtr.Zero;
        private IntPtr _oldSciterWndProc = IntPtr.Zero;

        private const int GWL_WNDPROC = -4;
        private const uint WM_NCCALCSIZE = 0x0083;
        private const uint WM_NCPAINT = 0x0085;
        private const uint WM_NCACTIVATE = 0x0086;
        private const uint WM_SETTEXT = 0x000C;

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_CHILD = 0x40000000;
        private const int WS_POPUP = unchecked((int)0x80000000);

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const string SCITER_ACTION_PREFIX = "__SCITER_ACTION__:";
        private bool _isProcessingAction = false;

        public SciterEmbeddedControl(ISciterNode node, IWorkflowEditorHost? editorHost = null)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _editorHost = editorHost;
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _host = GetHost();

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
                // Find top-level ancestor created by Sciter (below the desktop/null and not the WPF parent)
                IntPtr current = _sciterWindow;
                while (true)
                {
                    IntPtr parent = GetParent(current);
                    if (parent == IntPtr.Zero || parent == hwndParent.Handle)
                    {
                        break;
                    }
                    current = parent;
                }
                _rootSciterWindow = current;

                // Reparent the root Sciter window to WPF parent handle
                SetParent(_rootSciterWindow, hwndParent.Handle);

                const int WS_VISIBLE = 0x10000000;
                const int WS_CLIPCHILDREN = 0x02000000;
                const int WS_CLIPSIBLINGS = 0x04000000;
                int style = WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_CLIPSIBLINGS;

                // Strip titlebar/caption from BOTH the root window and the inner window just in case!
                SetWindowLong(_rootSciterWindow, GWL_STYLE, style);
                SetWindowLong(_rootSciterWindow, GWL_EXSTYLE, 0);
                SetWindowPos(_rootSciterWindow, IntPtr.Zero, 0, 0, 0, 0, 
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

                if (_rootSciterWindow != _sciterWindow)
                {
                    SetWindowLong(_sciterWindow, GWL_STYLE, style);
                    SetWindowLong(_sciterWindow, GWL_EXSTYLE, 0);
                    SetWindowPos(_sciterWindow, IntPtr.Zero, 0, 0, 0, 0, 
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                }

                try
                {
                    _subclassWndProc = new WndProcDelegate(SubclassWndProc);
                    var newWndProcPtr = Marshal.GetFunctionPointerForDelegate(_subclassWndProc);
                    _oldWndProc = SetWindowLongPtr(_rootSciterWindow, GWL_WNDPROC, newWndProcPtr);

                    if (_rootSciterWindow != _sciterWindow && _sciterWindow != IntPtr.Zero)
                    {
                        _oldSciterWndProc = SetWindowLongPtr(_sciterWindow, GWL_WNDPROC, newWndProcPtr);
                    }
                }
                catch { }
            }

            // Register interop event handler
            Func<string, List<SciterValue>, SciterValue?> handleScriptCall = (methodName, args) =>
            {
                if (methodName == "submitForm" || methodName == "hostSubmitAndClose" || methodName == "sciterSubmit" || methodName == "sciterSubmitAndClose" || methodName == "updateValue" || methodName == "hostSubmit" || methodName == "sciterUpdate")
                {
                    bool hasMapData = false;
                    if (args.Count > 0)
                    {
                        var dataVal = args[0];
                        if (dataVal.IsMap || dataVal.IsObject)
                        {
                            var mapItems = _host.GetMapItems(ref dataVal);
                            if (mapItems != null && mapItems.Count > 0)
                            {
                                hasMapData = true;
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

                                    if (_node.DynamicOutputs != null)
                                    {
                                        var dyn = _node.DynamicOutputs.FirstOrDefault(o =>
                                            string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                                        if (dyn != null)
                                            dyn.UserValueOverride = value?.ToString();
                                    }
                                }
                            }
                        }
                    }

                    if (!hasMapData)
                    {
                        var paramsCode = _node.ParamsCode ?? "";
                        UpdateOutputsFromDom(paramsCode, _node.ResolvedOutputs);
                    }

                    if (_editorHost != null)
                    {
                        _editorHost.RequestSyncDataPanels(immediate: false);

                        try
                        {
                            var vm = _editorHost.ViewModel;
                            if (vm != null)
                            {
                                var field = typeof(FlowMy.ViewModels.WorkflowEditorViewModel)
                                    .GetField("_executionVisualizer",
                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (field?.GetValue(vm) is FlowMy.Services.Workflow.IWorkflowExecutionVisualizer visualizer)
                                {
                                    visualizer.RefreshSavedOutputs(new[] { (WorkflowNode)_node });
                                }
                            }
                        }
                        catch (Exception exVis)
                        {
                            System.Diagnostics.Debug.WriteLine($"Sciter UI RefreshSavedOutputs error: {exVis.Message}");
                        }
                    }

                    if (methodName == "submitForm" || methodName == "hostSubmitAndClose" || methodName == "sciterSubmit" || methodName == "sciterSubmitAndClose")
                    {
                        FormSubmitted?.Invoke(this, new System.Collections.Generic.Dictionary<string, object?>(_node.ResolvedOutputs));
                    }

                    return _host.CreateValue(true);
                }
                else if (methodName == "sciterRunSingleNode" || methodName == "hostRunSingleNode" || methodName == "runSingleNode")
                {
                    if (args.Count > 0)
                    {
                        var dataVal = args[0];
                        if (dataVal.IsMap || dataVal.IsObject)
                        {
                            var mapItems = _host.GetMapItems(ref dataVal);
                            if (mapItems != null && mapItems.Count > 0)
                            {
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
                                    if (_node.DynamicOutputs != null)
                                    {
                                        var dyn = _node.DynamicOutputs.FirstOrDefault(o =>
                                            string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                                        if (dyn != null) dyn.UserValueOverride = value?.ToString();
                                    }
                                }
                            }
                        }
                    }

                    if (_editorHost != null)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                _editorHost.RequestRunSingleNode((WorkflowNode)_node);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Sciter] sciterRunSingleNode error: {ex.Message}");
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    return _host.CreateValue(true);
                }
                else if (methodName == "sciterRunFromNode" || methodName == "hostRunFromNode" || methodName == "runFromNode")
                {
                    if (args.Count > 0)
                    {
                        var dataVal = args[0];
                        if (dataVal.IsMap || dataVal.IsObject)
                        {
                            var mapItems = _host.GetMapItems(ref dataVal);
                            if (mapItems != null && mapItems.Count > 0)
                            {
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
                                    if (_node.DynamicOutputs != null)
                                    {
                                        var dyn = _node.DynamicOutputs.FirstOrDefault(o =>
                                            string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                                        if (dyn != null) dyn.UserValueOverride = value?.ToString();
                                    }
                                }
                            }
                        }
                    }

                    if (_editorHost != null)
                    {
                        Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            try
                            {
                                var vm = _editorHost.ViewModel;
                                if (vm != null)
                                {
                                    await vm.RunWorkflowFromNodeAsync((WorkflowNode)_node);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Sciter] sciterRunFromNode error: {ex.Message}");
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    return _host.CreateValue(true);
                }
                else if (methodName == "startWorkflow" || methodName == "hostStart" || methodName == "sciterStart" || methodName == "startTest")
                {
                    if (_editorHost != null)
                    {
                        Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            try
                            {
                                var vm = _editorHost.ViewModel;
                                if (vm != null)
                                {
                                    var vmType = vm.GetType();
                                    var startTestMethod = vmType.GetMethod("StartTest",
                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    if (startTestMethod != null)
                                    {
                                        var task = startTestMethod.Invoke(vm, null) as System.Threading.Tasks.Task;
                                        if (task != null) await task;
                                    }
                                    else
                                    {
                                        var commandProp = vmType.GetProperty("StartTestCommand",
                                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                        if (commandProp?.GetValue(vm) is System.Windows.Input.ICommand cmd && cmd.CanExecute(null))
                                        {
                                            cmd.Execute(null);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Sciter] startWorkflow error: {ex.Message}");
                            }
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                    return _host.CreateValue(true);
                }
                else if (methodName == "cancelForm" || methodName == "sciterCancel")
                {
                    FormCancelled?.Invoke(this, EventArgs.Empty);
                    return _host.CreateValue(true);
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

                        if (_node is DynamicUiNode dynNode)
                        {
                            var field = dynNode.Fields.FirstOrDefault(f => f.Key == key);
                            if (field != null)
                            {
                                return _host.CreateValue(field.DefaultValue ?? "");
                            }
                        }
                    }
                    return _host.CreateNullValue();
                }

                return _host.CreateNullValue();
            };

            var childEventHandler = new DynamicUiEventHandler(_sciterWindow, _host, handleScriptCall);
            _host.AddWindowEventHandler(childEventHandler);

            if (_rootSciterWindow != IntPtr.Zero && _rootSciterWindow != _sciterWindow)
            {
                var rootEventHandler = new DynamicUiEventHandler(_rootSciterWindow, _host, handleScriptCall);
                _host.AddWindowEventHandler(rootEventHandler);
            }

            // Defer loading content and starting timers to a background priority to prevent blocking the UI thread on node creation/dragging
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateContent();
                StartAutoRefreshTimers();
                StartActionPollTimer();
            }), System.Windows.Threading.DispatcherPriority.Background);

            return new HandleRef(this, _rootSciterWindow != IntPtr.Zero ? _rootSciterWindow : _sciterWindow);
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

                var jsSelector = selector.Replace("\\", "\\\\").Replace("'", "\\'");
                var script = $@"(function() {{
  try {{
    var el = document.querySelector('{jsSelector}') || (typeof document.$ === 'function' ? document.$('{jsSelector}') : null);
    if (!el) return '';
    var v = el.value !== undefined ? el.value : (el.textContent || '');
    return String(v);
  }} catch (e) {{
    return '';
  }}
}})()";

                var resultVal = _host.CreateNullValue();
                bool ok = _host.ExecuteWindowEval(_sciterWindow, script, out resultVal);
                if (ok)
                {
                    var valueStr = _host.GetValueString(ref resultVal);
                    var valStr = valueStr ?? "";
                    resolvedOutputs[key] = valStr;

                    if (_node.DynamicOutputs != null)
                    {
                        var dyn = _node.DynamicOutputs.FirstOrDefault(o =>
                            string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                        if (dyn != null)
                            dyn.UserValueOverride = valStr;
                    }
                }
            }

            if (_editorHost != null)
            {
                _editorHost.RequestSyncDataPanels(immediate: false);

                try
                {
                    var vm = _editorHost.ViewModel;
                    if (vm != null)
                    {
                        var field = typeof(FlowMy.ViewModels.WorkflowEditorViewModel)
                            .GetField("_executionVisualizer",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field?.GetValue(vm) is FlowMy.Services.Workflow.IWorkflowExecutionVisualizer visualizer)
                        {
                            visualizer.RefreshSavedOutputs(new[] { (WorkflowNode)_node });
                        }
                    }
                }
                catch (Exception exVis)
                {
                    System.Diagnostics.Debug.WriteLine($"Sciter UI RefreshSavedOutputs error: {exVis.Message}");
                }
            }
        }

        public void SubmitFormFromDom()
        {
            var paramsCode = _node.ParamsCode ?? "";
            UpdateOutputsFromDom(paramsCode, _node.ResolvedOutputs);
            if (_editorHost != null)
            {
                _editorHost.RequestSyncDataPanels(immediate: false);
            }
            FormSubmitted?.Invoke(this, new System.Collections.Generic.Dictionary<string, object?>(_node.ResolvedOutputs));
        }

        private string BuildRuntimeScript()
        {
            var inputValues = ResolveInputValues();
            var inputValuesJson = System.Text.Json.JsonSerializer.Serialize(inputValues);
            var paramsCode = _node.ParamsCode ?? "";
            var safeParamsCode = paramsCode.Replace("`", "\\`").Replace("$", "\\$");

            return $@"
<script>
(function() {{
  if (globalThis.__sciterHostReady) return;
  globalThis.__sciterHostReady = true;

  var liveVals = {inputValuesJson};
  globalThis.hostLive = {{ values: liveVals, _callbacks: [] }};
  globalThis.hostLive.on = function() {{
    var args = Array.prototype.slice.call(arguments);
    var cb = args[args.length - 1];
    if (typeof cb !== 'function') return;
    var keys = args.slice(0, -1);
    globalThis.hostLive._callbacks.push({{ keys: keys, cb: cb }});
    try {{
      if (keys.length === 1 && typeof keys[0] === 'string') {{
        var k = keys[0];
        var v = globalThis.hostLive.values[k];
        setTimeout(function() {{ try {{ cb(v); }} catch(e) {{}} }}, 0);
      }} else {{
        var vals0 = keys.length > 0
          ? keys.map(function(k) {{ return globalThis.hostLive.values[k]; }})
          : [globalThis.hostLive.values];
        setTimeout(function() {{ try {{ cb.apply(null, vals0); }} catch(e) {{}} }}, 0);
      }}
    }} catch(e) {{}}
  }};

  globalThis.hostLivePush = function(key, value) {{
    globalThis.hostLive.values[key] = value;
    var cbs = globalThis.hostLive._callbacks || [];
    for (var i = 0; i < cbs.length; i++) {{
      var item = cbs[i];
      try {{
        if (!item.keys || item.keys.length === 0) {{
          item.cb(globalThis.hostLive.values);
        }} else if (item.keys.length === 1 && item.keys[0] === key) {{
          item.cb(value);
        }} else if (item.keys.indexOf(key) >= 0) {{
          var vals = item.keys.map(function(k) {{ return globalThis.hostLive.values[k]; }});
          item.cb.apply(null, vals);
        }}
      }} catch(e) {{}}
    }}
  }};

  // Action queue: JS pushes actions here, C# polls via ExecuteWindowEval every 100ms
  if (!globalThis.__pendingActions) globalThis.__pendingActions = [];

  function sendHostAction(methodName, data) {{
    try {{
      globalThis.__pendingActions.push({{ action: methodName, data: data || {{}} }});
    }} catch(e) {{
      try {{ console.log('sendHostAction error: ' + e); }} catch(ex) {{}}
    }}
  }}

  function hostSubmit(data) {{
    if (!data || typeof data !== 'object') {{
      data = {{}};
      var paramsText = `{safeParamsCode}`;
      var lines = paramsText.split('\n');
      for (var i = 0; i < lines.length; i++) {{
        var line = lines[i].trim();
        if (!line || line.startsWith('//') || line.startsWith('#')) continue;
        var parts = line.includes(':') ? line.split(':') : line.split('=');
        if (parts.length < 2) continue;
        var key = parts[0].trim();
        var selector = parts[1].trim();
        try {{
          var el = document.querySelector(selector) || (typeof document.$ === 'function' ? document.$(selector) : null);
          if (el) {{
            if (typeof el.value !== 'undefined') {{
              data[key] = el.value;
            }} else if (el.textContent) {{
              data[key] = el.textContent;
            }}
          }}
        }} catch(e) {{}}
      }}
    }}
    sendHostAction('hostSubmit', data);
  }}

  function hostSubmitAndClose(data) {{
    if (!data || typeof data !== 'object') {{
      data = {{}};
      var paramsText = `{safeParamsCode}`;
      var lines = paramsText.split('\n');
      for (var i = 0; i < lines.length; i++) {{
        var line = lines[i].trim();
        if (!line || line.startsWith('//') || line.startsWith('#')) continue;
        var parts = line.includes(':') ? line.split(':') : line.split('=');
        if (parts.length < 2) continue;
        var key = parts[0].trim();
        var selector = parts[1].trim();
        try {{
          var el = document.querySelector(selector) || (typeof document.$ === 'function' ? document.$(selector) : null);
          if (el) {{
            if (typeof el.value !== 'undefined') {{
              data[key] = el.value;
            }} else if (el.textContent) {{
              data[key] = el.textContent;
            }}
          }}
        }} catch(e) {{}}
      }}
    }}
    sendHostAction('submitForm', data);
  }}

  function hostStart() {{
    sendHostAction('startWorkflow');
  }}

  function hostCancel() {{
    sendHostAction('cancelForm');
  }}

  function sciterRunSingleNode(data) {{
    if (!data || typeof data !== 'object') {{
      data = {{}};
      var paramsText = `{safeParamsCode}`;
      var lines = paramsText.split('\n');
      for (var i = 0; i < lines.length; i++) {{
        var line = lines[i].trim();
        if (!line || line.startsWith('//') || line.startsWith('#')) continue;
        var parts = line.includes(':') ? line.split(':') : line.split('=');
        if (parts.length < 2) continue;
        var key = parts[0].trim();
        var selector = parts[1].trim();
        try {{
          var el = document.querySelector(selector) || (typeof document.$ === 'function' ? document.$(selector) : null);
          if (el) {{
            if (typeof el.value !== 'undefined') {{
              data[key] = el.value;
            }} else if (el.textContent) {{
              data[key] = el.textContent;
            }}
          }}
        }} catch(e) {{}}
      }}
    }}
    sendHostAction('sciterRunSingleNode', data);
  }}

  function sciterRunFromNode(data) {{
    if (!data || typeof data !== 'object') {{
      data = {{}};
      var paramsText = `{safeParamsCode}`;
      var lines = paramsText.split('\n');
      for (var i = 0; i < lines.length; i++) {{
        var line = lines[i].trim();
        if (!line || line.startsWith('//') || line.startsWith('#')) continue;
        var parts = line.includes(':') ? line.split(':') : line.split('=');
        if (parts.length < 2) continue;
        var key = parts[0].trim();
        var selector = parts[1].trim();
        try {{
          var el = document.querySelector(selector) || (typeof document.$ === 'function' ? document.$(selector) : null);
          if (el) {{
            if (typeof el.value !== 'undefined') {{
              data[key] = el.value;
            }} else if (el.textContent) {{
              data[key] = el.textContent;
            }}
          }}
        }} catch(e) {{}}
      }}
    }}
    sendHostAction('sciterRunFromNode', data);
  }}

  function hostResolvePath(localPath, requestId) {{
    sendHostAction('hostResolvePath', {{ localPath: localPath, requestId: requestId }});
  }}

  function hostCurl(rawCurl, fileName, key) {{
    sendHostAction('hostCurl', {{ rawCurl: rawCurl, fileName: fileName, key: key }});
  }}

  function hostPickImages(requestId) {{
    sendHostAction('hostPickImages', {{ requestId: requestId }});
  }}

  globalThis.hostSubmit = hostSubmit;
  globalThis.hostSubmitAndClose = hostSubmitAndClose;
  globalThis.hostStart = hostStart;
  globalThis.hostCancel = hostCancel;
  globalThis.hostResolvePath = hostResolvePath;
  globalThis.hostCurl = hostCurl;
  globalThis.hostPickImages = hostPickImages;

  globalThis.sciterSubmit = hostSubmit;
  globalThis.sciterSubmitAndClose = hostSubmitAndClose;
  globalThis.sciterUpdate = hostSubmit;
  globalThis.sciterStart = hostStart;
  globalThis.sciterCancel = hostCancel;
  globalThis.sciterRunSingleNode = sciterRunSingleNode;
  globalThis.sciterRunFromNode = sciterRunFromNode;

  if (typeof window !== 'undefined') {{
    window.hostLive = globalThis.hostLive;
    window.hostLivePush = globalThis.hostLivePush;
    window.hostSubmit = hostSubmit;
    window.hostSubmitAndClose = hostSubmitAndClose;
    window.hostStart = hostStart;
    window.hostCancel = hostCancel;
    window.hostResolvePath = hostResolvePath;
    window.hostCurl = hostCurl;
    window.hostPickImages = hostPickImages;

    window.sciterSubmit = hostSubmit;
    window.sciterSubmitAndClose = hostSubmitAndClose;
    window.sciterUpdate = hostSubmit;
    window.sciterStart = hostStart;
    window.sciterCancel = hostCancel;
    window.sciterRunSingleNode = sciterRunSingleNode;
    window.sciterRunFromNode = sciterRunFromNode;
  }}
}})();
</script>";
        }

        private string? _customLoadedHtml = null;

        public void UpdateContent()
        {
            if (_host == null || _sciterWindow == IntPtr.Zero) return;

            var runtimeScript = BuildRuntimeScript();

            if (_customLoadedHtml != null)
            {
                var htmlToLoad = _customLoadedHtml;
                if (!htmlToLoad.Contains("__sciterHostReady", StringComparison.OrdinalIgnoreCase))
                {
                    if (htmlToLoad.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                        htmlToLoad = htmlToLoad.Replace("</head>", runtimeScript + "\n</head>", StringComparison.OrdinalIgnoreCase);
                    else if (htmlToLoad.Contains("<body>", StringComparison.OrdinalIgnoreCase))
                        htmlToLoad = htmlToLoad.Replace("<body>", "<body>\n" + runtimeScript, StringComparison.OrdinalIgnoreCase);
                    else
                        htmlToLoad = runtimeScript + "\n" + htmlToLoad;
                }
                _host.LoadHtml(htmlToLoad, _sciterWindow);
                var rVal = _host.CreateNullValue();
                _host.ExecuteWindowEval(_sciterWindow, "document.documentElement.setAttribute('window-frame', 'none');", out rVal);
                SetZoom(_currentZoom);
                return;
            }

            var inputValues = ResolveInputValues();
            var html = ReplaceVariables(_node.HtmlCode ?? string.Empty, inputValues);
            var css = ReplaceVariables(_node.CssCode ?? string.Empty, inputValues);
            var js = ReplaceVariables(_node.JsCode ?? string.Empty, inputValues);

            var combinedHtml = $@"<!DOCTYPE html>
<html window-frame=""none"">
<head>
    <meta charset=""utf-8"">
    {runtimeScript}
    <style>
        html, body {{
            margin: 0;
            padding: 0;
            width: 100%;
            height: 100%;
            background-color: #0f172a;
            color: #f8fafc;
            font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            overflow: auto;
            box-sizing: border-box;
        }}
        {css}
    </style>
</head>
<body>
    {html}
    <script>
        {js}
    </script>
</body>
</html>";

            _host.LoadHtml(combinedHtml, _sciterWindow);
            
            var resultVal = _host.CreateNullValue();
            _host.ExecuteWindowEval(_sciterWindow, "document.documentElement.setAttribute('window-frame', 'none');", out resultVal);

            SetZoom(_currentZoom);
        }

        public void LoadHtml(string htmlContent)
        {
            var runtimeScript = BuildRuntimeScript();
            if (!htmlContent.Contains("__sciterHostReady", StringComparison.OrdinalIgnoreCase))
            {
                if (htmlContent.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                    htmlContent = htmlContent.Replace("</head>", runtimeScript + "\n</head>", StringComparison.OrdinalIgnoreCase);
                else if (htmlContent.Contains("<body>", StringComparison.OrdinalIgnoreCase))
                    htmlContent = htmlContent.Replace("<body>", "<body>\n" + runtimeScript, StringComparison.OrdinalIgnoreCase);
                else
                    htmlContent = runtimeScript + "\n" + htmlContent;
            }

            _customLoadedHtml = htmlContent;
            if (_host == null || _sciterWindow == IntPtr.Zero) return;
            _host.LoadHtml(htmlContent, _sciterWindow);
            
            var resultVal = _host.CreateNullValue();
            _host.ExecuteWindowEval(_sciterWindow, "document.documentElement.setAttribute('window-frame', 'none');", out resultVal);

            SetZoom(_currentZoom);
        }

        public void SetZoom(double zoom)
        {
            _currentZoom = zoom;
            if (_host == null || _sciterWindow == IntPtr.Zero) return;

            string zStr = zoom.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string script = $@"(function() {{
                try {{
                    document.body.style.zoom = {zStr};
                    if (!document.body.style.zoom) {{
                        document.body.style.transform = 'scale({zStr})';
                        document.body.style.transformOrigin = 'top left';
                    }}
                }} catch(e) {{}}
            }})();";
            
            var resultVal = _host.CreateNullValue();
            _host.ExecuteWindowEval(_sciterWindow, script, out resultVal);
        }

        public void ForceMoveWindow()
        {
            try
            {
                var presentationSource = PresentationSource.FromVisual(this);
                if (presentationSource == null || presentationSource.RootVisual == null) return;

                var transform = this.TransformToAncestor(presentationSource.RootVisual);
                var rect = new Rect(0, 0, this.ActualWidth, this.ActualHeight);
                var transformedRect = transform.TransformBounds(rect);

                var hwnd = this.Handle;
                if (hwnd != IntPtr.Zero)
                {
                    const uint SWP_NOZORDER = 0x0004;
                    const uint SWP_NOACTIVATE = 0x0010;
                    
                    int x = (int)transformedRect.Left;
                    int y = (int)transformedRect.Top;
                    int cx = (int)transformedRect.Width;
                    int cy = (int)transformedRect.Height;

                    SetWindowPos(hwnd, IntPtr.Zero, x, y, cx, cy, SWP_NOZORDER | SWP_NOACTIVATE);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ForceMoveWindow error: {ex.Message}");
            }
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            StopActionPollTimer();
            StopAutoRefreshTimers();

            if (_oldWndProc != IntPtr.Zero && _rootSciterWindow != IntPtr.Zero)
            {
                try
                {
                    SetWindowLongPtr(_rootSciterWindow, GWL_WNDPROC, _oldWndProc);
                }
                catch { }
                _oldWndProc = IntPtr.Zero;
            }

            if (_oldSciterWndProc != IntPtr.Zero && _sciterWindow != IntPtr.Zero)
            {
                try
                {
                    SetWindowLongPtr(_sciterWindow, GWL_WNDPROC, _oldSciterWndProc);
                }
                catch { }
                _oldSciterWndProc = IntPtr.Zero;
            }

            if (_host != null && _sciterWindow != IntPtr.Zero)
            {
                try
                {
                    _host.CloseWindow(_sciterWindow);
                }
                catch { }
                _sciterWindow = IntPtr.Zero;
                _rootSciterWindow = IntPtr.Zero;
            }
        }

        public void RestartAutoRefreshTimers()
        {
            StartAutoRefreshTimers();
        }

        private void StopAutoRefreshTimers()
        {
            foreach (var t in _autoRefreshTimers.Values) t.Stop();
            _autoRefreshTimers.Clear();
        }

        /// <summary>
        /// Start a DispatcherTimer that polls the JS __pendingActions queue every 100ms.
        /// This is the reliable JS→C# communication bridge for embedded Sciter child windows
        /// where Window.this.xcall() does not work (no Sciter message loop / host.Process()).
        /// </summary>
        private void StartActionPollTimer()
        {
            StopActionPollTimer();
            _actionPollTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _actionPollTimer.Tick += ActionPollTimer_Tick;
            _actionPollTimer.Start();
        }

        private void StopActionPollTimer()
        {
            if (_actionPollTimer != null)
            {
                _actionPollTimer.Stop();
                _actionPollTimer.Tick -= ActionPollTimer_Tick;
                _actionPollTimer = null;
            }
        }

        private void ActionPollTimer_Tick(object? sender, EventArgs e)
        {
            if (_host == null || _sciterWindow == IntPtr.Zero)
            {
                StopActionPollTimer();
                return;
            }

            try
            {
                // Read and atomically clear the pending actions queue from JS
                var script = @"(function() {
                    var q = globalThis.__pendingActions;
                    if (!q || q.length === 0) return '';
                    globalThis.__pendingActions = [];
                    return JSON.stringify(q);
                })();";

                var resultVal = _host.CreateNullValue();
                bool ok = _host.ExecuteWindowEval(_sciterWindow, script, out resultVal);

                if (ok)
                {
                    var jsonArray = _host.GetValueString(ref resultVal);
                    if (!string.IsNullOrEmpty(jsonArray))
                    {
                        System.Diagnostics.Debug.WriteLine($"[Sciter Poll] Got actions: {jsonArray}");

                        // Parse the array of actions
                        using var doc = System.Text.Json.JsonDocument.Parse(jsonArray);
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var item in doc.RootElement.EnumerateArray())
                            {
                                try
                                {
                                    ProcessSciterAction(item.GetRawText());
                                }
                                catch (Exception exAction)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[Sciter Poll] Action error: {exAction.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Sciter Poll] Error: {ex.Message}");
            }
        }

        private void StartAutoRefreshTimers()
        {
            StopAutoRefreshTimers();
            if (_host == null || _sciterWindow == IntPtr.Zero) return;

            var mappings = _node.InputMappings ?? new System.Collections.Generic.List<CodeInputMapping>();
            foreach (var m in mappings)
            {
                if (!m.AutoRefreshEnabled) continue;
                var intervalMs = m.AutoRefreshUnit switch
                {
                    "s" => m.AutoRefreshInterval * 1000,
                    "min" => m.AutoRefreshInterval * 60000,
                    _ => m.AutoRefreshInterval // "ms"
                };
                intervalMs = Math.Max(100, intervalMs); // min 100ms
                var mapping = m; // capture
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
                timer.Tick += (s2, _) =>
                {
                    if (_host == null || _sciterWindow == IntPtr.Zero)
                    {
                        (s2 as System.Windows.Threading.DispatcherTimer)?.Stop();
                        return;
                    }
                    try
                    {
                        var value = ResolveSingleInputValue(mapping);
                        var jsKey = System.Text.Json.JsonSerializer.Serialize(mapping.EffectiveInputKey);
                        var jsVal = System.Text.Json.JsonSerializer.Serialize(value);
                        
                        string script = $@"if(typeof window.hostLivePush==='function') window.hostLivePush({jsKey},{jsVal});";
                        var resultVal = _host.CreateNullValue();
                        _host.ExecuteWindowEval(_sciterWindow, script, out resultVal);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Sciter auto-refresh push error: {ex.Message}");
                    }
                };
                timer.Start();
                _autoRefreshTimers[m.EffectiveInputKey] = timer;
            }
        }

        private string ResolveSingleInputValue(CodeInputMapping mapping)
        {
            if (_editorHost?.ViewModel == null) return string.Empty;
            var allNodes = _editorHost.ViewModel.Nodes;
            var connections = _editorHost.ViewModel.Connections;
            WorkflowNode? sourceNode = null;
            if (!string.IsNullOrWhiteSpace(mapping.SourceNodeId))
            {
                sourceNode = allNodes?.FirstOrDefault(n =>
                    string.Equals(n.Id, mapping.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                if (sourceNode == null && connections != null)
                {
                    var conn = connections.FirstOrDefault(c =>
                        c.ToNode == (WorkflowNode)_node && c.FromNode != null &&
                        string.Equals(c.FromNode.Id, mapping.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                    sourceNode = conn?.FromNode;
                }
            }
            if (sourceNode == null) return string.Empty;
            var key = string.IsNullOrWhiteSpace(mapping.SourceOutputKey) ? null : mapping.SourceOutputKey.Trim();
            if (string.IsNullOrWhiteSpace(key) && sourceNode.DynamicOutputs?.Count > 0)
                key = sourceNode.DynamicOutputs[0].Key ?? "output";
            var value = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, key ?? "output");
            if (string.Equals(value?.Trim(), "—", StringComparison.OrdinalIgnoreCase)) value = string.Empty;
            return value ?? string.Empty;
        }

        private System.Collections.Generic.Dictionary<string, string> ResolveInputValues()
        {
            var result = new System.Collections.Generic.Dictionary<string, string>();

            if (_editorHost?.ViewModel == null) return result;

            var mappings = _node.InputMappings ?? new System.Collections.Generic.List<CodeInputMapping>();
            if (mappings.Count == 0) return result;

            var connections = _editorHost.ViewModel.Connections;
            var allNodes = _editorHost.ViewModel.Nodes;

            foreach (var m in mappings)
            {
                WorkflowNode? sourceNode = null;

                if (!string.IsNullOrWhiteSpace(m.SourceNodeId))
                {
                    sourceNode = allNodes?.FirstOrDefault(n =>
                        string.Equals(n.Id, m.SourceNodeId, StringComparison.OrdinalIgnoreCase));

                    if (sourceNode == null && connections != null)
                    {
                        var conn = connections.FirstOrDefault(c =>
                            c.ToNode == (WorkflowNode)_node && c.FromNode != null &&
                            string.Equals(c.FromNode.Id, m.SourceNodeId, StringComparison.OrdinalIgnoreCase));
                        sourceNode = conn?.FromNode;
                    }
                }

                string inputValue = string.Empty;
                if (sourceNode != null)
                {
                    var key = string.IsNullOrWhiteSpace(m.SourceOutputKey) ? null : m.SourceOutputKey.Trim();
                    if (string.IsNullOrWhiteSpace(key) && sourceNode.DynamicOutputs != null && sourceNode.DynamicOutputs.Count > 0)
                        key = sourceNode.DynamicOutputs[0].Key ?? "output";
                    inputValue = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, key ?? "output");
                    if (string.Equals(inputValue?.Trim(), "—", StringComparison.OrdinalIgnoreCase))
                        inputValue = string.Empty;
                }

                var varName = m.EffectiveInputKey;
                if (string.IsNullOrWhiteSpace(varName)) varName = "input";

                result[varName] = inputValue ?? string.Empty;
            }

            return result;
        }

        private string ReplaceVariables(string text, System.Collections.Generic.Dictionary<string, string> variableValues)
        {
            if (string.IsNullOrEmpty(text) || variableValues.Count == 0) return text;

            var regex = new System.Text.RegularExpressions.Regex(@"\{([^}]+)\}");
            return regex.Replace(text, match =>
            {
                var variableName = match.Groups[1].Value.Trim();
                if (variableValues.TryGetValue(variableName, out var value) && value != null)
                {
                    return value;
                }
                return match.Value;
            });
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            IntPtr targetHwnd = _rootSciterWindow != IntPtr.Zero ? _rootSciterWindow : _sciterWindow;
            if (_host != null && targetHwnd != IntPtr.Zero)
            {
                int width = (int)sizeInfo.NewSize.Width;
                int height = (int)sizeInfo.NewSize.Height;
                MoveWindow(targetHwnd, 0, 0, width, height, true);
            }
        }

        private IntPtr SubclassWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            const uint WM_SHOWWINDOW = 0x0018;
            const uint WM_STYLECHANGING = 0x007C;

            if (msg == WM_SHOWWINDOW || msg == WM_STYLECHANGING)
            {
                try
                {
                    const int WS_CHILD_STYLE = 0x40000000;
                    const int WS_VISIBLE_STYLE = 0x10000000;
                    const int WS_CLIPCHILDREN_STYLE = 0x02000000;
                    const int WS_CLIPSIBLINGS_STYLE = 0x04000000;
                    int targetStyle = WS_CHILD_STYLE | WS_VISIBLE_STYLE | WS_CLIPCHILDREN_STYLE | WS_CLIPSIBLINGS_STYLE;

                    var curStyle = GetWindowLong(hWnd, GWL_STYLE);
                    if ((curStyle & WS_CHILD_STYLE) == 0 || (curStyle & 0x00C00000) != 0) // WS_CAPTION = 0x00C00000
                    {
                        SetWindowLong(hWnd, GWL_STYLE, targetStyle);
                        SetWindowLong(hWnd, GWL_EXSTYLE, 0);
                    }
                }
                catch { }
            }


            if (msg == WM_NCCALCSIZE)
            {
                // Ensure client area covers the entire window to strip titlebar/caption/borders
                return IntPtr.Zero;
            }
            if (msg == WM_NCPAINT)
            {
                // Prevent drawing the default window frame/caption
                return IntPtr.Zero;
            }
            if (msg == WM_NCACTIVATE)
            {
                var oldWndProc = (hWnd == _sciterWindow && _oldSciterWndProc != IntPtr.Zero) ? _oldSciterWndProc : _oldWndProc;
                CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
                return new IntPtr(1); // Return TRUE to prevent default caption drawing
            }
            
            var targetOldWndProc = (hWnd == _sciterWindow && _oldSciterWndProc != IntPtr.Zero) ? _oldSciterWndProc : _oldWndProc;
            return CallWindowProc(targetOldWndProc, hWnd, msg, wParam, lParam);
        }

        /// <summary>
        /// Process a JS→C# action received via polling bridge (DispatcherTimer + ExecuteWindowEval).
        /// Parses JSON payload and dispatches to the appropriate C# handler.
        /// </summary>
        private void ProcessSciterAction(string json)
        {
            if (_host == null || _sciterWindow == IntPtr.Zero) return;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var action = root.GetProperty("action").GetString() ?? "";
                var dataElement = root.TryGetProperty("data", out var d) ? d : default;

                System.Diagnostics.Debug.WriteLine($"[Sciter Action] Received: {action}");

                // Extract key-value pairs from "data" JSON object into a dictionary
                var formData = new Dictionary<string, object?>();
                if (dataElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in dataElement.EnumerateObject())
                    {
                        object? val = prop.Value.ValueKind switch
                        {
                            System.Text.Json.JsonValueKind.True => true,
                            System.Text.Json.JsonValueKind.False => false,
                            System.Text.Json.JsonValueKind.Number => prop.Value.TryGetInt32(out var iv) ? (object)iv : prop.Value.GetDouble(),
                            System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                            _ => prop.Value.GetRawText()
                        };
                        formData[prop.Name] = val;
                    }
                }

                // --- Handle submit/update actions ---
                if (action == "submitForm" || action == "hostSubmitAndClose" || action == "sciterSubmit" || action == "sciterSubmitAndClose" ||
                    action == "updateValue" || action == "hostSubmit" || action == "sciterUpdate")
                {
                    // Write form data to node outputs
                    if (formData.Count > 0)
                    {
                        foreach (var kvp in formData)
                        {
                            _node.ResolvedOutputs[kvp.Key] = kvp.Value;

                            if (_node.DynamicOutputs != null)
                            {
                                var dyn = _node.DynamicOutputs.FirstOrDefault(o =>
                                    string.Equals(o.Key, kvp.Key, StringComparison.OrdinalIgnoreCase));
                                if (dyn != null)
                                    dyn.UserValueOverride = kvp.Value?.ToString();
                            }
                        }
                    }
                    else
                    {
                        // Fallback: read from DOM using ParamsCode selectors
                        var paramsCode = _node.ParamsCode ?? "";
                        UpdateOutputsFromDom(paramsCode, _node.ResolvedOutputs);
                    }

                    if (_editorHost != null)
                    {
                        _editorHost.RequestSyncDataPanels(immediate: false);

                        try
                        {
                            var vm = _editorHost.ViewModel;
                            if (vm != null)
                            {
                                var field = typeof(FlowMy.ViewModels.WorkflowEditorViewModel)
                                    .GetField("_executionVisualizer",
                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (field?.GetValue(vm) is FlowMy.Services.Workflow.IWorkflowExecutionVisualizer visualizer)
                                {
                                    visualizer.RefreshSavedOutputs(new[] { (WorkflowNode)_node });
                                }
                            }
                        }
                        catch (Exception exVis)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Sciter Action] RefreshSavedOutputs error: {exVis.Message}");
                        }
                    }

                    if (action == "submitForm" || action == "hostSubmitAndClose" || action == "sciterSubmit" || action == "sciterSubmitAndClose")
                    {
                        FormSubmitted?.Invoke(this, new Dictionary<string, object?>(_node.ResolvedOutputs));
                    }
                }
                // --- Handle Run Single Node ---
                else if (action == "sciterRunSingleNode" || action == "hostRunSingleNode" || action == "runSingleNode")
                {
                    // Write form data to outputs before running
                    foreach (var kvp in formData)
                    {
                        _node.ResolvedOutputs[kvp.Key] = kvp.Value;
                        if (_node.DynamicOutputs != null)
                        {
                            var dyn = _node.DynamicOutputs.FirstOrDefault(o =>
                                string.Equals(o.Key, kvp.Key, StringComparison.OrdinalIgnoreCase));
                            if (dyn != null) dyn.UserValueOverride = kvp.Value?.ToString();
                        }
                    }

                    if (_editorHost != null)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                _editorHost.RequestRunSingleNode((WorkflowNode)_node);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Sciter Action] sciterRunSingleNode error: {ex.Message}");
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
                // --- Handle Run From Node ---
                else if (action == "sciterRunFromNode" || action == "hostRunFromNode" || action == "runFromNode")
                {
                    foreach (var kvp in formData)
                    {
                        _node.ResolvedOutputs[kvp.Key] = kvp.Value;
                        if (_node.DynamicOutputs != null)
                        {
                            var dyn = _node.DynamicOutputs.FirstOrDefault(o =>
                                string.Equals(o.Key, kvp.Key, StringComparison.OrdinalIgnoreCase));
                            if (dyn != null) dyn.UserValueOverride = kvp.Value?.ToString();
                        }
                    }

                    if (_editorHost != null)
                    {
                        Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            try
                            {
                                var vm = _editorHost.ViewModel;
                                if (vm != null)
                                {
                                    await vm.RunWorkflowFromNodeAsync((WorkflowNode)_node);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Sciter Action] sciterRunFromNode error: {ex.Message}");
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
                // --- Handle Start Workflow ---
                else if (action == "startWorkflow" || action == "hostStart" || action == "sciterStart" || action == "startTest")
                {
                    if (_editorHost != null)
                    {
                        Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            try
                            {
                                var vm = _editorHost.ViewModel;
                                if (vm != null)
                                {
                                    var vmType = vm.GetType();
                                    var startTestMethod = vmType.GetMethod("StartTest",
                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    if (startTestMethod != null)
                                    {
                                        var task = startTestMethod.Invoke(vm, null) as System.Threading.Tasks.Task;
                                        if (task != null) await task;
                                    }
                                    else
                                    {
                                        var commandProp = vmType.GetProperty("StartTestCommand",
                                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                        if (commandProp?.GetValue(vm) is System.Windows.Input.ICommand cmd && cmd.CanExecute(null))
                                        {
                                            cmd.Execute(null);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Sciter Action] startWorkflow error: {ex.Message}");
                            }
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                }
                // --- Handle Cancel ---
                else if (action == "cancelForm" || action == "sciterCancel" || action == "hostCancel")
                {
                    FormCancelled?.Invoke(this, EventArgs.Empty);
                }
                // --- Handle getPrefilledValue ---
                else if (action == "getPrefilledValue")
                {
                    // This is typically called from JS during load, not via button click
                    // For WM_SETTEXT bridge, we can't return values. Use ExecuteWindowEval instead.
                    System.Diagnostics.Debug.WriteLine("[Sciter Action] getPrefilledValue called via WM_SETTEXT bridge - not supported in this mode");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Sciter Action] Unknown action: {action}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Sciter Action] JSON parse error: {ex.Message}, json={json}");
            }
        }
    }
}
