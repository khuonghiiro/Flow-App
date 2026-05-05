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
    private readonly Dictionary<string, HiddenNodeVisualState> _hiddenNodeVisuals = new();
    private readonly object _lock = new();

    private sealed class HiddenNodeVisualState
    {
        public WorkflowNode? Node { get; init; }
        public Visibility? BorderVisibility { get; init; }
        public Visibility? TitleVisibility { get; init; }
        public Dictionary<NodePort, Visibility> PortVisibility { get; init; } = new();
        public DependencyPropertyChangedEventHandler? BorderVisibilityGuard { get; set; }
        public DependencyPropertyChangedEventHandler? TitleVisibilityGuard { get; set; }
        public Dictionary<NodePort, DependencyPropertyChangedEventHandler> PortVisibilityGuards { get; } = new();
    }

    /// <summary>Số widget tối đa đồng thời.</summary>
    public int MaxWidgets { get; set; } = 32;

    /// <summary>Fire khi một widget được mở.</summary>
    public event EventHandler<string>? WidgetOpened;

    /// <summary>Fire khi một widget bị đóng (đã remove khỏi activeWidgets).</summary>
    public event EventHandler<string>? WidgetClosed;

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
                RestoreNodeVisualAfterWidgetClosed(node.Id);
                lock (_lock) { _activeWidgets.Remove(node.Id); }
                try { WidgetClosed?.Invoke(this, node.Id); } catch { }
            };
            _activeWidgets[node.Id] = widget;
            HideNodeVisualWhenWidgetOpened(node);
            widget.Show();
            try
            {
                widget.ExpandWidget();
                widget.Activate();
            }
            catch { }
            try { WidgetOpened?.Invoke(this, node.Id); } catch { }
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
                RestoreNodeVisualAfterWidgetClosed(nodeId);
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
                try { widget.ApplyConfigChanges(); } catch { }
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

    private void HideNodeVisualWhenWidgetOpened(WorkflowNode node)
    {
        // "Chuyển host" visual cho các node có nội dung nặng khi mở widget,
        // để widget hiển thị thay cho node trên canvas.
        if (node is not HtmlUiNode && node is not WebNode && node is not VideoProcessingNode) return;

        if (_hiddenNodeVisuals.ContainsKey(node.Id)) return;

        var state = new HiddenNodeVisualState
        {
            Node = node,
            BorderVisibility = node.Border?.Visibility,
            TitleVisibility = node.TitleTextBlockUI?.Visibility
        };

        foreach (var port in node.Ports)
        {
            if (port?.PortUI == null) continue;
            state.PortVisibility[port] = port.PortUI.Visibility;
        }

        _hiddenNodeVisuals[node.Id] = state;

        if (node.Border != null)
        {
            node.Border.Visibility = Visibility.Collapsed;
            state.BorderVisibilityGuard = (_, __) =>
            {
                if (_activeWidgets.ContainsKey(node.Id) && node.Border != null && node.Border.Visibility != Visibility.Collapsed)
                    node.Border.Visibility = Visibility.Collapsed;
            };
            node.Border.IsVisibleChanged += state.BorderVisibilityGuard;
        }
        if (node.TitleTextBlockUI != null)
        {
            node.TitleTextBlockUI.Visibility = Visibility.Collapsed;
            state.TitleVisibilityGuard = (_, __) =>
            {
                if (_activeWidgets.ContainsKey(node.Id) && node.TitleTextBlockUI != null && node.TitleTextBlockUI.Visibility != Visibility.Collapsed)
                    node.TitleTextBlockUI.Visibility = Visibility.Collapsed;
            };
            node.TitleTextBlockUI.IsVisibleChanged += state.TitleVisibilityGuard;
        }
        foreach (var port in node.Ports)
        {
            if (port?.PortUI == null) continue;
            port.PortUI.Visibility = Visibility.Collapsed;
            var p = port;
            DependencyPropertyChangedEventHandler guard = (_, __) =>
            {
                if (_activeWidgets.ContainsKey(node.Id) && p.PortUI != null && p.PortUI.Visibility != Visibility.Collapsed)
                    p.PortUI.Visibility = Visibility.Collapsed;
            };
            state.PortVisibilityGuards[p] = guard;
            port.PortUI.IsVisibleChanged += guard;
        }
    }

    private void RestoreNodeVisualAfterWidgetClosed(string nodeId)
    {
        if (!_hiddenNodeVisuals.TryGetValue(nodeId, out var state))
            return;

        _hiddenNodeVisuals.Remove(nodeId);
        var node = state.Node;
        if (node == null) return;

        if (node.Border != null && state.BorderVisibilityGuard != null)
            node.Border.IsVisibleChanged -= state.BorderVisibilityGuard;
        if (node.TitleTextBlockUI != null && state.TitleVisibilityGuard != null)
            node.TitleTextBlockUI.IsVisibleChanged -= state.TitleVisibilityGuard;
        foreach (var kv in state.PortVisibilityGuards)
        {
            var port = kv.Key;
            if (port?.PortUI != null)
                port.PortUI.IsVisibleChanged -= kv.Value;
        }

        if (node.Border != null && state.BorderVisibility.HasValue)
            node.Border.Visibility = state.BorderVisibility.Value;
        if (node.TitleTextBlockUI != null && state.TitleVisibility.HasValue)
            node.TitleTextBlockUI.Visibility = state.TitleVisibility.Value;

        foreach (var kv in state.PortVisibility)
        {
            var port = kv.Key;
            if (port?.PortUI != null)
                port.PortUI.Visibility = kv.Value;
        }
    }
}
