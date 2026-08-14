using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

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
        [ObservableProperty] private bool isPinned;

        /// <summary>Tổng số widget trong workflow này.</summary>
        public int WidgetCount => Widgets?.Count ?? 0;

        /// <summary>Cập nhật IsPinned dựa trên trạng thái ghim của các widget con.</summary>
        public void RefreshPinnedState()
        {
            IsPinned = Widgets?.Any(w => w.IsPinnedToTray) == true;
        }
    }
}
