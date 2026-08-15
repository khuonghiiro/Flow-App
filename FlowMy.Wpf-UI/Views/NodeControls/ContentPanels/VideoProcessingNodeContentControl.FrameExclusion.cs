// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Controls;
using FlowMy.Models.Nodes;
using FlowMy.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl
    {
        /// <summary>Kích thước mỗi thumbnail (px).</summary>
        private const int FrameThumbSize = 80;
        /// <summary>Max frame thumbnails per lazy-load window.</summary>
        private const int MaxFrameStripWindow = 60;

        private CancellationTokenSource? _frameStripCts;
        private bool _frameStripVisible;
        private int _frameStripCenterSecond;
        private double _frameStripLastFps;
        /// <summary>Cache hình ảnh thumbnail cho các frame đã excluded (dùng khi FPS thay đổi).</summary>
        private readonly Dictionary<double, BitmapSource> _excludedThumbCache = new();

        // ─── Toggle & Wire Events ─────────────────────────────────────────

        /// <summary>
        /// Gắn sự kiện cho FrameExcludeToggle, ClearExcludedFramesButton,
        /// RefreshFrameStripButton và FrameStripTimeSlider.
        /// </summary>
        private void WireFrameExclusionEvents()
        {
            if (FindName("FrameExclusionToggle") is System.Windows.Controls.Primitives.ToggleButton toggle)
            {
                toggle.Checked += (_, _) =>
                {
                    _frameStripVisible = true;
                    ShowFrameStripPanel();
                };
                toggle.Unchecked += (_, _) =>
                {
                    _frameStripVisible = false;
                    HideFrameStripPanel();
                };

                // Nút quick-access trên timeline toolbar → toggle FrameExclusionToggle
                if (FindName("FrameExcludeToggle") is Button quickToggle)
                {
                    quickToggle.Click += (_, _) =>
                    {
                        toggle.IsChecked = !(toggle.IsChecked ?? false);
                    };
                }
            }

            // Chọn tất cả frame đang hiển thị
            if (FindName("SelectAllVisibleFramesButton") is Button selectAllBtn)
            {
                selectAllBtn.Click += (_, _) =>
                {
                    SelectAllVisibleFrames();
                    RefreshFrameStripExclusionVisuals();
                    UpdateExcludedCountText();
                };
            }

            // Bỏ chọn tất cả frame đang hiển thị (chỉ visible, không phải toàn bộ)
            if (FindName("ClearExcludedFramesButton") is Button clearBtn)
            {
                clearBtn.Click += (_, _) =>
                {
                    ClearVisibleExcludedFrames();
                    RefreshFrameStripExclusionVisuals();
                    UpdateExcludedCountText();
                };
            }

            // Bỏ chọn tất cả frame ở mục Pinned
            if (FindName("ClearPinnedExcludedButton") is Button clearPinnedBtn)
            {
                clearPinnedBtn.Click += (_, _) =>
                {
                    ClearPinnedExcludedFrames();
                    UpdateExcludedCountText();
                };
            }

            if (FindName("RefreshFrameStripButton") is Button refreshBtn)
            {
                refreshBtn.Click += async (_, _) =>
                {
                    await LoadFrameStripWindowAsync(_frameStripCenterSecond, forceReload: true);
                };
            }

            WireTimeSliderEvents();
        }

        /// <summary>Wire slider ValueChanged — kéo đến giây nào thì load frame quanh đó.</summary>
        private void WireTimeSliderEvents()
        {
            if (FindName("FrameStripTimeSlider") is not Slider slider) return;

            slider.ValueChanged += async (_, e) =>
            {
                var sec = (int)Math.Floor(e.NewValue);
                if (sec == _frameStripCenterSecond && _frameStripLastFps == _node.ExtractFps) return;
                _frameStripCenterSecond = sec;
                UpdateTimeSliderLabels(sec);
                await LoadFrameStripWindowAsync(sec);
            };
        }

        // ─── Show / Hide ──────────────────────────────────────────────────

        private void ShowFrameStripPanel()
        {
            if (FindName("FrameExclusionContentGrid") is Grid contentGrid)
                contentGrid.Visibility = Visibility.Visible;

            InitTimeSliderRange();
            _ = LoadFrameStripWindowAsync(_frameStripCenterSecond, forceReload: true);
        }

        private void HideFrameStripPanel()
        {
            if (FindName("FrameExclusionContentGrid") is Grid contentGrid)
                contentGrid.Visibility = Visibility.Collapsed;
        }

        /// <summary>Thiết lập range cho slider theo duration video.</summary>
        private void InitTimeSliderRange()
        {
            if (FindName("FrameStripTimeSlider") is not Slider slider) return;
            var duration = GetNaturalDurationSeconds();
            var maxSec = Math.Max(0, (int)Math.Floor(duration));
            slider.Maximum = maxSec;
            slider.TickFrequency = 1;
            slider.IsSnapToTickEnabled = true;
            slider.Value = Math.Min(_frameStripCenterSecond, maxSec);

            if (FindName("FrameStripTimeEndLabel") is TextBlock endLabel)
                endLabel.Text = FormatTime(TimeSpan.FromSeconds(duration));

            UpdateTimeSliderLabels((int)slider.Value);
        }

        private void UpdateTimeSliderLabels(int centerSecond)
        {
            if (FindName("FrameStripTimeLabel") is TextBlock label)
                label.Text = FormatTime(TimeSpan.FromSeconds(centerSecond));
        }

        // ─── Lazy-Load Window ─────────────────────────────────────────────

        /// <summary>
        /// Load frame thumbnails chỉ quanh 1 đoạn ~2 giây (prev half + current + next half).
        /// </summary>
        private async Task LoadFrameStripWindowAsync(int centerSecond, bool forceReload = false)
        {
            CancelPendingFrameStrip();
            _frameStripCts = new CancellationTokenSource();
            var ct = _frameStripCts.Token;

            if (FindName("FrameStripWrapPanel") is not WrapPanel wrapPanel) return;
            wrapPanel.Children.Clear();

            var videoPath = _node.VideoPath;
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                AddPlaceholderText(wrapPanel, "Chưa có video để hiển thị frame.");
                return;
            }

            var fps = Math.Max(0.5, _node.ExtractFps);
            var duration = GetNaturalDurationSeconds();
            if (duration <= 0) duration = _node.TrimEndSec > 0 ? _node.TrimEndSec : 60;

            _frameStripLastFps = fps;

            // Tính window: giây trước + giây hiện tại + giây sau (3 giây)
            var windowStart = Math.Max(0, centerSecond - 1);
            var windowEnd = Math.Min(duration, centerSecond + 2);
            var windowDuration = windowEnd - windowStart;

            if (windowDuration <= 0)
            {
                AddPlaceholderText(wrapPanel, "Không có frame trong đoạn này.");
                return;
            }

            AddPlaceholderText(wrapPanel, "Đang tải frame...");

            try
            {
                var thumbs = await Task.Run(() =>
                    ExtractFrameWindowThumbnails(videoPath, windowStart, windowDuration, fps, ct), ct)
                    .ConfigureAwait(true);

                if (ct.IsCancellationRequested) return;

                wrapPanel.Children.Clear();
                if (thumbs.Count == 0)
                {
                    AddPlaceholderText(wrapPanel, "Không tạo được thumbnail.");
                    return;
                }

                BuildFrameGroupUi(wrapPanel, thumbs, centerSecond, windowStart, windowEnd);
                RefreshFrameStripExclusionVisuals();
                UpdateExcludedCountText();
                RenderPinnedExcludedFrames(windowStart, windowEnd, fps, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    wrapPanel.Children.Clear();
                    AddPlaceholderText(wrapPanel, $"Lỗi: {ex.Message}");
                }
            }
        }

        /// <summary>Khi ExtractFps slider thay đổi mà panel đang mở → reload.</summary>
        internal void OnExtractFpsChangedWhileFrameStripVisible()
        {
            if (!_frameStripVisible) return;
            _ = LoadFrameStripWindowAsync(_frameStripCenterSecond, forceReload: true);
        }

        private void RefreshFrameStripForCurrentWindow()
        {
            if (_frameStripVisible)
                _ = LoadFrameStripWindowAsync(_frameStripCenterSecond, forceReload: true);
        }

        // ─── FFmpeg Extraction (windowed) ─────────────────────────────────

        /// <summary>
        /// Tách frame thumbnails trong một đoạn [startSec, startSec+windowDuration] theo fps.
        /// </summary>
        private List<(BitmapSource image, double timestamp)> ExtractFrameWindowThumbnails(
            string videoPath, double startSec, double windowDuration, double fps,
            CancellationToken ct)
        {
            var results = new List<(BitmapSource, double)>();
            if (ct.IsCancellationRequested) return results;

            var ffmpegExe = FfmpegPathPreferencesStore.ResolveBinaryPath("ffmpeg");
            if (string.IsNullOrWhiteSpace(ffmpegExe) || !File.Exists(ffmpegExe))
                return results;

            // Giới hạn số frame tối đa
            var expectedFrames = (int)Math.Ceiling(windowDuration * fps);
            if (expectedFrames > MaxFrameStripWindow)
                fps = MaxFrameStripWindow / windowDuration;

            var thumbDir = Path.Combine(Path.GetTempPath(), "FlowMy_FrameStrip",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(thumbDir);

            var outPattern = Path.Combine(thumbDir, "thumb_%04d.jpg");
            var fpsStr = fps.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            var ssStr = startSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var tStr = windowDuration.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

            var args = $"-hide_banner -loglevel error " +
                       $"-ss {ssStr} -t {tStr} -i \"{videoPath}\" -an -sn " +
                       $"-vf \"fps={fpsStr},scale=w={FrameThumbSize}:h={FrameThumbSize}:force_original_aspect_ratio=decrease\" " +
                       $"-threads 2 -q:v 6 -y \"{outPattern}\"";

            RunFfmpegProcess(ffmpegExe, args, ct);
            if (ct.IsCancellationRequested) return results;

            var interval = 1.0 / fps;
            var files = Directory.GetFiles(thumbDir, "thumb_*.jpg")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var windowEnd = startSec + windowDuration;
            for (int i = 0; i < files.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                var ts = startSec + i * interval;
                // Bỏ qua các frame chạm hoặc vượt mốc windowEnd (tránh lấn sang giây của window tiếp theo)
                if (ts >= windowEnd - 0.0001) break;

                try
                {
                    var bmp = LoadBitmapImageFromFile(files[i]);
                    if (bmp != null) results.Add((bmp, ts));
                }
                catch { }
            }

            return results;
        }

        /// <summary>Tách 1 frame đơn lẻ tại timestamp cụ thể (cho pinned excluded).</summary>
        private BitmapSource? ExtractSingleFrameThumbnail(
            string videoPath, double timestampSec, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return null;
            var ffmpegExe = FfmpegPathPreferencesStore.ResolveBinaryPath("ffmpeg");
            if (string.IsNullOrWhiteSpace(ffmpegExe) || !File.Exists(ffmpegExe))
                return null;

            var thumbDir = Path.Combine(Path.GetTempPath(), "FlowMy_FrameStrip",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(thumbDir);

            var outFile = Path.Combine(thumbDir, "pinned.jpg");
            var ssStr = timestampSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var args = $"-hide_banner -loglevel error -ss {ssStr} " +
                       $"-i \"{videoPath}\" -an -sn -vframes 1 " +
                       $"-vf \"scale=w={FrameThumbSize}:h={FrameThumbSize}:force_original_aspect_ratio=decrease\" " +
                       $"-threads 2 -q:v 6 -y \"{outFile}\"";

            RunFfmpegProcess(ffmpegExe, args, ct);
            if (ct.IsCancellationRequested || !File.Exists(outFile)) return null;

            try { return LoadBitmapImageFromFile(outFile); }
            catch { return null; }
        }

        private static void RunFfmpegProcess(string exe, string args, CancellationToken ct)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            if (proc == null) return;
            using var reg = ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(); } catch { }
            });
            proc.WaitForExit(15000);
        }

        // ─── UI Building ──────────────────────────────────────────────────

        /// <summary>Nhóm frame theo giây và hiển thị với header thời gian.</summary>
        private void BuildFrameGroupUi(WrapPanel wrapPanel,
            List<(BitmapSource image, double timestamp)> thumbs, int centerSecond,
            double windowStart, double windowEnd)
        {
            var contentPanel = FindName("FrameStripContentPanel") as StackPanel;
            if (contentPanel == null)
            {
                // Fallback: dùng trực tiếp WrapPanel
                foreach (var item in thumbs)
                    wrapPanel.Children.Add(CreateFrameThumbnailControl(item.image, item.timestamp));
                return;
            }

            contentPanel.Children.Clear();

            var fps = Math.Max(0.5, _node.ExtractFps);

            // Group theo floor(timestamp) = giây, chỉ lấy các giây thuộc window [windowStart, windowEnd)
            var groups = thumbs.Where(t => t.timestamp >= windowStart && t.timestamp < windowEnd - 0.0001)
                               .GroupBy(t => (int)Math.Floor(t.timestamp))
                               .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                var sec = group.Key;
                var header = CreateTimeGroupHeader(sec, centerSecond);
                contentPanel.Children.Add(header);

                var groupWrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                foreach (var item in group)
                {
                    // Tính frame index dựa trên timestamp * fps
                    var frameIdx = (int)Math.Round(item.timestamp * fps);
                    var thumb = CreateFrameThumbnailControl(item.image, item.timestamp, frameIdx);
                    groupWrap.Children.Add(thumb);

                    if (_node.IsFrameExcluded(item.timestamp))
                    {
                        RemoveCachedExcludedThumb(item.timestamp, fps);
                        _excludedThumbCache[item.timestamp] = item.image;
                    }
                }
                contentPanel.Children.Add(groupWrap);
            }
        }

        /// <summary>Tạo header nhóm thời gian cho mỗi giây.</summary>
        private Border CreateTimeGroupHeader(int second, int centerSecond)
        {
            var isCurrent = second == centerSecond;
            var timeText = FormatTime(TimeSpan.FromSeconds(second));
            var rangeText = $"⏱ {timeText} — {FormatTime(TimeSpan.FromSeconds(second + 1))}";

            var orangeAccent = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x43)); // #FF9F43
            orangeAccent.Freeze();
            var orangeDim = new SolidColorBrush(Color.FromRgb(0xCC, 0x85, 0x3A)); // dimmer orange
            orangeDim.Freeze();

            var label = new TextBlock
            {
                Text = rangeText,
                FontSize = 9,
                FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                FontFamily = new FontFamily("Consolas"),
                Foreground = isCurrent ? orangeAccent : orangeDim,
                Margin = new Thickness(2, 2, 0, 1)
            };

            return new Border
            {
                Child = label,
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 2, 0, 0),
                CornerRadius = new CornerRadius(3),
                Background = isCurrent
                    ? new SolidColorBrush(Color.FromArgb(30, 255, 159, 67))
                    : Brushes.Transparent
            };
        }

        /// <summary>
        /// Tạo 1 thumbnail control với checkbox overlay cho mỗi frame.
        /// </summary>
        private Border CreateFrameThumbnailControl(BitmapSource bitmapSource, double timestamp,
            int frameIndex = -1)
        {
            // Tự động tính kích thước theo tỉ lệ thực tế (ngang / dọc / vuông)
            double targetWidth = FrameThumbSize;
            double targetHeight = FrameThumbSize * 0.5625; // fallback 16:9

            if (bitmapSource.PixelWidth > 0 && bitmapSource.PixelHeight > 0)
            {
                var aspect = (double)bitmapSource.PixelWidth / bitmapSource.PixelHeight;
                if (aspect >= 1.0)
                {
                    // Video ngang hoặc vuông
                    targetWidth = FrameThumbSize;
                    targetHeight = Math.Clamp(FrameThumbSize / aspect, 28, FrameThumbSize);
                }
                else
                {
                    // Video dọc (portrait)
                    targetHeight = FrameThumbSize;
                    targetWidth = Math.Clamp(FrameThumbSize * aspect, 36, FrameThumbSize);
                }
            }

            var image = new Image
            {
                Width = targetWidth,
                Height = targetHeight,
                Stretch = Stretch.Uniform,
                Source = bitmapSource
            };

            var checkBox = new CheckBox
            {
                IsChecked = _node.IsFrameExcluded(timestamp),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(2, 2, 0, 0),
                ToolTip = "Tích để loại bỏ frame này"
            };

            // Frame number label (top-right)
            var frameNumLabel = new TextBlock
            {
                Text = frameIndex >= 0 ? $"#{frameIndex}" : "",
                FontSize = 7.5,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 1, 0)
            };
            var frameNumBg = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 0.5, 2, 0.5),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 1, 0),
                Child = frameNumLabel,
                Visibility = frameIndex >= 0 ? Visibility.Visible : Visibility.Collapsed
            };

            // Timestamp label (bottom-center) — millisecond precision
            var timeLabel = new TextBlock
            {
                Text = FormatTimeMs(TimeSpan.FromSeconds(timestamp)),
                FontSize = 7,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 0)
            };

            var timeBg = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 0.5, 2, 0.5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 1),
                Child = timeLabel
            };

            // Overlay đỏ mờ khi excluded
            var excludeOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(100, 220, 40, 40)),
                Visibility = _node.IsFrameExcluded(timestamp) ? Visibility.Visible : Visibility.Collapsed,
                IsHitTestVisible = false
            };

            var grid = new Grid();
            grid.Children.Add(image);
            grid.Children.Add(excludeOverlay);
            grid.Children.Add(checkBox);
            grid.Children.Add(frameNumBg);
            grid.Children.Add(timeBg);

            var container = new Border
            {
                Width = targetWidth + 4,
                Height = targetHeight + 4,
                Margin = new Thickness(2),
                BorderBrush = _node.IsFrameExcluded(timestamp)
                    ? Brushes.OrangeRed : (Brush)FindResource("ThemeCardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromArgb(200, 15, 15, 15)),
                Tag = timestamp,
                Child = grid
            };

            WireThumbCheckboxEvents(checkBox, excludeOverlay, container, timestamp);
            WireThumbInteraction(container, checkBox);

            return container;
        }

        /// <summary>Wire checkbox Checked/Unchecked cho thumbnail frame.</summary>
        private void WireThumbCheckboxEvents(CheckBox checkBox, Border excludeOverlay,
            Border container, double timestamp)
        {
            checkBox.Checked += (_, _) =>
            {
                var fps = Math.Max(0.5, _node.ExtractFps);
                _node.ToggleFrameExclusion(timestamp, exclude: true);
                excludeOverlay.Visibility = Visibility.Visible;
                container.BorderBrush = Brushes.OrangeRed;

                // Cache thumbnail khi exclude — dùng cho pinned section khi FPS / giây đổi
                if (container.Child is Grid g && g.Children.Count > 0 &&
                    g.Children[0] is Image img && img.Source is BitmapSource bmp)
                {
                    RemoveCachedExcludedThumb(timestamp, fps);
                    _excludedThumbCache[timestamp] = bmp;
                }
                UpdateExcludedCountText();
            };
            checkBox.Unchecked += (_, _) =>
            {
                var fps = Math.Max(0.5, _node.ExtractFps);
                _node.ToggleFrameExclusion(timestamp, exclude: false);
                excludeOverlay.Visibility = Visibility.Collapsed;
                container.BorderBrush = (Brush)FindResource("ThemeCardBorderBrush");
                RemoveCachedExcludedThumb(timestamp, fps);
                UpdateExcludedCountText();
                // Nếu bỏ tích pinned frame → refresh pinned section
                RefreshPinnedAfterUncheck();
            };
        }

        /// <summary>Wire Del key + click toggle cho thumbnail container.</summary>
        private static void WireThumbInteraction(Border container, CheckBox checkBox)
        {
            container.Focusable = true;
            container.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Delete)
                {
                    checkBox.IsChecked = !(checkBox.IsChecked ?? false);
                    e.Handled = true;
                }
            };
            container.MouseLeftButtonDown += (_, e) =>
            {
                if (e.OriginalSource is not CheckBox)
                {
                    checkBox.IsChecked = !(checkBox.IsChecked ?? false);
                    e.Handled = true;
                }
            };
        }

        // ─── Pinned Excluded Frames ───────────────────────────────────────

        /// <summary>
        /// Hiển thị các frame excluded mà timestamp không nằm trên grid FPS hiện tại
        /// hoặc nằm ngoài window đang hiển thị.
        /// </summary>
        private void RenderPinnedExcludedFrames(
            double windowStart, double windowEnd, double currentFps,
            CancellationToken ct)
        {
            if (FindName("PinnedExcludedSection") is not Border section) return;
            if (FindName("PinnedExcludedWrapPanel") is not WrapPanel wrap) return;

            wrap.Children.Clear();

            var excludedTimestamps = _node.ExcludedFrameTimestamps;
            if (excludedTimestamps.Count == 0)
            {
                section.Visibility = Visibility.Collapsed;
                return;
            }

            var videoPath = _node.VideoPath;
            var pinnedItems = new List<(double ts, BitmapSource? img)>();

            foreach (var ts in excludedTimestamps)
            {
                // Bỏ qua nếu nằm trong window hiện tại (đã hiển thị ở main strip)
                if (ts >= windowStart && ts < windowEnd && IsOnFpsGrid(ts, currentFps))
                    continue;

                // Lấy thumbnail từ cache (bằng tolerance lookup) hoặc extract mới
                var bmp = GetCachedExcludedThumb(ts, currentFps);
                if (bmp == null && !string.IsNullOrWhiteSpace(videoPath) && File.Exists(videoPath))
                {
                    bmp = ExtractSingleFrameThumbnailSafe(videoPath, ts, ct);
                    if (bmp != null) _excludedThumbCache[ts] = bmp;
                }

                if (bmp != null) pinnedItems.Add((ts, bmp));
            }

            if (pinnedItems.Count == 0)
            {
                section.Visibility = Visibility.Collapsed;
                return;
            }

            section.Visibility = Visibility.Visible;
            foreach (var (ts, bmp) in pinnedItems.OrderBy(p => p.ts))
            {
                if (ct.IsCancellationRequested) break;
                if (bmp != null)
                {
                    var frameIdx = (int)Math.Round(ts * currentFps);
                    wrap.Children.Add(CreateFrameThumbnailControl(bmp, ts, frameIdx));
                }
            }
        }

        /// <summary>Kiểm tra timestamp có nằm trên grid FPS không.</summary>
        private static bool IsOnFpsGrid(double timestamp, double fps)
        {
            if (fps <= 0) return false;
            var interval = 1.0 / fps;
            var nearestIndex = Math.Round(timestamp / interval);
            var nearestTs = nearestIndex * interval;
            return Math.Abs(timestamp - nearestTs) < 0.02;
        }

        /// <summary>Extract 1 frame an toàn (bắt exception).</summary>
        private BitmapSource? ExtractSingleFrameThumbnailSafe(
            string videoPath, double ts, CancellationToken ct)
        {
            try { return ExtractSingleFrameThumbnail(videoPath, ts, ct); }
            catch { return null; }
        }

        /// <summary>Sau khi bỏ tích pinned frame → refresh section.</summary>
        private void RefreshPinnedAfterUncheck()
        {
            if (!_frameStripVisible) return;
            if (FindName("PinnedExcludedSection") is not Border section) return;

            var fps = Math.Max(0.5, _node.ExtractFps);
            var duration = GetNaturalDurationSeconds();
            var windowStart = Math.Max(0, _frameStripCenterSecond - 1);
            var windowEnd = Math.Min(duration, _frameStripCenterSecond + 2);

            using var cts = new CancellationTokenSource();
            RenderPinnedExcludedFrames(windowStart, windowEnd, fps, cts.Token);
        }
        /// <summary>Chọn (exclude) tất cả frame đang hiển thị.</summary>
        private void SelectAllVisibleFrames()
        {
            var fps = Math.Max(0.5, _node.ExtractFps);
            foreach (var (border, cb, overlay, bmp, ts) in GetVisibleThumbnailControls())
            {
                _node.ToggleFrameExclusion(ts, exclude: true);
                if (bmp != null)
                {
                    RemoveCachedExcludedThumb(ts, fps);
                    _excludedThumbCache[ts] = bmp;
                }
                if (cb != null) cb.IsChecked = true;
                if (overlay != null) overlay.Visibility = Visibility.Visible;
                border.BorderBrush = Brushes.OrangeRed;
            }
        }

        /// <summary>Bỏ chọn chỉ các frame đang hiển thị (visible).</summary>
        private void ClearVisibleExcludedFrames()
        {
            var fps = Math.Max(0.5, _node.ExtractFps);
            foreach (var (border, cb, overlay, bmp, ts) in GetVisibleThumbnailControls())
            {
                _node.ToggleFrameExclusion(ts, exclude: false);
                RemoveCachedExcludedThumb(ts, fps);
                if (cb != null) cb.IsChecked = false;
                if (overlay != null) overlay.Visibility = Visibility.Collapsed;
                border.BorderBrush = (Brush)FindResource("ThemeCardBorderBrush");
            }
        }

        /// <summary>Bỏ chọn tất cả pinned excluded frames (ngoài window hiện tại).</summary>
        private void ClearPinnedExcludedFrames()
        {
            var fps = Math.Max(0.5, _node.ExtractFps);
            var duration = GetNaturalDurationSeconds();
            var windowStart = Math.Max(0, _frameStripCenterSecond - 1);
            var windowEnd = Math.Min(duration, _frameStripCenterSecond + 2);

            // Tìm các excluded timestamp nằm ngoài window hiện tại hoặc không trên grid
            var pinnedTimestamps = _node.ExcludedFrameTimestamps
                .Where(ts => ts < windowStart || ts >= windowEnd || !IsOnFpsGrid(ts, fps))
                .ToList();

            foreach (var ts in pinnedTimestamps)
            {
                _node.ToggleFrameExclusion(ts, exclude: false);
                RemoveCachedExcludedThumb(ts, fps);
            }

            // Refresh pinned section
            RefreshPinnedAfterUncheck();
        }

        /// <summary>Tìm thumbnail cache theo timestamp có sai số tolerance.</summary>
        private BitmapSource? GetCachedExcludedThumb(double ts, double fps)
        {
            var tolerance = fps > 0 ? 0.5 / fps : 0.02;
            foreach (var kvp in _excludedThumbCache)
            {
                if (Math.Abs(kvp.Key - ts) < tolerance)
                    return kvp.Value;
            }
            return null;
        }

        /// <summary>Xóa thumbnail cache theo timestamp có sai số tolerance.</summary>
        private void RemoveCachedExcludedThumb(double ts, double fps)
        {
            var tolerance = fps > 0 ? 0.5 / fps : 0.02;
            var keysToRemove = _excludedThumbCache.Keys.Where(k => Math.Abs(k - ts) < tolerance).ToList();
            foreach (var k in keysToRemove)
                _excludedThumbCache.Remove(k);
        }

        /// <summary>Lấy danh sách các thumbnail control đang hiển thị.</summary>
        private List<(Border container, CheckBox? cb, Border? overlay, BitmapSource? bmp, double ts)> GetVisibleThumbnailControls()
        {
            var containers = new List<Border>();

            if (FindName("FrameStripContentPanel") is StackPanel contentPanel)
            {
                foreach (var wp in contentPanel.Children.OfType<WrapPanel>())
                    containers.AddRange(wp.Children.OfType<Border>());
            }

            if (containers.Count == 0 && FindName("FrameStripWrapPanel") is WrapPanel wrapPanel)
            {
                containers.AddRange(wrapPanel.Children.OfType<Border>());
            }

            var result = new List<(Border, CheckBox?, Border?, BitmapSource?, double)>();
            foreach (var border in containers)
            {
                if (border.Tag is not double ts) continue;

                CheckBox? cb = null;
                Border? overlay = null;
                BitmapSource? bmp = null;

                if (border.Child is Grid g)
                {
                    if (g.Children.Count > 0 && g.Children[0] is Image img && img.Source is BitmapSource bs)
                        bmp = bs;
                    if (g.Children.Count > 1 && g.Children[1] is Border ov)
                        overlay = ov;
                    if (g.Children.Count > 2 && g.Children[2] is CheckBox c)
                        cb = c;
                }

                result.Add((border, cb, overlay, bmp, ts));
            }

            return result;
        }

        // ─── Helpers ─────────────────────────────────────────────────────

        private void CancelPendingFrameStrip()
        {
            try
            {
                _frameStripCts?.Cancel();
                _frameStripCts?.Dispose();
            }
            catch { }
        }

        private void RefreshFrameStripExclusionVisuals()
        {
            if (FindName("FrameStripWrapPanel") is not WrapPanel wrapPanel) return;
            RefreshExclusionVisualsInPanel(wrapPanel);

            // Also refresh in FrameStripContentPanel (grouped mode)
            if (FindName("FrameStripContentPanel") is StackPanel contentPanel)
            {
                foreach (var child in contentPanel.Children.OfType<WrapPanel>())
                    RefreshExclusionVisualsInPanel(child);
            }
        }

        private void RefreshExclusionVisualsInPanel(WrapPanel panel)
        {
            foreach (var child in panel.Children.OfType<Border>())
            {
                if (child.Tag is not double ts) continue;
                var isExcluded = _node.IsFrameExcluded(ts);
                child.BorderBrush = isExcluded
                    ? Brushes.OrangeRed : (Brush)FindResource("ThemeCardBorderBrush");

                if (child.Child is Grid g)
                {
                    // Overlay is at index 1
                    if (g.Children.Count > 1 && g.Children[1] is Border overlay)
                        overlay.Visibility = isExcluded ? Visibility.Visible : Visibility.Collapsed;
                    // Checkbox is at index 2
                    if (g.Children.Count > 2 && g.Children[2] is CheckBox cb)
                        cb.IsChecked = isExcluded;
                }
            }
        }

        private void UpdateExcludedCountText()
        {
            var count = _node.ExcludedFrameTimestamps.Count;
            if (FindName("ExcludedCountText") is TextBlock tb)
                tb.Text = count.ToString();

            // Cập nhật badge đỏ (-N) bên cạnh EstimatedFrameCountText
            if (FindName("ExcludedFrameBadge") is TextBlock badge)
            {
                if (count > 0)
                {
                    badge.Text = $"(-{count})";
                    badge.Visibility = Visibility.Visible;
                }
                else
                {
                    badge.Visibility = Visibility.Collapsed;
                }
            }
        }

        private static BitmapImage LoadBitmapImageFromFile(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private static void AddPlaceholderText(WrapPanel panel, string text)
        {
            panel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = Brushes.Gray,
                Margin = new Thickness(4)
            });
        }

        /// <summary>Format timestamp với millisecond precision (ví dụ: 02:03.234).</summary>
        private static string FormatTimeMs(TimeSpan value)
        {
            if (value.TotalHours >= 1)
                return $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds:000}";
            return $"{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds:000}";
        }
    }
}
