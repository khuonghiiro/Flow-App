using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows;

namespace FlowMy.ViewModels
{
    public partial class ImageProcessingNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly ImageProcessingNode _imageNode;

        [ObservableProperty] private string _imageUrl = string.Empty;
        [ObservableProperty] private string? _imageUrlSourceNodeId;
        [ObservableProperty] private string? _imageUrlSourceOutputKey;
        [ObservableProperty] private ObservableCollection<WorkflowOutputKeyOption> _urlKeyOptions = new();
        
        private static readonly HttpClient _httpClient = CreateHttpClient();
        private bool _isDownloadingUrl = false;
        
        
        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(2)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            return client;
        }

        [ObservableProperty] private string _imageBase64 = string.Empty;
        [ObservableProperty] private string? _imageBase64SourceNodeId;
        [ObservableProperty] private string? _imageBase64SourceOutputKey;
        [ObservableProperty] private ObservableCollection<WorkflowOutputKeyOption> _base64KeyOptions = new();

        [ObservableProperty] private ImageInputMode _inputMode = ImageInputMode.Url;

        [ObservableProperty] private bool _preferGpu = true;
        [ObservableProperty] private string _ffmpegFilter = string.Empty;

        [ObservableProperty] private string _croppedFolderPath = string.Empty;
        [ObservableProperty] private string? _croppedFolderSourceNodeId;
        [ObservableProperty] private string? _croppedFolderSourceOutputKey;

        [ObservableProperty] private string? _renderNodeId;
        [ObservableProperty] private string? _renderNodeOutputKey;
        [ObservableProperty] private string? _returnIdNodeId;
        [ObservableProperty] private string? _returnIdOutputKey;

        [ObservableProperty] private string _renderCodeIdKeys = "codeId, CodeId, code_id";
        [ObservableProperty] private string _renderImageIdKeys = "id, Id, ID, mediaId, imageId, assetId";
        [ObservableProperty] private string _renderImageLinkKeys = "linkImage, linkImg, link_image, imageUrl, url, src, link, path";

        [ObservableProperty] private string _returnCodeIdKeys = "codeId, CodeId, code_id";
        [ObservableProperty] private string _returnImageIdKeys = "id, Id, ID, mediaId, imageId, assetId";
        [ObservableProperty] private string _returnImageLinkKeys = "linkImage, linkImg, link_image, imageUrl, url, src, link, path";

        [ObservableProperty] private ObservableCollection<WorkflowOutputKeyOption> _renderNodeKeyOptions = new();
        [ObservableProperty] private ObservableCollection<WorkflowOutputKeyOption> _returnIdKeyOptions = new();
        [ObservableProperty] private ObservableCollection<WorkflowOutputKeyOption> _croppedFolderKeyOptions = new();

        [ObservableProperty] private ObservableCollection<WorkflowDataSourceOption> _availableNodeOptions = new();
        [ObservableProperty] private ObservableCollection<WorkflowDataSourceOption> _renderNodeOptions = new();
        [ObservableProperty] private ObservableCollection<WorkflowDataSourceOption> _returnIdNodeOptions = new();

        // ═══════ Layer AI: Dynamic UI Configuration ═══════
        [ObservableProperty] private string _layerAiHtmlCode = string.Empty;
        [ObservableProperty] private string _layerAiCssCode = string.Empty;
        [ObservableProperty] private string _layerAiJsCode = string.Empty;
        [ObservableProperty] private string _layerAiParamsCode = string.Empty;
        [ObservableProperty] private int _layerAiCodeFontSize = 13;

        public ObservableCollection<CodeInputMappingItemViewModel> LayerAiInputMappingsList { get; } = new();
        public ObservableCollection<WorkflowDataSourceOption> LayerAiNodeOptions { get; } = new();

        public ImageProcessingNodeDialogViewModel(ImageProcessingNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _imageNode = node ?? throw new ArgumentNullException(nameof(node));

            ImageUrl = _imageNode.ImageUrl ?? string.Empty;
            ImageUrlSourceNodeId = _imageNode.ImageUrlSourceNodeId;
            ImageUrlSourceOutputKey = _imageNode.ImageUrlSourceOutputKey;

            ImageBase64 = _imageNode.ImageBase64 ?? string.Empty;
            ImageBase64SourceNodeId = _imageNode.ImageBase64SourceNodeId;
            ImageBase64SourceOutputKey = _imageNode.ImageBase64SourceOutputKey;

            InputMode = _imageNode.InputMode;
            PreferGpu = _imageNode.PreferGpu;
            FfmpegFilter = _imageNode.FfmpegFilter ?? string.Empty;

            CroppedFolderPath = _imageNode.CroppedFolderPath ?? string.Empty;
            CroppedFolderSourceNodeId = _imageNode.CroppedFolderSourceNodeId;
            CroppedFolderSourceOutputKey = _imageNode.CroppedFolderSourceOutputKey;

            RenderNodeId = _imageNode.RenderNodeId;
            RenderNodeOutputKey = _imageNode.RenderNodeOutputKey;

            RenderCodeIdKeys = _imageNode.RenderCodeIdKeys;
            RenderImageIdKeys = _imageNode.RenderImageIdKeys;
            RenderImageLinkKeys = _imageNode.RenderImageLinkKeys;

            ReturnIdNodeId = _imageNode.ReturnIdNodeId;
            ReturnIdOutputKey = _imageNode.ReturnIdOutputKey;

            ReturnCodeIdKeys = _imageNode.ReturnCodeIdKeys;
            ReturnImageIdKeys = _imageNode.ReturnImageIdKeys;
            ReturnImageLinkKeys = _imageNode.ReturnImageLinkKeys;

            // Layer AI code
            LayerAiHtmlCode = _imageNode.LayerAiHtmlCode ?? string.Empty;
            LayerAiCssCode = _imageNode.LayerAiCssCode ?? string.Empty;
            LayerAiJsCode = _imageNode.LayerAiJsCode ?? string.Empty;
            LayerAiParamsCode = _imageNode.LayerAiParamsCode ?? string.Empty;

            RefreshAvailableNodes();
            RefreshRenderNodeOptions();
            LoadLayerAiInputMappings();

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) => OnNodePropertyChanged(e.PropertyName ?? string.Empty);
            }
            
            // Subscribe vào các property thay đổi để refresh key options
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ImageUrlSourceNodeId))
                    RefreshUrlKeyOptions();
                else if (e.PropertyName == nameof(ImageBase64SourceNodeId))
                    RefreshBase64KeyOptions();
                else if (e.PropertyName == nameof(RenderNodeId))
                    RefreshRenderNodeKeyOptions();
                else if (e.PropertyName == nameof(ReturnIdNodeId))
                    RefreshReturnIdKeyOptions();
                else if (e.PropertyName == nameof(CroppedFolderSourceNodeId))
                    RefreshCroppedFolderKeyOptions();
            };
        }

        protected override string GetDefaultTitle() => "Xử lý ảnh";

        public void RefreshAvailableNodes()
        {
            var list = new List<WorkflowDataSourceOption>();
            if (_host.ViewModel?.Nodes != null)
            {
                foreach (var n in _host.ViewModel.Nodes)
                {
                    if (ReferenceEquals(n, _imageNode)) continue;
                    EnsureNodeDynamicOutputsSynced(n);
                    if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                    list.Add(CreateDataSourceOption(n));
                }
            }

            var currentUrlNodeId = ImageUrlSourceNodeId;
            var currentBase64NodeId = ImageBase64SourceNodeId;
            var currentCroppedFolderNodeId = CroppedFolderSourceNodeId;

            AvailableNodeOptions = new ObservableCollection<WorkflowDataSourceOption>(list);

            if (!string.IsNullOrWhiteSpace(currentUrlNodeId) && list.Any(o => string.Equals(o.NodeId, currentUrlNodeId, StringComparison.OrdinalIgnoreCase)))
                ImageUrlSourceNodeId = currentUrlNodeId;
            if (!string.IsNullOrWhiteSpace(currentBase64NodeId) && list.Any(o => string.Equals(o.NodeId, currentBase64NodeId, StringComparison.OrdinalIgnoreCase)))
                ImageBase64SourceNodeId = currentBase64NodeId;
            if (!string.IsNullOrWhiteSpace(currentCroppedFolderNodeId) && list.Any(o => string.Equals(o.NodeId, currentCroppedFolderNodeId, StringComparison.OrdinalIgnoreCase)))
                CroppedFolderSourceNodeId = currentCroppedFolderNodeId;

            // Refresh key options khi danh sách node thay đổi
            RefreshUrlKeyOptions();
            RefreshBase64KeyOptions();
            RefreshCroppedFolderKeyOptions();
            RefreshReturnIdNodeOptions();
            RefreshRenderNodeOptions();
        }

        public void RefreshReturnIdNodeOptions()
        {
            var list = new List<WorkflowDataSourceOption>();
            if (_host.ViewModel?.Nodes != null)
            {
                foreach (var n in _host.ViewModel.Nodes)
                {
                    if (ReferenceEquals(n, _imageNode)) continue;
                    EnsureNodeDynamicOutputsSynced(n);
                    if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                    list.Add(CreateDataSourceOption(n));
                }
            }

            var currentId = ReturnIdNodeId;
            ReturnIdNodeOptions = new ObservableCollection<WorkflowDataSourceOption>(list);

            if (!string.IsNullOrWhiteSpace(currentId) && list.Any(o => string.Equals(o.NodeId, currentId, StringComparison.OrdinalIgnoreCase)))
            {
                ReturnIdNodeId = currentId;
            }

            RefreshReturnIdKeyOptions();
        }

        public void RefreshReturnIdKeyOptions()
        {
            var currentKey = ReturnIdOutputKey;
            var options = GetOutputKeysForNode(ReturnIdNodeId);
            ReturnIdKeyOptions = new ObservableCollection<WorkflowOutputKeyOption>(options);

            if (!string.IsNullOrWhiteSpace(currentKey) && options.Any(o => string.Equals(o.Key, currentKey, StringComparison.OrdinalIgnoreCase)))
            {
                ReturnIdOutputKey = currentKey;
            }
            else if (options.Count > 0 && string.IsNullOrWhiteSpace(ReturnIdOutputKey))
            {
                ReturnIdOutputKey = options[0].Key;
            }
        }

        public void RefreshRenderNodeOptions()
        {
            var list = new List<WorkflowDataSourceOption>();
            if (_host.ViewModel?.Nodes != null)
            {
                foreach (var n in _host.ViewModel.Nodes)
                {
                    if (ReferenceEquals(n, _imageNode)) continue;
                    EnsureNodeDynamicOutputsSynced(n);
                    if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                    list.Add(CreateDataSourceOption(n));
                }
            }

            var currentId = RenderNodeId;
            RenderNodeOptions = new ObservableCollection<WorkflowDataSourceOption>(list);

            if (!string.IsNullOrWhiteSpace(currentId) && list.Any(o => string.Equals(o.NodeId, currentId, StringComparison.OrdinalIgnoreCase)))
            {
                RenderNodeId = currentId;
            }

            RefreshRenderNodeKeyOptions();
        }

        public void RefreshRenderNodeKeyOptions()
        {
            var currentKey = RenderNodeOutputKey;
            var options = GetOutputKeysForNode(RenderNodeId);
            RenderNodeKeyOptions = new ObservableCollection<WorkflowOutputKeyOption>(options);

            if (!string.IsNullOrWhiteSpace(currentKey) && options.Any(o => string.Equals(o.Key, currentKey, StringComparison.OrdinalIgnoreCase)))
            {
                RenderNodeOutputKey = currentKey;
            }
            else if (options.Count > 0 && string.IsNullOrWhiteSpace(RenderNodeOutputKey))
            {
                RenderNodeOutputKey = options[0].Key;
            }
        }

        public void RefreshUrlKeyOptions()
        {
            var currentKey = ImageUrlSourceOutputKey;
            var options = GetOutputKeysForNode(ImageUrlSourceNodeId);
            UrlKeyOptions = new ObservableCollection<WorkflowOutputKeyOption>(options);

            if (!string.IsNullOrWhiteSpace(currentKey) && options.Any(o => string.Equals(o.Key, currentKey, StringComparison.OrdinalIgnoreCase)))
            {
                ImageUrlSourceOutputKey = currentKey;
            }
            else if (options.Count > 0 && string.IsNullOrWhiteSpace(ImageUrlSourceOutputKey))
            {
                ImageUrlSourceOutputKey = options[0].Key;
            }
        }

        public void RefreshBase64KeyOptions()
        {
            var currentKey = ImageBase64SourceOutputKey;
            var options = GetOutputKeysForNode(ImageBase64SourceNodeId);
            Base64KeyOptions = new ObservableCollection<WorkflowOutputKeyOption>(options);

            if (!string.IsNullOrWhiteSpace(currentKey) && options.Any(o => string.Equals(o.Key, currentKey, StringComparison.OrdinalIgnoreCase)))
            {
                ImageBase64SourceOutputKey = currentKey;
            }
            else if (options.Count > 0 && string.IsNullOrWhiteSpace(ImageBase64SourceOutputKey))
            {
                ImageBase64SourceOutputKey = options[0].Key;
            }
        }

        public void RefreshCroppedFolderKeyOptions()
        {
            var currentKey = CroppedFolderSourceOutputKey;
            var options = GetOutputKeysForNode(CroppedFolderSourceNodeId);
            CroppedFolderKeyOptions = new ObservableCollection<WorkflowOutputKeyOption>(options);

            if (!string.IsNullOrWhiteSpace(currentKey) && options.Any(o => string.Equals(o.Key, currentKey, StringComparison.OrdinalIgnoreCase)))
            {
                CroppedFolderSourceOutputKey = currentKey;
            }
            else if (options.Count > 0 && string.IsNullOrWhiteSpace(CroppedFolderSourceOutputKey))
            {
                CroppedFolderSourceOutputKey = options[0].Key;
            }
        }

        [RelayCommand]
        private async Task ApplyUrlToNode()
        {
            InputMode = ImageInputMode.Url;
            
            // Nếu ImageUrl là URL online thì:
            // 1. Sync URL vào node trước để trigger UpdatePreviewAsync và hiển thị loading
            // 2. Download về temp trong background
            // 3. Cập nhật ImageUrl thành temp path sau khi download xong
            bool isUrl = !string.IsNullOrWhiteSpace(ImageUrl) && 
                         string.IsNullOrWhiteSpace(ImageUrlSourceNodeId) && // Không phải từ node khác
                         (ImageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                          ImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
            
            if (isUrl)
            {
                // Sync URL vào node trước để trigger preview và hiển thị loading
                SyncToNode(previewRefresh: true);
                
                // Download về temp trong background, sau đó cập nhật lại
                await DownloadImageFromUrlAsync(ImageUrl);
                
                // Sau khi download xong, cập nhật lại ImageUrl thành temp path và trigger preview
                SyncToNode(previewRefresh: true);
            }
            else
            {
                SyncToNode(previewRefresh: true);
            }
        }
        
        /// <summary>
        /// Tải ảnh từ URL về temp folder với chất lượng cao nhất và cập nhật ImageUrl thành path temp.
        /// </summary>
        private async Task DownloadImageFromUrlAsync(string url)
        {
            if (_isDownloadingUrl) return;
            
            try
            {
                _isDownloadingUrl = true;
                
                // Tạo temp folder nếu chưa có
                var tempDir = Path.Combine(Path.GetTempPath(), "FlowMy_ImageProcessing");
                Directory.CreateDirectory(tempDir);
                
                // Tạo tên file từ URL (lấy extension nếu có, mặc định .png)
                var uri = new Uri(url);
                var fileName = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains('.'))
                {
                    // Nếu không có tên file hoặc không có extension, dùng hash của URL
                    fileName = $"img_{Math.Abs(url.GetHashCode()):X}.png";
                }
                else
                {
                    // Đảm bảo có extension hợp lệ
                    var ext = Path.GetExtension(fileName).ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(ext) || 
                        !new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" }.Contains(ext))
                    {
                        fileName = Path.GetFileNameWithoutExtension(fileName) + ".png";
                    }
                }
                
                var tempPath = Path.Combine(tempDir, fileName);
                
                // Download ảnh với chất lượng cao nhất
                var imageBytes = await _httpClient.GetByteArrayAsync(url).ConfigureAwait(false);
                
                // Lưu vào temp file
                await File.WriteAllBytesAsync(tempPath, imageBytes).ConfigureAwait(false);
                
                // Cập nhật ImageUrl thành path temp
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    ImageUrl = tempPath;
                });
            }
            catch (Exception ex)
            {
                // Nếu download lỗi, giữ nguyên URL để user biết
                System.Diagnostics.Debug.WriteLine($"[ImageProcessingNodeDialog] Failed to download image from URL: {ex.Message}");
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Không thể tải ảnh từ URL: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            finally
            {
                _isDownloadingUrl = false;
            }
        }
        

        [RelayCommand]
        private void ApplyBase64ToNode()
        {
            InputMode = ImageInputMode.Base64;
            SyncToNode(previewRefresh: true);
        }

        private void SyncToNode(bool previewRefresh)
        {
            if (_imageNode.InputMode != InputMode) _imageNode.InputMode = InputMode;

            _imageNode.ImageUrl = ImageUrl ?? string.Empty;
            _imageNode.ImageUrlSourceNodeId = string.IsNullOrWhiteSpace(ImageUrlSourceNodeId) ? null : ImageUrlSourceNodeId;
            _imageNode.ImageUrlSourceOutputKey = string.IsNullOrWhiteSpace(ImageUrlSourceOutputKey) ? null : ImageUrlSourceOutputKey;

            _imageNode.ImageBase64 = ImageBase64 ?? string.Empty;
            _imageNode.ImageBase64SourceNodeId = string.IsNullOrWhiteSpace(ImageBase64SourceNodeId) ? null : ImageBase64SourceNodeId;
            _imageNode.ImageBase64SourceOutputKey = string.IsNullOrWhiteSpace(ImageBase64SourceOutputKey) ? null : ImageBase64SourceOutputKey;

            _imageNode.PreferGpu = PreferGpu;
            _imageNode.FfmpegFilter = FfmpegFilter ?? string.Empty;

            _imageNode.CroppedFolderPath = CroppedFolderPath ?? string.Empty;
            _imageNode.CroppedFolderSourceNodeId = string.IsNullOrWhiteSpace(CroppedFolderSourceNodeId) ? null : CroppedFolderSourceNodeId;
            _imageNode.CroppedFolderSourceOutputKey = string.IsNullOrWhiteSpace(CroppedFolderSourceOutputKey) ? null : CroppedFolderSourceOutputKey;

            _imageNode.RenderNodeId = string.IsNullOrWhiteSpace(RenderNodeId) ? null : RenderNodeId;
            _imageNode.RenderNodeOutputKey = string.IsNullOrWhiteSpace(RenderNodeOutputKey) ? null : RenderNodeOutputKey;

            _imageNode.RenderCodeIdKeys = RenderCodeIdKeys;
            _imageNode.RenderImageIdKeys = RenderImageIdKeys;
            _imageNode.RenderImageLinkKeys = RenderImageLinkKeys;

            _imageNode.ReturnIdNodeId = string.IsNullOrWhiteSpace(ReturnIdNodeId) ? null : ReturnIdNodeId;
            _imageNode.ReturnIdOutputKey = string.IsNullOrWhiteSpace(ReturnIdOutputKey) ? null : ReturnIdOutputKey;

            _imageNode.ReturnCodeIdKeys = ReturnCodeIdKeys;
            _imageNode.ReturnImageIdKeys = ReturnImageIdKeys;
            _imageNode.ReturnImageLinkKeys = ReturnImageLinkKeys;

            if (previewRefresh)
            {
                // Trigger NodeControl refresh even if string stays the same.
                _imageNode.RaisePropertyChanged(nameof(ImageProcessingNode.InputMode));
                _imageNode.RaisePropertyChanged(nameof(ImageProcessingNode.ImageUrl));
                _imageNode.RaisePropertyChanged(nameof(ImageProcessingNode.ImageBase64));
            }

            // Layer AI code
            _imageNode.LayerAiHtmlCode = LayerAiHtmlCode ?? string.Empty;
            _imageNode.LayerAiCssCode = LayerAiCssCode ?? string.Empty;
            _imageNode.LayerAiJsCode = LayerAiJsCode ?? string.Empty;
            _imageNode.LayerAiParamsCode = LayerAiParamsCode ?? string.Empty;
            SyncLayerAiInputMappingsToNode();

            _host.RequestSyncDataPanels(immediate: true);
        }

        protected override async void OnSaveTitle()
        {
            _imageNode.NotifyTitleChanged();
            
            // Nếu ImageUrl là URL online thì:
            // 1. Sync URL vào node trước để trigger UpdatePreviewAsync và hiển thị loading
            // 2. Download về temp trong background
            // 3. Cập nhật ImageUrl thành temp path sau khi download xong
            bool isUrl = !string.IsNullOrWhiteSpace(ImageUrl) && 
                         string.IsNullOrWhiteSpace(ImageUrlSourceNodeId) && // Không phải từ node khác
                         (ImageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                          ImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
            
            if (isUrl)
            {
                // Sync URL vào node trước để trigger preview và hiển thị loading
                SyncToNode(previewRefresh: true);
                
                // Download về temp trong background, sau đó cập nhật lại
                _ = Task.Run(async () =>
                {
                    await DownloadImageFromUrlAsync(ImageUrl);
                    
                    // Sau khi download xong, cập nhật lại ImageUrl thành temp path và trigger preview
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        SyncToNode(previewRefresh: true);
                    });
                });
            }
            else
            {
                SyncToNode(previewRefresh: false);
            }
        }

        // ═══════ Layer AI Input Mappings ═══════

        private void LoadLayerAiInputMappings()
        {
            LayerAiInputMappingsList.Clear();
            var mappings = _imageNode.LayerAiInputMappings ?? new List<CodeInputMapping>();
            if (mappings.Count == 0) mappings.Add(new CodeInputMapping());

            foreach (var m in mappings)
            {
                var item = new CodeInputMappingItemViewModel
                {
                    SourceNodeId = m.SourceNodeId,
                    SourceOutputKey = m.SourceOutputKey,
                    InputKeyOverride = m.InputKeyOverride ?? string.Empty,
                    AutoRefreshEnabled = m.AutoRefreshEnabled,
                    AutoRefreshInterval = m.AutoRefreshInterval,
                    AutoRefreshUnit = m.AutoRefreshUnit ?? "ms"
                };
                RefreshLayerAiOutputKeyOptionsFor(item);
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(CodeInputMappingItemViewModel.SourceNodeId))
                        RefreshLayerAiOutputKeyOptionsFor((CodeInputMappingItemViewModel)s!);
                };
                LayerAiInputMappingsList.Add(item);
            }

            // Refresh node options for Layer AI tab
            RefreshLayerAiNodeOptions();
        }

        public void RefreshLayerAiNodeOptions()
        {
            LayerAiNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null) return;
            foreach (var n in _host.ViewModel.Nodes)
            {
                if (ReferenceEquals(n, _imageNode)) continue;
                EnsureNodeDynamicOutputsSynced(n);
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                LayerAiNodeOptions.Add(CreateDataSourceOption(n));
            }
        }

        private void RefreshLayerAiOutputKeyOptionsFor(CodeInputMappingItemViewModel item)
        {
            item.AvailableOutputKeyOptions.Clear();
            var options = GetOutputKeysForNode(item.SourceNodeId);
            foreach (var o in options)
                item.AvailableOutputKeyOptions.Add(o);
        }

        private void SyncLayerAiInputMappingsToNode()
        {
            _imageNode.LayerAiInputMappings = LayerAiInputMappingsList.Select(x => new CodeInputMapping
            {
                SourceNodeId = x.SourceNodeId,
                SourceOutputKey = x.SourceOutputKey,
                InputKeyOverride = x.InputKeyOverride,
                AutoRefreshEnabled = x.AutoRefreshEnabled,
                AutoRefreshInterval = x.AutoRefreshInterval,
                AutoRefreshUnit = x.AutoRefreshUnit
            }).ToList();
        }

        [RelayCommand]
        private void AddLayerAiInputMapping()
        {
            var item = new CodeInputMappingItemViewModel();
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CodeInputMappingItemViewModel.SourceNodeId))
                    RefreshLayerAiOutputKeyOptionsFor((CodeInputMappingItemViewModel)s!);
            };
            LayerAiInputMappingsList.Add(item);
        }

        [RelayCommand]
        private void RemoveLayerAiInputMapping(CodeInputMappingItemViewModel? item)
        {
            if (item != null && LayerAiInputMappingsList.Contains(item) && LayerAiInputMappingsList.Count > 1)
            {
                LayerAiInputMappingsList.Remove(item);
            }
        }

        [RelayCommand]
        private void IncreaseLayerAiCodeFontSize()
        {
            if (LayerAiCodeFontSize < 24) LayerAiCodeFontSize++;
        }

        [RelayCommand]
        private void DecreaseLayerAiCodeFontSize()
        {
            if (LayerAiCodeFontSize > 9) LayerAiCodeFontSize--;
        }
    }
}


