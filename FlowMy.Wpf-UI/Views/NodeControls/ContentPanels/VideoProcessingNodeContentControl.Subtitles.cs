// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using FlowMy.Core.Models.Media;
using Microsoft.Win32;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl
    {
        private SubtitleItem? _cachedActiveSubtitle;
        private string? _cachedActiveSubtitleText;
        private bool _isBatchUpdatingSubtitles;

        public ObservableCollection<SubtitleLanguageTag> SubtitleLanguageTags { get; } = new();

        public void InitializeSubtitleUiEvents()
        {
            if (_node == null) return;

            SubtitleItemsControl.ItemsSource = _node.Subtitles;
            if (SubtitleLanguageTagsControl != null)
                SubtitleLanguageTagsControl.ItemsSource = SubtitleLanguageTags;

            _node.Subtitles.CollectionChanged += (s, e) =>
            {
                if (_isBatchUpdatingSubtitles) return;
                UpdateSubtitleBadge();
                RefreshAvailableLanguageTags(isInitialLoad: false);
            };
            UpdateSubtitleBadge();

            // Toolbar Events
            ImportSubtitleButton.Click += (s, e) => ImportSubtitleFile();
            if (ExportSubtitleJsonButton != null)
                ExportSubtitleJsonButton.Click += (s, e) => ExportSubtitleFile("json");
            ExportSubtitleSrtButton.Click += (s, e) => ExportSubtitleFile("srt");
            ExportSubtitleAssButton.Click += (s, e) => ExportSubtitleFile("ass");
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
            RefreshAvailableLanguageTags(isInitialLoad: true);
            ApplySubtitleStylesToLiveOverlay();
        }

        public void RefreshAvailableLanguageTags(bool isInitialLoad = false)
        {
            if (_node == null) return;

            var currentActiveCodes = SubtitleLanguageTags.Where(t => t.IsActive).Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var discoveredLanguages = new Dictionary<string, (string code, string name, string tag, bool isOriginal, string tagBg, string tagFg, string tagBorder, int count)>(StringComparer.OrdinalIgnoreCase);

            foreach (var sub in _node.Subtitles)
            {
                foreach (var line in sub.Lines)
                {
                    var code = string.IsNullOrWhiteSpace(line.LanguageCode) ? (line.IsOriginal ? "orig" : "vi") : line.LanguageCode;
                    if (!discoveredLanguages.ContainsKey(code))
                    {
                        var info = SubtitleLanguageHelper.GetLanguageInfo(code, line.IsOriginal);
                        discoveredLanguages[code] = (code, info.name, info.tag, line.IsOriginal, info.tagBg, info.tagFg, info.tagBorder, 1);
                    }
                    else
                    {
                        var exist = discoveredLanguages[code];
                        discoveredLanguages[code] = (exist.code, exist.name, exist.tag, exist.isOriginal || line.IsOriginal, exist.tagBg, exist.tagFg, exist.tagBorder, exist.count + 1);
                    }
                }
            }

            if (discoveredLanguages.Count == 0 && _node.Subtitles.Count > 0)
            {
                var info = SubtitleLanguageHelper.GetLanguageInfo("vi", false);
                discoveredLanguages["vi"] = ("vi", info.name, info.tag, false, info.tagBg, info.tagFg, info.tagBorder, _node.Subtitles.Count);
            }

            var newTagList = new List<SubtitleLanguageTag>();
            bool hasAnyActive = false;

            foreach (var kvp in discoveredLanguages.Values.OrderByDescending(v => v.isOriginal).ThenBy(v => v.name))
            {
                bool active;
                if (isInitialLoad || currentActiveCodes.Count == 0)
                {
                    // Mặc định CHỈ active tag bản gốc
                    active = kvp.isOriginal;
                }
                else
                {
                    active = currentActiveCodes.Contains(kvp.code);
                }

                if (active) hasAnyActive = true;

                newTagList.Add(new SubtitleLanguageTag
                {
                    Code = kvp.code,
                    Name = kvp.name,
                    Tag = kvp.tag,
                    IsOriginal = kvp.isOriginal,
                    IsActive = active,
                    LineCount = kvp.count,
                    TagBackgroundHex = kvp.tagBg,
                    TagForegroundHex = kvp.tagFg,
                    TagBorderHex = kvp.tagBorder
                });
            }

            // Nếu không có tag nào là bản gốc và chưa có tag nào active -> active tag đầu tiên
            if (!hasAnyActive && newTagList.Count > 0)
            {
                newTagList[0].IsActive = true;
            }

            SubtitleLanguageTags.Clear();
            foreach (var tag in newTagList)
            {
                SubtitleLanguageTags.Add(tag);
            }

            SyncLanguageTagsToSubtitleLines();
        }

        private void SyncLanguageTagsToSubtitleLines()
        {
            if (_node == null) return;
            var activeCodes = SubtitleLanguageTags.Where(t => t.IsActive).Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var sub in _node.Subtitles)
            {
                foreach (var line in sub.Lines)
                {
                    var code = string.IsNullOrWhiteSpace(line.LanguageCode) ? (line.IsOriginal ? "orig" : "vi") : line.LanguageCode;
                    line.IsActive = activeCodes.Contains(code);
                }
            }
        }

        private void LanguageTagToggle_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is FrameworkElement fe && fe.DataContext is SubtitleLanguageTag tag)
            {
                tag.IsActive = !tag.IsActive;
                SyncLanguageTagsToSubtitleLines();
                OnSubtitleStyleChanged();
            }
        }

        private void ShowOnlyOriginalLanguage_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            foreach (var t in SubtitleLanguageTags)
            {
                t.IsActive = t.IsOriginal;
            }
            if (!SubtitleLanguageTags.Any(t => t.IsActive) && SubtitleLanguageTags.Count > 0)
                SubtitleLanguageTags[0].IsActive = true;

            SyncLanguageTagsToSubtitleLines();
            OnSubtitleStyleChanged();
        }

        private void ShowOnlyTranslatedLanguage_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            foreach (var t in SubtitleLanguageTags)
            {
                t.IsActive = !t.IsOriginal;
            }
            if (!SubtitleLanguageTags.Any(t => t.IsActive) && SubtitleLanguageTags.Count > 0)
                SubtitleLanguageTags[0].IsActive = true;

            SyncLanguageTagsToSubtitleLines();
            OnSubtitleStyleChanged();
        }

        private void ShowAllLanguages_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            foreach (var t in SubtitleLanguageTags)
            {
                t.IsActive = true;
            }
            SyncLanguageTagsToSubtitleLines();
            OnSubtitleStyleChanged();
        }

        private void LineTag_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is FrameworkElement fe && fe.DataContext is SubtitleLineItem line)
            {
                line.IsActive = !line.IsActive;
                OnSubtitleStyleChanged();
            }
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
            if (activeSub == null)
            {
                if (SubtitleLiveOverlayBorder.Visibility != Visibility.Collapsed)
                    SubtitleLiveOverlayBorder.Visibility = Visibility.Collapsed;
                _cachedActiveSubtitle = null;
                return;
            }

            var activeLines = activeSub.Lines.Where(l => l.IsActive && !string.IsNullOrWhiteSpace(l.Text)).ToList();
            if (activeLines.Count == 0)
            {
                if (SubtitleLiveOverlayBorder.Visibility != Visibility.Collapsed)
                    SubtitleLiveOverlayBorder.Visibility = Visibility.Collapsed;
                _cachedActiveSubtitle = null;
                return;
            }

            var joinedText = string.Join(" | ", activeLines.Select(l => l.Text));
            if (activeSub == _cachedActiveSubtitle && joinedText == _cachedActiveSubtitleText)
                return;

            _cachedActiveSubtitle = activeSub;
            _cachedActiveSubtitleText = joinedText;

            SubtitleLiveTextBlock.Inlines.Clear();

            for (int i = 0; i < activeLines.Count; i++)
            {
                var line = activeLines[i];
                if (i > 0)
                    SubtitleLiveTextBlock.Inlines.Add(new LineBreak());

                if (line.IsOriginal)
                {
                    var origRun = new Run(line.Text)
                    {
                        FontSize = Math.Max(6.0, SubtitleLiveTextBlock.FontSize * 0.85),
                        Foreground = SubtitleLanguageHelper.GetFrozenBrush("#FCD34D", "#FCD34D")
                    };
                    SubtitleLiveTextBlock.Inlines.Add(origRun);
                }
                else
                {
                    var transRun = new Run(line.Text)
                    {
                        FontSize = SubtitleLiveTextBlock.FontSize,
                        FontWeight = SubtitleLiveTextBlock.FontWeight,
                        Foreground = SubtitleLiveTextBlock.Foreground
                    };
                    SubtitleLiveTextBlock.Inlines.Add(transRun);
                }
            }

            if (SubtitleLiveOverlayBorder.Visibility != Visibility.Visible)
                SubtitleLiveOverlayBorder.Visibility = Visibility.Visible;
        }

        public void ApplySubtitleStylesToLiveOverlay()
        {
            if (SubtitleLiveTextBlock == null || SubtitleLiveOverlayBorder == null) return;

            double vidW = (PreviewMedia != null && PreviewMedia.NaturalVideoWidth > 0) ? PreviewMedia.NaturalVideoWidth : 1920;
            double vidH = (PreviewMedia != null && PreviewMedia.NaturalVideoHeight > 0) ? PreviewMedia.NaturalVideoHeight : 1080;
            if (vidW <= 0) vidW = 1920;
            if (vidH <= 0) vidH = 1080;
            double minDim = Math.Min(vidW, vidH);

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

            double scale = containerW / vidW;
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
                scale = containerH / vidH;
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
                scale = 0.35;
            scale = Math.Clamp(scale, 0.05, 5.0);

            try
            {
                var fontName = (SubFontFamilyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Segoe UI";
                SubtitleLiveTextBlock.FontFamily = new FontFamily(fontName);
            }
            catch { SubtitleLiveTextBlock.FontFamily = new FontFamily("Segoe UI"); }

            double userFontSize = 24;
            if (SubFontSizeBox != null && double.TryParse(SubFontSizeBox.Text, out var fs) && !double.IsNaN(fs) && fs > 0)
                userFontSize = fs;

            double videoFontSize = Math.Max(12.0, (userFontSize * 2.25) * (minDim / 1080.0));
            double targetFontSize = (videoFontSize * 0.75) * scale;
            if (double.IsNaN(targetFontSize) || double.IsInfinity(targetFontSize) || targetFontSize < 3.0)
                targetFontSize = 8.0;

            SubtitleLiveTextBlock.FontSize = targetFontSize;
            SubtitleLiveTextBlock.FontWeight = SubBoldToggle.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
            SubtitleLiveTextBlock.FontStyle = SubItalicToggle.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;

            try
            {
                var colorHex = SubTextColorBox.Text?.Trim();
                if (!string.IsNullOrEmpty(colorHex))
                    SubtitleLiveTextBlock.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex)!;
                else
                    SubtitleLiveTextBlock.Foreground = Brushes.White;
            }
            catch { SubtitleLiveTextBlock.Foreground = Brushes.White; }

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

            bool isWrap = SubAutoWrapToggle.IsChecked == true;
            SubtitleLiveTextBlock.TextWrapping = isWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;

            double maxWidth = containerW - 2 * sMargin;
            if (double.IsNaN(maxWidth) || double.IsInfinity(maxWidth) || maxWidth < 40) maxWidth = 40;
            SubtitleLiveOverlayBorder.MaxWidth = maxWidth;

            double assBoxPadH = Math.Max(6, videoFontSize * 0.38) * 0.75;
            double assBoxPadV = Math.Max(4, videoFontSize * 0.22) * 0.75;
            double padH = Math.Max(2, assBoxPadH * scale);
            double padV = Math.Max(1, assBoxPadV * scale);
            SubtitleLiveOverlayBorder.Padding = new Thickness(padH, padV, padH, padV);

            double cornerR = Math.Max(1, 4.5 * (minDim / 1080.0) * scale);
            SubtitleLiveOverlayBorder.CornerRadius = new CornerRadius(cornerR);

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
            if (SubtitlesMasterContainer != null)
                SubtitlesMasterContainer.Visibility = style.Enabled ? Visibility.Visible : Visibility.Collapsed;
            if (SubtitleStatusText != null)
                SubtitleStatusText.Text = style.Enabled ? "ĐANG BẬT" : "TẮT";
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
            SubtitleItemsControl.ItemsSource = null;
            _isBatchUpdatingSubtitles = true;
            try
            {
                _node.Subtitles.Clear();
                foreach (var item in newList.OrderBy(s => s.StartTimeSec))
                    _node.Subtitles.Add(item);

                UpdateSubtitleBadge();
                RefreshAvailableLanguageTags(isInitialLoad: false);
            }
            finally
            {
                _isBatchUpdatingSubtitles = false;
                SubtitleItemsControl.ItemsSource = _node.Subtitles;
            }
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

                    var newItem = new SubtitleItem
                    {
                        StartTimeSec = curStart,
                        EndTimeSec = curEnd,
                        Text = chunkText,
                        OriginalText = sub.OriginalText,
                        SourceLanguage = sub.SourceLanguage
                    };
                    newItem.EnsureLinesFromText();
                    newList.Add(newItem);
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
            newSub.EnsureLinesFromText();
            _node.Subtitles.Add(newSub);
            UpdateSubtitleBadge();
            RefreshAvailableLanguageTags(isInitialLoad: false);
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
            SubtitleLanguageTags.Clear();
            UpdateSubtitleBadge();
            _cachedActiveSubtitle = null;
            UpdateLiveSubtitleOverlay(_currentPlayheadSec);
            AppendLog("🗑 Đã xóa tất cả các câu phụ đề.");
        }

        private void SubtitleCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is SubtitleItem sub)
            {
                if (_node != null)
                {
                    foreach (var s in _node.Subtitles)
                        s.IsSelected = (s == sub);
                }
                SeekVideoPlayerTo(sub.StartTimeSec);
            }
        }

        private void AddSubtitleLine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is SubtitleItem sub)
            {
                var lineCount = sub.Lines.Count;
                string defaultLang = lineCount switch
                {
                    0 => "zh",
                    1 => "vi",
                    2 => "en",
                    3 => "ja",
                    _ => "other"
                };
                var info = SubtitleLanguageHelper.GetLanguageInfo(defaultLang, isOriginal: lineCount == 0);
                sub.Lines.Add(new SubtitleLineItem
                {
                    LanguageCode = defaultLang,
                    LanguageName = info.name,
                    Tag = info.tag,
                    Text = string.Empty,
                    IsOriginal = lineCount == 0,
                    IsActive = true,
                    TagBackgroundHex = info.tagBg,
                    TagForegroundHex = info.tagFg,
                    TagBorderHex = info.tagBorder,
                    TextColorHex = info.textColor
                });
                sub.HookLinesEvents();
                RefreshAvailableLanguageTags(isInitialLoad: false);
                OnSubtitleStyleChanged();
            }
        }

        private void RemoveSubtitleLine_Click(object sender, RoutedEventArgs e)
        {
            if (_node != null && sender is FrameworkElement fe && fe.Tag is SubtitleLineItem line)
            {
                foreach (var sub in _node.Subtitles)
                {
                    if (sub.Lines.Contains(line) && sub.Lines.Count > 1)
                    {
                        sub.Lines.Remove(line);
                        RefreshAvailableLanguageTags(isInitialLoad: false);
                        OnSubtitleStyleChanged();
                        break;
                    }
                }
            }
        }

        private void SubStartMinus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is SubtitleItem sub)
            {
                sub.StartTimeSec = Math.Max(0, sub.StartTimeSec - 0.5);
                SeekVideoPlayerTo(sub.StartTimeSec);
                OnSubtitleStyleChanged();
            }
        }

        private void SubStartPlus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is SubtitleItem sub)
            {
                sub.StartTimeSec = Math.Min(sub.EndTimeSec - 0.1, sub.StartTimeSec + 0.5);
                SeekVideoPlayerTo(sub.StartTimeSec);
                OnSubtitleStyleChanged();
            }
        }

        private void SetSubStartToPlayhead_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is SubtitleItem sub)
            {
                sub.StartTimeSec = Math.Max(0, _currentPlayheadSec);
                if (sub.EndTimeSec <= sub.StartTimeSec)
                    sub.EndTimeSec = sub.StartTimeSec + 2.0;
                SeekVideoPlayerTo(sub.StartTimeSec);
                OnSubtitleStyleChanged();
            }
        }

        private void SubEndMinus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is SubtitleItem sub)
            {
                sub.EndTimeSec = Math.Max(sub.StartTimeSec + 0.1, sub.EndTimeSec - 0.5);
                SeekVideoPlayerTo(sub.EndTimeSec);
                OnSubtitleStyleChanged();
            }
        }

        private void SubEndPlus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is SubtitleItem sub)
            {
                sub.EndTimeSec = sub.EndTimeSec + 0.5;
                SeekVideoPlayerTo(sub.EndTimeSec);
                OnSubtitleStyleChanged();
            }
        }

        private void SetSubEndToPlayhead_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is SubtitleItem sub)
            {
                sub.EndTimeSec = Math.Max(sub.StartTimeSec + 0.1, _currentPlayheadSec);
                SeekVideoPlayerTo(sub.EndTimeSec);
                OnSubtitleStyleChanged();
            }
        }

        private void ShiftSubSegmentMinus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is SubtitleItem sub)
            {
                double dur = sub.DurationSec;
                sub.StartTimeSec = Math.Max(0, sub.StartTimeSec - 0.5);
                sub.EndTimeSec = sub.StartTimeSec + dur;
                SeekVideoPlayerTo(sub.StartTimeSec);
                OnSubtitleStyleChanged();
            }
        }

        private void ShiftSubSegmentPlus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is SubtitleItem sub)
            {
                double dur = sub.DurationSec;
                sub.StartTimeSec = sub.StartTimeSec + 0.5;
                sub.EndTimeSec = sub.StartTimeSec + dur;
                SeekVideoPlayerTo(sub.StartTimeSec);
                OnSubtitleStyleChanged();
            }
        }

        private void PlaySubtitleSegment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is SubtitleItem sub)
            {
                if (_node != null)
                {
                    foreach (var s in _node.Subtitles)
                        s.IsSelected = (s == sub);
                }
                SeekVideoPlayerTo(sub.StartTimeSec);
                if (!_isPlaying) TogglePlayPause();
            }
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
                RefreshAvailableLanguageTags(isInitialLoad: false);
                _cachedActiveSubtitle = null;
                UpdateLiveSubtitleOverlay(_currentPlayheadSec);
            }
        }

        private void ShowSubtitleLoading(string title, string subText = "")
        {
            if (SubtitleLoadingTitleText != null) SubtitleLoadingTitleText.Text = title;
            if (SubtitleLoadingSubText != null) SubtitleLoadingSubText.Text = subText;
            if (SubtitleLoadingOverlay != null)
            {
                SubtitleLoadingOverlay.Visibility = Visibility.Visible;
            }
        }

        private void UpdateSubtitleLoading(string title, string subText = "")
        {
            if (SubtitleLoadingTitleText != null) SubtitleLoadingTitleText.Text = title;
            if (SubtitleLoadingSubText != null) SubtitleLoadingSubText.Text = subText;
        }

        private void HideSubtitleLoading()
        {
            if (SubtitleLoadingOverlay != null)
            {
                SubtitleLoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void ImportSubtitleFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file phụ đề",
                Filter = "Tất cả định dạng phụ đề (*.json;*.srt;*.vtt;*.ass;*.ssa)|*.json;*.srt;*.vtt;*.ass;*.ssa|JSON WhisperX Subtitle (*.json)|*.json|SubRip Subtitle (*.srt)|*.srt|Advanced SubStation Alpha (*.ass;*.ssa)|*.ass;*.ssa|WebVTT (*.vtt)|*.vtt|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var filePath = dialog.FileName;
                var fileName = Path.GetFileName(filePath);
                try
                {
                    ShowSubtitleLoading("Đang nhập phụ đề...", $"Đang đọc và phân tích file {fileName}...");
                    await Task.Delay(40); // Cho phép UI render animation loading trước khi thực thi

                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    List<SubtitleItem> parsed;

                    if (ext == ".json")
                    {
                        var rawJson = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                        parsed = await Task.Run(() => ParseJson(rawJson));
                    }
                    else
                    {
                        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
                        parsed = await Task.Run(() => ext switch
                        {
                            ".ass" or ".ssa" => ParseAss(lines),
                            ".vtt" => ParseVtt(lines),
                            _ => ParseSrt(lines)
                        });
                    }

                    if (parsed != null && parsed.Count > 0)
                    {
                        UpdateSubtitleLoading("Đang nạp vào danh sách...", $"Đang tải {parsed.Count} câu phụ đề vào timeline...");
                        await Task.Delay(20);

                        if (_node != null)
                        {
                            SubtitleItemsControl.ItemsSource = null;
                            _isBatchUpdatingSubtitles = true;
                            try
                            {
                                _node.Subtitles.Clear();
                                foreach (var item in parsed)
                                {
                                    _node.Subtitles.Add(item);
                                }

                                UpdateSubtitleBadge();
                                RefreshAvailableLanguageTags(isInitialLoad: true);
                            }
                            finally
                            {
                                _isBatchUpdatingSubtitles = false;
                                SubtitleItemsControl.ItemsSource = _node.Subtitles;
                            }
                        }

                        AppendLog($"📥 Đã nhập thành công {parsed.Count} câu phụ đề từ {fileName} (Mặc định: Bật phụ đề bản gốc)");
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
                finally
                {
                    HideSubtitleLoading();
                }
            }
        }

        private void ExportSubtitleFile(string format)
        {
            if (_node == null || _node.Subtitles.Count == 0)
            {
                AppendLog("⚠ Chưa có câu phụ đề nào để xuất file.");
                return;
            }

            var fmt = (format ?? "srt").Trim().ToLowerInvariant();
            var (ext, filter, title) = fmt switch
            {
                "json" => ("json", "JSON Subtitle (*.json)|*.json", "Lưu file phụ đề đa ngôn ngữ .JSON"),
                "ass" => ("ass", "Advanced SubStation Alpha (*.ass)|*.ass", "Lưu file phụ đề .ASS"),
                _ => ("srt", "SubRip Subtitle (*.srt)|*.srt", "Lưu file phụ đề .SRT")
            };

            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = $"subtitles_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string content = fmt switch
                    {
                        "json" => BuildJsonFileContent(_node.Subtitles),
                        "ass" => BuildAssFileContent(_node.Subtitles, _node.SubtitleStyle),
                        _ => BuildSrtFileContent(_node.Subtitles)
                    };
                    File.WriteAllText(dialog.FileName, content, Encoding.UTF8);
                    AppendLog($"💾 Đã xuất file phụ đề thành công: {Path.GetFileName(dialog.FileName)}");
                }
                catch (Exception ex)
                {
                    AppendLog($"❌ Lỗi xuất file phụ đề: {ex.Message}");
                }
            }
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
            ShowOnlyOriginalLanguage_Click(this, new RoutedEventArgs());
            OnSubtitleStyleChanged();
            AppendLog("🔄 Đã đặt lại toàn bộ cài đặt phụ đề về mặc định.");
        }

        public static List<SubtitleItem> ParseJson(string rawJson)
        {
            var result = new List<SubtitleItem>();
            if (string.IsNullOrWhiteSpace(rawJson)) return result;

            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                string sourceLang = "zh";
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("source_language", out var slProp) && slProp.ValueKind == JsonValueKind.String)
                        sourceLang = slProp.GetString() ?? "zh";
                }

                JsonElement arrayElem = default;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    arrayElem = root;
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("subtitles", out var subsProp) && subsProp.ValueKind == JsonValueKind.Array)
                        arrayElem = subsProp;
                    else if (root.TryGetProperty("segments", out var segsProp) && segsProp.ValueKind == JsonValueKind.Array)
                        arrayElem = segsProp;
                    else if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
                        arrayElem = dataProp;
                }

                if (arrayElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arrayElem.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object) continue;

                        double startSec = 0;
                        double endSec = 2.0;

                        if (item.TryGetProperty("start", out var stProp))
                        {
                            if (stProp.ValueKind == JsonValueKind.Number) startSec = stProp.GetDouble();
                            else if (stProp.ValueKind == JsonValueKind.String && double.TryParse(stProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) startSec = s;
                        }
                        else if (item.TryGetProperty("start_time", out var stStrProp) && stStrProp.ValueKind == JsonValueKind.String)
                        {
                            SubtitleItem.TryParseHms(stStrProp.GetString(), out startSec);
                        }

                        if (item.TryGetProperty("end", out var etProp))
                        {
                            if (etProp.ValueKind == JsonValueKind.Number) endSec = etProp.GetDouble();
                            else if (etProp.ValueKind == JsonValueKind.String && double.TryParse(etProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var e)) endSec = e;
                        }
                        else if (item.TryGetProperty("end_time", out var etStrProp) && etStrProp.ValueKind == JsonValueKind.String)
                        {
                            SubtitleItem.TryParseHms(etStrProp.GetString(), out endSec);
                        }

                        var text = item.TryGetProperty("text", out var tProp) && tProp.ValueKind == JsonValueKind.String ? tProp.GetString() : string.Empty;
                        var origText = item.TryGetProperty("original_text", out var otProp) && otProp.ValueKind == JsonValueKind.String ? otProp.GetString() : string.Empty;

                        var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        if (item.TryGetProperty("translations", out var transProp) && transProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in transProp.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.String)
                                    translations[prop.Name] = prop.Value.GetString() ?? string.Empty;
                            }
                        }

                        var subItem = new SubtitleItem
                        {
                            StartTimeSec = startSec,
                            EndTimeSec = Math.Max(startSec + 0.1, endSec),
                            SourceLanguage = sourceLang,
                            OriginalText = origText ?? string.Empty,
                            Text = text ?? string.Empty,
                            Translations = translations
                        };

                        subItem.EnsureLinesFromText();
                        result.Add(subItem);
                    }
                }
            }
            catch { }

            return result;
        }

        public static List<SubtitleItem> ParseSrt(string[] lines)
        {
            var result = new List<SubtitleItem>();
            var timeRegex = new Regex(@"(\d{1,2}:\d{2}:\d{2}[,\.]\d{2,3})\s*-->\s*(\d{1,2}:\d{2}:\d{2}[,\.]\d{2,3})");

            SubtitleItem? currentItem = null;
            var textLines = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    if (currentItem != null)
                    {
                        CommitSrtLines(currentItem, textLines);
                        if (!string.IsNullOrWhiteSpace(currentItem.Text) || !string.IsNullOrWhiteSpace(currentItem.OriginalText))
                            result.Add(currentItem);
                        currentItem = null;
                        textLines.Clear();
                    }
                    continue;
                }

                var match = timeRegex.Match(trimmed);
                if (match.Success)
                {
                    if (currentItem != null)
                    {
                        CommitSrtLines(currentItem, textLines);
                        if (!string.IsNullOrWhiteSpace(currentItem.Text) || !string.IsNullOrWhiteSpace(currentItem.OriginalText))
                            result.Add(currentItem);
                        textLines.Clear();
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
                    if (int.TryParse(trimmed, out _) && textLines.Count == 0) continue;
                    textLines.Add(trimmed);
                }
            }

            if (currentItem != null)
            {
                CommitSrtLines(currentItem, textLines);
                if (!string.IsNullOrWhiteSpace(currentItem.Text) || !string.IsNullOrWhiteSpace(currentItem.OriginalText))
                    result.Add(currentItem);
            }

            return result;
        }

        private static void CommitSrtLines(SubtitleItem item, List<string> textLines)
        {
            if (textLines.Count >= 2)
            {
                item.OriginalText = textLines[0];
                item.Text = textLines[1];
            }
            else if (textLines.Count == 1)
            {
                item.Text = textLines[0];
            }
            item.EnsureLinesFromText();
        }

        private static bool TryParseTime(string raw, out TimeSpan ts)
        {
            raw = raw.Replace(',', '.');
            return TimeSpan.TryParse(raw, out ts);
        }

        public static List<SubtitleItem> ParseVtt(string[] lines) => ParseSrt(lines);

        public static List<SubtitleItem> ParseAss(string[] lines)
        {
            var rawItems = new List<(double st, double et, string style, string text)>();
            var timeRegex = new Regex(@"Dialogue:\s*\d+,\s*(\d+:\d{2}:\d{2}\.\d{2}),\s*(\d+:\d{2}:\d{2}\.\d{2}),([^,]*),[^,]+,[^,]+,[^,]+,[^,]+,[^,]+,(.*)");

            foreach (var line in lines)
            {
                var match = timeRegex.Match(line);
                if (match.Success)
                {
                    if (TimeSpan.TryParse(match.Groups[1].Value, out var st) && TimeSpan.TryParse(match.Groups[2].Value, out var et))
                    {
                        var style = match.Groups[3].Value.Trim();
                        var text = Regex.Replace(match.Groups[4].Value, @"\{[^}]*\}", "").Replace(@"\N", Environment.NewLine).Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            rawItems.Add((st.TotalSeconds, et.TotalSeconds, style, text));
                        }
                    }
                }
            }

            var result = new List<SubtitleItem>();
            var used = new bool[rawItems.Count];

            for (int i = 0; i < rawItems.Count; i++)
            {
                if (used[i]) continue;

                var current = rawItems[i];
                used[i] = true;

                int matchedIdx = -1;
                for (int j = i + 1; j < rawItems.Count; j++)
                {
                    if (used[j]) continue;
                    if (Math.Abs(rawItems[j].st - current.st) < 0.25 && Math.Abs(rawItems[j].et - current.et) < 0.35)
                    {
                        matchedIdx = j;
                        break;
                    }
                }

                if (matchedIdx >= 0)
                {
                    used[matchedIdx] = true;
                    var other = rawItems[matchedIdx];

                    string orig = current.text;
                    string trans = other.text;

                    if (current.style.Equals("Translated", StringComparison.OrdinalIgnoreCase) ||
                        other.style.Equals("Default", StringComparison.OrdinalIgnoreCase))
                    {
                        trans = current.text;
                        orig = other.text;
                    }

                    var sub = new SubtitleItem
                    {
                        StartTimeSec = Math.Min(current.st, other.st),
                        EndTimeSec = Math.Max(current.et, other.et),
                        OriginalText = orig,
                        Text = trans
                    };
                    sub.EnsureLinesFromText();
                    result.Add(sub);
                }
                else
                {
                    var sub = new SubtitleItem
                    {
                        StartTimeSec = current.st,
                        EndTimeSec = current.et,
                        Text = current.text
                    };
                    sub.EnsureLinesFromText();
                    result.Add(sub);
                }
            }

            return result;
        }

        public static string BuildJsonFileContent(IEnumerable<SubtitleItem> subtitles)
        {
            var list = subtitles.ToList();
            var firstWithLang = list.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.SourceLanguage));
            var sourceLang = firstWithLang?.SourceLanguage ?? "zh";
            var sourceLangInfo = SubtitleLanguageHelper.GetLanguageInfo(sourceLang, isOriginal: true);

            var subObjs = new List<object>();
            int id = 1;
            double maxEnd = 0;

            foreach (var sub in list)
            {
                var st = TimeSpan.FromSeconds(sub.StartTimeSec);
                var et = TimeSpan.FromSeconds(sub.EndTimeSec);
                if (sub.EndTimeSec > maxEnd) maxEnd = sub.EndTimeSec;

                string orig = !string.IsNullOrWhiteSpace(sub.OriginalText) ? sub.OriginalText : (sub.Lines.Count >= 2 ? sub.Lines[0].Text : (sub.Lines.Count == 1 && sub.Lines[0].IsOriginal ? sub.Lines[0].Text : string.Empty));
                string trans = !string.IsNullOrWhiteSpace(sub.Text) ? sub.Text : (sub.Lines.Count >= 2 ? sub.Lines[1].Text : (sub.Lines.Count == 1 ? sub.Lines[0].Text : string.Empty));

                var translationsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (sub.Translations != null && sub.Translations.Count > 0)
                {
                    foreach (var kvp in sub.Translations)
                        translationsMap[kvp.Key] = kvp.Value;
                }
                else
                {
                    foreach (var l in sub.Lines)
                    {
                        if (!string.IsNullOrWhiteSpace(l.LanguageCode) && !string.IsNullOrWhiteSpace(l.Text))
                            translationsMap[l.LanguageCode] = l.Text;
                    }
                }

                if (!translationsMap.ContainsKey(sourceLang) && !string.IsNullOrWhiteSpace(orig))
                    translationsMap[sourceLang] = orig;
                if (!translationsMap.ContainsKey("vi") && !string.IsNullOrWhiteSpace(trans))
                    translationsMap["vi"] = trans;

                subObjs.Add(new
                {
                    id = id,
                    start = Math.Round(sub.StartTimeSec, 3),
                    end = Math.Round(sub.EndTimeSec, 3),
                    duration = Math.Round(sub.DurationSec, 3),
                    start_time = $"{(int)st.TotalHours:00}:{st.Minutes:00}:{st.Seconds:00},{st.Milliseconds:000}",
                    end_time = $"{(int)et.TotalHours:00}:{et.Minutes:00}:{et.Seconds:00},{et.Milliseconds:000}",
                    text = trans,
                    original_text = orig,
                    translations = translationsMap
                });
                id++;
            }

            var payload = new
            {
                version = "1.0",
                engine = "whisperx",
                source_language = sourceLang,
                source_language_name = sourceLangInfo.name,
                total_segments = list.Count,
                total_duration_seconds = Math.Round(maxEnd, 2),
                created_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                subtitles = subObjs
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        }

        public static string BuildSrtFileContent(IEnumerable<SubtitleItem> subtitles)
        {
            var sb = new StringBuilder();
            int idx = 1;
            foreach (var sub in subtitles)
            {
                var st = TimeSpan.FromSeconds(sub.StartTimeSec);
                var et = TimeSpan.FromSeconds(sub.EndTimeSec);

                var activeLines = sub.Lines.Where(l => l.IsActive && !string.IsNullOrWhiteSpace(l.Text)).ToList();
                if (activeLines.Count == 0) continue;

                sb.AppendLine(idx.ToString());
                sb.AppendLine($"{(int)st.TotalHours:00}:{st.Minutes:00}:{st.Seconds:00},{st.Milliseconds:000} --> {(int)et.TotalHours:00}:{et.Minutes:00}:{et.Seconds:00},{et.Milliseconds:000}");

                foreach (var line in activeLines)
                {
                    sb.AppendLine(line.Text);
                }
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
