using FlowMy.Helpers;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using FlowMy.Views.NodeControls;
using CefSharp;
using CefSharp.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.Overlays
{
    public partial class LayerAiDialog : Window
    {
        #region General Helpers

        private void CleanupPlaceholders(List<EditorLayer> placeholders, EditorLayer parent, Window? owner)
        {
            foreach (var placeholder in placeholders)
            {
                // Mark as error state instead of removing — user can delete manually
                placeholder.IsLoading = false;
                placeholder.StopLoadingTimer(isError: true);
                placeholder.IsLoadingError = true;
                placeholder.Name = placeholder.Name + " (Lỗi)";
            }
            if (owner != null)
            {
                var panel = FindVisualChild<ImageEditorPanel>(owner);
                panel?.RefreshLayersList();
            }
        }

        private FlowMy.ViewModels.WorkflowEditorViewModel? _subscribedVm;

        private void SubscribeToViewModelEvents()
        {
            UnsubscribeFromViewModelEvents();
            if (_host?.ViewModel is FlowMy.ViewModels.WorkflowEditorViewModel vm)
            {
                _subscribedVm = vm;
                vm.PropertyChanged += HostViewModel_PropertyChanged;
                CheckWorkflowExecutionState();
            }
        }

        private void UnsubscribeFromViewModelEvents()
        {
            if (_subscribedVm != null)
            {
                _subscribedVm.PropertyChanged -= HostViewModel_PropertyChanged;
                _subscribedVm = null;
            }
        }

        private void HostViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FlowMy.ViewModels.WorkflowEditorViewModel.IsExecuting) ||
                e.PropertyName == nameof(FlowMy.ViewModels.WorkflowEditorViewModel.HasRunningNodes))
            {
                CheckWorkflowExecutionState();
            }
        }

        private void CheckWorkflowExecutionState()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_subscribedVm != null && !_subscribedVm.IsExecuting && !_subscribedVm.HasRunningNodes)
                {
                    SetButtonsLoading(false);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ResetButtons()
        {
            SetButtonsLoading(false);
        }

        private void SetButtonsLoading(bool isLoading)
        {
            _isAiLoading = false;
            BtnCancel.IsEnabled = true;
            if (BtnApply != null) BtnApply.IsEnabled = true;
            UpdateSendButtonsState();

            BtnSend.Content = CreatePlayIconPath();
            if (BtnSendWv != null) BtnSendWv.Content = CreatePlayIconPath();
            if (BtnSendWeb != null) BtnSendWeb.Content = CreatePlayIconPath();
        }

        private System.Windows.Shapes.Path CreatePlayIconPath()
        {
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 3 2 L 13 8 L 3 14 Z"),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111318")),
                Width = 10,
                Height = 10,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(2, 0, 0, 0)
            };
            return path;
        }

        private void TxtPrompt_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSendButtonsState();
        }




        private void UpdateSendButtonsState()
        {
            if (BtnSend != null) BtnSend.IsEnabled = true;
            if (BtnSendWv != null) BtnSendWv.IsEnabled = true;
            if (BtnSendWeb != null) BtnSendWeb.IsEnabled = true;
        }

        private void ApplySendModeUi()
        {
            var onStyle = FindResource("SuccessButton") as Style;
            var offStyle = FindResource("DangerButton") as Style;
            string text = _sendModeOn ? "Gửi AI: ON" : "Gửi AI: OFF";
            var style = _sendModeOn ? onStyle : offStyle;

            if (BtnToggleSendMode != null)
            {
                BtnToggleSendMode.Content = text;
                BtnToggleSendMode.Style = style;
            }
            if (BtnToggleSendModeExpanded != null)
            {
                BtnToggleSendModeExpanded.Content = text;
                BtnToggleSendModeExpanded.Style = style;
            }

            var sendVisibility = _sendModeOn ? Visibility.Visible : Visibility.Collapsed;
            var applyVisibility = _sendModeOn ? Visibility.Collapsed : Visibility.Visible;
            var cancelMargin = _sendModeOn ? new Thickness(0) : new Thickness(0, 0, 8, 0);

            if (BtnSend != null) BtnSend.Visibility = sendVisibility;
            if (BtnSendWv != null) BtnSendWv.Visibility = sendVisibility;
            if (BtnSendWeb != null) BtnSendWeb.Visibility = sendVisibility;

            if (BtnApply != null) BtnApply.Visibility = applyVisibility;
            if (BtnCancel != null) BtnCancel.Margin = cancelMargin;

            // Toggle batch size vs slot count selector in bottom bar
            if (PanelBatchSize != null) PanelBatchSize.Visibility = _sendModeOn ? Visibility.Visible : Visibility.Collapsed;
            if (PanelSlotCount != null) PanelSlotCount.Visibility = _sendModeOn ? Visibility.Collapsed : Visibility.Visible;

            if (!_sendModeOn)
            {
                // Enforce single select for slots in OFF mode: deselect all but the first selected slot
                int firstSelectedIdx = -1;
                for (int i = 0; i < _secondaryImages.Count; i++)
                {
                    if (_secondaryImages[i] != null && _secondaryImages[i].HasImage && _secondaryImages[i].IsSelected)
                    {
                        if (firstSelectedIdx == -1)
                        {
                            firstSelectedIdx = i;
                        }
                        else
                        {
                            _secondaryImages[i].IsSelected = false;
                        }
                    }
                }
                RefreshAllSlotsUI();
                UpdateSecondaryInfo();
            }

            UpdateSendButtonsState();
            UpdatePreviewImage();
        }

        private void BtnToggleSendMode_Click(object sender, RoutedEventArgs e)
        {
            _sendModeOn = !_sendModeOn;
            if (_node != null)
            {
                _node.LayerAiSendModeOn = _sendModeOn;
            }
            ApplySendModeUi();
        }

        private bool _promptHidden = false;

        private void ApplyPromptHiddenUi()
        {
            if (BtnTogglePrompt != null)
            {
                BtnTogglePrompt.Content = _promptHidden ? "Hiện Prompt" : "Ẩn Prompt";
            }

            // Update WebView Prompt Layout
            if (RowWvBrowser != null && RowWvGap != null && RowWvPrompt != null && GridPromptWvContainer != null)
            {
                if (_promptHidden)
                {
                    RowWvBrowser.Height = new GridLength(1, GridUnitType.Star);
                    RowWvGap.Height = new GridLength(0);
                    RowWvPrompt.Height = new GridLength(0);
                    GridPromptWvContainer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    RowWvBrowser.Height = new GridLength(4, GridUnitType.Star);
                    RowWvGap.Height = new GridLength(8);
                    RowWvPrompt.Height = new GridLength(1, GridUnitType.Star);
                    GridPromptWvContainer.Visibility = Visibility.Visible;
                }
            }

            // Update WebBrowser Prompt Layout
            if (RowWebBrowser != null && RowWebGap != null && RowWebPrompt != null && GridPromptWebContainer != null)
            {
                if (_promptHidden)
                {
                    RowWebBrowser.Height = new GridLength(1, GridUnitType.Star);
                    RowWebGap.Height = new GridLength(0);
                    RowWebPrompt.Height = new GridLength(0);
                    GridPromptWebContainer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    RowWebBrowser.Height = new GridLength(4, GridUnitType.Star);
                    RowWebGap.Height = new GridLength(8);
                    RowWebPrompt.Height = new GridLength(1, GridUnitType.Star);
                    GridPromptWebContainer.Visibility = Visibility.Visible;
                }
            }
        }

        private void BtnTogglePrompt_Click(object sender, RoutedEventArgs e)
        {
            _promptHidden = !_promptHidden;
            if (_node != null)
            {
                _node.LayerAiPromptHidden = _promptHidden;
            }
            ApplyPromptHiddenUi();
        }


        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            // First, make sure the current active layer's state is saved from UI
            SaveActiveLayerState();

            bool atLeastOneApplied = false;

            foreach (var layer in _selectedLayers)
            {
                if (!_layerStates.TryGetValue(layer, out var state)) continue;

                // Check if this layer has any secondary images in its state
                int countSlotsWithImages = state.SecondaryImages.Count(s => s.HasImage);
                if (countSlotsWithImages == 0) continue;

                var destinationParent = layer.ParentLayer ?? layer;
                BitmapSource sourceImg = layer.OriginalTransformBitmap ?? layer.Bitmap;
                var bounds = GetLayerContentBounds(sourceImg);
                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                {
                    bounds = new Rect(0, 0, sourceImg.PixelWidth, sourceImg.PixelHeight);
                }

                double? targetRatio = null;
                int? customW = null;
                int? customH = null;

                int selectedIndex = state.AspectRatioIndex;
                if (selectedIndex == 6)
                {
                    customW = int.TryParse(state.CustomWidth, out var w) ? w : 512;
                    customH = int.TryParse(state.CustomHeight, out var h) ? h : 512;
                }
                else if (selectedIndex > 0)
                {
                    targetRatio = selectedIndex switch
                    {
                        1 => 16.0 / 9.0,
                        2 => 4.0 / 3.0,
                        3 => 1.0,
                        4 => 3.0 / 4.0,
                        5 => 9.0 / 16.0,
                        _ => 1.0
                    };
                }

                EditorLayer? activeChild = null;

                for (int i = 0; i < state.SecondaryImages.Count; i++)
                {
                    var slot = state.SecondaryImages[i];
                    if (slot.HasImage)
                    {
                        var childLayer = new EditorLayer(destinationParent.Width, destinationParent.Height, $"Layer AI {destinationParent.ChildLayers.Count + 1}");
                        childLayer.ParentLayer = destinationParent;
                        destinationParent.ChildLayers.Add(childLayer);

                        ProcessAndApplyAiImage(childLayer, slot.Bitmap, layer, bounds, targetRatio, customW, customH);

                        if (slot.IsSelected)
                        {
                            activeChild = childLayer;
                        }
                    }
                }

                if (destinationParent.ChildLayers.Count > 0)
                {
                    destinationParent.ActiveChildLayer = activeChild ?? destinationParent.ChildLayers.Last();
                    
                    // Set radio indicators
                    foreach (var child in destinationParent.ChildLayers)
                    {
                        child.IsActive = (child == destinationParent.ActiveChildLayer);
                        child.IsSelected = (child == destinationParent.ActiveChildLayer);
                    }
                    destinationParent.IsActive = false;
                    destinationParent.IsSelected = false;
                    
                    // Update active document focus to the new active child
                    if (layer == _activeLayer)
                    {
                        _doc.ActiveLayer = destinationParent.ActiveChildLayer;
                    }
                    
                    destinationParent.OnPropertyChanged(nameof(EditorLayer.HasChildren));
                    atLeastOneApplied = true;
                }
            }

            if (atLeastOneApplied)
            {
                // Refresh panel
                var ownerRef = this.Owner;
                if (ownerRef != null)
                {
                    var panel = FindVisualChild<ImageEditorPanel>(ownerRef);
                    panel?.RefreshLayersList();
                    panel?.OnDocumentModified();
                }
            }

            try { DialogResult = true; } catch { }
            Close();
        }

        private static BitmapSource DrawPreviewImage(BitmapSource src, double? targetRatio, int? customW, int? customH, bool drawCheckerboard)
        {
            int srcW = src.PixelWidth;
            int srcH = src.PixelHeight;

            int newW = srcW;
            int newH = srcH;
            double currentRatio = (double)srcW / srcH;

            BitmapSource imageToDraw = src;

            if (customW.HasValue && customH.HasValue)
            {
                newW = customW.Value;
                newH = customH.Value;
                var scale = new ScaleTransform((double)newW / srcW, (double)newH / srcH);
                imageToDraw = new TransformedBitmap(src, scale);
            }
            else if (targetRatio.HasValue)
            {
                double ratio = targetRatio.Value;
                if (currentRatio > ratio)
                {
                    newH = (int)Math.Ceiling(srcW / ratio);
                }
                else if (currentRatio < ratio)
                {
                    newW = (int)Math.Ceiling(srcH * ratio);
                }
            }

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                if (drawCheckerboard)
                {
                    var brush = Application.Current.TryFindResource("PsDarkCheckeredBrush") as Brush ?? Brushes.Black;
                    dc.DrawRectangle(brush, null, new Rect(0, 0, newW, newH));
                }
                else
                {
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, newW, newH));
                }

                // Center the image within the padded dimensions
                double x = (newW - imageToDraw.PixelWidth) / 2.0;
                double y = (newH - imageToDraw.PixelHeight) / 2.0;
                dc.DrawImage(imageToDraw, new Rect(x, y, imageToDraw.PixelWidth, imageToDraw.PixelHeight));
            }

            var rtb = new RenderTargetBitmap(newW, newH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        private static string? ResolveFromNodeIfAny(IWorkflowEditorHost host, string? nodeId, string? key)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key)) return null;
            var src = host.ViewModel?.Nodes?.FirstOrDefault(n =>
                string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (src == null) return null;
            var value = NodeDataPanelService.ResolveDynamicValueByKey(src, key);
            if (string.IsNullOrWhiteSpace(value) || value == "—") return null;
            return value;
        }

        private static BitmapImage? CreateBitmapFromUrlOrFile(string value)
        {
            return ImageProcessingNodeControl.CreateBitmapFromUrlOrFile(value);
        }

        private static BitmapImage? CreateBitmapFromBase64(string base64)
        {
            try
            {
                string data = base64.Contains(',') ? base64.Split(',')[1] : base64;
                byte[] bytes = Convert.FromBase64String(data);
                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { }
            return null;
        }

        private static List<string> ParseImageLinksFromOutput(string? raw)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(raw) || raw == "—") return list;

            raw = raw.Trim();

            var linkSet = ParseKeySet(null, "linkImage, linkImg, link_image, imageUrl, url, src, link, path, base64, b64, data");

            try
            {
                using (var doc = System.Text.Json.JsonDocument.Parse(raw))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        var link = ExtractImageLinkFromJsonObj(root, linkSet);
                        if (!string.IsNullOrWhiteSpace(link)) list.Add(link);
                        return list;
                    }
                    else if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var elem in root.EnumerateArray())
                        {
                            if (elem.ValueKind == System.Text.Json.JsonValueKind.Object)
                            {
                                var link = ExtractImageLinkFromJsonObj(elem, linkSet);
                                if (!string.IsNullOrWhiteSpace(link)) list.Add(link);
                            }
                            else if (elem.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var str = elem.GetString();
                                if (!string.IsNullOrWhiteSpace(str)) list.Add(str.Trim());
                            }
                        }
                        if (list.Count > 0) return list;
                    }
                }
            }
            catch { }

            if (raw.StartsWith("["))
            {
                try
                {
                    var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw);
                    if (deserialized != null)
                    {
                        foreach (var item in deserialized)
                        {
                            if (!string.IsNullOrWhiteSpace(item))
                                list.Add(item.Trim());
                        }
                    }
                }
                catch
                {
                    var inner = raw.Trim();
                    if (inner.StartsWith("[")) inner = inner.Substring(1);
                    if (inner.EndsWith("]")) inner = inner.Substring(0, inner.Length - 1);
                    var parts = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(p => p.Trim().Trim('"'))
                                     .Where(p => !string.IsNullOrWhiteSpace(p));
                    foreach (var p in parts)
                    {
                        list.Add(p);
                    }
                }
            }
            else
            {
                list.Add(raw);
            }
            return list;
        }

        private static string? ExtractImageLinkFromJsonObj(System.Text.Json.JsonElement obj, HashSet<string> linkSet)
        {
            if (obj.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            foreach (var prop in obj.EnumerateObject())
            {
                if (linkSet.Contains(prop.Name))
                {
                    var val = prop.Value.ValueKind == System.Text.Json.JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
                    if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
                }
            }
            return null;
        }

        private static string? ResolveFromHistoricalCache(string nodeId, string key, string executionId)
        {
            var list = ResolveAllFromHistoricalCache(nodeId, key, executionId);
            return list.Count > 0 ? list[0] : null;
        }

        private static List<string> ResolveAllFromHistoricalCache(string nodeId, string key, string executionId)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(executionId)) return result;

            string actualRunId = executionId;
            if (WorkflowExecutionService.ExecutionIdMapping.TryGetValue(executionId, out var mapped))
                actualRunId = mapped;

            void AddValue(string? val)
            {
                var links = ParseImageLinksFromOutput(val);
                foreach (var link in links)
                {
                    if (!string.IsNullOrWhiteSpace(link) && !result.Contains(link, StringComparer.OrdinalIgnoreCase))
                        result.Add(link);
                }
            }

            // 1. Root runId & actualRunId
            if (WorkflowExecutionService.ScopedOutputsHistoricalCache.TryGetValue(executionId, out var byNode1) &&
                byNode1.TryGetValue(nodeId, out var byKey1) &&
                byKey1.TryGetValue(key, out var val1))
            {
                AddValue(val1);
            }

            if (!string.Equals(actualRunId, executionId, StringComparison.OrdinalIgnoreCase) &&
                WorkflowExecutionService.ScopedOutputsHistoricalCache.TryGetValue(actualRunId, out var byNode2) &&
                byNode2.TryGetValue(nodeId, out var byKey2) &&
                byKey2.TryGetValue(key, out var val2))
            {
                AddValue(val2);
            }

            // 2. Child runs (executionId:* hoặc actualRunId:*)
            var prefix1 = executionId + ":";
            var prefix2 = actualRunId + ":";
            foreach (var kv in WorkflowExecutionService.ScopedOutputsHistoricalCache)
            {
                if (kv.Key.StartsWith(prefix1, StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.Value.TryGetValue(nodeId, out var childKey) &&
                        childKey.TryGetValue(key, out var childVal))
                    {
                        AddValue(childVal);
                    }
                }
            }

            return result;
        }

        private void LoadSavedSettings()
        {
            if (_node == null) return;

            // Load prompt
            var savedPrompt = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "prompt", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedPrompt))
            {
                SetRichText(TxtPrompt, savedPrompt);
            }
            else
            {
                SetRichText(TxtPrompt, _node.ProcessorPrompt ?? string.Empty);
            }

            // Load batch size (promptSize)
            var savedSize = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "promptSize", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedSize) && int.TryParse(savedSize, out var bSize))
            {
                CmbBatchSize.SelectedIndex = Math.Clamp(bSize - 1, 0, 3);
            }
            else
            {
                CmbBatchSize.SelectedIndex = Math.Clamp(_node.PromptSize - 1, 0, 3);
            }

            // Load aspect ratio
            var savedAspect = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "aspectRatio", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedAspect))
            {
                CmbAspectRatio.SelectedIndex = savedAspect switch
                {
                    "16:9" => 1,
                    "4:3" => 2,
                    "1:1" => 3,
                    "3:4" => 4,
                    "9:16" => 5,
                    "Free" => 6,
                    _ => 3
                };
            }
            else
            {
                CmbAspectRatio.SelectedIndex = 3;
            }

            // Load custom width and height
            BitmapSource sourceImg = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;
            var bounds = GetLayerContentBounds(sourceImg);

            var savedWidth = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropWidth", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedWidth))
            {
                TxtCustomWidth.Text = savedWidth;
            }
            else
            {
                if (!bounds.IsEmpty && bounds.Width > 0)
                {
                    TxtCustomWidth.Text = ((int)bounds.Width).ToString();
                }
            }

            var savedHeight = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropHeight", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedHeight))
            {
                TxtCustomHeight.Text = savedHeight;
            }
            else
            {
                if (!bounds.IsEmpty && bounds.Height > 0)
                {
                    TxtCustomHeight.Text = ((int)bounds.Height).ToString();
                }
            }

            // Restore active tab selection
            var savedTabStr = _node.LayerAiActiveTab ?? "Prompt";
            var savedTab = savedTabStr switch
            {
                "WebView" => ActiveTab.WebView,
                "WebBrowser" => ActiveTab.WebBrowser,
                _ => ActiveTab.Prompt
            };
            
            // Force SwitchToTab to run fully by setting _activeTab to a different state first
            _activeTab = (savedTab == ActiveTab.Prompt) ? ActiveTab.WebBrowser : ActiveTab.Prompt;
            SwitchToTab(savedTab);

            // Restore prompt hidden state
            _promptHidden = _node.LayerAiPromptHidden;
            ApplyPromptHiddenUi();

            // Restore AI Send Mode
            _sendModeOn = _node.LayerAiSendModeOn;
            ApplySendModeUi();
        }

        private void RefreshRelatedNodeDialogs()
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win is BaseNodeDialog baseDialog && baseDialog.DataContext is FlowMy.ViewModels.BaseNodeDialogViewModel dialogVm && dialogVm.Node == _node)
                {
                    baseDialog.Dispatcher.Invoke(() => baseDialog.RefreshOutputsUI());
                }
            }
        }

        private static Rect GetLayerContentBounds(BitmapSource bitmap)
        {
            if (bitmap == null) return Rect.Empty;
            try
            {
                int w = bitmap.PixelWidth;
                int h = bitmap.PixelHeight;
                if (w <= 0 || h <= 0) return Rect.Empty;

                int stride = w * 4;
                byte[] pixels = new byte[stride * h];
                bitmap.CopyPixels(pixels, stride, 0);

                int minX = w, maxX = 0, minY = h, maxY = 0;
                bool found = false;

                for (int y = 0; y < h; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        byte alpha = pixels[rowOffset + x * 4 + 3];
                        if (alpha > 5) // Ignore transparent edges
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                            found = true;
                        }
                    }
                }

                if (!found)
                {
                    return new Rect(0, 0, w, h);
                }

                return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            catch
            {
                return new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                {
                    return t;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private void ProcessAndApplyAiImage(
            EditorLayer childLayer,
            BitmapSource aiBmp,
            EditorLayer activeLayer,
            Rect originalBounds,
            double? targetRatio,
            int? customW,
            int? customH)
        {
            // 1. Get original crop dimensions
            BitmapSource sourceImg = activeLayer.OriginalTransformBitmap ?? activeLayer.Bitmap;
            int srcW = (int)originalBounds.Width;
            int srcH = (int)originalBounds.Height;
            if (srcW <= 0) srcW = sourceImg.PixelWidth;
            if (srcH <= 0) srcH = sourceImg.PixelHeight;

            // 2. Compute the newW and newH (the size of the image sent to AI)
            int newW = srcW;
            int newH = srcH;
            double currentRatio = (double)srcW / srcH;

            if (customW.HasValue && customH.HasValue)
            {
                newW = customW.Value;
                newH = customH.Value;
            }
            else if (targetRatio.HasValue)
            {
                double ratio = targetRatio.Value;
                if (currentRatio > ratio)
                {
                    newH = (int)Math.Ceiling(srcW / ratio);
                }
                else if (currentRatio < ratio)
                {
                    newW = (int)Math.Ceiling(srcH * ratio);
                }
            }

            // 3. Compute resolution scale to preserve the high quality of the AI/slot image
            double resolutionScale = Math.Max(1.0, Math.Max((double)aiBmp.PixelWidth / newW, (double)aiBmp.PixelHeight / newH));
            int hiNewW = (int)Math.Round(newW * resolutionScale);
            int hiNewH = (int)Math.Round(newH * resolutionScale);
            int hiSrcW = (int)Math.Round(srcW * resolutionScale);
            int hiSrcH = (int)Math.Round(srcH * resolutionScale);

            // Resize to high-resolution target dimensions
            BitmapSource resizedAi = ResizeBitmapHighQuality(aiBmp, hiNewW, hiNewH, uniformToFill: !customW.HasValue);

            // 4. Calculate crop offsets in high resolution
            BitmapSource croppedAiRegion;
            if (customW.HasValue && customH.HasValue)
            {
                croppedAiRegion = resizedAi;
            }
            else
            {
                double xOffset = (hiNewW - hiSrcW) / 2.0;
                double yOffset = (hiNewH - hiSrcH) / 2.0;
                int cropX = Math.Clamp((int)Math.Round(xOffset), 0, hiNewW - 1);
                int cropY = Math.Clamp((int)Math.Round(yOffset), 0, hiNewH - 1);
                int cropW = Math.Clamp(hiSrcW, 1, hiNewW - cropX);
                int cropH = Math.Clamp(hiSrcH, 1, hiNewH - cropY);

                croppedAiRegion = new CroppedBitmap(resizedAi, new Int32Rect(cropX, cropY, cropW, cropH));
            }

            // Calculate parent positioning
            double parentX = 0;
            double parentY = 0;
            if (activeLayer != null)
            {
                if (!activeLayer.ContentBounds.IsEmpty)
                {
                    parentX = activeLayer.ContentBounds.X;
                    parentY = activeLayer.ContentBounds.Y;
                }
                else
                {
                    parentX = activeLayer.OffsetX;
                    parentY = activeLayer.OffsetY;
                }
            }
            int posX = (int)Math.Clamp(parentX + originalBounds.X, 0, childLayer.Width - 1);
            int posY = (int)Math.Clamp(parentY + originalBounds.Y, 0, childLayer.Height - 1);
            int finalW = Math.Clamp(srcW, 1, childLayer.Width - posX);
            int finalH = Math.Clamp(srcH, 1, childLayer.Height - posY);

            // 5. Get original mask template and resize it to match high-resolution cropped AI region
            BitmapSource maskTemplate = activeLayer.OriginalTransformBitmap ?? activeLayer.Bitmap;
            var maskBounds = GetLayerContentBounds(maskTemplate);
            if (!maskBounds.IsEmpty && maskBounds.Width > 0 && maskBounds.Height > 0)
            {
                int mx = Math.Clamp((int)maskBounds.X, 0, maskTemplate.PixelWidth - 1);
                int my = Math.Clamp((int)maskBounds.Y, 0, maskTemplate.PixelHeight - 1);
                int mw = Math.Clamp((int)Math.Ceiling(maskBounds.Width), 1, maskTemplate.PixelWidth - mx);
                int mh = Math.Clamp((int)Math.Ceiling(maskBounds.Height), 1, maskTemplate.PixelHeight - my);
                if (mw > 0 && mh > 0 && (mx > 0 || my > 0 || mw < maskTemplate.PixelWidth || mh < maskTemplate.PixelHeight))
                {
                    maskTemplate = new CroppedBitmap(maskTemplate, new Int32Rect(mx, my, mw, mh));
                }
            }

            BitmapSource resizedMask = ResizeBitmapHighQuality(maskTemplate, croppedAiRegion.PixelWidth, croppedAiRegion.PixelHeight, uniformToFill: false);
            int hiW = croppedAiRegion.PixelWidth;
            int hiH = croppedAiRegion.PixelHeight;

            // Create the masked cropped AI region using SkiaSharp (100% in native memory, no slow CPU loops!)
            var maskedBmp = new WriteableBitmap(hiW, hiH, 96, 96, PixelFormats.Bgra32, null);
            maskedBmp.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(hiW, hiH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, maskedBmp.BackBuffer, maskedBmp.BackBufferStride))
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(SkiaSharp.SKColors.Transparent);

                    // 1. Draw the cropped AI image
                    var aiStride = croppedAiRegion.PixelWidth * 4;
                    var aiPixels = new byte[aiStride * croppedAiRegion.PixelHeight];
                    croppedAiRegion.CopyPixels(aiPixels, aiStride, 0);

                    using (var aiSkBmp = new SkiaSharp.SKBitmap())
                    {
                        var handleAi = System.Runtime.InteropServices.GCHandle.Alloc(aiPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                        try
                        {
                            aiSkBmp.InstallPixels(info, handleAi.AddrOfPinnedObject(), aiStride);
                            canvas.DrawBitmap(aiSkBmp, 0, 0);
                        }
                        finally
                        {
                            handleAi.Free();
                        }
                    }

                    // 2. Blend with the mask using DstIn (dest alpha * source alpha)
                    var maskStride = resizedMask.PixelWidth * 4;
                    var maskPixels = new byte[maskStride * resizedMask.PixelHeight];
                    resizedMask.CopyPixels(maskPixels, maskStride, 0);

                    using (var maskSkBmp = new SkiaSharp.SKBitmap())
                    {
                        var handleMask = System.Runtime.InteropServices.GCHandle.Alloc(maskPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                        try
                        {
                            maskSkBmp.InstallPixels(info, handleMask.AddrOfPinnedObject(), maskStride);
                            using (var paint = new SkiaSharp.SKPaint())
                            {
                                paint.BlendMode = SkiaSharp.SKBlendMode.DstIn;
                                canvas.DrawBitmap(maskSkBmp, 0, 0, paint);
                            }
                        }
                        finally
                        {
                            handleMask.Free();
                        }
                    }
                }
                maskedBmp.AddDirtyRect(new Int32Rect(0, 0, hiW, hiH));
            }
            finally
            {
                maskedBmp.Unlock();
            }

            // Draw masked cropped AI region onto childLayer.Bitmap using SkiaSharp
            childLayer.Bitmap.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(childLayer.Width, childLayer.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, childLayer.Bitmap.BackBuffer, childLayer.Bitmap.BackBufferStride))
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(SkiaSharp.SKColors.Transparent);

                    maskedBmp.Lock();
                    try
                    {
                        var maskedInfo = new SkiaSharp.SKImageInfo(maskedBmp.PixelWidth, maskedBmp.PixelHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                        using (var maskedSkBmp = new SkiaSharp.SKBitmap())
                        {
                            maskedSkBmp.InstallPixels(maskedInfo, maskedBmp.BackBuffer, maskedBmp.BackBufferStride);
                            using (var paint = new SkiaSharp.SKPaint())
                            {
                                paint.FilterQuality = SkiaSharp.SKFilterQuality.High;
                                paint.IsAntialias = true;
                                canvas.DrawBitmap(maskedSkBmp, new SkiaSharp.SKRect(posX, posY, posX + finalW, posY + finalH), paint);
                            }
                        }
                    }
                    finally
                    {
                        maskedBmp.Unlock();
                    }
                }
                childLayer.Bitmap.AddDirtyRect(new Int32Rect(0, 0, childLayer.Width, childLayer.Height));
            }
            finally
            {
                childLayer.Bitmap.Unlock();
            }

            // Set OriginalTransformBitmap and ContentBounds so that transform tool works properly
            childLayer.OriginalTransformBitmap = maskedBmp;
            childLayer.ContentBounds = new Rect(posX, posY, finalW, finalH);
            childLayer.PngBytes = null;

            childLayer.InvalidateThumbnail();
        }

        private static BitmapSource ResizeBitmapHighQuality(BitmapSource source, int targetWidth, int targetHeight, bool uniformToFill = false)
        {
            if (source.PixelWidth == targetWidth && source.PixelHeight == targetHeight)
            {
                return source;
            }

            int drawW = targetWidth;
            int drawH = targetHeight;
            float x = 0;
            float y = 0;

            if (uniformToFill)
            {
                double scale = Math.Max((double)targetWidth / source.PixelWidth, (double)targetHeight / source.PixelHeight);
                drawW = (int)Math.Ceiling(source.PixelWidth * scale);
                drawH = (int)Math.Ceiling(source.PixelHeight * scale);
                x = (float)((targetWidth - drawW) / 2.0);
                y = (float)((targetHeight - drawH) / 2.0);
            }

            // Copy pixels from WPF BitmapSource to a byte array
            int stride = source.PixelWidth * 4;
            byte[] pixels = new byte[stride * source.PixelHeight];
            
            BitmapSource formattedSource = source;
            if (source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Pbgra32)
            {
                formattedSource = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            }
            formattedSource.CopyPixels(pixels, stride, 0);

            // Create target WriteableBitmap and perform high-quality scaling using SkiaSharp (up to 50x faster)
            var targetBmp = new WriteableBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Bgra32, null);
            targetBmp.Lock();
            try
            {
                var srcInfo = new SkiaSharp.SKImageInfo(source.PixelWidth, source.PixelHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                var dstInfo = new SkiaSharp.SKImageInfo(targetWidth, targetHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);

                using (var srcBitmap = new SkiaSharp.SKBitmap())
                {
                    var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                    try
                    {
                        srcBitmap.InstallPixels(srcInfo, handle.AddrOfPinnedObject(), stride);

                        using (var surface = SkiaSharp.SKSurface.Create(dstInfo, targetBmp.BackBuffer, targetBmp.BackBufferStride))
                        {
                            if (surface != null)
                            {
                                var canvas = surface.Canvas;
                                canvas.Clear(SkiaSharp.SKColors.Transparent);
                                using (var paint = new SkiaSharp.SKPaint())
                                {
                                    paint.FilterQuality = SkiaSharp.SKFilterQuality.High;
                                    paint.IsAntialias = true;
                                    canvas.DrawBitmap(srcBitmap, new SkiaSharp.SKRect(x, y, x + drawW, y + drawH), paint);
                                }
                            }
                        }
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
                targetBmp.AddDirtyRect(new Int32Rect(0, 0, targetWidth, targetHeight));
            }
            finally
            {
                targetBmp.Unlock();
            }

            targetBmp.Freeze();
            return targetBmp;
        }


        #endregion

        #region Autocomplete & RichText Helpers

        private string GetRichText(RichTextBox rtb)
        {
            if (rtb?.Document == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            foreach (var block in rtb.Document.Blocks)
            {
                if (block is Paragraph p)
                {
                    foreach (var inline in p.Inlines)
                    {
                        if (inline is Run run)
                        {
                            sb.Append(run.Text);
                        }
                        else if (inline is InlineUIContainer uiContainer && uiContainer.Child is Border b && b.Child is Image img)
                        {
                            if (img.Tag is string codeId)
                            {
                                sb.Append(codeId);
                            }
                        }
                    }
                    sb.AppendLine();
                }
            }
            return sb.ToString().TrimEnd('\r', '\n');
        }

        private void SetRichText(RichTextBox rtb, string text)
        {
            if (rtb == null) return;
            rtb.Document.Blocks.Clear();
            if (string.IsNullOrEmpty(text)) return;
            
            // Basic text injection
            var p = new Paragraph(new Run(text));
            rtb.Document.Blocks.Add(p);
        }

        private void TxtPrompt_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (sender is RichTextBox rtb && rtb.IsFocused)
            {
                var caret = rtb.CaretPosition;
                if (caret != null)
                {
                    string textInRun = caret.GetTextInRun(LogicalDirection.Backward);
                    if (textInRun.EndsWith("@"))
                    {
                        // Show autocomplete popup
                        var rect = caret.GetCharacterRect(LogicalDirection.Backward);
                        PopupPromptAutocomplete.PlacementTarget = rtb;
                        PopupPromptAutocomplete.PlacementRectangle = rect;
                        
                        PopulateAutocompleteList();
                        PopupPromptAutocomplete.IsOpen = true;
                    }
                    else if (e.Key == Key.Escape || e.Key == Key.Back)
                    {
                        if (!textInRun.Contains("@"))
                            PopupPromptAutocomplete.IsOpen = false;
                    }
                }
            }
        }

                private (string resolvedPrompt, string promptJson) BuildResolvedPromptAndJson(string rawPrompt, string? overrideMainCodeId = null)
        {
            if (string.IsNullOrEmpty(rawPrompt)) rawPrompt = string.Empty;

            // Real CodeId for main layer (or override crop codeId if provided)
            string mainRealCodeId = !string.IsNullOrWhiteSpace(overrideMainCodeId)
                ? overrideMainCodeId
                : (string.IsNullOrWhiteSpace(_activeLayer?.CodeId)
                    ? (_activeLayer != null ? (_activeLayer.CodeId = Guid.NewGuid().ToString("N")) : Guid.NewGuid().ToString("N"))
                    : _activeLayer.CodeId);

            bool hasMainImage = _activeLayer != null && (_activeLayer.Thumbnail != null || _activeLayer.Bitmap != null);

            var childTags = new System.Collections.Generic.List<string>();
            var childCodeIds = new System.Collections.Generic.List<string>();
            var imagesDict = new System.Collections.Generic.Dictionary<string, object>();
            var tagsMap = new System.Collections.Generic.Dictionary<string, object>();

            // 1. Main image info
            if (hasMainImage)
            {
                string? mainId = !string.IsNullOrWhiteSpace(_activeLayer?.CurrentImageId) ? _activeLayer.CurrentImageId : null;
                imagesDict[mainRealCodeId] = new
                {
                    fileName = $"{mainRealCodeId}.png",
                    id = mainId
                };
                tagsMap["@main"] = mainRealCodeId;
            }

            // 2. Secondary/child images info
            for (int i = 0; i < _secondaryImages.Count; i++)
            {
                if (_secondaryImages[i].HasImage)
                {
                    string tag = $"@img{i}";
                    childTags.Add(tag);

                    string secRealCodeId = string.IsNullOrWhiteSpace(_secondaryImages[i].CodeId)
                        ? (_secondaryImages[i].CodeId = Guid.NewGuid().ToString("N"))
                        : _secondaryImages[i].CodeId;
                    childCodeIds.Add(secRealCodeId);

                    string? secId = _secondaryImages[i].GetImageId(CmbAspectRatio?.SelectedIndex ?? 3);

                    imagesDict[secRealCodeId] = new
                    {
                        fileName = $"{secRealCodeId}.png",
                        id = secId
                    };
                    tagsMap[tag] = secRealCodeId;
                }
            }

            // 3. All image tags
            var allTags = new System.Collections.Generic.List<string>();
            if (hasMainImage)
            {
                allTags.Add("@main");
            }
            allTags.AddRange(childTags);

            tagsMap["@child"] = childTags;
            tagsMap["@all"] = allTags;

            string childTagsStr = string.Join(", ", childTags);
            string allTagsStr = string.Join(", ", allTags);

            // 4. Resolved prompt text
            string resolvedPrompt = rawPrompt;
            if (resolvedPrompt.Contains("@all"))
            {
                resolvedPrompt = resolvedPrompt.Replace("@all", allTagsStr);
            }
            if (resolvedPrompt.Contains("@child"))
            {
                resolvedPrompt = resolvedPrompt.Replace("@child", childTagsStr);
            }

            // 5. Build Parts array (tokenizing rawPrompt into text & ref parts)
            var partsList = new System.Collections.Generic.List<object>();
            var regex = new System.Text.RegularExpressions.Regex(@"(@all|@child|@main|@img\d+)");
            int lastIdx = 0;
            foreach (System.Text.RegularExpressions.Match match in regex.Matches(rawPrompt))
            {
                if (match.Index > lastIdx)
                {
                    string textChunk = rawPrompt.Substring(lastIdx, match.Index - lastIdx);
                    partsList.Add(new { text = textChunk });
                }

                string tagMatched = match.Value;
                partsList.Add(new { @ref = tagMatched });

                lastIdx = match.Index + match.Length;
            }

            if (lastIdx < rawPrompt.Length)
            {
                partsList.Add(new { text = rawPrompt.Substring(lastIdx) });
            }

            // 6. Assemble payload
            var payload = new
            {
                rawPrompt = rawPrompt,
                prompt = resolvedPrompt,
                images = imagesDict,
                tags = tagsMap,
                parts = partsList
            };

            string jsonString = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            return (resolvedPrompt, jsonString);
        }

        private void PopulateAutocompleteList()
        {
            var items = new System.Collections.Generic.List<object>();
            
            items.Add(new { DisplayName = "Tất cả ảnh (@all)", Bitmap = (BitmapSource?)null, CodeId = "@all" });
            
            if (_secondaryImages.Count > 0)
            {
                items.Add(new { DisplayName = "Tất cả ảnh con (@child)", Bitmap = (BitmapSource?)null, CodeId = "@child" });
            }

            if (_activeLayer != null && (_activeLayer.Thumbnail != null || _activeLayer.Bitmap != null))
            {
                items.Add(new { DisplayName = "Ảnh chính", Bitmap = _activeLayer.Thumbnail ?? (BitmapSource)_activeLayer.Bitmap, CodeId = "@main" });
            }
            
            for (int i = 0; i < _secondaryImages.Count; i++)
            {
                if (_secondaryImages[i].HasImage && _secondaryImages[i].Bitmap != null)
                {
                    items.Add(new { 
                        DisplayName = $"Ảnh {i + 1}", 
                        Bitmap = _secondaryImages[i].Bitmap, 
                        CodeId = $"@img{i}" 
                    });
                }
            }
            
            ListPromptAutocomplete.ItemsSource = items;
            if (items.Count > 0)
            {
                ListPromptAutocomplete.SelectedIndex = 0;
            }
        }

        private void ListPromptAutocomplete_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is DependencyObject dep)
            {
                var scrollViewer = FindVisualChild<ScrollViewer>(dep);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (e.Delta / 3.0));
                    e.Handled = true;
                }
            }
        }

        private void InsertImageTag(object selectedItem)
        {
            if (selectedItem == null) return;
            
            RichTextBox? rtb = null;
            if (ActiveTab.Prompt == _activeTab) rtb = TxtPrompt;
            else if (ActiveTab.WebView == _activeTab) rtb = TxtPromptWv;
            else if (ActiveTab.WebBrowser == _activeTab) rtb = TxtPromptWeb;

            if (rtb == null) return;

            dynamic item = selectedItem;
            string codeId = item.CodeId;
            BitmapSource? bmp = item.Bitmap;

            var caret = rtb.CaretPosition;
            if (caret == null) return;

            // Delete the '@' character
            TextPointer startDelete = caret.GetPositionAtOffset(-1, LogicalDirection.Backward);
            if (startDelete != null)
            {
                new TextRange(startDelete, caret).Text = "";
            }

            if (codeId == "@all")
            {
                rtb.CaretPosition.InsertTextInRun("@all ");
                rtb.CaretPosition = rtb.CaretPosition.GetPositionAtOffset(5, LogicalDirection.Forward);
            }
            else if (codeId == "@child")
            {
                rtb.CaretPosition.InsertTextInRun("@child ");
                rtb.CaretPosition = rtb.CaretPosition.GetPositionAtOffset(7, LogicalDirection.Forward);
            }
            else if (bmp != null)
            {
                var border = new Border
                {
                    Width = 20,
                    Height = 20,
                    CornerRadius = new CornerRadius(4),
                    ClipToBounds = true,
                    Background = new SolidColorBrush(Color.FromRgb(21, 23, 30)),
                    Margin = new Thickness(2, 0, 2, -4)
                };
                var img = new Image
                {
                    Source = bmp,
                    Stretch = Stretch.UniformToFill,
                    Tag = codeId
                };
                border.Child = img;

                var container = new InlineUIContainer(border, rtb.CaretPosition);
                rtb.CaretPosition = container.ElementEnd;
            }

            PopupPromptAutocomplete.IsOpen = false;
            rtb.Focus();
        }

        private void ListPromptAutocomplete_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (ListPromptAutocomplete.SelectedItem != null)
            {
                InsertImageTag(ListPromptAutocomplete.SelectedItem);
                e.Handled = true;
            }
        }

        private void ListPromptAutocomplete_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                InsertImageTag(ListPromptAutocomplete.SelectedItem);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                PopupPromptAutocomplete.IsOpen = false;
                e.Handled = true;
            }
        }
        
        #endregion

    }
}
