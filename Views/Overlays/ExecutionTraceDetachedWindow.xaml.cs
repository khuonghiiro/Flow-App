using System;
using System.Windows;
using FlowMy.ViewModels;

namespace FlowMy.Views.Overlays;

/// <summary>
/// Cửa sổ nổi chứa panel Execution Log khi user chọn mode "Detached" (kiểu DevTools pop-out).
/// Host logic:
/// - <see cref="AttachContent"/> reparent UIElement từ main grid vào window này.
/// - <see cref="DetachedClosing"/> được parent subscribe để đưa UIElement về lại main grid.
/// Chúng ta KHÔNG clone panel để tránh mất state (scroll, filter, expanded cards...).
/// </summary>
public partial class ExecutionTraceDetachedWindow : Window
{
    public ExecutionTraceDetachedWindow()
    {
        InitializeComponent();
    }

    /// <summary>Đặt UIElement làm content chính. Gọi <see cref="DetachContent"/> để gỡ trước khi đóng window.</summary>
    public void AttachContent(UIElement element)
    {
        HostGrid.Children.Clear();
        HostGrid.Children.Add(element);
    }

    /// <summary>Gỡ UIElement khỏi content (để caller gắn lại vào main grid).</summary>
    public UIElement? DetachContent()
    {
        if (HostGrid.Children.Count == 0) return null;
        var el = HostGrid.Children[0] as UIElement;
        HostGrid.Children.Clear();
        return el;
    }

    /// <summary>Sync ra VM khi user kéo resize/move window (để persist vị trí, size).</summary>
    public void BindToViewModel(WorkflowEditorViewModel vm)
    {
        DataContext = vm;

        // Khôi phục vị trí/size từ VM. NaN = để window tự chọn (WindowStartupLocation=CenterOwner nếu Owner set).
        if (!double.IsNaN(vm.ExecutionTracePanelDetachedLeft) &&
            !double.IsNaN(vm.ExecutionTracePanelDetachedTop))
        {
            Left = vm.ExecutionTracePanelDetachedLeft;
            Top = vm.ExecutionTracePanelDetachedTop;
        }
        if (vm.ExecutionTracePanelDetachedWidth >= 300) Width = vm.ExecutionTracePanelDetachedWidth;
        if (vm.ExecutionTracePanelDetachedHeight >= 200) Height = vm.ExecutionTracePanelDetachedHeight;

        // Persist khi user thay đổi.
        LocationChanged += (_, _) =>
        {
            if (WindowState != WindowState.Normal) return;
            vm.ExecutionTracePanelDetachedLeft = Left;
            vm.ExecutionTracePanelDetachedTop = Top;
        };
        SizeChanged += (_, e) =>
        {
            if (WindowState != WindowState.Normal) return;
            vm.ExecutionTracePanelDetachedWidth = e.NewSize.Width;
            vm.ExecutionTracePanelDetachedHeight = e.NewSize.Height;
        };
    }
}
