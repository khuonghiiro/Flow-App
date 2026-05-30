using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FlowMy.Views.Overlays
{
    public partial class BorderHighlightNodeDialog : BaseNodeDialog
    {
        private readonly BorderHighlightNodeDialogViewModel _viewModel;

        public BorderHighlightNodeDialog(BorderHighlightNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new BorderHighlightNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner);

            UpdateTitleColorPreview();
            UpdateBorderColorPreview();
            UpdateEffectPreview();

            // Set initial visibility of target app panel
            TargetAppPanel.Visibility = _viewModel.HighlightMode == Models.BorderHighlightMode.TargetApp
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Hide/show target app panel based on mode
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.HighlightMode))
                {
                    TargetAppPanel.Visibility = _viewModel.HighlightMode == Models.BorderHighlightMode.TargetApp
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else if (e.PropertyName == nameof(_viewModel.BorderColorHex))
                {
                    UpdateBorderColorPreview();
                    UpdateEffectPreview();
                }
                else if (e.PropertyName == nameof(_viewModel.EffectType))
                {
                    UpdateEffectPreview();
                }
            };
        }

        protected override Panel? GetInputsPanel() => InputsPanel;

        protected override Panel? GetOutputsPanel() => OutputsPanel;

        private void BorderColorPickerButton_Click(object sender, RoutedEventArgs e)
        {
            var hex = ShowColorPicker(_viewModel.BorderColorHex);
            if (!string.IsNullOrWhiteSpace(hex))
            {
                _viewModel.BorderColorHex = hex;
                UpdateBorderColorPreview();
            }
        }

        private void UpdateBorderColorPreview()
        {
            if (BorderColorPreview != null)
            {
                BorderColorPreview.Background = ResolveBrush(_viewModel.BorderColorHex, Brushes.Transparent);
            }
        }

        private void EffectTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateEffectPreview();
        }

        private void UpdateEffectPreview()
        {
            if (EffectPreview == null) return;

            // Reset animations
            EffectPreview.BeginAnimation(BorderBrushProperty, null);
            EffectPreview.BeginAnimation(OpacityProperty, null);

            var effectType = _viewModel.EffectType;
            var color = ResolveBrush(_viewModel.BorderColorHex, Brushes.Cyan) as SolidColorBrush;
            if (color == null) color = new SolidColorBrush(Colors.Cyan);

            EffectPreview.BorderBrush = color;

            switch (effectType)
            {
                case BorderEffectType.Pulse:
                    var pulseStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
                    var pulseAnim = new DoubleAnimation
                    {
                        From = 0.3,
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(1.0),
                        AutoReverse = true
                    };
                    Storyboard.SetTarget(pulseAnim, EffectPreview);
                    Storyboard.SetTargetProperty(pulseAnim, new PropertyPath("Opacity"));
                    pulseStoryboard.Children.Add(pulseAnim);
                    pulseStoryboard.Begin();
                    break;

                case BorderEffectType.Glow:
                    var glowStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
                    var glowAnim = new DoubleAnimation
                    {
                        From = 0.5,
                        To = 1.0,
                        Duration = TimeSpan.FromSeconds(0.5),
                        AutoReverse = true
                    };
                    Storyboard.SetTarget(glowAnim, EffectPreview);
                    Storyboard.SetTargetProperty(glowAnim, new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Opacity)"));
                    glowStoryboard.Children.Add(glowAnim);
                    glowStoryboard.Begin();
                    break;

                case BorderEffectType.Rainbow:
                    var rainbowStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
                    var colors = new[] { Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Blue, Colors.Indigo, Colors.Violet };
                    
                    for (int i = 0; i < colors.Length; i++)
                    {
                        var colorAnim = new ColorAnimation
                        {
                            From = colors[i],
                            To = colors[(i + 1) % colors.Length],
                            Duration = TimeSpan.FromSeconds(1),
                            BeginTime = TimeSpan.FromSeconds(i)
                        };
                        
                        Storyboard.SetTarget(colorAnim, EffectPreview);
                        Storyboard.SetTargetProperty(colorAnim, new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Color)"));
                        rainbowStoryboard.Children.Add(colorAnim);
                    }
                    
                    rainbowStoryboard.Begin();
                    break;

                case BorderEffectType.None:
                default:
                    EffectPreview.Opacity = 1.0;
                    break;
            }
        }
    }
}
