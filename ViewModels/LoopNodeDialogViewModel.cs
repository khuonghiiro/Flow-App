using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class LoopNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly LoopNode _loopNode;

        /// <summary>
        /// Danh sách node trong Loop Body (cho combobox Node trong Outputs và Gán dữ liệu).
        /// </summary>
        public ObservableCollection<WorkflowDataSourceOption> BodyNodeOptions { get; } = new();

        [ObservableProperty]
        private LoopType _loopType;

        [ObservableProperty]
        private int _repeatCount;

        [ObservableProperty]
        private int _startIndex;

        [ObservableProperty]
        private int _endIndex;

        [ObservableProperty]
        private bool _isLoopCountConnected;

        [ObservableProperty]
        private bool _isLoopCountNumberType;

        [ObservableProperty]
        private bool _showLoopArrayInput;

        [ObservableProperty]
        private string _loopCountErrorMessage = string.Empty;

        [ObservableProperty]
        private string _loopArrayErrorMessage = string.Empty;

        // Property để enable/disable TextBoxes
        // Enable khi: không có kết nối HOẶC output không phải số
        // Disable khi: có kết nối VÀ output là số
        public bool IsTextBoxesEnabled => !IsLoopCountConnected || !IsLoopCountNumberType;

        // Options cho ComboBox LoopType
        public ObservableCollection<LoopTypeOption> LoopTypeOptions { get; } = new()
        {
            new LoopTypeOption(LoopType.ForLoop, "Lặp theo chỉ số"),
            new LoopTypeOption(LoopType.RepeatN, "Lặp N lần"),
            new LoopTypeOption(LoopType.ForEachArray, "Lặp từng phần tử trong mảng")
        };

        public LoopNodeDialogViewModel(LoopNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _loopNode = node;
            // Quan trọng: gán qua property LoopType để OnLoopTypeChanged được gọi,
            // đảm bảo LoadInputs() chạy lại với LoopType đúng (ForEachArray)
            // khi mở lại dialog, tránh hiển thị key "loopCount" thay vì "loopArray".
            LoopType = node.LoopType;
            _repeatCount = node.RepeatCount;
            _startIndex = node.StartIndex;
            _endIndex = node.EndIndex;
            _loopCountErrorMessage = string.Empty;
            _loopArrayErrorMessage = string.Empty;

            UpdateConnectionStatesInternal();
            UpdatePanelVisibility();
            RefreshBodyNodeOptions();

            // Sync các properties khác khi node thay đổi
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(LoopNode.LoopType))
                    {
                        LoopType = node.LoopType;
                        // Sync title khi LoopType thay đổi (LoopNode tự động update title)
                        NodeTitle = node.Title ?? GetDefaultTitle();
                    }
                    else if (e.PropertyName == nameof(LoopNode.RepeatCount))
                    {
                        RepeatCount = node.RepeatCount;
                    }
                    else if (e.PropertyName == nameof(LoopNode.StartIndex))
                    {
                        StartIndex = node.StartIndex;
                    }
                    else if (e.PropertyName == nameof(LoopNode.EndIndex))
                    {
                        EndIndex = node.EndIndex;
                    }
                    OnNodePropertyChanged(e.PropertyName ?? string.Empty);
                };
            }

            // Listen to input changes để update connection states
            foreach (var inputVm in Inputs)
            {
                inputVm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(InputItemViewModel.SelectedSourceNodeId) ||
                        e.PropertyName == nameof(InputItemViewModel.SelectedSourceOutputKey))
                    {
                        UpdateConnectionStatesInternal();
                    }
                };
            }
        }

        protected override string GetDefaultTitle() => "Loop";

        partial void OnIsLoopCountConnectedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsTextBoxesEnabled));
        }

        partial void OnIsLoopCountNumberTypeChanged(bool value)
        {
            OnPropertyChanged(nameof(IsTextBoxesEnabled));
        }

        partial void OnLoopTypeChanged(LoopType value)
        {
            UpdatePanelVisibility();
            // Reload inputs để filter "loopArray" khi LoopType thay đổi
            LoadInputs();
            // Re-subscribe to input changes
            foreach (var inputVm in Inputs)
            {
                inputVm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(InputItemViewModel.SelectedSourceNodeId) ||
                        e.PropertyName == nameof(InputItemViewModel.SelectedSourceOutputKey))
                    {
                        UpdateConnectionStatesInternal();
                    }
                };
            }

            // Sync title trong ViewModel nếu title hiện tại là title mặc định
            // Điều này đảm bảo title được hiển thị đúng khi LoopType thay đổi trong combobox
            var defaultTitles = new[] { "For Loop", "Repeat N Times", "For Each Array", "Loop" };
            if (defaultTitles.Contains(NodeTitle))
            {
                // Update title trong ViewModel để hiển thị title mặc định mới
                NodeTitle = value switch
                {
                    LoopType.ForLoop => "For Loop",
                    LoopType.RepeatN => "Repeat N Times",
                    LoopType.ForEachArray => "For Each Array",
                    _ => "Loop"
                };
            }
        }

        // ⚠️ CRITICAL: Public method để code-behind có thể gọi khi input thay đổi
        public void UpdateConnectionStates()
        {
            UpdateConnectionStatesInternal();
        }

        // Override LoadInputs để filter inputs dựa trên LoopType
        protected override void LoadInputs()
        {
            Inputs.Clear();
            // NOTE: Base ctor calls LoadInputs() before this derived ctor runs,
            // so _loopNode is not assigned yet here. Always use _node (set by base ctor).
            if (_node is not LoopNode loopNode) return;
            if (loopNode.DynamicInputs == null || loopNode.DynamicInputs.Count == 0) return;

            // Refresh AvailableSources cho tất cả inputs trước khi tạo InputItemViewModel
            // Quan trọng: Phải gọi để lấy upstream nodes chính xác (loại bỏ downstream và return paths)
            RefreshAvailableSourcesForInputs();

            foreach (var input in loopNode.DynamicInputs)
            {
                // Ẩn input "loopArray" nếu LoopType không phải ForEachArray
                if (input.Key == "loopArray" && LoopType != LoopType.ForEachArray)
                {
                    continue;
                }

                // Ẩn input "loopCount" nếu LoopType là ForEachArray
                if (input.Key == "loopCount" && LoopType == LoopType.ForEachArray)
                {
                    continue;
                }

                var inputVm = new InputItemViewModel(loopNode, input, _host);
                // Refresh AvailableSources trong InputItemViewModel sau khi refresh input.AvailableSources
                if (input.AvailableSources != null)
                {
                    inputVm.AvailableSources = new ObservableCollection<WorkflowDataSourceOption>(input.AvailableSources);
                }
                Inputs.Add(inputVm);

                // Listen to changes để update connection states
                inputVm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(InputItemViewModel.SelectedSourceNodeId) ||
                        e.PropertyName == nameof(InputItemViewModel.SelectedSourceOutputKey))
                    {
                        UpdateConnectionStatesInternal();
                    }
                };
            }
        }

        private void UpdateConnectionStatesInternal()
        {
            // Xử lý validation cho loopCount (ForLoop và RepeatN)
            var loopCountInputVm = Inputs.FirstOrDefault(i => i.Key == "loopCount");

            if (loopCountInputVm != null && !string.IsNullOrWhiteSpace(loopCountInputVm.SelectedSourceNodeId))
            {
                IsLoopCountConnected = true;

                // Lấy output option từ AvailableOutputKeyOptions của inputVm
                var selectedOutputKey = loopCountInputVm.SelectedSourceOutputKey;


                if (selectedOutputKey == null) return;

                // Tìm output option tương ứng với output key đã chọn
                var outputOption = !string.IsNullOrWhiteSpace(selectedOutputKey)
                    ? loopCountInputVm.AvailableOutputKeyOptions?
                        .FirstOrDefault(o => string.Equals(o.Key, selectedOutputKey, StringComparison.OrdinalIgnoreCase))
                    : null;

                var isNumberType = loopCountInputVm.Value.IsNumber();

                IsLoopCountNumberType = isNumberType;

                // Hiển thị error message nếu có kết nối nhưng không phải số
                if (!isNumberType)
                {
                    var outputTypeName = outputOption?.Type?.ToString() ?? "không xác định";
                    LoopCountErrorMessage = $"⚠️ Output của node kết nối phải là Integer hoặc Number, nhưng hiện tại là: {outputTypeName}";
                }
                else
                {
                    LoopCountErrorMessage = string.Empty;
                }

                // Notify IsTextBoxesEnabled changed
                OnPropertyChanged(nameof(IsTextBoxesEnabled));
            }
            else
            {
                IsLoopCountConnected = false;
                IsLoopCountNumberType = false;
                LoopCountErrorMessage = string.Empty;
                // Notify IsTextBoxesEnabled changed
                OnPropertyChanged(nameof(IsTextBoxesEnabled));
            }

            // Xử lý validation cho loopArray (ForEachArray)
            if (LoopType == LoopType.ForEachArray)
            {
                var loopArrayInputVm = Inputs.FirstOrDefault(i => i.Key == "loopArray");

                if (loopArrayInputVm != null && !string.IsNullOrWhiteSpace(loopArrayInputVm.SelectedSourceNodeId))
                {
                    // Lấy output option từ AvailableOutputKeyOptions của inputVm
                    var selectedOutputKey = loopArrayInputVm.SelectedSourceOutputKey;

                    if (selectedOutputKey == null) return;

                    // Tìm output option tương ứng với output key đã chọn
                    var outputOption = !string.IsNullOrWhiteSpace(selectedOutputKey)
                        ? loopArrayInputVm.AvailableOutputKeyOptions?
                            .FirstOrDefault(o => string.Equals(o.Key, selectedOutputKey, StringComparison.OrdinalIgnoreCase))
                        : null;

                    // Kiểm tra xem output type có phải Array không
                    var isArrayType = outputOption?.Type == WorkflowDataType.ArrayString
                        || outputOption?.Type == WorkflowDataType.ArrayNumber
                        || outputOption?.Type == WorkflowDataType.ArrayDynamic;

                    // Hiển thị error message nếu có kết nối nhưng không phải mảng
                    if (!isArrayType)
                    {
                        var outputTypeName = outputOption?.Type?.ToString() ?? "không xác định";
                        LoopArrayErrorMessage = $"⚠️ Output của node kết nối phải là Array (ArrayString, ArrayNumber, hoặc ArrayDynamic), nhưng hiện tại là: {outputTypeName}";
                    }
                    else
                    {
                        LoopArrayErrorMessage = string.Empty;
                    }
                }
                else
                {
                    // Không có kết nối, không cần hiển thị error
                    LoopArrayErrorMessage = string.Empty;
                }
            }
            else
            {
                // Không phải ForEachArray, clear error message
                LoopArrayErrorMessage = string.Empty;
            }
        }

        private void UpdatePanelVisibility()
        {
            // Hiển thị input "loopArray" chỉ khi LoopType là ForEachArray
            ShowLoopArrayInput = LoopType == LoopType.ForEachArray;
        }

        // Override OnSaveTitle để thêm logic riêng thay vì override SaveTitle
        protected override void OnSaveTitle()
        {
            if (_loopNode.LoopType != LoopType)
            {
                _loopNode.LoopType = LoopType;
                _host.RequestSyncDataPanels(immediate: true);
            }

            if (_loopNode.RepeatCount != RepeatCount && RepeatCount >= 1)
            {
                _loopNode.RepeatCount = RepeatCount;
                _host.RequestSyncDataPanels(immediate: true);
            }

            if (_loopNode.StartIndex != StartIndex)
            {
                _loopNode.StartIndex = StartIndex;
                _host.RequestSyncDataPanels(immediate: true);
            }

            if (_loopNode.EndIndex != EndIndex)
            {
                _loopNode.EndIndex = EndIndex;
                _host.RequestSyncDataPanels(immediate: true);
            }

            // Rebuild DynamicOutputs từ ListOutNodes + CustomOutputMappings
            if (_host.ViewModel != null)
            {
                _loopNode.RebuildOutputsFromLoopBody(
                    _host.ViewModel.Connections.ToList(),
                    _host.ViewModel.Nodes);
            }
            _host.RequestSyncDataPanels(immediate: true);
        }

        /// <summary>
        /// Làm mới danh sách node trong body (gọi khi mở dialog hoặc khi connections thay đổi).
        /// </summary>
        public void RefreshBodyNodeOptions()
        {
            BodyNodeOptions.Clear();
            if (_host.ViewModel?.Connections == null || _host.ViewModel?.Nodes == null) return;

            var bodyNodes = _loopNode.GetLoopBodyClusterNodes(
                _host.ViewModel.Connections.ToList(),
                _host.ViewModel.Nodes);

            foreach (var n in bodyNodes.Where(n => n.DynamicOutputs != null && n.DynamicOutputs.Count > 0))
            {
                BodyNodeOptions.Add(CreateDataSourceOption(n));
            }
        }

    }

    /// <summary>
    /// Wrapper class để hiển thị LoopType trong ComboBox với text tùy chỉnh.
    /// </summary>
    public class LoopTypeOption
    {
        public LoopType Value { get; set; }
        public string DisplayName { get; set; }

        public LoopTypeOption(LoopType value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }
}
