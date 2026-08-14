// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Controls;
using FlowMy.Converters;
using FlowMy.Effects;
using FlowMy.Helpers;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Utilities;
using FlowMy.Services.Workflow;
using FlowMy.Services.Workflow.NodeExecutors;
using FlowMy.Views.Overlays;
using Microsoft.Win32;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using DrawingBitmap = System.Drawing.Bitmap;
using WinForms = System.Windows.Forms;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl : UserControl
    {
        /// <summary>WPF preview font — export nhãn frame dùng cùng raster (Segoe UI Semibold → system UI).</summary>
        private static readonly FontFamily FrameLabelPreviewFontFamily = CreateFrameLabelPreviewFontFamily();

        private static FontFamily CreateFrameLabelPreviewFontFamily()
        {
            try { return new FontFamily("Segoe UI Semibold"); }
            catch { return SystemFonts.MessageFontFamily; }
        }

        private enum TimelineDragMode
        {
            None,
            Scrub,
            TrimStart,
            TrimEnd
        }

        private enum PreviewQualityMode
        {
            Auto,
            Low,
            Normal,
            High
        }

        private static readonly int[] PreviewQualityLevelHeights = { 144, 240, 480, 720, 1080, 1440, 2160 };

        private const double MinAutoFitNodeWidth = 540;
        private const double MinAutoFitNodeHeight = 340;
        private const double MaxAutoFitNodeWidth = 1280;
        private const double MaxAutoFitNodeHeight = 920;
        private const double MinPreviewHeight = 180;
        private const double MaxPreviewHeight = 620;
        private const double HorizontalPadding = 18;
        /// <summary>Half visual diameter of timeline scrub thumb (matches XAML ellipse).</summary>
        private const double ProgressThumbHalfWidth = 7;
        private const double NonPreviewContentHeight = 230;
        /// <summary>Bo góc node + khung preview — đồng bộ <c>NodeChromeCornerRadius</c> trong <see cref="VideoProcessingNodeControl"/>.</summary>
        private const double VideoNodeCornerRadius = 10;
        private const int DragSeekThrottleLowMs = 140;
        private const int DragSeekThrottleNormalMs = 90;
        private const int DragSeekThrottleHighMs = 35;

        private readonly VideoProcessingNode _node;
        private readonly IWorkflowEditorHost? _host;
        private readonly NotifyCollectionChangedEventHandler _audioTracksChangedHandler;
        private readonly PropertyChangedEventHandler _propertyChangedHandler;
        private readonly DispatcherTimer _timelineTimer;
        private readonly DispatcherTimer _beforeAfterFlickerTimer;

        private bool _subscriptionsAttached;
        private bool _isProgressDragging;
        private bool _isPlaying;
        private bool _isMuted;
        private bool _suppressControlSync;
        private double _frameResizeScale = 1.0;
        private bool _isLightTheme;
        private bool _portraitVideoLogLayout;
        private bool _isNodeZoomed;
        private int _prevZIndex;
        private double _prevNodeWidth;
        private double _prevNodeHeight;
        private double _prevNodeX;
        private double _prevNodeY;
        private double _pendingSeekRatio = -1;
        private DateTime _lastDragSeekAtUtc = DateTime.MinValue;
        private DateTime _lastSeekRequestAtUtc = DateTime.MinValue;
        private double _lastSeekTargetSeconds = -1;
        private double _lastSeekLatencyMs = -1;
        private bool _isSeekLatencyPending;
        private CancellationTokenSource? _sourceFpsProbeCts;
        private bool _isProbingFps;
        private DateTime _dragReleaseBoostUntilUtc = DateTime.MinValue;
        private TimelineDragMode _timelineDragMode = TimelineDragMode.None;
        private TimelineDragMode _trimReviewDragMode = TimelineDragMode.None;
        private bool _previewEffectTemporarilyDisabled;
        private VideoEqEffect? _videoEqEffect;
        private bool _trimUiInitialized;
        private double _trimUiStartX;
        private double _trimUiEndX;
        private double _trimUiPlayX;
        private double _lastVolume = 0.7;
        private int? _fixedResolutionHeight;
        private DateTime _lastRunStartedAtUtc = DateTime.UtcNow;
        private bool _showAfterPreview;
        private bool _suppressOverlayEditorSync;
        private bool _pendingOverlayApply;
        private string? _beforePreviewPath;
        private string? _afterPreviewPath;
        private bool _isFlickerMode;
        private bool _isSwitchingComparePreview;
        private bool _isSelectingVideoDialog;
        private double _selectedAspectW = 16;
        private double _selectedAspectH = 9;
        private bool _aspectAuto = true;
        private bool _isTrimReviewDragging;
        private bool _isFrameControlSync;
        private int _trimFramePreviewRequestId;

        public event Action<double, double>? SuggestedNodeSizeReady;
        public event Action<string>? LogLineReceived;

        public VideoProcessingNodeContentControl(VideoProcessingNode node, IWorkflowEditorHost? host = null)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _host = host;

            InitializeComponent();
            ApplyThemeBrushes(GetTextBrush(_node.ColorKey));
            InitializeIcon();
            _timelineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _timelineTimer.Tick += (_, _) => UpdatePlaybackUi();
            _beforeAfterFlickerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            _beforeAfterFlickerTimer.Tick += (_, _) =>
            {
                if (!_isFlickerMode) return;
                _showAfterPreview = !_showAfterPreview;
                var target = _showAfterPreview ? _afterPreviewPath : _beforePreviewPath;
                LoadPreviewFromPath(target, isAfterPath: _showAfterPreview);
            };

            _audioTracksChangedHandler = (_, _) => RunOnUiThread(RefreshInfoText);
            _propertyChangedHandler = (_, e) =>
                RunOnUiThread(() => OnNodePropertyChanged(e.PropertyName ?? string.Empty));

            InitializeInteractiveControls();

            Loaded += (_, _) =>
            {
                AttachSubscriptions();
                SyncControlValuesFromModel();
                RefreshInfoText();
                RefreshVideoPreview();
                UpdatePlaybackUi();
                ApplyLocalTheme();
                SyncUserControlRoundedClip();
                RefreshLargeNodeUiScale();
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    SyncUserControlRoundedClip();
                    RefreshLargeNodeUiScale();
                    UpdatePreviewAspectRatio();
                    UpdateVideoLogColumnLayout();
                }));
            };
            Unloaded += (_, _) => DetachSubscriptions();
        }

        private void ApplyThemeBrushes(Brush textBrush)
        {
            var primary = TryFindResource("ThemeTextPrimaryBrush") as Brush
                          ?? (_isLightTheme ? Brushes.Black : Brushes.White);
            var secondary = TryFindResource("ThemeTextSecondaryBrush") as Brush ?? textBrush;
            var onAccent = TryFindResource("ThemeOnAccentTextBrush") as Brush ?? Brushes.White;
            TitleText.Foreground = primary;
            IconView.Fill = primary;
            VideoPathText.Foreground = secondary;
            UpdateHwBadgeUi();
        }

        private void InitializeIcon()
        {
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(string.Empty, typeof(Uri), "circle-video sharp-light",
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            if (iconUri != null) IconView.Source = iconUri;
            IconView.Fill = TryFindResource("ThemeTextPrimaryBrush") as Brush
                            ?? (_isLightTheme ? Brushes.Black : Brushes.White);
        }

        private void TabNavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SwitchTab();
        }

        private void SwitchTab()
        {
            var allTabs = new FrameworkElement[]
            {
                GeneralTabContent, GradingTabContent, FiltersTabContent,
                AudioTabContent, TrimConcatTabContent, OutputsTabContent, SettingsTabContent
            };
            foreach (var t in allTabs) if (t != null) t.Visibility = Visibility.Collapsed;

            var idx = TabNavList.SelectedIndex;
            FrameworkElement targetTab = (FrameworkElement)(idx switch
            {
                0 => GeneralTabContent,
                1 => GradingTabContent,
                2 => FiltersTabContent,
                3 => AudioTabContent,
                4 => TrimConcatTabContent,
                5 => OutputsTabContent,
                6 => SettingsTabContent,
                _ => GeneralTabContent
            });
            targetTab.Visibility = Visibility.Visible;
            targetTab.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));

            UpdateBottomBarGroupHighlight(Math.Max(0, idx));
        }

        private void UpdateBottomBarGroupHighlight(int tabIndex)
        {
        }

        private void UpdateActionButtonLabelVisibility()
        {
        }

        private async void RunProcessingFlow(bool singleNodeOnly = false)
        {
            if (_host == null) return;
            try
            {
                SwitchToLogView();
                SyncRuntimeConfigFromUi();
                _lastRunStartedAtUtc = DateTime.UtcNow;
                ProgressStatusText.Text = "Running...";
                var vm = _host.ViewModel;
                if (vm == null) return;

                if (singleNodeOnly)
                {
                    await vm.RunSingleNodeAsync(_node);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(_node.VideoSourceNodeId))
                    {
                        var sourceNode = vm.Nodes?.FirstOrDefault(n =>
                            string.Equals(n.Id, _node.VideoSourceNodeId, StringComparison.OrdinalIgnoreCase));
                        if (sourceNode != null)
                            await vm.RunSingleNodeAsync(sourceNode);
                    }
                    await vm.RunWorkflowFromNodeAsync(_node);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Run error: {ex.Message}");
            }
        }

        private void SyncRuntimeConfigFromUi()
        {
            _node.AudioOutputFolderPath = (AudioOutputFolderText.Text ?? string.Empty).Trim();

            if (_node.UseDialogVideoConfig)
            {
                // When using dialog config, let executor resolve output paths from dialog fields.
                // Clear local override to avoid treating a folder path as an exact output file.
                _node.OutputPathOverride = string.Empty;
                return;
            }

            _node.FrameOutputFolderPath = (FrameOutputFolderText.Text ?? string.Empty).Trim();
            var outputVideoPath = (OutputPathText.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputVideoPath))
                outputVideoPath = (DefaultOutputVideoPathText.Text ?? string.Empty).Trim();
            _node.OutputPathOverride = outputVideoPath;
            _node.DefaultOutputVideoPath = (DefaultOutputVideoPathText.Text ?? string.Empty).Trim();
        }

        private void OnNodePropertyChanged(string propertyName)
        {
            if (propertyName is nameof(VideoProcessingNode.Width) or nameof(VideoProcessingNode.Height))
            {
                RefreshLargeNodeUiScale();
            }
            if (propertyName == nameof(VideoProcessingNode.VideoPath))
            {
                if (_isSwitchingComparePreview)
                {
                    _isSwitchingComparePreview = false;
                }
                else
                {
                    // User selected/changed video manually -> disable compare mode
                    // to avoid flicker timer overriding the new source path.
                    StopComparePreviewMode();
                    _beforePreviewPath = _node.VideoPath;
                }
                RefreshVideoPreview();
                _isProbingFps = true;
                _ = ProbeSourceFpsAndRefreshUiAsync();
            }
            if (propertyName == nameof(VideoProcessingNode.FrameOutputFolderPath))
            {
                _suppressControlSync = true;
                FrameOutputFolderText.Text = _node.FrameOutputFolderPath ?? string.Empty;
                _suppressControlSync = false;
                RefreshOutputsSummaryUi();
            }
            if (propertyName == nameof(VideoProcessingNode.DefaultOutputVideoPath))
            {
                _suppressControlSync = true;
                DefaultOutputVideoPathText.Text = _node.DefaultOutputVideoPath ?? string.Empty;
                if (!_node.UseDialogVideoConfig)
                {
                    OutputPathText.Text = _node.DefaultOutputVideoPath ?? string.Empty;
                }
                _suppressControlSync = false;
                RefreshOutputsSummaryUi();
            }
            if (propertyName == nameof(VideoProcessingNode.OutputBase64))
            {
                RefreshOutputsSummaryUi();
            }
            if (propertyName is nameof(VideoProcessingNode.ExtractFramesEnabled)
                or nameof(VideoProcessingNode.ExportVideoEnabled)
                or nameof(VideoProcessingNode.ExtractAudioEnabled))
            {
                RefreshOutputsSummaryUi();
            }
            if (propertyName == nameof(VideoProcessingNode.AudioOutputFolderPath))
            {
                _suppressControlSync = true;
                AudioOutputFolderText.Text = _node.AudioOutputFolderPath ?? string.Empty;
                _suppressControlSync = false;
                RefreshOutputsSummaryUi();
            }
            if (propertyName == nameof(VideoProcessingNode.SourceFps)) UpdateFrameExtractionPreview();
            if (propertyName == nameof(VideoProcessingNode.PreferredHwAccel)) UpdateHwBadgeUi();
            if (propertyName == nameof(VideoProcessingNode.UseDialogVideoConfig))
            {
                _suppressControlSync = true;
                UseDialogVideoConfigCheckBox.IsChecked = _node.UseDialogVideoConfig;
                _suppressControlSync = false;
                ApplyConfigSourceMode();
            }
            if (propertyName is nameof(VideoProcessingNode.WatermarkEnabled) or nameof(VideoProcessingNode.WatermarkImagePath)
                or nameof(VideoProcessingNode.WatermarkPosition) or nameof(VideoProcessingNode.WatermarkOpacity)
                or nameof(VideoProcessingNode.WatermarkWidthFraction) or nameof(VideoProcessingNode.WatermarkInsetFraction))
                UpdateWatermarkPreviewUi();

            if (propertyName is nameof(VideoProcessingNode.GridCollageEnabled) or nameof(VideoProcessingNode.GridCollageWidth)
                or nameof(VideoProcessingNode.GridCollageHeight) or nameof(VideoProcessingNode.GridCollageFrameCount)
                or nameof(VideoProcessingNode.GridCollageBackgroundColor) or nameof(VideoProcessingNode.GridCollageColorKey)
                or nameof(VideoProcessingNode.GridCollagePadding) or nameof(VideoProcessingNode.GridCollageMargin)
                or nameof(VideoProcessingNode.GridCollageAspectMode))
            {
                SyncGridCollageControlsFromModel();
            }

            RefreshInfoText();
        }

        private void AttachSubscriptions()
        {
            if (_subscriptionsAttached) return;
            _node.AudioTracks.CollectionChanged += _audioTracksChangedHandler;
            _node.PropertyChanged += _propertyChangedHandler;
            AudioTracksList.ItemsSource = _node.AudioTracks;
            OverlayCanvasControl.ItemsSource = _node.Overlays;
            UpdateOverlayCanvasBounds();
            OverlayLayerList.ItemsSource = _node.Overlays;
            VideoProcessingNodeExecutor.ProgressChanged += HandleExecutorProgress;
            VideoProcessingNodeExecutor.LogLine += HandleExecutorLog;
            _subscriptionsAttached = true;
        }

        private void DetachSubscriptions()
        {
            if (!_subscriptionsAttached) return;
            _node.AudioTracks.CollectionChanged -= _audioTracksChangedHandler;
            _node.PropertyChanged -= _propertyChangedHandler;
            OverlayCanvasControl.ItemsSource = null;
            OverlayLayerList.ItemsSource = null;
            VideoProcessingNodeExecutor.ProgressChanged -= HandleExecutorProgress;
            VideoProcessingNodeExecutor.LogLine -= HandleExecutorLog;
            _timelineTimer.Stop();
            _beforeAfterFlickerTimer.Stop();
            PreviewMedia.Stop();
            _subscriptionsAttached = false;
        }

        private void HandleExecutorProgress(VideoProcessingNode node, double percent, string status)
        {
            if (!ReferenceEquals(node, _node)) return;
            UpdateProgress(percent, status);
            if (_pendingOverlayApply && percent >= 99)
            {
                Dispatcher.BeginInvoke(new Action(OnOverlayApplyCompleted));
            }
        }

        private void HandleExecutorLog(VideoProcessingNode node, string line)
        {
            if (!ReferenceEquals(node, _node)) return;
            AppendLog(line);
        }

        private void AppendLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var brush = GetLogLineBrush(line, out var isBold);
                var p = new Paragraph(new Run(line))
                {
                    Foreground = brush,
                    Margin = new Thickness(0, 1, 0, 1),
                    FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal
                };

                if (LogRichTextBox.Document == null)
                    LogRichTextBox.Document = new FlowDocument();

                LogRichTextBox.Document.Blocks.Add(p);
                LogScrollViewer.ScrollToEnd();
                LogLineReceived?.Invoke(line);
            }));
        }

        private Brush GetLogLineBrush(string line, out bool isBold)
        {
            isBold = false;
            var lower = line.ToLowerInvariant();

            // 1. Success / Saved Video / Completed (Vibrant Green)
            if (line.Contains("✓") || line.Contains("✅") || lower.Contains("[success]") || lower.Contains("[lưu video]") ||
                lower.Contains("thành công") || lower.Contains("saved") || lower.Contains("export complete") || lower.Contains("video saved"))
            {
                isBold = true;
                return new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)); // Emerald Green #4ADE80
            }

            // 2. Error / Failed / Exception (Vibrant Red)
            if (line.Contains("❌") || lower.Contains("[err]") || lower.Contains("[error]") || lower.Contains("failed") ||
                lower.Contains("exception") || lower.Contains("lỗi"))
            {
                isBold = true;
                return new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)); // Crimson Red #F87171
            }

            // 3. Warning (Warm Amber/Yellow)
            if (line.Contains("⚠") || lower.Contains("[warn]") || lower.Contains("[warning]") || lower.Contains("cảnh báo"))
            {
                return new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)); // Amber #FBBF24
            }

            // 4. Info / Process / Action (Cyan / Blue)
            if (line.Contains("▶") || line.Contains("🎬") || line.Contains("🎞") || line.Contains("✂") || line.Contains("💾") ||
                lower.Contains("[info]") || lower.Contains("[dbg]") || lower.Contains("running..."))
            {
                return new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)); // Sky Blue #38BDF8
            }

            // Default primary text color
            return _isLightTheme ? new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)) : new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
        }

        private void RunOnUiThread(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                action();
                return;
            }

            _ = Dispatcher.BeginInvoke(action);
        }

        private void UpdateProgress(double percent, string status)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ProgressPercentText.Text = $"{percent:0}%";
                ProgressStatusText.Text = status;
                var parentWidth = ((FrameworkElement)ProgressFill.Parent).ActualWidth;
                ProgressFill.Width = Math.Max(0, parentWidth * percent / 100);
                var elapsed = DateTime.UtcNow - _lastRunStartedAtUtc;
                ElapsedTimeText.Text = $"Elapsed: {elapsed:mm\\:ss}";
                EstimatedTimeText.Text = percent > 0 ? $"ETA: {TimeSpan.FromSeconds(elapsed.TotalSeconds * (100 - percent) / percent):mm\\:ss}" : "ETA: --";
            }));
        }

        public void SwitchToLogView()
        {
            RunOnUiThread(() =>
            {
                if (PortraitVideoLogTabControl != null && PortraitVideoLogTabControl.Visibility == Visibility.Visible)
                {
                    PortraitVideoLogTabControl.SelectedIndex = 1;
                }
                if (LogScrollViewer != null)
                {
                    LogScrollViewer.ScrollToEnd();
                }
            });
        }
    }
}
