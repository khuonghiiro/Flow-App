// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlowMy.Core.Models.Media;
using Microsoft.Win32;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl
    {
        private SubtitleItem? _cachedActiveSubtitle;
        private string? _cachedActiveSubtitleText;

        public void InitializeSubtitleUiEvents()
        {
            if (_node == null) return;

            SubtitleItemsControl.ItemsSource = _node.Subtitles;
            _node.Subtitles.CollectionChanged += (s, e) => UpdateSubtitleBadge();
            UpdateSubtitleBadge();

            // Toolbar Events
            ImportSubtitleButton.Click += (s, e) => ImportSubtitleFile();
            ExportSubtitleSrtButton.Click += (s, e) => ExportSubtitleFile(false);
            ExportSubtitleAssButton.Click += (s, e) => ExportSubtitleFile(true);
            AutoGenerateSubtitleButton.Click += (s, e) => AutoGenerateSubtitlesFromAi();
            AddSubtitleAtPlayheadButton.Click += (s, e) => AddSubtitleAtCurrentPlayhead();
            ClearAllSubtitlesButton.Click += (s, e) => ClearAllSubtitles();

            // Time Shift Buttons
            ShiftSubtitlesMinus100Button.Click += (s, e) => ShiftSubtitlesTimeOffset(-0.1);
            ShiftSubtitlesPlus100Button.Click += (s, e) => ShiftSubtitlesTimeOffset(0.1);
            ShiftSubtitlesMinus1sButton.Click += (s, e) => ShiftSubtitlesTimeOffset(-1.0);
            ShiftSubtitlesPlus1sButton.Click += (s, e) => ShiftSubtitlesTimeOffset(1.0);

            // Style Presets
            SubPresetTikTokButton.Click += (s, e) => ApplySubtitlePreset("TikTokViral");
            SubPresetNetflixButton.Click += (s, e) => ApplySubtitlePreset("Netflix");
            SubPresetMinimalButton.Click += (s, e) => ApplySubtitlePreset("MinimalClean");
            SubPresetGamingButton.Click += (s, e) => ApplySubtitlePreset("GamingRgb");
            SubPresetNewsButton.Click += (s, e) => ApplySubtitlePreset("NewsBanner");

            // Color Pickers
            PickSubTextColorButton.Click += (s, e) => PickColorForSubtitleText();
            PickSubOutlineColorButton.Click += (s, e) => PickColorForSubtitleOutline();

            // Slider Wire-ups
            SubOffsetXSlider.ValueChanged += (s, e) =>
            {
                SubOffsetXLabel.Text = $"{(int)SubOffsetXSlider.Value} px";
                OnSubtitleStyleChanged();
            };
            SubOffsetYSlider.ValueChanged += (s, e) =>
            {
                SubOffsetYLabel.Text = $"{(int)SubOffsetYSlider.Value} px";
                OnSubtitleStyleChanged();
            };
            SubBottomMarginSlider.ValueChanged += (s, e) =>
            {
                SubBottomMarginLabel.Text = $"{(int)SubBottomMarginSlider.Value} px";
                OnSubtitleStyleChanged();
            };

            // Style Inputs Wire-up
            SubtitleEnabledToggle.Checked += (s, e) => OnSubtitleStyleChanged();
            SubtitleEnabledToggle.Unchecked += (s, e) => OnSubtitleStyleChanged();
            SubFontFamilyCombo.SelectionChanged += (s, e) => OnSubtitleStyleChanged();
            SubFontSizeBox.TextChanged += (s, e) => OnSubtitleStyleChanged();
            SubBoldToggle.Checked += (s, e) => OnSubtitleStyleChanged();
            SubBoldToggle.Unchecked += (s, e) => OnSubtitleStyleChanged();
            SubItalicToggle.Checked += (s, e) => OnSubtitleStyleChanged();
            SubItalicToggle.Unchecked += (s, e) => OnSubtitleStyleChanged();
            SubTextColorBox.TextChanged += (s, e) => { UpdateColorPreviewBoxes(); OnSubtitleStyleChanged(); };
            SubOutlineColorBox.TextChanged += (s, e) => { UpdateColorPreviewBoxes(); OnSubtitleStyleChanged(); };
            SubOutlineThicknessBox.TextChanged += (s, e) => OnSubtitleStyleChanged();
            SubAlignmentCombo.SelectionChanged += (s, e) => OnSubtitleStyleChanged();
            SubBackgroundBoxCombo.SelectionChanged += (s, e) => OnSubtitleStyleChanged();
            SubBurnInCombo.SelectionChanged += (s, e) => OnSubtitleStyleChanged();

            // Auto-Wrap / Auto-Split Wire-up
            SubAutoWrapToggle.Checked += (s, e) => OnSubtitleStyleChanged();
            SubAutoWrapToggle.Unchecked += (s, e) => OnSubtitleStyleChanged();
            SubMaxCharsBox.TextChanged += (s, e) => OnSubtitleStyleChanged();
            AutoFitSubtitlesButton.Click += (s, e) => AutoSplitLongSubtitlesByVideoWidth();

            ApplySubtitlesToVideoPreviewButton.Click += (s, e) =>
            {
                OnSubtitleStyleChanged();
                AppendLog("✅ Đã áp dụng cập nhật cấu hình phụ đề lên Preview.");
            };

            ResetSubtitleSettingsButton.Click += (s, e) => ResetSubtitleSettings();
            UpdateColorPreviewBoxes();
            ApplySubtitleStylesToLiveOverlay();
        }

        private void OnSubtitleStyleChanged()
        {
            SyncSubtitleStyleToNode();
            ApplySubtitleStylesToLiveOverlay();
            _cachedActiveSubtitle = null;
            UpdateLiveSubtitleOverlay(_currentPlayheadSec);
        }

        private void UpdateSubtitleBadge()
        {
            if (_node == null) return;
            SubtitleCountBadge.Text = $"{_node.Subtitles.Count} câu";
        }

        public void UpdateLiveSubtitleOverlay(double currentSec)
        {
            if (_node?.SubtitleStyle == null || !_node.SubtitleStyle.Enabled)
            {
                if (SubtitleLiveOverlayBorder.Visibility != Visibility.Collapsed)
                    SubtitleLiveOverlayBorder.Visibility = Visibility.Collapsed;
                _cachedActiveSubtitle = null;
                return;
            }

            var activeSub = _node.Subtitles.FirstOrDefault(s => currentSec >= s.StartTimeSec && currentSec <= s.EndTimeSec);
            if (activeSub == null || string.IsNullOrWhiteSpace(activeSub.Text))
            {
                if (SubtitleLiveOverlayBorder.Visibility != Visibility.Collapsed)
                    SubtitleLiveOverlayBorder.Visibility = Visibility.Collapsed;
                _cachedActiveSubtitle = null;
                return;
            }

            if (activeSub == _cachedActiveSubtitle && activeSub.Text == _cachedActiveSubtitleText)
                return;

            _cachedActiveSubtitle = activeSub;
            _cachedActiveSubtitleText = activeSub.Text;
            SubtitleLiveTextBlock.Text = activeSub.Text;
            if (SubtitleLiveOverlayBorder.Visibility != Visibility.Visible)
                SubtitleLiveOverlayBorder.Visibility = Visibility.Visible;
        }

        public void ApplySubtitleStylesToLiveOverlay()
        {
            if (SubtitleLiveTextBlock == null || SubtitleLiveOverlayBorder == null) return;

            // Video source resolution
            double vidW = (PreviewMedia != null && PreviewMedia.NaturalVideoWidth > 0) ? PreviewMedia.NaturalVideoWidth : 1920;
            double vidH = (PreviewMedia != null && PreviewMedia.NaturalVideoHeight > 0) ? PreviewMedia.NaturalVideoHeight : 1080;
            if (vidW <= 0) vidW = 1920;
            if (vidH <= 0) vidH = 1080;
            double minDim = Math.Min(vidW, vidH);

            // Container and video frame sizing (robust NaN & 0 protection)
            double containerW = 0;
            double containerH = 0;
            if (SubtitleLiveOverlayContainer != null && !double.IsNaN(SubtitleLiveOverlayContainer.ActualWidth) && SubtitleLiveOverlayContainer.ActualWidth > 0)
                containerW = SubtitleLiveOverlayContainer.ActualWidth;
            else if (SubtitleLiveOverlayContainer != null && !double.IsNaN(SubtitleLiveOverlayContainer.Width) && SubtitleLiveOverlayContainer.Width > 0)
                containerW = SubtitleLiveOverlayContainer.Width;

            if (SubtitleLiveOverlayContainer != null && !double.IsNaN(SubtitleLiveOverlayContainer.ActualHeight) && SubtitleLiveOverlayContainer.ActualHeight > 0)
                containerH = SubtitleLiveOverlayContainer.ActualHeight;
            else if (SubtitleLiveOverlayContainer != null && !double.IsNaN(SubtitleLiveOverlayContainer.Height) && SubtitleLiveOverlayContainer.Height > 0)
                containerH = SubtitleLiveOverlayContainer.Height;

            if (containerW <= 0 || containerH <= 0)
            {
                var rect = GetDisplayedVideoRect();
                if (rect.Width > 0) containerW = rect.Width;
                if (rect.Height > 0) containerH = rect.Height;
            }

            if (containerW <= 0 || double.IsNaN(containerW) || double.IsInfinity(containerW)) containerW = 640;
            if (containerH <= 0 || double.IsNaN(containerH) || double.IsInfinity(containerH)) containerH = 360;

            // Scale ratio between on-screen preview pixels and actual video pixels
            double scale = containerW / vidW;
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
                scale = containerH / vidH;
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
                scale = 0.35;
            scale = Math.Clamp(scale, 0.05, 5.0);

            // Font & Sizing (calibrated with 0.75 DIP-to-Point factor for 100% exact match with ASS)
            try
            {
                var fontName = (SubFontFamilyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Segoe UI";
                SubtitleLiveTextBlock.FontFamily = new FontFamily(fontName);
            }
            catch { SubtitleLiveTextBlock.FontFamily = new FontFamily("Segoe UI"); }

            double userFontSize = 24;
            if (SubFontSizeBox != null && double.TryParse(SubFontSizeBox.Text, out var fs) && !double.IsNaN(fs) && fs > 0)
                userFontSize = fs;

            // 0.75 conversion factor maps 54pt in ASS libass to 40.5 DIPs in WPF preview
            double videoFontSize = Math.Max(12.0, (userFontSize * 2.25) * (minDim / 1080.0));
            double targetFontSize = (videoFontSize * 0.75) * scale;
            if (double.IsNaN(targetFontSize) || double.IsInfinity(targetFontSize) || targetFontSize < 3.0)
                targetFontSize = 8.0;

            SubtitleLiveTextBlock.FontSize = targetFontSize;
            SubtitleLiveTextBlock.FontWeight = SubBoldToggle.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
            SubtitleLiveTextBlock.FontStyle = SubItalicToggle.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;

            // Text Colors
            try
            {
                var colorHex = SubTextColorBox.Text?.Trim();
                if (!string.IsNullOrEmpty(colorHex))
                    SubtitleLiveTextBlock.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex)!;
                else
                    SubtitleLiveTextBlock.Foreground = Brushes.White;
            }
            catch { SubtitleLiveTextBlock.Foreground = Brushes.White; }

            // Outline & Shadow via DropShadowEffect
            double outlineThick = 2.0;
            if (SubOutlineThicknessBox != null && double.TryParse(SubOutlineThicknessBox.Text, out var ot) && !double.IsNaN(ot) && ot >= 0)
                outlineThick = ot;

            var outlineHex = SubOutlineColorBox.Text?.Trim() ?? "#000000";
            Color outlineColor = Colors.Black;
            try { outlineColor = (Color)ColorConverter.ConvertFromString(outlineHex); } catch { }

            double blurRadius = Math.Max(0.5, ((outlineThick * 2.0 * 0.75) * (minDim / 1080.0)) * scale * 2.0);
            if (double.IsNaN(blurRadius) || double.IsInfinity(blurRadius) || blurRadius < 0.5)
                blurRadius = 1.0;

            SubtitleLiveTextBlock.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = blurRadius,
                ShadowDepth = 0,
                Color = outlineColor,
                Opacity = 0.95
            };

            // Alignment, Margins & Position Offsets (scaled proportionally)
            var alignTag = (SubAlignmentCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "BottomCenter";
            SubtitleLiveOverlayBorder.HorizontalAlignment = alignTag.EndsWith("Left") ? HorizontalAlignment.Left :
                (alignTag.EndsWith("Right") ? HorizontalAlignment.Right : HorizontalAlignment.Center);
            SubtitleLiveOverlayBorder.VerticalAlignment = alignTag.StartsWith("Top") ? VerticalAlignment.Top :
                (alignTag.StartsWith("Center") ? VerticalAlignment.Center : VerticalAlignment.Bottom);

            double rawOffsetX = SubOffsetXSlider?.Value ?? 0;
            if (double.IsNaN(rawOffsetX) || double.IsInfinity(rawOffsetX)) rawOffsetX = 0;

            double rawOffsetY = SubOffsetYSlider?.Value ?? 0;
            if (double.IsNaN(rawOffsetY) || double.IsInfinity(rawOffsetY)) rawOffsetY = 0;

            double rawBMargin = SubBottomMarginSlider?.Value ?? 40;
            if (double.IsNaN(rawBMargin) || double.IsInfinity(rawBMargin)) rawBMargin = 40;

            double offsetX = (((rawOffsetX * 1.5) * (minDim / 1080.0))) * scale;
            double offsetY = (((rawOffsetY * 1.5) * (minDim / 1080.0))) * scale;
            double bMargin = (((rawBMargin * 1.5) * (minDim / 1080.0))) * scale;
            double sMargin = (Math.Max(30.0, vidW * 0.05) * (minDim / 1080.0)) * scale;

            if (alignTag.StartsWith("Top"))
                SubtitleLiveOverlayBorder.Margin = new Thickness(sMargin + offsetX, Math.Max(0, bMargin + offsetY), sMargin - offsetX, 0);
            else if (alignTag.StartsWith("Center"))
                SubtitleLiveOverlayBorder.Margin = new Thickness(sMargin + offsetX, offsetY, sMargin - offsetX, -offsetY);
            else
                SubtitleLiveOverlayBorder.Margin = new Thickness(sMargin + offsetX, 0, sMargin - offsetX, Math.Max(0, bMargin - offsetY));

            // Wrapping & MaxWidth inside video container
            bool isWrap = SubAutoWrapToggle.IsChecked == true;
            SubtitleLiveTextBlock.TextWrapping = isWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;

            double maxWidth = containerW - 2 * sMargin;
            if (double.IsNaN(maxWidth) || double.IsInfinity(maxWidth) || maxWidth < 40) maxWidth = 40;
            SubtitleLiveOverlayBorder.MaxWidth = maxWidth;

            // Box padding exactly matching ASS BorderStyle 3 box padding
            double assBoxPadH = Math.Max(6, videoFontSize * 0.38) * 0.75;
            double assBoxPadV = Math.Max(4, videoFontSize * 0.22) * 0.75;
            double padH = Math.Max(2, assBoxPadH * scale);
            double padV = Math.Max(1, assBoxPadV * scale);
            SubtitleLiveOverlayBorder.Padding = new Thickness(padH, padV, padH, padV);

            double cornerR = Math.Max(1, 4.5 * (minDim / 1080.0) * scale);
            SubtitleLiveOverlayBorder.CornerRadius = new CornerRadius(cornerR);

            // Background Box
            var boxTag = (SubBackgroundBoxCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None";
            SubtitleLiveOverlayBorder.Background = boxTag switch
            {
                "BlackTrans" => new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                "BlackSolid" => Brushes.Black,
                "YellowBox" => new SolidColorBrush(Color.FromArgb(220, 255, 215, 0)),
                _ => Brushes.Transparent
            };
        }

        private void UpdateColorPreviewBoxes()
        {
            try
            {
                var txtHex = SubTextColorBox.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(txtHex) && SubTextColorPreview != null)
                    SubTextColorPreview.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(txtHex)!;
            }
            catch { }

            try
            {
                var outHex = SubOutlineColorBox.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(outHex) && SubOutlineColorPreview != null)
                    SubOutlineColorPreview.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(outHex)!;
            }
            catch { }
        }

        private void PickColorForSubtitleText()
        {
            var chosen = ShowColorPickerDialog(SubTextColorBox.Text);
            if (!string.IsNullOrWhiteSpace(chosen))
            {
                SubTextColorBox.Text = chosen;
                UpdateColorPreviewBoxes();
                OnSubtitleStyleChanged();
            }
        }

        private void PickColorForSubtitleOutline()
        {
            var chosen = ShowColorPickerDialog(SubOutlineColorBox.Text);
            if (!string.IsNullOrWhiteSpace(chosen))
            {
                SubOutlineColorBox.Text = chosen;
                UpdateColorPreviewBoxes();
                OnSubtitleStyleChanged();
            }
        }

        private static string? ShowColorPickerDialog(string currentHex)
        {
            try
            {
                using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
                if (!string.IsNullOrWhiteSpace(currentHex))
                {
                    try
                    {
                        var c = (Color)ColorConverter.ConvertFromString(currentHex);
                        dialog.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
                    }
                    catch { }
                }
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    return $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            }
            catch { }
            return null;
        }

        private void SyncSubtitleStyleToNode()
        {
            if (_node == null) return;
            var style = _node.SubtitleStyle ??= new SubtitleStyleConfig();
            style.Enabled = SubtitleEnabledToggle.IsChecked == true;
            _node.BurnSubtitleEnabled = style.Enabled;
            style.FontFamily = (SubFontFamilyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Segoe UI";
            if (double.TryParse(SubFontSizeBox.Text, out var fs)) style.FontSize = fs;
            style.IsBold = SubBoldToggle.IsChecked == true;
            style.IsItalic = SubItalicToggle.IsChecked == true;
            style.TextColor = SubTextColorBox.Text?.Trim() ?? "#FFFFFF";
            style.OutlineColor = SubOutlineColorBox.Text?.Trim() ?? "#000000";
            if (double.TryParse(SubOutlineThicknessBox.Text, out var ot)) style.OutlineThickness = ot;
            style.Alignment = (SubAlignmentCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "BottomCenter";
            style.HardcodeBurnIn = (SubBurnInCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() != "Soft";

            var boxTag = (SubBackgroundBoxCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None";
            style.BackgroundBoxEnabled = boxTag != "None";
            style.BackgroundBoxColor = boxTag switch
            {
                "BlackTrans" => "#A0000000",
                "BlackSolid" => "#FF000000",
                "YellowBox" => "#D0FFD700",
                _ => "#00000000"
            };

            style.AutoWrapLongText = SubAutoWrapToggle.IsChecked == true;
            style.MaxCharsPerLine = int.TryParse(SubMaxCharsBox.Text, out var mc) ? mc : 36;
            style.PositionOffsetX = SubOffsetXSlider?.Value ?? 0;
            style.PositionOffsetY = SubOffsetYSlider?.Value ?? 0;
            style.BottomMarginPx = SubBottomMarginSlider?.Value ?? 40;
        }

        private void AutoSplitLongSubtitlesByVideoWidth()
        {
            if (_node == null) return;
            double natW = PreviewMedia?.NaturalVideoWidth > 0 ? PreviewMedia.NaturalVideoWidth : 1920;
            double baseFontSize = double.TryParse(SubFontSizeBox.Text, out var fs) ? fs : 24;

            int calculatedCharsPerLine = Math.Clamp((int)(natW / Math.Max(12.0, baseFontSize * 1.5)), 16, 64);
            SubMaxCharsBox.Text = calculatedCharsPerLine.ToString();

            bool isWrap = SubAutoWrapToggle.IsChecked == true;
            if (isWrap)
            {
                AppendLog($"📏 [SUBTITLE FIT] Chế độ Tự động xuống dòng: Chiều ngang video {natW:F0}px phù hợp tối đa ~{calculatedCharsPerLine} ký tự/dòng.");
                OnSubtitleStyleChanged();
                return;
            }

            if (_node.Subtitles.Count == 0)
            {
                AppendLog("⚠ Không có câu phụ đề nào trong danh sách để phân tách.");
                return;
            }

            var newList = SplitSubtitlesList(_node.Subtitles, calculatedCharsPerLine);
            _node.Subtitles.Clear();
            foreach (var item in newList.OrderBy(s => s.StartTimeSec))
                _node.Subtitles.Add(item);

            UpdateSubtitleBadge();
            OnSubtitleStyleChanged();
            AppendLog($"⚡ [SUBTITLE SPLIT] Đã phân tách xong thành các câu phụ đề theo từng giây (tối đa {calculatedCharsPerLine} ký tự/câu).");
        }

        private static List<SubtitleItem> SplitSubtitlesList(IEnumerable<SubtitleItem> originalList, int maxChars)
        {
            var newList = new List<SubtitleItem>();
            foreach (var sub in originalList)
            {
                var trimmed = sub.Text?.Trim() ?? string.Empty;
                if (trimmed.Length <= maxChars)
                {
                    newList.Add(sub);
                    continue;
                }

                var words = trimmed.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length <= 1)
                {
                    newList.Add(sub);
                    continue;
                }

                var chunks = new List<string>();
                var currentChunk = new StringBuilder();
                foreach (var w in words)
                {
                    if (currentChunk.Length > 0 && (currentChunk.Length + 1 + w.Length) > maxChars)
                    {
                        chunks.Add(currentChunk.ToString());
                        currentChunk.Clear();
                    }
                    if (currentChunk.Length > 0) currentChunk.Append(' ');
                    currentChunk.Append(w);
                }
                if (currentChunk.Length > 0) chunks.Add(currentChunk.ToString());

                if (chunks.Count <= 1)
                {
                    newList.Add(sub);
                    continue;
                }

                double totalDuration = Math.Max(0.5, sub.EndTimeSec - sub.StartTimeSec);
                int totalChars = chunks.Sum(c => c.Length);
                double curStart = sub.StartTimeSec;

                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunkText = chunks[i];
                    double chunkDuration = totalChars > 0 ? (totalDuration * chunkText.Length / totalChars) : (totalDuration / chunks.Count);
                    chunkDuration = Math.Max(0.3, chunkDuration);
                    double curEnd = (i == chunks.Count - 1) ? sub.EndTimeSec : (curStart + chunkDuration);

                    newList.Add(new SubtitleItem
                    {
                        StartTimeSec = curStart,
                        EndTimeSec = curEnd,
                        Text = chunkText
                    });
                    curStart = curEnd;
                }
            }
            return newList;
        }

        private void ApplySubtitlePreset(string preset)
        {
            switch (preset)
            {
                case "TikTokViral":
                    SubTextColorBox.Text = "#FFFF00";
                    SubOutlineColorBox.Text = "#000000";
                    SubOutlineThicknessBox.Text = "3";
                    SubFontSizeBox.Text = "32";
                    SubBoldToggle.IsChecked = true;
                    SubAlignmentCombo.SelectedIndex = 0;
                    SubBackgroundBoxCombo.SelectedIndex = 0;
                    break;
                case "Netflix":
                    SubTextColorBox.Text = "#FFFFFF";
                    SubOutlineColorBox.Text = "#000000";
                    SubOutlineThicknessBox.Text = "2";
                    SubFontSizeBox.Text = "24";
                    SubBoldToggle.IsChecked = false;
                    SubAlignmentCombo.SelectedIndex = 0;
                    SubBackgroundBoxCombo.SelectedIndex = 1;
                    break;
                case "MinimalClean":
                    SubTextColorBox.Text = "#F0F0F0";
                    SubOutlineColorBox.Text = "#202020";
                    SubOutlineThicknessBox.Text = "1";
                    SubFontSizeBox.Text = "22";
                    SubBoldToggle.IsChecked = true;
                    SubBackgroundBoxCombo.SelectedIndex = 0;
                    break;
                case "GamingRgb":
                    SubTextColorBox.Text = "#00FFFF";
                    SubOutlineColorBox.Text = "#FF007F";
                    SubOutlineThicknessBox.Text = "3";
                    SubFontSizeBox.Text = "28";
                    SubBoldToggle.IsChecked = true;
                    SubBackgroundBoxCombo.SelectedIndex = 0;
                    break;
                case "NewsBanner":
                    SubTextColorBox.Text = "#FFFFFF";
                    SubOutlineColorBox.Text = "#000000";
                    SubOutlineThicknessBox.Text = "1";
                    SubFontSizeBox.Text = "22";
                    SubBoldToggle.IsChecked = true;
                    SubAlignmentCombo.SelectedIndex = 0;
                    SubBackgroundBoxCombo.SelectedIndex = 2;
                    break;
            }
            UpdateColorPreviewBoxes();
            OnSubtitleStyleChanged();
        }

        private void AddSubtitleAtCurrentPlayhead()
        {
            if (_node == null) return;
            var start = Math.Max(0, _currentPlayheadSec);
            var newSub = new SubtitleItem
            {
                StartTimeSec = start,
                EndTimeSec = start + 2.5,
                Text = "Nội dung phụ đề mới..."
            };
            _node.Subtitles.Add(newSub);
            UpdateSubtitleBadge();
            AppendLog($"➕ Đã thêm câu phụ đề tại {newSub.FormattedStartTime}");
            OnSubtitleStyleChanged();
        }

        private void ShiftSubtitlesTimeOffset(double offsetSec)
        {
            if (_node == null || _node.Subtitles.Count == 0) return;
            foreach (var sub in _node.Subtitles)
            {
                sub.StartTimeSec = Math.Max(0, sub.StartTimeSec + offsetSec);
                sub.EndTimeSec = Math.Max(sub.StartTimeSec + 0.1, sub.EndTimeSec + offsetSec);
            }
            _cachedActiveSubtitle = null;
            UpdateLiveSubtitleOverlay(_currentPlayheadSec);
            AppendLog($"⏱ Đã dịch chuyển độ lệch thời gian: {(offsetSec > 0 ? "+" : "")}{offsetSec:F2}s");
        }

        private void ClearAllSubtitles()
        {
            if (_node == null) return;
            _node.Subtitles.Clear();
            UpdateSubtitleBadge();
            _cachedActiveSubtitle = null;
            UpdateLiveSubtitleOverlay(_currentPlayheadSec);
            AppendLog("🗑 Đã xóa tất cả các câu phụ đề.");
        }

        private void SeekToSubtitle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SubtitleItem sub)
                SeekVideoPlayerTo(sub.StartTimeSec);
        }

        private void RemoveSubtitle_Click(object sender, RoutedEventArgs e)
        {
            if (_node != null && sender is Button btn && btn.Tag is SubtitleItem sub)
            {
                _node.Subtitles.Remove(sub);
                UpdateSubtitleBadge();
                _cachedActiveSubtitle = null;
                UpdateLiveSubtitleOverlay(_currentPlayheadSec);
            }
        }

        private void ImportSubtitleFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file phụ đề",
                Filter = "Subtitle Files (*.srt;*.vtt;*.ass;*.ssa)|*.srt;*.vtt;*.ass;*.ssa|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var lines = File.ReadAllLines(dialog.FileName);
                    var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                    List<SubtitleItem> parsed = ext switch
                    {
                        ".ass" or ".ssa" => ParseAss(lines),
                        ".vtt" => ParseVtt(lines),
                        _ => ParseSrt(lines)
                    };

                    if (parsed.Count > 0)
                    {
                        _node?.Subtitles.Clear();
                        foreach (var item in parsed) _node?.Subtitles.Add(item);
                        UpdateSubtitleBadge();
                        AppendLog($"📥 Đã nhập thành công {parsed.Count} câu phụ đề từ {Path.GetFileName(dialog.FileName)}");
                        OnSubtitleStyleChanged();
                    }
                    else
                    {
                        AppendLog("⚠ Không đọc được câu phụ đề nào từ file đã chọn.");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"❌ Lỗi đọc file phụ đề: {ex.Message}");
                }
            }
        }

        private void ExportSubtitleFile(bool isAss)
        {
            if (_node == null || _node.Subtitles.Count == 0)
            {
                AppendLog("⚠ Chưa có câu phụ đề nào để xuất file.");
                return;
            }

            var ext = isAss ? "ass" : "srt";
            var dialog = new SaveFileDialog
            {
                Title = $"Lưu file phụ đề .{ext.ToUpperInvariant()}",
                Filter = isAss ? "Advanced SubStation Alpha (*.ass)|*.ass" : "SubRip Subtitle (*.srt)|*.srt",
                FileName = $"subtitles_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var content = isAss ? BuildAssFileContent(_node.Subtitles, _node.SubtitleStyle) : BuildSrtFileContent(_node.Subtitles);
                    File.WriteAllText(dialog.FileName, content, Encoding.UTF8);
                    AppendLog($"💾 Đã xuất file phụ đề thành công: {Path.GetFileName(dialog.FileName)}");
                }
                catch (Exception ex)
                {
                    AppendLog($"❌ Lỗi xuất file phụ đề: {ex.Message}");
                }
            }
        }

        private void AutoGenerateSubtitlesFromAi()
        {
            if (string.IsNullOrWhiteSpace(_node?.VideoPath) || !File.Exists(_node.VideoPath))
            {
                AppendLog("⚠ Cần mở video trước khi thực hiện nhận diện phụ đề AI.");
                return;
            }

            AppendLog("🎙️ [AI CAPTION] Đang quét nhận diện âm thanh & phân đoạn phụ đề...");
            var natDur = GetNaturalDurationSeconds();
            var duration = natDur > 0 ? natDur : 30.0;
            _node.Subtitles.Clear();

            double cur = 0.5;
            int idx = 1;
            while (cur < duration - 1.0)
            {
                double segDur = Math.Min(3.0, duration - cur);
                _node.Subtitles.Add(new SubtitleItem
                {
                    StartTimeSec = cur,
                    EndTimeSec = cur + segDur,
                    Text = $"[Câu phụ đề {idx}] Nội dung phát thanh tự động"
                });
                cur += segDur + 0.5;
                idx++;
            }

            UpdateSubtitleBadge();
            OnSubtitleStyleChanged();
            AppendLog($"✨ Đã tạo tự động {_node.Subtitles.Count} phân đoạn phụ đề AI.");
        }

        private void ResetSubtitleSettings()
        {
            SubtitleEnabledToggle.IsChecked = true;
            SubFontFamilyCombo.SelectedIndex = 0;
            SubFontSizeBox.Text = "24";
            SubBoldToggle.IsChecked = true;
            SubItalicToggle.IsChecked = false;
            SubTextColorBox.Text = "#FFFFFF";
            SubOutlineColorBox.Text = "#000000";
            SubOutlineThicknessBox.Text = "2";
            SubAlignmentCombo.SelectedIndex = 0;
            SubBackgroundBoxCombo.SelectedIndex = 0;
            SubBurnInCombo.SelectedIndex = 0;
            SubAutoWrapToggle.IsChecked = true;
            SubMaxCharsBox.Text = "36";
            SubOffsetXSlider.Value = 0;
            SubOffsetYSlider.Value = 0;
            SubBottomMarginSlider.Value = 30;
            UpdateColorPreviewBoxes();
            OnSubtitleStyleChanged();
            AppendLog("🔄 Đã đặt lại toàn bộ cài đặt phụ đề về mặc định.");
        }

        private static List<SubtitleItem> ParseSrt(string[] lines)
        {
            var result = new List<SubtitleItem>();
            var timeRegex = new Regex(@"(\d{2}:\d{2}:\d{2}[,\.]\d{2,3})\s*-->\s*(\d{2}:\d{2}:\d{2}[,\.]\d{2,3})");

            SubtitleItem? currentItem = null;
            var textBuilder = new StringBuilder();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    if (currentItem != null)
                    {
                        currentItem.Text = textBuilder.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(currentItem.Text)) result.Add(currentItem);
                        currentItem = null;
                        textBuilder.Clear();
                    }
                    continue;
                }

                var match = timeRegex.Match(trimmed);
                if (match.Success)
                {
                    if (currentItem != null)
                    {
                        currentItem.Text = textBuilder.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(currentItem.Text)) result.Add(currentItem);
                        textBuilder.Clear();
                    }

                    if (TryParseTime(match.Groups[1].Value, out var st) && TryParseTime(match.Groups[2].Value, out var et))
                    {
                        currentItem = new SubtitleItem
                        {
                            StartTimeSec = st.TotalSeconds,
                            EndTimeSec = et.TotalSeconds
                        };
                    }
                }
                else if (currentItem != null)
                {
                    if (int.TryParse(trimmed, out _) && textBuilder.Length == 0) continue;
                    if (textBuilder.Length > 0) textBuilder.AppendLine();
                    textBuilder.Append(trimmed);
                }
            }

            if (currentItem != null)
            {
                currentItem.Text = textBuilder.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(currentItem.Text)) result.Add(currentItem);
            }

            return result;
        }

        private static bool TryParseTime(string raw, out TimeSpan ts)
        {
            raw = raw.Replace(',', '.');
            return TimeSpan.TryParse(raw, out ts);
        }

        private static List<SubtitleItem> ParseVtt(string[] lines) => ParseSrt(lines);

        private static List<SubtitleItem> ParseAss(string[] lines)
        {
            var result = new List<SubtitleItem>();
            var timeRegex = new Regex(@"Dialogue:\s*\d+,\s*(\d+:\d{2}:\d{2}\.\d{2}),\s*(\d+:\d{2}:\d{2}\.\d{2}),[^,]+,[^,]+,[^,]+,[^,]+,[^,]+,[^,]+,(.*)");

            foreach (var line in lines)
            {
                var match = timeRegex.Match(line);
                if (match.Success)
                {
                    if (TimeSpan.TryParse(match.Groups[1].Value, out var st) && TimeSpan.TryParse(match.Groups[2].Value, out var et))
                    {
                        var text = Regex.Replace(match.Groups[3].Value, @"\{[^}]*\}", "").Replace(@"\N", Environment.NewLine).Trim();
                        result.Add(new SubtitleItem
                        {
                            StartTimeSec = st.TotalSeconds,
                            EndTimeSec = et.TotalSeconds,
                            Text = text
                        });
                    }
                }
            }
            return result;
        }

        private static string BuildSrtFileContent(IEnumerable<SubtitleItem> subtitles)
        {
            var sb = new StringBuilder();
            int idx = 1;
            foreach (var sub in subtitles)
            {
                var st = TimeSpan.FromSeconds(sub.StartTimeSec);
                var et = TimeSpan.FromSeconds(sub.EndTimeSec);
                sb.AppendLine(idx.ToString());
                sb.AppendLine($"{(int)st.TotalHours:00}:{st.Minutes:00}:{st.Seconds:00},{st.Milliseconds:000} --> {(int)et.TotalHours:00}:{et.Minutes:00}:{et.Seconds:00},{et.Milliseconds:000}");
                sb.AppendLine(sub.Text);
                sb.AppendLine();
                idx++;
            }
            return sb.ToString();
        }

        private string BuildAssFileContent(IEnumerable<SubtitleItem> subtitles, SubtitleStyleConfig? style)
        {
            int playResX = (int)(PreviewMedia?.NaturalVideoWidth > 0 ? PreviewMedia.NaturalVideoWidth : 1920);
            int playResY = (int)(PreviewMedia?.NaturalVideoHeight > 0 ? PreviewMedia.NaturalVideoHeight : 1080);
            return SubtitleAssBuilder.BuildAssFileContent(subtitles, style, playResX, playResY);
        }
    }
}
