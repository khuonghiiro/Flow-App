using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace FlowMy.ViewModels
{
    public partial class HotkeyPressEventNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly HotkeyPressEventNode _hotkeyPressNode;

        [ObservableProperty]
        private string _hotkeyDisplayText;

        [ObservableProperty]
        private int _pressDelayMs;

        // ── Toạ độ từ node khác ──────────────────────────────────────────────
        [ObservableProperty] private string? _coordSourceNodeId;
        [ObservableProperty] private string? _coordSourceOutputKey;
        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();

        // ── Click tại vị trí toạ độ ──────────────────────────────────────────
        [ObservableProperty] private bool _clickOnPosition = true;
        [ObservableProperty] private int _clickDurationMs = 1;

        // ── Chọn app để focus ────────────────────────────────────────────────
        [ObservableProperty] private WindowInfo? _selectedTargetWindow;
        public ObservableCollection<WindowInfo> ActiveWindows { get; } = new();
        public IRelayCommand LoadWindowsCommand { get; }

        public HotkeyPressEventNodeDialogViewModel(HotkeyPressEventNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _hotkeyPressNode = node;
            _hotkeyDisplayText = FormatHotkeyText(node.Key);
            _pressDelayMs = node.PressDelayMs;

            // Sync toạ độ & click
            CoordSourceNodeId    = node.CoordSourceNodeId;
            CoordSourceOutputKey = node.CoordSourceOutputKey;
            ClickOnPosition      = node.ClickOnPosition;
            ClickDurationMs      = node.ClickDurationMs;

            // Load danh sách node có output
            RefreshAllNodesWithOutputs(AvailableNodeOptions);

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
                    if (e.PropertyName == nameof(HotkeyPressEventNode.PressDelayMs))
                    {
                        PressDelayMs = node.PressDelayMs;
                    }
                    else if (e.PropertyName == nameof(WorkflowNode.Key) || e.PropertyName == nameof(HotkeyPressEventNode.TriggerHotkey))
                    {
                        HotkeyDisplayText = FormatHotkeyText(node.Key);
                        // Reload outputs để hiển thị key mới ngay lập tức
                        LoadOutputs();
                    }
                    OnNodePropertyChanged(e.PropertyName);
                };
            }
        }

        protected override string GetDefaultTitle() => "Hotkey Press";

        private string FormatHotkeyText(string? hotkey)
        {
            var k = (hotkey ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(k) ? "Chọn hotkey…" : k;
        }

        // ── Partial callbacks ────────────────────────────────────────────────

        partial void OnCoordSourceNodeIdChanged(string? value)
        {
            RefreshOutputKeyOptions();
        }

        // ── Load danh sách cửa sổ đang mở ───────────────────────────────────
        private void ExecuteLoadWindows()
        {
            string? prevProcess = SelectedTargetWindow?.ProcessName ?? _hotkeyPressNode.TargetProcessName;
            string? prevTitle   = SelectedTargetWindow?.Title       ?? _hotkeyPressNode.TargetWindowTitle;

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

        // Override OnSaveTitle để thêm logic riêng thay vì override SaveTitle
        protected override void OnSaveTitle()
        {
            if (_hotkeyPressNode.PressDelayMs != PressDelayMs)
            {
                _hotkeyPressNode.PressDelayMs = PressDelayMs;
                _host.RequestSyncDataPanels(immediate: true);
            }

            _hotkeyPressNode.CoordSourceNodeId    = string.IsNullOrWhiteSpace(CoordSourceNodeId)    ? null : CoordSourceNodeId;
            _hotkeyPressNode.CoordSourceOutputKey = string.IsNullOrWhiteSpace(CoordSourceOutputKey) ? null : CoordSourceOutputKey;
            _hotkeyPressNode.ClickOnPosition  = ClickOnPosition;
            _hotkeyPressNode.ClickDurationMs  = ClickDurationMs;

            _hotkeyPressNode.TargetProcessName = SelectedTargetWindow?.ProcessName ?? string.Empty;
            _hotkeyPressNode.TargetWindowTitle = SelectedTargetWindow?.Title       ?? string.Empty;
        }

        [RelayCommand]
        private void CaptureHotkey()
        {
            try
            {
                var dlg = new FlowMy.Views.Overlays.HotkeyCaptureDialog
                {
                    Owner = Application.Current?.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    InitialHotkeyText = _hotkeyPressNode.Key
                };

                var ok = dlg.ShowDialog() == true;
                if (!ok) return;

                // null => user did not pick anything, keep current
                if (dlg.CapturedHotkeyText == null) return;

                // "" => user cleared
                _hotkeyPressNode.TriggerHotkey = dlg.CapturedHotkeyText;
                HotkeyDisplayText = FormatHotkeyText(_hotkeyPressNode.Key);
                // Reload outputs để hiển thị key mới ngay lập tức
                LoadOutputs();
            }
            catch
            {
                // swallow errors
            }
        }
    }
}
