using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowMy.ViewModels
{
    public partial class InputItemViewModel : ObservableObject
    {
        private readonly WorkflowNode _node;
        private readonly WorkflowDynamicDataPort _input;
        private readonly IWorkflowEditorHost _host;

        [ObservableProperty]
        private string _key;

        [ObservableProperty]
        private string _value;

        [ObservableProperty]
        private ObservableCollection<WorkflowDataSourceOption>? _availableSources;

        [ObservableProperty]
        private string? _selectedSourceNodeId;

        [ObservableProperty]
        private ObservableCollection<WorkflowOutputKeyOption>? _availableOutputKeyOptions;

        [ObservableProperty]
        private string? _selectedSourceOutputKey;

        public InputItemViewModel(WorkflowNode node, WorkflowDynamicDataPort input, IWorkflowEditorHost host)
        {
            _node = node;
            _input = input;
            _host = host;

            Key = input.Key ?? "—";
            Value = "—";

            AvailableSources = input.AvailableSources != null
                ? new ObservableCollection<WorkflowDataSourceOption>(input.AvailableSources)
                : new ObservableCollection<WorkflowDataSourceOption>();
            SelectedSourceNodeId = input.SelectedSourceNodeId;

            // ✅ QUAN TRỌNG: Lưu SelectedSourceOutputKey trước khi RefreshOutputKeyOptions
            // để đảm bảo giá trị đã restore từ JSON không bị mất (kể cả khi user đã đổi tên key ở node nguồn)
            var savedSelectedSourceOutputKey = input.SelectedSourceOutputKey;

            NodeChrome.RefreshOutputKeyOptions(host, input);
            
            // ✅ QUAN TRỌNG: Restore SelectedSourceOutputKey sau khi RefreshOutputKeyOptions
            // Nếu RefreshOutputKeyOptions đã giữ lại giá trị thì không cần restore.
            // Nếu key cũ không còn (ví dụ user đổi tên output key ở node nguồn),
            // fallback sang key đầu tiên trong danh sách mới (thường là duy nhất).
            if (!string.IsNullOrWhiteSpace(savedSelectedSourceOutputKey))
            {
                // Kiểm tra xem giá trị đã được giữ lại chưa
                if (input.AvailableOutputKeyOptions != null &&
                    input.AvailableOutputKeyOptions.Any(k => string.Equals(k.Key, savedSelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase)))
                {
                    // Có trong keyOptions, đảm bảo SelectedSourceOutputKey được set đúng
                    if (string.IsNullOrWhiteSpace(input.SelectedSourceOutputKey) ||
                        !string.Equals(input.SelectedSourceOutputKey, savedSelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase))
                    {
                        input.SelectedSourceOutputKey = input.AvailableOutputKeyOptions
                            .First(k => string.Equals(k.Key, savedSelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase)).Key;
                    }
                }
                else
                {
                    // Không có trong keyOptions (ví dụ user đã đổi tên key ở node nguồn).
                    // Nếu chỉ có 1 key mới, tự động chọn key đó để giữ kết nối hợp lệ.
                    if (input.AvailableOutputKeyOptions != null && input.AvailableOutputKeyOptions.Count == 1)
                    {
                        input.SelectedSourceOutputKey = input.AvailableOutputKeyOptions[0].Key;
                    }
                    else
                    {
                        // Nhiều key hoặc không có key nào: giữ nguyên giá trị cũ, để user tự chọn lại.
                        input.SelectedSourceOutputKey = savedSelectedSourceOutputKey;
                    }
                }
            }

            AvailableOutputKeyOptions = input.AvailableOutputKeyOptions != null
                ? new ObservableCollection<WorkflowOutputKeyOption>(input.AvailableOutputKeyOptions)
                : new ObservableCollection<WorkflowOutputKeyOption>();
            SelectedSourceOutputKey = input.SelectedSourceOutputKey;

            UpdateValue();

            // Sync khi thay đổi
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedSourceNodeId))
                {
                    // ✅ QUAN TRỌNG: Lưu SelectedSourceOutputKey trước khi RefreshOutputKeyOptions
                    // để đảm bảo giá trị đã restore từ JSON không bị mất (kể cả khi user đã đổi tên key ở node nguồn)
                    var savedSelectedSourceOutputKey = _input.SelectedSourceOutputKey;

                    _input.SelectedSourceNodeId = SelectedSourceNodeId;
                    NodeChrome.RefreshOutputKeyOptions(_host, _input);
                    
                    // ✅ QUAN TRỌNG: Restore SelectedSourceOutputKey sau khi RefreshOutputKeyOptions
                    if (!string.IsNullOrWhiteSpace(savedSelectedSourceOutputKey))
                    {
                        if (_input.AvailableOutputKeyOptions != null &&
                            _input.AvailableOutputKeyOptions.Any(k => string.Equals(k.Key, savedSelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase)))
                        {
                            // Có trong keyOptions, đảm bảo SelectedSourceOutputKey được set đúng
                            if (string.IsNullOrWhiteSpace(_input.SelectedSourceOutputKey) ||
                                !string.Equals(_input.SelectedSourceOutputKey, savedSelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase))
                            {
                                _input.SelectedSourceOutputKey = _input.AvailableOutputKeyOptions
                                    .First(k => string.Equals(k.Key, savedSelectedSourceOutputKey, StringComparison.OrdinalIgnoreCase)).Key;
                            }
                        }
                        else
                        {
                            // Không có trong keyOptions (ví dụ user đã đổi tên key ở node nguồn).
                            // Nếu chỉ có 1 key mới, tự động chọn key đó để giữ kết nối hợp lệ.
                            if (_input.AvailableOutputKeyOptions != null && _input.AvailableOutputKeyOptions.Count == 1)
                            {
                                _input.SelectedSourceOutputKey = _input.AvailableOutputKeyOptions[0].Key;
                            }
                            else
                            {
                                // Nhiều key hoặc không có key nào: giữ nguyên giá trị cũ, để user tự chọn lại.
                                _input.SelectedSourceOutputKey = savedSelectedSourceOutputKey;
                            }
                        }
                    }

                    AvailableOutputKeyOptions = _input.AvailableOutputKeyOptions != null
                        ? new ObservableCollection<WorkflowOutputKeyOption>(_input.AvailableOutputKeyOptions)
                        : new ObservableCollection<WorkflowOutputKeyOption>();
                    SelectedSourceOutputKey = _input.SelectedSourceOutputKey;
                    UpdateValue();
                }
                else if (e.PropertyName == nameof(SelectedSourceOutputKey))
                {
                    _input.SelectedSourceOutputKey = SelectedSourceOutputKey;
                    UpdateValue();
                }
            };
        }

        private void UpdateValue()
        {
            var resolved = NodeChrome.GetInputResolvedValue(_host, _node, _input);
            Value = resolved;
        }
    }

}
