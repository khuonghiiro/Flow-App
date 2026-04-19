using FlowMy.Services;
using FlowMy.Services.Interaction;
using FlowMy.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace FlowMy.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        [ObservableProperty] private ObservableCollection<WidgetShortcutItem> widgetShortcuts = new();
        [ObservableProperty] private int widgetEnabledCount;
        [ObservableProperty] private bool hasWidgetShortcuts;

        public MainViewModel()
        {
            RefreshWidgetShortcuts();
        }

        /// <summary>
        /// Quét tất cả workflow JSON trong thư mục mặc định, tìm các node có
        /// FloatingWidget.IsEnabled = true và build danh sách shortcut hiển thị.
        /// </summary>
        [RelayCommand]
        public void RefreshWidgetShortcuts()
        {
            WidgetShortcuts.Clear();
            try
            {
                var scan = WidgetShortcutScanner.Scan();
                foreach (var item in scan.Items.OrderBy(i => i.WorkflowName).ThenBy(i => i.WidgetName))
                {
                    WidgetShortcuts.Add(item);
                }
                WidgetEnabledCount = scan.EnabledCount;
            }
            catch
            {
                WidgetEnabledCount = 0;
            }
            HasWidgetShortcuts = WidgetShortcuts.Count > 0;
        }

        /// <summary>
        /// Command để mở WorkflowEditorWindow
        /// </summary>
        [RelayCommand]
        private void OpenWorkflowEditor()
        {
            OpenWorkflowEditorInternal(workflowNameToLoad: null, widgetNodeId: null);
        }

        /// <summary>
        /// Kích hoạt 1 widget shortcut: mở editor, load workflow tương ứng,
        /// sau đó bật widget cho node đó.
        /// </summary>
        [RelayCommand]
        private void LaunchWidget(WidgetShortcutItem? item)
        {
            if (item == null) return;
            OpenWorkflowEditorInternal(item.WorkflowName, item.NodeId);
        }

        private void OpenWorkflowEditorInternal(string? workflowNameToLoad, string? widgetNodeId)
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

            // Ẩn MainWindow khi mở WorkflowEditorWindow
            mainWindow?.Hide();

            // Hiện lại MainWindow khi WorkflowEditorWindow đóng → refresh shortcut list
            workflowWindow.Closed += (_, __) =>
            {
                scope.Dispose();
                if (mainWindow != null)
                {
                    mainWindow.Show();
                    mainWindow.Activate();
                }
                RefreshWidgetShortcuts();
            };

            // Sau khi window load xong: set workflow (nếu chỉ định) và mở widget
            if (!string.IsNullOrWhiteSpace(workflowNameToLoad))
            {
                workflowWindow.Loaded += (_, __) =>
                {
                    try
                    {
                        var vm = workflowWindow.ViewModel;
                        if (vm == null) return;

                        // Set CurrentWorkflowName để trigger LoadWorkflow
                        vm.CurrentWorkflowName = workflowNameToLoad!;

                        // Defer activation tới sau khi nodes đã được add
                        workflowWindow.Dispatcher.BeginInvoke(new System.Action(() =>
                        {
                            if (string.IsNullOrWhiteSpace(widgetNodeId)) return;
                            var node = vm.Nodes?.FirstOrDefault(n =>
                                string.Equals(n.Id, widgetNodeId, System.StringComparison.OrdinalIgnoreCase));
                            if (node == null) return;

                            // Đảm bảo widget được bật
                            node.FloatingWidget ??= new FlowMy.Models.FloatingWidgetConfig();
                            node.FloatingWidget.IsEnabled = true;

                            FloatingWidgetManager.Instance.OpenWidget(node, workflowWindow);
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    catch { /* ignore */ }
                };
            }

            workflowWindow.Show();
        }
    }
}
