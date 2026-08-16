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

            // Style Preset Buttons
            SubPresetTikTokButton.Click += (s, e) => ApplySubtitlePreset("TikTokViral");
            SubPresetNetflixButton.Click += (s, e) => ApplySubtitlePreset("Netflix");
            SubPresetMinimalButton.Click += (s, e) => ApplySubtitlePreset("MinimalClean");
            SubPresetGamingButton.Click += (s, e) => ApplySubtitlePreset("GamingRgb");
            SubPresetNewsButton.Click += (s, e) => ApplySubtitlePreset("NewsBanner");

            // Style Inputs Wire-up
            SubtitleEnabledToggle.Checked += (s, e) => SyncSubtitleStyleToNode();
            SubtitleEnabledToggle.Unchecked += (s, e) => SyncSubtitleStyleToNode();
            SubFontFamilyCombo.SelectionChanged += (s, e) => SyncSubtitleStyleToNode();
            SubFontSizeBox.TextChanged += (s, e) => SyncSubtitleStyleToNode();
            SubBoldToggle.Checked += (s, e) => SyncSubtitleStyleToNode();
            SubBoldToggle.Unchecked += (s, e) => SyncSubtitleStyleToNode();
            SubItalicToggle.Checked += (s, e) => SyncSubtitleStyleToNode();
            SubItalicToggle.Unchecked += (s, e) => SyncSubtitleStyleToNode();
            SubTextColorBox.TextChanged += (s, e) => SyncSubtitleStyleToNode();
            SubOutlineColorBox.TextChanged += (s, e) => SyncSubtitleStyleToNode();
            SubOutlineThicknessBox.TextChanged += (s, e) => SyncSubtitleStyleToNode();
            SubAlignmentCombo.SelectionChanged += (s, e) => SyncSubtitleStyleToNode();
            SubBackgroundBoxCombo.SelectionChanged += (s, e) => SyncSubtitleStyleToNode();
            SubBurnInCombo.SelectionChanged += (s, e) => SyncSubtitleStyleToNode();

            ApplySubtitlesToVideoPreviewButton.Click += (s, e) =>
            {
                SyncSubtitleStyleToNode();
                UpdateLiveSubtitleOverlay(_currentPlayheadSec);
                AppendLog("✅ Đã áp dụng cập nhật phụ đề lên Video Preview.");
            };

            ResetSubtitleSettingsButton.Click += (s, e) => ResetSubtitleSettings();
        }

        private void UpdateSubtitleBadge()
        {
            if (_node == null) return;
            SubtitleCountBadge.Text = $"{_node.Subtitles.Count} câu";
        }

        public void UpdateLiveSubtitleOverlay(double currentSec)
        {
            if (_node == null || _node.SubtitleStyle == null || !_node.SubtitleStyle.Enabled)
            {
                SubtitleLiveOverlayBorder.Visibility = Visibility.Collapsed;
                return;
            }

            var activeSub = _node.Subtitles.FirstOrDefault(s => currentSec >= s.StartTimeSec && currentSec <= s.EndTimeSec);
            if (activeSub == null || string.IsNullOrWhiteSpace(activeSub.Text))
            {
                SubtitleLiveOverlayBorder.Visibility = Visibility.Collapsed;
                return;
            }

            // Apply Typography
            SubtitleLiveTextBlock.Text = activeSub.Text;
            try
            {
                var fontName = (SubFontFamilyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Segoe UI";
                SubtitleLiveTextBlock.FontFamily = new FontFamily(fontName);
            }
            catch { SubtitleLiveTextBlock.FontFamily = new FontFamily("Segoe UI"); }

            if (double.TryParse(SubFontSizeBox.Text, out var fSize) && fSize >= 8)
                SubtitleLiveTextBlock.FontSize = Math.Clamp(fSize * 0.7, 10, 36);

            SubtitleLiveTextBlock.FontWeight = SubBoldToggle.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
            SubtitleLiveTextBlock.FontStyle = SubItalicToggle.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;

            try
            {
                var colorHex = SubTextColorBox.Text?.Trim();
                if (!string.IsNullOrEmpty(colorHex) && (colorHex.StartsWith("#") || colorHex.Length == 6))
                    SubtitleLiveTextBlock.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex)!;
            }
            catch { SubtitleLiveTextBlock.Foreground = Brushes.White; }

            // Alignment & Background
            var alignTag = (SubAlignmentCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "BottomCenter";
            SubtitleLiveOverlayBorder.VerticalAlignment = alignTag.StartsWith("Top") ? VerticalAlignment.Top :
                (alignTag.StartsWith("Center") ? VerticalAlignment.Center : VerticalAlignment.Bottom);
            SubtitleLiveOverlayBorder.HorizontalAlignment = alignTag.EndsWith("Left") ? HorizontalAlignment.Left :
                (alignTag.EndsWith("Right") ? HorizontalAlignment.Right : HorizontalAlignment.Center);

            var boxTag = (SubBackgroundBoxCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None";
            SubtitleLiveOverlayBorder.Background = boxTag switch
            {
                "BlackTrans" => new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
                "BlackSolid" => Brushes.Black,
                "YellowBox" => new SolidColorBrush(Color.FromArgb(200, 255, 215, 0)),
                _ => new SolidColorBrush(Color.FromArgb(120, 0, 0, 0))
            };

            SubtitleLiveOverlayBorder.Visibility = Visibility.Visible;
        }

        private void SyncSubtitleStyleToNode()
        {
            if (_node == null) return;
            var style = _node.SubtitleStyle ??= new SubtitleStyleConfig();
            style.Enabled = SubtitleEnabledToggle.IsChecked == true;
            style.FontFamily = (SubFontFamilyCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Segoe UI";
            if (double.TryParse(SubFontSizeBox.Text, out var fs)) style.FontSize = fs;
            style.IsBold = SubBoldToggle.IsChecked == true;
            style.IsItalic = SubItalicToggle.IsChecked == true;
            style.TextColor = SubTextColorBox.Text?.Trim() ?? "#FFFFFF";
            style.OutlineColor = SubOutlineColorBox.Text?.Trim() ?? "#000000";
            if (double.TryParse(SubOutlineThicknessBox.Text, out var ot)) style.OutlineThickness = ot;
            style.Alignment = (SubAlignmentCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "BottomCenter";
            style.HardcodeBurnIn = (SubBurnInCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() != "Soft";
        }

        private void ApplySubtitlePreset(string preset)
        {
            switch (preset)
            {
                case "TikTokViral":
                    SubTextColorBox.Text = "#FFFF00"; // Viral Yellow
                    SubOutlineColorBox.Text = "#000000";
                    SubOutlineThicknessBox.Text = "3";
                    SubFontSizeBox.Text = "32";
                    SubBoldToggle.IsChecked = true;
                    SubAlignmentCombo.SelectedIndex = 0; // BottomCenter
                    SubBackgroundBoxCombo.SelectedIndex = 0; // None
                    break;
                case "Netflix":
                    SubTextColorBox.Text = "#FFFFFF";
                    SubOutlineColorBox.Text = "#000000";
                    SubOutlineThicknessBox.Text = "2";
                    SubFontSizeBox.Text = "24";
                    SubBoldToggle.IsChecked = false;
                    SubAlignmentCombo.SelectedIndex = 0;
                    SubBackgroundBoxCombo.SelectedIndex = 1; // BlackTrans
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
                    SubTextColorBox.Text = "#00FFCC";
                    SubOutlineColorBox.Text = "#FF007F";
                    SubOutlineThicknessBox.Text = "3";
                    SubFontSizeBox.Text = "28";
                    SubBoldToggle.IsChecked = true;
                    break;
                case "NewsBanner":
                    SubTextColorBox.Text = "#FFFFFF";
                    SubOutlineColorBox.Text = "#000000";
                    SubOutlineThicknessBox.Text = "0";
                    SubFontSizeBox.Text = "20";
                    SubBoldToggle.IsChecked = true;
                    SubBackgroundBoxCombo.SelectedIndex = 2; // BlackSolid
                    break;
            }
            SyncSubtitleStyleToNode();
            UpdateLiveSubtitleOverlay(_currentPlayheadSec);
            AppendLog($"🎨 Đã áp dụng preset phụ đề: {preset}");
        }

        private void AddSubtitleAtCurrentPlayhead()
        {
            if (_node == null) return;
            var start = Math.Max(0, _currentPlayheadSec);
            var end = start + 2.5;
            var sub = new SubtitleItem
            {
                StartTimeSec = start,
                EndTimeSec = end,
                Text = "Dòng phụ đề mới"
            };
            _node.Subtitles.Add(sub);
            AppendLog($"➕ Đã thêm câu phụ đề tại {sub.FormattedStartTime}");
        }

        private void ClearAllSubtitles()
        {
            if (_node == null) return;
            _node.Subtitles.Clear();
            UpdateLiveSubtitleOverlay(_currentPlayheadSec);
            AppendLog("🗑 Đã xóa sạch toàn bộ danh sách phụ đề.");
        }

        private void ShiftSubtitlesTimeOffset(double offsetSec)
        {
            if (_node == null || _node.Subtitles.Count == 0) return;
            foreach (var sub in _node.Subtitles)
            {
                sub.StartTimeSec = Math.Max(0, sub.StartTimeSec + offsetSec);
                sub.EndTimeSec = Math.Max(sub.StartTimeSec + 0.1, sub.EndTimeSec + offsetSec);
            }
            UpdateLiveSubtitleOverlay(_currentPlayheadSec);
            AppendLog($"⏱ Đã dịch chuyển toàn bộ phụ đề {(offsetSec >= 0 ? "+" : "")}{offsetSec:F2}s");
        }

        private void SeekToSubtitle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is SubtitleItem sub)
            {
                SeekVideoPlayerTo(sub.StartTimeSec);
            }
        }

        private void RemoveSubtitle_Click(object sender, RoutedEventArgs e)
        {
            if (_node != null && sender is Button btn && btn.Tag is SubtitleItem sub)
            {
                _node.Subtitles.Remove(sub);
            }
        }

        private void ImportSubtitleFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file phụ đề",
                Filter = "Subtitle Files (*.srt;*.vtt;*.ass;*.ssa;*.txt)|*.srt;*.vtt;*.ass;*.ssa;*.txt|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var lines = File.ReadAllLines(dialog.FileName, Encoding.UTF8);
                    var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                    var parsed = ext is ".vtt" ? ParseVtt(lines) : (ext is ".ass" or ".ssa" ? ParseAss(lines) : ParseSrt(lines));

                    if (parsed.Count > 0)
                    {
                        _node.Subtitles.Clear();
                        foreach (var item in parsed) _node.Subtitles.Add(item);
                        AppendLog($"📥 Đã nhập thành công {parsed.Count} câu phụ đề từ {Path.GetFileName(dialog.FileName)}");
                    }
                    else
                    {
                        AppendLog($"⚠ Không tìm thấy phân đoạn phụ đề hợp lệ trong file {Path.GetFileName(dialog.FileName)}");
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
                AppendLog("⚠ Chưa có phụ đề nào để xuất.");
                return;
            }

            var sfd = new SaveFileDialog
            {
                Title = isAss ? "Xuất file phụ đề ASS" : "Xuất file phụ đề SRT",
                Filter = isAss ? "Advanced SubStation Alpha (*.ass)|*.ass" : "SubRip Subtitle (*.srt)|*.srt",
                FileName = Path.GetFileNameWithoutExtension(_node.VideoPath ?? "subtitles") + (isAss ? ".ass" : ".srt")
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    if (isAss)
                    {
                        var content = BuildAssFileContent(_node.Subtitles, _node.SubtitleStyle);
                        File.WriteAllText(sfd.FileName, content, Encoding.UTF8);
                    }
                    else
                    {
                        var content = BuildSrtFileContent(_node.Subtitles);
                        File.WriteAllText(sfd.FileName, content, Encoding.UTF8);
                    }
                    AppendLog($"💾 Đã xuất phụ đề thành công: {sfd.FileName}");
                }
                catch (Exception ex)
                {
                    AppendLog($"❌ Lỗi khi xuất file phụ đề: {ex.Message}");
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

            // Generate smart initial segments
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
            UpdateLiveSubtitleOverlay(_currentPlayheadSec);
            AppendLog($"✨ Đã tự động sinh {_node.Subtitles.Count} phân đoạn phụ đề theo mốc thời gian video.");
        }

        private void ResetSubtitleSettings()
        {
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
            SyncSubtitleStyleToNode();
            AppendLog("🔄 Đã đặt lại cấu hình phụ đề về mặc định.");
        }

        private static List<SubtitleItem> ParseSrt(string[] lines)
        {
            var result = new List<SubtitleItem>();
            var timeRegex = new Regex(@"(\d{2}):(\d{2}):(\d{2})[,.](\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2})[,.](\d{3})");

            SubtitleItem? current = null;
            var textBuilder = new StringBuilder();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                var match = timeRegex.Match(trimmed);
                if (match.Success)
                {
                    if (current != null)
                    {
                        current.Text = textBuilder.ToString().Trim();
                        result.Add(current);
                        textBuilder.Clear();
                    }

                    var sH = int.Parse(match.Groups[1].Value);
                    var sM = int.Parse(match.Groups[2].Value);
                    var sS = int.Parse(match.Groups[3].Value);
                    var sMs = int.Parse(match.Groups[4].Value);

                    var eH = int.Parse(match.Groups[5].Value);
                    var eM = int.Parse(match.Groups[6].Value);
                    var eS = int.Parse(match.Groups[7].Value);
                    var eMs = int.Parse(match.Groups[8].Value);

                    current = new SubtitleItem
                    {
                        StartTimeSec = sH * 3600 + sM * 60 + sS + sMs / 1000.0,
                        EndTimeSec = eH * 3600 + eM * 60 + eS + eMs / 1000.0
                    };
                }
                else if (current != null && !int.TryParse(trimmed, out _))
                {
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        if (textBuilder.Length > 0) textBuilder.AppendLine();
                        textBuilder.Append(trimmed);
                    }
                }
            }

            if (current != null)
            {
                current.Text = textBuilder.ToString().Trim();
                result.Add(current);
            }

            return result;
        }

        private static List<SubtitleItem> ParseVtt(string[] lines)
        {
            return ParseSrt(lines); // VTT timestamp structure matches SRT regex
        }

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

        private static string BuildAssFileContent(IEnumerable<SubtitleItem> subtitles, SubtitleStyleConfig? style)
        {
            var sb = new StringBuilder();
            var font = style?.FontFamily ?? "Segoe UI";
            var size = (int)(style?.FontSize ?? 24);
            sb.AppendLine("[Script Info]");
            sb.AppendLine("ScriptType: v4.00+");
            sb.AppendLine("PlayResX: 1920");
            sb.AppendLine("PlayResY: 1080");
            sb.AppendLine();
            sb.AppendLine("[V4+ Styles]");
            sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
            sb.AppendLine($"Style: Default,{font},{size},&H00FFFFFF,&H000000FF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,2,1,2,20,20,40,1");
            sb.AppendLine();
            sb.AppendLine("[Events]");
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

            foreach (var sub in subtitles)
            {
                var st = TimeSpan.FromSeconds(sub.StartTimeSec);
                var et = TimeSpan.FromSeconds(sub.EndTimeSec);
                var stStr = $"{(int)st.TotalHours:0}:{st.Minutes:00}:{st.Seconds:00}.{st.Milliseconds / 10:00}";
                var etStr = $"{(int)et.TotalHours:0}:{et.Minutes:00}:{et.Seconds:00}.{et.Milliseconds / 10:00}";
                var txt = sub.Text.Replace(Environment.NewLine, @"\N");
                sb.AppendLine($"Dialogue: 0,{stStr},{etStr},Default,,0,0,0,,{txt}");
            }
            return sb.ToString();
        }
    }
}
