// =========================================================================================================
// AI / DEVELOPER ARCHITECTURAL DIRECTIVE:
// This file is part of the partial class LayerAiDialog (Views/Overlays/LayerAiDialog).
// CRITICAL RULE: DO NOT BLOAT OR STUFF EXCESSIVE LOGIC INTO A SINGLE FILE.
// Maintain each partial class file at a maximum size of ~1000 - 1500 lines of code.
// When adding new features or handlers, refactor and create dedicated partial class files per module.
// =========================================================================================================

using FlowMy.Models.ImageEditor;
using CefSharp;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.Overlays
{
    public partial class LayerAiDialog : Window
    {
        #region Two-Way Drag and Drop (WPF <-> WebView2 / CefSharp)

        private void SetupDragAndDrop()
        {
            // --- WPF-to-WebView Drag Source Setup ---
            var dragSources = new List<FrameworkElement>();
            if (ImgPreview != null) dragSources.Add(ImgPreview);
            if (ImgPreviewWv != null) dragSources.Add(ImgPreviewWv);
            if (BorderImgPreview != null) dragSources.Add(BorderImgPreview);
            if (BorderImgPreviewWv != null) dragSources.Add(BorderImgPreviewWv);
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
            if (BorderImgPreview != null) dropTargets.Add(BorderImgPreview);
            if (BorderImgPreviewWv != null) dropTargets.Add(BorderImgPreviewWv);
            dropTargets.AddRange(_slotBorders);
            dropTargets.AddRange(_slotBordersWv);
            dropTargets.AddRange(_slotImages);
            dropTargets.AddRange(_slotImagesWv);
            dropTargets.AddRange(_slotPlaceholders);
            dropTargets.AddRange(_slotPlaceholdersWv);

            foreach (var target in dropTargets)
            {
                if (target == null) continue;
                target.AllowDrop = true;
                target.DragOver -= Target_DragOver;
                target.DragOver += Target_DragOver;
                target.Drop -= Control_Drop;
                target.Drop += Control_Drop;
            }
        }

        private void Target_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
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
            if (e.LeftButton != MouseButtonState.Pressed || !_isMouseDownOnImage) return;

            Point currentPos = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isMouseDownOnImage = false; // Reset to prevent re-triggering

                if (sender is FrameworkElement element)
                {
                    BitmapSource? bitmap = null;
                    string tempFileName = "DragImage";

                    // Determine source bitmap based on sender element
                    if (element == ImgPreview || element == ImgPreviewWv || element == BorderImgPreview || element == BorderImgPreviewWv)
                    {
                        bitmap = _activeLayer?.OriginalTransformBitmap ?? _activeLayer?.Bitmap ?? (ImgPreview?.Source as BitmapSource) ?? (ImgPreviewWv?.Source as BitmapSource);
                        tempFileName = "Main";
                    }
                    else
                    {
                        Border? border = element as Border ?? FindParentBorderOrTarget<Border>(element);
                        if (border != null && (border.Name == "ImgPreview" || border.Name == "ImgPreviewWv" || border.Name == "BorderImgPreview" || border.Name == "BorderImgPreviewWv" || border == BorderImgPreview || border == BorderImgPreviewWv))
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

                    // 5. Native DIB / DeviceIndependentBitmap format
                    try
                    {
                        var dibStream = GetDibStreamFromBitmapSource(bitmap);
                        if (dibStream != null)
                        {
                            data.SetData("DeviceIndependentBitmap", dibStream);
                        }
                    }
                    catch { }

                    // 6. Direct BitmapSource format for WPF controls
                    data.SetData(typeof(BitmapSource), bitmap);

                    // Trigger DragDrop operation
                    try
                    {
                        System.Windows.DragDrop.DoDragDrop(element, data, DragDropEffects.Copy | DragDropEffects.Move);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"DoDragDrop exception: {ex.Message}");
                    }
                    finally
                    {
                        // Clean up temporary drag file after drop operation completes
                        Task.Run(async () =>
                        {
                            await Task.Delay(3000);
                            try
                            {
                                if (File.Exists(tempPath)) File.Delete(tempPath);
                            }
                            catch { }
                        });
                    }
                }
            }
        }

        private static T? FindParentBorderOrTarget<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParentBorderOrTarget<T>(parentObject);
        }

        private static string GetHtmlDataFormatString(string html)
        {
            string header =
                "Version:0.9\r\n" +
                "StartHTML:00000000\r\n" +
                "EndHTML:00000000\r\n" +
                "StartFragment:00000000\r\n" +
                "EndFragment:00000000\r\n";

            string htmlHeader = "<html><body><!--StartFragment-->";
            string htmlFooter = "<!--EndFragment--></body></html>";

            int startHTML = header.Length;
            int startFragment = startHTML + htmlHeader.Length;
            int endFragment = startFragment + System.Text.Encoding.UTF8.GetByteCount(html);
            int endHTML = endFragment + htmlFooter.Length;

            string formattedHeader =
                $"Version:0.9\r\n" +
                $"StartHTML:{startHTML:D8}\r\n" +
                $"EndHTML:{endHTML:D8}\r\n" +
                $"StartFragment:{startFragment:D8}\r\n" +
                $"EndFragment:{endFragment:D8}\r\n";

            return formattedHeader + htmlHeader + html + htmlFooter;
        }

        private static MemoryStream? GetDibStreamFromBitmapSource(BitmapSource bitmap)
        {
            if (bitmap == null) return null;

            var formatConverted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr24, null, 0);
            int width = formatConverted.PixelWidth;
            int height = formatConverted.PixelHeight;
            int stride = (width * 3 + 3) & ~3; // 4-byte aligned stride for DIB
            byte[] pixelData = new byte[stride * height];

            formatConverted.CopyPixels(pixelData, stride, 0);

            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, System.Text.Encoding.Default, leaveOpen: true))
            {
                // BITMAPINFOHEADER struct (40 bytes)
                writer.Write(40);                  // biSize
                writer.Write(width);               // biWidth
                writer.Write(height);              // biHeight (positive for bottom-up DIB)
                writer.Write((short)1);            // biPlanes
                writer.Write((short)24);           // biBitCount (24-bit BGR)
                writer.Write(0);                   // biCompression (BI_RGB)
                writer.Write(pixelData.Length);    // biSizeImage
                writer.Write(0);                   // biXPelsPerMeter
                writer.Write(0);                   // biYPelsPerMeter
                writer.Write(0);                   // biClrUsed
                writer.Write(0);                   // biClrImportant

                // Write pixel data line by line (bottom-up order for standard DIB format)
                for (int y = height - 1; y >= 0; y--)
                {
                    writer.Write(pixelData, y * stride, stride);
                }
            }

            ms.Position = 0;
            return ms;
        }

        private void LayerAiDialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // Check mouse position relative to window elements
                Point mousePos = Mouse.GetPosition(this);
                HitTestResult hitResult = VisualTreeHelper.HitTest(this, mousePos);

                if (hitResult != null && hitResult.VisualHit != null)
                {
                    var hitElement = hitResult.VisualHit as FrameworkElement;
                    var border = hitElement as Border ?? FindParentBorderOrTarget<Border>(hitResult.VisualHit);

                    if (border != null && (border.Name == "ImgPreview" || border.Name == "ImgPreviewWv" || border.Name == "BorderImgPreview" || border.Name == "BorderImgPreviewWv" || border == BorderImgPreview || border == BorderImgPreviewWv || border.Tag is string))
                    {
                        TryPasteImageFromClipboard(border);
                        e.Handled = true;
                    }
                }
            }
        }

        private void TryPasteImageFromClipboard(FrameworkElement targetElement)
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    var bitmap = Clipboard.GetImage();
                    if (bitmap != null)
                    {
                        ProcessDroppedImage(targetElement, bitmap);
                    }
                }
                else if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    if (files.Count > 0)
                    {
                        string filePath = files[0]!;
                        if (File.Exists(filePath))
                        {
                            var ext = Path.GetExtension(filePath).ToLowerInvariant();
                            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".webp")
                            {
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.UriSource = new Uri(filePath);
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.EndInit();
                                bmp.Freeze();

                                ProcessDroppedImage(targetElement, bmp);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to paste image from clipboard: {ex.Message}");
            }
        }

        private async void Control_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            try
            {
                var droppedBitmap = await GetImageFromDragEventArgsAsync(e);
                if (droppedBitmap != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProcessDroppedImage(sender, droppedBitmap);
                    });
                }

                // Reset CefSharp / web browser drag state
                _ = Task.Run(() => ResetWebBrowserDragStateAsync());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to handle drop: {ex.Message}");
            }
        }

        private async Task ResetWebBrowserDragStateAsync()
        {
            try
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    if (_webTabs != null)
                    {
                        foreach (var tab in _webTabs)
                        {
                            if (tab?.WebView != null)
                            {
                                try
                                {
                                    tab.WebView.GetBrowser()?.MainFrame?.ExecuteJavaScriptAsync("if (window.resetDragState) window.resetDragState();");
                                }
                                catch { }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to reset web browser drag state: {ex.Message}");
            }
        }

        private async Task<BitmapSource?> GetImageFromDragEventArgsAsync(DragEventArgs e)
        {
            // 1. Direct FileContents (COM or MemoryStream - direct original image data from Chromium/Edge)
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
                    else if (raw is MemoryStream[] msArray && msArray.Length > 0 && msArray[0] != null)
                    {
                        var ms0 = msArray[0];
                        ms0.Position = 0;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms0;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
                catch { }

                var comBmp = LoadBitmapFromFileContentsData(e.Data);
                if (comBmp != null) return comBmp;
            }

            // 2. DeviceIndependentBitmap / DeviceIndependentBitmapV5 (raw pixel bytes from Chromium/Edge)
            if (e.Data.GetDataPresent("DeviceIndependentBitmap"))
            {
                if (e.Data.GetData("DeviceIndependentBitmap") is MemoryStream dibMs)
                {
                    var dibBmp = LoadBitmapFromDibStream(dibMs);
                    if (dibBmp != null) return dibBmp;
                }
            }
            if (e.Data.GetDataPresent("DeviceIndependentBitmapV5"))
            {
                if (e.Data.GetData("DeviceIndependentBitmapV5") is MemoryStream dibMs5)
                {
                    var dibBmp = LoadBitmapFromDibStream(dibMs5);
                    if (dibBmp != null) return dibBmp;
                }
            }

            // 3. Direct WPF BitmapSource drop
            if (e.Data.GetDataPresent(typeof(BitmapSource)))
            {
                if (e.Data.GetData(typeof(BitmapSource)) is BitmapSource bmp)
                {
                    return bmp;
                }
            }
            if (e.Data.GetDataPresent(DataFormats.Bitmap))
            {
                if (e.Data.GetData(DataFormats.Bitmap) is BitmapSource bmp)
                {
                    return bmp;
                }
            }

            // 4. FileDrop (from Windows Explorer or external app)
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    string filePath = files[0];
                    if (File.Exists(filePath))
                    {
                        var ext = Path.GetExtension(filePath).ToLowerInvariant();
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".webp")
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
                            catch { }
                        }
                    }
                }
            }

            // 5. HTML format extraction
            string? url = null;
            string? sourcePageUrl = null;

            if (e.Data.GetDataPresent(DataFormats.Html))
            {
                string? htmlText = null;
                var rawHtml = e.Data.GetData(DataFormats.Html);
                if (rawHtml is string strHtml)
                {
                    htmlText = strHtml;
                }
                else if (rawHtml is MemoryStream htmlMs)
                {
                    htmlText = System.Text.Encoding.UTF8.GetString(htmlMs.ToArray());
                }

                if (!string.IsNullOrEmpty(htmlText))
                {
                    var sourceUrlMatch = System.Text.RegularExpressions.Regex.Match(htmlText, @"SourceURL:\s*([^\r\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (sourceUrlMatch.Success)
                    {
                        sourcePageUrl = sourceUrlMatch.Groups[1].Value.Trim();
                    }

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

                    if (string.IsNullOrEmpty(foundUrl))
                    {
                        var linkMatch = System.Text.RegularExpressions.Regex.Match(htmlText, @"href\s*=\s*[""']([^""' >]+\.(?:png|jpg|jpeg|gif|webp|bmp))[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (linkMatch.Success) foundUrl = linkMatch.Groups[1].Value;
                    }

                    if (string.IsNullOrEmpty(foundUrl))
                    {
                        var bgMatch = System.Text.RegularExpressions.Regex.Match(htmlText, @"url\(\s*['""]?([^'"")]+?)['""]?\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (bgMatch.Success) foundUrl = bgMatch.Groups[1].Value;
                    }

                    if (!string.IsNullOrEmpty(foundUrl))
                    {
                        url = System.Net.WebUtility.HtmlDecode(foundUrl);
                    }
                }
            }

            // 6. UniformResourceLocator / text/uri-list
            if (string.IsNullOrEmpty(url))
            {
                string[] urlFormats = new[] { "text/uri-list", "UniformResourceLocator", "UniformResourceLocatorW" };
                foreach (var fmt in urlFormats)
                {
                    if (e.Data.GetDataPresent(fmt))
                    {
                        var data = e.Data.GetData(fmt);
                        if (data is MemoryStream ms)
                        {
                            byte[] bytes = ms.ToArray();
                            string rawUrl = System.Text.Encoding.Unicode.GetString(bytes).Trim('\0');
                            if (!rawUrl.StartsWith("http") && !rawUrl.StartsWith("data:"))
                            {
                                rawUrl = System.Text.Encoding.ASCII.GetString(bytes).Trim('\0');
                            }
                            if (rawUrl.StartsWith("http://") || rawUrl.StartsWith("https://") || rawUrl.StartsWith("data:image/"))
                            {
                                url = rawUrl;
                                break;
                            }
                        }
                        else if (data is string strUrl && (strUrl.StartsWith("http://") || strUrl.StartsWith("https://") || strUrl.StartsWith("data:image/")))
                        {
                            url = strUrl;
                            break;
                        }
                    }
                }
            }

            // 7. Text / UnicodeText
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

            // 8. Resolve and load image from URL
            if (!string.IsNullOrWhiteSpace(url))
            {
                url = url.Trim();
                if (url.StartsWith("//")) url = "https:" + url;

                if (url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadBitmapFromBase64DataUrl(url);
                }

                Uri? uri = null;
                if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
                {
                    uri = absoluteUri;
                }
                else
                {
                    string? pageUrl = sourcePageUrl;
                    if (string.IsNullOrWhiteSpace(pageUrl))
                    {
                        if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count && _webTabs[_activeTabIdx].WebView != null)
                        {
                            pageUrl = _webTabs[_activeTabIdx].WebView.Address;
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
                        return await DownloadImageFromUrlAsync(uri.AbsoluteUri);
                    }
                    else if (uri.Scheme == "data")
                    {
                        return LoadBitmapFromBase64DataUrl(url);
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

        private static BitmapSource? LoadBitmapFromBase64DataUrl(string dataUrl)
        {
            try
            {
                int commaIdx = dataUrl.IndexOf(',');
                if (commaIdx >= 0)
                {
                    string base64Str = dataUrl.Substring(commaIdx + 1);
                    byte[] bytes = Convert.FromBase64String(base64Str);
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse base64 data URL: {ex.Message}");
            }
            return null;
        }

        private async Task<BitmapSource?> DownloadImageFromUrlAsync(string url)
        {
            try
            {
                ChromiumWebBrowser? activeWv = null;
                if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count) activeWv = _webTabs[_activeTabIdx].WebView;
                else if (_dynamicWebView != null) activeWv = _dynamicWebView;

                Uri? uri = null;
                if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
                {
                    string? basePageUrl = activeWv?.Address ?? _node?.LayerAiWebUrl ?? TxtWebUrl?.Text;
                    if (!string.IsNullOrWhiteSpace(basePageUrl) && Uri.TryCreate(basePageUrl, UriKind.Absolute, out var baseUri))
                    {
                        Uri.TryCreate(baseUri, url, out uri);
                    }
                }

                if (uri == null) return null;

                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                    string? pageUrl = activeWv?.Address ?? _node?.LayerAiWebUrl ?? TxtWebUrl?.Text;
                    if (!string.IsNullOrWhiteSpace(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageUri))
                    {
                        client.DefaultRequestHeaders.Referrer = pageUri;
                    }

                    try
                    {
                        ICookieManager? cookieManager = activeWv?.RequestContext?.GetCookieManager(null) ?? Cef.GetGlobalCookieManager();
                        if (cookieManager != null)
                        {
                            var cookieTask = cookieManager.VisitUrlCookiesAsync(uri.ToString(), true);
                            if (await Task.WhenAny(cookieTask, Task.Delay(200)) == cookieTask)
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
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to get CefSharp cookies: {ex.Message}");
                    }

                    byte[] data = await client.GetByteArrayAsync(uri);
                    using (var ms = new MemoryStream(data))
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to download image from URL '{url}': {ex.Message}");
            }
            return null;
        }

        private static BitmapSource? LoadBitmapFromDibStream(MemoryStream dibStream)
        {
            try
            {
                byte[] dibBytes = dibStream.ToArray();
                if (dibBytes.Length < 40) return null;

                int headerSize = BitConverter.ToInt32(dibBytes, 0);
                int width = BitConverter.ToInt32(dibBytes, 4);
                int height = BitConverter.ToInt32(dibBytes, 8);
                short planes = BitConverter.ToInt16(dibBytes, 12);
                short bitCount = BitConverter.ToInt16(dibBytes, 14);
                int compression = BitConverter.ToInt32(dibBytes, 16);
                int imageSize = BitConverter.ToInt32(dibBytes, 20);
                int colorsUsed = BitConverter.ToInt32(dibBytes, 32);

                int colorTableSize = 0;
                if (bitCount <= 8)
                {
                    colorTableSize = (colorsUsed > 0 ? colorsUsed : (1 << bitCount)) * 4;
                }
                else if (compression == 3) // BI_BITFIELDS
                {
                    colorTableSize = 12;
                }

                int pixelOffset = 14 + headerSize + colorTableSize;
                int totalFileSize = 14 + dibBytes.Length;

                byte[] bmpBytes = new byte[totalFileSize];
                bmpBytes[0] = 0x42;
                bmpBytes[1] = 0x4D;
                Array.Copy(BitConverter.GetBytes(totalFileSize), 0, bmpBytes, 2, 4);
                bmpBytes[6] = 0; bmpBytes[7] = 0; bmpBytes[8] = 0; bmpBytes[9] = 0;
                Array.Copy(BitConverter.GetBytes(pixelOffset), 0, bmpBytes, 10, 4);
                Array.Copy(dibBytes, 0, bmpBytes, 14, dibBytes.Length);

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
                System.Diagnostics.Debug.WriteLine($"Failed to parse DIB stream: {ex.Message}");
            }
            return null;
        }

        private static BitmapSource? LoadBitmapFromFileContentsData(IDataObject dataObject)
        {
            try
            {
                if (!(dataObject is System.Runtime.InteropServices.ComTypes.IDataObject comDataObject))
                    return null;

                int formatId = System.Windows.DataFormats.GetDataFormat("FileContents").Id;
                if (formatId == 0) return null;

                var formatetc = new System.Runtime.InteropServices.ComTypes.FORMATETC
                {
                    cfFormat = (short)formatId,
                    dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,
                    lindex = 0,
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"COM FileContents retrieval failed: {ex.Message}");
            }
            return null;
        }

        private void ProcessDroppedImage(object sender, BitmapSource bitmap)
        {
            bool isMainImage = false;
            if (sender is FrameworkElement fe)
            {
                if (fe == ImgPreview || fe == ImgPreviewWv || fe == BorderImgPreview || fe == BorderImgPreviewWv)
                {
                    isMainImage = true;
                }
                else if (fe.Name == "ImgPreview" || fe.Name == "ImgPreviewWv" || fe.Name == "BorderImgPreview" || fe.Name == "BorderImgPreviewWv")
                {
                    isMainImage = true;
                }
                else
                {
                    var parentBorder = FindParentBorderOrTarget<Border>(fe);
                    if (parentBorder != null && (parentBorder.Name == "BorderImgPreview" || parentBorder.Name == "BorderImgPreviewWv" || parentBorder == BorderImgPreview || parentBorder == BorderImgPreviewWv))
                    {
                        isMainImage = true;
                    }
                }

                FlashSlotBorder(fe);
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
            Border? slotBorder = (sender as Border) ?? FindParentBorderOrTarget<Border>(sender as DependencyObject);
            if (slotBorder != null && slotBorder.Tag is string tagStr && int.TryParse(tagStr, out int tagIdx))
            {
                idx = tagIdx;
            }
            else if (sender is FrameworkElement feSlot2)
            {
                var name = feSlot2.Name ?? "";
                if (name.EndsWith("0")) idx = 0;
                else if (name.EndsWith("1")) idx = 1;
                else if (name.EndsWith("2")) idx = 2;
                else if (name.EndsWith("3")) idx = 3;
            }

            if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
            {
                _secondaryImages[idx].Bitmap = bitmap;
                _secondaryImages[idx].FilePath = null;
                _secondaryImages[idx].IsSelected = true;
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

                _secondaryImages[targetIdx].Bitmap = bitmap;
                _secondaryImages[targetIdx].FilePath = null;
                _secondaryImages[targetIdx].IsSelected = true;
                RefreshAllSlotsUI();
            }
        }

        private async void FlashSlotBorder(FrameworkElement slotContainer)
        {
            if (slotContainer is Border border)
            {
                var oldBrush = border.BorderBrush;
                border.BorderBrush = FindResource("AccentColor") as Brush ?? Brushes.Lime;
                await Task.Delay(300);
                border.BorderBrush = oldBrush;
            }
        }

        #endregion
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
