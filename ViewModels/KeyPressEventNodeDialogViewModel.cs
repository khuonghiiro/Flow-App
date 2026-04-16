using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
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

        public KeyPressEventNodeDialogViewModel(KeyPressEventNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _keyPressNode = node;
            _keyDisplayText = FormatKeyText(node.Key);
            _pressDelayMs = node.PressDelayMs;

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

