using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace FlowMy.Models.ListBoxs
{
    public partial class SelectedItemTag : ObservableObject
    {
        [ObservableProperty]
        private string id;

        [ObservableProperty]
        private string displayText;

        [ObservableProperty]
        private Color tagColor;

        [ObservableProperty]
        private bool isInactive; // THÊM DÒNG NÀY
    }

}
