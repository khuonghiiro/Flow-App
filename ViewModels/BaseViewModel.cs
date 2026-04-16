using CommunityToolkit.Mvvm.ComponentModel;

namespace FlowMy.ViewModels
{
    public partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string? error;

        public virtual void OnAppearing() { }
        public virtual void OnDisappearing() { }
    }
}
