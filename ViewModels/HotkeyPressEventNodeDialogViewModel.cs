using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
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

        public HotkeyPressEventNodeDialogViewModel(HotkeyPressEventNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _hotkeyPressNode = node;
            _hotkeyDisplayText = FormatHotkeyText(node.Key);
            _pressDelayMs = node.PressDelayMs;

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

        // Override OnSaveTitle để thêm logic riêng thay vì override SaveTitle
        protected override void OnSaveTitle()
        {
            if (_hotkeyPressNode.PressDelayMs != PressDelayMs)
            {
                _hotkeyPressNode.PressDelayMs = PressDelayMs;
                _host.RequestSyncDataPanels(immediate: true);
            }
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
