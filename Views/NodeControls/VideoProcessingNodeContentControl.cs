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
    public partial class VideoProcessingNodeContentControl : UserControl
    {
        private const double MinAutoFitNodeWidth = 540;
        private const double MinAutoFitNodeHeight = 340;
        private const double MaxAutoFitNodeWidth = 1280;
        private const double MaxAutoFitNodeHeight = 920;
        private const double MinPreviewHeight = 180;
        private const double MaxPreviewHeight = 620;
        private const double HorizontalPadding = 18;
        private const double NonPreviewContentHeight = 208;

        private readonly VideoProcessingNode _node;
        private readonly NotifyCollectionChangedEventHandler _audioTracksChangedHandler;
        private readonly PropertyChangedEventHandler _propertyChangedHandler;
        private bool _subscriptionsAttached;

        public event Action<double, double>? SuggestedNodeSizeReady;

        public VideoProcessingNodeContentControl(VideoProcessingNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            InitializeComponent();
            ApplyThemeBrushes(GetTextBrush(_node.ColorKey));
            InitializeIcon();
            InitializeInteractiveControls();

            _audioTracksChangedHandler = (_, _) => RefreshInfoText();
            _propertyChangedHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(VideoProcessingNode.SourceFps))
                {
                    FpsSlider.Maximum = Math.Max(1, _node.SourceFps);
                    if (_node.ExtractFps > FpsSlider.Maximum) _node.ExtractFps = FpsSlider.Maximum;
                }

                if (e.PropertyName == nameof(VideoProcessingNode.VideoPath))
                    RefreshVideoPreview();

                if (e.PropertyName is nameof(VideoProcessingNode.SourceFps)
                    or nameof(VideoProcessingNode.ExtractFps)
                    or nameof(VideoProcessingNode.OutputBase64)
                    or nameof(VideoProcessingNode.PreferGpu)
                    or nameof(VideoProcessingNode.PreferredHwAccel))
                {
                    RefreshInfoText();
                    SyncControlValuesFromModel();
                }
            };

            Loaded += (_, _) =>
            {
                AttachSubscriptions();
                SyncControlValuesFromModel();
                RefreshInfoText();
                RefreshVideoPreview();
            };
            Unloaded += (_, _) => DetachSubscriptions();
        }

        private void ApplyThemeBrushes(Brush textBrush)
        {
            TitleText.Foreground = textBrush;
            VideoPathText.Foreground = textBrush;
            PreviewText.Foreground = textBrush;
            MetaLineText.Foreground = textBrush;
            AudioSummaryText.Foreground = textBrush;
            OutputBase64CheckBox.Foreground = textBrush;
            PreferGpuCheckBox.Foreground = textBrush;
            BrightnessLabel.Foreground = textBrush;
            ContrastLabel.Foreground = textBrush;
            SaturationLabel.Foreground = textBrush;
            HueLabel.Foreground = textBrush;
        }

        private void InitializeIcon()
        {
            var iconConverter = new IconKeyToPathConverter();
            Uri? iconUri = iconConverter.Convert(string.Empty, typeof(Uri), "circle-video sharp-light",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            if (iconUri != null)
            {
                IconView.Source = iconUri;
            }
            IconView.Fill = GetTextBrush(_node.ColorKey);
        }

        private void InitializeInteractiveControls()
        {
            OpenVideoButton.Click += (_, e) =>
            {
                e.Handled = true;
                SelectVideo();
            };

            PreviewMedia.MediaOpened += (_, _) => EmitAutoFitSizeSuggestion();
            PreviewMedia.MediaEnded += (_, _) =>
            {
                PreviewMedia.Position = TimeSpan.Zero;
                PreviewMedia.Play();
            };

            FpsSlider.Minimum = 1;
            FpsSlider.Maximum = Math.Max(1, _node.SourceFps);
            FpsSlider.ValueChanged += (_, e) => _node.ExtractFps = e.NewValue;
            BrightnessSlider.ValueChanged += (_, e) =>
            {
                _node.Brightness = e.NewValue;
                BrightnessLabel.Text = $"Brightness: {e.NewValue:0.##}";
            };
            ContrastSlider.ValueChanged += (_, e) =>
            {
                _node.Contrast = e.NewValue;
                ContrastLabel.Text = $"Contrast: {e.NewValue:0.##}";
            };
            SaturationSlider.ValueChanged += (_, e) =>
            {
                _node.Saturation = e.NewValue;
                SaturationLabel.Text = $"Saturation: {e.NewValue:0.##}";
            };
            HueSlider.ValueChanged += (_, e) =>
            {
                _node.Hue = e.NewValue;
                HueLabel.Text = $"Hue: {e.NewValue:0.##}";
            };

            OutputBase64CheckBox.Checked += (_, _) => _node.OutputBase64 = true;
            OutputBase64CheckBox.Unchecked += (_, _) => _node.OutputBase64 = false;
            PreferGpuCheckBox.Checked += (_, _) => _node.PreferGpu = true;
            PreferGpuCheckBox.Unchecked += (_, _) => _node.PreferGpu = false;

            PresetNeutralButton.Click += (_, _) => ApplyGradingPreset(0, 1, 1, 0);
            PresetVividButton.Click += (_, _) => ApplyGradingPreset(0.05, 1.15, 1.35, 8);
            PresetCinematicButton.Click += (_, _) => ApplyGradingPreset(-0.08, 1.22, 0.82, -12);
            PresetBwButton.Click += (_, _) => ApplyGradingPreset(0, 1.1, 0.0, 0);
            ResetGradingButton.Click += (_, _) => ApplyGradingPreset(0, 1, 1, 0);
        }

        private void ApplyGradingPreset(double brightness, double contrast, double saturation, double hue)
        {
            _node.Brightness = brightness;
            _node.Contrast = contrast;
            _node.Saturation = saturation;
            _node.Hue = hue;
            SyncControlValuesFromModel();
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

        private void SyncControlValuesFromModel()
        {
            FpsSlider.Maximum = Math.Max(1, _node.SourceFps);
            FpsSlider.Value = _node.ExtractFps;
            OutputBase64CheckBox.IsChecked = _node.OutputBase64;
            PreferGpuCheckBox.IsChecked = _node.PreferGpu;
            BrightnessSlider.Value = _node.Brightness;
            ContrastSlider.Value = _node.Contrast;
            SaturationSlider.Value = _node.Saturation;
            HueSlider.Value = _node.Hue;
            BrightnessLabel.Text = $"Brightness: {_node.Brightness:0.##}";
            ContrastLabel.Text = $"Contrast: {_node.Contrast:0.##}";
            SaturationLabel.Text = $"Saturation: {_node.Saturation:0.##}";
            HueLabel.Text = $"Hue: {_node.Hue:0.##}";
        }

        private void RefreshInfoText()
        {
            var path = _node.VideoPath?.Trim() ?? string.Empty;
            VideoPathText.Text = string.IsNullOrWhiteSpace(path) ? "Chua chon file video" : path;
            MetaLineText.Text = $"FPS source: {_node.SourceFps:0.##} | Extract: {_node.ExtractFps:0.##}/s | HW: {_node.PreferredHwAccel}";
            AudioSummaryText.Text = $"Audio tracks: {_node.AudioTracks.Count} | Output: {(_node.OutputBase64 ? "base64" : "file")}";
        }

        private void RefreshVideoPreview()
        {
            var path = _node.VideoPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                PreviewMedia.Stop();
                PreviewMedia.Source = null;
                PreviewMedia.Visibility = Visibility.Collapsed;
                PreviewText.Visibility = Visibility.Visible;
                PreviewText.Text = "Chua chon video";
                return;
            }

            try
            {
                PreviewMedia.Source = Uri.TryCreate(path, UriKind.Absolute, out var uri)
                    ? uri
                    : new Uri(path, UriKind.RelativeOrAbsolute);
                PreviewMedia.Visibility = Visibility.Visible;
                PreviewText.Visibility = Visibility.Collapsed;
                PreviewMedia.Position = TimeSpan.Zero;
                PreviewMedia.Play();
            }
            catch
            {
                PreviewMedia.Stop();
                PreviewMedia.Source = null;
                PreviewMedia.Visibility = Visibility.Collapsed;
                PreviewText.Visibility = Visibility.Visible;
                PreviewText.Text = "Khong the preview video";
            }
        }

        private void EmitAutoFitSizeSuggestion()
        {
            try
            {
                var naturalW = PreviewMedia.NaturalVideoWidth;
                var naturalH = PreviewMedia.NaturalVideoHeight;
                if (naturalW <= 0 || naturalH <= 0) return;

                var aspect = naturalW / (double)naturalH;
                if (double.IsNaN(aspect) || double.IsInfinity(aspect) || aspect <= 0) return;

                var previewHeight = Math.Clamp(
                    naturalH,
                    MinPreviewHeight,
                    Math.Min(MaxPreviewHeight, MaxAutoFitNodeHeight - NonPreviewContentHeight));
                var previewWidth = previewHeight * aspect;

                var suggestedWidth = Math.Clamp(previewWidth + HorizontalPadding, MinAutoFitNodeWidth, MaxAutoFitNodeWidth);
                var suggestedHeight = Math.Clamp(previewHeight + NonPreviewContentHeight, MinAutoFitNodeHeight, MaxAutoFitNodeHeight);
                SuggestedNodeSizeReady?.Invoke(suggestedWidth, suggestedHeight);
            }
            catch
            {
                // best-effort
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
            PreviewMedia.Stop();
            _node.AudioTracks.CollectionChanged -= _audioTracksChangedHandler;
            _node.PropertyChanged -= _propertyChangedHandler;
            _subscriptionsAttached = false;
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
