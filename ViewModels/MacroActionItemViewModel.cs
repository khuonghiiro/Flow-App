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

        // ─── Skip Settings ───
        // true = bỏ qua hoàn toàn loại thao tác này khi phát lại
        [ObservableProperty] private bool _skipMouseMove;
        [ObservableProperty] private bool _skipKeyPress;
        [ObservableProperty] private bool _skipMouseClick;
        [ObservableProperty] private bool _skipMouseScroll;

        // ─── IsEnabled for TextBox (disabled when skip is checked) ───
        public bool IsMouseMoveDelayEnabled => !SkipMouseMove;
        public bool IsKeyPressDelayEnabled => !SkipKeyPress;
        public bool IsMouseClickDelayEnabled => !SkipMouseClick;
        public bool IsMouseScrollDelayEnabled => !SkipMouseScroll;

        partial void OnSkipMouseMoveChanged(bool value) => OnPropertyChanged(nameof(IsMouseMoveDelayEnabled));
        partial void OnSkipKeyPressChanged(bool value) => OnPropertyChanged(nameof(IsKeyPressDelayEnabled));
        partial void OnSkipMouseClickChanged(bool value) => OnPropertyChanged(nameof(IsMouseClickDelayEnabled));
        partial void OnSkipMouseScrollChanged(bool value) => OnPropertyChanged(nameof(IsMouseScrollDelayEnabled));

        /// <summary>Có dữ liệu macro hay chưa.</summary>
        public bool HasMacroData => !string.IsNullOrWhiteSpace(MacroDataJson);

        partial void OnMacroDataJsonChanged(string value)
            => OnPropertyChanged(nameof(HasMacroData));

        // ─── Display helpers ───

        /// <summary>Text hiển thị cho speed override (-1 = "Gốc")</summary>
        public string MouseMoveDelayDisplay => MouseMoveDelayMs < 0 ? "Gốc" : $"{MouseMoveDelayMs}ms";
        public string KeyPressDelayDisplay => KeyPressDelayMs < 0 ? "Gốc" : $"{KeyPressDelayMs}ms";
        public string MouseClickDelayDisplay => MouseClickDelayMs < 0 ? "Gốc" : $"{MouseClickDelayMs}ms";
        public string MouseScrollDelayDisplay => MouseScrollDelayMs < 0 ? "Gốc" : $"{MouseScrollDelayMs}ms";
    }
}
