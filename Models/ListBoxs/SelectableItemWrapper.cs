using CommunityToolkit.Mvvm.ComponentModel;

namespace FlowMy.Models.ListBoxs
{
    public partial class SelectableItemWrapper : ObservableObject
    {
        public static event Action<SelectableItemWrapper> SelectionChanged;

        [ObservableProperty]
        private string id = string.Empty;

        [ObservableProperty]
        private string displayText = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private string additionalInfo = string.Empty;

        [ObservableProperty]
        private bool isSelected = false;

        [ObservableProperty]
        private bool isInactive;

        [ObservableProperty]
        private string checkBoxText = string.Empty;

        public object OriginalObject { get; set; }

        // Thêm method này để handle khi IsSelected thay đổi
        partial void OnIsSelectedChanged(bool value)
        {
            // Trigger update through a static event
            SelectionChanged?.Invoke(this);
        }
    }
}
