using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class EmbedApplicationNodeDialog : BaseNodeDialog
    {
        private readonly EmbedApplicationNodeDialogViewModel _viewModel;

        public EmbedApplicationNodeDialog(EmbedApplicationNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new EmbedApplicationNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            UpdateTitleColorPreview();
        }

        protected override Panel? GetInputsPanel() => null;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void BeforeSaveOnClose()
        {
            // Flush all bindings
            ApplicationComboBox?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
            TitleTextBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            
            // Flush các TextBox khác (EmbeddedWidth, EmbeddedHeight)
            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
            
            // Flush các CheckBox (IsActive, ShowBorder, AllowInteraction, AutoRefresh)
            foreach (var checkBox in FindVisualChildren<CheckBox>(this))
            {
                checkBox.GetBindingExpression(CheckBox.IsCheckedProperty)?.UpdateSource();
            }
            
            // Flush Slider (RefreshRate)
            foreach (var slider in FindVisualChildren<Slider>(this))
            {
                slider.GetBindingExpression(Slider.ValueProperty)?.UpdateSource();
            }
            
            // Flush ComboBoxes (CaptureMode, TitleDisplayMode, TitleColorComboBox)
            foreach (var comboBox in FindVisualChildren<ComboBox>(this))
            {
                comboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                    yield return t;
                
                foreach (var childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }
    }
}
