using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using FlowMy.Models.Nodes;

namespace FlowMy.Models
{
    public enum LoopType
    {
        ForLoop,
        RepeatN,
        ForEachArray
    }

    /// <summary>
    /// Custom output của Loop: lấy giá trị từ (SourceNodeId, SourceOutputKey) trong body để expose ra output của Loop.
    /// </summary>
    public sealed class LoopCustomOutputMapping
    {
        public string SourceNodeId { get; set; } = string.Empty;
        public string SourceOutputKey { get; set; } = string.Empty;
        /// <summary>Key hiển thị cho output này trên Loop (mặc định = SourceOutputKey).</summary>
        public string OutputKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// Gán dữ liệu trong vòng lặp: từ (SourceNodeId, SourceOutputKey) sang (TargetNodeId, TargetKey).
    /// </summary>
    public sealed class LoopDataAssignment
    {
        public string SourceNodeId { get; set; } = string.Empty;
        public string SourceOutputKey { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string TargetKey { get; set; } = string.Empty;
    }

    public sealed class LoopNode : WorkflowNode, INotifyPropertyChanged
    {
        private LoopType _loopType = LoopType.RepeatN;
        private int _repeatCount = 5;
        private int _startIndex = 0;
        private int _endIndex = 10;
        private string _arrayInputKey = "array";
        private WorkflowDataType _inputType = WorkflowDataType.Integer;
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;

        /// <summary>
        /// Output tùy chỉnh: chọn node + key trong body để expose ra output của Loop (dùng trong loop body).
        /// </summary>
        public System.Collections.Generic.List<LoopCustomOutputMapping> CustomOutputMappings { get; } = new();

        /// <summary>
        /// Gán dữ liệu trong mỗi iteration: từ (SourceNodeId, SourceOutputKey) sang (TargetNodeId, TargetKey).
        /// </summary>
        public System.Collections.Generic.List<LoopDataAssignment> DataAssignments { get; } = new();

        public LoopNode()
        {
            Type = NodeType.Loop;
            Title = "Loop";

            // ✅ Tạo Loop Body Node
            LoopBodyNode = new LoopBodyNode
            {
                Id = $"LoopBody_{Guid.NewGuid()}",
                Title = "Loop Body",
                ParentLoopNode = this
            };

            // ✅ Thêm 2 DynamicInputs cho Chrome input
            // Input 1: Integer - cho số lần lặp (dùng cho ForLoop và RepeatN)
            DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "loopCount",
                DisplayName = "Số lần lặp",
                ConvertType = WorkflowDataType.Integer
            });

            // Input 2: Array - cho mảng dữ liệu (dùng cho ForEachArray)
            DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "loopArray",
                DisplayName = "Mảng dữ liệu",
                ConvertType = WorkflowDataType.ArrayDynamic // Default, sẽ được user chọn trong UI
            });

