using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// Đại diện cho 1 workflow trên MainWindow launcher, chứa danh sách
    /// floating widget đã được cấu hình (IsEnabled = true) của workflow đó.
    /// </summary>
    public partial class WorkflowWidgetGroupItem : ObservableObject
    {
        [ObservableProperty] private string workflowName = string.Empty;
        [ObservableProperty] private ObservableCollection<WidgetShortcutItem> widgets = new();

        /// <summary>Tổng số widget trong workflow này.</summary>
        public int WidgetCount => Widgets?.Count ?? 0;
    }
}
