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
using System.Windows.Threading;
using FlowMy.Services.Interfaces;

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
        private readonly HashSet<string> _trayPinnedKeys = new(System.StringComparer.OrdinalIgnoreCase);

        public MainViewModel()
        {
            FloatingWidgetManager.Instance.WidgetOpened += OnWidgetOpened;
            FloatingWidgetManager.Instance.WidgetClosed += OnWidgetClosed;
            RefreshWidgetShortcuts();
            SyncTrayPinnedMenu();
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
            HasWidgetShortcuts = WidgetShortcuts.Count > 0;
            SyncWidgetOpenStates();
            SyncPinnedStatesFromCache();
            SyncTrayPinnedMenu();

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

        [RelayCommand]
        private void TogglePinToTray(WidgetShortcutItem? item)
        {
            if (item == null) return;
            var key = BuildTrayPinKey(item.WorkflowName, item.NodeId);
            if (string.IsNullOrWhiteSpace(key)) return;

            if (item.IsPinnedToTray)
            {
                item.IsPinnedToTray = false;
                _trayPinnedKeys.Remove(key);
            }
            else
            {
                item.IsPinnedToTray = true;
                _trayPinnedKeys.Add(key);
            }

            SyncTrayPinnedMenu();
        }

        private void SyncPinnedStatesFromCache()
        {
            foreach (var item in WidgetShortcuts)
            {
                var key = BuildTrayPinKey(item.WorkflowName, item.NodeId);
                item.IsPinnedToTray = !string.IsNullOrWhiteSpace(key) && _trayPinnedKeys.Contains(key);
            }
        }

        private void SyncTrayPinnedMenu()
        {
            try
            {
                var tray = App.Services?.GetService<ITrayService>();
                if (tray == null) return;

                var pinned = WidgetShortcuts
                    .Where(w => w.IsPinnedToTray)
                    .Select(w => (w.NodeId, Label: string.IsNullOrWhiteSpace(w.WidgetName) ? w.DisplayLabel : $"{w.WidgetName} — {w.WorkflowName}"))
                    .ToList();

                tray.SetPinnedWidgets(pinned, nodeId =>
                {
                    var item = WidgetShortcuts.FirstOrDefault(w =>
                        string.Equals(w.NodeId, nodeId, System.StringComparison.OrdinalIgnoreCase));
                    if (item == null) return;

                    // Mở widget overlay (headless) từ tray.
                    _ = LaunchWidgetHeadless(item);
                });
            }
            catch { }
        }

        private static string BuildTrayPinKey(string? workflowName, string? nodeId)
        {
            if (string.IsNullOrWhiteSpace(workflowName) || string.IsNullOrWhiteSpace(nodeId)) return string.Empty;
            return $"{workflowName}::{nodeId}";
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
            if (item == null || item.IsLaunchingHeadless || item.IsWidgetOpen) return;
            item.IsLaunchingHeadless = true;
            try
            {
                // Đẩy 1 nhịp render để spinner ⏳ hiện ngay trước khi vào đoạn load nặng.
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                    await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                else
                    await Task.Yield();

                OpenWorkflowEditorInternal(
                    item.WorkflowName,
                    new List<string> { item.NodeId },
                    headless: true,
                    hideMainWindowWhenHeadless: true);
                item.IsWidgetOpen = await WaitUntilWidgetOpenAsync(item.NodeId, timeoutMs: 4000);
            }
            finally
            {
                item.IsLaunchingHeadless = false;
                item.IsWidgetOpen = FloatingWidgetManager.Instance.IsWidgetOpen(item.NodeId);
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

        /// <summary>
        /// Mở trực tiếp màn cấu hình widget từ MainWindow cho 1 shortcut cụ thể.
        /// </summary>
        [RelayCommand]
        private async Task ConfigureWidget(WidgetShortcutItem? item)
        {
            if (item == null || item.IsConfiguring) return;
            item.IsConfiguring = true;
            LastConfiguredWorkflowName = item.WorkflowName ?? string.Empty;
            LastConfiguredNodeId = item.NodeId ?? string.Empty;
            HasLastConfiguredWidget = !string.IsNullOrWhiteSpace(LastConfiguredWorkflowName)
                                      && !string.IsNullOrWhiteSpace(LastConfiguredNodeId);
            try
            {
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

            await Task.Yield();
            OpenFloatingWidgetConfigFromMainWindow(LastConfiguredWorkflowName, LastConfiguredNodeId);
        }

        private void OpenFloatingWidgetConfigFromMainWindow(string? workflowName, string? nodeId)
        {
            if (string.IsNullOrWhiteSpace(workflowName)) return;
            if (!TryLoadWidgetConfigNodesFast(workflowName, out var workflowFilePath, out var rawJson, out var nodes))
                return;

            var mainWindow = Application.Current.MainWindow;
            void PersistChanges()
            {
                try { SaveWidgetConfigNodesFast(workflowFilePath, rawJson, nodes); } catch { }
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
                dialog.SelectNodeById(nodeId);

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
                if (!File.Exists(workflowFilePath)) return false;

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
                    var colorKey = nodeEl.TryGetProperty("ColorKey", out var colorKeyEl) ? (colorKeyEl.GetString() ?? string.Empty) : string.Empty;
                    var type = ParseNodeType(nodeEl);
                    if (type == FlowMy.Models.NodeType.Start || type == FlowMy.Models.NodeType.End) continue;

                    FlowMy.Models.FloatingWidgetConfig? floating = null;
                    if (nodeEl.TryGetProperty("Properties", out var propsEl)
                        && propsEl.ValueKind == JsonValueKind.Object
                        && propsEl.TryGetProperty("FloatingWidget", out var fwEl))
                    {
                        try
                        {
                            var fwJson = fwEl.ValueKind == JsonValueKind.String ? fwEl.GetString() : fwEl.GetRawText();
                            if (!string.IsNullOrWhiteSpace(fwJson))
                                floating = JsonSerializer.Deserialize<FlowMy.Models.FloatingWidgetConfig>(fwJson);
                        }
                        catch { floating = null; }
                    }

                    nodes.Add(new FlowMy.Models.WorkflowNode
                    {
                        Id = id,
                        Title = title,
                        ColorKey = colorKey,
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
            string? autoOpenConfigNodeId = null,
            WidgetShortcutItem? configureItem = null,
            bool hideMainWindowWhenHeadless = false)
        {
            if (App.Services == null) return;

            var mainWindow = Application.Current.MainWindow;

            var scope = App.Services.CreateScope();
            var workflowWindow = scope.ServiceProvider.GetService(typeof(WorkflowEditorWindow)) as WorkflowEditorWindow;
            if (workflowWindow == null)
            {
                if (configureItem != null) configureItem.IsConfiguring = false;
                scope.Dispose();
                return;
            }

            workflowWindow.Owner = mainWindow;

            // Preload workflow TRƯỚC khi Show() để tránh flash/viewport nhảy:
            // LoadWorkflow chạy ngay trong ViewModel, IsLoading đã trở về false
            // trước khi cửa sổ render lần đầu. Khi cửa sổ Loaded → ViewState
            // được apply đúng 1 lần (kèm re-apply ở ApplicationIdle).
            if (!string.IsNullOrWhiteSpace(workflowNameToLoad))
            {
                try
                {
                    var vm = workflowWindow.ViewModel;
                    if (vm != null)
                    {
                        vm.CurrentWorkflowName = workflowNameToLoad!;
                    }
                }
                catch { /* ignore: fall back tới gán sau Loaded */ }
            }

            if (headless)
            {
                workflowWindow.ConfigureHeadlessCanvasOptimization(widgetNodeIds);
                // Không ẩn MainWindow: user vẫn thấy launcher và card gốc.
                // WorkflowEditorWindow chạy ngầm để widget sống được.
                workflowWindow.ShowInTaskbar = false;
                workflowWindow.WindowState = WindowState.Minimized;
                workflowWindow.Visibility = Visibility.Hidden;
                if (hideMainWindowWhenHeadless)
                {
                    mainWindow?.Hide();
                }
            }
            else
            {
                // Ẩn MainWindow khi mở WorkflowEditorWindow ở chế độ bình thường
                mainWindow?.Hide();
            }

            workflowWindow.Closed += (_, __) =>
            {
                scope.Dispose();
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
                try
                {
                    var vm = workflowWindow.ViewModel;
                    if (vm == null) return;

                    foreach (var wid in widgetNodeIds)
                    {
                        if (string.IsNullOrWhiteSpace(wid)) continue;
                        var node = vm.Nodes?.FirstOrDefault(n =>
                            string.Equals(n.Id, wid, System.StringComparison.OrdinalIgnoreCase));
                        if (node == null) continue;

                        node.FloatingWidget ??= new FlowMy.Models.FloatingWidgetConfig();
                        node.FloatingWidget.IsEnabled = true;

                        FloatingWidgetManager.Instance.OpenWidget(node, workflowWindow);
                    }

                    if (headless)
                    {
                        // Sau khi mở widget cuối: nếu không còn widget nào active → đóng editor
                        HookHeadlessAutoCloseOnLastWidget(workflowWindow);
                    }
                }
                catch { /* best effort */ }
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
                workflowWindow.Loaded += async (_, __) =>
                {
                    try
                    {
                        var nodes = await WaitForWorkflowNodesAsync(
                            workflowWindow,
                            autoOpenConfigNodeId,
                            timeoutMs: 7000);
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
                    finally
                    {
                        if (configureItem != null)
                            configureItem.IsConfiguring = false;
                    }
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

        private void OnWidgetOpened(object? sender, string nodeId) => UpdateWidgetOpenState(nodeId, true);

        private void OnWidgetClosed(object? sender, string nodeId) => UpdateWidgetOpenState(nodeId, false);

        private void UpdateWidgetOpenState(string? nodeId, bool isOpen)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return;

            void Apply()
            {
                foreach (var item in WidgetShortcuts)
                {
                    if (string.Equals(item.NodeId, nodeId, System.StringComparison.OrdinalIgnoreCase))
                        item.IsWidgetOpen = isOpen;
                }
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                Apply();
            else
                dispatcher.BeginInvoke((System.Action)Apply);
        }

        private void SyncWidgetOpenStates()
        {
            foreach (var item in WidgetShortcuts)
                item.IsWidgetOpen = FloatingWidgetManager.Instance.IsWidgetOpen(item.NodeId);
        }

        private static async Task<bool> WaitUntilWidgetOpenAsync(string? nodeId, int timeoutMs)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return false;
            var mgr = FloatingWidgetManager.Instance;
            var elapsed = 0;
            const int step = 120;
            while (elapsed < timeoutMs)
            {
                if (mgr.IsWidgetOpen(nodeId)) return true;
                await Task.Delay(step);
                elapsed += step;
            }
            return mgr.IsWidgetOpen(nodeId);
        }

        private static async Task<List<FlowMy.Models.WorkflowNode>> WaitForWorkflowNodesAsync(
            WorkflowEditorWindow workflowWindow,
            string? targetNodeId,
            int timeoutMs)
        {
            var elapsed = 0;
            const int stepMs = 120;
            while (elapsed < timeoutMs)
            {
                var nodes = workflowWindow.ViewModel?.Nodes?.ToList()
                    ?? new List<FlowMy.Models.WorkflowNode>();
                if (nodes.Count > 0)
                {
                    if (string.IsNullOrWhiteSpace(targetNodeId)
                        || nodes.Any(n => string.Equals(n.Id, targetNodeId, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        return nodes;
                    }
                }

                await Task.Delay(stepMs);
                elapsed += stepMs;
            }

            return workflowWindow.ViewModel?.Nodes?.ToList()
                ?? new List<FlowMy.Models.WorkflowNode>();
        }
    }
}
