using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace FlowMy.Views.Overlays
{
    public partial class NotificationNodeDialog : BaseNodeDialog
    {
        private readonly NotificationNodeDialogViewModel _viewModel;

        public NotificationNodeDialog(NotificationNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();

            _viewModel = new NotificationNodeDialogViewModel(node, host);

            InitializeBase(_viewModel, owner);

            _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
            UpdateColorPreviews();
        }

        protected override Panel? GetInputsPanel() => null;

        protected override Panel? GetOutputsPanel() => null;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        private void ViewModelOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NotificationNodeDialogViewModel.ToastTitleColorKey) ||
                e.PropertyName == nameof(NotificationNodeDialogViewModel.ToastContentColorKey) ||
                e.PropertyName == nameof(NotificationNodeDialogViewModel.ToastBackgroundColorKey) ||
                e.PropertyName == nameof(NotificationNodeDialogViewModel.ToastBackgroundOpacity))
            {
                UpdateColorPreviews();
            }
        }

        private void PickToastTitleColor_Click(object sender, RoutedEventArgs e)
        {
            var hex = ShowColorPicker(_viewModel.ToastTitleColorKey);
            if (!string.IsNullOrWhiteSpace(hex))
            {
                _viewModel.ToastTitleColorKey = hex;
                UpdateColorPreviews();
            }
        }

        private void PickToastContentColor_Click(object sender, RoutedEventArgs e)
        {
            var hex = ShowColorPicker(_viewModel.ToastContentColorKey);
            if (!string.IsNullOrWhiteSpace(hex))
            {
                _viewModel.ToastContentColorKey = hex;
                UpdateColorPreviews();
            }
        }

        private void PickToastBackgroundColor_Click(object sender, RoutedEventArgs e)
        {
            var hex = ShowColorPicker(_viewModel.ToastBackgroundColorKey);
            if (!string.IsNullOrWhiteSpace(hex))
            {
                _viewModel.ToastBackgroundColorKey = hex;
                UpdateColorPreviews();
            }
        }

        /// <summary>
        /// Mở ColorDialog và trả về mã màu dạng #RRGGBB nếu user chọn OK.
        /// </summary>
        private static string? ShowColorPicker(string? currentHex)
        {
            try
            {
                using var dialog = new WinForms.ColorDialog
                {
                    FullOpen = true
                };

                // Nếu đang có mã hex hiện tại, set làm màu ban đầu
                if (!string.IsNullOrWhiteSpace(currentHex) && currentHex.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var color = System.Drawing.ColorTranslator.FromHtml(currentHex);
                        dialog.Color = color;
                    }
                    catch
                    {
                        // ignore
                    }
                }

                var result = dialog.ShowDialog();
                if (result == WinForms.DialogResult.OK)
                {
                    var c = dialog.Color;
                    // Convert to #RRGGBB
                    return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }
            }
            catch
            {
            }

            return null;
        }

        private void UpdateColorPreviews()
        {
            try
            {
                if (ToastTitleColorPreview != null)
                {
                    ToastTitleColorPreview.Background = ResolveBrush(_viewModel.ToastTitleColorKey,
                        Application.Current.TryFindResource("CardColor") as Brush
                        ?? new SolidColorBrush(Colors.White));
                }

                if (ToastContentColorPreview != null)
                {
                    ToastContentColorPreview.Background = ResolveBrush(_viewModel.ToastContentColorKey,
                        Application.Current.TryFindResource("TextSecondary") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(229, 231, 235)));
                }

                if (ToastBackgroundColorPreview != null)
                {
                    var bgBrush = ResolveBrush(_viewModel.ToastBackgroundColorKey,
                        Application.Current.TryFindResource("DarkBrush") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(15, 23, 42)));
                    bgBrush = bgBrush.Clone();
                    bgBrush.Opacity = _viewModel.ToastBackgroundOpacity;
                    ToastBackgroundColorPreview.Background = bgBrush;
                }
            }
            catch
            {
            }
        }

        private static Brush ResolveBrush(string? key, Brush fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback;

            try
            {
                if (key == "LimeGreen")
                    return new SolidColorBrush(Colors.LimeGreen);

                if (key.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                {
                    var converter = new BrushConverter();
                    var brush = converter.ConvertFromString(key) as Brush;
                    if (brush != null)
                        return brush;
                }

                var resource = Application.Current.TryFindResource(key);
                if (resource is Brush b)
                    return b;
                if (resource is Color c)
                    return new SolidColorBrush(c);
            }
            catch
            {
            }

            return fallback;
        }
    }
}

