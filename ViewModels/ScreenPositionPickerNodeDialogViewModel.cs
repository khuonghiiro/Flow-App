using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    /// <summary>Option cho combobox hành động chuột.</summary>
    public sealed class MouseActionOption
    {
        public ScreenPositionMouseAction Value { get; }
        public string DisplayName { get; }
        public MouseActionOption(ScreenPositionMouseAction value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }
    }

    public partial class ScreenPositionPickerNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly ScreenPositionPickerNode _node;

        // ── Toạ độ nguồn từ node khác ──
        [ObservableProperty] private string? _coordSourceNodeId;
        [ObservableProperty] private string? _coordSourceOutputKey;

        // ── Hành động chuột ──
        [ObservableProperty] private ScreenPositionMouseAction _mouseAction;

        // ── Click ──
        [ObservableProperty] private int _clickCount = 1;
        [ObservableProperty] private int _holdDurationMs = 1;

        // ── Scroll ──
        [ObservableProperty] private int _scrollCount = 1;
        [ObservableProperty] private int _scrollIntervalMs = 1000;

        // ── Visibility flags ──
        [ObservableProperty] private bool _isClickAction;
        [ObservableProperty] private bool _isScrollAction;

        // ── Chọn app để focus trước khi chọn vị trí ──
        [ObservableProperty] private WindowInfo? _selectedTargetWindow;
        public ObservableCollection<WindowInfo> ActiveWindows { get; } = new();
        public IRelayCommand LoadWindowsCommand { get; }

        // ── Return to Original Screen ───────────────────────────────────────────
        [ObservableProperty] private bool _returnToOriginalScreen = false;

        // ── Collections ──
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();

        public ObservableCollection<MouseActionOption> MouseActionOptions { get; } = new()
        {
            new MouseActionOption(ScreenPositionMouseAction.None,        "Không xử lý"),
            new MouseActionOption(ScreenPositionMouseAction.LeftClick,   "Chuột trái"),
            new MouseActionOption(ScreenPositionMouseAction.RightClick,  "Chuột phải"),
            new MouseActionOption(ScreenPositionMouseAction.ScrollUp,    "Scroll lên (top)"),
            new MouseActionOption(ScreenPositionMouseAction.ScrollDown,  "Scroll xuống (bottom)"),
        };

        public ScreenPositionPickerNodeDialogViewModel(ScreenPositionPickerNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));

            // Sync từ node → VM
            CoordSourceNodeId    = node.CoordSourceNodeId;
            CoordSourceOutputKey = node.CoordSourceOutputKey;
            MouseAction          = node.MouseAction;
            ClickCount           = node.ClickCount;
            HoldDurationMs       = node.HoldDurationMs;
            ScrollCount          = node.ScrollCount;
            ScrollIntervalMs     = node.ScrollIntervalMs;
            ReturnToOriginalScreen = node.ReturnToOriginalScreen;

            // Load windows command
            LoadWindowsCommand = new RelayCommand(ExecuteLoadWindows);
            ExecuteLoadWindows();

            UpdateActionFlags();
            RefreshAvailableNodes();
            RefreshOutputKeyOptions();
        }

        protected override string GetDefaultTitle() => "Screen Position";

        // ── Partial callbacks (CommunityToolkit.Mvvm) ──

        partial void OnMouseActionChanged(ScreenPositionMouseAction value)
        {
            UpdateActionFlags();
        }

        partial void OnCoordSourceNodeIdChanged(string? value)
        {
            RefreshOutputKeyOptions();
        }

        partial void OnReturnToOriginalScreenChanged(bool value)
        {
            _node.ReturnToOriginalScreen = value;
            _host.RequestSyncDataPanels(immediate: true);
        }

        // ── Helpers ──

        private void UpdateActionFlags()
        {
            IsClickAction  = MouseAction == ScreenPositionMouseAction.LeftClick
                          || MouseAction == ScreenPositionMouseAction.RightClick;
            IsScrollAction = MouseAction == ScreenPositionMouseAction.ScrollUp
                          || MouseAction == ScreenPositionMouseAction.ScrollDown;
        }

        private void RefreshAvailableNodes()
        {
            AvailableNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null) return;

            foreach (var n in _host.ViewModel.Nodes)
            {
                if (ReferenceEquals(n, _node)) continue;
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                AvailableNodeOptions.Add(CreateDataSourceOption(n));
            }
        }

        private void RefreshOutputKeyOptions()
        {
            AvailableOutputKeyOptions.Clear();
            if (string.IsNullOrWhiteSpace(CoordSourceNodeId) || _host.ViewModel?.Nodes == null) return;

            var src = _host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, CoordSourceNodeId, StringComparison.OrdinalIgnoreCase));
            if (src?.DynamicOutputs == null) return;

            foreach (var o in src.DynamicOutputs)
            {
                var key = o.Key ?? string.Empty;
                AvailableOutputKeyOptions.Add(new WorkflowOutputKeyOption
                {
                    Key = key,
                    DisplayName = o.DisplayName ?? key,
                    Type = o.OutputType ?? o.ConvertType
                });
            }
        }

        // ── Load danh sách cửa sổ đang mở ───────────────────────────────────
        private void ExecuteLoadWindows()
        {
            string? prevProcess = SelectedTargetWindow?.ProcessName ?? _node.TargetProcessName;
            string? prevTitle   = SelectedTargetWindow?.Title       ?? _node.TargetWindowTitle;

            var windows = WindowHelper.GetActiveWindows();
            ActiveWindows.Clear();
            foreach (var w in windows)
                ActiveWindows.Add(w);

            if (!string.IsNullOrWhiteSpace(prevProcess))
            {
                // Ưu tiên exact match (title + process)
                var match = ActiveWindows.FirstOrDefault(x =>
                    x.ProcessName == prevProcess && x.Title == prevTitle)
                    ?? ActiveWindows.FirstOrDefault(x => x.ProcessName == prevProcess);
                SelectedTargetWindow = match;
            }
        }

        protected override void OnSaveTitle()
        {
            bool needSync = false;

            void Sync<T>(ref T field, T value, Action<T> setter) where T : IEquatable<T>
            {
                if (!field.Equals(value)) { setter(value); needSync = true; }
            }

            _node.CoordSourceNodeId    = CoordSourceNodeId;
            _node.CoordSourceOutputKey = CoordSourceOutputKey;

            if (_node.MouseAction != MouseAction)    { _node.MouseAction    = MouseAction;    needSync = true; }
            if (_node.ClickCount  != ClickCount)     { _node.ClickCount     = ClickCount;     needSync = true; }
            if (_node.HoldDurationMs != HoldDurationMs) { _node.HoldDurationMs = HoldDurationMs; needSync = true; }
            if (_node.ScrollCount != ScrollCount)    { _node.ScrollCount    = ScrollCount;    needSync = true; }
            if (_node.ScrollIntervalMs != ScrollIntervalMs) { _node.ScrollIntervalMs = ScrollIntervalMs; needSync = true; }

            // Lưu target app
            _node.TargetProcessName = SelectedTargetWindow?.ProcessName ?? string.Empty;
            _node.TargetWindowTitle = SelectedTargetWindow?.Title       ?? string.Empty;
            _node.ReturnToOriginalScreen = ReturnToOriginalScreen;

            if (needSync)
                _host.RequestSyncDataPanels(immediate: true);

            _node.NotifyTitleChanged();
        }
    }
}
