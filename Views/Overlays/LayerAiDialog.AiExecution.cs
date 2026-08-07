// =========================================================================================================
// AI / DEVELOPER ARCHITECTURAL DIRECTIVE:
// This file is part of the partial class LayerAiDialog (Views/Overlays/LayerAiDialog).
// CRITICAL RULE: DO NOT BLOAT OR STUFF EXCESSIVE LOGIC INTO A SINGLE FILE.
// Maintain each partial class file at a maximum size of ~1000 - 1500 lines of code.
// When adding new features or handlers, refactor and create dedicated partial class files per module.
// =========================================================================================================

using FlowMy.Models.ImageEditor;
using FlowMy.Services.Workflow;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.Overlays
{
    public partial class LayerAiDialog : Window
    {
        #region Send AI (BtnSend_Click)

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsLoading(true);

            var destinationParent = _activeLayer.ParentLayer ?? _activeLayer;
            var placeholders = new List<EditorLayer>();

            try
            {
                BitmapSource sourceImg = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;
                var bounds = GetLayerContentBounds(sourceImg);
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    int x = Math.Clamp((int)bounds.X, 0, sourceImg.PixelWidth - 1);
                    int y = Math.Clamp((int)bounds.Y, 0, sourceImg.PixelHeight - 1);
                    int w = Math.Clamp((int)Math.Ceiling(bounds.Width), 1, sourceImg.PixelWidth - x);
                    int h = Math.Clamp((int)Math.Ceiling(bounds.Height), 1, sourceImg.PixelHeight - y);
                    if (w > 0 && h > 0 && (x > 0 || y > 0 || w < sourceImg.PixelWidth || h < sourceImg.PixelHeight))
                    {
                        sourceImg = new CroppedBitmap(sourceImg, new Int32Rect(x, y, w, h));
                    }
                }
                BitmapSource processedImg;

                double? targetRatio = null;
                int? customW = null;
                int? customH = null;

                int selectedIndex = CmbAspectRatio.SelectedIndex;
                if (selectedIndex == 0)
                {
                    processedImg = DrawPreviewImage(sourceImg, null, null, null, drawCheckerboard: false);
                }
                else if (selectedIndex == 6)
                {
                    int targetW = int.TryParse(TxtCustomWidth.Text, out var w) ? w : 512;
                    int targetH = int.TryParse(TxtCustomHeight.Text, out var h) ? h : 512;
                    customW = targetW;
                    customH = targetH;
                    processedImg = DrawPreviewImage(sourceImg, null, targetW, targetH, drawCheckerboard: false);
                }
                else
                {
                    double ratio = selectedIndex switch
                    {
                        1 => 16.0 / 9.0,
                        2 => 4.0 / 3.0,
                        3 => 1.0,
                        4 => 3.0 / 4.0,
                        5 => 9.0 / 16.0,
                        _ => 1.0
                    };
                    targetRatio = ratio;
                    processedImg = DrawPreviewImage(sourceImg, ratio, null, null, drawCheckerboard: false);
                }

                int batchSize = CmbBatchSize.SelectedIndex switch
                {
                    0 => 1,
                    1 => 2,
                    2 => 3,
                    3 => 4,
                    _ => 1
                };

                // Create placeholder layers for batch results
                for (int b = 0; b < batchSize; b++)
                {
                    var child = new EditorLayer(_activeLayer.Width, _activeLayer.Height, $"AI Result #{b + 1}")
                    {
                        ParentLayer = _activeLayer
                    };

                    if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                    {
                        child.ContentBounds = bounds;
                    }

                    child.LayerAiPrompt = GetActivePromptText();
                    child.LayerAiBatchSizeIndex = CmbBatchSize.SelectedIndex;
                    child.LayerAiAspectRatioIndex = CmbAspectRatio.SelectedIndex;
                    child.LayerAiCustomWidth = TxtCustomWidth.Text;
                    child.LayerAiCustomHeight = TxtCustomHeight.Text;

                    _activeLayer.ChildLayers.Add(child);
                    placeholders.Add(child);
                }

                var ownerWindow = Window.GetWindow(this);

                // Save active state to model
                SaveActiveLayerState();

                // Trigger background AI task
                var promptText = GetActivePromptText();
                var executionId = Guid.NewGuid().ToString("N");
                PendingExecutionIds.Enqueue(executionId);

                _host?.RequestRunSingleNode(_node);

                // Asynchronously monitor completion
                _ = Task.Run(async () =>
                {
                    var timeout = TimeSpan.FromMinutes(3);
                    var start = DateTime.Now;
                    BitmapSource? resultBmp = null;

                    while (DateTime.Now - start < timeout)
                    {
                        await Task.Delay(500);

                        // Check node execution output
                        var outPort = _node?.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "outputImage", StringComparison.OrdinalIgnoreCase));
                        if (outPort != null && !string.IsNullOrEmpty(outPort.LastResolvedOutputValue))
                        {
                            var val = outPort.LastResolvedOutputValue;
                            if (File.Exists(val))
                            {
                                try
                                {
                                    var bmp = new BitmapImage();
                                    bmp.BeginInit();
                                    bmp.UriSource = new Uri(val);
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.EndInit();
                                    bmp.Freeze();
                                    resultBmp = bmp;
                                    break;
                                }
                                catch { }
                            }
                        }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        SetButtonsLoading(false);
                        if (resultBmp != null)
                        {
                            for (int i = 0; i < placeholders.Count; i++)
                            {
                                ProcessAndApplyAiImage(placeholders[i], resultBmp, _activeLayer, bounds, targetRatio, customW, customH);
                            }
                        }
                        else
                        {
                            CleanupPlaceholders(placeholders, destinationParent, ownerWindow);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                SetButtonsLoading(false);
                CleanupPlaceholders(placeholders, destinationParent, Window.GetWindow(this));
                MessageBox.Show($"Lỗi gửi AI: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetButtonsLoading(bool isLoading)
        {
            _isAiLoading = isLoading;
            BtnSend.IsEnabled = !isLoading;
            if (BtnSendWv != null) BtnSendWv.IsEnabled = !isLoading;
            if (BtnSendWeb != null) BtnSendWeb.IsEnabled = !isLoading;

            BtnSend.Content = isLoading ? "⏳..." : "✨ Gửi";
            if (BtnSendWv != null) BtnSendWv.Content = isLoading ? "⏳..." : "✨ Gửi";
            if (BtnSendWeb != null) BtnSendWeb.Content = isLoading ? "⏳..." : "✨ Gửi";
        }

        private static bool CheckAndFulfillCropGuidIfAny(string codeId, string returnedId)
        {
            if (string.IsNullOrWhiteSpace(returnedId)) return false;

            if (ActiveExecutionScopes.Count > 0)
            {
                var scopeList = ActiveExecutionScopes.Values.ToList();
                for (int i = scopeList.Count - 1; i >= 0; i--)
                {
                    var scope = scopeList[i];
                    if (scope != null && scope.CropGuidRegistry.TryGetValue(codeId, out var scopeItem) && scopeItem != null)
                    {
                        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                        {
                            if (!string.IsNullOrWhiteSpace(returnedId))
                            {
                                scopeItem.SetImageId(0, returnedId);
                            }
                        });
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion
    }
}
