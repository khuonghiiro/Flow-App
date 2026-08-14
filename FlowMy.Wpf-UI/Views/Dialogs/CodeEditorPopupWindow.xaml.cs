// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.ViewModels;
using System.Windows;

namespace FlowMy.Views.Overlays
{
    public partial class CodeEditorPopupWindow : Window
    {
        public CodeEditorPopupWindow(CodeNodeDialogViewModel viewModel, Window owner)
        {
            InitializeComponent();
            Owner = owner;
            DataContext = viewModel;
            if (PopupVarNameRun != null)
                PopupVarNameRun.Text = viewModel.EffectiveInputKeyDisplay;
        }

        private void ClosePopupButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
