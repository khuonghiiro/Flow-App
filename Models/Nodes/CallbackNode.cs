using FlowMy.Models;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.Nodes
{
    public enum CallbackFlowBehavior
    {
        JumpOnly = 0,
        JumpThenContinue = 1
    }

    /// <summary>
    /// Node callback - cho phép chạy lại workflow từ một node đã chọn.
    /// Node này chỉ có input port, không có output port.
    /// Khi logic chạy vào node này, nó sẽ callback về node đã chọn để chạy lại workflow từ node đó.
    /// </summary>
    public sealed class CallbackNode : WorkflowNode, INotifyPropertyChanged
    {
        // ===== PROPERTIES =====

        /// <summary>
        /// ID của node cần callback (chạy lại workflow từ node này)
        /// </summary>
        private string _targetNodeId = string.Empty;
        public string TargetNodeId
        {
            get => _targetNodeId;
            set
            {
                if (_targetNodeId != value)
                {
                    _targetNodeId = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Số lần tối đa callback được phép trong 1 luồng (mặc định 3)
        /// </summary>
        private int _maxCallbackCount = 3;
        public int MaxCallbackCount
        {
            get => _maxCallbackCount;
            set
            {
                if (_maxCallbackCount != value)
                {
                    _maxCallbackCount = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Cách node callback điều phối flow:
        /// - JumpOnly: chỉ callback về target node
        /// - JumpThenContinue: callback xong thì chạy tiếp các node nối ra từ callback
        /// </summary>
        private CallbackFlowBehavior _flowBehavior = CallbackFlowBehavior.JumpOnly;
        public CallbackFlowBehavior FlowBehavior
        {
            get => _flowBehavior;
            set
            {
                if (_flowBehavior != value)
                {
                    _flowBehavior = value;
                    SyncPortsForBehavior();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// TitleDisplayMode: Hidden / Hover / Always
        /// </summary>
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
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
        /// TitleColorMode: NodeColor / CustomColor
        /// </summary>
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
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

        private string? _titleColorKey;
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
        /// Reference tới UI element (dùng bởi NodeControl và Renderer)
        /// </summary>
        public System.Windows.Controls.TextBlock? TitleTextBlockUI { get; set; }

        // ===== CONSTRUCTOR =====

        public CallbackNode()
        {
            Type = NodeType.Callback;
            Title = "Callback";

            // Khởi tạo properties
            TargetNodeId = string.Empty;
            MaxCallbackCount = 3;
            FlowBehavior = CallbackFlowBehavior.JumpOnly;

            EnsurePorts();
            SyncPortsForBehavior();
        }

        public void EnsurePorts()
        {
            if (!Ports.Any(p => p.IsInput))
            {
                Ports.Add(new NodePort
                {
                    Id = Guid.NewGuid().ToString(),
                    IsInput = true,
                    Position = PortPosition.Left,
                    IsVisible = true,
                    ColorKey = "Info"
                });
            }

            if (!Ports.Any(p => !p.IsInput))
            {
                Ports.Add(new NodePort
                {
                    Id = Guid.NewGuid().ToString(),
                    IsInput = false,
                    Position = PortPosition.Right,
                    IsVisible = false,
                    ColorKey = "SunsetOrange"
                });
            }
        }

        public void SyncPortsForBehavior()
        {
            EnsurePorts();

            var outputPort = Ports.FirstOrDefault(p => !p.IsInput);
            if (outputPort != null)
            {
                outputPort.IsVisible = FlowBehavior == CallbackFlowBehavior.JumpThenContinue;
            }
        }

        // ===== INOTIFYPROPERTYCHANGED =====

        public new event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Notify Title changed (dùng khi Title bị thay đổi từ bên ngoài)
        /// </summary>
        public void NotifyTitleChanged()
        {
            OnPropertyChanged(nameof(Title));
        }
    }
}
