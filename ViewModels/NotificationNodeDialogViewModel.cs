using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace FlowMy.ViewModels
{
    public sealed partial class NotificationNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly NotificationNode _notificationNode;

        [ObservableProperty]
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;

        // Title mapping
        public ObservableCollection<WorkflowDataSourceOption> TitleSourceNodes { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> TitleOutputKeys { get; } = new();

        [ObservableProperty]
        private string _titleSelectedSourceNodeId = string.Empty;

        [ObservableProperty]
        private string _titleSelectedSourceOutputKey = string.Empty;

        // Content mapping
        public ObservableCollection<WorkflowDataSourceOption> ContentSourceNodes { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> ContentOutputKeys { get; } = new();

        [ObservableProperty]
        private string _contentSelectedSourceNodeId = string.Empty;

        [ObservableProperty]
        private string _contentSelectedSourceOutputKey = string.Empty;

        // Duration mapping
        public ObservableCollection<WorkflowDataSourceOption> DurationSourceNodes { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> DurationOutputKeys { get; } = new();

        [ObservableProperty]
        private string _durationSelectedSourceNodeId = string.Empty;

        [ObservableProperty]
        private string _durationSelectedSourceOutputKey = string.Empty;

        [ObservableProperty]
        private int _defaultDurationSeconds = 5;

        [ObservableProperty]
        private string _staticTitle = string.Empty;

        [ObservableProperty]
        private string _staticContent = string.Empty;

        [ObservableProperty]
        private string? _toastTitleColorKey;

        [ObservableProperty]
        private string? _toastContentColorKey;

        [ObservableProperty]
        private string? _toastBackgroundColorKey;

        [ObservableProperty]
        private double _toastBackgroundOpacity = 0.85;

        public ObservableCollection<TitleDisplayModeOption> TitleDisplayModeOptions { get; } = new()
        {
            new TitleDisplayModeOption(TitleDisplayMode.Hidden, "Ẩn tiêu đề"),
            new TitleDisplayModeOption(TitleDisplayMode.Hover, "Hiện khi hover"),
            new TitleDisplayModeOption(TitleDisplayMode.Always, "Luôn hiện")
        };

        public NotificationNodeDialogViewModel(NotificationNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _notificationNode = node ?? throw new ArgumentNullException(nameof(node));

            TitleDisplayMode = _notificationNode.TitleDisplayMode;

            _defaultDurationSeconds = _notificationNode.DefaultDurationSeconds;

            _staticTitle = _notificationNode.StaticTitle;
            _staticContent = _notificationNode.StaticContent;

            _toastTitleColorKey = _notificationNode.ToastTitleColorKey;
            _toastContentColorKey = _notificationNode.ToastContentColorKey;
            _toastBackgroundColorKey = _notificationNode.ToastBackgroundColorKey;
            _toastBackgroundOpacity = _notificationNode.ToastBackgroundOpacity;

            // Load existing mappings
            if (_notificationNode.TitleInput != null)
            {
                _titleSelectedSourceNodeId = _notificationNode.TitleInput.SourceNodeId;
                _titleSelectedSourceOutputKey = _notificationNode.TitleInput.SourceOutputKey;
            }

            if (_notificationNode.ContentInput != null)
            {
                _contentSelectedSourceNodeId = _notificationNode.ContentInput.SourceNodeId;
                _contentSelectedSourceOutputKey = _notificationNode.ContentInput.SourceOutputKey;
            }

            if (_notificationNode.DurationInput != null)
            {
                _durationSelectedSourceNodeId = _notificationNode.DurationInput.SourceNodeId;
                _durationSelectedSourceOutputKey = _notificationNode.DurationInput.SourceOutputKey;
            }

            RefreshAllSources();

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(NotificationNode.TitleDisplayMode))
                    {
                        TitleDisplayMode = _notificationNode.TitleDisplayMode;
                    }
                    else if (e.PropertyName == nameof(NotificationNode.DefaultDurationSeconds))
                    {
                        DefaultDurationSeconds = _notificationNode.DefaultDurationSeconds;
                    }

                    OnNodePropertyChanged(e.PropertyName ?? string.Empty);
                };
            }
        }

        protected override string GetDefaultTitle() => "Notification";

        private void RefreshAllSources()
        {
            TitleSourceNodes.Clear();
            ContentSourceNodes.Clear();
            DurationSourceNodes.Clear();

            TitleOutputKeys.Clear();
            ContentOutputKeys.Clear();
            DurationOutputKeys.Clear();

            if (_host.ViewModel == null) return;

            var vm = _host.ViewModel;
            var connections = vm.Connections;
            if (connections == null || connections.Count == 0) return;

            var upstream = new System.Collections.Generic.HashSet<WorkflowNode>();
            var stack = new System.Collections.Generic.Stack<WorkflowNode>();
            stack.Push(_notificationNode);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                var incoming = connections
                    .Where(c => c.ToNode == current && c.FromNode != null)
                    .ToList();

                foreach (var conn in incoming)
                {
                    var src = conn.FromNode;
                    if (src == null) continue;

                    if (upstream.Add(src))
                    {
                        stack.Push(src);
                    }
                }
            }

            var producerNodes = upstream
                .Where(n => n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
                .Where(n => !ReferenceEquals(n, _notificationNode))
                .ToList();

            foreach (var n in producerNodes)
            {
                var option = new WorkflowDataSourceOption
                {
                    NodeId = n.Id,
                    Title = string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title,
                    NodeType = n.Type,
                    NodeTypeDisplayName = ResolveNodeTypeDisplayName(n.Type),
                    IconKey = ResolveNodeIconKey(n.Type),
                    NodeBrush = n.NodeBrush,
                    NodeTextBrush = ResolveTextOnNodeBrush(n.ColorKey)
                };

                TitleSourceNodes.Add(option);
                ContentSourceNodes.Add(option);
                DurationSourceNodes.Add(option);
            }

            RefreshOutputKeysForTitle();
            RefreshOutputKeysForContent();
            RefreshOutputKeysForDuration();
        }

        private void RefreshOutputKeysForTitle()
        {
            TitleOutputKeys.Clear();
            if (string.IsNullOrEmpty(TitleSelectedSourceNodeId) || _host.ViewModel == null) return;

            var src = _host.ViewModel.Nodes.FirstOrDefault(n => n.Id == TitleSelectedSourceNodeId);
            if (src?.DynamicOutputs == null) return;

            foreach (var output in src.DynamicOutputs)
            {
                if (string.IsNullOrWhiteSpace(output.Key)) continue;
                TitleOutputKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = output.Key,
                    DisplayName = output.DisplayName ?? output.Key,
                    Type = output.OutputType
                });
            }
        }

        private void RefreshOutputKeysForContent()
        {
            ContentOutputKeys.Clear();
            if (string.IsNullOrEmpty(ContentSelectedSourceNodeId) || _host.ViewModel == null) return;

            var src = _host.ViewModel.Nodes.FirstOrDefault(n => n.Id == ContentSelectedSourceNodeId);
            if (src?.DynamicOutputs == null) return;

            foreach (var output in src.DynamicOutputs)
            {
                if (string.IsNullOrWhiteSpace(output.Key)) continue;
                ContentOutputKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = output.Key,
                    DisplayName = output.DisplayName ?? output.Key,
                    Type = output.OutputType
                });
            }
        }

        private void RefreshOutputKeysForDuration()
        {
            DurationOutputKeys.Clear();
            if (string.IsNullOrEmpty(DurationSelectedSourceNodeId) || _host.ViewModel == null) return;

            var src = _host.ViewModel.Nodes.FirstOrDefault(n => n.Id == DurationSelectedSourceNodeId);
            if (src?.DynamicOutputs == null) return;

            foreach (var output in src.DynamicOutputs)
            {
                if (string.IsNullOrWhiteSpace(output.Key)) continue;
                DurationOutputKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = output.Key,
                    DisplayName = output.DisplayName ?? output.Key,
                    Type = output.OutputType
                });
            }
        }

        partial void OnTitleSelectedSourceNodeIdChanged(string value)
        {
            RefreshOutputKeysForTitle();
        }

        partial void OnContentSelectedSourceNodeIdChanged(string value)
        {
            RefreshOutputKeysForContent();
        }

        partial void OnDurationSelectedSourceNodeIdChanged(string value)
        {
            RefreshOutputKeysForDuration();
        }

        partial void OnTitleSelectedSourceOutputKeyChanged(string value)
        {
            if (_notificationNode.TitleInput == null)
            {
                _notificationNode.TitleInput = new InputVariable();
            }

            _notificationNode.TitleInput.SourceNodeId = TitleSelectedSourceNodeId;
            _notificationNode.TitleInput.SourceOutputKey = value;
        }

        partial void OnContentSelectedSourceOutputKeyChanged(string value)
        {
            if (_notificationNode.ContentInput == null)
            {
                _notificationNode.ContentInput = new InputVariable();
            }

            _notificationNode.ContentInput.SourceNodeId = ContentSelectedSourceNodeId;
            _notificationNode.ContentInput.SourceOutputKey = value;
        }

        partial void OnDurationSelectedSourceOutputKeyChanged(string value)
        {
            if (_notificationNode.DurationInput == null)
            {
                _notificationNode.DurationInput = new InputVariable();
            }

            _notificationNode.DurationInput.SourceNodeId = DurationSelectedSourceNodeId;
            _notificationNode.DurationInput.SourceOutputKey = value;
        }

        partial void OnDefaultDurationSecondsChanged(int value)
        {
            _notificationNode.DefaultDurationSeconds = value;
        }

        partial void OnStaticTitleChanged(string value)
        {
            _notificationNode.StaticTitle = value;
        }

        partial void OnStaticContentChanged(string value)
        {
            _notificationNode.StaticContent = value;
        }

        partial void OnToastTitleColorKeyChanged(string? value)
        {
            _notificationNode.ToastTitleColorKey = value;
        }

        partial void OnToastContentColorKeyChanged(string? value)
        {
            _notificationNode.ToastContentColorKey = value;
        }

        partial void OnToastBackgroundColorKeyChanged(string? value)
        {
            _notificationNode.ToastBackgroundColorKey = value;
        }

        partial void OnToastBackgroundOpacityChanged(double value)
        {
            _notificationNode.ToastBackgroundOpacity = value;
        }

        partial void OnTitleDisplayModeChanged(TitleDisplayMode value)
        {
            if (_notificationNode.TitleDisplayMode != value)
            {
                _notificationNode.TitleDisplayMode = value;
            }
        }

        protected override void OnSaveTitle()
        {
            _notificationNode.NotifyTitleChanged();
            _host.RequestSyncDataPanels(immediate: true);
        }

        private static string ResolveNodeTypeDisplayName(NodeType type)
        {
            return type switch
            {
                NodeType.IfElse => "Conditional",
                NodeType.KeyPressEvent => "Key Press Event",
                NodeType.HotkeyPressEvent => "Hotkey Press Event",
                NodeType.MouseEvent => "Mouse Event",
                NodeType.StringSplit => "String Split",
                NodeType.AssignData => "Assign Data",
                NodeType.MediaGallery => "Media Gallery",
                NodeType.ImageProcessing => "Image Processing",
                NodeType.HttpRequest => "HTTP Request",
                NodeType.FileDownload => "File Download",
                NodeType.DataFetcher => "Data Fetcher",
                NodeType.KeyValueBridge => "Key Value Bridge",
                NodeType.FlowOverwrite => "Flow Overwrite",
                NodeType.FolderFilePaths => "Folder File Paths",
                NodeType.AsyncTaskDispatchCollect => "Async Dispatch Collect",
                NodeType.KeyScopedStore => "Key Scoped Store",
                _ => type.ToString()
            };
        }

        private static string ResolveNodeIconKey(NodeType type)
        {
            return type switch
            {
                NodeType.Start => "play duotone-regular",
                NodeType.End => "flag-checkered sharp-duotone-solid",
                NodeType.Input => "left-to-dotted-line duotone-regular",
                NodeType.Output => "right-to-dotted-line duotone-regular",
                NodeType.Process => "cog",
                NodeType.IfElse => "list-tree sharp-light",
                NodeType.Loop => "arrows-spin duotone",
                NodeType.Break => "circle-stop duotone",
                NodeType.Continue => "diagram-predecessor duotone-light",
                NodeType.Delay => "timer regular",
                NodeType.Keyboard => "keyboard duotone",
                NodeType.KeyPressEvent => "key duotone-regular",
                NodeType.HotkeyPressEvent => "keyboard duotone",
                NodeType.MouseEvent => "computer-mouse duotone",
                NodeType.Variable => "square-root-variable",
                NodeType.Function => "calculator",
                NodeType.ScreenPosition => "crosshairs sharp-duotone-solid",
                NodeType.ScreenCapture => "camera-viewfinder duotone-light",
                NodeType.StringSplit => "scissors light",
                NodeType.ListOut => "list-radio regular",
                NodeType.AssignData => "arrows-left-right duotone",
                NodeType.MediaGallery => "image-stack duotone",
                NodeType.ImageProcessing => "image notdog-duo-solid",
                NodeType.Code => "code duotone-regular",
                NodeType.HtmlUi => "html5 brands",
                NodeType.Folder => "folder-open duotone-thin",
                NodeType.HttpRequest => "globe-pointer sharp-duotone-light",
                NodeType.Web => "internet-explorer brands",
                NodeType.AsyncTask => "diagram-project duotone-light",
                NodeType.DataFetcher => "inbox-out duotone-light",
                NodeType.FolderFilePaths => "file-import duotone-light",
                NodeType.KeyValueBridge => "list-check solid",
                NodeType.FlowOverwrite => "merge sharp-regular",
                NodeType.Notification => "message-captions duotone-regular",
                NodeType.Storage => "arrow-progress sharp-regular",
                NodeType.Callback => "arrows-turn-right regular",
                NodeType.FileDownload => "download solid",
                NodeType.AsyncTaskDispatchCollect => "list-radio regular",
                NodeType.KeyScopedStore => "arrow-progress sharp-regular",
                NodeType.LoopContext => "arrows-spin duotone",
                NodeType.Condition => "list-tree sharp-light",
                _ => "cog"
            };
        }

        private static Brush ResolveTextOnNodeBrush(string? nodeColorKey)
        {
            var app = Application.Current;
            if (app != null && !string.IsNullOrWhiteSpace(nodeColorKey))
            {
                var clean = nodeColorKey.Trim();
                if (clean.EndsWith("Brush", StringComparison.OrdinalIgnoreCase))
                    clean = clean[..^"Brush".Length];

                if (app.TryFindResource($"TextOn{clean}Brush") is Brush textOnBrush) return textOnBrush;
                if (app.TryFindResource($"TextOn{clean}") is Brush textOnKey) return textOnKey;
            }

            return app?.TryFindResource("TextOnPrimaryBrush") as Brush ?? Brushes.White;
        }
    }
}

