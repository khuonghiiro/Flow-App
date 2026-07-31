using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;

namespace FlowMy.ViewModels
{
    public class AsyncTaskBodyOutputMappingItemViewModel : INotifyPropertyChanged
    {
        private string? _sourceNodeId;
        private string? _sourceOutputKey;
        private string? _outputKey;
        private bool _isCollectArray = true;
        private ObservableCollection<OutputKeyOption> _availableOutputKeys = new();

        public string? SourceNodeId
        {
            get => _sourceNodeId;
            set
            {
                if (_sourceNodeId != value)
                {
                    _sourceNodeId = value;
                    OnPropertyChanged();
                    UpdateAvailableOutputKeys();
                    if (AvailableOutputKeys.Count > 0 &&
                        (string.IsNullOrWhiteSpace(_sourceOutputKey) ||
                         !AvailableOutputKeys.Any(k => string.Equals(k.Key, _sourceOutputKey, StringComparison.OrdinalIgnoreCase))))
                    {
                        SourceOutputKey = AvailableOutputKeys[0].Key;
                    }
                }
            }
        }

        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set
            {
                if (_sourceOutputKey != value)
                {
                    _sourceOutputKey = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? OutputKey
        {
            get => _outputKey;
            set
            {
                if (_outputKey != value)
                {
                    _outputKey = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsCollectArray
        {
            get => _isCollectArray;
            set
            {
                if (_isCollectArray != value)
                {
                    _isCollectArray = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<OutputKeyOption> AvailableOutputKeys
        {
            get => _availableOutputKeys;
            set
            {
                _availableOutputKeys = value;
                OnPropertyChanged();
            }
        }

        public Func<string?, IEnumerable<OutputKeyOption>>? GetOutputKeysFunc { get; set; }

        public void UpdateAvailableOutputKeys()
        {
            AvailableOutputKeys.Clear();
            if (GetOutputKeysFunc != null && !string.IsNullOrWhiteSpace(SourceNodeId))
            {
                foreach (var opt in GetOutputKeysFunc(SourceNodeId))
                {
                    AvailableOutputKeys.Add(opt);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class BodyNodeOption
    {
        public string NodeId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    public class OutputKeyOption
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class AsyncTaskBodyDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly AsyncTaskNode _parentAsyncTaskNode;
        private readonly AsyncTaskBodyNode _bodyNode;

        public ObservableCollection<WorkflowDataSourceOption> InnerNodeOptions { get; } = new();
        public ObservableCollection<AsyncTaskBodyOutputMappingItemViewModel> Mappings { get; } = new();

        public ICommand AddMappingCommand { get; }
        public ICommand RemoveMappingCommand { get; }

        private bool _waitForAllThreads;
        public bool WaitForAllThreads
        {
            get => _waitForAllThreads;
            set
            {
                if (_waitForAllThreads != value)
                {
                    _waitForAllThreads = value;
                    OnPropertyChanged();
                }
            }
        }

        public AsyncTaskBodyDialogViewModel(AsyncTaskBodyNode bodyNode, IWorkflowEditorHost host)
            : base(bodyNode, host)
        {
            _bodyNode = bodyNode ?? throw new ArgumentNullException(nameof(bodyNode));
            _parentAsyncTaskNode = bodyNode.ParentAsyncTaskNode ?? throw new ArgumentNullException(nameof(bodyNode.ParentAsyncTaskNode));

            AddMappingCommand = new RelayCommand(AddNewMapping);
            RemoveMappingCommand = new RelayCommand<AsyncTaskBodyOutputMappingItemViewModel>(RemoveMapping);

            _waitForAllThreads = !_parentAsyncTaskNode.ReadResultsInBody;

            LoadInnerNodes();
            LoadMappings();
        }

        protected override string GetDefaultTitle() => "Async Task Body";

        private void LoadInnerNodes()
        {
            InnerNodeOptions.Clear();
            if (_host?.ViewModel?.Nodes == null) return;

            // Find all nodes that are placed inside the AsyncTaskBody bounds or connected within it
            var innerNodes = _host.ViewModel.Nodes
                .Where(n => n != _bodyNode && n != _parentAsyncTaskNode && IsNodeInsideBody(n))
                .OrderBy(n => n.Title)
                .ToList();

            // Fallback: if bounds checking finds none, offer all nodes in host
            if (innerNodes.Count == 0)
            {
                innerNodes = _host.ViewModel.Nodes
                    .Where(n => n != _bodyNode && n != _parentAsyncTaskNode)
                    .OrderBy(n => n.Title)
                    .ToList();
            }

            foreach (var node in innerNodes)
            {
                InnerNodeOptions.Add(CreateDataSourceOption(node));
            }
        }

        private bool IsNodeInsideBody(WorkflowNode node)
        {
            if (_bodyNode == null) return false;
            // Check canvas coordinates overlap with AsyncTaskBody bounds
            double bodyLeft = _bodyNode.X;
            double bodyRight = _bodyNode.X + _bodyNode.Width;
            double bodyTop = _bodyNode.Y;
            double bodyBottom = _bodyNode.Y + _bodyNode.Height;

            return (node.X >= bodyLeft && node.X <= bodyRight &&
                    node.Y >= bodyTop && node.Y <= bodyBottom);
        }

        private IEnumerable<OutputKeyOption> GetOutputKeysForNode(string? nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || _host?.ViewModel?.Nodes == null)
                yield break;

            var targetNode = _host.ViewModel.Nodes.FirstOrDefault(n => string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (targetNode == null) yield break;

            EnsureNodeDynamicOutputsSynced(targetNode);

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Thêm các output key thuộc về node được chọn, loại bỏ các key ghép có ngoặc [...]
            if (targetNode.DynamicOutputs != null)
            {
                foreach (var dyn in targetNode.DynamicOutputs)
                {
                    var key = dyn.Key?.Trim();
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    // Bỏ các key ghép dạng bracket indexing / property (ví dụ: data[0], item[field])
                    if (key.Contains('[') || key.Contains(']')) continue;

                    if (keys.Add(key))
                    {
                        yield return new OutputKeyOption
                        {
                            Key = key,
                            DisplayName = !string.IsNullOrWhiteSpace(dyn.DisplayName) ? $"{key} ({dyn.DisplayName})" : key
                        };
                    }
                }
            }

            // Đảm bảo key mặc định "output" luôn sẵn có nếu chưa được thêm
            if (keys.Add("output"))
            {
                yield return new OutputKeyOption
                {
                    Key = "output",
                    DisplayName = "output (Mặc định)"
                };
            }
        }

        private void LoadMappings()
        {
            Mappings.Clear();
            if (_parentAsyncTaskNode.BodyOutputMappings == null) return;

            foreach (var m in _parentAsyncTaskNode.BodyOutputMappings)
            {
                var vm = new AsyncTaskBodyOutputMappingItemViewModel
                {
                    SourceNodeId = m.SourceNodeId,
                    SourceOutputKey = m.SourceOutputKey,
                    OutputKey = m.OutputKey,
                    IsCollectArray = m.IsCollectArray,
                    GetOutputKeysFunc = GetOutputKeysForNode
                };
                vm.UpdateAvailableOutputKeys();
                Mappings.Add(vm);
            }
        }

        private void AddNewMapping()
        {
            var defaultSourceNodeId = InnerNodeOptions.FirstOrDefault()?.NodeId;
            var vm = new AsyncTaskBodyOutputMappingItemViewModel
            {
                SourceNodeId = defaultSourceNodeId,
                SourceOutputKey = "output",
                OutputKey = $"result_{Mappings.Count + 1}",
                IsCollectArray = true,
                GetOutputKeysFunc = GetOutputKeysForNode
            };
            vm.UpdateAvailableOutputKeys();
            Mappings.Add(vm);
        }

        private void RemoveMapping(AsyncTaskBodyOutputMappingItemViewModel? item)
        {
            if (item != null)
            {
                Mappings.Remove(item);
            }
        }

        public void SaveChanges()
        {
            _parentAsyncTaskNode.ReadResultsInBody = !WaitForAllThreads;
            _parentAsyncTaskNode.BodyOutputMappings.Clear();
            foreach (var vm in Mappings)
            {
                if (!string.IsNullOrWhiteSpace(vm.SourceNodeId) &&
                    !string.IsNullOrWhiteSpace(vm.SourceOutputKey) &&
                    !string.IsNullOrWhiteSpace(vm.OutputKey))
                {
                    _parentAsyncTaskNode.BodyOutputMappings.Add(new AsyncTaskBodyOutputMapping
                    {
                        SourceNodeId = vm.SourceNodeId.Trim(),
                        SourceOutputKey = vm.SourceOutputKey.Trim(),
                        OutputKey = vm.OutputKey.Trim(),
                        IsCollectArray = vm.IsCollectArray
                    });
                }
            }

            _parentAsyncTaskNode.SyncBodyOutputDynamicPorts();
        }
    }
}
