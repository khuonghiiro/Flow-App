using FlowMy.Services;
using FlowMy.Services.Interaction;
using FlowMy.Services.Workflow;
using FlowMy.Views;
using FlowMy.Views.Overlays;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;

namespace FlowMy.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        [ObservableProperty] private ObservableCollection<WorkflowWidgetGroupItem> widgetGroups = new();
        [ObservableProperty] private ObservableCollection<WidgetShortcutItem> widgetShortcuts = new();
        [ObservableProperty] private int widgetEnabledCount;
        [ObservableProperty] private int workflowCount;
        [ObservableProperty] private bool hasWidgetShortcuts;
        [ObservableProperty] private string lastConfiguredWorkflowName = string.Empty;
        [ObservableProperty] private string lastConfiguredNodeId = string.Empty;
        [ObservableProperty] private bool hasLastConfiguredWidget;
        [ObservableProperty] private bool isConfigureLastWidgetLoading;
        private readonly Dictionary<string, WorkflowEditorWindow> _headlessWorkflowWindows = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<WorkflowEditorWindow> _isRehidingHeadlessWindow = new();

        public MainViewModel()
        {
            FloatingWidgetManager.Instance.WidgetOpened += OnWidgetOpened;
            FloatingWidgetManager.Instance.WidgetClosed += OnWidgetClosed;
            RefreshWidgetShortcuts();
        }

        private void OnWidgetOpened(object? sender, string nodeId)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new System.Action(() =>
            {
                var match = WidgetShortcuts.FirstOrDefault(w =>
                    string.Equals(w.NodeId, nodeId, System.StringComparison.OrdinalIgnoreCase));
                if (match != null) match.IsWidgetOpen = true;
            }));
        }

        private void OnWidgetClosed(object? sender, string nodeId)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new System.Action(() =>
            {
                var match = WidgetShortcuts.FirstOrDefault(w =>
                    string.Equals(w.NodeId, nodeId, System.StringComparison.OrdinalIgnoreCase));
                if (match != null) match.IsWidgetOpen = false;
            }));
        }

        /// <summary>
        /// Quét tất cả workflow JSON trong thư mục mặc định, tìm các node có
        /// FloatingWidget.IsEnabled = true và build danh sách shortcut hiển thị
        /// (dạng flat + dạng group theo workflow).
        /// </summary>
        [RelayCommand]
        public void RefreshWidgetShortcuts()
        {
            WidgetShortcuts.Clear();
            WidgetGroups.Clear();
            try
            {
                var scan = WidgetShortcutScanner.Scan();
                var ordered = scan.Items
                    .OrderBy(i => i.WorkflowName)
                    .ThenBy(i => i.WidgetName)
                    .ToList();

                foreach (var item in ordered)
                {
                    WidgetShortcuts.Add(item);
                }
                WidgetEnabledCount = scan.EnabledCount;

                foreach (var group in ordered.GroupBy(i => i.WorkflowName))
                {
                    var g = new WorkflowWidgetGroupItem { WorkflowName = group.Key };
                    foreach (var w in group) g.Widgets.Add(w);
                    WidgetGroups.Add(g);
                }
                WorkflowCount = WidgetGroups.Count;
            }
            catch
            {
                WidgetEnabledCount = 0;
                WorkflowCount = 0;
            }

            // Đồng bộ trạng thái widget đang mở để disable nút 📌 tương ứng.
            var activeWidgetIds = FloatingWidgetManager.Instance.GetActiveWidgetNodeIds()
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);
            foreach (var item in WidgetShortcuts)
            {
                item.IsWidgetOpen = activeWidgetIds.Contains(item.NodeId);
            }

            HasWidgetShortcuts = WidgetShortcuts.Count > 0;

            // Validate shortcut gần nhất còn tồn tại sau khi refresh.
            if (!string.IsNullOrWhiteSpace(LastConfiguredWorkflowName) &&
                !string.IsNullOrWhiteSpace(LastConfiguredNodeId))
            {
                HasLastConfiguredWidget = WidgetShortcuts.Any(w =>
                    string.Equals(w.WorkflowName, LastConfiguredWorkflowName, System.StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(w.NodeId, LastConfiguredNodeId, System.StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                HasLastConfiguredWidget = false;
            }
        }

        /// <summary>
        /// Command để mở WorkflowEditorWindow (không load workflow nào cụ thể).
        /// </summary>
        [RelayCommand]
        private void OpenWorkflowEditor()
        {
            OpenWorkflowEditorInternal(
                workflowNameToLoad: null,
                widgetNodeIds: null,
                headless: false);
        }

        /// <summary>
        /// Mở editor cho cả workflow (mở tất cả widget đã cấu hình trong workflow đó).
        /// </summary>
        [RelayCommand]
        private void OpenWorkflow(WorkflowWidgetGroupItem? group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.WorkflowName)) return;
            var ids = group.Widgets.Select(w => w.NodeId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            OpenWorkflowEditorInternal(group.WorkflowName, ids, headless: false);
        }

        /// <summary>
        /// Kích hoạt 1 widget shortcut: mở editor + bật widget đó (hiển thị editor canvas).
        /// </summary>
        [RelayCommand]
        private void LaunchWidget(WidgetShortcutItem? item)
        {
            if (item == null) return;
            OpenWorkflowEditorInternal(
                item.WorkflowName,
                new List<string> { item.NodeId },
                headless: false);
        }

        /// <summary>
        /// Chỉ mở widget (headless): workflow được load nhưng cửa sổ editor bị ẩn.
        /// Giảm tải render canvas/nodes/connections. Widget vẫn hoạt động,
        /// khi đóng widget cuối → đóng luôn workflow editor.
        /// </summary>
        [RelayCommand]
        private async Task LaunchWidgetHeadless(WidgetShortcutItem? item)
        {
            if (item == null) return;
            if (item.IsLaunchingHeadless) return;

            item.IsLaunchingHeadless = true;
            try
            {
                // Trả control về UI loop để icon loading kịp render, không tạo delay cứng.
                await Task.Yield();
                OpenWorkflowEditorInternal(
                    item.WorkflowName,
                    new List<string> { item.NodeId },
                    headless: true);
                item.IsWidgetOpen = true;
            }
            finally
            {
                item.IsLaunchingHeadless = false;
            }
        }

        /// <summary>
        /// Mở tất cả widget của 1 workflow trong chế độ headless (editor ẩn).
        /// </summary>
        [RelayCommand]
        private void LaunchAllWidgetsHeadless(WorkflowWidgetGroupItem? group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.WorkflowName)) return;
            var ids = group.Widgets.Select(w => w.NodeId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            if (ids.Count == 0) return;
            OpenWorkflowEditorInternal(group.WorkflowName, ids, headless: true);
        }

        [RelayCommand]
        private void ReopenHeadlessWorkflow(WidgetShortcutItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.WorkflowName)) return;
            if (!_headlessWorkflowWindows.TryGetValue(item.WorkflowName, out var workflowWindow)) return;

            workflowWindow.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                try
                {
                    // Hiện window trước để user thấy ngay; phần restore canvas chạy deferred giảm cảm giác "lag".
                    workflowWindow.Owner = null;
                    workflowWindow.ShowInTaskbar = true;
                    workflowWindow.Visibility = Visibility.Visible;
                    workflowWindow.WindowState = WindowState.Maximized;
                    if (workflowWindow.Visibility != Visibility.Visible)
                    {
                        workflowWindow.Show();
                    }
                    workflowWindow.Activate();
                    workflowWindow.Focus();

                    workflowWindow.Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        try
                        {
                            workflowWindow.DisableHeadlessCanvasOptimizationForDebug();
                        }
                        catch { }
                    }), System.Windows.Threading.DispatcherPriority.Background);

                    // Double-activate nhẹ để chắc chắn window nổi lên foreground trên một số máy.
                    workflowWindow.Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        try
                        {
                            workflowWindow.WindowState = WindowState.Maximized;
                            workflowWindow.Activate();
                            workflowWindow.Focus();
                        }
                        catch { }
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
                catch { }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// Mở trực tiếp màn cấu hình widget từ MainWindow cho 1 shortcut cụ thể.
        /// </summary>
        [RelayCommand]
        private async Task ConfigureWidget(WidgetShortcutItem? item)
        {
            if (item == null) return;
            if (item.IsConfiguring) return;

            item.IsConfiguring = true;
            LastConfiguredWorkflowName = item.WorkflowName ?? string.Empty;
            LastConfiguredNodeId = item.NodeId ?? string.Empty;
            HasLastConfiguredWidget = !string.IsNullOrWhiteSpace(LastConfiguredWorkflowName)
                                      && !string.IsNullOrWhiteSpace(LastConfiguredNodeId);
            try
            {
                // Trả control về UI loop để icon loading kịp render, không tạo delay cứng.
                await Task.Yield();
                OpenFloatingWidgetConfigFromMainWindow(item.WorkflowName, item.NodeId);
            }
            finally
            {
                item.IsConfiguring = false;
            }
        }

        /// <summary>
        /// Mở lại nhanh màn cấu hình của widget gần nhất đã chỉnh từ MainWindow.
        /// </summary>
        [RelayCommand]
        private async Task ConfigureLastWidget()
        {
            if (IsConfigureLastWidgetLoading) return;
            if (!HasLastConfiguredWidget ||
                string.IsNullOrWhiteSpace(LastConfiguredWorkflowName) ||
                string.IsNullOrWhiteSpace(LastConfiguredNodeId))
            {
                return;
            }

            // Nếu list mới refresh mà item đã bị xóa thì dừng.
            var exists = WidgetShortcuts.Any(w =>
                string.Equals(w.WorkflowName, LastConfiguredWorkflowName, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(w.NodeId, LastConfiguredNodeId, System.StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                HasLastConfiguredWidget = false;
                return;
            }

            IsConfigureLastWidgetLoading = true;
            try
            {
                // Trả control về UI loop để icon loading kịp render, không tạo delay cứng.
                await Task.Yield();
                OpenFloatingWidgetConfigFromMainWindow(LastConfiguredWorkflowName, LastConfiguredNodeId);
            }
            finally
            {
                IsConfigureLastWidgetLoading = false;
            }
        }

        private void OpenFloatingWidgetConfigFromMainWindow(string? workflowName, string? nodeId)
        {
            if (string.IsNullOrWhiteSpace(workflowName)) return;

            if (!TryLoadWidgetConfigNodesFast(workflowName, out var workflowFilePath, out var rawJson, out var nodes))
                return;

            var mainWindow = Application.Current.MainWindow;

            void PersistChanges()
            {
                try
                {
                    SaveWidgetConfigNodesFast(workflowFilePath, rawJson, nodes);
                }
                catch
                {
                    // best effort
                }
            }

            var dialog = new FloatingWidgetConfigDialog(
                nodes,
                host: null,
                persistChanges: PersistChanges,
                runtimeActionsEnabled: false)
            {
                Owner = mainWindow
            };

            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                dialog.SelectNodeById(nodeId);
            }

            dialog.ShowDialog();
            RefreshWidgetShortcuts();
        }

        private static bool TryLoadWidgetConfigNodesFast(
            string workflowName,
            out string workflowFilePath,
            out string rawJson,
            out List<FlowMy.Models.WorkflowNode> nodes)
        {
            workflowFilePath = string.Empty;
            rawJson = string.Empty;
            nodes = new List<FlowMy.Models.WorkflowNode>();

            try
            {
                var dir = FileWorkflowPersistenceService.GetDefaultWorkflowsDirectory();
                workflowFilePath = Path.Combine(dir, $"{workflowName}.json");
                if (!File.Exists(workflowFilePath))
                    return false;

                rawJson = File.ReadAllText(workflowFilePath);
                using var doc = JsonDocument.Parse(rawJson);
                if (!doc.RootElement.TryGetProperty("Nodes", out var nodesEl) || nodesEl.ValueKind != JsonValueKind.Array)
                    return false;

                foreach (var nodeEl in nodesEl.EnumerateArray())
                {
                    if (nodeEl.ValueKind != JsonValueKind.Object) continue;

                    var id = nodeEl.TryGetProperty("Id", out var idEl) ? (idEl.GetString() ?? string.Empty) : string.Empty;
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    var title = nodeEl.TryGetProperty("Title", out var titleEl) ? (titleEl.GetString() ?? string.Empty) : string.Empty;
                    var type = ParseNodeType(nodeEl);
                    if (type == FlowMy.Models.NodeType.Start || type == FlowMy.Models.NodeType.End) continue;

                    FlowMy.Models.FloatingWidgetConfig? floating = null;
                    if (nodeEl.TryGetProperty("Properties", out var propsEl) &&
                        propsEl.ValueKind == JsonValueKind.Object &&
                        propsEl.TryGetProperty("FloatingWidget", out var fwEl))
                    {
                        try
                        {
                            var fwJson = fwEl.ValueKind == JsonValueKind.String
                                ? fwEl.GetString()
                                : fwEl.GetRawText();
                            if (!string.IsNullOrWhiteSpace(fwJson))
                                floating = JsonSerializer.Deserialize<FlowMy.Models.FloatingWidgetConfig>(fwJson);
                        }
                        catch
                        {
                            floating = null;
                        }
                    }

                    nodes.Add(new FlowMy.Models.WorkflowNode
                    {
                        Id = id,
                        Title = title,
                        Type = type,
                        FloatingWidget = floating
                    });
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SaveWidgetConfigNodesFast(
            string workflowFilePath,
            string rawJson,
            IEnumerable<FlowMy.Models.WorkflowNode> nodes)
        {
            var root = JsonNode.Parse(rawJson) as JsonObject;
            var nodeArray = root?["Nodes"] as JsonArray;
            if (root == null || nodeArray == null) return;

            var map = nodes
                .Where(n => !string.IsNullOrWhiteSpace(n.Id))
                .ToDictionary(n => n.Id, n => n.FloatingWidget, System.StringComparer.OrdinalIgnoreCase);

            foreach (var item in nodeArray)
            {
                if (item is not JsonObject nodeObj) continue;
                var id = nodeObj["Id"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!map.TryGetValue(id, out var cfg)) continue;

                if (nodeObj["Properties"] is not JsonObject propsObj)
                {
                    propsObj = new JsonObject();
                    nodeObj["Properties"] = propsObj;
                }

                if (cfg == null)
                {
                    propsObj.Remove("FloatingWidget");
                    continue;
                }

                // Giữ tương thích schema hiện tại: lưu dạng JSON string trong Properties.FloatingWidget.
                propsObj["FloatingWidget"] = JsonSerializer.Serialize(cfg);
            }

            var updated = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(workflowFilePath, updated);
        }

        private static FlowMy.Models.NodeType ParseNodeType(JsonElement nodeEl)
        {
            if (!nodeEl.TryGetProperty("Type", out var typeEl))
                return FlowMy.Models.NodeType.Generic;

            try
            {
                if (typeEl.ValueKind == JsonValueKind.String)
                {
                    var s = typeEl.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && System.Enum.TryParse<FlowMy.Models.NodeType>(s, true, out var parsed))
                        return parsed;
                }
                else if (typeEl.ValueKind == JsonValueKind.Number && typeEl.TryGetInt32(out var i))
                {
                    return (FlowMy.Models.NodeType)i;
                }
            }
            catch { }

            return FlowMy.Models.NodeType.Generic;
        }

        private void OpenWorkflowEditorInternal(
            string? workflowNameToLoad,
            IList<string>? widgetNodeIds,
            bool headless,
            string? autoOpenConfigNodeId = null)
        {
            if (App.Services == null) return;

            var mainWindow = Application.Current.MainWindow;

            var scope = App.Services.CreateScope();
            var workflowWindow = scope.ServiceProvider.GetService(typeof(WorkflowEditorWindow)) as WorkflowEditorWindow;
            if (workflowWindow == null)
            {
                scope.Dispose();
                return;
            }

            workflowWindow.Owner = mainWindow;
            workflowWindow.Closing += (_, e) =>
            {
                // Với headless session: khi user bấm X trong lúc widget còn chạy, chỉ ẩn về nền thay vì đóng hẳn.
                if (_isRehidingHeadlessWindow.Contains(workflowWindow)) return;
                var nodeIds = workflowWindow.ViewModel?.Nodes?
                    .Select(n => n.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet(System.StringComparer.OrdinalIgnoreCase);
                if (nodeIds == null || nodeIds.Count == 0) return;

                var active = FloatingWidgetManager.Instance.GetActiveWidgetNodeIds();
                bool hasAnyWidgetFromThisWorkflow = active.Any(id => nodeIds.Contains(id));
                if (!hasAnyWidgetFromThisWorkflow) return;

                e.Cancel = true;
                _isRehidingHeadlessWindow.Add(workflowWindow);
                try
                {
                    PrepareWindowForHeadlessBackground(workflowWindow);
                }
                finally
                {
                    _isRehidingHeadlessWindow.Remove(workflowWindow);
                }
            };

            if (headless)
            {
                workflowWindow.ConfigureHeadlessCanvasOptimization(widgetNodeIds);
                if (!string.IsNullOrWhiteSpace(workflowNameToLoad))
                {
                    _headlessWorkflowWindows[workflowNameToLoad] = workflowWindow;
                }
                PrepareWindowForHeadlessBackground(workflowWindow);
            }
            else
            {
                // Ẩn MainWindow khi mở WorkflowEditorWindow ở chế độ bình thường
                mainWindow?.Hide();
            }

            workflowWindow.Closed += (_, __) =>
            {
                scope.Dispose();
                foreach (var kv in _headlessWorkflowWindows.Where(kv => ReferenceEquals(kv.Value, workflowWindow)).ToList())
                {
                    _headlessWorkflowWindows.Remove(kv.Key);
                }
                if (!headless && mainWindow != null)
                {
                    mainWindow.Show();
                    mainWindow.Activate();
                }
                RefreshWidgetShortcuts();
            };

            void ActivateWidgets()
            {
                if (widgetNodeIds == null || widgetNodeIds.Count == 0) return;
                workflowWindow.Dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        var targetIds = widgetNodeIds
                            .Where(id => !string.IsNullOrWhiteSpace(id))
                            .Distinct(System.StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        if (targetIds.Count == 0) return;

                        var remaining = new HashSet<string>(targetIds, System.StringComparer.OrdinalIgnoreCase);

                        // Chờ workflow load xong rồi mới map nodeId -> node để tránh miss khi Loaded chạy quá sớm.
                        for (int attempt = 0; attempt < 40 && remaining.Count > 0; attempt++)
                        {
                            var vm = workflowWindow.ViewModel;
                            if (vm != null && vm.Nodes != null && vm.Nodes.Count > 0)
                            {
                                foreach (var wid in remaining.ToList())
                                {
                                    var node = vm.Nodes.FirstOrDefault(n =>
                                        string.Equals(n.Id, wid, System.StringComparison.OrdinalIgnoreCase));
                                    if (node == null) continue;

                                    node.FloatingWidget ??= new FlowMy.Models.FloatingWidgetConfig();
                                    node.FloatingWidget.IsEnabled = true;
                                    if (headless)
                                    {
                                        // Launch từ nút 📌 ở MainWindow: widget chạy ngầm, không tạo item trên taskbar.
                                        node.FloatingWidget.ShowInTaskbar = false;
                                    }

                                    FloatingWidgetManager.Instance.OpenWidget(node, workflowWindow);
                                    remaining.Remove(wid);
                                }
                            }

                            if (remaining.Count == 0) break;
                            await Task.Delay(100);
                        }
                        if (headless && remaining.Count == 0)
                        {
                            // Sau khi mở widget cuối: nếu không còn widget nào active → đóng editor
                            HookHeadlessAutoCloseOnLastWidget(workflowWindow);
                        }
                    }
                    catch { /* best effort */ }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }

            if (widgetNodeIds != null && widgetNodeIds.Count > 0)
            {
                workflowWindow.Loaded += (_, __) =>
                {
                    // Defer để nodes đã được add hoàn tất (Preload chạy LoadWorkflow
                    // trước Show(), nhưng item template của canvas cần 1 tick layout).
                    workflowWindow.Dispatcher.BeginInvoke(
                        new System.Action(ActivateWidgets),
                        System.Windows.Threading.DispatcherPriority.Background);
                };
            }

            // Nếu được yêu cầu từ MainWindow: auto bật dialog cấu hình widget ngay khi editor load xong.
            if (!string.IsNullOrWhiteSpace(autoOpenConfigNodeId))
            {
                workflowWindow.Loaded += (_, __) =>
                {
                    try
                    {
                        var nodes = workflowWindow.ViewModel?.Nodes?.ToList()
                            ?? new List<FlowMy.Models.WorkflowNode>();
                        if (nodes.Count == 0) return;

                        var nodeIdsInWorkflow = nodes
                            .Select(n => n.Id)
                            .Where(id => !string.IsNullOrWhiteSpace(id))
                            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

                        var dialog = new FloatingWidgetConfigDialog(nodes, workflowWindow)
                        {
                            // Nếu mở cấu hình từ MainWindow ở chế độ headless thì owner là MainWindow
                            // để dialog luôn hiện dù editor đang ẩn.
                            Owner = headless ? mainWindow : workflowWindow
                        };
                        dialog.SelectNodeById(autoOpenConfigNodeId);
                        dialog.ShowDialog();
                        RefreshWidgetShortcuts();

                        // Với luồng config-headless: nếu sau khi đóng dialog workflow này không có
                        // widget nào đang active thì đóng editor nền ngay để giải phóng tài nguyên.
                        if (headless)
                        {
                            var active = FloatingWidgetManager.Instance.GetActiveWidgetNodeIds();
                            bool hasAnyWidgetFromThisWorkflow = active.Any(id => nodeIdsInWorkflow.Contains(id));
                            if (!hasAnyWidgetFromThisWorkflow)
                            {
                                try { workflowWindow.Close(); } catch { }
                            }
                            else
                            {
                                // Nếu có widget active thì giữ editor nền sống, và tự đóng khi widget cuối đóng.
                                HookHeadlessAutoCloseOnLastWidget(workflowWindow);
                            }
                        }
                    }
                    catch { }
                };
            }

            if (!headless)
            {
                workflowWindow.Show();
            }
            else
            {
                // Show để Loaded event kích hoạt + workflow engine sống, sau đó ẩn lại
                workflowWindow.Show();
                workflowWindow.Hide();
            }

            // Set workflow name SAU khi window đã show để giảm block khởi tạo ban đầu trên MainWindow.
            if (!string.IsNullOrWhiteSpace(workflowNameToLoad))
            {
                workflowWindow.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new System.Action(() =>
                {
                    try
                    {
                        var vm = workflowWindow.ViewModel;
                        if (vm != null && !string.Equals(vm.CurrentWorkflowName, workflowNameToLoad, System.StringComparison.OrdinalIgnoreCase))
                        {
                            vm.CurrentWorkflowName = workflowNameToLoad!;
                        }
                    }
                    catch { }
                }));
            }
        }

        private static void PrepareWindowForHeadlessBackground(WorkflowEditorWindow workflowWindow)
        {
            // Không ẩn MainWindow: user vẫn thấy launcher và card gốc.
            // WorkflowEditorWindow chạy ngầm để widget sống được.
            workflowWindow.ShowInTaskbar = false;
            workflowWindow.WindowState = WindowState.Minimized;
            workflowWindow.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Trong chế độ headless: khi tất cả widget bị đóng thì tự đóng luôn
        /// workflow editor ngầm để giải phóng tài nguyên.
        /// </summary>
        private static void HookHeadlessAutoCloseOnLastWidget(WorkflowEditorWindow workflowWindow)
        {
            var mgr = FloatingWidgetManager.Instance;
            System.EventHandler<string>? handler = null;
            handler = (_, _) =>
            {
                if (mgr.GetActiveWidgetNodeIds().Count == 0)
                {
                    mgr.WidgetClosed -= handler!;
                    try { workflowWindow.Close(); } catch { }
                }
            };
            mgr.WidgetClosed += handler;
        }
    }
}
