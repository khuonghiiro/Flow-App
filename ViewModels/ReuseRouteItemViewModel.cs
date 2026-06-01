using FlowMy.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// ViewModel cho từng dòng cấu hình "tái sử dụng flow" trong dialog node.
    /// </summary>
    public partial class ReuseRouteItemViewModel : ObservableObject
    {
        /// <summary>Id của node nối trực tiếp vào input của node hiện tại.</summary>
        [ObservableProperty]
        private string _incomingNodeId = string.Empty;

        /// <summary>Tiêu đề hiển thị cho node in.</summary>
        [ObservableProperty]
        private string _incomingNodeTitle = string.Empty;

        /// <summary>Id của node out được chọn (nối trực tiếp ra từ node hiện tại).</summary>
        [ObservableProperty]
        private string? _selectedOutgoingNodeId;

        /// <summary>Danh sách node out có thể chọn (nối trực tiếp ra từ node hiện tại).</summary>
        public ObservableCollection<WorkflowDataSourceOption> OutgoingOptions { get; } = new();

        /// <summary>
        /// Kiểu line được chọn cho connection từ node hiện tại sang node OUT.
        /// Giá trị là "WorkflowDefault" hoặc tên enum ConnectionLineStyle
        /// ("Bezier", "Orthogonal", "Straight", "SmoothOrthogonal", "Arc", "RadialFanout", "Windy").
        /// </summary>
        [ObservableProperty]
        private string? _selectedLineStyleKey;

        /// <summary>
        /// Loại chức năng thực thi khi workflow chạy đến node này.
        /// - null hoặc "Default": Chạy logic mặc định
        /// - "Capture": Chụp ảnh trước khi chạy logic (cho TextScanNode, ScreenCaptureNode)
        /// </summary>
        [ObservableProperty]
        private string _selectedFunctionType = string.Empty;

        /// <summary>
        /// Selected item for ComboBox binding (used instead of SelectedValue)
        /// </summary>
        [ObservableProperty]
        private FunctionTypeOption? _selectedFunctionTypeItem;

        partial void OnSelectedFunctionTypeChanged(string value)
        {
            System.Diagnostics.Debug.WriteLine($"ReuseRouteItemViewModel: SelectedFunctionType changed to '{value}' for IncomingNodeId={IncomingNodeId}");
        }

        partial void OnSelectedFunctionTypeItemChanged(FunctionTypeOption? value)
        {
            System.Diagnostics.Debug.WriteLine($"ReuseRouteItemViewModel: SelectedFunctionTypeItem changed to '{value?.DisplayName}' for IncomingNodeId={IncomingNodeId}");
            if (value != null)
            {
                SelectedFunctionType = value.Value ?? string.Empty;
            }
        }
    }
}


