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
        #region Aspect Ratio & Preview

        private void CmbAspectRatio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingUI) return;
            if (PanelCustomSize == null) return;
            if (_activeLayer != null)
            {
                _activeLayer.LayerAiAspectRatioIndex = CmbAspectRatio.SelectedIndex;
            }
            PanelCustomSize.Visibility = (CmbAspectRatio.SelectedIndex == 6) ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreviewImage();
        }

        private void CmbBatchSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingUI || TxtSecondaryInfo == null) return;
            // Cập nhật lại tổng số ảnh hiển thị ở chế độ ảnh đơn khi thay đổi batch size
            UpdateSecondaryInfo();
        }

        private void TxtCustomSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingUI) return;
            UpdatePreviewImage();
        }

        private int GetSelectedSlotIndex()
        {
            if (_secondaryImages == null) return -1;
            for (int i = 0; i < _secondaryImages.Count; i++)
            {
                if (_secondaryImages[i] != null && _secondaryImages[i].HasImage && _secondaryImages[i].IsSelected)
                {
                    return i;
                }
            }
            return -1;
        }

        private BitmapSource MaskAndCropSecondaryImage(BitmapSource secondary, BitmapSource croppedOriginal)
        {
            try
            {
                int srcW = croppedOriginal.PixelWidth;
                int srcH = croppedOriginal.PixelHeight;

                // Compute high-resolution dimensions matching the croppedOriginal aspect ratio
                double scale = Math.Max(1.0, Math.Max((double)secondary.PixelWidth / srcW, (double)secondary.PixelHeight / srcH));
                int hiW = (int)Math.Round(srcW * scale);
                int hiH = (int)Math.Round(srcH * scale);

                // 1. Resize secondary image to match high-resolution dimensions (crop/fill aspect ratio) using SkiaSharp-backed Resize
                BitmapSource resizedSecondary = ResizeBitmapHighQuality(secondary, hiW, hiH, uniformToFill: true);

                // 2. Resize original cropped image (which contains the mask) using SkiaSharp-backed Resize
                BitmapSource resizedOriginal = ResizeBitmapHighQuality(croppedOriginal, hiW, hiH, uniformToFill: false);

                // 3. Perform native masking via SkiaSharp DstIn blend mode (extremely fast!)
                var maskedBmp = new WriteableBitmap(hiW, hiH, 96, 96, PixelFormats.Bgra32, null);
                maskedBmp.Lock();
                try
                {
                    var info = new SkiaSharp.SKImageInfo(hiW, hiH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                    using (var surface = SkiaSharp.SKSurface.Create(info, maskedBmp.BackBuffer, maskedBmp.BackBufferStride))
                    {
                        var canvas = surface.Canvas;
                        canvas.Clear(SkiaSharp.SKColors.Transparent);

                        // Draw secondary image
                        var secStride = resizedSecondary.PixelWidth * 4;
                        var secPixels = new byte[secStride * resizedSecondary.PixelHeight];
                        resizedSecondary.CopyPixels(secPixels, secStride, 0);

                        using (var secSkBmp = new SkiaSharp.SKBitmap())
                        {
                            var handleSec = System.Runtime.InteropServices.GCHandle.Alloc(secPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                            try
                            {
                                secSkBmp.InstallPixels(info, handleSec.AddrOfPinnedObject(), secStride);
                                canvas.DrawBitmap(secSkBmp, 0, 0);
                            }
                            finally
                            {
                                handleSec.Free();
                            }
                        }

                        // Apply original alpha mask via DstIn
                        var origStride = resizedOriginal.PixelWidth * 4;
                        var origPixels = new byte[origStride * resizedOriginal.PixelHeight];
                        resizedOriginal.CopyPixels(origPixels, origStride, 0);

                        using (var origSkBmp = new SkiaSharp.SKBitmap())
                        {
                            var handleOrig = System.Runtime.InteropServices.GCHandle.Alloc(origPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                            try
                            {
                                origSkBmp.InstallPixels(info, handleOrig.AddrOfPinnedObject(), origStride);
                                using (var paint = new SkiaSharp.SKPaint())
                                {
                                    paint.BlendMode = SkiaSharp.SKBlendMode.DstIn;
                                    canvas.DrawBitmap(origSkBmp, 0, 0, paint);
                                }
                            }
                            finally
                            {
                                handleOrig.Free();
                            }
                        }
                    }
                    maskedBmp.AddDirtyRect(new Int32Rect(0, 0, hiW, hiH));
                }
                finally
                {
                    maskedBmp.Unlock();
                }

                maskedBmp.Freeze();
                return maskedBmp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to mask secondary image: {ex.Message}");
                return secondary;
            }
        }

        private void UpdatePreviewImage()
        {
            if (ImgPreview == null || _activeLayer == null) return;

            try
            {
                // 1. Get the cropped original image first (as the base and alpha template)
                BitmapSource baseImg = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;
                var bounds = GetLayerContentBounds(baseImg);
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    int x = Math.Clamp((int)bounds.X, 0, baseImg.PixelWidth - 1);
                    int y = Math.Clamp((int)bounds.Y, 0, baseImg.PixelHeight - 1);
                    int w = Math.Clamp((int)Math.Ceiling(bounds.Width), 1, baseImg.PixelWidth - x);
                    int h = Math.Clamp((int)Math.Ceiling(bounds.Height), 1, baseImg.PixelHeight - y);
                    if (w > 0 && h > 0 && (x > 0 || y > 0 || w < baseImg.PixelWidth || h < baseImg.PixelHeight))
                    {
                        baseImg = new CroppedBitmap(baseImg, new Int32Rect(x, y, w, h));
                    }
                }

                BitmapSource sourceImg;
                
                // 2. In OFF mode, check if a slot is selected. If so, override preview source with masked secondary image.
                int selectedSlotIdx = GetSelectedSlotIndex();
                if (!_sendModeOn && selectedSlotIdx >= 0 && _secondaryImages[selectedSlotIdx].HasImage)
                {
                    sourceImg = MaskAndCropSecondaryImage(_secondaryImages[selectedSlotIdx].Bitmap, baseImg);
                }
                else
                {
                    sourceImg = baseImg;
                }

                BitmapSource processedImg;

                int selectedIndex = CmbAspectRatio.SelectedIndex;
                if (selectedIndex == 0)
                {
                    processedImg = DrawPreviewImage(sourceImg, null, null, null, drawCheckerboard: true);
                }
                else if (selectedIndex == 6)
                {
                    int targetW = int.TryParse(TxtCustomWidth.Text, out var w) ? w : 512;
                    int targetH = int.TryParse(TxtCustomHeight.Text, out var h) ? h : 512;
                    processedImg = DrawPreviewImage(sourceImg, null, targetW, targetH, drawCheckerboard: true);
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
                    processedImg = DrawPreviewImage(sourceImg, ratio, null, null, drawCheckerboard: true);
                }

                ImgPreview.Source = processedImg;
                if (ImgPreviewWv != null)
                {
                    ImgPreviewWv.Source = processedImg;
                }

                bool layerHasId = !string.IsNullOrEmpty(_activeLayer.GetImageId(selectedIndex));
                
                if (ImgPreviewAiBadge != null)
                {
                    ImgPreviewAiBadge.Visibility = layerHasId ? Visibility.Visible : Visibility.Collapsed;
                }
                if (ImgPreviewWvAiBadge != null)
                {
                    ImgPreviewWvAiBadge.Visibility = layerHasId ? Visibility.Visible : Visibility.Collapsed;
                }
                
                var normalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));

                if (ImgPreviewBorder != null)
                {
                    ImgPreviewBorder.BorderBrush = normalBrush;
                }
                if (ImgPreviewWvBorder != null)
                {
                    ImgPreviewWvBorder.BorderBrush = normalBrush;
                }
            }
            catch { }
        }

        #endregion

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
                    customW = int.TryParse(TxtCustomWidth.Text, out var w) ? w : 512;
                    customH = int.TryParse(TxtCustomHeight.Text, out var h) ? h : 512;
                    processedImg = DrawPreviewImage(sourceImg, null, customW, customH, drawCheckerboard: false);
                }
                else
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
                    processedImg = DrawPreviewImage(sourceImg, targetRatio, null, null, drawCheckerboard: false);
                }

                // Convert main image to base64
                var b64 = ImageProcessorHelper.ToBase64(processedImg);

                // Bind main image base64 output
                var cropBase64Port = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropBase64", StringComparison.OrdinalIgnoreCase));
                if (cropBase64Port != null) cropBase64Port.UserValueOverride = b64;

                // mainCodeId = Layer CodeId (identity của layer trên canvas, để downstream biết ảnh thuộc layer nào)
                string mainCodeId = string.IsNullOrWhiteSpace(_activeLayer.CodeId) ? (_activeLayer.CodeId = Guid.NewGuid().ToString("N")) : _activeLayer.CodeId;

                // mainCropCodeId = GUID mới cho ảnh crop mỗi lần gửi (dùng trong promptJson, cropListObjects JSON và CropGuidRegistry)
                string mainCropCodeId = Guid.NewGuid().ToString("N");

                var activePromptText = GetActivePromptText();
                var (resolvedPrompt, promptJson) = BuildResolvedPromptAndJson(activePromptText, mainCropCodeId);
                _node.ProcessorPrompt = activePromptText;

                GetOrAddDynamicOutputPort("prompt", "Layer AI - Prompt").UserValueOverride = resolvedPrompt;
                GetOrAddDynamicOutputPort("promptJson", "Layer AI - Prompt JSON (Multimodal Parts)").UserValueOverride = promptJson;

                int batchSize = CmbBatchSize.SelectedIndex + 1;
                // Ảnh đơn mode: tổng output = numberOfSelectedSecondaries × batchSize
                // Ảnh chung mode hoặc không có ảnh phụ: tổng output = batchSize (giữ nguyên)
                var selectedSecForCount = _secondaryImages.Where(s => s.HasImage && s.IsSelected).ToList();
                int totalOutputCount = (!_isCombinedMode && selectedSecForCount.Count > 0)
                    ? selectedSecForCount.Count * batchSize
                    : batchSize;
                _node.PromptSize = totalOutputCount;
                var sizePort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "promptSize", StringComparison.OrdinalIgnoreCase));
                if (sizePort != null) sizePort.UserValueOverride = totalOutputCount.ToString();

                var widthPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropWidth", StringComparison.OrdinalIgnoreCase));
                if (widthPort != null) widthPort.UserValueOverride = processedImg.PixelWidth.ToString();

                var heightPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropHeight", StringComparison.OrdinalIgnoreCase));
                if (heightPort != null) heightPort.UserValueOverride = processedImg.PixelHeight.ToString();

                string execId = Guid.NewGuid().ToString("N");
                _node.LastExecutionId = execId;
                var execIdPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "executionId", StringComparison.OrdinalIgnoreCase));
                if (execIdPort != null) execIdPort.UserValueOverride = execId;

                PendingExecutionIds.Enqueue(execId);

                _node.IsVerticalMode = (selectedIndex == 4 || selectedIndex == 5);
                string aspectStr = selectedIndex switch
                {
                    1 => "16:9",
                    2 => "4:3",
                    3 => "1:1",
                    4 => "3:4",
                    5 => "9:16",
                    6 => "Free",
                    _ => "Default"
                };
                var aspectPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "aspectRatio", StringComparison.OrdinalIgnoreCase));
                if (aspectPort != null)
                {
                    aspectPort.UserValueOverride = aspectStr;
                }

                var execSvc = _host?.ViewModel?.WorkflowExecutionService;
                string? existingMainId = _activeLayer.GetImageId(selectedIndex);
                var selectedSecItems = _secondaryImages.Where(s => s.HasImage && s.IsSelected).ToList();
                var existingSecIds = selectedSecItems.Select(s => s.GetImageId(selectedIndex)).Where(id => !string.IsNullOrEmpty(id)).ToList();

                CropGuidRegistry[mainCropCodeId] = new CodeCropMappingInfo
                {
                    CodeId = mainCropCodeId,
                    TargetLayer = _activeLayer,
                    AspectRatioIndex = selectedIndex,
                    ExecutionId = execId
                };

                // Output port: mainCodeId = layer CodeId (để biết ảnh render thuộc layer nào)
                GetOrAddDynamicOutputPort("mainCodeId", "Layer AI - Main Code ID").UserValueOverride = mainCodeId;

                if (execSvc != null)
                {
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "mainCodeId", mainCodeId);
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "prompt", resolvedPrompt);
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "promptJson", promptJson);
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "promptSize", totalOutputCount.ToString());
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "cropWidth", processedImg.PixelWidth.ToString());
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "cropHeight", processedImg.PixelHeight.ToString());
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "aspectRatio", aspectStr);
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "isCombinedImage", _isCombinedMode.ToString().ToLowerInvariant());
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "executionId", execId);
                }

                // *** Collect main and secondary images into cropListObjects array (index [0] = main image) ***
                // Dùng mainCropCodeId (GUID riêng cho crop) thay vì mainCodeId (layer CodeId)
                await CollectAndSetListBase64Async(b64, mainCropCodeId, existingMainId, execId, selectedIndex);

                // Refresh outputs list in node dialog immediately to reflect the generated overrides
                RefreshRelatedNodeDialogs();

                // Create variant placeholders in parent's ChildLayers before starting workflow execution
                for (int i = 0; i < totalOutputCount; i++)
                {
                    var placeholder = new EditorLayer(destinationParent.Width, destinationParent.Height, $"Layer AI {destinationParent.ChildLayers.Count + 1}");
                    placeholder.ParentLayer = destinationParent;
                    placeholder.IsLoading = true;
                    placeholder.StartLoadingTimer();
                    destinationParent.ChildLayers.Add(placeholder);
                    placeholders.Add(placeholder);
                }

                // Register execution scope for thread-safe multi-execution mapping
                var scope = new LayerAiExecutionScope
                {
                    ExecutionId = execId,
                    MainLayer = _activeLayer,
                    AspectRatioIndex = selectedIndex,
                    SecondaryImages = selectedSecItems,
                    Placeholders = placeholders
                };
                ActiveExecutionScopes[execId] = scope;

                // Notify HasChildren changed on parent so collapse toggle appears
                destinationParent.OnPropertyChanged(nameof(EditorLayer.HasChildren));

                // Refresh main panel immediately to render loading placeholders in the layers ListBox
                var editorPanel = FindVisualChild<ImageEditorPanel>(this.Owner);
                editorPanel?.RefreshLayersList();

                // Close dialog immediately — workflow runs in background, results applied to placeholders
                try { DialogResult = true; } catch { }
                Close();

                // Capture references needed for background processing
                var activeLayerRef = _activeLayer;
                var docRef = _doc;
                var nodeRef = _node;
                var hostRef = _host;
                var ownerRef = this.Owner;

                // Fire-and-forget: run workflow, then process results on UI thread
                _ = Task.Run(async () =>
                {
                    var filledPlaceholders = new HashSet<EditorLayer>();
                    Action<string, string, string, string?> realtimeHandler = (runId, targetNodeId, targetKey, valStr) =>
                    {
                        if (string.IsNullOrWhiteSpace(valStr) || valStr == "—") return;
                        
                        // Check if payload contains codeId and return ID object/array
                        ProcessCodeIdResult(valStr, nodeRef.ReturnCodeIdKeys, nodeRef.ReturnImageIdKeys, nodeRef.ReturnImageLinkKeys);
                        if (!string.IsNullOrWhiteSpace(nodeRef.RenderNodeId))
                        {
                            ProcessCodeIdResult(valStr, nodeRef.RenderCodeIdKeys, nodeRef.RenderImageIdKeys, nodeRef.RenderImageLinkKeys);
                        }

                        bool isIdOutput = string.Equals(targetKey, "mainImageId", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(targetKey, "imageId", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(targetKey, "mediaId", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(targetKey, "uploadedId", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(targetKey, "listImageIds", StringComparison.OrdinalIgnoreCase);

                        bool isRenderOutput = string.Equals(targetNodeId, nodeRef.RenderNodeId, StringComparison.OrdinalIgnoreCase) &&
                                              string.Equals(targetKey, nodeRef.RenderNodeOutputKey, StringComparison.OrdinalIgnoreCase);

                        if (!isIdOutput && !isRenderOutput) return;

                        string actualRunId = execId;
                        if (WorkflowExecutionService.ExecutionIdMapping.TryGetValue(execId, out var mappedRunId))
                        {
                            actualRunId = mappedRunId;
                        }

                        bool isMatch = string.Equals(runId, execId, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(runId, actualRunId, StringComparison.OrdinalIgnoreCase) ||
                                       runId.StartsWith(execId + ":", StringComparison.OrdinalIgnoreCase) ||
                                       runId.StartsWith(actualRunId + ":", StringComparison.OrdinalIgnoreCase);

                        if (!isMatch) return;

                        if (isIdOutput)
                        {
                            Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                if (ActiveExecutionScopes.TryGetValue(execId, out var execScope) && execScope != null)
                                {
                                    if (string.Equals(targetKey, "listImageIds", StringComparison.OrdinalIgnoreCase))
                                    {
                                        try
                                        {
                                            var idList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(valStr);
                                            if (idList != null)
                                            {
                                                for (int i = 0; i < idList.Count && i < execScope.SecondaryImages.Count; i++)
                                                {
                                                    execScope.SecondaryImages[i].SetImageId(execScope.AspectRatioIndex, idList[i]);
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                    else
                                    {
                                        execScope.MainLayer.SetImageId(execScope.AspectRatioIndex, valStr);
                                    }
                                }
                            });
                            return;
                        }

                        var links = ParseImageLinksFromOutput(valStr);
                        if (links.Count == 0) return;

                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            foreach (var entry in links)
                            {
                                if (string.IsNullOrWhiteSpace(entry)) continue;

                                EditorLayer? placeholder = null;
                                lock (placeholders)
                                {
                                    placeholder = placeholders.FirstOrDefault(p => !filledPlaceholders.Contains(p));
                                    if (placeholder != null)
                                    {
                                        filledPlaceholders.Add(placeholder);
                                    }
                                }

                                if (placeholder != null)
                                {
                                    BitmapImage? bmp = CreateBitmapFromUrlOrFile(entry.Trim()) ?? CreateBitmapFromBase64(entry.Trim());
                                    if (bmp != null)
                                    {
                                        placeholder.IsLoading = false;
                                        placeholder.StopLoadingTimer();
                                        ProcessAndApplyAiImage(placeholder, bmp, activeLayerRef, bounds, targetRatio, customW, customH);

                                        // Extract returned ID and assign to placeholder & main layer for this aspect ratio
                                        string returnedId = ExtractOrGenerateImageId(entry);
                                        placeholder.SetImageId(selectedIndex, returnedId);
                                        if (activeLayerRef != null)
                                        {
                                            activeLayerRef.SetImageId(selectedIndex, returnedId);
                                        }

                                        destinationParent.ActiveChildLayer = placeholder;
                                        docRef.ActiveLayer = placeholder;

                                        foreach (var child in destinationParent.ChildLayers)
                                        {
                                            child.IsActive = (child == placeholder);
                                            child.IsSelected = (child == placeholder);
                                        }

                                        var panel = FindVisualChild<ImageEditorPanel>(ownerRef);
                                        panel?.RefreshLayersList();
                                        panel?.OnDocumentModified();
                                    }
                                }
                            }
                        });
                    };

                    try
                    {
                        WorkflowExecutionService.OnScopedOutputSetGlobal += realtimeHandler;

                        // Run workflow on background thread via reflection
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            var vm = hostRef.ViewModel;
                            if (vm != null)
                            {
                                var vmType = vm.GetType();
                                var startTestMethod = vmType.GetMethod("StartTest", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (startTestMethod != null)
                                {
                                    var parameters = startTestMethod.GetParameters();
                                    object?[] args = parameters.Length > 0 ? new object?[] { execId } : null;
                                    if (startTestMethod.Invoke(vm, args) is Task t)
                                    {
                                        await t;
                                    }
                                }
                            }
                        }).Task.Unwrap();

                        // Process results on UI thread (fallback for any remaining unfilled placeholders)
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                WorkflowExecutionService.OnScopedOutputSetGlobal -= realtimeHandler;

                                // Refresh outputs list again to show final outputs/execution IDs
                                RefreshRelatedNodeDialogs();

                                // Resolve AI outputs
                                if (string.IsNullOrWhiteSpace(nodeRef.RenderNodeId) || string.IsNullOrWhiteSpace(nodeRef.RenderNodeOutputKey))
                                {
                                    CleanupPlaceholders(placeholders, destinationParent, ownerRef);
                                    return;
                                }

                                string actualRunId = execId;
                                if (WorkflowExecutionService.ExecutionIdMapping.TryGetValue(execId, out var mappedRunId))
                                {
                                    actualRunId = mappedRunId;
                                }

                                var allLinks = ResolveAllFromHistoricalCache(nodeRef.RenderNodeId, nodeRef.RenderNodeOutputKey, actualRunId);
                                if (allLinks.Count == 0)
                                {
                                    var rawFallback = ResolveFromNodeIfAny(hostRef, nodeRef.RenderNodeId, nodeRef.RenderNodeOutputKey);
                                    allLinks = ParseImageLinksFromOutput(rawFallback);
                                }

                                // Fill any remaining unfilled placeholders from fallback
                                foreach (var entry in allLinks)
                                {
                                    if (string.IsNullOrWhiteSpace(entry)) continue;

                                    EditorLayer? placeholder = null;
                                    lock (placeholders)
                                    {
                                        placeholder = placeholders.FirstOrDefault(p => !filledPlaceholders.Contains(p));
                                        if (placeholder != null)
                                        {
                                            filledPlaceholders.Add(placeholder);
                                        }
                                    }

                                    if (placeholder != null)
                                    {
                                        BitmapImage? bmp = CreateBitmapFromUrlOrFile(entry.Trim()) ?? CreateBitmapFromBase64(entry.Trim());
                                        if (bmp != null)
                                        {
                                            placeholder.IsLoading = false;
                                            placeholder.StopLoadingTimer();
                                            ProcessAndApplyAiImage(placeholder, bmp, activeLayerRef, bounds, targetRatio, customW, customH);
                                        }
                                    }
                                }

                                // Dọn dẹp các placeholders chưa được dùng
                                var unfilledList = placeholders.Where(p => !filledPlaceholders.Contains(p)).ToList();
                                foreach (var p in unfilledList)
                                {
                                    p.StopLoadingTimer();
                                    destinationParent.ChildLayers.Remove(p);
                                }

                                if (destinationParent.ChildLayers.Count > 0)
                                {
                                    destinationParent.ActiveChildLayer = destinationParent.ChildLayers.Last();
                                    docRef.ActiveLayer = destinationParent.ActiveChildLayer;

                                    foreach (var child in destinationParent.ChildLayers)
                                    {
                                        child.IsActive = (child == destinationParent.ActiveChildLayer);
                                        child.IsSelected = (child == destinationParent.ActiveChildLayer);
                                    }
                                    destinationParent.IsActive = false;
                                    destinationParent.IsSelected = false;
                                }

                                // Dọn dẹp cache của lần chạy này để tránh rò rỉ RAM
                                WorkflowExecutionService.ExecutionIdMapping.TryRemove(execId, out _);
                                WorkflowExecutionService.ScopedOutputsHistoricalCache.TryRemove(actualRunId, out _);

                                var childPrefix = actualRunId + ":";
                                var childrenKeys = WorkflowExecutionService.ScopedOutputsHistoricalCache.Keys
                                    .Where(k => k.StartsWith(childPrefix, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                                foreach (var childKey in childrenKeys)
                                {
                                    WorkflowExecutionService.ScopedOutputsHistoricalCache.TryRemove(childKey, out _);
                                }

                                var panel = FindVisualChild<ImageEditorPanel>(ownerRef);
                                panel?.RefreshLayersList();
                                panel?.OnDocumentModified();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine("AI result processing error: " + ex.Message);
                                CleanupPlaceholders(placeholders, destinationParent, ownerRef);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        WorkflowExecutionService.OnScopedOutputSetGlobal -= realtimeHandler;
                        System.Diagnostics.Debug.WriteLine("AI execution error: " + ex.Message);
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CleanupPlaceholders(placeholders, destinationParent, ownerRef);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                // Pre-workflow error (e.g. image processing) — mark placeholders as error
                CleanupPlaceholders(placeholders, destinationParent, this.Owner);

                MessageBox.Show("Lỗi thực thi AI: " + ex.Message, "AI Layer Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetButtons();
            }
        }

        /// <summary>
        /// Collect main image (index [0]) and selected secondary images (index [1..n]), convert to base64 if no ID present, and set cropListObjects output.
        /// </summary>
        private async Task CollectAndSetListBase64Async(string mainB64, string mainCodeId, string? existingMainId, string? execId = null, int aspectRatioIndex = 3)
        {
            var selectedSecItems = _secondaryImages
                .Where(s => s.HasImage && s.IsSelected && s.Bitmap != null)
                .ToList();

            var cropListObjects = new List<object>();

            // 1. Main image at index [0]
            bool mainHasId = !string.IsNullOrWhiteSpace(existingMainId);
            cropListObjects.Add(new
            {
                codeId = mainCodeId,
                base64 = mainHasId ? "" : mainB64,
                id = mainHasId ? existingMainId : (string?)null
            });

            // 2. Secondary images at index [1..n]
            for (int i = 0; i < selectedSecItems.Count; i++)
            {
                var secItem = selectedSecItems[i];
                string secCodeId = string.IsNullOrWhiteSpace(secItem.CodeId) ? (secItem.CodeId = Guid.NewGuid().ToString("N")) : secItem.CodeId;
                string? existingId = secItem.GetImageId(aspectRatioIndex);

                if (!string.IsNullOrEmpty(execId))
                {
                    CropGuidRegistry[secCodeId] = new CodeCropMappingInfo
                    {
                        CodeId = secCodeId,
                        TargetLayer = _activeLayer,
                        SecondaryImage = secItem,
                        SecondaryImageIndex = _secondaryImages.IndexOf(secItem),
                        AspectRatioIndex = aspectRatioIndex,
                        ExecutionId = execId
                    };
                }

                bool secHasId = !string.IsNullOrWhiteSpace(existingId);
                string b64 = secHasId ? "" : ImageProcessorHelper.ToBase64(secItem.Bitmap!);

                cropListObjects.Add(new
                {
                    codeId = secCodeId,
                    base64 = b64,
                    id = secHasId ? existingId : (string?)null
                });
            }

            string cropListObjectsJson = System.Text.Json.JsonSerializer.Serialize(cropListObjects);

            GetOrAddDynamicOutputPort("cropListObjects", "Layer AI - Crops List Objects (JSON)", FlowMy.Models.WorkflowDataType.ArrayDynamic, isMultiple: true).UserValueOverride = cropListObjectsJson;

            if (!string.IsNullOrEmpty(execId))
            {
                var execSvc = _host?.ViewModel?.WorkflowExecutionService;
                if (execSvc != null)
                {
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "cropListObjects", cropListObjectsJson);
                }
            }
        }

        private FlowMy.Models.WorkflowDynamicDataPort GetOrAddDynamicOutputPort(string key, string displayName, FlowMy.Models.WorkflowDataType dataType = FlowMy.Models.WorkflowDataType.String, bool isMultiple = false)
        {
            if (_node.DynamicOutputs == null)
            {
                _node.DynamicOutputs = new System.Collections.Generic.List<FlowMy.Models.WorkflowDynamicDataPort>();
            }
            var port = _node.DynamicOutputs.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
            if (port == null)
            {
                port = new FlowMy.Models.WorkflowDynamicDataPort
                {
                    Key = key,
                    DisplayName = displayName,
                    OutputType = dataType,
                    IsMultiple = isMultiple
                };
                _node.DynamicOutputs.Add(port);
            }
            else
            {
                port.OutputType = dataType;
                port.IsMultiple = isMultiple;
            }
            return port;
        }

        private static string ExtractOrGenerateImageId(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return Guid.NewGuid().ToString("N");
            var trimmed = entry.Trim();
            if (trimmed.Length > 200 || trimmed.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(trimmed.Substring(0, Math.Min(trimmed.Length, 1000))));
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                if (!string.IsNullOrEmpty(query["id"])) return query["id"]!;
                if (!string.IsNullOrEmpty(query["media_id"])) return query["media_id"]!;
                var fileName = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
                if (!string.IsNullOrWhiteSpace(fileName)) return fileName;
            }
            return trimmed;
        }

        private static HashSet<string> ParseKeySet(string? input, string defaultKeys)
        {
            var raw = string.IsNullOrWhiteSpace(input) ? defaultKeys : input;
            var keys = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        }

        public static bool ProcessCodeIdResult(string valStr, string? codeIdKeys = null, string? imageIdKeys = null, string? imageLinkKeys = null)
        {
            if (string.IsNullOrWhiteSpace(valStr)) return false;

            var codeIdSet = ParseKeySet(codeIdKeys, "codeId, CodeId, code_id");
            var imageIdSet = ParseKeySet(imageIdKeys, "id, Id, ID, mediaId, imageId, assetId");
            var imageLinkSet = ParseKeySet(imageLinkKeys, "linkImage, linkImg, link_image, imageUrl, url, src, link, path");

            try
            {
                using (var doc = System.Text.Json.JsonDocument.Parse(valStr))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        return TryApplyCodeIdObject(root, codeIdSet, imageIdSet, imageLinkSet);
                    }
                    else if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        bool anyApplied = false;
                        foreach (var element in root.EnumerateArray())
                        {
                            if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
                            {
                                if (TryApplyCodeIdObject(element, codeIdSet, imageIdSet, imageLinkSet)) anyApplied = true;
                            }
                        }
                        return anyApplied;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool TryApplyCodeIdObject(System.Text.Json.JsonElement element, HashSet<string> codeIdSet, HashSet<string> imageIdSet, HashSet<string> imageLinkSet)
        {
            if (element.ValueKind != System.Text.Json.JsonValueKind.Object) return false;

            string? codeId = null;
            string? returnedId = null;
            string? returnedLink = null;

            foreach (var prop in element.EnumerateObject())
            {
                var name = prop.Name;
                var val = prop.Value.ValueKind == System.Text.Json.JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
                if (string.IsNullOrWhiteSpace(val)) continue;

                if (codeIdSet.Contains(name))
                {
                    codeId = val;
                }
                else if (imageIdSet.Contains(name))
                {
                    returnedId = val;
                }
                else if (imageLinkSet.Contains(name))
                {
                    returnedLink = val;
                }
            }

            if (!string.IsNullOrWhiteSpace(codeId))
            {
                if (CropGuidRegistry.TryGetValue(codeId, out var info) && info != null)
                {
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(returnedId))
                        {
                            if (info.SecondaryImage != null)
                            {
                                info.SecondaryImage.SetImageId(info.AspectRatioIndex, returnedId);
                                info.SecondaryImage.ArchiveCurrentIfHasId();
                            }
                            else if (info.TargetLayer != null && info.SecondaryImageIndex == -1)
                            {
                                info.TargetLayer.SetImageId(info.AspectRatioIndex, returnedId);
                            }
                            
                            // Always sync back to the underlying EditorLayer to ensure data isn't lost if the dialog is closed
                            if (info.TargetLayer != null && info.SecondaryImageIndex >= 0 && info.SecondaryImageIndex < info.TargetLayer.LayerAiSecondaryImages.Count)
                            {
                                var layerSec = info.TargetLayer.LayerAiSecondaryImages[info.SecondaryImageIndex];
                                layerSec.SetImageId(info.AspectRatioIndex, returnedId);
                                
                                if (layerSec.Bitmap != null)
                                {
                                    bool exists = layerSec.SavedChildImages.Any(c => string.Equals(c.ImageId, returnedId, StringComparison.OrdinalIgnoreCase));
                                    if (!exists)
                                    {
                                        layerSec.SavedChildImages.Add(new EditorLayer.LayerAiSecondaryImage
                                        {
                                            ImageId = returnedId,
                                            FilePath = layerSec.FilePath,
                                            IsSelected = layerSec.IsSelected,
                                            Bitmap = layerSec.Bitmap,
                                            PngBytes = layerSec.PngBytes
                                        });
                                    }
                                }
                            }

                            // Refresh any open LayerAiDialog windows so secondary images history appears immediately
                            try
                            {
                                foreach (Window win in System.Windows.Application.Current.Windows)
                                {
                                    if (win is LayerAiDialog dlg && !dlg.IsClosed)
                                    {
                                        dlg.RefreshAllSlotsUI();
                                    }
                                }
                            }
                            catch { }
                        }
                    });
                    return true;
                }
            }
            return false;
        }

        #endregion

    }
}
