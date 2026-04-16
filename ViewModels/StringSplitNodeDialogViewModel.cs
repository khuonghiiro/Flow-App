using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class StringSplitNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly StringSplitNode _stringSplitNode;

        /// <summary>
        /// Expose node for code-behind access (e.g., color preview).
        /// </summary>
        public WorkflowNode Node => _stringSplitNode;

        [ObservableProperty]
        private string _regexPattern;

        [ObservableProperty]
        private string _outputKey;

        // ⚠️ Validation cho input: chỉ chấp nhận String
        [ObservableProperty]
        private string _inputErrorMessage = string.Empty;

        public ObservableCollection<TitleDisplayModeOption> TitleDisplayModeOptions { get; } = new()
        {
            new TitleDisplayModeOption(TitleDisplayMode.Hidden, "Ẩn tiêu đề"),
            new TitleDisplayModeOption(TitleDisplayMode.Hover, "Hiện khi hover"),
            new TitleDisplayModeOption(TitleDisplayMode.Always, "Luôn hiện")
        };

        public StringSplitNodeDialogViewModel(StringSplitNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _stringSplitNode = node ?? throw new ArgumentNullException(nameof(node));

            // Initialize properties từ node
            _regexPattern = node.RegexPattern;
            _outputKey = node.OutputKey;
            _inputErrorMessage = string.Empty;

            // ⚠️ CRITICAL: Combine ALL PropertyChanged handlers in ONE block
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(StringSplitNode.RegexPattern))
                    {
                        RegexPattern = _stringSplitNode.RegexPattern;
                    }
                    else if (e.PropertyName == nameof(StringSplitNode.OutputKey))
                    {
                        OutputKey = _stringSplitNode.OutputKey;
                    }

                    OnNodePropertyChanged(e.PropertyName ?? string.Empty);
                };
            }

            // Sync changes từ ViewModel về Node
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(RegexPattern) && _stringSplitNode.RegexPattern != RegexPattern)
                {
                    _stringSplitNode.RegexPattern = RegexPattern;
                    _host.RequestSyncDataPanels(immediate: true);
                }
                else if (e.PropertyName == nameof(OutputKey) && _stringSplitNode.OutputKey != OutputKey)
                {
                    _stringSplitNode.OutputKey = OutputKey;
                    _host.RequestSyncDataPanels(immediate: true);
                }
            };

            // Lắng nghe thay đổi trên Inputs để update validation
            foreach (var inputVm in Inputs)
            {
                inputVm.PropertyChanged += InputVmOnPropertyChanged;
            }
        }

        protected override string GetDefaultTitle() => "String Split";

        // ⚠️ Override OnSaveTitle để lưu thêm properties riêng
        // (Title và TitleDisplayMode đã được xử lý tự động trong base class)
        protected override void OnSaveTitle()
        {
            // Base SaveTitle() đã gán _node.Title = NodeTitle, ở đây chỉ cần notify để UI cập nhật
            _stringSplitNode.NotifyTitleChanged();

            // Sync properties từ ViewModel về Node
            if (_stringSplitNode.RegexPattern != RegexPattern)
            {
                _stringSplitNode.RegexPattern = RegexPattern;
                _host.RequestSyncDataPanels(immediate: true);
            }

            if (_stringSplitNode.OutputKey != OutputKey)
            {
                _stringSplitNode.OutputKey = OutputKey;
                _host.RequestSyncDataPanels(immediate: true);
            }
        }

        // ⚠️ CRITICAL: Nếu node có DynamicInputs, phải override LoadInputs()
        // và gọi RefreshAvailableSourcesForInputs() TRƯỚC KHI tạo InputItemViewModel
        protected override void LoadInputs()
        {
            Inputs.Clear();
            if (_node is not StringSplitNode stringSplitNode) return;
            if (stringSplitNode.DynamicInputs == null || stringSplitNode.DynamicInputs.Count == 0) return;

            // ⚠️ CRITICAL: Refresh AvailableSources để combobox hiển thị tiêu đề mới nhất
            // Quan trọng: Phải gọi để lấy upstream nodes chính xác (loại bỏ downstream và return paths)
            RefreshAvailableSourcesForInputs();

            foreach (var input in stringSplitNode.DynamicInputs)
            {
                var inputVm = new InputItemViewModel(stringSplitNode, input, _host);

                // ⚠️ CRITICAL: Update AvailableSources trong InputItemViewModel
                if (input.AvailableSources != null)
                {
                    inputVm.AvailableSources = new ObservableCollection<WorkflowDataSourceOption>(input.AvailableSources);
                }

                Inputs.Add(inputVm);

                // Lắng nghe thay đổi để cập nhật validation khi người dùng chọn source/key
                inputVm.PropertyChanged += InputVmOnPropertyChanged;
            }

            // Sau khi (re)load inputs, cập nhật lại trạng thái validate một lần
            UpdateInputValidationState();
        }

        /// <summary>
        /// Lắng nghe thay đổi SelectedSourceNodeId / SelectedSourceOutputKey trên từng InputItemViewModel
        /// để cập nhật validate type string cho input.
        /// </summary>
        private void InputVmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InputItemViewModel.SelectedSourceNodeId) ||
                e.PropertyName == nameof(InputItemViewModel.SelectedSourceOutputKey))
            {
                UpdateInputValidationState();
            }
        }

        /// <summary>
        /// Validate: input "inputString" chỉ chấp nhận output type String.
        /// Nếu type khác, hiển thị error message cho người dùng.
        /// </summary>
        private void UpdateInputValidationState()
        {
            // Giả định node chỉ có 1 dynamic input với key "inputString"
            var inputVm = Inputs.FirstOrDefault(i => i.Key == "inputString");

            if (inputVm == null || string.IsNullOrWhiteSpace(inputVm.SelectedSourceNodeId))
            {
                InputErrorMessage = string.Empty;
                return;
            }

            var selectedOutputKey = inputVm.SelectedSourceOutputKey;
            if (string.IsNullOrWhiteSpace(selectedOutputKey))
            {
                InputErrorMessage = string.Empty;
                return;
            }

            var outputOption = inputVm.AvailableOutputKeyOptions?
                .FirstOrDefault(o => string.Equals(o.Key, selectedOutputKey, StringComparison.OrdinalIgnoreCase));

            if (outputOption == null)
            {
                InputErrorMessage = string.Empty;
                return;
            }

            // Chỉ chấp nhận String (theo yêu cầu); các type khác → cảnh báo
            var isStringType = outputOption.Type == WorkflowDataType.String;

            if (!isStringType)
            {
                var outputTypeName = outputOption.Type?.ToString() ?? "không xác định";
                InputErrorMessage = $"⚠️ Output của node kết nối phải là String, nhưng hiện tại là: {outputTypeName}";
            }
            else
            {
                InputErrorMessage = string.Empty;
            }
        }
    }
}
