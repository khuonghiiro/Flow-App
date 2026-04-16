using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Represents a single output mapping configuration.
    /// Maps a key from a source node to a new key name.
    /// </summary>
    public class OutputMapping : INotifyPropertyChanged
    {
        private string _newKey = string.Empty;
        private string _sourceNodeId = string.Empty;
        private string _sourceOutputKey = string.Empty;

        /// <summary>
        /// The new key name for the output (can be renamed from original).
        /// </summary>
        public string NewKey
        {
            get => _newKey;
            set
            {
                if (_newKey != value)
                {
                    _newKey = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The ID of the source node to get value from.
        /// </summary>
        public string SourceNodeId
        {
            get => _sourceNodeId;
            set
            {
                if (_sourceNodeId != value)
                {
                    _sourceNodeId = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The output key from the source node.
        /// </summary>
        public string SourceOutputKey
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Node để lọc và đổi tên các output keys từ upstream nodes.
    /// Downstream nodes chỉ thấy các outputs được cấu hình trong ListOutNode, 
    /// không thấy các upstream nodes khác.
    /// 
    /// Ví dụ: A -> B -> C -> D -> ListOut -> Z -> L -> P
    /// - Node D thấy combobox A, B (C không có output)
    /// - Node Z chỉ thấy ListOut (không thấy A, B, D)
    /// - Node L thấy ListOut và Z (nếu Z có output)
    /// </summary>
    public sealed class ListOutNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;
        private List<OutputMapping> _outputMappings = new();

        public ListOutNode()
        {
            Type = NodeType.ListOut;
            Title = "List Out";

            // Initialize with empty mappings - user will configure via dialog
            // DynamicOutputs will be built dynamically based on OutputMappings
        }

        /// <summary>
        /// List of output mappings - each defines a key mapping from source node to new key.
        /// </summary>
        public List<OutputMapping> OutputMappings
        {
            get => _outputMappings;
            set
            {
                if (_outputMappings != value)
                {
                    _outputMappings = value ?? new List<OutputMapping>();
                    OnPropertyChanged();
                    // Rebuild DynamicOutputs when mappings change
                    RebuildDynamicOutputs();
                }
            }
        }

        /// <summary>
        /// Chế độ hiển thị tiêu đề (mặc định Always).
        /// </summary>
        public TitleDisplayMode TitleDisplayMode
        {
            get => _titleDisplayMode;
            set
            {
                if (_titleDisplayMode != value)
                {
                    _titleDisplayMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Chế độ màu sắc tiêu đề (mặc định NodeColor - theo màu node).
        /// </summary>
        public TitleColorMode TitleColorMode
        {
            get => _titleColorMode;
            set
            {
                if (_titleColorMode != value)
                {
                    _titleColorMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Key của màu tùy chọn cho tiêu đề (khi TitleColorMode = CustomColor).
        /// </summary>
        public string? TitleColorKey
        {
            get => _titleColorKey;
            set
            {
                if (_titleColorKey != value)
                {
                    _titleColorKey = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Runtime cache for resolved output values (không serialize).
        /// Key = NewKey, Value = resolved value from source node.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, object?> ResolvedOutputs { get; set; } = new();

        public new event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Helper để notify PropertyChanged khi Title thay đổi từ bên ngoài.
        /// </summary>
        public void NotifyTitleChanged()
        {
            OnPropertyChanged(nameof(Title));
        }

        /// <summary>
        /// Notify khi OutputMappings thay đổi để UI có thể refresh.
        /// </summary>
        public void NotifyOutputMappingsChanged()
        {
            OnPropertyChanged(nameof(OutputMappings));
            RebuildDynamicOutputs();
        }

        /// <summary>
        /// Rebuild DynamicOutputs từ OutputMappings.
        /// Được gọi khi OutputMappings thay đổi.
        /// </summary>
        public void RebuildDynamicOutputs()
        {
            DynamicOutputs.Clear();

            foreach (var mapping in _outputMappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.NewKey)) continue;

                DynamicOutputs.Add(new WorkflowDynamicDataPort
                {
                    Key = mapping.NewKey,
                    DisplayName = mapping.NewKey,
                    // Type will be determined at runtime from source
                    OutputType = WorkflowDataType.String, // Default, may be updated at runtime
                    IsUserAdded = true
                });
            }
        }

        /// <summary>
        /// Add a new output mapping.
        /// </summary>
        public void AddMapping(string newKey, string sourceNodeId, string sourceOutputKey)
        {
            _outputMappings.Add(new OutputMapping
            {
                NewKey = newKey,
                SourceNodeId = sourceNodeId,
                SourceOutputKey = sourceOutputKey
            });
            RebuildDynamicOutputs();
            OnPropertyChanged(nameof(OutputMappings));
        }

        /// <summary>
        /// Remove a mapping at specified index.
        /// </summary>
        public void RemoveMapping(int index)
        {
            if (index >= 0 && index < _outputMappings.Count)
            {
                _outputMappings.RemoveAt(index);
                RebuildDynamicOutputs();
                OnPropertyChanged(nameof(OutputMappings));
            }
        }

        /// <summary>
        /// Clear all mappings.
        /// </summary>
        public void ClearMappings()
        {
            _outputMappings.Clear();
            RebuildDynamicOutputs();
            OnPropertyChanged(nameof(OutputMappings));
        }
    }
}

