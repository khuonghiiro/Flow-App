namespace FlowMy.Models
{
    /// <summary>
    /// Node Git Source — pull/clone repo từ remote, cấu hình icon/tooltip cho palette,
    /// khi thực thi sẽ mở VSCodium với source đã clone.
    /// </summary>
    public sealed class GitSourceNode : WorkflowNode
    {
        private string _repoUrl = string.Empty;
        private string _localPath = string.Empty;
        private string _branch = "main";
        private string _displayName = string.Empty;
        private string _iconKey = "git-alt brands";
        private string _tooltipText = string.Empty;
        private string _contextMenuDescription = string.Empty;
        private string _vscodiumPath = "vscodium";
        private string _lastCommitHash = string.Empty;
        private string _lastPullTime = string.Empty;
        private bool _autoOpenOnExecute = true;

        public GitSourceNode()
        {
            Type = NodeType.GitSource;
            Title = "Git Source";
            TitleDisplayMode = TitleDisplayMode.Always;

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

            // Default outputs
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "localPath",
                DisplayName = "Local Path",
                OutputType = WorkflowDataType.String
            });
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "branch",
                DisplayName = "Branch",
                OutputType = WorkflowDataType.String
            });
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "lastCommit",
                DisplayName = "Last Commit",
                OutputType = WorkflowDataType.String
            });
        }

        /// <summary>URL remote repository (HTTPS hoặc SSH).</summary>
        public string RepoUrl
        {
            get => _repoUrl;
            set { if (_repoUrl != value) { _repoUrl = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Đường dẫn local nơi clone/pull repo.</summary>
        public string LocalPath
        {
            get => _localPath;
            set { if (_localPath != value) { _localPath = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(IsGitRepoCloned)); } }
        }

        /// <summary>Branch cần checkout.</summary>
        public string Branch
        {
            get => _branch;
            set { if (_branch != value) { _branch = value ?? "main"; OnPropertyChanged(); } }
        }

        /// <summary>Tên hiển thị trên palette (nếu trống dùng Title).</summary>
        public string DisplayName
        {
            get => _displayName;
            set { if (_displayName != value) { _displayName = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Icon key cho node trên palette (dùng IconSelectorUserControl để chọn).</summary>
        public string IconKey
        {
            get => _iconKey;
            set { if (_iconKey != value) { _iconKey = value ?? "git-alt brands"; OnPropertyChanged(); } }
        }

        private string _iconColorKey = "White";
        /// <summary>Màu icon (để đảm bảo icon nhìn rõ trên nền node).</summary>
        public string IconColorKey
        {
            get => _iconColorKey;
            set { if (_iconColorKey != value) { _iconColorKey = value ?? "White"; OnPropertyChanged(); OnPropertyChanged(nameof(IconBrushResolved)); } }
        }

        /// <summary>Brush đã resolve cho icon — dùng trong palette DataTemplate binding.</summary>
        public System.Windows.Media.Brush IconBrushResolved
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_iconColorKey))
                    return System.Windows.Media.Brushes.White;

                // Try hex color
                if (_iconColorKey.StartsWith("#"))
                {
                    try
                    {
                        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_iconColorKey);
                        return new System.Windows.Media.SolidColorBrush(color);
                    }
                    catch { }
                }

                // Try named color
                if (_iconColorKey.Equals("White", System.StringComparison.OrdinalIgnoreCase))
                    return System.Windows.Media.Brushes.White;
                if (_iconColorKey.Equals("Black", System.StringComparison.OrdinalIgnoreCase))
                    return System.Windows.Media.Brushes.Black;

                // Try resource brush
                var brush = System.Windows.Application.Current?.TryFindResource($"{_iconColorKey}Brush") as System.Windows.Media.Brush
                         ?? System.Windows.Application.Current?.TryFindResource(_iconColorKey) as System.Windows.Media.Brush;
                return brush ?? System.Windows.Media.Brushes.White;
            }
        }

        /// <summary>Nội dung tooltip khi hover node trên palette.</summary>
        public string TooltipText
        {
            get => _tooltipText;
            set { if (_tooltipText != value) { _tooltipText = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Mô tả chi tiết hiện khi chuột phải vào node.</summary>
        public string ContextMenuDescription
        {
            get => _contextMenuDescription;
            set { if (_contextMenuDescription != value) { _contextMenuDescription = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Đường dẫn tới VSCodium executable.</summary>
        public string VscodiumPath
        {
            get => _vscodiumPath;
            set { if (_vscodiumPath != value) { _vscodiumPath = value ?? "vscodium"; OnPropertyChanged(); } }
        }

        /// <summary>Hash commit cuối cùng đã pull.</summary>
        public string LastCommitHash
        {
            get => _lastCommitHash;
            set { if (_lastCommitHash != value) { _lastCommitHash = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Thời gian pull cuối cùng (ISO 8601).</summary>
        public string LastPullTime
        {
            get => _lastPullTime;
            set { if (_lastPullTime != value) { _lastPullTime = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Tự động mở VSCodium khi node được thực thi.</summary>
        public bool AutoOpenOnExecute
        {
            get => _autoOpenOnExecute;
            set { if (_autoOpenOnExecute != value) { _autoOpenOnExecute = value; OnPropertyChanged(); } }
        }

        private string _commandText = string.Empty;
        /// <summary>Command line để chạy trong thư mục source.</summary>
        public string CommandText
        {
            get => _commandText;
            set { if (_commandText != value) { _commandText = value ?? string.Empty; OnPropertyChanged(); } }
        }

        private bool _isPartialClone;
        /// <summary>Bật/tắt chế độ partial clone (chỉ pull một phần của repo).</summary>
        public bool IsPartialClone
        {
            get => _isPartialClone;
            set { if (_isPartialClone != value) { _isPartialClone = value; OnPropertyChanged(); } }
        }

        private string _sparsePaths = string.Empty;
        /// <summary>Danh sách paths cho sparse checkout (mỗi dòng một đường dẫn).</summary>
        public string SparsePaths
        {
            get => _sparsePaths;
            set { if (_sparsePaths != value) { _sparsePaths = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Kiểm tra folder local có tồn tại .git hay không (đã clone).</summary>
        public bool IsGitRepoCloned
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_localPath)) return false;
                return System.IO.Directory.Exists(System.IO.Path.Combine(_localPath, ".git"));
            }
        }

        private bool _isRunning;
        /// <summary>Trạng thái đang chạy cmd (play/pause toggle).</summary>
        public bool IsRunning
        {
            get => _isRunning;
            set { if (_isRunning != value) { _isRunning = value; OnPropertyChanged(); } }
        }

        /// <summary>Gọi để cập nhật trạng thái clone trên UI.</summary>
        public void RefreshCloneStatus() => OnPropertyChanged(nameof(IsGitRepoCloned));

        public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));
    }
}
