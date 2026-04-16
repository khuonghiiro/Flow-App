using FlowMy.ViewModels;
using System.Windows;

namespace FlowMy.Views.Overlays
{
    public partial class HtmlUiEditorPopupWindow : Window
    {
        public HtmlUiEditorPopupWindow(HtmlUiNodeDialogViewModel viewModel, Window owner)
        {
            InitializeComponent();
            Owner = owner;
            DataContext = viewModel;
        }

        private void ClosePopupButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

