using System.ComponentModel;
using System.Runtime.CompilerServices;
using FlowMy.Models;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Một cặp gán: từ (SourceNodeId, SourceOutputKey) sang (TargetNodeId, TargetKey).
    /// </summary>
    public sealed class AssignDataAssignment
    {
        public string SourceNodeId { get; set; } = string.Empty;
        public string SourceOutputKey { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string TargetKey { get; set; } = string.Empty;

        /// <summary>
        /// Nếu true: trước khi lấy giá trị sẽ chạy lại logic của node nguồn để lấy giá trị mới nhất
        /// (dùng khi node nguồn có giá trị cũ / cần refresh).
        /// </summary>
        public bool RefreshSourceBeforeUse { get; set; }
    }

    /// <summary>
    /// Node xử lý gán dữ liệu: lấy giá trị từ output của node nguồn và gán vào key của node đích.
    /// Ví dụ: node Input có key và value, khi dùng node Gán dữ liệu thì value của Input đồng bộ với dữ liệu gán.
    /// </summary>
    public sealed class AssignDataNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Danh sách gán: từ (SourceNodeId, SourceOutputKey) sang (TargetNodeId, TargetKey).
        /// </summary>
        public System.Collections.Generic.List<AssignDataAssignment> Assignments { get; } = new();

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

        public AssignDataNode()
        {
            Type = NodeType.AssignData;
            Title = "Gán dữ liệu";

            // Flow: input (trái), output (phải)
            Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"           // Port IN: dùng màu Info theo guideline
            });
            Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"   // Port OUT: dùng màu SunsetOrange theo guideline
            });
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Method helper để notify PropertyChanged khi Title thay đổi từ bên ngoài
        /// (ví dụ: từ ViewModel hoặc khi copy node).
        /// </summary>
        public void NotifyTitleChanged()
        {
            OnPropertyChanged(nameof(Title));
        }
    }
}
