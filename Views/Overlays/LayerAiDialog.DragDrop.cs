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
using System.IO;
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
                    if (element == ImgPreview || element == ImgPreviewWv)
                    {
                        bitmap = _activeLayer?.OriginalTransformBitmap ?? _activeLayer?.Bitmap ?? (ImgPreview?.Source as BitmapSource) ?? (ImgPreviewWv?.Source as BitmapSource);
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

                    if (border != null && (border.Name == "ImgPreview" || border.Name == "ImgPreviewWv" || border.Tag is string))
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
            try
            {
                // 1. Direct BitmapSource drop (from WPF internal drag)
                if (e.Data.GetDataPresent(typeof(BitmapSource)))
                {
                    if (e.Data.GetData(typeof(BitmapSource)) is BitmapSource bmp)
                    {
                        ProcessDroppedImage(sender, bmp);
                        return;
                    }
                }

                // 2. FileDrop (from Windows Explorer or external app)
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
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.UriSource = new Uri(filePath);
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.EndInit();
                                bmp.Freeze();

                                ProcessDroppedImage(sender, bmp);
                                return;
                            }
                        }
                    }
                }

                string? url = null;

                // 3. Check HTML format for <img> src attribute
                if (e.Data.GetDataPresent(DataFormats.Html))
                {
                    var htmlText = e.Data.GetData(DataFormats.Html) as string;
                    if (!string.IsNullOrEmpty(htmlText))
                    {
                        var srcMatch = System.Text.RegularExpressions.Regex.Match(htmlText, @"<img[^>]+(?:src|data-src|srcset)\s*=\s*[""']([^""'>]+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (srcMatch.Success)
                        {
                            var foundUrl = srcMatch.Groups[1].Value;
                            if (foundUrl.StartsWith("data:image/"))
                            {
                                var base64Bmp = LoadBitmapFromBase64DataUrl(foundUrl);
                                if (base64Bmp != null)
                                {
                                    ProcessDroppedImage(sender, base64Bmp);
                                    return;
                                }
                            }
                            else
                            {
                                var attrs = new[] { "src", "data-src", "data-original", "data-lazy-src", "srcset" };
                                foreach (var attr in attrs)
                                {
                                    var match = System.Text.RegularExpressions.Regex.Match(htmlText, $@"{attr}\s*=\s*[""']([^""'>]+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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
                    }
                }

                // 4. Check UniformResourceLocator (CefSharp / Edge / IE drops URL as MemoryStream)
                if (string.IsNullOrEmpty(url))
                {
                    string[] urlFormats = new[] { "UniformResourceLocator", "UniformResourceLocatorW", "System.String" };
                    foreach (var fmt in urlFormats)
                    {
                        if (e.Data.GetDataPresent(fmt))
                        {
                            var data = e.Data.GetData(fmt);
                            if (data is MemoryStream ms)
                            {
                                byte[] bytes = ms.ToArray();
                                string rawUrl = System.Text.Encoding.Unicode.GetString(bytes).Trim('\0');
                                if (!rawUrl.StartsWith("http"))
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

                // 5. Check Text / UnicodeText for image URL
                if (string.IsNullOrEmpty(url) && (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText)))
                {
                    string text = (e.Data.GetData(DataFormats.Text) as string) ?? (e.Data.GetData(DataFormats.UnicodeText) as string) ?? "";
                    text = text.Trim();
                    if (text.StartsWith("http://") || text.StartsWith("https://") || text.StartsWith("data:image/"))
                    {
                        url = text;
                    }
                }

                // If image URL is found, download or decode base64
                if (!string.IsNullOrEmpty(url))
                {
                    if (url.StartsWith("//")) url = "https:" + url;

                    if (url.StartsWith("data:image/"))
                    {
                        var base64Bmp = LoadBitmapFromBase64DataUrl(url);
                        if (base64Bmp != null)
                        {
                            ProcessDroppedImage(sender, base64Bmp);
                            return;
                        }
                    }
                    else
                    {
                        var webBmp = await DownloadImageFromUrlAsync(url);
                        if (webBmp != null)
                        {
                            ProcessDroppedImage(sender, webBmp);
                            return;
                        }
                    }
                }

                // 6. DeviceIndependentBitmap / DIB drop (CefSharp native image drag)
                if (e.Data.GetDataPresent("DeviceIndependentBitmap"))
                {
                    if (e.Data.GetData("DeviceIndependentBitmap") is MemoryStream dibMs)
                    {
                        var dibBmp = LoadBitmapFromDibStream(dibMs);
                        if (dibBmp != null)
                        {
                            ProcessDroppedImage(sender, dibBmp);
                            return;
                        }
                    }
                }

                // 7. COM FileContents drop (CefSharp / Edge download drop)
                if (e.Data.GetDataPresent("FileContents"))
                {
                    var contentsBmp = LoadBitmapFromFileContentsData(e.Data);
                    if (contentsBmp != null)
                    {
                        ProcessDroppedImage(sender, contentsBmp);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to handle drop: {ex.Message}");
            }
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

        private static async Task<BitmapSource?> DownloadImageFromUrlAsync(string url)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                byte[] data = await client.GetByteArrayAsync(url);
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to download image from URL '{url}': {ex.Message}");
            }
            return null;
        }

        private static BitmapSource? LoadBitmapFromDibStream(MemoryStream ms)
        {
            try
            {
                byte[] dibBytes = ms.ToArray();
                if (dibBytes.Length < 40) return null;

                // Create standard BITMAPFILEHEADER (14 bytes)
                int pixelOffset = 14 + 40;
                int fileSize = 14 + dibBytes.Length;

                using (var bmpMs = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(bmpMs, System.Text.Encoding.Default, leaveOpen: true))
                    {
                        writer.Write((byte)'B');
                        writer.Write((byte)'M');
                        writer.Write(fileSize);
                        writer.Write((short)0);
                        writer.Write((short)0);
                        writer.Write(pixelOffset);
                        writer.Write(dibBytes);
                    }

                    bmpMs.Position = 0;
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = bmpMs;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
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
                var data = dataObject.GetData("FileContents");
                if (data is MemoryStream ms)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"COM FileContents retrieval failed: {ex.Message}");
            }
            return null;
        }

        private void ProcessDroppedImage(object sender, BitmapSource bitmap)
        {
            bool isMainImage = false;
            if (sender is FrameworkElement fe && (fe.Name == "ImgPreview" || fe.Name == "ImgPreviewWv"))
            {
                isMainImage = true;
            }

            if (sender is FrameworkElement feContainer)
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
            if (sender is FrameworkElement feSlot && feSlot.Tag is string tagStr && int.TryParse(tagStr, out int tagIdx))
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
