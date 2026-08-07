// =========================================================================================================
// AI / DEVELOPER ARCHITECTURAL DIRECTIVE:
// This file is part of the partial class LayerAiDialog (Views/Overlays/LayerAiDialog).
// CRITICAL RULE: DO NOT BLOAT OR STUFF EXCESSIVE LOGIC INTO A SINGLE FILE.
// Maintain each partial class file at a maximum size of ~1000 - 1500 lines of code.
// When adding new features or handlers, refactor and create dedicated partial class files per module.
// =========================================================================================================

using FlowMy.Models.ImageEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.Overlays
{
    public class InlineImageRefInfo
    {
        public string Name { get; set; } = string.Empty;
        public string CodeId { get; set; } = string.Empty;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public BitmapSource? Bitmap { get; set; }
        public bool IsMainImage { get; set; }
        public bool IsAllSubImages { get; set; }
        public int SlotIndex { get; set; } = -1;
    }

    public partial class LayerAiDialog : Window
    {
        #region Prompt Image Suggestion (@ Popup & RichTextBox Inline Support)

        private System.Windows.Controls.Primitives.Popup? _promptSuggestPopup;
        private ListBox? _promptSuggestListBox;
        private RichTextBox? _activePromptRtb;

        private RichTextBox GetActivePromptRichTextBox()
        {
            if (_activeTab == ActiveTab.Prompt)
            {
                return TxtPrompt;
            }
            else if (_activeTab == ActiveTab.WebView)
            {
                return TxtPromptWv;
            }
            else if (_activeTab == ActiveTab.WebBrowser)
            {
                return TxtPromptWeb;
            }
            return TxtPrompt;
        }

        private void HookPromptRichTextBoxEvents(RichTextBox rtb)
        {
            HookPromptRichTextBox(rtb);
        }

        private void SetupPromptSuggestPopup()
        {
            _promptSuggestListBox = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x1c, 0x23)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                MaxHeight = 220,
                MinWidth = 220,
                FontSize = 11.5
            };

            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.White));
            itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0, 1, 0, 1)));
            itemStyle.Setters.Add(new Setter(ListBoxItem.CursorProperty, Cursors.Hand));

            var template = new ControlTemplate(typeof(ListBoxItem));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "Bd";
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(4, 3, 4, 3));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.BorderBrushProperty, Brushes.Transparent);

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenter);

            template.VisualTree = borderFactory;

            var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2a, 0x2e, 0x3d)), "Bd"));
            hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x4f, 0xff, 0xb0)), "Bd"));

            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x35, 0x3b, 0x50)), "Bd"));
            selectedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x4f, 0xff, 0xb0)), "Bd"));

            template.Triggers.Add(hoverTrigger);
            template.Triggers.Add(selectedTrigger);

            itemStyle.Setters.Add(new Setter(ListBoxItem.TemplateProperty, template));
            _promptSuggestListBox.ItemContainerStyle = itemStyle;

            _promptSuggestListBox.MouseLeftButtonUp += (s, e) =>
            {
                InlineImageRefInfo? info = null;
                if (_promptSuggestListBox.SelectedItem is ListBoxItem lbi && lbi.Tag is InlineImageRefInfo itemInfo)
                {
                    info = itemInfo;
                }
                else if (_promptSuggestListBox.SelectedItem is InlineImageRefInfo itemDirect)
                {
                    info = itemDirect;
                }

                if (info != null && _activePromptRtb != null)
                {
                    InsertImageInlineIntoRichTextBox(_activePromptRtb, info);
                    if (_promptSuggestPopup != null) _promptSuggestPopup.IsOpen = false;
                }
            };

            _promptSuggestListBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    InlineImageRefInfo? info = null;
                    if (_promptSuggestListBox.SelectedItem is ListBoxItem lbi && lbi.Tag is InlineImageRefInfo itemInfo)
                    {
                        info = itemInfo;
                    }
                    else if (_promptSuggestListBox.SelectedItem is InlineImageRefInfo itemDirect)
                    {
                        info = itemDirect;
                    }

                    if (info != null && _activePromptRtb != null)
                    {
                        InsertImageInlineIntoRichTextBox(_activePromptRtb, info);
                        if (_promptSuggestPopup != null) _promptSuggestPopup.IsOpen = false;
                    }
                }
            };

            _promptSuggestPopup = new System.Windows.Controls.Primitives.Popup
            {
                StaysOpen = false,
                AllowsTransparency = true,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                Child = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x1c, 0x23)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x2a, 0x2e, 0x3d)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(2),
                    Child = _promptSuggestListBox
                }
            };
        }

        private void HookPromptRichTextBox(RichTextBox rtb)
        {
            if (rtb == null) return;
            SetupPromptSuggestPopup();

            rtb.TextChanged += (s, e) =>
            {
                CheckAndTriggerPromptSuggestPopup(rtb);
            };

            rtb.KeyDown += (s, e) =>
            {
                if (_promptSuggestPopup != null && _promptSuggestPopup.IsOpen)
                {
                    if (e.Key == Key.Down && _promptSuggestListBox != null)
                    {
                        _promptSuggestListBox.Focus();
                        if (_promptSuggestListBox.Items.Count > 0 && _promptSuggestListBox.SelectedIndex < 0)
                        {
                            _promptSuggestListBox.SelectedIndex = 0;
                        }
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Escape)
                    {
                        _promptSuggestPopup.IsOpen = false;
                        e.Handled = true;
                    }
                }
            };
        }

        private void CheckAndTriggerPromptSuggestPopup(RichTextBox rtb)
        {
            if (!rtb.IsKeyboardFocused) return;

            var caret = rtb.CaretPosition;
            if (caret == null) return;

            var textBefore = caret.GetTextInRun(LogicalDirection.Backward);
            int atIdx = textBefore.LastIndexOf('@');

            if (atIdx >= 0)
            {
                string query = textBefore.Substring(atIdx + 1);
                if (!query.Contains(' ') && !query.Contains('\n'))
                {
                    ShowPromptSuggestPopup(rtb, query);
                    return;
                }
            }

            if (_promptSuggestPopup != null)
            {
                _promptSuggestPopup.IsOpen = false;
            }
        }

        private void ShowPromptSuggestPopup(RichTextBox rtb, string query)
        {
            if (_promptSuggestPopup == null || _promptSuggestListBox == null) return;
            _activePromptRtb = rtb;

            _promptSuggestListBox.Items.Clear();

            var suggestions = GetAvailableImageSuggestions(query);
            if (suggestions.Count == 0)
            {
                _promptSuggestPopup.IsOpen = false;
                return;
            }

            foreach (var item in suggestions)
            {
                var itemContainer = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(4, 3, 4, 3)
                };

                var borderThumb = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x2e, 0x3d)),
                    Margin = new Thickness(0, 0, 6, 0),
                    ClipToBounds = true
                };

                if (item.Bitmap != null)
                {
                    var img = new Image
                    {
                        Source = item.Bitmap,
                        Stretch = Stretch.UniformToFill
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    borderThumb.Child = img;
                }
                else
                {
                    borderThumb.Child = new TextBlock
                    {
                        Text = item.IsAllSubImages ? "🖼️*" : "📸",
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brushes.White
                    };
                }
                itemContainer.Children.Add(borderThumb);

                var txtName = new TextBlock
                {
                    Text = item.Name,
                    Foreground = Brushes.White,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold
                };
                itemContainer.Children.Add(txtName);

                if (!string.IsNullOrEmpty(item.CodeId))
                {
                    var txtCode = new TextBlock
                    {
                        Text = $" (@{item.CodeId})",
                        Foreground = FindResource("TextMuted") as Brush ?? Brushes.Gray,
                        FontSize = 9.5,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 0, 0)
                    };
                    itemContainer.Children.Add(txtCode);
                }

                var listboxItem = new ListBoxItem
                {
                    Content = itemContainer,
                    Tag = item,
                    Cursor = Cursors.Hand
                };
                _promptSuggestListBox.Items.Add(listboxItem);
            }

            _promptSuggestPopup.PlacementTarget = rtb;
            _promptSuggestPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;

            var rect = rtb.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
            _promptSuggestPopup.HorizontalOffset = rect.X;
            _promptSuggestPopup.VerticalOffset = rect.Y + rect.Height + 2;

            _promptSuggestPopup.IsOpen = true;
        }

        private List<InlineImageRefInfo> GetAvailableImageSuggestions(string query)
        {
            var list = new List<InlineImageRefInfo>();

            string mainCodeId = string.IsNullOrWhiteSpace(_activeLayer?.CodeId) ? string.Empty : _activeLayer.CodeId;

            // 1. Main Image
            BitmapSource? mainBmp = _activeLayer?.Bitmap ?? (ImgPreview?.Source as BitmapSource);
            var mainInfo = new InlineImageRefInfo
            {
                Name = "Ảnh chính",
                CodeId = mainCodeId,
                Bitmap = mainBmp,
                IsMainImage = true
            };
            if (MatchesQuery(mainInfo, query)) list.Add(mainInfo);

            // 2. All Secondary Images (combined)
            var allSubInfo = new InlineImageRefInfo
            {
                Name = "Tất cả ảnh con",
                CodeId = "Tat_Ca_Anh_con",
                IsAllSubImages = true
            };
            if (MatchesQuery(allSubInfo, query)) list.Add(allSubInfo);

            // 3. Individual Secondary Images
            for (int i = 0; i < _secondaryImages.Count; i++)
            {
                var slot = _secondaryImages[i];
                if (slot.HasImage)
                {
                    var slotInfo = new InlineImageRefInfo
                    {
                        Name = $"Ảnh phụ {i + 1}",
                        CodeId = slot.CodeId,
                        Bitmap = slot.Bitmap,
                        SlotIndex = i
                    };
                    if (MatchesQuery(slotInfo, query)) list.Add(slotInfo);
                }
            }

            return list;
        }

        private static bool MatchesQuery(InlineImageRefInfo info, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            query = query.ToLowerInvariant();
            return info.Name.ToLowerInvariant().Contains(query) ||
                   info.CodeId.ToLowerInvariant().Contains(query);
        }

        private static InlineUIContainer CreateInlineImageContainer(InlineImageRefInfo info, Action<InlineUIContainer> onDelete)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x2e, 0x3d)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x4f, 0xff, 0xb0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(3, 1, 3, 1),
                Margin = new Thickness(2, 0, 2, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Tag = info
            };

            var grid = new Grid();

            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var borderThumb = new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new CornerRadius(2),
                Background = Brushes.Black,
                Margin = new Thickness(0, 0, 4, 0),
                ClipToBounds = true
            };

            if (info.Bitmap != null)
            {
                var img = new Image
                {
                    Source = info.Bitmap,
                    Stretch = Stretch.UniformToFill
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                borderThumb.Child = img;
            }
            else
            {
                borderThumb.Child = new TextBlock
                {
                    Text = info.IsAllSubImages ? "🖼️*" : "📸",
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.White
                };
            }
            sp.Children.Add(borderThumb);

            var txtName = new TextBlock
            {
                Text = info.Name,
                Foreground = Brushes.White,
                FontSize = 10.5,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(txtName);

            grid.Children.Add(sp);

            var btnClose = new Button
            {
                Content = "✕",
                Width = 14,
                Height = 14,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(220, 220, 53, 69)),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0)
            };

            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;
            btnClose.Template = template;

            sp.Children.Add(btnClose);

            border.Child = grid;
            border.MouseEnter += (s, e) => { btnClose.Visibility = Visibility.Visible; };
            border.MouseLeave += (s, e) => { btnClose.Visibility = Visibility.Collapsed; };

            var container = new InlineUIContainer(border) { BaselineAlignment = BaselineAlignment.Center };

            btnClose.Click += (s, e) =>
            {
                e.Handled = true;
                onDelete?.Invoke(container);
            };

            return container;
        }

        private void InsertImageInlineIntoRichTextBox(RichTextBox rtb, InlineImageRefInfo info)
        {
            if (rtb == null || info == null) return;

            var caret = rtb.CaretPosition;
            if (caret == null) return;

            rtb.BeginChange();
            try
            {
                var textBefore = caret.GetTextInRun(LogicalDirection.Backward);
                int atIdx = textBefore.LastIndexOf('@');
                if (atIdx >= 0)
                {
                    int deleteLen = textBefore.Length - atIdx;
                    var startDelete = caret.GetPositionAtOffset(-deleteLen, LogicalDirection.Backward);
                    if (startDelete != null)
                    {
                        var range = new TextRange(startDelete, caret);
                        range.Text = string.Empty;
                    }
                }

                var container = CreateInlineImageContainer(info, (c) => DeleteInlineContainerFromRichTextBox(rtb, c));

                var p = rtb.CaretPosition.Paragraph;
                if (p == null)
                {
                    p = new Paragraph();
                    rtb.Document.Blocks.Add(p);
                }

                var insertPointer = rtb.CaretPosition;
                if (insertPointer.Parent is Inline targetInline)
                {
                    p.Inlines.InsertAfter(targetInline, container);
                }
                else
                {
                    p.Inlines.Add(container);
                }

                var spaceRun = new Run(" ");
                p.Inlines.InsertAfter(container, spaceRun);

                rtb.CaretPosition = spaceRun.ElementEnd;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to insert inline image: {ex.Message}");
            }
            finally
            {
                rtb.EndChange();
            }
        }

        private static void DeleteInlineContainerFromRichTextBox(RichTextBox rtb, InlineUIContainer container)
        {
            if (rtb == null || container == null) return;

            try
            {
                var p = container.Parent as Paragraph;
                if (p != null)
                {
                    p.Inlines.Remove(container);
                }
            }
            catch { }
        }

        private string GetPromptText(RichTextBox rtb)
        {
            if (rtb == null) return string.Empty;

            var sb = new StringBuilder();
            foreach (var block in rtb.Document.Blocks)
            {
                if (block is Paragraph p)
                {
                    foreach (var inline in p.Inlines)
                    {
                        if (inline is Run r)
                        {
                            sb.Append(r.Text);
                        }
                        else if (inline is InlineUIContainer container && container.Child is Border border && border.Tag is InlineImageRefInfo info)
                        {
                            if (info.IsMainImage)
                            {
                                sb.Append("@" + info.CodeId);
                            }
                            else if (info.IsAllSubImages)
                            {
                                sb.Append("@Tat_Ca_Anh_con");
                            }
                            else if (!string.IsNullOrEmpty(info.CodeId))
                            {
                                sb.Append("@" + info.CodeId);
                            }
                        }
                    }
                    sb.AppendLine();
                }
            }
            string result = sb.ToString();
            if (rtb.Document.Blocks.Count <= 1)
            {
                result = result.TrimEnd('\r', '\n');
            }
            return result;
        }

        private void SetPromptText(RichTextBox rtb, string text)
        {
            if (rtb == null) return;

            bool wasUndoEnabled = rtb.IsUndoEnabled;
            rtb.IsUndoEnabled = false;

            try
            {
                rtb.Document.Blocks.Clear();
                var p = new Paragraph();

                if (string.IsNullOrEmpty(text))
                {
                    rtb.Document.Blocks.Add(p);
                    return;
                }

                string mainCodeId = string.IsNullOrWhiteSpace(_activeLayer?.CodeId) ? string.Empty : _activeLayer.CodeId;

                var tokens = System.Text.RegularExpressions.Regex.Split(text, @"(@[a-zA-Z0-9_]+)");
                foreach (var token in tokens)
                {
                    if (string.IsNullOrEmpty(token)) continue;

                    if (token.StartsWith("@"))
                    {
                        string codeIdToken = token.Substring(1);

                        InlineImageRefInfo? matchedInfo = null;

                        if (codeIdToken.Equals("Tat_Ca_Anh_con", StringComparison.OrdinalIgnoreCase))
                        {
                            matchedInfo = new InlineImageRefInfo
                            {
                                Name = "Tất cả ảnh con",
                                CodeId = "Tat_Ca_Anh_con",
                                IsAllSubImages = true
                            };
                        }
                        else if (!string.IsNullOrEmpty(mainCodeId) && codeIdToken.Equals(mainCodeId, StringComparison.OrdinalIgnoreCase))
                        {
                            BitmapSource? mainBmp = _activeLayer?.Bitmap ?? (ImgPreview?.Source as BitmapSource);
                            matchedInfo = new InlineImageRefInfo
                            {
                                Name = "Ảnh chính",
                                CodeId = mainCodeId,
                                Bitmap = mainBmp,
                                IsMainImage = true
                            };
                        }
                        else
                        {
                            var secMatch = _secondaryImages.FirstOrDefault(s => s.HasImage && !string.IsNullOrEmpty(s.CodeId) && s.CodeId.Equals(codeIdToken, StringComparison.OrdinalIgnoreCase));
                            if (secMatch != null)
                            {
                                int idx = _secondaryImages.IndexOf(secMatch);
                                matchedInfo = new InlineImageRefInfo
                                {
                                    Name = $"Ảnh phụ {idx + 1}",
                                    CodeId = secMatch.CodeId,
                                    Bitmap = secMatch.Bitmap,
                                    SlotIndex = idx
                                };
                            }
                        }

                        if (matchedInfo != null)
                        {
                            var container = CreateInlineImageContainer(matchedInfo, (c) => DeleteInlineContainerFromRichTextBox(rtb, c));
                            p.Inlines.Add(container);
                            continue;
                        }
                    }

                    p.Inlines.Add(new Run(token));
                }

                rtb.Document.Blocks.Add(p);
            }
            finally
            {
                rtb.IsUndoEnabled = wasUndoEnabled;
            }
        }

        #endregion
    }
}
