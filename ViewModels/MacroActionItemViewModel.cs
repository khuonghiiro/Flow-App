using CommunityToolkit.Mvvm.ComponentModel;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// ViewModel cho một item trong danh sách thao tác (multi-action list).
    /// Bao gồm thông tin thao tác, speed override, và trạng thái default.
    /// </summary>
    public partial class MacroActionItemViewModel : ObservableObject
    {
        [ObservableProperty] private string _id = string.Empty;
        [ObservableProperty] private string _name = "Thao tác mới";
        [ObservableProperty] private string _macroDataJson = "";
        [ObservableProperty] private bool _isDefault;

        // ─── Speed Override Settings ───
        // -1 = dùng thời gian gốc, >= 0 = override (ms)
        [ObservableProperty] private int _mouseMoveDelayMs = -1;
        [ObservableProperty] private int _keyPressDelayMs = -1;
        [ObservableProperty] private int _mouseClickDelayMs = -1;
        [ObservableProperty] private int _mouseScrollDelayMs = -1;

        /// <summary>Có dữ liệu macro hay chưa.</summary>
        public bool HasMacroData => !string.IsNullOrWhiteSpace(MacroDataJson);

        partial void OnMacroDataJsonChanged(string value)
            => OnPropertyChanged(nameof(HasMacroData));

        // ─── Display helpers ───

        /// <summary>Text hiển thị cho speed override (-1 = "Gốc")</summary>
        public string MouseMoveDelayDisplay => _mouseMoveDelayMs < 0 ? "Gốc" : $"{_mouseMoveDelayMs}ms";
        public string KeyPressDelayDisplay => _keyPressDelayMs < 0 ? "Gốc" : $"{_keyPressDelayMs}ms";
        public string MouseClickDelayDisplay => _mouseClickDelayMs < 0 ? "Gốc" : $"{_mouseClickDelayMs}ms";
        public string MouseScrollDelayDisplay => _mouseScrollDelayMs < 0 ? "Gốc" : $"{_mouseScrollDelayMs}ms";
    }
}
