using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace FlowMy.Models.Nodes
{
    public sealed class EmbedApplicationNode : WorkflowNode
    {
        private string _processName = string.Empty;
        private int _processId;
        private IntPtr _windowHandle = IntPtr.Zero;
        private string _windowTitle = string.Empty;
        private double _embeddedWidth = 800;
        private double _embeddedHeight = 600;
        private bool _isActive = true;
        private bool _showBorder = true;
        private bool _allowInteraction = true;
        private bool _autoRefresh = true;
        private int _refreshRate = 30; // FPS
        private EmbedCaptureMode _captureMode = EmbedCaptureMode.Interactive;
        private bool _hasEmbeddedWindow = false;

        public EmbedApplicationNode()
        {
            Type = NodeType.EmbedApplication;
            Title = "Embed Application";
            TitleDisplayMode = TitleDisplayMode.Always;

            // Ports sẽ được thêm bởi TemplateFactory
            // KHÔNG thêm ports ở đây để tránh duplicate

            // Setup dynamic outputs
            RebuildDynamicOutputs();
        }

        /// <summary>
        /// Tên process của ứng dụng được chọn
        /// </summary>
        public string ProcessName
        {
            get => _processName;
            set { if (_processName != value) { _processName = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Process ID của ứng dụng được chọn
        /// </summary>
        public int ProcessId
        {
            get => _processId;
            set { if (_processId != value) { _processId = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Window Handle của ứng dụng được embed
        /// </summary>
        public IntPtr WindowHandle
        {
            get => _windowHandle;
            set { if (_windowHandle != value) { _windowHandle = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Tiêu đề window của ứng dụng
        /// </summary>
        public string WindowTitle
        {
            get => _windowTitle;
            set { if (_windowTitle != value) { _windowTitle = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Chiều rộng của embedded window trên canvas
        /// </summary>
        public double EmbeddedWidth
        {
            get => _embeddedWidth;
            set { if (Math.Abs(_embeddedWidth - value) > 0.01) { _embeddedWidth = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Chiều cao của embedded window trên canvas
        /// </summary>
        public double EmbeddedHeight
        {
            get => _embeddedHeight;
            set { if (Math.Abs(_embeddedHeight - value) > 0.01) { _embeddedHeight = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Có active ứng dụng khi hiển thị hay không
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Hiển thị border xung quanh embedded window
        /// </summary>
        public bool ShowBorder
        {
            get => _showBorder;
            set { if (_showBorder != value) { _showBorder = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Cho phép tương tác với embedded window (click, type, etc.)
        /// </summary>
        public bool AllowInteraction
        {
            get => _allowInteraction;
            set { if (_allowInteraction != value) { _allowInteraction = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Tự động refresh embedded window
        /// </summary>
        public bool AutoRefresh
        {
            get => _autoRefresh;
            set { if (_autoRefresh != value) { _autoRefresh = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Tốc độ refresh (FPS)
        /// </summary>
        public int RefreshRate
        {
            get => _refreshRate;
            set { if (_refreshRate != value) { _refreshRate = Math.Clamp(value, 1, 60); OnPropertyChanged(); } }
        }

        /// <summary>
        /// Chế độ capture
        /// </summary>
        public EmbedCaptureMode CaptureMode
        {
            get => _captureMode;
            set { if (_captureMode != value) { _captureMode = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Đã có window được embed chưa
        /// </summary>
        public bool HasEmbeddedWindow
        {
            get => _hasEmbeddedWindow;
            set { if (_hasEmbeddedWindow != value) { _hasEmbeddedWindow = value; OnPropertyChanged(); } }
        }

        public void RebuildDynamicOutputs()
        {
            DynamicOutputs.Clear();
            
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "windowHandle",
                DisplayName = "Window Handle",
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });

            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "processId",
                DisplayName = "Process ID",
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });

            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "windowTitle",
                DisplayName = "Window Title",
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });

            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "processName",
                DisplayName = "Process Name",
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });
        }
    }

    public enum EmbedCaptureMode
    {
        /// <summary>
        /// Chỉ hiển thị, không tương tác
        /// </summary>
        DisplayOnly,

        /// <summary>
        /// Cho phép tương tác đầy đủ
        /// </summary>
        Interactive,

        /// <summary>
        /// Snapshot tĩnh (không update real-time)
        /// </summary>
        Snapshot
    }
}
