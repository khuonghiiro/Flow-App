using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace FlowMy.ViewModels
{
    public partial class MacroRecorderNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly MacroRecorderNode _macroRecorderNode;

        [ObservableProperty]
        private string _outputKey;

        [ObservableProperty]
        private string _macroDataJson;

        [ObservableProperty]
        private string _selectedPlaybackMode;

        [ObservableProperty]
        private int _repeatIntervalMs;

        [ObservableProperty]
        private int _repeatCount;

        /// <summary>
        /// Trả về true khi SelectedPlaybackMode == "Lặp lại", false khi "Chạy 1 lần".
        /// </summary>
        public bool IsRepeatVisible => SelectedPlaybackMode == "Lặp lại";

        /// <summary>
        /// Trả về true khi MacroDataJson có nội dung (không null, không rỗng, không chỉ khoảng trắng).
        /// </summary>
        public bool CanExportJson => !string.IsNullOrWhiteSpace(MacroDataJson);

        /// <summary>
        /// Danh sách chế độ phát lại cho ComboBox.
        /// </summary>
        public ObservableCollection<string> PlaybackModeOptions { get; } = new()
        {
            "Chạy 1 lần",
            "Lặp lại"
        };

        public MacroRecorderNodeDialogViewModel(MacroRecorderNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _macroRecorderNode = node;

            // Khởi tạo từ node properties
            _outputKey = node.OutputKey ?? "macroData";
            _macroDataJson = node.MacroDataJson ?? "";
            _selectedPlaybackMode = node.PlaybackMode == MacroPlaybackMode.Repeat ? "Lặp lại" : "Chạy 1 lần";
            _repeatIntervalMs = node.RepeatIntervalMs;
            _repeatCount = node.RepeatCount;

            // Sync từ node khi node thay đổi bên ngoài
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MacroRecorderNode.OutputKey))
                        OutputKey = node.OutputKey ?? "macroData";
                    else if (e.PropertyName == nameof(MacroRecorderNode.MacroDataJson))
                    {
                        MacroDataJson = node.MacroDataJson ?? "";
                        OnPropertyChanged(nameof(CanExportJson));
                    }
                    else if (e.PropertyName == nameof(MacroRecorderNode.PlaybackMode))
                        SelectedPlaybackMode = node.PlaybackMode == MacroPlaybackMode.Repeat ? "Lặp lại" : "Chạy 1 lần";
                    else if (e.PropertyName == nameof(MacroRecorderNode.RepeatIntervalMs))
                        RepeatIntervalMs = node.RepeatIntervalMs;
                    else if (e.PropertyName == nameof(MacroRecorderNode.RepeatCount))
                        RepeatCount = node.RepeatCount;

                    OnNodePropertyChanged(e.PropertyName);
                };
            }
        }

        protected override string GetDefaultTitle() => "Macro Recorder";

        /// <summary>
        /// Khi SelectedPlaybackMode thay đổi, thông báo IsRepeatVisible để UI cập nhật visibility.
        /// </summary>
        partial void OnSelectedPlaybackModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsRepeatVisible));
        }

        /// <summary>
        /// Khi MacroDataJson thay đổi, thông báo CanExportJson để UI cập nhật trạng thái nút Export.
        /// </summary>
        partial void OnMacroDataJsonChanged(string value)
        {
            OnPropertyChanged(nameof(CanExportJson));
        }

        /// <summary>
        /// Override OnSaveTitle để sync tất cả properties về node.
        /// </summary>
        protected override void OnSaveTitle()
        {
            bool needSync = false;

            // Sync NodeTitle về node.Title
            if (_macroRecorderNode.Title != NodeTitle)
            {
                _macroRecorderNode.Title = NodeTitle;
                needSync = true;
            }

            if (_macroRecorderNode.OutputKey != OutputKey)
            {
                _macroRecorderNode.OutputKey = OutputKey;
                needSync = true;
            }

            if (_macroRecorderNode.MacroDataJson != MacroDataJson)
            {
                _macroRecorderNode.MacroDataJson = MacroDataJson;
                needSync = true;
            }

            var newPlaybackMode = SelectedPlaybackMode == "Lặp lại"
                ? MacroPlaybackMode.Repeat
                : MacroPlaybackMode.Once;

            if (_macroRecorderNode.PlaybackMode != newPlaybackMode)
            {
                _macroRecorderNode.PlaybackMode = newPlaybackMode;
                needSync = true;
            }

            if (_macroRecorderNode.RepeatIntervalMs != RepeatIntervalMs)
            {
                _macroRecorderNode.RepeatIntervalMs = RepeatIntervalMs;
                needSync = true;
            }

            if (_macroRecorderNode.RepeatCount != RepeatCount)
            {
                _macroRecorderNode.RepeatCount = RepeatCount;
                needSync = true;
            }

            if (needSync)
                _host.RequestSyncDataPanels(immediate: true);
        }
    }
}
