using CommunityToolkit.Mvvm.ComponentModel;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// Đại diện cho 1 floating widget đã được cấu hình trong một workflow,
    /// dùng để hiển thị ở MainWindow launcher.
    /// </summary>
    public partial class WidgetShortcutItem : ObservableObject
    {
        [ObservableProperty] private string widgetName = string.Empty;
        [ObservableProperty] private string workflowName = string.Empty;
        [ObservableProperty] private string nodeId = string.Empty;
        [ObservableProperty] private string nodeTitle = string.Empty;
        [ObservableProperty] private string iconKey = string.Empty;
        [ObservableProperty] private bool isEnabled;
        [ObservableProperty] private bool isLaunchingHeadless;
        [ObservableProperty] private bool isConfiguring;
        [ObservableProperty] private bool isWidgetOpen;
        [ObservableProperty] private bool isHeadlessDebugVisible;
        [ObservableProperty] private bool isReopeningHeadless;
        [ObservableProperty] private bool isPinnedToTray;

        /// <summary>Hiển thị dạng "Widget Name — Workflow".</summary>
        public string DisplayLabel
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(WidgetName)
                    ? (string.IsNullOrWhiteSpace(NodeTitle) ? NodeId : NodeTitle)
                    : WidgetName;
                return $"{name}  —  {WorkflowName}";
            }
        }
    }
}
