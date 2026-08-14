using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using FlowMy.ViewModels;

namespace FlowMy.Views.Overlays;

/// <summary>
/// Cửa sổ nổi chứa panel Execution Log khi user chọn mode "Detached" (kiểu DevTools pop-out).
/// Host logic:
/// - <see cref="AttachContent"/> reparent UIElement từ main grid vào window này.
/// - <see cref="DetachContent"/> được parent gọi để đưa UIElement về lại main grid.
/// Chúng ta KHÔNG clone panel để tránh mất state (scroll, filter, expanded cards...).
///
/// Taskbar behaviour:
/// - Đặt AppUserModelID riêng (khác main app) qua <c>SetWindowAppId</c> để Windows cho nó
///   một icon taskbar độc lập, không bị group vào icon gốc của Flow-App (giống DevTools Chrome).
/// - Start ở state <see cref="WindowState.Maximized"/> để user mở xong thấy ngay panel đầy màn hình;
///   khi user đã có position/size lưu, restore lại về Normal với tọa độ đã lưu.
/// </summary>
public partial class ExecutionTraceDetachedWindow : Window
{
    private const string DetachedWindowAppId = "FlowMy.ExecutionLog.DetachedWindow";

    public ExecutionTraceDetachedWindow()
    {
        InitializeComponent();
        // Phải set AppID ngay trước khi HWND của window được tạo để Windows shell nhận diện
        // nó như "process khác" cho mục đích taskbar grouping. SourceInitialized là thời điểm
        // HWND vừa có nhưng window chưa hiển thị → dùng DwmSetWindowAttribute tương đương.
        SourceInitialized += OnSourceInitialized;
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

        // Khôi phục vị trí/size từ VM NẾU user đã có preference từ lần trước; nếu vị trí hợp lệ
        // thì chuyển window sang Normal + set Left/Top/Width/Height, bỏ trạng thái Maximized mặc định.
        // Ngược lại giữ Maximized để đảm bảo mở lên là thấy ngay (UX yêu cầu: "phóng to nó lên").
        bool hasSavedPosition = !double.IsNaN(vm.ExecutionTracePanelDetachedLeft) &&
                                !double.IsNaN(vm.ExecutionTracePanelDetachedTop);

        if (hasSavedPosition)
        {
            WindowState = WindowState.Normal;
            Left = vm.ExecutionTracePanelDetachedLeft;
            Top = vm.ExecutionTracePanelDetachedTop;
            if (vm.ExecutionTracePanelDetachedWidth >= 300) Width = vm.ExecutionTracePanelDetachedWidth;
            if (vm.ExecutionTracePanelDetachedHeight >= 200) Height = vm.ExecutionTracePanelDetachedHeight;
        }

        // Persist khi user thay đổi. Chỉ lưu khi window đang Normal (Maximized/Minimized không có
        // Left/Top/size chính xác cho lần mở sau).
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

    /// <summary>
    /// Mang cửa sổ về trạng thái hiển thị và đưa focus lên (giống user click icon DevTools trong taskbar).
    /// Gọi khi window đã tồn tại nhưng đang bị minimize hoặc đang bị che khuất.
    /// </summary>
    public void RestoreAndActivate()
    {
        if (WindowState == WindowState.Minimized)
        {
            // Nếu user có preference vị trí thì về Normal, ngược lại Maximized theo UX ban đầu.
            WindowState = double.IsNaN(((WorkflowEditorViewModel?)DataContext)?.ExecutionTracePanelDetachedLeft ?? double.NaN)
                ? WindowState.Maximized
                : WindowState.Normal;
        }
        if (!IsVisible) Show();
        Activate();
        Focus();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            SetWindowAppId(hwnd, DetachedWindowAppId);
        }
        catch
        {
            // Best-effort: nếu shell API lỗi (Windows phiên bản rất cũ) thì bỏ qua, window vẫn hoạt động,
            // chỉ là có thể bị group vào icon gốc của app.
        }
    }

    // ===== Win32 / Shell interop để set AppUserModelID cho window =====
    // Tham khảo: https://learn.microsoft.com/en-us/windows/win32/shell/appids
    // Khi 2 window cùng process có 2 AppID khác nhau, Windows sẽ cho mỗi cái một taskbar button
    // riêng (không group), đồng thời pin/jump-list cũng tách riêng.

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr hwnd,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

    private static readonly Guid IID_IPropertyStore = new("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");

    // PKEY_AppUserModel_ID = { {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, 5 }
    private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 5
    };

    private static void SetWindowAppId(IntPtr hwnd, string appId)
    {
        var iid = IID_IPropertyStore;
        var hr = SHGetPropertyStoreForWindow(hwnd, ref iid, out var store);
        if (hr != 0 || store == null) return;
        try
        {
            // Copy ra local vì SetValue nhận ref PROPERTYKEY, và static readonly không dùng được ref.
            var key = PKEY_AppUserModel_ID;
            var pv = new PROPVARIANT { vt = 31 /* VT_LPWSTR */, pwszVal = Marshal.StringToCoTaskMemUni(appId) };
            try
            {
                store.SetValue(ref key, ref pv);
                store.Commit();
            }
            finally
            {
                if (pv.pwszVal != IntPtr.Zero) Marshal.FreeCoTaskMem(pv.pwszVal);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr pwszVal;
        public IntPtr padding;
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        void Commit();
    }
}
