using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Models.Nodes;
using Microsoft.Win32;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views.NodeControls
{
    /// <summary>
    /// Shared video-processing content used by both node canvas and floating widget.
    /// </summary>
    public sealed class VideoProcessingNodeContentControl : UserControl
    {
        private readonly VideoProcessingNode _node;
        private TextBlock _previewText = null!;
        private MediaElement _previewMedia = null!;
        private TextBlock _fpsText = null!;
        private Slider _fpsSlider = null!;
        private TextBlock _audioSummary = null!;
        private readonly NotifyCollectionChangedEventHandler _audioTracksChangedHandler;
        private readonly PropertyChangedEventHandler _propertyChangedHandler;
        private bool _subscriptionsAttached;

        public VideoProcessingNodeContentControl(VideoProcessingNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));

            var textBrush = GetTextBrush(node.ColorKey);
            Content = BuildLayout(textBrush);

            _audioTracksChangedHandler = (_, _) => RefreshInlineText();

            _propertyChangedHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(VideoProcessingNode.SourceFps))
                {
                    _fpsSlider.Maximum = Math.Max(1, _node.SourceFps);
                    if (_node.ExtractFps > _fpsSlider.Maximum) _node.ExtractFps = _fpsSlider.Maximum;
                    RefreshInlineText();
                }
                else if (e.PropertyName == nameof(VideoProcessingNode.ExtractFps) ||
                         e.PropertyName == nameof(VideoProcessingNode.OutputBase64))
                {
                    RefreshInlineText();
                }
                else if (e.PropertyName == nameof(VideoProcessingNode.VideoPath))
                {
                    RefreshVideoPreview();
                }
            };
            Loaded += (_, _) =>
            {
                AttachSubscriptions();
                RefreshInlineText();
                RefreshVideoPreview();
            };
            Unloaded += (_, _) => DetachSubscriptions();
        }

        private FrameworkElement BuildLayout(Brush textBrush)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(null, typeof(Uri), "circle-video sharp-light",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var icon = new SvgViewboxEx
            {
                Source = iconUri,
                Width = 26,
                Height = 26,
                Margin = new Thickness(8, 8, 8, 4),
                Fill = textBrush,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var selectVideoButton = new Button
            {
                Content = "Mo video...",
                Style = Application.Current.TryFindResource("PrimaryButton") as Style,
                Width = 84,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 8, 8, 4)
            };
            selectVideoButton.Click += (_, e) =>
            {
                e.Handled = true;
                SelectVideo();
            };

            var topPanel = new Grid();
            topPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topPanel.Children.Add(icon);
            Grid.SetColumn(icon, 0);
            topPanel.Children.Add(selectVideoButton);
            Grid.SetColumn(selectVideoButton, 2);
            root.Children.Add(topPanel);
            Grid.SetRow(topPanel, 0);

            var previewBorder = new Border
            {
                Margin = new Thickness(8, 0, 8, 6),
                Background = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6)
            };
            var previewGrid = new Grid();
            _previewText = new TextBlock
            {
                Foreground = textBrush,
                Margin = new Thickness(8),
                FontSize = 11,
                Opacity = 0.85,
                TextWrapping = TextWrapping.Wrap,
                Text = "Chua chon video"
            };
            _previewMedia = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                Stretch = Stretch.UniformToFill,
                Volume = 0,
                ScrubbingEnabled = true,
                Visibility = Visibility.Collapsed
            };
            _previewMedia.MediaEnded += (_, _) =>
            {
                _previewMedia.Position = TimeSpan.Zero;
                _previewMedia.Play();
            };
            previewGrid.Children.Add(_previewMedia);
            previewGrid.Children.Add(_previewText);
            previewBorder.Child = previewGrid;
            root.Children.Add(previewBorder);
            Grid.SetRow(previewBorder, 1);

            _fpsText = new TextBlock
            {
                Margin = new Thickness(8, 0, 8, 4),
                Foreground = textBrush,
                FontSize = 11
            };
            root.Children.Add(_fpsText);
            Grid.SetRow(_fpsText, 2);

            _fpsSlider = CreateSlider(1, Math.Max(1, _node.SourceFps), _node.ExtractFps, v => _node.ExtractFps = v);
            _fpsSlider.Margin = new Thickness(8, 0, 8, 4);
            root.Children.Add(_fpsSlider);
            Grid.SetRow(_fpsSlider, 3);

            var gradingPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8, 0, 8, 6) };
            gradingPanel.Children.Add(CreateLabeledSlider("Brightness", -1, 1, _node.Brightness, v => _node.Brightness = v));
            gradingPanel.Children.Add(CreateLabeledSlider("Contrast", 0.1, 3, _node.Contrast, v => _node.Contrast = v));
            gradingPanel.Children.Add(CreateLabeledSlider("Saturation", 0, 3, _node.Saturation, v => _node.Saturation = v));
            gradingPanel.Children.Add(CreateLabeledSlider("Hue", -180, 180, _node.Hue, v => _node.Hue = v));
            root.Children.Add(gradingPanel);
            Grid.SetRow(gradingPanel, 4);

            _audioSummary = new TextBlock
            {
                Margin = new Thickness(8, 0, 8, 8),
                Foreground = textBrush,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            root.Children.Add(_audioSummary);
            Grid.SetRow(_audioSummary, 5);

            return root;
        }

        private void SelectVideo()
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Chon video",
                    Filter = "Video Files|*.mp4;*.mov;*.mkv;*.avi;*.webm|All Files|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };
                if (dlg.ShowDialog() == true)
                {
                    _node.VideoPath = dlg.FileName;
                    _node.RaisePropertyChanged(nameof(VideoProcessingNode.VideoPath));
                }
            }
            catch
            {
                // best-effort
            }
        }

        private void RefreshInlineText()
        {
            _fpsText.Text = $"FPS video: {_node.SourceFps:0.##} | Trich: {_node.ExtractFps:0.##}/s";
            _audioSummary.Text = $"Audio tracks: {_node.AudioTracks.Count} | Output: {(_node.OutputBase64 ? "base64" : "file")}";
        }

        private void RefreshVideoPreview()
        {
            var path = _node.VideoPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                _previewMedia.Stop();
                _previewMedia.Source = null;
                _previewMedia.Visibility = Visibility.Collapsed;
                _previewText.Visibility = Visibility.Visible;
                _previewText.Text = "Chua chon video";
                return;
            }

            try
            {
                _previewMedia.Source = Uri.TryCreate(path, UriKind.Absolute, out var uri) ? uri : new Uri(path, UriKind.RelativeOrAbsolute);
                _previewMedia.Visibility = Visibility.Visible;
                _previewText.Visibility = Visibility.Collapsed;
                _previewMedia.Position = TimeSpan.Zero;
                _previewMedia.Play();
            }
            catch
            {
                _previewMedia.Stop();
                _previewMedia.Source = null;
                _previewMedia.Visibility = Visibility.Collapsed;
                _previewText.Visibility = Visibility.Visible;
                _previewText.Text = "Khong the preview video";
            }
        }

        private void AttachSubscriptions()
        {
            if (_subscriptionsAttached) return;
            _node.AudioTracks.CollectionChanged += _audioTracksChangedHandler;
            _node.PropertyChanged += _propertyChangedHandler;
            _subscriptionsAttached = true;
        }

        private void DetachSubscriptions()
        {
            if (!_subscriptionsAttached) return;
            _previewMedia.Stop();
            _node.AudioTracks.CollectionChanged -= _audioTracksChangedHandler;
            _node.PropertyChanged -= _propertyChangedHandler;
            _subscriptionsAttached = false;
        }

        private static Slider CreateSlider(double min, double max, double value, Action<double> onChanged)
        {
            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                TickFrequency = 0.1,
                IsSnapToTickEnabled = false
            };
            slider.ValueChanged += (_, e) => onChanged(e.NewValue);
            return slider;
        }

        private static FrameworkElement CreateLabeledSlider(string label, double min, double max, double value, Action<double> onChanged)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 3) };
            stack.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = Brushes.White, Opacity = 0.9 });
            stack.Children.Add(CreateSlider(min, max, value, onChanged));
            return stack;
        }

        private static Brush GetTextBrush(string? colorKey)
        {
            if (string.IsNullOrWhiteSpace(colorKey))
                return new SolidColorBrush(Color.FromRgb(229, 231, 235));
            return Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush
                   ?? new SolidColorBrush(Color.FromRgb(229, 231, 235));
        }
    }
}
