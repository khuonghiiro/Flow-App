// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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
    public class AsyncTaskBodySourceItemViewModel : INotifyPropertyChanged
    {
        private string? _sourceNodeId;
        private string? _sourceOutputKey;
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

    public class AsyncTaskBodyOutputMappingItemViewModel : INotifyPropertyChanged
    {
        private string? _outputKey;
        private bool _isCollectArray = true;
        public ObservableCollection<AsyncTaskBodySourceItemViewModel> Sources { get; } = new();

        public ICommand AddSourceCommand { get; }
        public ICommand RemoveSourceCommand { get; }

        public Func<string?, IEnumerable<OutputKeyOption>>? GetOutputKeysFunc { get; set; }

        public AsyncTaskBodyOutputMappingItemViewModel()
        {
            AddSourceCommand = new RelayCommand(AddSource);
            RemoveSourceCommand = new RelayCommand<AsyncTaskBodySourceItemViewModel>(RemoveSource);
        }

        public string? SourceNodeId
        {
            get => Sources.FirstOrDefault()?.SourceNodeId;
            set
            {
                if (Sources.Count == 0) AddSource();
                if (Sources.Count > 0) Sources[0].SourceNodeId = value;
                OnPropertyChanged();
            }
        }

        public string? SourceOutputKey
        {
            get => Sources.FirstOrDefault()?.SourceOutputKey;
            set
            {
                if (Sources.Count == 0) AddSource();
                if (Sources.Count > 0) Sources[0].SourceOutputKey = value;
                OnPropertyChanged();
            }
        }

        public void AddSource()
        {
            var item = new AsyncTaskBodySourceItemViewModel
            {
                GetOutputKeysFunc = GetOutputKeysFunc,
                SourceOutputKey = "output"
            };
            Sources.Add(item);
        }

        public void RemoveSource(AsyncTaskBodySourceItemViewModel? item)
        {
            if (item != null && Sources.Contains(item) && Sources.Count > 1)
            {
                Sources.Remove(item);
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

        private string _outputMappingsHeader = "📤 Output Mappings (0 items)";
        public string OutputMappingsHeader
        {
            get => _outputMappingsHeader;
            set
            {
                if (_outputMappingsHeader != value)
                {
                    _outputMappingsHeader = value;
                    OnPropertyChanged();
                }
            }
        }

        private void UpdateOutputMappingsHeader()
        {
            OutputMappingsHeader = $"📤 Output Mappings ({Mappings.Count} items)";
        }

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

            Mappings.CollectionChanged += (_, _) => UpdateOutputMappingsHeader();

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

            // Thêm các output key thuộc về node được chọn (lọc qua GetActiveOutputs để bỏ key unchecked)
            foreach (var dyn in GetActiveOutputs(targetNode))
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
            if (_parentAsyncTaskNode.BodyOutputMappings == null)
            {
                UpdateOutputMappingsHeader();
                return;
            }

            foreach (var m in _parentAsyncTaskNode.BodyOutputMappings)
            {
                var vm = new AsyncTaskBodyOutputMappingItemViewModel
                {
                    OutputKey = m.OutputKey,
                    IsCollectArray = m.IsCollectArray,
                    GetOutputKeysFunc = GetOutputKeysForNode
                };

                var effectiveSources = m.GetEffectiveSources().ToList();
                if (effectiveSources.Count > 0)
                {
                    foreach (var src in effectiveSources)
                    {
                        var srcVm = new AsyncTaskBodySourceItemViewModel
                        {
                            GetOutputKeysFunc = GetOutputKeysForNode,
                            SourceNodeId = src.SourceNodeId,
                            SourceOutputKey = src.SourceOutputKey
                        };
                        srcVm.UpdateAvailableOutputKeys();
                        vm.Sources.Add(srcVm);
                    }
                }
                else
                {
                    vm.AddSource();
                    if (vm.Sources.Count > 0)
                    {
                        vm.Sources[0].SourceNodeId = m.SourceNodeId;
                        vm.Sources[0].SourceOutputKey = m.SourceOutputKey;
                        vm.Sources[0].UpdateAvailableOutputKeys();
                    }
                }

                Mappings.Add(vm);
            }
            UpdateOutputMappingsHeader();
        }

        private void AddNewMapping()
        {
            var defaultSourceNodeId = InnerNodeOptions.FirstOrDefault()?.NodeId;
            var vm = new AsyncTaskBodyOutputMappingItemViewModel
            {
                OutputKey = $"result_{Mappings.Count + 1}",
                IsCollectArray = true,
                GetOutputKeysFunc = GetOutputKeysForNode
            };

            var srcVm = new AsyncTaskBodySourceItemViewModel
            {
                GetOutputKeysFunc = GetOutputKeysForNode,
                SourceNodeId = defaultSourceNodeId,
                SourceOutputKey = "output"
            };
            srcVm.UpdateAvailableOutputKeys();
            vm.Sources.Add(srcVm);

            Mappings.Add(vm);
            UpdateOutputMappingsHeader();
        }

        private void RemoveMapping(AsyncTaskBodyOutputMappingItemViewModel? item)
        {
            if (item != null)
            {
                Mappings.Remove(item);
                UpdateOutputMappingsHeader();
            }
        }

        public void SaveChanges()
        {
            _parentAsyncTaskNode.ReadResultsInBody = !WaitForAllThreads;
            _parentAsyncTaskNode.BodyOutputMappings.Clear();
            foreach (var vm in Mappings)
            {
                if (!string.IsNullOrWhiteSpace(vm.OutputKey))
                {
                    var mapping = new AsyncTaskBodyOutputMapping
                    {
                        OutputKey = vm.OutputKey.Trim(),
                        IsCollectArray = vm.IsCollectArray
                    };

                    foreach (var srcVm in vm.Sources)
                    {
                        if (!string.IsNullOrWhiteSpace(srcVm.SourceNodeId) && !string.IsNullOrWhiteSpace(srcVm.SourceOutputKey))
                        {
                            mapping.Sources.Add(new AsyncTaskBodySourceItem
                            {
                                SourceNodeId = srcVm.SourceNodeId.Trim(),
                                SourceOutputKey = srcVm.SourceOutputKey.Trim()
                            });
                        }
                    }

                    if (mapping.Sources.Count > 0)
                    {
                        // Sync primary SourceNodeId/SourceOutputKey from first source for backward compatibility
                        mapping.SourceNodeId = mapping.Sources[0].SourceNodeId;
                        mapping.SourceOutputKey = mapping.Sources[0].SourceOutputKey;
                        _parentAsyncTaskNode.BodyOutputMappings.Add(mapping);
                    }
                }
            }

            _parentAsyncTaskNode.SyncBodyOutputDynamicPorts();
        }
    }
}
