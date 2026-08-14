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
        /// <summary>Số frame thumbnail đại diện tối đa được tạo.</summary>
        private const int MaxFrameStripThumbnails = 40;
        /// <summary>Kích thước mỗi thumbnail (px).</summary>
        private const int FrameThumbSize = 80;

        private CancellationTokenSource? _frameStripCts;
        private bool _frameStripLoaded;
        private bool _frameStripVisible;

        // ─── Toggle & Load ───────────────────────────────────────────────

        /// <summary>
        /// Gắn sự kiện cho nút FrameExcludeToggle, ClearExcludedFramesButton, RefreshFrameStripButton.
        /// </summary>
        private void WireFrameExclusionEvents()
        {
            if (FindName("FrameExcludeToggle") is Button toggle)
            {
                toggle.Click += (_, _) =>
                {
                    _frameStripVisible = !_frameStripVisible;
                    if (_frameStripVisible)
                        ShowFrameStripPanel();
                    else
                        HideFrameStripPanel();
                };
            }

            if (FindName("ClearExcludedFramesButton") is Button clearBtn)
            {
                clearBtn.Click += (_, _) =>
                {
                    _node.ClearExcludedFrames();
                    RefreshFrameStripExclusionVisuals();
                    UpdateExcludedCountText();
                };
            }

            if (FindName("RefreshFrameStripButton") is Button refreshBtn)
            {
                refreshBtn.Click += async (_, _) =>
                {
                    _frameStripLoaded = false;
                    await LoadFrameStripThumbnailsAsync();
                };
            }
        }

        private void ShowFrameStripPanel()
        {
            if (FindName("FrameStripPanel") is Border panel)
                panel.Visibility = Visibility.Visible;

            if (!_frameStripLoaded)
                _ = LoadFrameStripThumbnailsAsync();
        }

        private void HideFrameStripPanel()
        {
            if (FindName("FrameStripPanel") is Border panel)
                panel.Visibility = Visibility.Collapsed;
        }

        // ─── Thumbnail Generation ────────────────────────────────────────

        /// <summary>
        /// Tách thumbnail đại diện từ video bằng 1 tiến trình FFmpeg duy nhất, load vào FrameStripWrapPanel.
        /// </summary>
        private async Task LoadFrameStripThumbnailsAsync()
        {
            try
            {
                _frameStripCts?.Cancel();
                _frameStripCts?.Dispose();
            }
            catch { }

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

            AddPlaceholderText(wrapPanel, "Đang tải frame...");

            try
            {
                var thumbDir = Path.Combine(Path.GetTempPath(), "FlowMy_FrameStrip", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(thumbDir);

                var videoDuration = GetNaturalDurationSeconds();
                if (videoDuration <= 0) videoDuration = _node.TrimEndSec > 0 ? _node.TrimEndSec : 60;

                var thumbs = await Task.Run(() =>
                    ExtractFrameStripThumbnails(videoPath, thumbDir, videoDuration, ct), ct).ConfigureAwait(true);

                if (ct.IsCancellationRequested) return;

                wrapPanel.Children.Clear();
                if (thumbs.Count == 0)
                {
                    AddPlaceholderText(wrapPanel, "Không tạo được frame thumbnail từ video.");
                    return;
                }

                foreach (var item in thumbs)
                {
                    if (ct.IsCancellationRequested) break;
                    var thumb = CreateFrameThumbnailControl(item.image, item.timestamp);
                    wrapPanel.Children.Add(thumb);
                }

                _frameStripLoaded = true;
                RefreshFrameStripExclusionVisuals();
                UpdateExcludedCountText();
            }
            catch (OperationCanceledException)
            {
                // Hủy tác vụ hợp lệ khi user thao tác khác
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    wrapPanel.Children.Clear();
                    AddPlaceholderText(wrapPanel, $"Lỗi: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Chạy 1 lệnh FFmpeg duy nhất tách frame theo FPS đại diện (siêu nhanh, không spam process).
        /// </summary>
        private List<(BitmapSource image, double timestamp)> ExtractFrameStripThumbnails(
            string videoPath, string outputDir, double duration, CancellationToken ct)
        {
            var results = new List<(BitmapSource, double)>();
            if (ct.IsCancellationRequested) return results;

            var ffmpegExe = FfmpegPathPreferencesStore.ResolveBinaryPath("ffmpeg");
            if (string.IsNullOrWhiteSpace(ffmpegExe) || !File.Exists(ffmpegExe))
                return results;

            var count = Math.Min(32, Math.Max(8, (int)(duration / 2)));
            var interval = Math.Max(0.05, duration / count);
            var extractFps = 1.0 / interval;
            var outFilePattern = Path.Combine(outputDir, "thumb_%04d.jpg");

            var args = $"-hide_banner -loglevel error -i \"{videoPath}\" -vf \"fps={extractFps.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)},scale={FrameThumbSize}:-1\" -q:v 5 -y \"{outFilePattern}\"";

            var psi = new ProcessStartInfo(ffmpegExe, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                using var reg = ct.Register(() =>
                {
                    try { if (!proc.HasExited) proc.Kill(); } catch { }
                });

                proc.WaitForExit(10000);
            }

            if (ct.IsCancellationRequested) return results;

            var files = Directory.GetFiles(outputDir, "thumb_*.jpg")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < files.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                var file = files[i];
                var ts = Math.Min(duration, i * interval);

                try
                {
                    var bmp = LoadBitmapImageFromFile(file);
                    if (bmp != null)
                        results.Add((bmp, ts));
                }
                catch { }
            }

            return results;
        }

        // ─── UI Controls ─────────────────────────────────────────────────

        /// <summary>
        /// Tạo 1 thumbnail control với checkbox overlay cho mỗi frame.
        /// </summary>
        private Border CreateFrameThumbnailControl(BitmapSource bitmapSource, double timestamp)
        {
            var image = new Image
            {
                Width = FrameThumbSize,
                Height = FrameThumbSize * 0.5625, // 16:9 aspect
                Stretch = Stretch.UniformToFill,
                Source = bitmapSource
            };

            var checkBox = new CheckBox
            {
                IsChecked = _node.IsFrameExcluded(timestamp),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 2, 2, 0),
                ToolTip = "Tích để loại bỏ frame này"
            };

            var timeLabel = new TextBlock
            {
                Text = FormatTime(TimeSpan.FromSeconds(timestamp)),
                FontSize = 8,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 1)
            };

            var timeBg = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(3, 1, 3, 1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 2),
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
            grid.Children.Add(timeBg);

            var container = new Border
            {
                Width = FrameThumbSize + 4,
                Height = (FrameThumbSize * 0.5625) + 4,
                Margin = new Thickness(2),
                BorderBrush = _node.IsFrameExcluded(timestamp)
                    ? Brushes.OrangeRed : (Brush)FindResource("ThemeCardBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Cursor = Cursors.Hand,
                Tag = timestamp,
                Child = grid
            };

            // Wire checkbox toggle
            checkBox.Checked += (_, _) =>
            {
                _node.ToggleFrameExclusion(timestamp);
                excludeOverlay.Visibility = Visibility.Visible;
                container.BorderBrush = Brushes.OrangeRed;
                UpdateExcludedCountText();
            };
            checkBox.Unchecked += (_, _) =>
            {
                _node.ToggleFrameExclusion(timestamp);
                excludeOverlay.Visibility = Visibility.Collapsed;
                container.BorderBrush = (Brush)FindResource("ThemeCardBorderBrush");
                UpdateExcludedCountText();
            };

            // Wire Del key on container
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
                // Click vào thumbnail (không phải checkbox) → toggle
                if (e.OriginalSource is not CheckBox)
                {
                    checkBox.IsChecked = !(checkBox.IsChecked ?? false);
                    e.Handled = true;
                }
            };

            return container;
        }

        // ─── Helpers ─────────────────────────────────────────────────────

        private void RefreshFrameStripExclusionVisuals()
        {
            if (FindName("FrameStripWrapPanel") is not WrapPanel wrapPanel) return;
            foreach (var child in wrapPanel.Children.OfType<Border>())
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
            if (FindName("ExcludedCountText") is System.Windows.Documents.Run run)
                run.Text = _node.ExcludedFrameTimestamps.Count.ToString();
        }

        private static BitmapImage LoadBitmapImageFromFile(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.DecodePixelWidth = FrameThumbSize;
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
    }
}
