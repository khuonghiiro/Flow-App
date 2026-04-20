using FlowMy.Services;
using FlowMy.Services.Interaction;
using FlowMy.Views;
using FlowMy.Views.Overlays;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
                // Cho UI đủ thời gian render icon loading trước khi vào luồng mở nặng.
                await Task.Delay(180);
                OpenWorkflowEditorInternal(
                    item.WorkflowName,
                    new List<string> { item.NodeId },
                    headless: true);
                item.IsWidgetOpen = true;
                await Task.Delay(700);
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
                // Cho UI đủ thời gian render icon loading trước khi vào luồng mở nặng.
                await Task.Delay(180);
                OpenWorkflowEditorInternal(
                    workflowNameToLoad: item.WorkflowName,
                    widgetNodeIds: null,
                    headless: true,
                    autoOpenConfigNodeId: item.NodeId);
                await Task.Delay(900);
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
                // Cho UI đủ thời gian render icon loading trước khi vào luồng mở nặng.
                await Task.Delay(180);
                OpenWorkflowEditorInternal(
                    workflowNameToLoad: LastConfiguredWorkflowName,
                    widgetNodeIds: null,
                    headless: true,
                    autoOpenConfigNodeId: LastConfiguredNodeId);
                await Task.Delay(900);
            }
            finally
            {
                IsConfigureLastWidgetLoading = false;
            }
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
                // Không ẩn MainWindow: user vẫn thấy launcher và card gốc.
                // WorkflowEditorWindow chạy ngầm để widget sống được.
                workflowWindow.ShowInTaskbar = false;
                workflowWindow.WindowState = WindowState.Minimized;
                workflowWindow.Visibility = Visibility.Hidden;
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
                        if (headless)
                        {
                            // Launch từ nút 📌 ở MainWindow: widget chạy ngầm, không tạo item trên taskbar.
                            node.FloatingWidget.ShowInTaskbar = false;
                        }

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