            // ✅ Thêm DynamicOutput cho item hiện tại (dùng cho ForEachArray)
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "item",
                DisplayName = "Item",
                IsMultiple = false,
                OutputType = WorkflowDataType.String
            });

            // ✅ Output index (dùng cho For/Repeat/ForEach) - để downstream nodes resolve được kiểu
            // (TemplateFactory cũng có thể thêm output "index" cho các workflow cũ, nên chỉ add nếu chưa có)
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "index",
                DisplayName = "Loop Index",
                IsMultiple = false,
                OutputType = WorkflowDataType.Integer
            });
        }

        /// <summary>
        /// Node đại diện cho Loop Body (vùng chứa logic)
        /// </summary>
        public LoopBodyNode LoopBodyNode { get; }

        public LoopType LoopType
        {
            get => _loopType;
            set
            {
                if (_loopType != value)
                {
                    _loopType = value;
                    UpdatePortsVisibility(); // ✅ Cập nhật visibility của ports ngay lập tức
                    OnPropertyChanged();
                    UpdateTitle();

                }
            }
        }

        private void UpdatePortsVisibility()
        {
            var indexPort = Ports.FirstOrDefault(p => p.Id == "LoopIndexOut");

            if (indexPort != null)
            {
                indexPort.IsVisible = (_loopType == LoopType.ForLoop || _loopType == LoopType.RepeatN || _loopType == LoopType.ForEachArray);
            }

            // Cập nhật visibility của DynamicInputs dựa trên LoopType
            UpdateInputsVisibility();
        }

        private void UpdateInputsVisibility()
        {
            // Logic này không cần thiết nữa vì NodeChrome đã filter inputs dựa trên LoopType
            // Giữ lại để tương lai có thể dùng nếu cần
        }

        public int RepeatCount
        {
            get => _repeatCount;
            set
            {
                if (_repeatCount != value && value >= 1)
                {
                    _repeatCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public Border? ContainerBorder { get; set; }

        // Container properties moved to LoopBodyNode
        // public double ContainerWidth ... 
        // public double ContainerHeight ...
        // public double ContainerOffsetX ...
        // public double ContainerOffsetY ...

        /// <summary>
        /// Connection mặc định giữa Loop Node Bottom và Loop Body Top (không được xóa)
        /// </summary>
        public WorkflowConnection? DefaultConnection { get; set; }

        /// <summary>
        /// Cache các ListOutNodes trong LoopBody để resolve value nhanh hơn.
        /// Được cập nhật khi gọi RebuildOutputsFromLoopBody().
        /// </summary>
        public System.Collections.Generic.List<ListOutNode>? CachedListOutNodes { get; set; }

        public int StartIndex
        {
            get => _startIndex;
            set
            {
                if (_startIndex != value)
                {
                    _startIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public int EndIndex
        {
            get => _endIndex;
            set
            {
                if (_endIndex != value)
                {
                    _endIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ArrayInputKey
        {
            get => _arrayInputKey;
            set
            {
                if (_arrayInputKey != value)
                {
                    _arrayInputKey = value ?? "array";
                    OnPropertyChanged();
                }
            }
        }

        public WorkflowDataType InputType
        {
            get => _inputType;
            set
            {
                if (_inputType != value)
                {
                    _inputType = value;
                    OnPropertyChanged();
                }
            }
        }

        private void UpdateInputType()
        {
            // Không cần update nữa vì có 2 inputs riêng biệt
        }

        private void UpdateTitle()
        {
            // Chỉ update title nếu title hiện tại là một trong các title mặc định
            // Điều này giữ lại title đã được user chỉnh sửa
            var defaultTitles = new[]
            {
                "For Loop",
                "Repeat N Times",
                "For Each Array",
                "Loop"
            };

            // Nếu title hiện tại là title mặc định, thì update theo LoopType mới
            // Nếu title đã được user chỉnh sửa (không phải title mặc định), giữ nguyên
            if (defaultTitles.Contains(Title))
            {
                Title = LoopType switch
                {
                    LoopType.ForLoop => "For Loop",
                    LoopType.RepeatN => "Repeat N Times",
                    LoopType.ForEachArray => "For Each Array",
                    _ => "Loop"
                };
            }
        }

        /// <summary>
        /// Chế độ hiển thị tiêu đề của node (mặc định Always).
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

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Method helper để notify PropertyChanged khi Title thay đổi từ bên ngoài
        /// (ví dụ: từ ViewModel hoặc khi copy node)
        /// </summary>
        public void NotifyTitleChanged()
        {
            OnPropertyChanged(nameof(Title));
        }

        /// <summary>
        /// Rebuild DynamicOutputs từ ListOutNodes trong LoopBody.
        /// Tìm tất cả ListOutNode trong LoopBody và thêm các outputs của chúng vào LoopNode.
        /// </summary>
        /// <param name="connections">List of connections để tìm nodes trong LoopBody</param>
        /// <param name="allNodes">List of all nodes trong workflow (để tìm ListOutNodes)</param>
        public void RebuildOutputsFromLoopBody(System.Collections.Generic.List<WorkflowConnection> connections, System.Collections.Generic.IEnumerable<WorkflowNode> allNodes)
        {
            if (LoopBodyNode == null) return;

            // Tìm tất cả nodes trong LoopBody cluster
            var bodyNodes = GetLoopBodyClusterNodes(connections, allNodes);

            // Tìm tất cả ListOutNodes trong LoopBody và cache lại
            var listOutNodes = bodyNodes.OfType<ListOutNode>().ToList();
            CachedListOutNodes = listOutNodes;

            // Lấy các outputs mặc định (index, item) - giữ lại
            var defaultOutputKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "index", "item" };
            var existingDefaultOutputs = DynamicOutputs
                .Where(o => defaultOutputKeys.Contains(o.Key))
                .ToList();

            // Clear tất cả outputs (sẽ thêm lại default outputs sau)
            DynamicOutputs.Clear();

            // Thêm lại các default outputs
            foreach (var output in existingDefaultOutputs)
            {
                DynamicOutputs.Add(output);
            }

            // Thêm outputs từ tất cả ListOutNodes trong LoopBody
            foreach (var listOutNode in listOutNodes)
            {
                if (listOutNode.DynamicOutputs == null) continue;

                foreach (var output in listOutNode.DynamicOutputs)
                {
                    // Tránh duplicate key (ưu tiên giữ output đầu tiên nếu có duplicate)
                    if (DynamicOutputs.Any(o => string.Equals(o.Key, output.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // Tạo copy của output để thêm vào LoopNode
                    var outputType = output.OutputType ?? output.ConvertType;
                    DynamicOutputs.Add(new WorkflowDynamicDataPort
                    {
                        Key = output.Key,
                        DisplayName = output.DisplayName ?? output.Key,
                        OutputType = outputType,
                        ConvertType = outputType,
                        IsMultiple = output.IsMultiple,
                        IsUserAdded = true
                    });
                }
            }

            // Thêm outputs từ CustomOutputMappings (dialog: button + chọn node + key trong body)
            foreach (var mapping in CustomOutputMappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.SourceNodeId) || string.IsNullOrWhiteSpace(mapping.SourceOutputKey)) continue;
                var outKey = string.IsNullOrWhiteSpace(mapping.OutputKey) ? mapping.SourceOutputKey : mapping.OutputKey;
                if (DynamicOutputs.Any(o => string.Equals(o.Key, outKey, StringComparison.OrdinalIgnoreCase))) continue;

                DynamicOutputs.Add(new WorkflowDynamicDataPort
                {
                    Key = outKey,
                    DisplayName = outKey,
                    OutputType = WorkflowDataType.String,
                    ConvertType = WorkflowDataType.String,
                    IsMultiple = false,
                    IsUserAdded = true,
                    SelectedSourceNodeId = mapping.SourceNodeId,
                    SelectedSourceOutputKey = mapping.SourceOutputKey
                });
            }

            // Notify property changed để UI có thể refresh
            OnPropertyChanged(nameof(DynamicOutputs));
        }

        /// <summary>
        /// Lấy toàn bộ nodes nằm trong LoopBody cluster: tất cả nodes được kết nối
        /// (trực tiếp hoặc gián tiếp) với LoopBodyNode, bỏ qua LoopNode cha.
        /// Public để dialog có thể lấy danh sách node trong body cho combobox.
        /// </summary>
        public System.Collections.Generic.List<WorkflowNode> GetLoopBodyClusterNodes(
            System.Collections.Generic.List<WorkflowConnection> connections,
            System.Collections.Generic.IEnumerable<WorkflowNode> allNodes)
        {
            var result = new System.Collections.Generic.List<WorkflowNode>();
            var body = LoopBodyNode;
            if (body == null) return result;

            var visited = new System.Collections.Generic.HashSet<WorkflowNode> { body };
            var queue = new System.Collections.Generic.Queue<WorkflowNode>();
            queue.Enqueue(body);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                var neighbors = connections
                    .Where(c => c.FromNode == current || c.ToNode == current)
                    .Select(c => c.FromNode == current ? c.ToNode : c.FromNode)
                    .Where(n => n != null);

                foreach (var neighbor in neighbors)
                {
                    // Bỏ qua LoopNode cha để không lan ra ngoài qua default connection
                    if (ReferenceEquals(neighbor, this)) continue;

                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Loại bỏ chính LoopBodyNode, chỉ trả về các node "bên trong" body
            visited.Remove(body);
            result.AddRange(visited);
            return result;
        }
    }
}