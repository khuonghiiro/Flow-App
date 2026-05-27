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

        public KeyPressEventNodeDialogViewModel(KeyPressEventNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _keyPressNode = node;
            _keyDisplayText = FormatKeyText(node.Key);
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
                    if (e.PropertyName == nameof(KeyPressEventNode.PressDelayMs))
                    {
                        PressDelayMs = node.PressDelayMs;
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

            // Lưu PressDelayMs
            if (_keyPressNode.PressDelayMs != PressDelayMs)
            {
                _keyPressNode.PressDelayMs = PressDelayMs;
                needSyncDataPanels = true;
            }

            _keyPressNode.CoordSourceNodeId    = string.IsNullOrWhiteSpace(CoordSourceNodeId)    ? null : CoordSourceNodeId;
            _keyPressNode.CoordSourceOutputKey = string.IsNullOrWhiteSpace(CoordSourceOutputKey) ? null : CoordSourceOutputKey;
            _keyPressNode.ClickOnPosition  = ClickOnPosition;
            _keyPressNode.ClickDurationMs  = ClickDurationMs;

            _keyPressNode.TargetProcessName = SelectedTargetWindow?.ProcessName ?? string.Empty;
            _keyPressNode.TargetWindowTitle = SelectedTargetWindow?.Title       ?? string.Empty;

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
}

