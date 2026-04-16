using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Node hiển thị thông báo dạng toast (giống React-Toastify).
    /// Lấy title/content/duration từ upstream nodes qua mapping.
    /// </summary>
    public sealed class NotificationNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;

        private InputVariable _titleInput = new() { VariableKey = "title" };
        private InputVariable _contentInput = new() { VariableKey = "content" };
        private InputVariable _durationInput = new() { VariableKey = "duration" };

        private string _staticTitle = string.Empty;
        private string _staticContent = string.Empty;

        private string? _toastTitleColorKey;
        private string? _toastContentColorKey;
        private string? _toastBackgroundColorKey;
        private double _toastBackgroundOpacity = 0.85;

        /// <summary>Thời gian hiển thị mặc định (giây) nếu không map được duration từ upstream.</summary>
        private int _defaultDurationSeconds = 5;

        public NotificationNode()
        {
            Type = NodeType.Notification;
            Title = "Notification";

            // 1 input + 1 output port (flow)
            Ports.Add(new NodePort
            {
                Id = System.Guid.NewGuid().ToString(),
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });

            Ports.Add(new NodePort
            {
                Id = System.Guid.NewGuid().ToString(),
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"
            });
        }

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

        /// <summary>Mapping cho tiêu đề thông báo.</summary>
        public InputVariable TitleInput
        {
            get => _titleInput;
            set
            {
                if (_titleInput != value)
                {
                    _titleInput = value ?? new InputVariable();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Mapping cho nội dung thông báo.</summary>
        public InputVariable ContentInput
        {
            get => _contentInput;
            set
            {
                if (_contentInput != value)
                {
                    _contentInput = value ?? new InputVariable();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Mapping cho thời gian hiển thị (giây).</summary>
        public InputVariable DurationInput
        {
            get => _durationInput;
            set
            {
                if (_durationInput != value)
                {
                    _durationInput = value ?? new InputVariable();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Thời gian hiển thị mặc định (giây).</summary>
        public int DefaultDurationSeconds
        {
            get => _defaultDurationSeconds;
            set
            {
                var clamped = value < 1 ? 1 : value;
                if (_defaultDurationSeconds != clamped)
                {
                    _defaultDurationSeconds = clamped;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Tiêu đề tĩnh dùng khi không map từ node khác.
        /// </summary>
        public string StaticTitle
        {
            get => _staticTitle;
            set
            {
                if (_staticTitle != value)
                {
                    _staticTitle = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Nội dung tĩnh dùng khi không map từ node khác.
        /// </summary>
        public string StaticContent
        {
            get => _staticContent;
            set
            {
                if (_staticContent != value)
                {
                    _staticContent = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Resource key màu tiêu đề toast (Brush).
        /// </summary>
        public string? ToastTitleColorKey
        {
            get => _toastTitleColorKey;
            set
            {
                if (_toastTitleColorKey != value)
                {
                    _toastTitleColorKey = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Resource key màu nội dung toast (Brush).
        /// </summary>
        public string? ToastContentColorKey
        {
            get => _toastContentColorKey;
            set
            {
                if (_toastContentColorKey != value)
                {
                    _toastContentColorKey = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Resource key màu nền toast (Brush).
        /// </summary>
        public string? ToastBackgroundColorKey
        {
            get => _toastBackgroundColorKey;
            set
            {
                if (_toastBackgroundColorKey != value)
                {
                    _toastBackgroundColorKey = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Độ trong suốt nền toast (0–1).
        /// </summary>
        public double ToastBackgroundOpacity
        {
            get => _toastBackgroundOpacity;
            set
            {
                var clamped = value;
                if (clamped < 0.0) clamped = 0.0;
                if (clamped > 1.0) clamped = 1.0;
                if (Math.Abs(_toastBackgroundOpacity - clamped) > double.Epsilon)
                {
                    _toastBackgroundOpacity = clamped;
                    OnPropertyChanged();
                }
            }
        }

        // TitleDisplayMode + TitleColorMode support
        public TextBlock? TitleTextBlockUI { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void NotifyTitleChanged()
        {
            OnPropertyChanged(nameof(Title));
        }
    }
}

