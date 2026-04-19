using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.Overlays;
using System.Windows;

namespace FlowMy.Services;

/// <summary>
/// Singleton quản lý tất cả floating widget windows.
/// Mỗi node chỉ được mở 1 widget tại một thời điểm.
/// </summary>
public sealed class FloatingWidgetManager
{
    private static readonly Lazy<FloatingWidgetManager> _lazy = new(() => new FloatingWidgetManager());
    public static FloatingWidgetManager Instance => _lazy.Value;

    private readonly Dictionary<string, FloatingWidgetWindow> _activeWidgets = new();
    private readonly object _lock = new();

    /// <summary>Số widget tối đa đồng thời.</summary>
    public int MaxWidgets { get; set; } = 10;

    private FloatingWidgetManager() { }

    /// <summary>Mở floating widget cho node. Nếu đã mở thì focus.</summary>
    public void OpenWidget(WorkflowNode node, IWorkflowEditorHost host)
    {
        if (node == null || host == null) return;

        lock (_lock)
        {
            // Nếu đã có widget cho node này, focus lên
            if (_activeWidgets.TryGetValue(node.Id, out var existing))
            {
                try
                {
                    existing.Activate();
                    existing.ExpandWidget();
                }
                catch { /* window có thể đã closed */ }
                return;
            }

            // Check limit
            if (_activeWidgets.Count >= MaxWidgets)
            {
                MessageBox.Show(
                    $"Đã đạt giới hạn {MaxWidgets} widget đồng thời.\nVui lòng đóng bớt widget trước khi mở thêm.",
                    "Giới hạn Widget",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Khởi tạo config nếu chưa có
            node.FloatingWidget ??= new FloatingWidgetConfig();
            node.FloatingWidget.IsEnabled = true;

            // Tạo widget window
            var widget = new FloatingWidgetWindow(node, host);
            widget.Closed += (s, e) =>
            {
                lock (_lock) { _activeWidgets.Remove(node.Id); }
            };
            _activeWidgets[node.Id] = widget;
            widget.Show();
        }
    }

    /// <summary>Đóng widget theo nodeId.</summary>
    public void CloseWidget(string nodeId)
    {
        lock (_lock)
        {
            if (_activeWidgets.TryGetValue(nodeId, out var widget))
            {
                _activeWidgets.Remove(nodeId);
                try { widget.Close(); } catch { }
            }
        }
    }

    /// <summary>Đóng tất cả widgets.</summary>
    public void CloseAllWidgets()
    {
        lock (_lock)
        {
            foreach (var kvp in _activeWidgets.ToList())
            {
                try { kvp.Value.Close(); } catch { }
            }
            _activeWidgets.Clear();
        }
    }

    /// <summary>Kiểm tra xem node có đang mở widget không.</summary>
    public bool IsWidgetOpen(string nodeId)
    {
        lock (_lock) { return _activeWidgets.ContainsKey(nodeId); }
    }

    /// <summary>Toggle mở/đóng widget.</summary>
    public void ToggleWidget(WorkflowNode node, IWorkflowEditorHost host)
    {
        if (node == null) return;
        if (IsWidgetOpen(node.Id))
            CloseWidget(node.Id);
        else
            OpenWidget(node, host);
    }

    /// <summary>Yêu cầu widget refresh nội dung (khi node thay đổi).</summary>
    public void RefreshWidget(string nodeId)
    {
        lock (_lock)
        {
            if (_activeWidgets.TryGetValue(nodeId, out var widget))
            {
                try { widget.RefreshContent(); } catch { }
            }
        }
    }

    /// <summary>Lấy danh sách các node IDs đang có widget mở.</summary>
    public List<string> GetActiveWidgetNodeIds()
    {
        lock (_lock) { return _activeWidgets.Keys.ToList(); }
    }

    /// <summary>Tự động restore widgets khi load workflow (nếu IsEnabled=true).</summary>
    public void AutoRestoreWidgets(IEnumerable<WorkflowNode> nodes, IWorkflowEditorHost host)
    {
        foreach (var node in nodes)
        {
            if (node.FloatingWidget is { IsEnabled: true })
            {
                OpenWidget(node, host);
            }
        }
    }
}
