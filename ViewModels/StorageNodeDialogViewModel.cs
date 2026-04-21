using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class StorageOutputItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _key = string.Empty;

        [ObservableProperty]
        private string _value = string.Empty;
    }

    public partial class StorageNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly StorageNode _storageNode;
        private bool _isLoadingFromNode = false; // Flag để tránh reset khi load từ node

        public ObservableCollection<StorageOutputItemViewModel> StoredOutputs { get; } = new();
        public ObservableCollection<StorageOutputItemViewModel> DefaultOutputs { get; } = new();
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeys { get; } = new();

        [ObservableProperty]
        private string? _selectedSourceNodeId;

        [ObservableProperty]
        private string? _selectedSourceOutputKey;

        [ObservableProperty]
        private bool _isInputMode = true;

        public StorageNodeDialogViewModel(StorageNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _storageNode = node;

            // Load IsInputMode từ node
            IsInputMode = _storageNode.IsInputMode;

            // Load default outputs nếu có (khi IsInputMode = false)
            if (_storageNode.StoredOutputs.Count > 0)
            {
                foreach (var kv in _storageNode.StoredOutputs)
                {
                    DefaultOutputs.Add(new StorageOutputItemViewModel
                    {
                        Key = kv.Key,
                        Value = kv.Value ?? string.Empty
                    });
                }
            }

            RefreshAvailableNodes();
            
            // ✅ Load SourceNodeId và SourceOutputKey từ node
            _isLoadingFromNode = true; // Flag để tránh reset khi load
            try
            {
                if (!string.IsNullOrWhiteSpace(_storageNode.SourceNodeId))
                {
                    SelectedSourceNodeId = _storageNode.SourceNodeId;
                    RefreshAvailableOutputKeys();
                    
                    // Set SourceOutputKey (bao gồm cả empty string)
                    // Nếu SourceOutputKey là null/empty → chọn item "(Tất cả keys)"
                    SelectedSourceOutputKey = _storageNode.SourceOutputKey ?? string.Empty;
                }
                else
                {
                    // Nếu chưa chọn node nào, vẫn refresh keys để có item trống
                    RefreshAvailableOutputKeys();
                }
            }
            finally
            {
                _isLoadingFromNode = false;
            }

            // ✅ Refresh StoredOutputs preview dựa trên SourceOutputKey đã load
            RefreshStoredOutputsPreview();
        }

        protected override string GetDefaultTitle() => "Storage";

        private void RefreshAvailableNodes()
        {
            AvailableNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null) return;

            foreach (var n in _host.ViewModel.Nodes)
            {
                // Nếu IsInputMode = false (unchecked), chỉ hiển thị storage nodes khác
                if (!IsInputMode)
                {
                    if (!(n is StorageNode otherStorage) || otherStorage.Id == _storageNode.Id)
                        continue;
                    if (otherStorage.DynamicOutputs == null || otherStorage.DynamicOutputs.Count == 0)
                        continue;
                    AvailableNodeOptions.Add(CreateDataSourceOption(otherStorage));
                }
                else
                {
                    // IsInputMode = true (checked): logic như cũ - hiển thị tất cả nodes trừ storage node hiện tại
                    if (n is StorageNode) continue;
                    if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                    AvailableNodeOptions.Add(CreateDataSourceOption(n));
                }
            }
        }

        private void RefreshAvailableOutputKeys()
        {
            AvailableOutputKeys.Clear();

            // ✅ Thêm item trống để có thể bỏ chọn (lấy tất cả keys)
            AvailableOutputKeys.Add(new WorkflowOutputKeyOption
            {
                Key = string.Empty,
                Type = WorkflowDataType.String,
                DisplayName = "(Tất cả keys)"
            });

            if (string.IsNullOrWhiteSpace(SelectedSourceNodeId) || _host.ViewModel?.Nodes == null) return;

            var node = _host.ViewModel.Nodes.FirstOrDefault(n => string.Equals(n.Id, SelectedSourceNodeId, StringComparison.OrdinalIgnoreCase));
            
            // ✅ Nếu node là StorageNode (unchecked mode), lấy từ StoredOutputs
            if (node is StorageNode sourceStorage)
            {
                foreach (var kv in sourceStorage.StoredOutputs)
                {
                    if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                    
                    AvailableOutputKeys.Add(new WorkflowOutputKeyOption
                    {
                        Key = kv.Key,
                        Type = WorkflowDataType.String,
                        DisplayName = kv.Key
                    });
                }
            }
            // ✅ Nếu node thường, lấy từ DynamicOutputs
            else if (node?.DynamicOutputs != null)
            {
                foreach (var o in node.DynamicOutputs)
                {
                    var key = o.Key ?? string.Empty;
                    AvailableOutputKeys.Add(new WorkflowOutputKeyOption
                    {
                        Key = key,
                        Type = o.OutputType ?? o.ConvertType,
                        DisplayName = o.DisplayName ?? key
                    });
                }
            }
        }

        partial void OnSelectedSourceNodeIdChanged(string? value)
        {
            _storageNode.SourceNodeId = value;
            
            // ⚠️ CHỈ reset SourceOutputKey khi user thay đổi, KHÔNG reset khi load từ node
            if (!_isLoadingFromNode)
            {
                _storageNode.SourceOutputKey = null;
                SelectedSourceOutputKey = null;
            }
            
            RefreshAvailableOutputKeys();

            // Khi đổi node nguồn, hiển thị snapshot hiện tại (từ StoredOutputs) nếu có
            RefreshStoredOutputsPreview();
        }

        partial void OnSelectedSourceOutputKeyChanged(string? value)
        {
            // ✅ Nếu chọn item trống hoặc "(Tất cả keys)" → set null để lấy tất cả
            if (string.IsNullOrWhiteSpace(value))
            {
                _storageNode.SourceOutputKey = null;
            }
            else
            {
                _storageNode.SourceOutputKey = value;
            }

            // ✅ Refresh StoredOutputs preview dựa trên key đã chọn
            RefreshStoredOutputsPreview();
        }

        private void RefreshStoredOutputsPreview()
        {
            StoredOutputs.Clear();

            // ✅ Nếu chọn 1 key cụ thể → chỉ hiển thị key đó
            if (!string.IsNullOrWhiteSpace(_storageNode.SourceOutputKey))
            {
                if (_storageNode.StoredOutputs.TryGetValue(_storageNode.SourceOutputKey, out var value))
                {
                    StoredOutputs.Add(new StorageOutputItemViewModel
                    {
                        Key = _storageNode.SourceOutputKey,
                        Value = value ?? string.Empty
                    });
                }
            }
            // ✅ Nếu không chọn key (lấy tất cả) → hiển thị tất cả keys
            else
            {
                foreach (var kv in _storageNode.StoredOutputs)
                {
                    StoredOutputs.Add(new StorageOutputItemViewModel
                    {
                        Key = kv.Key,
                        Value = kv.Value ?? string.Empty
                    });
                }
            }
        }

        partial void OnIsInputModeChanged(bool value)
        {
            System.Diagnostics.Debug.WriteLine($"[StorageNodeDialogViewModel] OnIsInputModeChanged: {value}");
            _storageNode.IsInputMode = value;
            
            // Refresh available nodes khi đổi mode
            RefreshAvailableNodes();
            
            // Clear selection nếu cần
            if (!value && !string.IsNullOrWhiteSpace(SelectedSourceNodeId))
            {
                // Kiểm tra xem selected node có phải storage node không
                var selectedNode = _host.ViewModel?.Nodes?.FirstOrDefault(n => 
                    string.Equals(n.Id, SelectedSourceNodeId, StringComparison.OrdinalIgnoreCase));
                if (selectedNode is not StorageNode)
                {
                    SelectedSourceNodeId = null;
                    SelectedSourceOutputKey = null;
                }
            }
            
            // Trigger re-render ports
            _host.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[StorageNodeDialogViewModel] Dispatcher callback - updating ports for IsInputMode={value}");
                
                // ✅ FORCE XÓA TẤT CẢ port UI khỏi canvas trước
                foreach (var port in _storageNode.Ports)
                {
                    if (port.PortUI != null && _host.WorkflowCanvas != null)
                    {
                        if (_host.WorkflowCanvas.Children.Contains(port.PortUI))
                        {
                            _host.WorkflowCanvas.Children.Remove(port.PortUI);
                            System.Diagnostics.Debug.WriteLine($"[StorageNodeDialogViewModel] Removed port UI from canvas: IsInput={port.IsInput}");
                        }
                        port.PortUI = null; // Clear reference
                    }
                }
                
                // Update port visibility trực tiếp
                foreach (var port in _storageNode.Ports)
                {
                    bool shouldShowPort = _storageNode.IsInputMode 
                        ? port.IsInput 
                        : !port.IsInput;
                    port.IsVisible = shouldShowPort;
                    
                    System.Diagnostics.Debug.WriteLine($"[StorageNodeDialogViewModel] Setting port IsInput={port.IsInput} to IsVisible={shouldShowPort}");
                }
                
                // ✅ XÓA connections đến/từ ports bị ẩn
                if (_host.ViewModel != null)
                {
                    var connectionsToRemove = _host.ViewModel.Connections
                        .Where(c => 
                            // Connection từ port OUT của storage node (khi port OUT bị ẩn)
                            (c.FromNode == _storageNode && c.FromPort != null && !c.FromPort.IsVisible) ||
                            // Connection đến port IN của storage node (khi port IN bị ẩn)
                            (c.ToNode == _storageNode && c.ToPort != null && !c.ToPort.IsVisible))
                        .ToList();
                    
                    foreach (var conn in connectionsToRemove)
                    {
                        System.Diagnostics.Debug.WriteLine($"[StorageNodeDialogViewModel] Removing connection from {conn.FromNode?.Id} to {conn.ToNode?.Id}");
                        
                        // Xóa visuals trước
                        _host.ConnectionRenderer.RemoveConnectionVisuals(conn);
                        
                        // Sau đó xóa khỏi collection
                        _host.ViewModel.Connections.Remove(conn);
                    }
                }
                
                // Gọi UpdateNodePosition để trigger logic update port visibility trong StorageNodeRenderer
                _host.UpdateNodePosition(_storageNode, _storageNode.X, _storageNode.Y);
                
                // Re-render connections còn lại
                if (_host.ViewModel != null)
                {
                    var remainingConnections = _host.ViewModel.Connections
                        .Where(c => c.FromNode == _storageNode || c.ToNode == _storageNode)
                        .ToList();
                    
                    foreach (var conn in remainingConnections)
                    {
                        _host.RenderConnection(conn);
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            
            // Request sync để update data panels
            _host.RequestSyncDataPanels(immediate: true);
        }

        [RelayCommand]
        private void AddDefaultOutput()
        {
            DefaultOutputs.Add(new StorageOutputItemViewModel
            {
                Key = $"key{DefaultOutputs.Count + 1}",
                Value = string.Empty
            });
        }

        [RelayCommand]
        private void RemoveDefaultOutput(StorageOutputItemViewModel item)
        {
            if (item != null)
            {
                DefaultOutputs.Remove(item);
            }
        }

        protected override void OnSaveTitle()
        {
            // Sync title và IsInputMode ra node
            _storageNode.IsInputMode = IsInputMode;

            // ✅ Sync SelectedSourceNodeId và SelectedSourceOutputKey ra node
            _storageNode.SourceNodeId = SelectedSourceNodeId;
            _storageNode.SourceOutputKey = string.IsNullOrWhiteSpace(SelectedSourceOutputKey) 
                ? null 
                : SelectedSourceOutputKey;

            // ✅ Sync default outputs vào StoredOutputs khi IsInputMode = false (output mode)
            if (!IsInputMode)
            {
                _storageNode.StoredOutputs.Clear();
                _storageNode.DynamicOutputs.Clear();

                foreach (var item in DefaultOutputs)
                {
                    if (string.IsNullOrWhiteSpace(item.Key)) continue;

                    var key = item.Key.Trim();
                    _storageNode.StoredOutputs[key] = item.Value ?? string.Empty;

                    // Sync vào DynamicOutputs để hiển thị trong data panel
                    _storageNode.DynamicOutputs.Add(new WorkflowDynamicDataPort
                    {
                        Key = key,
                        DisplayName = key,
                        IsMultiple = false,
                        OutputType = WorkflowDataType.String,
                        UserValueOverride = item.Value ?? string.Empty
                    });
                }
            }

            _storageNode.NotifyTitleChanged();
            _host.RequestSyncDataPanels(immediate: true);
        }
    }
}

