using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.Enums;
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

        [ObservableProperty]
        private HotkeyTriggerModeEnum _triggerMode;

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
            new BackgroundInputModeOption(FlowMy.Helpers.BackgroundInputHelper.InputMode.InterceptionDriver, "Interception Driver (Tốt nhất - cần cài driver)"),
            new BackgroundInputModeOption(FlowMy.Helpers.BackgroundInputHelper.InputMode.DirectMessage, "DirectMessage (Nhanh, không cần driver)"),
            new BackgroundInputModeOption(FlowMy.Helpers.BackgroundInputHelper.InputMode.SilentActivation, "SilentActivation (Cân bằng)"),
            new BackgroundInputModeOption(FlowMy.Helpers.BackgroundInputHelper.InputMode.ForegroundActivation, "ForegroundActivation (Giống thật)")
        };
        
        public HotkeyPressEventNodeDialogViewModel(HotkeyPressEventNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _hotkeyPressNode = node;
            _hotkeyDisplayText = FormatHotkeyText(node.Key);
            _pressDelayMs = node.PressDelayMs;
            _triggerMode = node.TriggerMode;

            // Sync background mode
            UseBackgroundMode = node.UseBackgroundMode;
            BackgroundInputMode = node.BackgroundInputMode;
            ReturnToOriginalScreen = node.ReturnToOriginalScreen;

            // Sync toạ độ & click
            CoordSourceNodeId    = node.CoordSourceNodeId;
            CoordSourceOutputKey = node.CoordSourceOutputKey;
            ClickOnPosition      = node.ClickOnPosition;
            ClickDurationMs      = node.ClickDurationMs;
            // Sync vị trí thủ công — đọc từ node để hiển thị đúng khi mở lại dialog
            PositionText = node.PositionText;

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
                    else if (e.PropertyName == nameof(HotkeyPressEventNode.TriggerMode))
                    {
                        TriggerMode = node.TriggerMode;
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

        partial void OnBackgroundInputModeChanged(FlowMy.Helpers.BackgroundInputHelper.InputMode value)
        {
            if (value == FlowMy.Helpers.BackgroundInputHelper.InputMode.InterceptionDriver
                && !FlowMy.Helpers.InterceptionInputHelper.IsDriverInstalled())
            {
                // Lấy dialog đang active (chính là node dialog này) làm owner
                // để MessageBox hiển thị đúng trên cùng, không bị che khuất
                var ownerWindow = System.Windows.Application.Current?.Dispatcher.Invoke(
                    () => System.Windows.Application.Current.Windows
                            .OfType<System.Windows.Window>()
                            .FirstOrDefault(w => w.IsActive)
                          ?? System.Windows.Application.Current.MainWindow);
                bool ok = FlowMy.Helpers.InterceptionInputHelper.PromptAndInstallDriver(ownerWindow);
                if (!ok)
                {
                    // Revert về Auto nếu user từ chối hoặc cài thất bại
                    BackgroundInputMode = FlowMy.Helpers.BackgroundInputHelper.InputMode.Auto;
                }
            }
        }

        partial void OnReturnToOriginalScreenChanged(bool value)
        {
            _hotkeyPressNode.ReturnToOriginalScreen = value;
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
            _hotkeyPressNode.TriggerMode     = TriggerMode;

            // ManualPosition đã được set trực tiếp vào node từ PickPositionButton_Click
            // Không cần sync lại ở đây

            _hotkeyPressNode.TargetProcessName = SelectedTargetWindow?.ProcessName ?? string.Empty;
            _hotkeyPressNode.TargetWindowTitle = SelectedTargetWindow?.Title       ?? string.Empty;

            _hotkeyPressNode.UseBackgroundMode = UseBackgroundMode;
            _hotkeyPressNode.BackgroundInputMode = BackgroundInputMode;
            _hotkeyPressNode.ReturnToOriginalScreen = ReturnToOriginalScreen;
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

    /// <summary>
    /// Wrapper class để hiển thị BackgroundInputMode trong ComboBox với text tùy chỉnh.
    /// </summary>
    public class BackgroundInputModeOption
    {
        public FlowMy.Helpers.BackgroundInputHelper.InputMode Value { get; set; }
        public string DisplayName { get; set; }

        public BackgroundInputModeOption(FlowMy.Helpers.BackgroundInputHelper.InputMode value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }
}
