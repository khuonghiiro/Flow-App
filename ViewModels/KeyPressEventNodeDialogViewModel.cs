using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.ViewModels
{
    public partial class KeyPressEventNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly KeyPressEventNode _keyPressNode;

        [ObservableProperty]
        private string _keyDisplayText;

        [ObservableProperty]
        private int _pressDelay;

        [ObservableProperty]
        private string _delayUnit = "ms";

        [ObservableProperty]
        private bool _isAsync = false;

        public ObservableCollection<StringDelayUnitOption> DelayUnitOptions { get; } = new()
        {
            new StringDelayUnitOption("ms", "Mili giây (ms)"),
            new StringDelayUnitOption("s", "Giây (s)"),
            new StringDelayUnitOption("m", "Phút (m)"),
            new StringDelayUnitOption("h", "Giờ (h)")
        };

        // ── Toạ độ từ node khác ──────────────────────────────────────────────
        [ObservableProperty] private string? _coordSourceNodeId;
        [ObservableProperty] private string? _coordSourceOutputKey;
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();

        // ── Click tại vị trí toạ độ ──────────────────────────────────────────
        [ObservableProperty] private bool _clickOnPosition = true;
        [ObservableProperty] private int _clickDurationMs = 1;

        // ── Toạ độ thủ công — hiển thị text trong dialog ─────────────────────
        [ObservableProperty] private string _positionText = "Chưa chọn vị trí";

        // ── Chọn app để focus ────────────────────────────────────────────────
        [ObservableProperty] private WindowInfo? _selectedTargetWindow;
        public ObservableCollection<WindowInfo> ActiveWindows { get; } = new();
        public IRelayCommand LoadWindowsCommand { get; }

        // ── Background Mode ─────────────────────────────────────────────────────
        [ObservableProperty] private bool _useBackgroundMode = false;
        [ObservableProperty] private FlowMy.Helpers.BackgroundInputHelper.InputMode _backgroundInputMode = FlowMy.Helpers.BackgroundInputHelper.InputMode.Auto;
        
        // ── Return to Original Screen ───────────────────────────────────────────
        [ObservableProperty] private bool _returnToOriginalScreen = false;
        public ObservableCollection<BackgroundInputModeOption> BackgroundInputModeOptions { get; } = new()
        {
            new BackgroundInputModeOption(FlowMy.Helpers.BackgroundInputHelper.InputMode.Auto, "Auto (Tự chọn)"),
            new BackgroundInputModeOption(FlowMy.Helpers.BackgroundInputHelper.InputMode.DirectMessage, "DirectMessage (Nhanh, không cần driver)"),
            new BackgroundInputModeOption(FlowMy.Helpers.BackgroundInputHelper.InputMode.SilentActivation, "SilentActivation (Cân bằng)"),
            new BackgroundInputModeOption(FlowMy.Helpers.BackgroundInputHelper.InputMode.ForegroundActivation, "ForegroundActivation (Giống thật)")
        };
        
        public KeyPressEventNodeDialogViewModel(KeyPressEventNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _keyPressNode = node;
            _keyDisplayText = FormatKeyText(node.Key);
            _pressDelay = node.PressDelay;
            _delayUnit = node.DelayUnit;
            _isAsync = node.IsAsync;

            // Sync background mode
            UseBackgroundMode = node.UseBackgroundMode;
            BackgroundInputMode = node.BackgroundInputMode;
            ReturnToOriginalScreen = node.ReturnToOriginalScreen;

            // Sync toạ độ & click
            CoordSourceNodeId    = node.CoordSourceNodeId;
            CoordSourceOutputKey = node.CoordSourceOutputKey;
            ClickOnPosition      = node.ClickOnPosition;
            ClickDurationMs      = node.ClickDurationMs;
            PositionText         = node.PositionText;

            // Load danh sách node có output (chỉ upstream nodes)
            RefreshUpstreamNodes();

            // Load windows command
            LoadWindowsCommand = new RelayCommand(ExecuteLoadWindows);
            ExecuteLoadWindows();

            // Refresh key options khi đổi node nguồn
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CoordSourceNodeId))
                    RefreshOutputKeyOptions();
            };
            RefreshOutputKeyOptions();

            // Sync các properties khác khi node thay đổi
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(KeyPressEventNode.PressDelay))
                    {
                        PressDelay = node.PressDelay;
                    }
                    else if (e.PropertyName == nameof(KeyPressEventNode.DelayUnit))
                    {
                        DelayUnit = node.DelayUnit;
                    }
                    else if (e.PropertyName == nameof(KeyPressEventNode.IsAsync))
                    {
                        IsAsync = node.IsAsync;
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.Key) || e.PropertyName == nameof(KeyPressEventNode.TriggerKey))
                    {
                        KeyDisplayText = FormatKeyText(node.Key);
                        // Reload outputs để hiển thị key mới ngay lập tức
                        LoadOutputs();
                    }
                };
            }
        }

        protected override string GetDefaultTitle() => "Key Press";

        private string FormatKeyText(string? key)
        {
            var k = (key ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(k) ? "Chọn phím…" : k;
        }

        // ── Partial callbacks ────────────────────────────────────────────────

        partial void OnCoordSourceNodeIdChanged(string? value)
        {
            RefreshOutputKeyOptions();
        }

        partial void OnBackgroundInputModeChanged(FlowMy.Helpers.BackgroundInputHelper.InputMode value)
        {
            // Interception Driver đã được gỡ bỏ — không cần xử lý
        }

        partial void OnReturnToOriginalScreenChanged(bool value)
        {
            _keyPressNode.ReturnToOriginalScreen = value;
            _host.RequestSyncDataPanels(immediate: true);
        }

        // ── Lấy danh sách upstream nodes (kết nối đến port IN) ───────────────
        private void RefreshUpstreamNodes()
        {
            AvailableNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null || _host.ViewModel.Connections == null) return;

            var connections = _host.ViewModel.Connections;
            var upstream = new System.Collections.Generic.HashSet<WorkflowNode>();
            var stack = new System.Collections.Generic.Stack<WorkflowNode>();
            stack.Push(_node);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                var incoming = connections
                    .Where(c => c.ToNode == current && c.FromNode != null)
                    .ToList();

                foreach (var conn in incoming)
                {
                    var src = conn.FromNode;
                    if (src == null || ReferenceEquals(src, _node)) continue;
                    if (upstream.Add(src))
                        stack.Push(src);
                }
            }

            foreach (var n in upstream)
            {
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                AvailableNodeOptions.Add(CreateDataSourceOption(n));
            }
        }

        // ── Load danh sách cửa sổ đang mở ───────────────────────────────────
        private void ExecuteLoadWindows()
        {
            string? prevProcess = SelectedTargetWindow?.ProcessName ?? _keyPressNode.TargetProcessName;
            string? prevTitle   = SelectedTargetWindow?.Title       ?? _keyPressNode.TargetWindowTitle;

            var windows = WindowHelper.GetActiveWindows();
            ActiveWindows.Clear();
            foreach (var w in windows)
                ActiveWindows.Add(w);

            if (!string.IsNullOrWhiteSpace(prevProcess))
            {
                var match = ActiveWindows.FirstOrDefault(x =>
                    x.ProcessName == prevProcess && x.Title == prevTitle)
                    ?? ActiveWindows.FirstOrDefault(x => x.ProcessName == prevProcess);
                SelectedTargetWindow = match;
            }
        }

        // ── Refresh output key options ───────────────────────────────────────
        private void RefreshOutputKeyOptions()
        {
            FillOutputKeys(CoordSourceNodeId, AvailableOutputKeyOptions);
        }

        // Override OnSaveTitle để thêm logic riêng (ngoài phần chung đã xử lý ở base: Title, ReuseRoutes, Port Positions, ...)
        protected override void OnSaveTitle()
        {
            bool needSyncDataPanels = false;

            // Lưu delay properties
            if (_keyPressNode.PressDelay != PressDelay)
            {
                _keyPressNode.PressDelay = PressDelay;
                needSyncDataPanels = true;
            }
            if (_keyPressNode.DelayUnit != DelayUnit)
            {
                _keyPressNode.DelayUnit = DelayUnit;
                needSyncDataPanels = true;
            }
            if (_keyPressNode.IsAsync != IsAsync)
            {
                _keyPressNode.IsAsync = IsAsync;
                needSyncDataPanels = true;
            }

            _keyPressNode.CoordSourceNodeId    = string.IsNullOrWhiteSpace(CoordSourceNodeId)    ? null : CoordSourceNodeId;
            _keyPressNode.CoordSourceOutputKey = string.IsNullOrWhiteSpace(CoordSourceOutputKey) ? null : CoordSourceOutputKey;
            _keyPressNode.ClickOnPosition  = ClickOnPosition;
            _keyPressNode.ClickDurationMs  = ClickDurationMs;

            // ManualPosition đã được set trực tiếp vào node từ PickPositionButton_Click

            _keyPressNode.TargetProcessName = SelectedTargetWindow?.ProcessName ?? string.Empty;
            _keyPressNode.TargetWindowTitle = SelectedTargetWindow?.Title       ?? string.Empty;

            _keyPressNode.UseBackgroundMode = UseBackgroundMode;
            _keyPressNode.BackgroundInputMode = BackgroundInputMode;
            _keyPressNode.ReturnToOriginalScreen = ReturnToOriginalScreen;

            if (needSyncDataPanels)
            {
                _host.RequestSyncDataPanels(immediate: true);
            }
        }

        [RelayCommand]
        private void CaptureKey()
        {
            try
            {
                var dlg = new FlowMy.Views.Overlays.KeyCaptureDialog
                {
                    Owner = Application.Current?.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.CapturedKeyText))
                {
                    _keyPressNode.TriggerKey = dlg.CapturedKeyText;
                    KeyDisplayText = FormatKeyText(_keyPressNode.Key);
                    // Reload outputs để hiển thị key mới ngay lập tức
                    LoadOutputs();
                }
            }
            catch
            {
                // swallow errors
            }
        }
    }

    /// <summary>
    /// Wrapper class để hiển thị TitleDisplayMode trong ComboBox với text tùy chỉnh.
    /// </summary>
    public class TitleDisplayModeOption
    {
        public TitleDisplayMode Value { get; set; }
        public string DisplayName { get; set; }

        public TitleDisplayModeOption(TitleDisplayMode value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Wrapper class để hiển thị DelayUnit.
    /// </summary>
    public class StringDelayUnitOption
    {
        public string Value { get; set; }
        public string DisplayName { get; set; }

        public StringDelayUnitOption(string value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }
}

