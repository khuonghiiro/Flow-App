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
        private void ApplyGradingPreset(double brightness, double contrast, double saturation, double hue, double gamma)
        {
            _previewEffectTemporarilyDisabled = false;
            _node.Brightness = brightness;
            _node.Contrast = contrast;
            _node.Saturation = saturation;
            _node.Hue = hue;
            _node.Gamma = gamma;
            SyncControlValuesFromModel();
            ApplyPreviewColorTransform();
        }

        private void ApplyPreviewColorTransform()
        {
            if (_previewEffectTemporarilyDisabled)
            {
                PreviewMedia.Effect = null;
                GradingOverlay.Background = Brushes.Transparent;
                PreviewMedia.Opacity = 1.0;
                return;
            }

            var brightness = Math.Clamp(_node.Brightness, -1.0, 1.0);
            var contrast = Math.Clamp(_node.Contrast, 0.1, 3.0);
            var saturation = Math.Clamp(_node.Saturation, 0.0, 3.0);
            var hueDeg = Math.Clamp(_node.Hue, -180.0, 180.0);
            var gamma = Math.Clamp(_node.Gamma, 0.1, 3.0);

            if (VideoEqEffect.ShaderAvailable)
            {
                _videoEqEffect ??= new VideoEqEffect();
                var hueRad = hueDeg * (Math.PI / 180.0);
                _videoEqEffect.Bc = new System.Windows.Point(brightness, contrast);
                _videoEqEffect.Sg = new System.Windows.Point(saturation, gamma);
                _videoEqEffect.HueCs = new System.Windows.Point(Math.Cos(hueRad), Math.Sin(hueRad));
                PreviewMedia.Effect = _videoEqEffect;
                GradingOverlay.Background = Brushes.Transparent;
                PreviewMedia.Opacity = 1.0;
                return;
            }

            // Software fallback — approximate tint + opacity (legacy preview).
            var strength = (_node.PreviewVisualStrengthMode ?? "balanced").ToLowerInvariant();
            var strengthScale = strength switch
            {
                "fast" => 0.65,
                "strong" => 1.45,
                _ => 1.0
            };

            var tintStrength = Math.Min(0.45, (Math.Abs(hueDeg) / 180.0 * 0.28 + Math.Max(0, saturation - 1.0) * 0.06) * strengthScale);
            var hueColor = HsvToColor((hueDeg + 360.0) % 360.0, 0.9, 1.0);
            byte tintAlpha;
            Color tintRgb;

            if (brightness >= 0)
            {
                tintAlpha = (byte)Math.Clamp((int)((brightness * 90 + tintStrength * 100) * strengthScale), 0, 170);
                tintRgb = tintStrength > 0.01 ? hueColor : Color.FromRgb(255, 255, 255);
            }
            else
            {
                tintAlpha = (byte)Math.Clamp((int)((-brightness * 120 + tintStrength * 90) * strengthScale), 0, 190);
                if (tintStrength > 0.01)
                {
                    tintRgb = Color.FromRgb(
                        (byte)Math.Max(0, hueColor.R - 55),
                        (byte)Math.Max(0, hueColor.G - 55),
                        (byte)Math.Max(0, hueColor.B - 55));
                }
                else
                {
                    tintRgb = Color.FromRgb(0, 0, 0);
                }
            }

            PreviewMedia.Effect = null;
            GradingOverlay.Background = new SolidColorBrush(Color.FromArgb(tintAlpha, tintRgb.R, tintRgb.G, tintRgb.B));

            var contrastOpacityBoost = (contrast - 1.0) * 0.11 * strengthScale;
            var saturationPenalty = (1.0 - Math.Min(1.0, saturation)) * 0.18 * strengthScale;
            var gammaPenalty = Math.Max(0, 1.0 - gamma) * 0.12 * strengthScale;
            PreviewMedia.Opacity = Math.Clamp(1.0 + contrastOpacityBoost - saturationPenalty - gammaPenalty, 0.52, 1.0);
        }

        /// <summary>
        /// Applies real-time preview transforms: Blur, Rotate/Flip, Speed.
        /// Blur uses BlurEffect on VideoViewbox (separate from color grading on PreviewMedia).
        /// Rotate/Flip uses RenderTransform on VideoViewbox.
        /// Speed uses MediaElement.SpeedRatio.
        /// </summary>
        private void ApplyPreviewTransformEffects()
        {
            // --- Blur ---
            if (_node.BlurEnabled && _node.BlurRadius > 0)
            {
                VideoViewbox.Effect = new BlurEffect { Radius = _node.BlurRadius * 1.5, KernelType = KernelType.Gaussian };
            }
            else
            {
                VideoViewbox.Effect = null;
            }

            // --- Rotate / Flip ---
            var rotation = _node.RotationDegrees % 360;
            var scaleX = _node.FlipH ? -1.0 : 1.0;
            var scaleY = _node.FlipV ? -1.0 : 1.0;

            if (Math.Abs(rotation) < 0.1 && scaleX > 0 && scaleY > 0)
            {
                VideoViewbox.RenderTransform = null;
            }
            else
            {
                var group = new TransformGroup();
                if (Math.Abs(rotation) > 0.1)
                    group.Children.Add(new RotateTransform(rotation));
                if (scaleX < 0 || scaleY < 0)
                    group.Children.Add(new ScaleTransform(scaleX, scaleY));
                VideoViewbox.RenderTransformOrigin = new Point(0.5, 0.5);
                VideoViewbox.RenderTransform = group;
            }

            // --- Speed ---
            try
            {
                if (PreviewMedia.Source != null)
                {
                    var targetSpeed = Math.Clamp(_node.SpeedFactor, 0.1, 8.0);
                    if (Math.Abs(PreviewMedia.SpeedRatio - targetSpeed) > 0.01)
                        PreviewMedia.SpeedRatio = targetSpeed;
                }
            }
            catch
            {
                /* best-effort: avoid WPF MediaElement exception when uninitialized */
            }
        }

        private static Color HsvToColor(double hue, double saturation, double value)
        {
            var c = value * saturation;
            var x = c * (1 - Math.Abs((hue / 60.0 % 2) - 1));
            var m = value - c;
            double r1, g1, b1;
            if (hue < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (hue < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (hue < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (hue < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (hue < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            return Color.FromRgb(
                (byte)Math.Clamp((int)((r1 + m) * 255), 0, 255),
                (byte)Math.Clamp((int)((g1 + m) * 255), 0, 255),
                (byte)Math.Clamp((int)((b1 + m) * 255), 0, 255));
        }

        private readonly System.Windows.Media.MediaPlayer _audioTrackPreviewPlayer = new System.Windows.Media.MediaPlayer();

        private void PreviewMixAudio_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not VideoAudioTrackConfig track) return;

            var audioPath = track.SourceOutputKey;
            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
            {
                AppendLog("⚠ Không tìm thấy file audio để nghe thử. Vui lòng chọn file audio hợp lệ.");
                return;
            }

            try
            {
                var startSec = Math.Max(0, track.StartAtSec);
                if (PreviewMedia != null)
                {
                    if (PreviewMedia.NaturalDuration.HasTimeSpan && startSec <= PreviewMedia.NaturalDuration.TimeSpan.TotalSeconds)
                        PreviewMedia.Position = TimeSpan.FromSeconds(startSec);
                    PreviewMedia.Play();
                }

                _audioTrackPreviewPlayer.Close();
                _audioTrackPreviewPlayer.Open(new Uri(audioPath, UriKind.Absolute));
                _audioTrackPreviewPlayer.Volume = Math.Clamp(track.VolumePercent / 100.0, 0.0, 1.0);

                var trimStartSec = Math.Max(0, track.TrimStartSec);
                _audioTrackPreviewPlayer.Position = TimeSpan.FromSeconds(trimStartSec);
                _audioTrackPreviewPlayer.Play();

                AppendLog($"▶ Đang nghe thử track audio lồng tại vị trí {startSec:0.##}s (Trim start: {trimStartSec:0.##}s)...");
            }
            catch (Exception ex)
            {
                AppendLog($"⚠ Lỗi nghe thử audio: {ex.Message}");
            }
        }

        private void RemoveAudioTrack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VideoAudioTrackConfig track)
            {
                _audioTrackPreviewPlayer.Close();
                _node.AudioTracks.Remove(track);
            }
        }

        private void BrowseConcatVideoItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VideoConcatItemConfig item)
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Chọn video ghép",
                    Filter = "Media Files|*.mp4;*.avi;*.mov;*.mkv;*.webm;*.flv;*.ts;*.m4v|All Files|*.*"
                };
                if (dialog.ShowDialog() == true)
                {
                    item.SourcePath = dialog.FileName;
                }
            }
        }

        private void RemoveConcatVideoItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VideoConcatItemConfig item)
            {
                _node.ConcatVideos.Remove(item);
            }
        }

        private void AddOverlayItem(string type)
        {
            if (type == "image")
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Chọn ảnh overlay",
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All|*.*"
                };
                if (dlg.ShowDialog() != true) return;

                double wFrac = 0.3;
                double hFrac = 0.3;

                try
                {
                    using var stream = File.OpenRead(dlg.FileName);
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count > 0 && decoder.Frames[0].PixelWidth > 0 && decoder.Frames[0].PixelHeight > 0)
                    {
                        var imgW = (double)decoder.Frames[0].PixelWidth;
                        var imgH = (double)decoder.Frames[0].PixelHeight;
                        var imgAspect = imgW / imgH;

                        var surfaceW = PreviewMedia.NaturalVideoWidth > 0 ? (double)PreviewMedia.NaturalVideoWidth : 1920.0;
                        var surfaceH = PreviewMedia.NaturalVideoHeight > 0 ? (double)PreviewMedia.NaturalVideoHeight : 1080.0;

                        wFrac = 0.3;
                        hFrac = (wFrac * surfaceW / imgAspect) / surfaceH;
                        hFrac = Math.Clamp(hFrac, 0.05, 0.9);
                    }
                }
                catch
                {
                    /* best-effort image aspect ratio probing */
                }

                _node.Overlays.Add(new OverlayItem
                {
                    Type = "image",
                    Source = dlg.FileName,
                    X = 0.08,
                    Y = 0.08,
                    Width = Math.Round(wFrac, 4),
                    Height = Math.Round(hFrac, 4),
                    Opacity = 1.0,
                    IsVisible = true
                });
            }
            else
            {
                _node.Overlays.Add(new OverlayItem
                {
                    Type = "text",
                    Source = "Double-click để sửa text",
                    X = 0.12,
                    Y = 0.12,
                    Width = 0.35,
                    Height = 0.15,
                    FontFamily = "Arial",
                    FontColor = "White",
                    FontSize = 28,
                    TextAlignment = "Left",
                    Opacity = 1.0,
                    IsVisible = true
                });
            }

            var selected = _node.Overlays.LastOrDefault();
            OverlayCanvasControl.SelectedItem = selected;
            OverlayLayerList.SelectedItem = selected;
        }

        private void RemoveSelectedOverlayItem()
        {
            if (OverlayLayerList.SelectedItem is not OverlayItem selected) return;
            _node.Overlays.Remove(selected);
            OverlayCanvasControl.SelectedItem = null;
            OverlayLayerList.SelectedItem = null;
        }

        private void MoveSelectedOverlay(int direction)
        {
            if (OverlayLayerList.SelectedItem is not OverlayItem selected) return;
            var currentIndex = _node.Overlays.IndexOf(selected);
            if (currentIndex < 0) return;
            var targetIndex = Math.Clamp(currentIndex + direction, 0, _node.Overlays.Count - 1);
            if (targetIndex == currentIndex) return;
            _node.Overlays.Move(currentIndex, targetIndex);
            OverlayLayerList.SelectedItem = selected;
        }

        private void OverlayLayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OverlayLayerList.SelectedItem is OverlayItem selected)
            {
                OverlayCanvasControl.SelectedItem = selected;
                SyncOverlayEditorFromSelection(selected);
            }
            else
            {
                SyncOverlayEditorFromSelection(null);
            }
        }

        private void OverlayCanvasControl_SelectionChanged(object? sender, OverlayItem? item)
        {
            OverlayLayerList.SelectedItem = item;
            SyncOverlayEditorFromSelection(item);
        }

        private void ApplyOverlaysToVideo()
        {
            var visibleCount = _node.Overlays.Count(o => o.IsVisible);
            if (visibleCount == 0)
            {
                AppendLog("⚠ Chưa có overlay nào đang hiển thị để áp dụng.");
                return;
            }

            _pendingOverlayApply = true;
            _beforePreviewPath = _node.VideoPath;
            _showAfterPreview = false;
            _isFlickerMode = false;
            _beforeAfterFlickerTimer.Stop();
            TabNavList.SelectedIndex = 6;
            AppendLog($"🎞 Bắt đầu áp dụng {visibleCount} overlay item lên video...");
            RunProcessingFlow();
        }

        private void OnOverlayApplyCompleted()
        {
            if (!_pendingOverlayApply) return;
            _pendingOverlayApply = false;

            var outputCandidate = (_node.OutputPathOverride ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputCandidate) || !File.Exists(outputCandidate))
            {
                outputCandidate = OutputVideoPathText.Text?.Trim() ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(outputCandidate) && File.Exists(outputCandidate))
            {
                _afterPreviewPath = outputCandidate;
                _showAfterPreview = true;
                _isFlickerMode = false;
                _beforeAfterFlickerTimer.Stop();
                LoadPreviewFromPath(_afterPreviewPath, isAfterPath: true);
                ToggleBeforeAfterButton.Content = "After";
                AppendLog("✅ Đã áp dụng overlay và chuyển preview sang bản After.");
            }
            else
            {
                AppendLog("ℹ Xử lý xong nhưng chưa tìm thấy file output để bật preview After.");
            }
        }

        private void ToggleBeforeAfterPreview()
        {
            if (string.IsNullOrWhiteSpace(_beforePreviewPath) || string.IsNullOrWhiteSpace(_afterPreviewPath))
            {
                AppendLog("ℹ Chưa có đủ before/after để so sánh.");
                return;
            }

            if (!_showAfterPreview && !_isFlickerMode)
            {
                _showAfterPreview = true;
                LoadPreviewFromPath(_afterPreviewPath, isAfterPath: true);
                ToggleBeforeAfterButton.Content = "After";
                return;
            }

            if (_showAfterPreview && !_isFlickerMode)
            {
                _isFlickerMode = true;
                _beforeAfterFlickerTimer.Start();
                ToggleBeforeAfterButton.Content = "Flicker";
                return;
            }

            _isFlickerMode = false;
            _beforeAfterFlickerTimer.Stop();
            _showAfterPreview = false;
            LoadPreviewFromPath(_beforePreviewPath, isAfterPath: false);
            ToggleBeforeAfterButton.Content = "Before";
        }

        private void LoadPreviewFromPath(string? path, bool isAfterPath)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            _showAfterPreview = isAfterPath;
            _isSwitchingComparePreview = true;
            _node.VideoPath = path;
            _node.RaisePropertyChanged(nameof(VideoProcessingNode.VideoPath));
        }

        private void StopComparePreviewMode()
        {
            _isFlickerMode = false;
            _beforeAfterFlickerTimer.Stop();
            _showAfterPreview = false;
            ToggleBeforeAfterButton.Content = "Before/After";
        }

        private void SyncOverlayEditorFromSelection(OverlayItem? item)
        {
            _suppressOverlayEditorSync = true;
            try
            {
                var has = item != null;
                OverlayTypeCombo.IsEnabled = has;
                OverlaySourcePathTextBox.IsEnabled = has;
                OverlaySourceTextArea.IsEnabled = has;
                OverlayXSlider.IsEnabled = has;
                OverlayYSlider.IsEnabled = has;
                OverlayWidthSlider.IsEnabled = has;
                OverlayHeightSlider.IsEnabled = has;
                OverlayOpacitySlider.IsEnabled = has;
                OverlayRotationSlider.IsEnabled = has;
                OverlayFontFamilyCombo.IsEnabled = has;
                OverlayFontColorTextBox.IsEnabled = has;
                OverlayFontSizeSlider.IsEnabled = has;
                OverlayTextAlignRow.IsEnabled = has;
                OverlayVisibleCheckBox.IsEnabled = has;
                OverlayLockedCheckBox.IsEnabled = has;

                if (!has)
                {
                    OverlayTypeCombo.SelectedIndex = -1;
                    OverlaySourcePathTextBox.Text = string.Empty;
                    OverlaySourceTextArea.Text = string.Empty;
                    OverlayTextPropsPanel.Visibility = Visibility.Collapsed;
                    OverlayImageSourcePanel.Visibility = Visibility.Collapsed;
                    return;
                }

                OverlayTypeCombo.SelectedIndex = (item!.Type ?? "text").ToLowerInvariant() switch
                {
                    "image" => 1,
                    "logo" => 2,
                    _ => 0
                };

                var isText = string.Equals((item.Type ?? "text").Trim(), "text", StringComparison.OrdinalIgnoreCase);
                OverlayTextPropsPanel.Visibility = isText ? Visibility.Visible : Visibility.Collapsed;
                OverlayImageSourcePanel.Visibility = isText ? Visibility.Collapsed : Visibility.Visible;

                if (isText)
                    OverlaySourceTextArea.Text = item.Source;
                else
                    OverlaySourcePathTextBox.Text = item.Source;

                OverlayXSlider.Value = item.X;
                OverlayYSlider.Value = item.Y;
                OverlayWidthSlider.Value = item.Width;
                OverlayHeightSlider.Value = item.Height;
                OverlayOpacitySlider.Value = item.Opacity;
                OverlayRotationSlider.Value = item.Rotation;
                var desiredFont = string.IsNullOrWhiteSpace(item.FontFamily) ? "Arial" : item.FontFamily.Trim();
                var match = OverlayFontFamilyCombo.Items.OfType<string>()
                    .FirstOrDefault(s => string.Equals(s, desiredFont, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    OverlayFontFamilyCombo.Items.Add(desiredFont);
                    match = desiredFont;
                }
                OverlayFontFamilyCombo.SelectedItem = match;
                OverlayFontColorTextBox.Text = item.FontColor;
                OverlayFontSizeSlider.Value = item.FontSize;
                var align = (item.TextAlignment ?? "Left").Trim().ToLowerInvariant();
                OverlayAlignLeftRadio.IsChecked = align != "center" && align != "right";
                OverlayAlignCenterRadio.IsChecked = align == "center";
                OverlayAlignRightRadio.IsChecked = align == "right";
                OverlayVisibleCheckBox.IsChecked = item.IsVisible;
                OverlayLockedCheckBox.IsChecked = item.IsLocked;
            }
            finally
            {
                _suppressOverlayEditorSync = false;
            }
        }

        private void ApplyOverlayPropertyEditorChanges()
        {
            if (_suppressOverlayEditorSync) return;
            if (OverlayLayerList.SelectedItem is not OverlayItem selected) return;

            var selectedType = (OverlayTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "text";
            selected.Type = selectedType;
            var isText = string.Equals(selectedType, "text", StringComparison.OrdinalIgnoreCase);
            OverlayTextPropsPanel.Visibility = isText ? Visibility.Visible : Visibility.Collapsed;
            OverlayImageSourcePanel.Visibility = isText ? Visibility.Collapsed : Visibility.Visible;
            selected.Source = isText ? (OverlaySourceTextArea.Text ?? string.Empty) : (OverlaySourcePathTextBox.Text ?? string.Empty);
            selected.X = OverlayXSlider.Value;
            selected.Y = OverlayYSlider.Value;
            selected.Width = OverlayWidthSlider.Value;
            selected.Height = OverlayHeightSlider.Value;
            selected.Opacity = OverlayOpacitySlider.Value;
            selected.Rotation = OverlayRotationSlider.Value;
            if (isText)
            {
                var family = (OverlayFontFamilyCombo.SelectedItem as string)
                    ?? "Arial";
                selected.FontFamily = family;
                selected.FontColor = OverlayFontColorTextBox.Text;
                selected.FontSize = (int)OverlayFontSizeSlider.Value;
                selected.TextAlignment = OverlayAlignCenterRadio.IsChecked == true ? "Center"
                    : OverlayAlignRightRadio.IsChecked == true ? "Right"
                    : "Left";
            }
            selected.IsVisible = OverlayVisibleCheckBox.IsChecked == true;
            selected.IsLocked = OverlayLockedCheckBox.IsChecked == true;
            OverlayLayerList.Items.Refresh();
        }

        private void BrowseAudioTrack_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not VideoAudioTrackConfig track) return;
            var dlg = new OpenFileDialog
            {
                Title = "Chọn file audio",
                Filter = "Audio Files|*.mp3;*.wav;*.aac;*.flac;*.ogg;*.m4a|All|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            track.SourceOutputKey = dlg.FileName;
            AudioTracksList.Items.Refresh();
        }

        private void RunSpecificOperation(string operationType)
        {
            if (string.IsNullOrWhiteSpace(_node.VideoPath))
            {
                AppendLog("⚠ Chưa chọn video nguồn.");
                return;
            }

            SyncRuntimeConfigFromUi();
            _lastRunStartedAtUtc = DateTime.UtcNow;
            ProgressStatusText.Text = $"Running: {operationType}...";

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    switch (operationType)
                    {
                        case "extract_frames":
                            var configuredFrameFolder = (_node.FrameOutputFolderPath ?? string.Empty).Trim();
                            if (!string.IsNullOrWhiteSpace(configuredFrameFolder))
                                EnsureDirectoryExists(configuredFrameFolder);
                            await VideoProcessingNodeExecutor.RunExtractFramesOnlyAsync(
                                _node,
                                line => AppendLog(line),
                                (pct, status) => UpdateProgress(pct, status),
                                configuredFrameFolder,
                                System.Threading.CancellationToken.None);
                            break;
                        case "burn_subtitle":
                            if (string.IsNullOrWhiteSpace(_node.SubtitlePath))
                            {
                                _ = Dispatcher.BeginInvoke(new Action(() => AppendLog("⚠ Chưa chọn file subtitle.")));
                                return;
                            }
                            await VideoProcessingNodeExecutor.RunBurnSubtitleAsync(
                                _node,
                                line => AppendLog(line),
                                (pct, status) => UpdateProgress(pct, status),
                                System.Threading.CancellationToken.None);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _ = Dispatcher.BeginInvoke(new Action(() => AppendLog($"❌ Error: {ex.Message}")));
                }
            });
        }

        private void TakeSnapshot()
        {
            if (PreviewMedia.Source == null) return;
            var dlg = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                FileName = $"snapshot_{DateTime.Now:HHmmss}.png"
            };
            if (dlg.ShowDialog() != true) return;
            var outputPath = dlg.FileName;
            var position = PreviewMedia.Position.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    // Source of truth: FFmpeg pipeline (same as extract output).
                    await VideoProcessingNodeExecutor.RunSnapshotAsync(_node, position, outputPath, System.Threading.CancellationToken.None);
                    _ = Dispatcher.BeginInvoke(new Action(() => AppendLog($"✅ Snapshot saved (ffmpeg): {outputPath}")));
                }
                catch (Exception ex)
                {
                    try
                    {
                        // UI capture fallback if FFmpeg fails.
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var root = GetVideoColumnRenderRoot();
                            root.UpdateLayout();
                            VideoAreaGrid.UpdateLayout();
                            var dpi = VisualTreeHelper.GetDpi(root);
                            var containerW = Math.Max(1, (int)Math.Round(root.ActualWidth * dpi.DpiScaleX));
                            var containerH = Math.Max(1, (int)Math.Round(root.ActualHeight * dpi.DpiScaleY));
                            var rtb = new RenderTargetBitmap(containerW, containerH, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
                            rtb.Render(root);
                            var displayedRect = GetDisplayedVideoRect();
                            var topLeft = VideoAreaGrid.TranslatePoint(new Point(displayedRect.X, displayedRect.Y), root);
                            var cropX = Math.Max(0, (int)Math.Floor(topLeft.X * dpi.DpiScaleX));
                            var cropY = Math.Max(0, (int)Math.Floor(topLeft.Y * dpi.DpiScaleY));
                            var cropW = Math.Max(1, Math.Min(containerW - cropX, (int)Math.Round(displayedRect.Width * dpi.DpiScaleX)));
                            var cropH = Math.Max(1, Math.Min(containerH - cropY, (int)Math.Round(displayedRect.Height * dpi.DpiScaleY)));
                            var cropped = new CroppedBitmap(rtb, new Int32Rect(cropX, cropY, cropW, cropH));
                            var encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(cropped));
                            using var fs = new System.IO.FileStream(outputPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
                            encoder.Save(fs);
                        });
                        _ = Dispatcher.BeginInvoke(new Action(() => AppendLog($"✅ Snapshot saved (UI fallback): {outputPath}")));
                    }
                    catch (Exception fallbackEx)
                    {
                        _ = Dispatcher.BeginInvoke(new Action(() => AppendLog($"❌ Snapshot failed: {ex.Message} | fallback: {fallbackEx.Message}")));
                    }
                }
            });
        }

        private void SetRotate(double deg, Button activeButton)
        {
            _node.RotationDegrees = deg;
            foreach (var b in new[] { Rotate0Button, Rotate90Button, Rotate180Button, Rotate270Button })
                b.ClearValue(BackgroundProperty);
            activeButton.Background = new SolidColorBrush(Color.FromRgb(0x7C, 0x6B, 0xF8));
            ApplyPreviewTransformEffects();
        }

        private void ToggleFlip(Button button, bool isHorizontal)
        {
            if (isHorizontal) _node.FlipH = !_node.FlipH; else _node.FlipV = !_node.FlipV;
            var enabled = isHorizontal ? _node.FlipH : _node.FlipV;
            if (enabled)
                button.Background = new SolidColorBrush(Color.FromRgb(0x7C, 0x6B, 0xF8));
            else
                button.ClearValue(BackgroundProperty);
            ApplyPreviewTransformEffects();
        }

        private void SetScale(double scale, int? fixedHeight, Button activeButton)
        {
            _fixedResolutionHeight = fixedHeight;
            _node.ResolutionScale = scale;
            _node.FixedResolutionHeight = fixedHeight;
            foreach (var b in new[] { Scale100Button, Scale75Button, Scale50Button, Scale25Button, Scale1080Button, Scale720Button })
                b.ClearValue(BackgroundProperty);
            activeButton.Background = new SolidColorBrush(Color.FromRgb(0x7C, 0x6B, 0xF8));
        }

        private void UpdateVolumeIcon()
        {
            MuteButton.Content = CreateTransportIcon(_isMuted ? "volume-xmark duotone-light" : (PreviewMedia.Volume > 0.5 ? "volume-high duotone-light" : "volume-low duotone-light"));
        }

        private void SetTransportIcons()
        {
            SkipBackButton.Content = CreateTransportIcon("backward regular");
            SkipForwardButton.Content = CreateTransportIcon("forward sharp-regular");
            PlayPauseButton.Content = CreateTransportIcon("play regular");
            StopButton.Content = CreateTransportIcon("stop sharp-regular");
            UpdateVideoPlayerExpandUi();
        }

        private SvgViewboxEx CreateTransportIcon(string iconKey)
        {
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(string.Empty, typeof(Uri), iconKey,
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            var icon = new SvgViewboxEx
            {
                Width = 14,
                Height = 14,
                Source = iconUri!,
                Fill = GetThemeIconBrush()
            };
            if (iconKey != null && iconKey.StartsWith("play", StringComparison.OrdinalIgnoreCase))
            {
                icon.Margin = new System.Windows.Thickness(2, 0, 0, 0);
            }
            return icon;
        }

        private Brush GetThemeIconBrush()
        {
            if (Resources["ThemeTextPrimaryBrush"] is Brush brush)
            {
                return brush;
            }

            return _isLightTheme
                ? new SolidColorBrush(Color.FromRgb(35, 42, 52))
                : new SolidColorBrush(Color.FromRgb(232, 240, 255));
        }

        private void ApplyLocalTheme()
        {
            var isLight = _isLightTheme;
            var shellBg = isLight ? Color.FromRgb(235, 240, 248) : Color.FromRgb(15, 15, 23);
            Background = new SolidColorBrush(shellBg);

            Color accentColor = Color.FromRgb(124, 107, 248);
            if (Application.Current?.TryFindResource("PrimaryBrush") is SolidColorBrush appPrimary && appPrimary.Color.A > 0)
                accentColor = appPrimary.Color;

            Color cardTop = isLight ? Color.FromRgb(255, 255, 255) : Color.FromArgb(26, 255, 255, 255);
            Color innerTop = isLight ? Color.FromRgb(244, 247, 253) : Color.FromArgb(24, 0, 0, 0);

            Color primaryText = isLight ? Color.FromRgb(15, 23, 42) : Color.FromRgb(236, 236, 244);
            Color secondaryText = isLight ? Color.FromRgb(51, 65, 85) : Color.FromRgb(156, 163, 184);
            Foreground = new SolidColorBrush(primaryText);

            Resources["ThemeTextPrimaryBrush"] = new SolidColorBrush(primaryText);
            Resources["ThemeTextSecondaryBrush"] = new SolidColorBrush(secondaryText);
            Resources["ThemeCardBackgroundBrush"] = new SolidColorBrush(cardTop);
            Resources["ThemeCardBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(196, 207, 224) : Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInnerCardBackgroundBrush"] = new SolidColorBrush(innerTop);
            Resources["ThemeInnerCardBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(204, 215, 232) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInputBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(255, 255, 255) : Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInputBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(168, 184, 208) : Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            Resources["ThemeInputForegroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(15, 23, 42) : Color.FromRgb(241, 245, 255));
            Resources["ThemeOverlayBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0xF2, 238, 243, 252) : Color.FromArgb(0xAA, 0x00, 0x00, 0x00));
            Resources["ThemeOverlayBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(176, 192, 216) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            Resources["ThemeTimelinePanelBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(226, 234, 246) : Color.FromArgb(0xEE, 0x0A, 0x0A, 0x18));
            Resources["ThemeTrackBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(148, 163, 184) : Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF));
            Resources["ThemeTimelineTrackBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(148, 163, 184) : Color.FromRgb(52, 54, 66));
            Resources["ThemeTimelineProgressBrush"] = new SolidColorBrush(accentColor);
            Resources["ThemeTimelineThumbStrokeBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(30, 41, 59) : Color.FromRgb(226, 232, 245));
            Resources["ThemeAccentGlowColor"] = accentColor;
            Resources["ThemeAccentBrush"] = new SolidColorBrush(accentColor);

            Resources["ThemePanelHeaderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(222, 230, 244) : Color.FromRgb(32, 35, 48));
            Resources["ThemePanelHeaderBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(172, 188, 214) : Color.FromRgb(53, 58, 77));
            Resources["ThemeVideoViewportBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(200, 210, 228) : Color.FromRgb(12, 15, 23));

            Color warmAmber = Color.FromRgb(0xF5, 0x9E, 0x0B);
            Resources["ThemeWarmAccentBrush"] = new SolidColorBrush(warmAmber);
            Resources["ThemeWarmAccentBrushSoft"] = new SolidColorBrush(Color.FromArgb(0x66, warmAmber.R, warmAmber.G, warmAmber.B));
            Resources["ThemeBottomBarGroupInactiveBorderBrush"] = new SolidColorBrush(
                isLight ? Color.FromRgb(164, 180, 206) : Color.FromArgb(0x42, 0xFF, 0xFF, 0xFF));
            Resources["ThemeBottomBarActiveGroupBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(isLight ? (byte)0x35 : (byte)0x2A, warmAmber.R, warmAmber.G, warmAmber.B));

            Color chromePrimaryBg = isLight ? Color.FromRgb(79, 70, 229) : Color.FromRgb(48, 50, 64);
            Color chromePrimaryHover = isLight ? Color.FromRgb(67, 56, 202) : Color.FromRgb(58, 61, 78);
            Color chromeSecondaryBg = isLight ? Color.FromRgb(226, 232, 244) : Color.FromRgb(40, 42, 54);
            Color chromeSecondaryHover = isLight ? Color.FromRgb(210, 220, 238) : Color.FromRgb(50, 52, 68);
            Resources["ThemeVideoChromePrimaryBgBrush"] = new SolidColorBrush(chromePrimaryBg);
            Resources["ThemeVideoChromePrimaryHoverBgBrush"] = new SolidColorBrush(chromePrimaryHover);
            Resources["ThemeVideoChromePrimaryFgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            Resources["ThemeVideoChromePrimaryBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(164, 180, 206) : Color.FromArgb(0x45, 0xFF, 0xFF, 0xFF));
            Resources["ThemeVideoChromeSecondaryBgBrush"] = new SolidColorBrush(chromeSecondaryBg);
            Resources["ThemeVideoChromeSecondaryHoverBgBrush"] = new SolidColorBrush(chromeSecondaryHover);
            Resources["ThemeVideoChromeSecondaryFgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(15, 23, 42) : Color.FromRgb(232, 236, 248));
            Resources["ThemeVideoChromeSecondaryBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(160, 176, 202) : Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF));
            Resources["ThemePresetChipBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(226, 234, 246) : Color.FromRgb(36, 37, 48));
            Resources["ThemePresetChipBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(164, 180, 206) : Color.FromRgb(58, 60, 76));
            Resources["ThemePresetChipHoverBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(208, 218, 238) : Color.FromRgb(48, 50, 66));
            Resources["ThemePresetChipPressedBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(192, 204, 228) : Color.FromRgb(44, 46, 60));
            Resources["ThemePresetChipResetBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(216, 224, 238) : Color.FromRgb(40, 44, 56));
            Resources["ThemePresetChipResetBorderBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(148, 164, 192) : Color.FromRgb(70, 74, 90));
            Color transportPlayBg = isLight ? Color.FromRgb(79, 70, 229) : Color.FromRgb(99, 102, 241);
            Color transportPlayHoverBg = isLight ? Color.FromRgb(67, 56, 202) : Color.FromRgb(79, 82, 220);
            Resources["ThemeTransportPlayBgBrush"] = new SolidColorBrush(transportPlayBg);
            Resources["ThemeTransportPlayHoverBgBrush"] = new SolidColorBrush(transportPlayHoverBg);
            Resources["ThemeTransportPlayFgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            Resources["ThemeTransportIconHoverBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(204, 214, 234) : Color.FromRgb(48, 50, 64));
            Resources["ThemeQuickOverlayHoverBgBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(208, 218, 236) : Color.FromRgb(52, 54, 70));
            Resources["ThemeVideoOpenButtonFgBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            Resources["ThemeValueBadgeBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(212, 222, 238) : Color.FromRgb(42, 43, 56));

            Color framePreviewBg = isLight ? Color.FromRgb(255, 255, 255) : Color.FromArgb(235, 28, 30, 38);
            Color framePreviewFg = isLight ? Color.FromRgb(15, 23, 42) : Color.FromRgb(241, 245, 255);
            Resources["ThemeFrameLabelPreviewBg"] = new SolidColorBrush(framePreviewBg);
            Resources["ThemeFrameLabelPreviewFg"] = new SolidColorBrush(framePreviewFg);
            Resources["ThemeTabNavBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(222, 230, 244) : Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
            Resources["ThemeLogContainerBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(250, 252, 255) : Color.FromArgb(0x0C, 0x00, 0x00, 0x00));
            Resources["ThemeActionBarBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(224, 232, 246) : Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
            Resources["ThemeActionBarBorderBrush"] = new SolidColorBrush(
                isLight ? Color.FromRgb(234, 179, 8) : Color.FromArgb(0x5A, warmAmber.R, warmAmber.G, warmAmber.B));
            Color sliderRedActive = isLight ? Color.FromRgb(220, 38, 38) : Color.FromRgb(239, 68, 68);
            Color sliderRedDisabled = isLight ? Color.FromRgb(248, 113, 113) : Color.FromRgb(252, 165, 165);
            Resources["ThemeSliderActiveBrush"] = new SolidColorBrush(sliderRedActive);
            Resources["ThemeSliderThumbBrush"] = new SolidColorBrush(sliderRedActive);
            Resources["ThemeSliderThumbStrokeBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            Resources["ThemeSliderDisabledBrush"] = new SolidColorBrush(sliderRedDisabled);

            Resources["ThemeOnAccentTextBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            Resources["ThemeComboPopupBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(255, 255, 255) : Color.FromRgb(30, 30, 48));
            Resources["ThemeComboItemHoverBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(214, 226, 246) : Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            Resources["ThemeTabHoverBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(210, 222, 242) : Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
            Resources["ThemeTabSelectedBackgroundBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(99, 102, 241) : Color.FromArgb(200, accentColor.R, accentColor.G, accentColor.B));
            Resources["ThemeVideoLogSegmentTrackBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(216, 226, 242) : Color.FromArgb(0x55, 24, 26, 34));
            Resources["ThemeComboSelectedItemBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(224, 231, 255) : Color.FromArgb(90, accentColor.R, accentColor.G, accentColor.B));
            
            Resources["ThemeActionExtractBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x50, 0x3B, 0x82, 0xF6) : Color.FromArgb(0x20, 0x5B, 0x8F, 0xF9));
            Resources["ThemeActionSubtitleBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x50, 0x63, 0x66, 0xF1) : Color.FromArgb(0x20, 0x7C, 0x6B, 0xF8));
            Resources["ThemeActionWatermarkBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x50, 0x0D, 0x94, 0x88) : Color.FromArgb(0x20, 0x14, 0xB8, 0xA6));
            Resources["ThemeActionConvertBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x50, 0x8B, 0x5C, 0xF6) : Color.FromArgb(0x20, 0xA7, 0x8B, 0xFA));
            Resources["ThemeActionTrimBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x50, 0xEF, 0x44, 0x44) : Color.FromArgb(0x20, 0xEF, 0x44, 0x44));
            Resources["ThemeActionSnapshotBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x50, 0xF5, 0x9E, 0x0B) : Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B));
            Resources["ThemeActionFolderVideoBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x50, 0x10, 0xB9, 0x81) : Color.FromArgb(0x20, 0x4A, 0xDE, 0x80));
            Resources["ThemeActionFolderFramesBrush"] = new SolidColorBrush(isLight ? Color.FromArgb(0x50, 0xF5, 0x9E, 0x0B) : Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B));

            // Secondary button chips
            Color secBgTop = isLight ? Color.FromRgb(220, 228, 242) : Color.FromArgb(37, 255, 255, 255);
            Resources["SecondaryButtonBackground"] = new SolidColorBrush(secBgTop);
            Resources["SecondaryButtonForeground"] = new SolidColorBrush(isLight ? Color.FromRgb(15, 23, 42) : Color.FromRgb(236, 236, 244));
            Resources["SecondaryButtonBorder"] = new SolidColorBrush(isLight ? Color.FromRgb(164, 180, 206) : Color.FromArgb(0x40, 255, 255, 255));

            var textPrimary = (Brush)Resources["ThemeTextPrimaryBrush"];
            var textSecondary = (Brush)Resources["ThemeTextSecondaryBrush"];
            SetForegroundIfExists("TimeCurrentText", textSecondary);
            SetForegroundIfExists("TimeTotalText", textSecondary);
            SetForegroundIfExists("SeekPerfText", textSecondary);
            SetForegroundIfExists("FrameInfoText", textPrimary);
            SetForegroundIfExists("VideoPathText", textSecondary);
            SetForegroundIfExists("CodecInfoText", textSecondary);
            SetForegroundIfExists("AudioSummaryText", textSecondary);
            SetForegroundIfExists("ConfigMissingSummaryText", textPrimary);
            SetForegroundIfExists("ProgressPercentText", textSecondary);
            SetForegroundIfExists("ElapsedTimeText", textSecondary);
            SetForegroundIfExists("EstimatedTimeText", textSecondary);
            TitleText.Foreground = textPrimary;
            if (IconView != null)
                IconView.Fill = textPrimary;
            UpdateHwBadgeUi();

            ThemeModeButton.Content = CreateThemeModeIcon(isLight ? "moon regular" : "sun-bright duotone-thin", isLight);
            SetTransportIcons();
            SyncUserControlRoundedClip();
            UpdateBottomBarGroupHighlight(Math.Max(0, TabNavList.SelectedIndex));
            ApplyThemeBrushes(GetTextBrush(_node.ColorKey));
        }

        private void UpdateHwBadgeUi()
        {
            if (HwBadge == null) return;

            var hw = (_node?.PreferredHwAccel ?? string.Empty).Trim().ToLowerInvariant();
            bool isCudaOrGpu = !string.IsNullOrEmpty(hw) && hw != "cpu" && hw != "none";

            if (isCudaOrGpu)
            {
                HwBadge.Background = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                var label = string.IsNullOrWhiteSpace(_node?.PreferredHwAccel) ? "CUDA" : _node.PreferredHwAccel.ToUpperInvariant();
                HwBadge.ToolTip = label;
            }
            else
            {
                HwBadge.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                HwBadge.ToolTip = "CPU";
            }
        }

        private void SetForegroundIfExists(string elementName, Brush brush)
        {
            if (FindName(elementName) is TextBlock tb)
            {
                tb.Foreground = brush;
            }
        }

        private static SvgViewboxEx CreateThemeModeIcon(string iconKey, bool isLightMode)
        {
            var iconConverter = new IconKeyToPathConverter();
            var iconUri = iconConverter.Convert(string.Empty, typeof(Uri), iconKey,
                System.Globalization.CultureInfo.CurrentCulture) as Uri;
            return new SvgViewboxEx
            {
                Width = 15,
                Height = 15,
                Source = iconUri!,
                Fill = isLightMode ? new SolidColorBrush(Color.FromRgb(56, 63, 74)) : new SolidColorBrush(Color.FromRgb(255, 219, 116))
            };
        }

        private void ToggleNodeZoom()
        {
            if (_node == null || _host == null || _node.Border == null) return;

            var border = _node.Border;
            var minW = border.MinWidth > 0 ? border.MinWidth : 540;
            var minH = border.MinHeight > 0 ? border.MinHeight : 340;

            // Collapse back to pre-expand frame.
            if (_isNodeZoomed)
            {
                var restoreX = _prevNodeX;
                var restoreY = _prevNodeY;
                var restoreW = _prevNodeWidth > 0 ? _prevNodeWidth : minW;
                var restoreH = _prevNodeHeight > 0 ? _prevNodeHeight : minH;

                _node.X = restoreX;
                _node.Y = restoreY;
                _node.Width = Math.Max(minW, restoreW);
                _node.Height = Math.Max(minH, restoreH);
                border.Width = _node.Width;
                border.Height = _node.Height;
                _host.UpdateNodePosition(_node, restoreX, restoreY);
                _host.UpdateCanvasSize();
                if (_host is WorkflowEditorWindow win)
                    win.SetViewportExpandedUiHidden(false);

                // Khôi phục ZIndex ban đầu khi thu nhỏ node
                Canvas.SetZIndex(border, _prevZIndex);
                Panel.SetZIndex(border, _prevZIndex);

                _isNodeZoomed = false;
                ToggleNodeSizeButton.Content = CreateTransportIcon("expand utility-fill-semibold");
                RefreshLargeNodeUiScale();
                return;
            }

            // Expand to current visible workflow viewport (same behavior idea as HtmlUi node).
            if (_host is WorkflowEditorWindow winExpand)
                winExpand.SetViewportExpandedUiHidden(true);

            // Lưu ZIndex và vị trí/kích thước ban đầu trước khi phóng to
            _prevZIndex = Canvas.GetZIndex(border);

            _prevNodeX = _node.X;
            _prevNodeY = _node.Y;
            _prevNodeWidth = _node.Width;
            _prevNodeHeight = _node.Height;

            // Đẩy ZIndex lên cao nhất (999999) để đè lên các node và control khác trên canvas
            Canvas.SetZIndex(border, 999999);
            Panel.SetZIndex(border, 999999);

            var vp = GetWorkflowViewportCanvasRect();
            if (vp.IsEmpty || vp.Width < 1 || vp.Height < 1)
            {
                _node.Width = Math.Max(1366, _node.Width);
                _node.Height = Math.Max(768, _node.Height);
                border.Width = _node.Width;
                border.Height = _node.Height;
                _isNodeZoomed = true;
                ToggleNodeSizeButton.Content = CreateTransportIcon("compress utility-fill-semibold");
                RefreshLargeNodeUiScale();
                return;
            }

            var nextW = Math.Max(minW, vp.Width);
            var nextH = Math.Max(minH, vp.Height);
            _node.X = vp.Left;
            _node.Y = vp.Top;
            _node.Width = nextW;
            _node.Height = nextH;
            border.Width = nextW;
            border.Height = nextH;
            _host.UpdateNodePosition(_node, vp.Left, vp.Top);
            _host.UpdateCanvasSize();

            _isNodeZoomed = true;
            ToggleNodeSizeButton.Content = CreateTransportIcon("compress utility-fill-semibold");
            RefreshLargeNodeUiScale();
        }

        private void ToggleVideoPlayerExpand()
        {
            _isVideoPlayerExpanded = !_isVideoPlayerExpanded;
            UpdateVideoPlayerExpandUi();
            UpdatePreviewAspectRatio();
        }

        private void UpdateVideoPlayerExpandUi()
        {
            if (ToggleVideoPlayerExpandButton == null) return;

            if (_isVideoPlayerExpanded)
            {
                ToggleVideoPlayerExpandButton.ToolTip = "Thu nhỏ khung video (Hiện nhật ký)";
                ToggleVideoPlayerExpandButton.Content = CreateTransportIcon("compress utility-fill-semibold");
            }
            else
            {
                ToggleVideoPlayerExpandButton.ToolTip = "Phóng to khung video (Ẩn nhật ký)";
                ToggleVideoPlayerExpandButton.Content = CreateTransportIcon("expand utility-fill-semibold");
            }
        }

        private Rect GetWorkflowViewportCanvasRect()
        {
            if (_host == null) return Rect.Empty;
            var sv = _host.ScrollViewer;
            if (sv == null) return Rect.Empty;
            try { sv.UpdateLayout(); } catch { /* ignore */ }

            var scrollX = sv.HorizontalOffset;
            var scrollY = sv.VerticalOffset;
            var viewportW = sv.ViewportWidth > 1 ? sv.ViewportWidth : sv.ActualWidth;
            var viewportH = sv.ViewportHeight > 1 ? sv.ViewportHeight : sv.ActualHeight;
            if (viewportW < 1 || viewportH < 1) return Rect.Empty;

            var z = _host.ScaleTransform?.ScaleX ?? 1.0;
            if (z <= 0.0001) z = 1.0;
            var tx = _host.TranslateTransform?.X ?? 0;
            var ty = _host.TranslateTransform?.Y ?? 0;

            var canvasLeft = (scrollX - tx) / z;
            var canvasTop = (scrollY - ty) / z;
            var canvasW = viewportW / z;
            var canvasH = viewportH / z;
            if (double.IsNaN(canvasLeft) || double.IsInfinity(canvasLeft) ||
                double.IsNaN(canvasTop) || double.IsInfinity(canvasTop))
                return Rect.Empty;

            return new Rect(canvasLeft, canvasTop, canvasW, canvasH);
        }

        private void RefreshLargeNodeUiScale()
        {
            if (RootContentGrid == null) return;

            var owner = Window.GetWindow(this);
            bool isWidget = owner != null && owner.GetType().Name == "FloatingWidgetWindow";

            if (isWidget)
            {
                RootContentGrid.LayoutTransform = Transform.Identity;
                return;
            }

            if (_isNodeZoomed)
            {
                // Khi phóng to vừa màn hình (Zoom mode): Triệt tiêu canvas zoom scale z (invZ = 1.0 / z)
                // để UI luôn hiển thị đúng tỉ lệ 1.0x (100% native resolution) trên màn hình thực tế,
                // không phụ thuộc vào tỉ lệ zoom/pan của canvas.
                double z = _host?.ScaleTransform?.ScaleX ?? 1.0;
                if (z <= 0.0001) z = 1.0;

                double invZ = 1.0 / z;
                RootContentGrid.LayoutTransform = Math.Abs(invZ - 1.0) < 0.001 ? Transform.Identity : new ScaleTransform(invZ, invZ);
            }
            else
            {
                // Khi ở chế độ canvas: Tỉ lệ UI lấy baseline mặc định 1366px × 768px.
                // Khi node ở kích thước 1366x768 -> scale = 1.0x (Hiển thị nét, đẹp, bố cục gọn gàng chuẩn XAML gốc).
                double nodeW = _node != null && _node.Width > 0 ? _node.Width : (ActualWidth > 0 ? ActualWidth : 1366);
                double nodeH = _node != null && _node.Height > 0 ? _node.Height : (ActualHeight > 0 ? ActualHeight : 768);

                // Baseline 1366px × 768px
                double scaleW = nodeW / 1366.0;
                double scaleH = nodeH / 768.0;

                // Dùng Max(scaleW, scaleH) để khi kéo node to hơn 1366x768 thì UI scale tăng mượt mà tương ứng
                double scaleDimension = Math.Max(scaleW, scaleH);

                // Sub-linear curved scale cho canvas drag
                double scaleVal = Math.Clamp(Math.Pow(scaleDimension, 0.65), 0.6, 3.0);

                RootContentGrid.LayoutTransform = Math.Abs(scaleVal - 1.0) < 0.01 ? Transform.Identity : new ScaleTransform(scaleVal, scaleVal);
            }
        }

        private void EmitAutoFitSizeSuggestion()
        {
            var naturalW = PreviewMedia.NaturalVideoWidth;
            var naturalH = PreviewMedia.NaturalVideoHeight;
            if (naturalW <= 0 || naturalH <= 0) return;
            var aspect = naturalW / (double)naturalH;
            if (aspect <= 0 || double.IsNaN(aspect) || double.IsInfinity(aspect)) return;

            var previewHeight = Math.Clamp(naturalH, MinPreviewHeight, Math.Min(MaxPreviewHeight, MaxAutoFitNodeHeight - NonPreviewContentHeight));
            var previewWidth = previewHeight * aspect;
            var suggestedWidth = Math.Clamp(previewWidth + HorizontalPadding, MinAutoFitNodeWidth, MaxAutoFitNodeWidth);
            var suggestedHeight = Math.Clamp(previewHeight + NonPreviewContentHeight, MinAutoFitNodeHeight, MaxAutoFitNodeHeight);
            SuggestedNodeSizeReady?.Invoke(suggestedWidth, suggestedHeight);
        }

        private void ApplyPortraitVideoLogLayout()
        {
            if (PortraitVideoLogTabControl != null)
                PortraitVideoLogTabControl.Visibility = Visibility.Collapsed;
            if (VideoContainerGrid != null)
                VideoContainerGrid.Visibility = Visibility.Visible;
            _portraitVideoLogLayout = false;

            UpdatePreviewAspectRatio();
        }

        /// <summary>Đảm bảo Video và Log luôn được xếp cùng 1 cột duy nhất.</summary>
        private void UpdateVideoLogColumnLayout()
        {
            if (PortraitVideoLogTabControl != null)
                PortraitVideoLogTabControl.Visibility = Visibility.Collapsed;
            if (VideoContainerGrid != null)
                VideoContainerGrid.Visibility = Visibility.Visible;
            _portraitVideoLogLayout = false;

            UpdatePreviewAspectRatio();
        }

        private FrameworkElement GetVideoColumnRenderRoot()
        {
            return (_portraitVideoLogLayout && PreviewColumnShellGrid != null) ? PreviewColumnShellGrid : VideoContainerGrid;
        }

        private void UpdatePreviewAspectRatio()
        {
            if (PreviewContainerBorder == null || VideoContainerGrid == null) return;

            var outerH = PreviewContainerBorder.ActualHeight;
            if (outerH <= 0) return;

            UpdateAdaptivePreviewRows(outerH);

            if (PreviewMedia.Source != null)
            {
                VideoViewbox.Visibility = Visibility.Visible;
                PreviewPlaceholder.Visibility = Visibility.Collapsed;
            }
            else
            {
                VideoViewbox.Visibility = Visibility.Collapsed;
                PreviewPlaceholder.Visibility = Visibility.Visible;
            }

            SyncVideoViewportClip();
            UpdateOverlayCanvasBounds();
            UpdateWatermarkPreviewUi();
        }

        private void SetAspectRatio(double w, double h, bool auto)
        {
            _selectedAspectW = w;
            _selectedAspectH = h;
            _aspectAuto = auto;
            ApplyAspectRatioToMedia();
        }

        private void ApplyAspectRatioToMedia()
        {
            double targetW;
            double targetH;
            if (_aspectAuto)
            {
                var natW = PreviewMedia.NaturalVideoWidth > 0 ? PreviewMedia.NaturalVideoWidth : 1280;
                var natH = PreviewMedia.NaturalVideoHeight > 0 ? PreviewMedia.NaturalVideoHeight : 720;
                targetW = natW;
                targetH = natH;
            }
            else if (_selectedAspectW > 0 && _selectedAspectH > 0)
            {
                var baseW = 1280.0;
                targetW = baseW;
                targetH = baseW * (_selectedAspectH / _selectedAspectW);
            }
            else
            {
                targetW = 1280;
                targetH = 720;
            }

            var qualityCap = GetConfiguredPreviewMaxHeight();
            if (qualityCap.HasValue && qualityCap.Value > 0 && targetH > qualityCap.Value)
            {
                var scale = qualityCap.Value / targetH;
                targetW *= scale;
                targetH = qualityCap.Value;
            }

            PreviewMedia.Width = targetW;
            PreviewMedia.Height = targetH;

            UpdatePreviewAspectRatio();
        }

        private void UpdateAdaptivePreviewRows(double containerHeight)
        {
            if (VideoContainerGrid == null || VideoContainerGrid.RowDefinitions.Count < 4) return;

            var rowVideoPlayerCard = VideoContainerGrid.RowDefinitions[1];
            var rowLog = VideoContainerGrid.RowDefinitions[3];

            // Determine actual video aspect ratio
            double natW = 1280;
            double natH = 720;

            if (_aspectAuto)
            {
                if (PreviewMedia != null && PreviewMedia.NaturalVideoWidth > 0 && PreviewMedia.NaturalVideoHeight > 0)
                {
                    natW = PreviewMedia.NaturalVideoWidth;
                    natH = PreviewMedia.NaturalVideoHeight;
                }
            }
            else if (_selectedAspectW > 0 && _selectedAspectH > 0)
            {
                natW = _selectedAspectW;
                natH = _selectedAspectH;
            }

            var isPortrait = natH > natW;
            var aspect = natW / Math.Max(1.0, natH);

            var outerW = PreviewContainerBorder != null && PreviewContainerBorder.ActualWidth > 0
                ? PreviewContainerBorder.ActualWidth
                : 480.0;
            var cardWidth = Math.Max(100.0, outerW - 20.0);

            double timelineH = 108.0;
            double minLogH = 120.0;
            double maxViewportH = Math.Max(120.0, containerHeight - timelineH - minLogH - 30.0);

            double targetViewportH;
            if (_isVideoPlayerExpanded)
            {
                if (LogPanelBorder != null)
                {
                    LogPanelBorder.Visibility = Visibility.Collapsed;
                }
                rowLog.Height = new GridLength(0);

                var availableContainerViewportH = Math.Max(160.0, containerHeight - timelineH - 24.0);
                var widthConstrainedViewportH = cardWidth / aspect;

                targetViewportH = Math.Min(availableContainerViewportH, widthConstrainedViewportH);
                targetViewportH = Math.Max(160.0, targetViewportH);

                if (VideoPlayerCardBorder != null)
                {
                    VideoPlayerCardBorder.Margin = new Thickness(10, 10, 10, 10);
                    VideoPlayerCardBorder.CornerRadius = new CornerRadius(10);
                }
            }
            else
            {
                if (LogPanelBorder != null)
                {
                    LogPanelBorder.Visibility = Visibility.Visible;
                }
                rowLog.Height = new GridLength(1, GridUnitType.Star);

                if (isPortrait)
                {
                    // Vertical/portrait video: Fix height proportionally (max 320px or 42% container height) so log takes all remaining height
                    var maxPortraitViewportH = Math.Min(320.0, Math.Max(160.0, containerHeight * 0.42));
                    targetViewportH = Math.Clamp(cardWidth / aspect, 160.0, maxPortraitViewportH);
                }
                else
                {
                    // Horizontal/landscape video: Fix width to container width, calculate exact height matching aspect ratio
                    var idealViewportH = cardWidth / aspect;
                    var clampMin = Math.Min(140.0, maxViewportH);
                    targetViewportH = Math.Clamp(idealViewportH, clampMin, maxViewportH);
                }

                if (VideoPlayerCardBorder != null)
                {
                    VideoPlayerCardBorder.Margin = new Thickness(10, 10, 10, 0);
                    VideoPlayerCardBorder.CornerRadius = new CornerRadius(0, 0, 10, 10);
                }
            }

            if (VideoViewportClipBorder != null)
            {
                VideoViewportClipBorder.Height = targetViewportH;
            }

            rowVideoPlayerCard.Height = GridLength.Auto;
        }

        private void RefreshOutputsSummaryUi()
        {
            SetTextIfExists("OutputModeSummaryText", _node.OutputBase64 ? "Base64" : "File");
            if (FindName("OutputModeBadgeText") is TextBlock badgeText && FindName("OutputModeBadgeBorder") is Border badgeBorder)
            {
                badgeText.Text = _node.OutputBase64 ? "Chế độ: Base64" : "Chế độ: Đường dẫn (Link)";
                badgeText.Foreground = _node.OutputBase64
                    ? new SolidColorBrush(Color.FromRgb(129, 140, 248))
                    : new SolidColorBrush(Color.FromRgb(56, 189, 248));
                badgeBorder.Background = _node.OutputBase64
                    ? new SolidColorBrush(Color.FromArgb(0x33, 0x63, 0x66, 0xF1))
                    : new SolidColorBrush(Color.FromArgb(0x33, 0x0E, 0xA5, 0xE9));
                badgeBorder.BorderBrush = _node.OutputBase64
                    ? new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1))
                    : new SolidColorBrush(Color.FromRgb(0x0E, 0xA5, 0xE9));
            }
            SetTextIfExists("OutputFormatSummaryText", (OutputFormatCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "MP4 (H.264)");
            SetTextIfExists("OutputAudioSummaryText", $"{_node.AudioTracks.Count} track | codec: {_node.AudioCodec} | bitrate: {_node.AudioBitrate}");
            var estimatedFrames = Math.Round(GetNaturalDurationSeconds() * (_node.ExtractAllFrames ? _node.SourceFps : _node.ExtractFps));
            SetTextIfExists("OutputEstimatedFramesText", $"{estimatedFrames:0} frame");

            var outputVideoPath = (_node.UseDialogVideoConfig
                ? (_node.DefaultOutputVideoPath ?? string.Empty)
                : (OutputPathText.Text ?? string.Empty)).Trim();
            if (string.IsNullOrWhiteSpace(outputVideoPath))
                outputVideoPath = (DefaultOutputVideoPathText.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputVideoPath))
                outputVideoPath = (_node.OutputPathOverride ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(outputVideoPath))
                _node.OutputPathOverride = outputVideoPath;

            if (string.IsNullOrWhiteSpace(outputVideoPath))
            {
                OutputVideoPathText.Text = GetDefaultVideoOutputFolder();
                OpenOutputVideoButton.IsEnabled = false;
            }
            else
            {
                OutputVideoPathText.Text = outputVideoPath;
                var outputDir = System.IO.Path.GetDirectoryName(outputVideoPath) ?? string.Empty;
                var ready = File.Exists(outputVideoPath) || Directory.Exists(outputDir);
                OpenOutputVideoButton.IsEnabled = ready;
            }

            var framesDir = (_node.UseDialogVideoConfig
                ? (_node.FrameOutputFolderPath ?? string.Empty)
                : (FrameOutputFolderText.Text ?? string.Empty)).Trim();
            if (string.IsNullOrWhiteSpace(framesDir))
                framesDir = GetDefaultFrameOutputFolder();
            if (string.IsNullOrWhiteSpace(framesDir))
            {
                OutputFramesFolderText.Text = "Chưa xác định thư mục frame";
                OpenFramesFolderButton.IsEnabled = false;
                OpenFramesFolderButton.Visibility = Visibility.Visible;
            }
            else
            {
                OutputFramesFolderText.Text = framesDir;
                var framesReady = Directory.Exists(framesDir) || !_node.OutputBase64;
                OpenFramesFolderButton.IsEnabled = framesReady;
                OpenFramesFolderButton.Visibility = Visibility.Visible;
            }

            var okVideo = !string.IsNullOrWhiteSpace(_node.VideoPath);
            var okOutput = !string.IsNullOrWhiteSpace(_node.OutputPathOverride);
            var okSubtitle = !_node.BurnSubtitleEnabled || !string.IsNullOrWhiteSpace(_node.SubtitlePath);
            var okWatermark = !_node.WatermarkEnabled || !string.IsNullOrWhiteSpace(_node.WatermarkImagePath);
            var okTextOverlay = !_node.TextOverlayEnabled || !string.IsNullOrWhiteSpace(_node.OverlayText);

            SetConfigCheck(ConfigCheckVideoText, okVideo, "Đã chọn video nguồn", "Thiếu video nguồn");
            SetConfigCheck(ConfigCheckOutputText, okOutput, "Đã đặt đường dẫn video đầu ra", "Chưa đặt đường dẫn video đầu ra");
            SetConfigCheck(ConfigCheckSubtitleText, okSubtitle, "Subtitle hợp lệ", "Đã bật burn subtitle nhưng chưa chọn file subtitle");
            SetConfigCheck(ConfigCheckWatermarkText, okWatermark, "Watermark hợp lệ", "Đã bật watermark nhưng chưa chọn ảnh");
            SetConfigCheck(ConfigCheckTextOverlayText, okTextOverlay, "Text overlay hợp lệ", "Đã bật chèn chữ nhưng nội dung chữ đang trống");

            var missingCount = new[] { okVideo, okOutput, okSubtitle, okWatermark, okTextOverlay }.Count(x => !x);
            if (missingCount == 0)
            {
                SetTextStyleIfExists("ConfigMissingSummaryText", "✓ Tất cả cấu hình đã đầy đủ", new SolidColorBrush(Color.FromRgb(74, 222, 128)));
            }
            else
            {
                SetTextStyleIfExists("ConfigMissingSummaryText", $"⚠ Còn thiếu {missingCount} cấu hình cần thiết", new SolidColorBrush(Color.FromRgb(248, 113, 113)));
            }
        }

        private string GetDefaultFrameOutputFolder()
        {
            var downloadsRoot = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "flow-frame");
            return System.IO.Path.Combine(downloadsRoot, GetVideoFileNameStem());
        }

        private string GetDefaultVideoOutputFolder()
        {
            var downloadsRoot = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "flow-video");
            return System.IO.Path.Combine(downloadsRoot, GetVideoFileNameStem());
        }

        private string GetDefaultAudioOutputFolder()
        {
            var downloadsRoot = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "flow-audio");
            return System.IO.Path.Combine(downloadsRoot, GetVideoFileNameStem());
        }

        private void OpenDefaultVideoFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = GetDefaultVideoOutputFolder();
            try
            {
                Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start("explorer.exe", folder);
                AppendLog($"📁 Đã tạo & mở thư mục xuất video mặc định: {folder}");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi tạo thư mục: {ex.Message}");
            }
        }

        private void OpenDefaultFrameFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = GetDefaultFrameOutputFolder();
            try
            {
                Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start("explorer.exe", folder);
                AppendLog($"📁 Đã tạo & mở thư mục xuất frame mặc định: {folder}");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi tạo thư mục: {ex.Message}");
            }
        }

        private void OpenDefaultAudioFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = GetDefaultAudioOutputFolder();
            try
            {
                Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start("explorer.exe", folder);
                AppendLog($"📁 Đã tạo & mở thư mục xuất audio mặc định: {folder}");
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi tạo thư mục: {ex.Message}");
            }
        }

        public string GetCurrentVideoOutputFolder()
        {
            var overridePath = (_node.OutputPathOverride ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                try
                {
                    var dir = System.IO.Path.GetDirectoryName(overridePath);
                    if (!string.IsNullOrWhiteSpace(dir)) return dir;
                }
                catch { }
            }
            var defaultPath = (_node.DefaultOutputVideoPath ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(defaultPath))
            {
                try
                {
                    var dir = System.IO.Path.GetDirectoryName(defaultPath);
                    if (!string.IsNullOrWhiteSpace(dir)) return dir;
                    return defaultPath;
                }
                catch { }
            }
            return GetDefaultVideoOutputFolder();
        }

        private string GetVideoFileNameStem()
        {
            var source = (_node.VideoPath ?? string.Empty).Trim();
            var stem = string.Empty;
            try
            {
                stem = System.IO.Path.GetFileNameWithoutExtension(source);
            }
            catch
            {
                stem = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(stem))
                stem = "video";

            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                stem = stem.Replace(c, '_');

            return stem;
        }

        private void UpdateFrameExtractionPreview()
        {
            var duration = Math.Max(0.1, GetNaturalDurationSeconds());
            var sourceFps = _node.SourceFps > 0 ? _node.SourceFps : 24;
            var totalFramesInVideo = Math.Max(1, (int)Math.Floor(duration * sourceFps));

            if (FpsSlider != null) FpsSlider.Maximum = totalFramesInVideo;
            if (ExtractFpsRateSlider != null) ExtractFpsRateSlider.Maximum = Math.Max(1, (int)Math.Round(sourceFps));

            if (_node.ExtractAllFrames)
            {
                if (EstFramePerSecText != null) EstFramePerSecText.Text = $"{sourceFps:0.##}";
                if (EstimatedFrameCountText != null) EstimatedFrameCountText.Text = $"{totalFramesInVideo:N0}";
                if (EstFrameIntervalText != null) EstFrameIntervalText.Text = $"{(1000.0 / sourceFps):0.#} ms";
                SetTextIfExists("FrameIndexPreviewText", $"Tất cả frame: {totalFramesInVideo:N0} frame trong {duration:0.#}s ({sourceFps:0.##} fps)");
                return;
            }

            if (_node.ExtractByFpsEnabled)
            {
                var rate = Math.Clamp(_node.ExtractFps, 1, Math.Max(1, sourceFps));
                _node.ExtractFps = rate;
                var estTotal = Math.Max(1, (int)Math.Round(duration * rate));

                if (EstFramePerSecText != null) EstFramePerSecText.Text = $"{rate:0.##} fps";
                if (EstimatedFrameCountText != null) EstimatedFrameCountText.Text = $"{estTotal:N0}";

                var intervalMs = 1000.0 / Math.Max(0.001, rate);
                if (EstFrameIntervalText != null)
                    EstFrameIntervalText.Text = intervalMs >= 1000 ? $"{intervalMs / 1000.0:0.##} s" : $"{intervalMs:0.#} ms";

                _isFrameControlSync = true;
                try
                {
                    if (ExtractFpsRateSlider != null && (int)Math.Round(ExtractFpsRateSlider.Value) != (int)rate)
                        ExtractFpsRateSlider.Value = rate;
                    if (ExtractFpsRateBox != null && ExtractFpsRateBox.Text != ((int)rate).ToString())
                        ExtractFpsRateBox.Text = ((int)rate).ToString();
                    if (ExtractFpsRateLabel != null)
                        ExtractFpsRateLabel.Text = $"{rate:0.#} frame/s";
                }
                finally
                {
                    _isFrameControlSync = false;
                }

                var offsetSec = 0.5 / rate;
                SetTextIfExists("FrameIndexPreviewText", $"Tách {rate:0.#} frame/giây trong {duration:0.#}s video (~{estTotal} frame | Khoảng cách đều: ~{intervalMs:0.#}ms, căn giữa mốc đầu {offsetSec:0.###}s)");
            }
            else
            {
                var targetCount = Math.Clamp(_node.ExtractFrameCount, 1, totalFramesInVideo);
                _node.ExtractFrameCount = targetCount;

                if (EstimatedFrameCountText != null) EstimatedFrameCountText.Text = $"{targetCount:N0}";
                var calcFps = (double)targetCount / duration;
                _node.ExtractFps = calcFps;
                if (EstFramePerSecText != null) EstFramePerSecText.Text = $"{calcFps:0.##} fps";

                _isFrameControlSync = true;
                try
                {
                    if (FpsSlider != null && (int)Math.Round(FpsSlider.Value) != targetCount)
                        FpsSlider.Value = targetCount;
                    if (FpsValueBox != null && FpsValueBox.Text != targetCount.ToString())
                        FpsValueBox.Text = targetCount.ToString();
                    if (FpsValueText != null)
                        FpsValueText.Text = $"{targetCount}";
                }
                finally
                {
                    _isFrameControlSync = false;
                }

                var extractFpsSafe = Math.Max(0.001, calcFps);
                var intervalMs = 1000.0 / extractFpsSafe;
                if (EstFrameIntervalText != null)
                    EstFrameIntervalText.Text = intervalMs >= 1000 ? $"{intervalMs / 1000.0:0.##} s" : $"{intervalMs:0.#} ms";

                var stepSec = duration / Math.Max(1, targetCount);
                SetTextIfExists("FrameIndexPreviewText", $"Tách đúng {targetCount} frame trong {duration:0.#}s video (~1 frame mỗi {stepSec:0.##}s | FPS gốc: {sourceFps:0.##})");
            }
        }

        private void SetTextIfExists(string elementName, string text)
        {
            if (FindName(elementName) is TextBlock tb)
            {
                tb.Text = text;
            }
        }

        private static void SelectComboBoxItemByTag(ComboBox comboBox, string? tag)
        {
            if (comboBox == null || string.IsNullOrWhiteSpace(tag)) return;
            for (var i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private static void SelectComboBoxItemByContent(ComboBox comboBox, string? content)
        {
            if (comboBox == null || string.IsNullOrWhiteSpace(content)) return;
            for (var i = 0; i < comboBox.Items.Count; i++)
            {
                if (comboBox.Items[i] is ComboBoxItem item &&
                    string.Equals(item.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SetTextStyleIfExists(string elementName, string text, Brush foreground)
        {
            if (FindName(elementName) is TextBlock tb)
            {
                tb.Text = text;
                tb.Foreground = foreground;
            }
        }

        private static void SetConfigCheck(TextBlock target, bool ok, string okText, string warningText)
        {
            target.Text = ok ? $"✓ {okText}" : $"⚠ {warningText}";
            target.Foreground = ok ? new SolidColorBrush(Color.FromRgb(74, 222, 128)) : new SolidColorBrush(Color.FromRgb(248, 113, 113));
            target.FontWeight = ok ? FontWeights.Normal : FontWeights.SemiBold;
        }

        private static void OpenPathFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            try
            {
                if (text.IndexOfAny(new[] { '*', '?' }) >= 0)
                    return;
                var extension = System.IO.Path.GetExtension(text);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    var fileParent = System.IO.Path.GetDirectoryName(text);
                    if (!string.IsNullOrWhiteSpace(fileParent) && !Directory.Exists(fileParent))
                        Directory.CreateDirectory(fileParent);
                }
                else if (!Directory.Exists(text))
                {
                    Directory.CreateDirectory(text);
                }
            }
            catch
            {
                // best-effort for opening path
            }

            if (Directory.Exists(text) || File.Exists(text))
            {
                Process.Start(new ProcessStartInfo { FileName = text, UseShellExecute = true });
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(text);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                Process.Start(new ProcessStartInfo { FileName = parent, UseShellExecute = true });
            }
        }

        private static void EnsureParentDirectoryExists(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;
            try
            {
                var parent = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
                    Directory.CreateDirectory(parent);
            }
            catch
            {
                // best-effort
            }
        }

        private static void EnsureDirectoryExists(string? directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath)) return;
            try
            {
                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);
            }
            catch
            {
                // best-effort
            }
        }

        private static string FormatTime(TimeSpan value)
            => value.TotalHours >= 1 ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}" : $"{value.Minutes:00}:{value.Seconds:00}";

        public void ResetGradingTabToDefaults()
        {
            _previewEffectTemporarilyDisabled = false;
            _node.Brightness = 0;
            _node.Contrast = 1.0;
            _node.Saturation = 1.0;
            _node.Hue = 0;
            _node.Gamma = 1.0;

            _node.SharpenEnabled = false;
            _node.SharpenStrength = 1.0;

            _node.DenoiseEnabled = false;
            _node.DenoiseStrength = 3.0;

            _node.BlurEnabled = false;
            _node.BlurRadius = 3.0;

            _node.RotationDegrees = 0;
            _node.FlipH = false;
            _node.FlipV = false;

            _node.StabilizeEnabled = false;
            _node.SpeedFactor = 1.0;

            SyncControlValuesFromModel();
            ApplyPreviewColorTransform();
            ApplyPreviewTransformEffects();
            AppendLog("🔄 Đã đặt lại mặc định các thông số bộ lọc màu.");
        }

        public void ResetFiltersTabToDefaults()
        {
            _node.WatermarkEnabled = false;
            _node.WatermarkImagePath = string.Empty;

            _node.TextOverlayEnabled = false;
            _node.OverlayText = string.Empty;

            _node.FrameLabelEnabled = false;
            _node.FrameLabelTemplate = "Frame {index} - {time}";
            _node.FrameLabelX = 0;
            _node.FrameLabelY = 0;
            _node.FrameLabelFontSize = 18;
            _node.FrameLabelTimeFormat = "HHMMSS";
            _node.FrameLabelPaddingLeft = 10;
            _node.FrameLabelPaddingTop = 6;
            _node.FrameLabelPaddingRight = 10;
            _node.FrameLabelPaddingBottom = 6;
            _node.FrameLabelTextColor = "black";
            _node.FrameLabelBackgroundColor = "white";
            _node.FrameLabelDebugSamplesEnabled = false;

            _node.Overlays.Clear();

            SyncControlValuesFromModel();
            UpdateWatermarkPreviewUi();
            UpdateFrameLabelPreviewUi();
            AppendLog("🔄 Đã đặt lại cài đặt tab chèn ảnh/nhãn/overlay.");
        }

        public void UpdatePreviewAudioVolume()
        {
            try
            {
                if (_isDspAudioPreviewActive && _audioDspPlayer != null)
                {
                    if (PreviewMedia != null)
                    {
                        PreviewMedia.IsMuted = true;
                        PreviewMedia.Volume = 0;
                    }
                    var masterVol = Math.Clamp(_node.PreviewVolume, 0.0, 1.0);
                    var sourceVolFactor = Math.Clamp(_node.SourceAudioVolumePercent / 100.0, 0.0, 3.0);
                    _audioDspPlayer.Volume = Math.Clamp(masterVol * sourceVolFactor, 0.0, 1.0);
                    if (_node.AudioSpeedFactor >= 0.1 && _node.AudioSpeedFactor <= 4.0)
                    {
                        _audioDspPlayer.SpeedRatio = _node.AudioSpeedFactor;
                    }
                }
                else if (PreviewMedia != null)
                {
                    var isMuted = !_node.SourceAudioEnabled || _node.SourceAudioVolumePercent <= 0 || _isMuted || _node.PreviewVolume <= 0;
                    PreviewMedia.IsMuted = isMuted;
                    if (!isMuted)
                    {
                        var masterVol = Math.Clamp(_node.PreviewVolume, 0.0, 1.0);
                        var sourceVolFactor = Math.Clamp(_node.SourceAudioVolumePercent / 100.0, 0.0, 3.0);
                        PreviewMedia.Volume = Math.Clamp(masterVol * sourceVolFactor, 0.0, 1.0);
                    }
                    else
                    {
                        PreviewMedia.Volume = 0;
                    }

                    if (PreviewMedia.SpeedRatio != _node.AudioSpeedFactor && _node.AudioSpeedFactor >= 0.1 && _node.AudioSpeedFactor <= 4.0)
                    {
                        PreviewMedia.SpeedRatio = _node.AudioSpeedFactor;
                    }
                }
            }
            catch { /* best effort */ }
        }

        public void ResetAudioTabToDefaults()
        {
            _node.SourceAudioEnabled = true;
            _node.SourceAudioVolumePercent = 100.0;
            _node.AudioSpeedFactor = 1.0;
            _node.AudioTrimEnabled = false;
            _node.AudioTrimStartSec = 0.0;
            _node.AudioTrimEndSec = 0.0;
            _node.AudioEqPreset = "neutral";
            _node.AudioBassGain = 0.0;
            _node.AudioTrebleGain = 0.0;
            _node.AudioFadeInSec = 0.0;
            _node.AudioFadeOutSec = 0.0;
            _node.AudioNormalizeEnabled = false;
            _node.AudioTargetLufs = -14.0;
            _node.AudioDenoiseEnabled = false;
            _node.AudioHighpassFilter = false;
            _node.AudioLowpassFilter = false;
            _node.AudioExportFormat = "mp3";
            _node.AudioExportBitrate = "320k";
            _node.AudioExportSampleRate = "48000";
            _node.AudioExportChannels = "stereo";
            _node.AudioTracks.Clear();

            SyncControlValuesFromModel();
            AppendLog("🔄 Đã đặt lại cài đặt âm thanh về mặc định.");
        }

        public void ResetTrimConcatTabToDefaults()
        {
            _node.TrimEnabled = false;
            _node.TrimStartSec = 0;
            _node.TrimEndSec = 0;
            _node.ConcatEnabled = false;
            _node.ConcatVideos.Clear();

            SyncControlValuesFromModel();
            AppendLog("🔄 Đã đặt lại cài đặt Cắt ghép video về mặc định.");
        }

        public void ResetSettingsTabToDefaults()
        {
            _node.OutputFormat = "mp4_h264";
            _node.EncoderPreset = "medium";
            _node.Crf = 23;
            _node.TwoPassEnabled = false;

            _node.AudioCodec = "aac";
            _node.AudioBitrate = "192k";

            _frameResizeScale = 1.0;
            _node.FrameResizeScale = 1.0;
            _node.ResolutionScale = 1.0;
            _node.OutputPathOverride = string.Empty;

            SyncControlValuesFromModel();
            AppendLog("🔄 Đã đặt lại cài đặt hệ thống & cấu hình xuất file về mặc định.");
        }

        public void SaveSettingsTabConfig()
        {
            SyncRuntimeConfigFromUi();
            _node.RaisePropertyChanged(nameof(VideoProcessingNode.FrameOutputFolderPath));
            _node.RaisePropertyChanged(nameof(VideoProcessingNode.DefaultOutputVideoPath));
            _node.RaisePropertyChanged(nameof(VideoProcessingNode.AudioOutputFolderPath));
            _node.RaisePropertyChanged(nameof(VideoProcessingNode.UseDialogVideoConfig));
            AppendLog("💾 Đã lưu cấu hình cài đặt node thành công!");
        }

        public async Task LoadTrimConcatToPreviewAsync()
        {
            var currentVideo = _node.VideoPath;
            if (string.IsNullOrWhiteSpace(currentVideo) && !_node.ConcatEnabled)
            {
                AppendLog("⚠️ Chưa có video đầu vào hoặc danh sách video ghép.");
                return;
            }

            SwitchToLogView();
            AppendLog("🎬 [XEM TRƯỚC CẮT/GHÉP] Đang xử lý tạo file video cắt/ghép để phát thử...");

            var tempRoot = Path.Combine(Path.GetTempPath(), "FlowMy_VideoProcessing");
            Directory.CreateDirectory(tempRoot);

            var videoInput = currentVideo ?? string.Empty;

            try
            {
                if (_node.ConcatEnabled && _node.ConcatVideos.Count > 0)
                {
                    var concatList = new List<string>();
                    if (!string.IsNullOrWhiteSpace(videoInput) && File.Exists(videoInput)) concatList.Add(videoInput);
                    foreach (var item in _node.ConcatVideos)
                    {
                        if (!string.IsNullOrWhiteSpace(item.SourcePath) && File.Exists(item.SourcePath))
                            concatList.Add(item.SourcePath);
                    }

                    if (concatList.Count > 0)
                    {
                        var concatTxtPath = Path.Combine(tempRoot, $"concat_preview_{Guid.NewGuid():N}.txt");
                        var lines = concatList.Select(p => $"file '{p.Replace("'", "'\\''")}'");
                        await File.WriteAllLinesAsync(concatTxtPath, lines).ConfigureAwait(false);

                        var concatOutPath = Path.Combine(tempRoot, $"concat_result_{Guid.NewGuid():N}.mp4");
                        await VideoProcessingNodeExecutor.RunFfmpegAsync(new[]
                        {
                            "-y", "-hide_banner", "-loglevel", "error",
                            "-f", "concat", "-safe", "0",
                            "-i", concatTxtPath,
                            "-c", "copy",
                            concatOutPath
                        }, CancellationToken.None).ConfigureAwait(false);

                        if (File.Exists(concatOutPath))
                            videoInput = concatOutPath;
                    }
                }

                if (_node.TrimEnabled && _node.TrimEndSec > _node.TrimStartSec && !string.IsNullOrWhiteSpace(videoInput) && File.Exists(videoInput))
                {
                    var trimOutPath = Path.Combine(tempRoot, $"trim_preview_{Guid.NewGuid():N}.mp4");
                    await VideoProcessingNodeExecutor.RunFfmpegAsync(new[]
                    {
                        "-y", "-hide_banner", "-loglevel", "error",
                        "-ss", _node.TrimStartSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                        "-to", _node.TrimEndSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                        "-i", videoInput,
                        "-c", "copy",
                        trimOutPath
                    }, CancellationToken.None).ConfigureAwait(false);

                    if (File.Exists(trimOutPath))
                        videoInput = trimOutPath;
                }

                if (File.Exists(videoInput))
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _node.VideoPath = videoInput;
                        RefreshVideoPreview();
                        AppendLog($"✅ [XEM TRƯỚC CẮT/GHÉP] Đã tải video cắt/ghép lên Preview! ({videoInput})");
                    });
                }
                else
                {
                    AppendLog("❌ Không thể tạo video cắt/ghép xem trước.");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi tạo video cắt/ghép xem trước: {ex.Message}");
            }
        }

        private static Brush GetTextBrush(string? colorKey)
        {
            if (string.IsNullOrWhiteSpace(colorKey)) return new SolidColorBrush(Color.FromRgb(229, 231, 235));
            return Application.Current.TryFindResource($"TextOn{colorKey}Brush") as Brush ?? new SolidColorBrush(Color.FromRgb(229, 231, 235));
        }
    }
}
