// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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
        #region Two-Way Drag and Drop (WPF <-> WebView2)

        private void SetupDragAndDrop()
        {
            // --- WPF-to-WebView Drag Source Setup ---
            var dragSources = new List<FrameworkElement>();
            if (ImgPreview != null) dragSources.Add(ImgPreview);
            if (ImgPreviewWv != null) dragSources.Add(ImgPreviewWv);
            dragSources.AddRange(_slotBorders);
            dragSources.AddRange(_slotBordersWv);

            foreach (var src in dragSources)
            {
                if (src == null) continue;

                src.PreviewMouseLeftButtonDown -= Src_PreviewMouseLeftButtonDown;
                src.PreviewMouseLeftButtonUp -= Src_PreviewMouseLeftButtonUp;
                src.PreviewMouseMove -= Src_PreviewMouseMove;

                src.PreviewMouseLeftButtonDown += Src_PreviewMouseLeftButtonDown;
                src.PreviewMouseLeftButtonUp += Src_PreviewMouseLeftButtonUp;
                src.PreviewMouseMove += Src_PreviewMouseMove;
            }

            // --- WebView-to-WPF Drop Target Setup ---
            var dropTargets = new List<FrameworkElement>();
            if (ImgPreview != null) dropTargets.Add(ImgPreview);
            if (ImgPreviewWv != null) dropTargets.Add(ImgPreviewWv);
            dropTargets.AddRange(_slotBorders);
            dropTargets.AddRange(_slotBordersWv);

            foreach (var target in dropTargets)
            {
                if (target == null) continue;
                target.AllowDrop = true;
                target.DragOver += (s, e) =>
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                };
                target.Drop += Control_Drop;
            }
        }

        private void Src_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isMouseDownOnImage = true;
        }

        private void Src_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseDownOnImage = false;
        }

        private void Src_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isMouseDownOnImage && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPosition = e.GetPosition(null);
                if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isMouseDownOnImage = false;
                    StartDragDrop(sender);
                }
            }
        }

        private static TParent? FindParentBorderOrTarget<TParent>(DependencyObject? child) where TParent : DependencyObject
        {
            if (child == null) return null;
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is TParent target) return target;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void StartDragDrop(object sender)
        {
            try
            {
                BitmapSource? bitmap = null;
                string tempFileName = "dragged_image";

                FrameworkElement? element = sender as FrameworkElement;
                if (element != null)
                {
                    if (element.Name == "ImgPreview" || element.Name == "ImgPreviewWv")
                    {
                        bitmap = _activeLayer?.OriginalTransformBitmap ?? _activeLayer?.Bitmap ?? (ImgPreview?.Source as BitmapSource) ?? (ImgPreviewWv?.Source as BitmapSource);
                        tempFileName = "Main";
                    }
                    else if (element is Image img && (img.Name == "ImgPreview" || img.Name == "ImgPreviewWv"))
                    {
                        bitmap = _activeLayer?.OriginalTransformBitmap ?? _activeLayer?.Bitmap ?? (img.Source as BitmapSource);
                        tempFileName = "Main";
                    }
                    else
                    {
                        Border? border = element as Border ?? FindParentBorderOrTarget<Border>(element);
                        if (border != null && (border.Name == "ImgPreview" || border.Name == "ImgPreviewWv"))
                        {
                            bitmap = _activeLayer?.OriginalTransformBitmap ?? _activeLayer?.Bitmap ?? (ImgPreview?.Source as BitmapSource) ?? (ImgPreviewWv?.Source as BitmapSource);
                            tempFileName = "Main";
                        }
                        else if (border != null && border.Tag is string tagStr && int.TryParse(tagStr, out int idx) && idx >= 0 && idx < _secondaryImages.Count)
                        {
                            bitmap = _secondaryImages[idx].Bitmap;
                            tempFileName = $"{idx + 1}.Slot";
                        }
                        else if (element is Image slotImg && slotImg.Source is BitmapSource srcBmp)
                        {
                            bitmap = srcBmp;
                            tempFileName = "Slot";
                        }
                    }
                }

                if (bitmap == null) return;

                // Save bitmap to unique temporary file to prevent access conflicts
                var uniqueName = $"{tempFileName}_{Guid.NewGuid():N}.png";
                var tempPath = Path.Combine(Path.GetTempPath(), uniqueName);
                using (var fileStream = new FileStream(tempPath, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(fileStream);
                }

                var fileUri = new Uri(tempPath).AbsoluteUri;

                // Create DataObject supporting all standard WPF, Windows OLE, CefSharp, and Web Browser formats
                var data = new DataObject();

                // 1. FileDrop (CF_HDROP)
                var fileList = new System.Collections.Specialized.StringCollection { tempPath };
                data.SetFileDropList(fileList);

                // 2. Text & UnicodeText formats (file path + file URL for web drag handlers)
                data.SetData(DataFormats.Text, tempPath);
                data.SetData(DataFormats.UnicodeText, tempPath);

                // 3. Standard Web URI List format (text/uri-list)
                data.SetData("text/uri-list", fileUri);

                // 4. HTML format for web drop targets
                string htmlContent = $"<img src=\"{fileUri}\"/>";
                data.SetData(DataFormats.Html, GetHtmlDataFormatString(htmlContent));

                // 5. Raw Bitmap format
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        var enc = new PngBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(bitmap));
                        enc.Save(ms);
                        using (var sysBmp = new System.Drawing.Bitmap(ms))
                        {
                            data.SetData(DataFormats.Bitmap, sysBmp, true);
                        }
                    }
                }
                catch { }

                // Set drag ghost image
                SetDragImage(data, bitmap);

                // Execute drag drop
                DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting drag drop: {ex.Message}");
            }
        }

        private static string GetHtmlDataFormatString(string html)
        {
            string header =
                "Version:0.9\r\n" +
                "StartHTML:0000000000\r\n" +
                "EndHTML:0000000000\r\n" +
                "StartFragment:0000000000\r\n" +
                "EndFragment:0000000000\r\n";
            string fragmentStart = "<!--StartFragment-->";
            string fragmentEnd = "<!--EndFragment-->";

            string fullHtml = "<html><body>" + fragmentStart + html + fragmentEnd + "</body></html>";

            int startHtml = header.Length;
            int startFragment = startHtml + "<html><body>".Length + fragmentStart.Length;
            int endFragment = startFragment + System.Text.Encoding.UTF8.GetByteCount(html);
            int endHtml = startFragment + System.Text.Encoding.UTF8.GetByteCount(html + fragmentEnd + "</body></html>");

            string formattedHeader =
                $"Version:0.9\r\n" +
                $"StartHTML:{startHtml:D10}\r\n" +
                $"EndHTML:{endHtml:D10}\r\n" +
                $"StartFragment:{startFragment:D10}\r\n" +
                $"EndFragment:{endFragment:D10}\r\n";

            return formattedHeader + fullHtml;
        }

        private void SlotBorder_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
                {
                    if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
                    {
                        _secondaryImages[idx].Bitmap = null;
                        _secondaryImages[idx].FilePath = null;
                        _secondaryImages[idx].ImageId = null;
                        _secondaryImages[idx].IsSelected = false;
                        RefreshAllSlotsUI();
                        UpdateSecondaryInfo();
                        UpdatePreviewImage();
                    }
                    e.Handled = true;
                }
            }
        }

        private async void Control_Drop(object sender, DragEventArgs e)
        {
            try
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;

                if (e.Data.GetDataPresent("LayerAiHistoryItem"))
                {
                    if (LayerAiDialog.DraggedHistoryItem != null)
                    {
                        ProcessDroppedHistoryItem(sender, LayerAiDialog.DraggedHistoryItem);
                        return;
                    }
                    else if (e.Data.GetData("LayerAiHistoryItem") is SecondaryImageItem historyItem)
                    {
                        ProcessDroppedHistoryItem(sender, historyItem);
                        return;
                    }
                }

                BitmapSource? droppedBitmap = await GetImageFromDragEventArgsAsync(e);

                if (droppedBitmap != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProcessDroppedImage(sender, droppedBitmap);
                    });
                }

                // Reset CefSharp drag state asynchronously without blocking UI updates
                _ = Task.Run(() => ResetWebView2DragStateAsync());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error on drop: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ResetWebView2DragStateAsync()
        {
            try
            {
                var cacheState = LayerAiWebViewCache.GetOrCreateState(_node.Id);
                foreach (var cachedTab in cacheState.WebBrowsers)
                {
                    var activeWv = cachedTab.WebView;
                    if (activeWv != null)
                    {
                        try { await activeWv.EvaluateScriptAsync("if (window.resetDragState) window.resetDragState();"); } catch { }
                    }
                }
                var dynamicWv = cacheState.DynamicWebView;
                if (dynamicWv != null)
                {
                    await dynamicWv.EvaluateScriptAsync("if (window.resetDragState) window.resetDragState();");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to reset drag state: {ex.Message}");
            }
        }

        private async Task<BitmapSource?> GetImageFromDragEventArgsAsync(DragEventArgs e)
        {
            // 0a. Check FileContents (direct original compressed file data from Chrome/Edge)
            if (e.Data.GetDataPresent("FileContents"))
            {
                try
                {
                    var raw = e.Data.GetData("FileContents");
                    if (raw is MemoryStream ms)
                    {
                        ms.Position = 0;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                    else if (raw is Stream stm)
                    {
                        using var msCopy = new MemoryStream();
                        stm.CopyTo(msCopy);
                        msCopy.Position = 0;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = msCopy;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
                catch { }

                var comBmp = GetImageFromFileContentsCOM(e.Data);
                if (comBmp != null) return comBmp;
            }

            // 0. Check DeviceIndependentBitmap / DeviceIndependentBitmapV5 (direct raw pixels from Chrome/Edge)
            if (e.Data.GetDataPresent("DeviceIndependentBitmap"))
            {
                var data = e.Data.GetData("DeviceIndependentBitmap");
                if (data is MemoryStream ms)
                {
                    var bmp = GetImageFromDIB(ms);
                    if (bmp != null) return bmp;
                }
            }
            if (e.Data.GetDataPresent("DeviceIndependentBitmapV5"))
            {
                var data = e.Data.GetData("DeviceIndependentBitmapV5");
                if (data is MemoryStream ms)
                {
                    var bmp = GetImageFromDIB(ms);
                    if (bmp != null) return bmp;
                }
            }

            // 1. Check Bitmap directly
            if (e.Data.GetDataPresent(DataFormats.Bitmap))
            {
                if (e.Data.GetData(DataFormats.Bitmap) is BitmapSource bmp)
                {
                    return bmp;
                }
            }

            // 2. Check FileDrop
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    var filePath = files[0];
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                            bmp.EndInit();
                            bmp.Freeze();
                            return bmp;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error loading dropped file: {ex.Message}");
                        }
                    }
                }
            }

            // 3. Try to extract URL from various formats
            string? url = null;
            string? sourcePageUrl = null;

            // 3a. Check HTML Format (HTML snippet dragged from WebView2/Browser)
            if (e.Data.GetDataPresent(DataFormats.Html))
            {
                if (e.Data.GetData(DataFormats.Html) is string htmlText)
                {
                    // Try to extract SourceURL from HTML format header
                    var sourceUrlMatch = System.Text.RegularExpressions.Regex.Match(htmlText, @"SourceURL:\s*([^\r\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (sourceUrlMatch.Success)
                    {
                        sourcePageUrl = sourceUrlMatch.Groups[1].Value.Trim();
                    }

                    // Try to find source attributes in order of preference (real lazy-loaded/responsive image url first)
                    string? foundUrl = null;
                    var attributes = new[] { "data-src", "data-original", "data-srcset", "srcset", "src" };
                    foreach (var attr in attributes)
                    {
                        var regex = new System.Text.RegularExpressions.Regex(
                            attr + @"\s*=\s*[""']([^""' >]+)[""']", 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        var match = regex.Match(htmlText);
                        if (match.Success)
                        {
                            foundUrl = match.Groups[1].Value;
                            if (attr == "srcset" || attr == "data-srcset")
                            {
                                var parts = foundUrl.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                var firstUrl = parts.FirstOrDefault(p => p.StartsWith("http") || p.StartsWith("//") || p.StartsWith("/"));
                                if (firstUrl != null) foundUrl = firstUrl;
                            }
                            break;
                        }
                    }

                    // Fallback to href if it points to an image
                    if (string.IsNullOrEmpty(foundUrl))
                    {
                        var linkMatch = System.Text.RegularExpressions.Regex.Match(htmlText, @"href\s*=\s*[""']([^""' >]+\.(?:png|jpg|jpeg|gif|webp|bmp))[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (linkMatch.Success)
                        {
                            foundUrl = linkMatch.Groups[1].Value;
                        }
                    }

                    // Fallback to CSS background-image url()
                    if (string.IsNullOrEmpty(foundUrl))
                    {
                        var bgMatch = System.Text.RegularExpressions.Regex.Match(htmlText, @"url\(\s*['""]?([^'"")]+?)['""]?\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (bgMatch.Success)
                        {
                            foundUrl = bgMatch.Groups[1].Value;
                        }
                    }

                    if (!string.IsNullOrEmpty(foundUrl))
                    {
                        url = foundUrl;
                        url = System.Net.WebUtility.HtmlDecode(url);
                    }
                }
            }

            // 3b. Check UniformResourceLocator (WebView2 drops URL as a MemoryStream)
            if (string.IsNullOrEmpty(url) && e.Data.GetDataPresent("UniformResourceLocator"))
            {
                var data = e.Data.GetData("UniformResourceLocator");
                if (data is MemoryStream ms)
                {
                    byte[] bytes = ms.ToArray();
                    string rawUrl = System.Text.Encoding.ASCII.GetString(bytes).Trim('\0');
                    if (!string.IsNullOrWhiteSpace(rawUrl))
                    {
                        url = rawUrl;
                    }
                }
            }

            // 3c. Check Text / UnicodeText
            if (string.IsNullOrEmpty(url))
            {
                if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    url = e.Data.GetData(DataFormats.Text) as string;
                }
                else if (e.Data.GetDataPresent(DataFormats.UnicodeText))
                {
                    url = e.Data.GetData(DataFormats.UnicodeText) as string;
                }
            }

            // 4. Resolve URL (either Web URL or Data URL)
            if (!string.IsNullOrWhiteSpace(url))
            {
                url = url.Trim();
                if (url.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateBitmapFromBase64(url);
                }

                Uri? uri = null;
                if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
                {
                    uri = absoluteUri;
                }
                else
                {
                    // Relative URL resolution based on base browser page URL
                    string? pageUrl = sourcePageUrl;
                    if (string.IsNullOrWhiteSpace(pageUrl))
                    {
                        ChromiumWebBrowser? activeWv = null;
                        if (_activeTab == ActiveTab.WebBrowser && _activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count) activeWv = _webTabs[_activeTabIdx].WebView;
                        else if (_activeTab == ActiveTab.WebView) activeWv = _dynamicWebView;
                        if (activeWv == null && _activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count) activeWv = _webTabs[_activeTabIdx].WebView;
                        if (activeWv == null) activeWv = _dynamicWebView;
                        if (activeWv != null && !string.IsNullOrWhiteSpace(activeWv.Address))
                        {
                            pageUrl = activeWv.Address;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(pageUrl)) pageUrl = _node?.LayerAiWebUrl;
                    if (string.IsNullOrWhiteSpace(pageUrl)) pageUrl = TxtWebUrl?.Text;

                    if (!string.IsNullOrWhiteSpace(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri))
                    {
                        if (Uri.TryCreate(baseUri, url, out var resolvedUri))
                        {
                            uri = resolvedUri;
                        }
                    }
                }

                if (uri != null)
                {
                    if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    {
                        return await DownloadImageAsync(uri);
                    }
                    else if (uri.Scheme == "data")
                    {
                        return CreateBitmapFromBase64(url);
                    }
                    else if (uri.Scheme == Uri.UriSchemeFile)
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.UriSource = uri;
                            bmp.EndInit();
                            bmp.Freeze();
                            return bmp;
                        }
                        catch { }
                    }
                }
            }

            return null;
        }

        private static void SetDragImage(DataObject dataObject, BitmapSource source)
        {
            try
            {
                // 1. Determine size (max 128x128)
                int maxW = 128;
                int maxH = 128;
                double ratio = (double)source.PixelWidth / source.PixelHeight;
                int targetW, targetH;
                if (ratio > 1)
                {
                    targetW = maxW;
                    targetH = (int)(maxW / ratio);
                }
                else
                {
                    targetH = maxH;
                    targetW = (int)(maxH * ratio);
                }
                if (targetW <= 0) targetW = 1;
                if (targetH <= 0) targetH = 1;

                // 2. Resize BitmapSource to target size
                var resized = ResizeBitmapHighQuality(source, targetW, targetH, uniformToFill: false);

                // 3. Convert to 32bpp ARGB GDI Bitmap and get Hbitmap
                IntPtr hBitmap = IntPtr.Zero;
                using (var ms = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(resized));
                    encoder.Save(ms);
                    using (var gdiBmp = new System.Drawing.Bitmap(ms))
                    {
                        hBitmap = gdiBmp.GetHbitmap();
                    }
                }

                if (hBitmap != IntPtr.Zero)
                {
                    try
                    {
                        var helper = (IDragSourceHelper)new DragDropHelper();
                        var pshdi = new SHDRAGIMAGE
                        {
                            sizeDragImage = new SIZE { cx = targetW, cy = targetH },
                            ptOffset = new POINT { x = targetW / 2, y = targetH / 2 },
                            hbmpDragImage = hBitmap,
                            crColorKey = 0x00000000
                        };
                        
                        helper.InitializeFromBitmap(ref pshdi, (System.Runtime.InteropServices.ComTypes.IDataObject)dataObject);
                    }
                    catch (InvalidCastException)
                    {
                        // Interface not supported on this thread/apartment, ignore silently
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set drag image: {ex.Message}");
            }
        }

        private static BitmapSource? GetImageFromDIB(MemoryStream dibStream)
        {
            try
            {
                byte[] dibBytes = dibStream.ToArray();
                if (dibBytes.Length < 40) return null; // BITMAPINFOHEADER is at least 40 bytes

                // Read BITMAPINFOHEADER fields
                int headerSize = BitConverter.ToInt32(dibBytes, 0);
                int width = BitConverter.ToInt32(dibBytes, 4);
                int height = BitConverter.ToInt32(dibBytes, 8);
                short planes = BitConverter.ToInt16(dibBytes, 12);
                short bitCount = BitConverter.ToInt16(dibBytes, 14);
                int compression = BitConverter.ToInt32(dibBytes, 16);
                int imageSize = BitConverter.ToInt32(dibBytes, 20);
                int colorsUsed = BitConverter.ToInt32(dibBytes, 32);

                // Calculate header sizes and offsets
                int colorTableSize = 0;
                if (bitCount <= 8)
                {
                    colorTableSize = (colorsUsed > 0 ? colorsUsed : (1 << bitCount)) * 4;
                }
                else if (compression == 3) // BI_BITFIELDS
                {
                    colorTableSize = 12; // 3 color masks (4 bytes each)
                }

                int pixelOffset = 14 + headerSize + colorTableSize;
                int totalFileSize = 14 + dibBytes.Length;

                byte[] bmpBytes = new byte[totalFileSize];
                
                // 1. Write BITMAPFILEHEADER
                // bfType (ASCII 'BM')
                bmpBytes[0] = 0x42;
                bmpBytes[1] = 0x4D;
                // bfSize
                Array.Copy(BitConverter.GetBytes(totalFileSize), 0, bmpBytes, 2, 4);
                // bfReserved1, bfReserved2 (0)
                bmpBytes[6] = 0;
                bmpBytes[7] = 0;
                bmpBytes[8] = 0;
                bmpBytes[9] = 0;
                // bfOffBits
                Array.Copy(BitConverter.GetBytes(pixelOffset), 0, bmpBytes, 10, 4);

                // 2. Copy the DIB bytes
                Array.Copy(dibBytes, 0, bmpBytes, 14, dibBytes.Length);

                // 3. Load using BmpBitmapDecoder
                using (var ms = new MemoryStream(bmpBytes))
                {
                    var decoder = new BmpBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count > 0)
                    {
                        var frame = decoder.Frames[0];
                        frame.Freeze();
                        return frame;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse DIB: {ex.Message}");
            }
            return null;
        }

        private void ProcessDroppedImage(object sender, BitmapSource bitmap)
        {
            bool isMainImage = false;
            FrameworkElement? feContainer = sender as FrameworkElement;
            Border? slotBorder = feContainer as Border ?? FindParentBorderOrTarget<Border>(feContainer as DependencyObject);

            if (feContainer != null && (feContainer.Name == "ImgPreview" || feContainer.Name == "ImgPreviewWv"))
            {
                isMainImage = true;
            }
            else if (slotBorder != null && (slotBorder.Name == "ImgPreview" || slotBorder.Name == "ImgPreviewWv"))
            {
                isMainImage = true;
            }

            if (feContainer != null)
            {
                FlashSlotBorder(feContainer);
            }

            if (isMainImage)
            {
                try
                {
                    int layerW = _activeLayer.Width;
                    int layerH = _activeLayer.Height;
                    var resized = ResizeBitmapHighQuality(bitmap, layerW, layerH, uniformToFill: true);
                    
                    var stride = layerW * 4;
                    var pixels = new byte[stride * layerH];
                    resized.CopyPixels(pixels, stride, 0);
                    
                    _activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, layerW, layerH), pixels, stride, 0);
                    _activeLayer.InvalidateThumbnail();
                    
                    UpdatePreviewImage();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to update main image from drop: {ex.Message}");
                }
                return;
            }

            // Otherwise, it is a slot drop
            int idx = -1;
            if (slotBorder != null && slotBorder.Tag is string tagStr && int.TryParse(tagStr, out int tagIdx))
            {
                idx = tagIdx;
            }
            else if (feContainer != null && feContainer.Tag is string tagStr2 && int.TryParse(tagStr2, out int tagIdx2))
            {
                idx = tagIdx2;
            }
            else if (feContainer != null)
            {
                var name = feContainer.Name ?? "";
                if (name.EndsWith("0")) idx = 0;
                else if (name.EndsWith("1")) idx = 1;
                else if (name.EndsWith("2")) idx = 2;
                else if (name.EndsWith("3")) idx = 3;
            }

            if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
            {
                _secondaryImages[idx].SetNewImage(bitmap, null);
                RefreshAllSlotsUI();
            }
            else
            {
                int targetIdx = -1;
                for (int i = 0; i < _secondaryImages.Count; i++)
                {
                    if (!_secondaryImages[i].HasImage)
                    {
                        targetIdx = i;
                        break;
                    }
                }
                if (targetIdx == -1) targetIdx = 0;

                _secondaryImages[targetIdx].SetNewImage(bitmap, null);
                RefreshAllSlotsUI();
            }
        }

        private void ProcessDroppedHistoryItem(object sender, SecondaryImageItem historyItem)
        {
            int idx = -1;
            FrameworkElement? feContainer = sender as FrameworkElement;
            Border? slotBorder = feContainer as Border ?? FindParentBorderOrTarget<Border>(feContainer as DependencyObject);

            if (slotBorder != null && slotBorder.Tag is string tagStr && int.TryParse(tagStr, out int tagIdx))
            {
                idx = tagIdx;
            }
            else if (feContainer != null && feContainer.Tag is string tagStr2 && int.TryParse(tagStr2, out int tagIdx2))
            {
                idx = tagIdx2;
            }
            else if (feContainer != null)
            {
                var name = feContainer.Name ?? "";
                if (name.EndsWith("0")) idx = 0;
                else if (name.EndsWith("1")) idx = 1;
                else if (name.EndsWith("2")) idx = 2;
                else if (name.EndsWith("3")) idx = 3;
            }

            if (feContainer != null) FlashSlotBorder(feContainer);

            if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
            {
                var item = _secondaryImages[idx];
                item.ArchiveCurrentIfHasId();
                item.Bitmap = historyItem.Bitmap;
                item.FilePath = historyItem.FilePath;
                item.ResetImageIdAndCodeId();
                if (!string.IsNullOrEmpty(historyItem.ImageId))
                {
                    item.ImageId = historyItem.ImageId;
                }
                if (!string.IsNullOrEmpty(historyItem.CodeId))
                {
                    item.CodeId = historyItem.CodeId;
                }
                if (historyItem.AspectRatioIds != null)
                {
                    foreach (var kvp in historyItem.AspectRatioIds)
                    {
                        item.AspectRatioIds[kvp.Key] = kvp.Value;
                    }
                }
                item.IsSelected = true;
                RefreshAllSlotsUI();
                UpdatePreviewImage();
            }
            else
            {
                int targetIdx = -1;
                for (int i = 0; i < _secondaryImages.Count; i++)
                {
                    if (!_secondaryImages[i].HasImage)
                    {
                        targetIdx = i;
                        break;
                    }
                }
                if (targetIdx == -1) targetIdx = 0;

                var item = _secondaryImages[targetIdx];
                item.ArchiveCurrentIfHasId();
                item.Bitmap = historyItem.Bitmap;
                item.FilePath = historyItem.FilePath;
                item.ResetImageIdAndCodeId();
                if (!string.IsNullOrEmpty(historyItem.ImageId))
                {
                    item.ImageId = historyItem.ImageId;
                }
                if (!string.IsNullOrEmpty(historyItem.CodeId))
                {
                    item.CodeId = historyItem.CodeId;
                }
                if (historyItem.AspectRatioIds != null)
                {
                    foreach (var kvp in historyItem.AspectRatioIds)
                    {
                        item.AspectRatioIds[kvp.Key] = kvp.Value;
                    }
                }
                item.IsSelected = true;
                RefreshAllSlotsUI();
                UpdatePreviewImage();
            }
        }

        private async void FlashSlotBorder(FrameworkElement slotContainer)
        {
            try
            {
                // Find parent border or border itself
                Border? border = slotContainer as Border;
                if (border == null && slotContainer is Image img)
                {
                    // Find the parent Border of the Image
                    border = img.Parent as Border;
                }

                if (border != null)
                {
                    var originalBrush = border.BorderBrush;
                    var originalThickness = border.BorderThickness;

                    // Flash vibrant green
                    var flashBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
                    border.BorderBrush = flashBrush;
                    border.BorderThickness = new Thickness(originalThickness.Left + 1);

                    // Blink: wait 150ms
                    await System.Threading.Tasks.Task.Delay(150);
                    border.BorderBrush = Brushes.Transparent;
                    await System.Threading.Tasks.Task.Delay(150);
                    
                    // Restore original
                    border.BorderBrush = originalBrush;
                    border.BorderThickness = originalThickness;
                }
            }
            catch { }
        }

        private async System.Threading.Tasks.Task<BitmapSource?> DownloadImageAsync(Uri uri)
        {
            try
            {
                // Find the active ChromiumWebBrowser instance to extract session cookies
                ChromiumWebBrowser? activeWv = null;
                if (_activeTab == ActiveTab.WebBrowser && _activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count) activeWv = _webTabs[_activeTabIdx].WebView;
                else if (_activeTab == ActiveTab.WebView) activeWv = _dynamicWebView;
                if (activeWv == null && _activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count) activeWv = _webTabs[_activeTabIdx].WebView;
                if (activeWv == null) activeWv = _dynamicWebView;

                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                    string? pageUrl = null;
                    if (activeWv != null && !string.IsNullOrWhiteSpace(activeWv.Address))
                    {
                        pageUrl = activeWv.Address;
                    }
                    if (string.IsNullOrWhiteSpace(pageUrl))
                    {
                        pageUrl = _node?.LayerAiWebUrl;
                    }
                    if (string.IsNullOrWhiteSpace(pageUrl))
                    {
                        pageUrl = TxtWebUrl?.Text;
                    }
                    
                    if (!string.IsNullOrWhiteSpace(pageUrl))
                    {
                        client.DefaultRequestHeaders.Referrer = new Uri(pageUrl);
                    }

                    if (activeWv != null)
                    {
                        try
                        {
                            ICookieManager? cookieManager = activeWv.RequestContext?.GetCookieManager(null) ?? Cef.GetGlobalCookieManager();
                            var cookieTask = cookieManager.VisitUrlCookiesAsync(uri.ToString(), true);
                            if (await System.Threading.Tasks.Task.WhenAny(cookieTask, System.Threading.Tasks.Task.Delay(150)) == cookieTask)
                            {
                                var cookies = await cookieTask;
                                if (cookies != null && cookies.Count > 0)
                                {
                                    var cookiePairs = cookies.Select(c => $"{c.Name}={c.Value}");
                                    string cookieHeaderValue = string.Join("; ", cookiePairs);
                                    client.DefaultRequestHeaders.Add("Cookie", cookieHeaderValue);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to get CefSharp cookies: {ex.Message}");
                        }
                    }

                    var data = await client.GetByteArrayAsync(uri);
                    using (var ms = new System.IO.MemoryStream(data))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to download dropped image: {ex.Message}");
                return null;
            }
        }

        private static BitmapSource? GetImageFromFileContentsCOM(System.Windows.IDataObject dataObject)
        {
            try
            {
                if (!(dataObject is System.Runtime.InteropServices.ComTypes.IDataObject comDataObject))
                    return null;

                // Get format ID for FileContents
                int formatId = System.Windows.DataFormats.GetDataFormat("FileContents").Id;
                if (formatId == 0) return null;

                var formatetc = new System.Runtime.InteropServices.ComTypes.FORMATETC
                {
                    cfFormat = (short)formatId,
                    dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,
                    lindex = 0, // first file
                    tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_ISTREAM | System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL
                };

                System.Runtime.InteropServices.ComTypes.STGMEDIUM medium;
                comDataObject.GetData(ref formatetc, out medium);
                
                if (medium.tymed == System.Runtime.InteropServices.ComTypes.TYMED.TYMED_ISTREAM && medium.unionmember != IntPtr.Zero)
                {
                    var stream = (System.Runtime.InteropServices.ComTypes.IStream)System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(medium.unionmember);
                    using (var ms = new MemoryStream())
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        IntPtr bytesReadPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(int));
                        try
                        {
                            do
                            {
                                stream.Read(buffer, buffer.Length, bytesReadPtr);
                                bytesRead = System.Runtime.InteropServices.Marshal.ReadInt32(bytesReadPtr);
                                if (bytesRead > 0)
                                {
                                    ms.Write(buffer, 0, bytesRead);
                                }
                            } while (bytesRead > 0);
                        }
                        finally
                        {
                            System.Runtime.InteropServices.Marshal.FreeHGlobal(bytesReadPtr);
                        }

                        ms.Position = 0;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
                else if (medium.tymed == System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL && medium.unionmember != IntPtr.Zero)
                {
                    IntPtr hGlobal = medium.unionmember;
                    IntPtr ptr = GlobalLock(hGlobal);
                    try
                    {
                        int size = GlobalSize(hGlobal);
                        if (size > 0)
                        {
                            byte[] bytes = new byte[size];
                            System.Runtime.InteropServices.Marshal.Copy(ptr, bytes, 0, size);
                            using (var ms = new MemoryStream(bytes))
                            {
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.StreamSource = ms;
                                bmp.EndInit();
                                bmp.Freeze();
                                return bmp;
                            }
                        }
                    }
                    finally
                    {
                        GlobalUnlock(hGlobal);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"COM FileContents retrieval failed: {ex.Message}");
            }
            return null;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern int GlobalSize(IntPtr hMem);
 
        #endregion

    }

    public static class LayerAiWebViewCache
    {
        private static readonly System.Collections.Generic.Dictionary<string, CachedWebViewState> _cache = new();

        public class CachedTabState
        {
            public ChromiumWebBrowser? WebView { get; set; }
            public string Url { get; set; } = "https://google.com";
            public string Title { get; set; } = "New Tab";
            public string ProfileName { get; set; } = "Shared";
        }

        public class CachedWebViewState
        {
            public ChromiumWebBrowser? DynamicWebView { get; set; }
            public System.Collections.Generic.List<CachedTabState> WebBrowsers { get; set; } = new();
            public string SplitMode { get; set; } = "Single";
            public int ActiveTabIdx { get; set; } = 0;
            public DateTime LastUsed { get; set; } = DateTime.Now;
            public System.Timers.Timer? SleepTimer { get; set; }
        }

        public static CachedWebViewState GetOrCreateState(string nodeId)
        {
            lock (_cache)
            {
                if (!_cache.TryGetValue(nodeId, out var state))
                {
                    state = new CachedWebViewState();
                    _cache[nodeId] = state;
                }
                state.LastUsed = DateTime.Now;

                // Stop sleep timer if it is running
                if (state.SleepTimer != null)
                {
                    state.SleepTimer.Stop();
                    state.SleepTimer.Dispose();
                    state.SleepTimer = null;
                }

                return state;
            }
        }

        public static void ReleaseToSleep(string nodeId)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(nodeId, out var state))
                {
                    state.LastUsed = DateTime.Now;

                    // Set a timer to put WebView2s to sleep after 10 minutes (600,000 ms)
                    state.SleepTimer?.Stop();
                    state.SleepTimer?.Dispose();

                    state.SleepTimer = new System.Timers.Timer(10 * 60 * 1000); // 10 minutes
                    state.SleepTimer.AutoReset = false;
                    state.SleepTimer.Elapsed += (s, e) =>
                    {
                        PutWebViewsToSleep(nodeId);
                    };
                    state.SleepTimer.Start();
                }
            }
        }

        private static void PutWebViewsToSleep(string nodeId)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(nodeId, out var state))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"ChromiumWebBrowser idle check for node {nodeId}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error putting browser to sleep: {ex.Message}");
                        }
                    });
                }
            }
        }

        public static void DisposeAll(string nodeId)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(nodeId, out var state))
                {
                    state.SleepTimer?.Stop();
                    state.SleepTimer?.Dispose();

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            state.DynamicWebView?.Dispose();
                            foreach (var tabState in state.WebBrowsers)
                            {
                                tabState.WebView?.Dispose();
                            }
                        }
                        catch { }
                    });
                    _cache.Remove(nodeId);
                }
            }
        }

        public static void DisposeAll()
        {
            lock (_cache)
            {
                var keys = System.Linq.Enumerable.ToList(_cache.Keys);
                foreach (var key in keys)
                {
                    DisposeAll(key);
                }
            }
        }
    }

    public static class LayerAiDialogManager
    {
        private class DialogCacheItem
        {
            public LayerAiDialog Dialog { get; set; } = null!;
            public System.Threading.Timer? IdleTimer { get; set; }
            public string NodeId { get; set; } = string.Empty;
        }

        private static readonly System.Collections.Generic.Dictionary<string, DialogCacheItem> _cache = new();
        private static readonly object _lock = new();

        public static LayerAiDialog OpenDialog(System.Collections.Generic.List<EditorLayer> selectedLayers, EditorLayer activeLayer, ImageProcessingNode node, IWorkflowEditorHost host, EditorDocument doc, Window? owner)
        {
            DialogCacheItem? item = null;
            string nodeId = node.Id;

            lock (_lock)
            {
                if (_cache.TryGetValue(nodeId, out var existingItem) && existingItem.Dialog != null)
                {
                    item = existingItem;
                    item.IdleTimer?.Dispose();
                    item.IdleTimer = null;
                }
            }

            if (item != null && item.Dialog != null)
            {
                bool reusedSuccessfully = false;
                var dialog = item.Dialog;

                try
                {
                    void ReinitUI()
                    {
                        if (!dialog.IsClosed)
                        {
                            dialog.ReinitializeSession(selectedLayers, activeLayer, node, host, doc, owner);
                            if (!dialog.IsVisible)
                            {
                                dialog.Show();
                            }
                            dialog.Activate();
                            dialog.Topmost = true;
                            reusedSuccessfully = true;
                        }
                    }

                    if (dialog.Dispatcher.CheckAccess())
                    {
                        ReinitUI();
                    }
                    else
                    {
                        dialog.Dispatcher.Invoke(ReinitUI);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LayerAiDialogManager] Reusing window failed: {ex.Message}");
                }

                if (reusedSuccessfully)
                {
                    return dialog;
                }

                RemoveFromCache(nodeId);
            }

            LayerAiDialog newDialog;
            var dispatcher = System.Windows.Application.Current?.Dispatcher;

            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                newDialog = dispatcher.Invoke(() => new LayerAiDialog(selectedLayers, activeLayer, node, host, doc, owner));
            }
            else
            {
                newDialog = new LayerAiDialog(selectedLayers, activeLayer, node, host, doc, owner);
            }

            var newItem = new DialogCacheItem
            {
                Dialog = newDialog,
                NodeId = nodeId
            };

            lock (_lock)
            {
                _cache[nodeId] = newItem;
            }

            void ShowUI()
            {
                newDialog.Show();
                newDialog.Activate();
            }

            if (newDialog.Dispatcher.CheckAccess())
            {
                ShowUI();
            }
            else
            {
                newDialog.Dispatcher.BeginInvoke(new Action(ShowUI));
            }

            return newDialog;
        }

        public static void RemoveFromCache(string nodeId)
        {
            System.Threading.Timer? timerToDispose = null;
            lock (_lock)
            {
                if (_cache.TryGetValue(nodeId, out var item))
                {
                    timerToDispose = item.IdleTimer;
                    item.IdleTimer = null;
                    _cache.Remove(nodeId);
                }
            }

            timerToDispose?.Dispose();
        }

        public static void OnDialogHidden(string nodeId)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(nodeId, out var item))
                {
                    item.IdleTimer?.Dispose();
                    item.IdleTimer = new System.Threading.Timer(OnIdleTimerExpired, nodeId, TimeSpan.FromMinutes(3), System.Threading.Timeout.InfiniteTimeSpan);
                }
            }
        }

        private static void OnIdleTimerExpired(object? state)
        {
            if (state is string nodeId)
            {
                CloseAndDisposeDialog(nodeId);
            }
        }

        public static void CloseAndDisposeDialog(string nodeId)
        {
            LayerAiDialog? dialogToClose = null;
            System.Threading.Timer? timerToDispose = null;

            lock (_lock)
            {
                if (_cache.TryGetValue(nodeId, out var item))
                {
                    timerToDispose = item.IdleTimer;
                    item.IdleTimer = null;
                    dialogToClose = item.Dialog;
                    _cache.Remove(nodeId);
                }
            }

            timerToDispose?.Dispose();

            if (dialogToClose != null)
            {
                try
                {
                    var disp = dialogToClose.Dispatcher;
                    if (disp != null && !disp.HasShutdownFinished && !disp.HasShutdownStarted)
                    {
                        disp.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                dialogToClose.ForceClose();
                            }
                            catch { }
                        }));
                    }
                }
                catch { }
            }

            try
            {
                LayerAiWebViewCache.DisposeAll(nodeId);
            }
            catch { }
        }

        public static void CloseAll()
        {
            System.Collections.Generic.List<string> keys;
            lock (_lock)
            {
                keys = System.Linq.Enumerable.ToList(_cache.Keys);
            }

            foreach (var key in keys)
            {
                CloseAndDisposeDialog(key);
            }
        }
    }

    #region Drag Drop Ghost COM Interfaces & Helpers
    [ComImport]
    [Guid("DE5CB7E3-F38A-4818-B9C3-0D3D322B60CC")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDragSourceHelper
    {
        void InitializeFromBitmap(
            ref SHDRAGIMAGE pshdi,
            System.Runtime.InteropServices.ComTypes.IDataObject pDataObject);

        void InitializeFromWindow(
            IntPtr hwnd,
            ref POINT ppt,
            System.Runtime.InteropServices.ComTypes.IDataObject pDataObject);
    }

    [ComImport]
    [Guid("4657278A-411B-11d2-839A-00C04FD918D0")]
    public class DragDropHelper
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SHDRAGIMAGE
    {
        public SIZE sizeDragImage;
        public POINT ptOffset;
        public IntPtr hbmpDragImage;
        public int crColorKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE
    {
        public int cx;
        public int cy;
    }
    #endregion

}
