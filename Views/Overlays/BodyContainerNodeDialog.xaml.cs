using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using FlowMy.Views.NodeControls;
using System;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using WinForms = System.Windows.Forms;

namespace FlowMy.Views.Overlays;

public partial class BodyContainerNodeDialog : BaseNodeDialog
{
    private readonly BodyContainerNodeDialogViewModel _viewModel;
    private readonly BodyContainerNode _node;

    public BodyContainerNodeDialog(BodyContainerNode node, IWorkflowEditorHost host, Window? owner)
        : base()
    {
        _node = node;
        InitializeComponent();
        _viewModel = new BodyContainerNodeDialogViewModel(node, host);
        InitializeBase(_viewModel, owner);
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BodyContainerNodeDialogViewModel.BodyBackgroundColorHex) ||
                e.PropertyName == nameof(BodyContainerNodeDialogViewModel.BodyBorderColorHex) ||
                e.PropertyName == nameof(BodyContainerNodeDialogViewModel.BackgroundOpacityPercent))
            {
                UpdateColorPreviews();
            }

            if (e.PropertyName == nameof(BodyContainerNodeDialogViewModel.BodyBackgroundColorHex) ||
                e.PropertyName == nameof(BodyContainerNodeDialogViewModel.BodyBorderColorHex) ||
                e.PropertyName == nameof(BodyContainerNodeDialogViewModel.UseUnifiedColors) ||
                e.PropertyName == nameof(BodyContainerNodeDialogViewModel.BackgroundOpacityPercent) ||
                e.PropertyName == nameof(BodyContainerNodeDialogViewModel.LockInnerNodes))
            {
                // Direct refresh safeguard from dialog side.
                BodyContainerControl.RefreshVisualFromNode(_node);
            }
        };
        UpdateColorPreviews();
    }

    protected override Panel? GetInputsPanel() => null;

    protected override Panel? GetOutputsPanel() => null;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        FlushEditorsToViewModel();
        ViewModel.SaveTitleCommand.Execute(null);
        Close();
    }

    protected override void BeforeSaveOnClose()
    {
        FlushEditorsToViewModel();
    }

    private void PickBodyBackgroundColor_Click(object sender, RoutedEventArgs e)
    {
        var picked = ShowColorPicker(_viewModel.BodyBackgroundColorHex);
        if (!string.IsNullOrWhiteSpace(picked))
            _viewModel.BodyBackgroundColorKey = picked;
    }

    private void PickBodyBorderColor_Click(object sender, RoutedEventArgs e)
    {
        var picked = ShowColorPicker(_viewModel.BodyBorderColorHex);
        if (!string.IsNullOrWhiteSpace(picked))
            _viewModel.BodyBorderColorKey = picked;
    }

    private static string? ShowColorPicker(string? currentHex)
    {
        try
        {
            using var dialog = new WinForms.ColorDialog { FullOpen = true };
            if (!string.IsNullOrWhiteSpace(currentHex) && currentHex.StartsWith("#", StringComparison.OrdinalIgnoreCase))
            {
                try { dialog.Color = System.Drawing.ColorTranslator.FromHtml(currentHex); } catch { }
            }
            return dialog.ShowDialog() == WinForms.DialogResult.OK
                ? $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"
                : null;
        }
        catch
        {
            return null;
        }
    }

    private void UpdateColorPreviews()
    {
        if (BodyBackgroundColorPreview != null)
        {
            var bgBrush = ResolveBrush(_viewModel.BodyBackgroundColorHex, new SolidColorBrush(Color.FromRgb(107, 114, 128)));
            bgBrush = bgBrush.Clone();
            bgBrush.Opacity = Math.Clamp(_viewModel.BackgroundOpacityPercent / 100.0, 0.0, 1.0);
            BodyBackgroundColorPreview.Background = bgBrush;
        }

        if (BodyBorderColorPreview != null)
        {
            BodyBorderColorPreview.Background = ResolveBrush(_viewModel.BodyBorderColorHex, new SolidColorBrush(Color.FromRgb(107, 114, 128)));
        }
    }

    private static Brush ResolveBrush(string? value, Brush fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        try
        {
            if (value.StartsWith("#", StringComparison.OrdinalIgnoreCase))
            {
                var bc = new BrushConverter();
                if (bc.ConvertFromString(value) is Brush fromHex) return fromHex;
            }
            var resource = Application.Current.TryFindResource(value);
            if (resource is Brush b) return b;
            if (resource is Color c) return new SolidColorBrush(c);
        }
        catch { }
        return fallback;
    }

    private void FlushEditorsToViewModel()
    {
        BodyBackgroundColorComboBox?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
        BodyBorderColorComboBox?.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
        UseUnifiedColorsCheckBox?.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateSource();
        LockInnerNodesCheckBox?.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateSource();
        BackgroundOpacitySlider?.GetBindingExpression(Slider.ValueProperty)?.UpdateSource();
        TitleTextBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        BindingOperations.GetBindingExpressionBase(this, DataContextProperty)?.UpdateSource();
    }
}
