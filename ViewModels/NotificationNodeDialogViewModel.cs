using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public sealed partial class NotificationNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly NotificationNode _notificationNode;

        /// <summary>
        /// Guard: khi true, các partial OnXxxChanged sẽ KHÔNG reset SelectedOutputKey về FirstOrDefault.
        /// Dùng khi load dữ liệu từ node vào VM (RefreshAllSources).
        /// </summary>
        private bool _isRestoringFromNode;

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

            // Load existing mappings - follow OutputNode pattern
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

            // Don't call RefreshAllSources here - it will be called in OnLoaded after UI is loaded

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

        public void RefreshAllSources()
        {
            // Lưu lại các OutputKey đang được chọn TRƯỚC khi refresh, để restore sau
            var savedTitleKey    = _notificationNode.TitleInput?.SourceOutputKey    ?? _titleSelectedSourceOutputKey;
            var savedContentKey  = _notificationNode.ContentInput?.SourceOutputKey  ?? _contentSelectedSourceOutputKey;
            var savedDurationKey = _notificationNode.DurationInput?.SourceOutputKey ?? _durationSelectedSourceOutputKey;

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
                var option = CreateDataSourceOption(n);

                TitleSourceNodes.Add(option);
                ContentSourceNodes.Add(CreateDataSourceOption_Clone(option));
                DurationSourceNodes.Add(CreateDataSourceOption_Clone(option));
            }

            // Bật guard để ngăn OnChanged reset output key
            _isRestoringFromNode = true;
            try
            {
                // Populate output keys cho node đang được chọn
                if (!string.IsNullOrEmpty(TitleSelectedSourceNodeId))
                {
                    RefreshOutputKeysForTitle();
                    // Restore key đã chọn nếu còn tồn tại trong list mới
                    if (!string.IsNullOrEmpty(savedTitleKey) &&
                        TitleOutputKeys.Any(k => k.Key == savedTitleKey))
                    {
                        TitleSelectedSourceOutputKey = savedTitleKey;
                    }
                }

                if (!string.IsNullOrEmpty(ContentSelectedSourceNodeId))
                {
                    RefreshOutputKeysForContent();
                    if (!string.IsNullOrEmpty(savedContentKey) &&
                        ContentOutputKeys.Any(k => k.Key == savedContentKey))
                    {
                        ContentSelectedSourceOutputKey = savedContentKey;
                    }
                }

                if (!string.IsNullOrEmpty(DurationSelectedSourceNodeId))
                {
                    RefreshOutputKeysForDuration();
                    if (!string.IsNullOrEmpty(savedDurationKey) &&
                        DurationOutputKeys.Any(k => k.Key == savedDurationKey))
                    {
                        DurationSelectedSourceOutputKey = savedDurationKey;
                    }
                }
            }
            finally
            {
                _isRestoringFromNode = false;
            }

            // WPF ComboBox có thể reset SelectedValue về null sau khi ItemsSource thay đổi
            // (do binding re-evaluation). Dùng Dispatcher để re-set lại sau khi WPF xử lý xong.
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                _isRestoringFromNode = true;
                try
                {
                    if (!string.IsNullOrEmpty(savedTitleKey) &&
                        TitleOutputKeys.Any(k => k.Key == savedTitleKey) &&
                        TitleSelectedSourceOutputKey != savedTitleKey)
                    {
                        TitleSelectedSourceOutputKey = savedTitleKey;
                    }

                    if (!string.IsNullOrEmpty(savedContentKey) &&
                        ContentOutputKeys.Any(k => k.Key == savedContentKey) &&
                        ContentSelectedSourceOutputKey != savedContentKey)
                    {
                        ContentSelectedSourceOutputKey = savedContentKey;
                    }

                    if (!string.IsNullOrEmpty(savedDurationKey) &&
                        DurationOutputKeys.Any(k => k.Key == savedDurationKey) &&
                        DurationSelectedSourceOutputKey != savedDurationKey)
                    {
                        DurationSelectedSourceOutputKey = savedDurationKey;
                    }
                }
                finally
                {
                    _isRestoringFromNode = false;
                }
            }), System.Windows.Threading.DispatcherPriority.DataBind);
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
            if (_notificationNode.TitleInput == null)
            {
                _notificationNode.TitleInput = new InputVariable();
            }
            _notificationNode.TitleInput.VariableKey = "title";
            _notificationNode.TitleInput.SourceNodeId = value;
            RefreshOutputKeysForTitle();
            // Reset output key chỉ khi user thực sự đổi node (không phải khi đang restore từ file)
            if (!_isRestoringFromNode &&
                !TitleOutputKeys.Any(k => k.Key == TitleSelectedSourceOutputKey))
            {
                TitleSelectedSourceOutputKey = TitleOutputKeys.FirstOrDefault()?.Key ?? string.Empty;
            }
        }

        partial void OnContentSelectedSourceNodeIdChanged(string value)
        {
            if (_notificationNode.ContentInput == null)
            {
                _notificationNode.ContentInput = new InputVariable();
            }
            _notificationNode.ContentInput.VariableKey = "content";
            _notificationNode.ContentInput.SourceNodeId = value;
            RefreshOutputKeysForContent();
            // Reset output key chỉ khi user thực sự đổi node (không phải khi đang restore từ file)
            if (!_isRestoringFromNode &&
                !ContentOutputKeys.Any(k => k.Key == ContentSelectedSourceOutputKey))
            {
                ContentSelectedSourceOutputKey = ContentOutputKeys.FirstOrDefault()?.Key ?? string.Empty;
            }
        }

        partial void OnDurationSelectedSourceNodeIdChanged(string value)
        {
            if (_notificationNode.DurationInput == null)
            {
                _notificationNode.DurationInput = new InputVariable();
            }
            _notificationNode.DurationInput.VariableKey = "duration";
            _notificationNode.DurationInput.SourceNodeId = value;
            RefreshOutputKeysForDuration();
            // Reset output key chỉ khi user thực sự đổi node (không phải khi đang restore từ file)
            if (!_isRestoringFromNode &&
                !DurationOutputKeys.Any(k => k.Key == DurationSelectedSourceOutputKey))
            {
                DurationSelectedSourceOutputKey = DurationOutputKeys.FirstOrDefault()?.Key ?? string.Empty;
            }
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

    }
}

