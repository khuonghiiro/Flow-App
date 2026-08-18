// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Controls
{
    public class AudioWaveformData
    {
        public float[] MinPeaks { get; set; } = Array.Empty<float>();
        public float[] MaxPeaks { get; set; } = Array.Empty<float>();
        public double DurationSec { get; set; }
        public int PeaksPerSecond { get; set; } = 100; // 10ms per bucket

        public bool IsValid => DurationSec > 0 && MaxPeaks.Length > 0 && MinPeaks.Length == MaxPeaks.Length;
    }

    /// <summary>
    /// Visualizer sóng âm chuẩn Studio tông Đỏ theo VideoProcessingStyles:
    /// - Dải sóng âm tông Đỏ / Crimson hiện đại (Peak & RMS Core gradient).
    /// - Nhấn giữ chuột trái và kéo rê trên sóng âm để tạo vùng cắt mới (Drag-to-select range).
    /// - Kéo thả 2 cột Start / End với tay nắm trực quan.
    /// - Cuộn ngang bằng con lăn chuột, Ctrl + Wheel để Zoom vào đúng vị trí con trỏ chuột.
    /// </summary>
    public class AudioTrimWaveformVisualizer : FrameworkElement
    {
        private enum DragMode { None, StartHandle, EndHandle, RangeBody, CreatingRange }

        private DragMode _activeDrag = DragMode.None;
        private double _dragStartMouseX;
        private double _dragStartTrimStart;
        private double _dragStartTrimEnd;
        private double _dragSelectAnchorSec;
        private bool _hasMovedDuringDrag;

        // Visual brushes & pens - Tông màu Đỏ đồng bộ VideoProcessingStyles
        private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(17, 18, 28)); // #11121C ThemeTimelinePanelBrush
        private static readonly Brush RulerBgBrush = new SolidColorBrush(Color.FromRgb(22, 23, 34)); // #161722 ThemeCardBackgroundBrush
        private static readonly Pen BorderPen = new(new SolidColorBrush(Color.FromRgb(40, 41, 61)), 1); // #28293D ThemeCardBorderBrush
        private static readonly Pen GridLinePen = new(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), 0.6);
        private static readonly Pen ZeroLinePen = new(new SolidColorBrush(Color.FromArgb(90, 239, 68, 68)), 0.8); // Red-500 #EF4444

        private static readonly Brush DimmedOverlayBrush = new SolidColorBrush(Color.FromArgb(200, 9, 10, 16)); // ~78% dark #090A10
        private static readonly Brush SelectedRegionBrush = new SolidColorBrush(Color.FromArgb(42, 239, 68, 68)); // Red 16% overlay
        private static readonly Pen SelectedBorderPen = new(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 1.5); // Red-500 #EF4444

        private static readonly Pen RulerTickPen = new(new SolidColorBrush(Color.FromRgb(226, 232, 240)), 1.0); // Slate-200
        private static readonly Pen RulerSubTickPen = new(new SolidColorBrush(Color.FromRgb(100, 116, 139)), 0.6); // Slate-500
        private static readonly Typeface RulerTypeface = new(new FontFamily("Consolas, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        private static readonly Pen StartHandlePen = new(new SolidColorBrush(Color.FromRgb(248, 113, 113)), 2.5); // Coral Red-400 #F87171
        private static readonly Brush StartHandleBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Red-600 #DC2626
        private static readonly Pen EndHandlePen = new(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 2.5); // Red-500 #EF4444
        private static readonly Brush EndHandleBrush = new SolidColorBrush(Color.FromRgb(185, 28, 28)); // Red-700 #B91C1C
        private static readonly Brush HandleGripCenterBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        private static readonly Pen PlayheadPen = new(new SolidColorBrush(Color.FromRgb(245, 158, 11)), 2.0); // Amber #F59E0B
        private static readonly Brush PlayheadBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));

        private LinearGradientBrush? _waveBarGradient;
        private LinearGradientBrush? _waveCoreGradient;
        private LinearGradientBrush? _placeholderGradient;
        private ScrollViewer? _attachedScrollViewer;

        #region Dependency Properties

        public static readonly DependencyProperty TotalDurationSecProperty =
            DependencyProperty.Register(nameof(TotalDurationSec), typeof(double), typeof(AudioTrimWaveformVisualizer),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnTotalDurationChanged));

        public static readonly DependencyProperty TrimStartSecProperty =
            DependencyProperty.Register(nameof(TrimStartSec), typeof(double), typeof(AudioTrimWaveformVisualizer),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnTrimValuesChanged));

        public static readonly DependencyProperty TrimEndSecProperty =
            DependencyProperty.Register(nameof(TrimEndSec), typeof(double), typeof(AudioTrimWaveformVisualizer),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnTrimValuesChanged));

        public static readonly DependencyProperty CurrentPlayheadSecProperty =
            DependencyProperty.Register(nameof(CurrentPlayheadSec), typeof(double), typeof(AudioTrimWaveformVisualizer),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ZoomLevelProperty =
            DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(AudioTrimWaveformVisualizer),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnZoomChangedCallback));

        public static readonly DependencyProperty WaveformDataProperty =
            DependencyProperty.Register(nameof(WaveformData), typeof(AudioWaveformData), typeof(AudioTrimWaveformVisualizer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnWaveformDataChanged));

        public static readonly DependencyProperty IsRangeDurationLockedProperty =
            DependencyProperty.Register(nameof(IsRangeDurationLocked), typeof(bool), typeof(AudioTrimWaveformVisualizer),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnLockedDurationChangedCallback));

        public static readonly DependencyProperty LockedDurationSecProperty =
            DependencyProperty.Register(nameof(LockedDurationSec), typeof(double), typeof(AudioTrimWaveformVisualizer),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnLockedDurationChangedCallback));

        public double TotalDurationSec
        {
            get => (double)GetValue(TotalDurationSecProperty);
            set => SetValue(TotalDurationSecProperty, value);
        }

        public double TrimStartSec
        {
            get => (double)GetValue(TrimStartSecProperty);
            set => SetValue(TrimStartSecProperty, value);
        }

        public double TrimEndSec
        {
            get => (double)GetValue(TrimEndSecProperty);
            set => SetValue(TrimEndSecProperty, value);
        }

        public double CurrentPlayheadSec
        {
            get => (double)GetValue(CurrentPlayheadSecProperty);
            set => SetValue(CurrentPlayheadSecProperty, value);
        }

        public double ZoomLevel
        {
            get => (double)GetValue(ZoomLevelProperty);
            set => SetValue(ZoomLevelProperty, Math.Clamp(value, 1.0, 100.0));
        }

        public AudioWaveformData? WaveformData
        {
            get => (AudioWaveformData?)GetValue(WaveformDataProperty);
            set => SetValue(WaveformDataProperty, value);
        }

        public bool IsRangeDurationLocked
        {
            get => (bool)GetValue(IsRangeDurationLockedProperty);
            set => SetValue(IsRangeDurationLockedProperty, value);
        }

        public double LockedDurationSec
        {
            get => (double)GetValue(LockedDurationSecProperty);
            set => SetValue(LockedDurationSecProperty, value);
        }

        private static void OnLockedDurationChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AudioTrimWaveformVisualizer v && v.IsRangeDurationLocked && v.LockedDurationSec > 0)
            {
                var total = Math.Max(0.1, v.TotalDurationSec > 0 ? v.TotalDurationSec : (v.WaveformData?.DurationSec ?? 10.0));
                var lockDur = Math.Min(total, v.LockedDurationSec);
                var newStart = Math.Clamp(v.TrimStartSec, 0.0, Math.Max(0.0, total - lockDur));
                v.TrimStartSec = newStart;
                v.TrimEndSec = newStart + lockDur;
                v.TrimRangeChanged?.Invoke(v, EventArgs.Empty);
                v.InvalidateVisual();
            }
        }

        #endregion

        #region Events

        public event EventHandler? TrimRangeChanged;
        public event Action<double>? SeekRequested;
        public event Action<double>? ZoomChanged;

        #endregion

        public AudioTrimWaveformVisualizer()
        {
            ClipToBounds = true;
            Focusable = true;
            MinHeight = 100;
            Height = 135;
            MinWidth = 100;
            Width = 800;
            Cursor = Cursors.Arrow;

            InitGradients();

            Loaded += (_, _) =>
            {
                AutoFindScrollViewer();
                UpdateVisualizerWidth();
            };

            IsVisibleChanged += (_, e) =>
            {
                if ((bool)e.NewValue)
                {
                    AutoFindScrollViewer();
                    UpdateVisualizerWidth();
                    InvalidateVisual();
                }
            };

            SizeChanged += (_, _) => InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var w = Width > 0 && !double.IsNaN(Width) ? Width : 800.0;
            var h = Height > 0 && !double.IsNaN(Height) ? Height : 135.0;
            return new Size(w, h);
        }

        private void InitGradients()
        {
            // Tông Đỏ Ruby gradient chuẩn cho đỉnh sóng (Peak)
            _waveBarGradient = new LinearGradientBrush(
                Color.FromRgb(248, 113, 113), // Coral Red #F87171
                Color.FromRgb(185, 28, 28),   // Dark Ruby Red #B91C1C
                new Point(0, 0), new Point(0, 1));
            _waveBarGradient.Freeze();

            // Tông Đỏ sáng rực rỡ cho lõi năng lượng giọng nói (RMS Core)
            _waveCoreGradient = new LinearGradientBrush(
                Color.FromRgb(254, 202, 202), // Light Coral #FECACA
                Color.FromRgb(239, 68, 68),   // Red-500 #EF4444
                new Point(0, 0), new Point(0, 1));
            _waveCoreGradient.Freeze();

            _placeholderGradient = new LinearGradientBrush(
                Color.FromArgb(190, 239, 68, 68),
                Color.FromArgb(130, 185, 28, 28),
                new Point(0, 0), new Point(0, 1));
            _placeholderGradient.Freeze();
        }

        private static void OnTotalDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AudioTrimWaveformVisualizer vis) vis.UpdateVisualizerWidth();
        }

        private static void OnTrimValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AudioTrimWaveformVisualizer vis) vis.TrimRangeChanged?.Invoke(vis, EventArgs.Empty);
        }

        private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AudioTrimWaveformVisualizer vis) vis.UpdateVisualizerWidth();
        }

        private static void OnZoomChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AudioTrimWaveformVisualizer vis && e.NewValue is double z)
            {
                vis.UpdateVisualizerWidth();
                vis.ZoomChanged?.Invoke(z);
            }
        }

        public void AttachScrollViewer(ScrollViewer sv)
        {
            if (_attachedScrollViewer == sv) return;
            if (_attachedScrollViewer != null)
            {
                _attachedScrollViewer.PreviewMouseWheel -= OnScrollViewerMouseWheel;
                _attachedScrollViewer.SizeChanged -= OnScrollViewerSizeChanged;
                _attachedScrollViewer.ScrollChanged -= OnScrollViewerScrollChanged;
            }

            _attachedScrollViewer = sv;
            if (_attachedScrollViewer != null)
            {
                _attachedScrollViewer.PreviewMouseWheel += OnScrollViewerMouseWheel;
                _attachedScrollViewer.SizeChanged += OnScrollViewerSizeChanged;
                _attachedScrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
            }
            UpdateVisualizerWidth();
        }

        private void AutoFindScrollViewer()
        {
            if (_attachedScrollViewer != null) return;
            DependencyObject curr = this;
            while (curr != null)
            {
                if (curr is ScrollViewer sv)
                {
                    AttachScrollViewer(sv);
                    break;
                }
                curr = VisualTreeHelper.GetParent(curr);
            }
        }

        private void OnScrollViewerSizeChanged(object sender, SizeChangedEventArgs e) => UpdateVisualizerWidth();

        private void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e) => InvalidateVisual();

        public void UpdateVisualizerWidth()
        {
            var viewportW = _attachedScrollViewer != null && _attachedScrollViewer.ActualWidth > 50
                ? _attachedScrollViewer.ActualWidth - 4
                : (ActualWidth > 50 ? ActualWidth : 800.0);

            var zoom = Math.Max(1.0, ZoomLevel);
            var targetW = Math.Max(viewportW, viewportW * zoom);

            if (Math.Abs(Width - targetW) > 0.5)
            {
                Width = targetW;
            }
            InvalidateVisual();
        }

        #region Coordinate Transformations

        public double TimeToPixelX(double timeSec)
        {
            var total = Math.Max(0.1, TotalDurationSec > 0 ? TotalDurationSec : (WaveformData?.DurationSec ?? 10.0));
            var w = Math.Max(50.0, Width > 0 ? Width : ActualWidth);
            var frac = Math.Clamp(timeSec / total, 0.0, 1.0);
            return frac * w;
        }

        public double PixelXToTime(double pixelX)
        {
            var total = Math.Max(0.1, TotalDurationSec > 0 ? TotalDurationSec : (WaveformData?.DurationSec ?? 10.0));
            var w = Math.Max(50.0, Width > 0 ? Width : ActualWidth);
            var frac = Math.Clamp(pixelX / w, 0.0, 1.0);
            return frac * total;
        }

        public (double VisibleStartX, double VisibleEndX) GetVisiblePixelRange()
        {
            var w = Math.Max(50.0, Width > 0 ? Width : ActualWidth);
            if (_attachedScrollViewer == null || _attachedScrollViewer.ViewportWidth <= 0)
            {
                return (0, w);
            }

            var startX = Math.Max(0, _attachedScrollViewer.HorizontalOffset - 100);
            var endX = Math.Min(w, startX + _attachedScrollViewer.ViewportWidth + 200);
            return (startX, endX);
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var w = Math.Max(50.0, Width > 0 ? Width : ActualWidth);
            var h = ActualHeight > 0 ? ActualHeight : 135.0;

            var rulerH = 24.0;
            var waveH = h - rulerH;
            var (visStartX, visEndX) = GetVisiblePixelRange();

            // 1. Nền tổng thể
            dc.DrawRectangle(BgBrush, BorderPen, new Rect(0, 0, w, h));

            // 2. Thước đo thời gian (Ruler Bar)
            dc.DrawRectangle(RulerBgBrush, null, new Rect(0, 0, w, rulerH));
            dc.DrawLine(BorderPen, new Point(0, rulerH), new Point(w, rulerH));

            RenderTimeRuler(dc, w, rulerH, visStartX, visEndX);

            // 3. Sóng âm tông Đỏ (Studio Red Waveform)
            RenderWaveform(dc, w, rulerH, waveH, visStartX, visEndX);

            // 4. Lớp phủ vùng cắt (Selection & Dimmed Regions)
            RenderTrimSelection(dc, w, rulerH, waveH);

            // 5. Kim phát (Playhead Cursor)
            RenderPlayhead(dc, w, rulerH, waveH);
        }

        private void RenderTimeRuler(DrawingContext dc, double totalW, double rulerH, double visStartX, double visEndX)
        {
            var totalDur = Math.Max(0.1, TotalDurationSec > 0 ? TotalDurationSec : (WaveformData?.DurationSec ?? 10.0));
            var pixelsPerSec = totalW / totalDur;

            var (majorStep, minorStep, format) = CalculateRulerSteps(pixelsPerSec);

            var vStartSec = PixelXToTime(visStartX);
            var vEndSec = PixelXToTime(visEndX);

            var firstMinor = Math.Floor(vStartSec / minorStep) * minorStep;
            for (var t = firstMinor; t <= vEndSec + minorStep; t += minorStep)
            {
                if (t < 0 || t > totalDur + 0.001) continue;
                var x = TimeToPixelX(t);

                var isMajor = Math.Abs((t / majorStep) - Math.Round(t / majorStep)) < 0.001;
                var tickH = isMajor ? rulerH * 0.60 : rulerH * 0.32;
                var pen = isMajor ? RulerTickPen : RulerSubTickPen;

                dc.DrawLine(pen, new Point(x, rulerH - tickH), new Point(x, rulerH));

                if (isMajor)
                {
                    var ts = TimeSpan.FromSeconds(Math.Max(0, t));
                    var labelStr = ts.ToString(format, CultureInfo.InvariantCulture);
                    var ft = new FormattedText(
                        labelStr,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        RulerTypeface,
                        9.0,
                        RulerTickPen.Brush,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    dc.DrawText(ft, new Point(x + 3, 2));
                }
            }
        }

        private static (double majorStep, double minorStep, string format) CalculateRulerSteps(double pps)
        {
            if (pps < 10) return (30.0, 10.0, @"mm\:ss");
            if (pps < 30) return (10.0, 2.0, @"mm\:ss");
            if (pps < 80) return (5.0, 1.0, @"mm\:ss");
            if (pps < 250) return (1.0, 0.2, @"mm\:ss\.f");
            if (pps < 800) return (0.5, 0.1, @"ss\.ff\s");
            if (pps < 2500) return (0.1, 0.02, @"ss\.fff\s");
            return (0.05, 0.01, @"ss\.fff\s");
        }

        private void RenderWaveform(DrawingContext dc, double totalW, double rulerH, double waveH, double visStartX, double visEndX)
        {
            var centerY = rulerH + (waveH / 2.0);
            var maxAmpH = (waveH / 2.0) - 4.0;

            // Đường lưới Decibel & đường trung tâm
            dc.DrawLine(GridLinePen, new Point(visStartX, centerY - maxAmpH * 0.5), new Point(visEndX, centerY - maxAmpH * 0.5));
            dc.DrawLine(GridLinePen, new Point(visStartX, centerY + maxAmpH * 0.5), new Point(visEndX, centerY + maxAmpH * 0.5));
            dc.DrawLine(ZeroLinePen, new Point(0, centerY), new Point(totalW, centerY));

            var data = WaveformData;
            if (data == null || !data.IsValid)
            {
                RenderPlaceholderWave(dc, totalW, rulerH, waveH, centerY, maxAmpH, visStartX, visEndX);
                return;
            }

            const double barPitch = 3.2; // 3.2px per visual bar
            const double barW = 2.2;     // 2.2px bar width, 1.0px gap

            var startBar = (int)Math.Max(0, Math.Floor(visStartX / barPitch));
            var endBar = (int)Math.Min(Math.Ceiling(totalW / barPitch), Math.Ceiling(visEndX / barPitch) + 1);

            var pps = data.PeaksPerSecond > 0 ? data.PeaksPerSecond : 100;
            var maxSampleIdx = data.MaxPeaks.Length - 1;

            for (var b = startBar; b <= endBar; b++)
            {
                var x = b * barPitch;
                var tStart = PixelXToTime(x);
                var tEnd = PixelXToTime(x + barPitch);

                var sIdx1 = (int)Math.Clamp(tStart * pps, 0, maxSampleIdx);
                var sIdx2 = (int)Math.Clamp(tEnd * pps, sIdx1, maxSampleIdx);

                var maxVal = 0f;
                for (var s = sIdx1; s <= sIdx2; s++)
                {
                    var pMax = Math.Abs(data.MaxPeaks[s]);
                    var pMin = Math.Abs(data.MinPeaks[s]);
                    var p = Math.Max(pMax, pMin);
                    if (p > maxVal) maxVal = p;
                }

                // Dynamic Range Boost để sóng âm tiếng nói nổi bật sắc nét
                var peak = Math.Clamp((float)Math.Pow(maxVal, 0.75), 0f, 1f);
                if (peak < 0.035f) peak = 0.035f;

                var barHalfH = peak * maxAmpH;
                var rect = new Rect(x, centerY - barHalfH, barW, barHalfH * 2.0);
                dc.DrawRoundedRectangle(_waveBarGradient, null, rect, 1, 1);

                // Core RMS layer tông đỏ sáng
                var coreH = barHalfH * 0.52;
                if (coreH > 1.5)
                {
                    dc.DrawRoundedRectangle(_waveCoreGradient, null, new Rect(x + 0.3, centerY - coreH, barW - 0.6, coreH * 2.0), 0.5, 0.5);
                }
            }
        }

        private void RenderPlaceholderWave(DrawingContext dc, double totalW, double rulerH, double waveH, double centerY, double maxAmpH, double visStartX, double visEndX)
        {
            const double barPitch = 3.5;
            var startBar = (int)(visStartX / barPitch);
            var endBar = (int)(visEndX / barPitch) + 1;

            for (var i = startBar; i <= endBar; i++)
            {
                var x = i * barPitch;
                var sin = (Math.Sin(i * 0.18) * 0.5 + 0.5) * (Math.Cos(i * 0.06) * 0.4 + 0.6);
                var barH = Math.Max(4.0, sin * maxAmpH * 0.75);
                var rect = new Rect(x, centerY - barH, 2.5, barH * 2.0);
                dc.DrawRoundedRectangle(_placeholderGradient, null, rect, 1, 1);
            }
        }

        private void RenderTrimSelection(DrawingContext dc, double totalW, double rulerH, double waveH)
        {
            var totalDur = Math.Max(0.1, TotalDurationSec > 0 ? TotalDurationSec : (WaveformData?.DurationSec ?? 10.0));
            var startX = Math.Clamp(TimeToPixelX(TrimStartSec), 0, totalW);
            var endX = Math.Clamp(TimeToPixelX(TrimEndSec > 0 ? TrimEndSec : totalDur), 0, totalW);
            if (endX < startX) endX = startX;

            var topY = rulerH;
            var bottomY = rulerH + waveH;
            var centerY = rulerH + (waveH / 2.0);

            // 1. Dimmed Left
            if (startX > 0)
            {
                dc.DrawRectangle(DimmedOverlayBrush, null, new Rect(0, topY, startX, waveH));
            }

            // 2. Selected Region Highlight
            var selW = Math.Max(0, endX - startX);
            if (selW > 0)
            {
                dc.DrawRectangle(SelectedRegionBrush, SelectedBorderPen, new Rect(startX, topY, selW, waveH));
            }

            // 3. Dimmed Right
            if (endX < totalW)
            {
                dc.DrawRectangle(DimmedOverlayBrush, null, new Rect(endX, topY, totalW - endX, waveH));
            }

            // 4. Start Handle (Red-400 / Coral)
            dc.DrawLine(StartHandlePen, new Point(startX, 0), new Point(startX, bottomY));
            DrawHandleBadge(dc, startX, 0, $"[ {FormatBadgeTime(TrimStartSec)}", StartHandleBrush, isLeft: true);
            DrawHandleGrip(dc, startX, centerY, StartHandleBrush);

            // 5. End Handle (Red-600 / Dark Red)
            dc.DrawLine(EndHandlePen, new Point(endX, 0), new Point(endX, bottomY));
            DrawHandleBadge(dc, endX, 0, $"{FormatBadgeTime(TrimEndSec)} ]", EndHandleBrush, isLeft: false);
            DrawHandleGrip(dc, endX, centerY, EndHandleBrush);
        }

        private void DrawHandleGrip(DrawingContext dc, double x, double centerY, Brush handleBrush)
        {
            // Tay nắm hình viên thuốc ở giữa cột
            var gripW = 10.0;
            var gripH = 26.0;
            var rect = new Rect(x - (gripW / 2.0), centerY - (gripH / 2.0), gripW, gripH);
            dc.DrawRoundedRectangle(handleBrush, new Pen(Brushes.White, 1.2), rect, 4, 4);

            // 2 vạch cầm tay bên trong
            var notchPen = new Pen(HandleGripCenterBrush, 1.0);
            dc.DrawLine(notchPen, new Point(x - 2, centerY - 6), new Point(x - 2, centerY + 6));
            dc.DrawLine(notchPen, new Point(x + 2, centerY - 6), new Point(x + 2, centerY + 6));
        }

        private void DrawHandleBadge(DrawingContext dc, double x, double y, string text, Brush badgeBrush, bool isLeft)
        {
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                RulerTypeface,
                9.5,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            var totalW = Math.Max(50.0, Width > 0 ? Width : ActualWidth);
            var badgeW = ft.Width + 10;
            var badgeH = 17.0;
            var badgeX = isLeft ? Math.Max(0, x - badgeW + 2) : Math.Min(totalW - badgeW, x - 2);

            dc.DrawRoundedRectangle(badgeBrush, new Pen(Brushes.White, 0.8), new Rect(badgeX, y + 2, badgeW, badgeH), 4, 4);
            dc.DrawText(ft, new Point(badgeX + 5, y + 3));
        }

        private void RenderPlayhead(DrawingContext dc, double totalW, double rulerH, double waveH)
        {
            if (CurrentPlayheadSec < 0) return;
            var x = TimeToPixelX(CurrentPlayheadSec);
            if (x < 0 || x > totalW) return;

            dc.DrawLine(PlayheadPen, new Point(x, 0), new Point(x, rulerH + waveH));

            var triGeometry = new StreamGeometry();
            using (var ctx = triGeometry.Open())
            {
                ctx.BeginFigure(new Point(x - 5, 0), isFilled: true, isClosed: true);
                ctx.LineTo(new Point(x + 5, 0), isStroked: false, isSmoothJoin: false);
                ctx.LineTo(new Point(x, 8), isStroked: false, isSmoothJoin: false);
            }
            triGeometry.Freeze();
            dc.DrawGeometry(PlayheadBrush, null, triGeometry);
        }

        private static string FormatBadgeTime(double sec)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, sec));
            return sec < 60 ? $"{sec:0.00}s" : $"{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}";
        }

        #endregion

        #region Mouse Interaction, Panning & Zoom

        private void OnScrollViewerMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_attachedScrollViewer == null) return;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // Ctrl + Mouse Wheel = Zoom in / Zoom out centered at mouse position
                var mousePosInVisualizer = e.GetPosition(this);
                var mousePosInViewport = e.GetPosition(_attachedScrollViewer);

                var oldZoom = ZoomLevel;
                var zoomFactor = e.Delta > 0 ? 1.25 : 0.8;
                var newZoom = Math.Clamp(oldZoom * zoomFactor, 1.0, 100.0);

                if (Math.Abs(newZoom - oldZoom) > 0.01)
                {
                    ZoomLevel = newZoom;
                    UpdateVisualizerWidth();

                    // Center scroll offset at mouse point
                    var newOffset = (mousePosInVisualizer.X * (newZoom / oldZoom)) - mousePosInViewport.X;
                    _attachedScrollViewer.ScrollToHorizontalOffset(Math.Max(0, newOffset));
                }
                e.Handled = true;
            }
            else
            {
                // Normal Mouse Wheel = Smooth Horizontal Scroll
                var scrollDelta = e.Delta > 0 ? -120 : 120;
                _attachedScrollViewer.ScrollToHorizontalOffset(_attachedScrollViewer.HorizontalOffset + scrollDelta);
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);

            var startX = TimeToPixelX(TrimStartSec);
            var endX = TimeToPixelX(TrimEndSec);

            if (_activeDrag == DragMode.None)
            {
                if (IsRangeDurationLocked && LockedDurationSec > 0)
                {
                    if (pos.X >= startX && pos.X <= endX)
                        Cursor = Cursors.SizeAll;
                    else
                        Cursor = Cursors.Hand;
                }
                else
                {
                    if (Math.Abs(pos.X - startX) <= 8 || Math.Abs(pos.X - endX) <= 8)
                        Cursor = Cursors.SizeWE;
                    else
                        Cursor = Cursors.Cross;
                }
                return;
            }

            var curTime = PixelXToTime(pos.X);
            var total = Math.Max(0.1, TotalDurationSec > 0 ? TotalDurationSec : (WaveformData?.DurationSec ?? 10.0));

            if (IsRangeDurationLocked && LockedDurationSec > 0)
            {
                var lockDur = Math.Min(total, LockedDurationSec);
                var deltaSec = curTime - PixelXToTime(_dragStartMouseX);
                var newStart = Math.Clamp(_dragStartTrimStart + deltaSec, 0.0, Math.Max(0.0, total - lockDur));
                TrimStartSec = newStart;
                TrimEndSec = newStart + lockDur;
                return;
            }

            switch (_activeDrag)
            {
                case DragMode.StartHandle:
                    TrimStartSec = Math.Clamp(curTime, 0.0, Math.Max(0.0, TrimEndSec - 0.05));
                    break;

                case DragMode.EndHandle:
                    TrimEndSec = Math.Clamp(curTime, TrimStartSec + 0.05, total);
                    break;

                case DragMode.CreatingRange:
                    if (Math.Abs(pos.X - _dragStartMouseX) > 4)
                    {
                        _hasMovedDuringDrag = true;
                        var minSec = Math.Clamp(Math.Min(_dragSelectAnchorSec, curTime), 0.0, total);
                        var maxSec = Math.Clamp(Math.Max(_dragSelectAnchorSec, curTime), 0.0, total);

                        if (maxSec - minSec < 0.05) maxSec = Math.Min(total, minSec + 0.05);

                        TrimStartSec = minSec;
                        TrimEndSec = maxSec;
                    }
                    break;

                case DragMode.RangeBody:
                    var deltaSec = curTime - PixelXToTime(_dragStartMouseX);
                    var rangeLen = _dragStartTrimEnd - _dragStartTrimStart;
                    var newStart = Math.Clamp(_dragStartTrimStart + deltaSec, 0.0, Math.Max(0.0, total - rangeLen));
                    var newEnd = newStart + rangeLen;

                    TrimStartSec = newStart;
                    TrimEndSec = newEnd;
                    break;
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            CaptureMouse();

            var pos = e.GetPosition(this);
            var startX = TimeToPixelX(TrimStartSec);
            var endX = TimeToPixelX(TrimEndSec);
            var total = Math.Max(0.1, TotalDurationSec > 0 ? TotalDurationSec : (WaveformData?.DurationSec ?? 10.0));

            if (IsRangeDurationLocked && LockedDurationSec > 0)
            {
                var lockDur = Math.Min(total, LockedDurationSec);
                if (pos.X < startX || pos.X > endX)
                {
                    // Click outside range -> Jump the locked window to center at clicked position
                    var clickedTime = PixelXToTime(pos.X);
                    var newStart = Math.Clamp(clickedTime - (lockDur / 2.0), 0.0, Math.Max(0.0, total - lockDur));
                    TrimStartSec = newStart;
                    TrimEndSec = newStart + lockDur;
                    _dragStartTrimStart = newStart;
                    _dragStartTrimEnd = newStart + lockDur;
                    _dragStartMouseX = pos.X;
                    _activeDrag = DragMode.RangeBody;
                    _hasMovedDuringDrag = true;
                    return;
                }

                _dragStartMouseX = pos.X;
                _dragStartTrimStart = TrimStartSec;
                _dragStartTrimEnd = TrimEndSec;
                _activeDrag = DragMode.RangeBody;
                _hasMovedDuringDrag = false;
                return;
            }

            _dragStartMouseX = pos.X;
            _dragStartTrimStart = TrimStartSec;
            _dragStartTrimEnd = TrimEndSec;
            _hasMovedDuringDrag = false;

            if (Math.Abs(pos.X - startX) <= 8)
            {
                _activeDrag = DragMode.StartHandle;
            }
            else if (Math.Abs(pos.X - endX) <= 8)
            {
                _activeDrag = DragMode.EndHandle;
            }
            else
            {
                // Nhấn giữ chuột trái tại bất kỳ đâu để tạo khoảng cắt mới (hoặc click để seek)
                _activeDrag = DragMode.CreatingRange;
                _dragSelectAnchorSec = PixelXToTime(pos.X);
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (_activeDrag == DragMode.CreatingRange && !_hasMovedDuringDrag && (!IsRangeDurationLocked || LockedDurationSec <= 0))
            {
                // Click đơn thuần không kéo -> Nhảy Playhead (Seek)
                var clickedSec = PixelXToTime(_dragStartMouseX);
                CurrentPlayheadSec = clickedSec;
                SeekRequested?.Invoke(clickedSec);
            }

            _activeDrag = DragMode.None;
            _hasMovedDuringDrag = false;
            ReleaseMouseCapture();
        }

        #endregion
    }
}
