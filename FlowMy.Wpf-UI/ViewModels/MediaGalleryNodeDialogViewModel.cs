using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace FlowMy.ViewModels
{
    public partial class MediaGalleryNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly MediaGalleryNode _galleryNode;

        [ObservableProperty] private string _frameDisplayWidthText = "120";
        [ObservableProperty] private string _frameDisplayHeightText = "90";
        [ObservableProperty] private string _titleKeyTemplate = "title";
        [ObservableProperty] private string _imageUrlKeyTemplate = "imageUrl";
        [ObservableProperty] private string _videoUrlKeyTemplate = "videoUrl";
        [ObservableProperty] private string _groupArrayKey = "workflows";
        [ObservableProperty] private string _groupTitleKey = "workflowId";
        [ObservableProperty] private string _groupItemsKey = "videos";
        [ObservableProperty] private string _folderSaveImages = "";
        [ObservableProperty] private string? _folderSourceNodeId;
        [ObservableProperty] private string? _folderSourceOutputKey;
        [ObservableProperty] private string _folderSaveVideos = "";
        [ObservableProperty] private string? _folderSourceNodeIdVideo;
        [ObservableProperty] private string? _folderSourceOutputKeyVideo;
        [ObservableProperty] private string? _jsonSourceNodeId;
        [ObservableProperty] private string? _jsonSourceOutputKey;
        [ObservableProperty] private ItemClickPreviewMode _itemClickPreviewMode = ItemClickPreviewMode.Image;
        [ObservableProperty] private GalleryDisplayMode _displayMode = GalleryDisplayMode.Grid;
        /// <summary>
        /// Cho phép nút Play trong dialog gọi lại logic node nguồn (single play).
        /// Checked = chạy lại node nguồn; Unchecked = chỉ lấy output hiện tại, không chạy lại.
        /// </summary>
        [ObservableProperty] private bool _canReexecuteSourceNode = false;

        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();

        public ObservableCollection<GalleryDisplayModeOption> GalleryDisplayModeOptions { get; } = new()
        {
            new GalleryDisplayModeOption { Mode = GalleryDisplayMode.Grid, DisplayName = "Ảnh/Video theo lưới" },
            new GalleryDisplayModeOption { Mode = GalleryDisplayMode.Grouped, DisplayName = "Ảnh/Video theo nhóm" }
        };

        public ObservableCollection<ItemClickPreviewOption> ItemClickPreviewOptions { get; } = new()
        {
            new ItemClickPreviewOption { Mode = ItemClickPreviewMode.Image, DisplayName = "Xem ảnh" },
            new ItemClickPreviewOption { Mode = ItemClickPreviewMode.Video, DisplayName = "Xem video" }
        };

        public MediaGalleryNodeDialogViewModel(MediaGalleryNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _galleryNode = node ?? throw new ArgumentNullException(nameof(node));
            FrameDisplayWidthText = _galleryNode.FrameDisplayWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);
            FrameDisplayHeightText = _galleryNode.FrameDisplayHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
            TitleKeyTemplate = _galleryNode.TitleKeyTemplate ?? "";
            ImageUrlKeyTemplate = _galleryNode.ImageUrlKeyTemplate ?? "";
            VideoUrlKeyTemplate = _galleryNode.VideoUrlKeyTemplate ?? "";
            GroupArrayKey = _galleryNode.GroupArrayKey ?? "";
            GroupTitleKey = _galleryNode.GroupTitleKey ?? "";
            GroupItemsKey = _galleryNode.GroupItemsKey ?? "";
            FolderSaveImages = _galleryNode.FolderSaveImages ?? "";
            FolderSourceNodeId = _galleryNode.FolderSourceNodeId;
            FolderSourceOutputKey = _galleryNode.FolderSourceOutputKey;
            FolderSaveVideos = _galleryNode.FolderSaveVideos ?? "";
            FolderSourceNodeIdVideo = _galleryNode.FolderSourceNodeIdVideo;
            FolderSourceOutputKeyVideo = _galleryNode.FolderSourceOutputKeyVideo;
            JsonSourceNodeId = _galleryNode.JsonSourceNodeId;
            JsonSourceOutputKey = _galleryNode.JsonSourceOutputKey;
            ItemClickPreviewMode = _galleryNode.ItemClickPreviewMode;
            DisplayMode = _galleryNode.DisplayMode;
            CanReexecuteSourceNode = _galleryNode.CanReexecuteSourceNode;

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) => OnNodePropertyChanged(e.PropertyName ?? "");
            }
        }

        protected override void OnNodePropertyChanged(string propertyName)
        {
            if (propertyName == nameof(MediaGalleryNode.FrameDisplayWidth)) FrameDisplayWidthText = _galleryNode.FrameDisplayWidth.ToString(System.Globalization.CultureInfo.InvariantCulture);
            else if (propertyName == nameof(MediaGalleryNode.FrameDisplayHeight)) FrameDisplayHeightText = _galleryNode.FrameDisplayHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
            else if (propertyName == nameof(MediaGalleryNode.TitleKeyTemplate)) TitleKeyTemplate = _galleryNode.TitleKeyTemplate ?? "";
            else if (propertyName == nameof(MediaGalleryNode.ImageUrlKeyTemplate)) ImageUrlKeyTemplate = _galleryNode.ImageUrlKeyTemplate ?? "";
            else if (propertyName == nameof(MediaGalleryNode.VideoUrlKeyTemplate)) VideoUrlKeyTemplate = _galleryNode.VideoUrlKeyTemplate ?? "";
            else if (propertyName == nameof(MediaGalleryNode.GroupArrayKey)) GroupArrayKey = _galleryNode.GroupArrayKey ?? "";
            else if (propertyName == nameof(MediaGalleryNode.GroupTitleKey)) GroupTitleKey = _galleryNode.GroupTitleKey ?? "";
            else if (propertyName == nameof(MediaGalleryNode.GroupItemsKey)) GroupItemsKey = _galleryNode.GroupItemsKey ?? "";
            else if (propertyName == nameof(MediaGalleryNode.FolderSaveImages)) FolderSaveImages = _galleryNode.FolderSaveImages ?? "";
            else if (propertyName == nameof(MediaGalleryNode.FolderSourceNodeId)) FolderSourceNodeId = _galleryNode.FolderSourceNodeId;
            else if (propertyName == nameof(MediaGalleryNode.FolderSourceOutputKey)) FolderSourceOutputKey = _galleryNode.FolderSourceOutputKey;
            else if (propertyName == nameof(MediaGalleryNode.FolderSaveVideos)) FolderSaveVideos = _galleryNode.FolderSaveVideos ?? "";
            else if (propertyName == nameof(MediaGalleryNode.FolderSourceNodeIdVideo)) FolderSourceNodeIdVideo = _galleryNode.FolderSourceNodeIdVideo;
            else if (propertyName == nameof(MediaGalleryNode.FolderSourceOutputKeyVideo)) FolderSourceOutputKeyVideo = _galleryNode.FolderSourceOutputKeyVideo;
            else if (propertyName == nameof(MediaGalleryNode.JsonSourceNodeId)) JsonSourceNodeId = _galleryNode.JsonSourceNodeId;
            else if (propertyName == nameof(MediaGalleryNode.JsonSourceOutputKey)) JsonSourceOutputKey = _galleryNode.JsonSourceOutputKey;
            else if (propertyName == nameof(MediaGalleryNode.ItemClickPreviewMode)) ItemClickPreviewMode = _galleryNode.ItemClickPreviewMode;
            else if (propertyName == nameof(MediaGalleryNode.DisplayMode)) DisplayMode = _galleryNode.DisplayMode;
            else if (propertyName == nameof(MediaGalleryNode.CanReexecuteSourceNode)) CanReexecuteSourceNode = _galleryNode.CanReexecuteSourceNode;
        }

        protected override string GetDefaultTitle() => "Gallery ảnh/video";

        public void RefreshAvailableNodes()
        {
            AvailableNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null) return;
            foreach (var n in _host.ViewModel.Nodes)
            {
                if (ReferenceEquals(n, _galleryNode)) continue;
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                AvailableNodeOptions.Add(CreateDataSourceOption(n));
            }
        }

        /// <summary>
        /// Nút Play cho MediaGallery: tùy theo CanReexecuteSourceNode mà
        /// - true: giữ nguyên behavior cũ (chạy lại logic node Gallery + node nguồn)
        /// - false: KHÔNG chạy lại logic node nguồn, chỉ lấy Output hiện tại từ node nguồn theo JsonSourceNodeId/JsonSourceOutputKey.
        /// </summary>
        [RelayCommand]
        private void RunSingleNodeWithOption()
        {
            // Checked: chạy lại logic node Gallery + upstream như cũ.
            if (CanReexecuteSourceNode)
            {
                _host.RequestRunSingleNode(_galleryNode);
                return;
            }

            // Unchecked: chỉ lấy Output hiện tại từ node nguồn, KHÔNG chạy lại node nguồn.
            if (string.IsNullOrWhiteSpace(JsonSourceNodeId))
                return;

            var srcNode = _host.ViewModel?.Nodes?
                .FirstOrDefault(n => string.Equals(n.Id, JsonSourceNodeId, StringComparison.OrdinalIgnoreCase));
            if (srcNode == null)
                return;

            var keyToUse = string.IsNullOrWhiteSpace(JsonSourceOutputKey) ? null : JsonSourceOutputKey.Trim();
            var jsonString = GetJsonByKey(srcNode, keyToUse);
            if (string.IsNullOrWhiteSpace(jsonString) || jsonString == "—")
                return;

            _galleryNode.LastJson = jsonString;

            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            dispatcher.Invoke(() =>
            {
                MediaGalleryJsonHelper.ParseAndFill(jsonString, _galleryNode);
            });
        }

        /// <summary>
        /// Lấy chuỗi JSON hiện tại từ node nguồn theo đúng key đã chọn (không chạy lại logic node).
        /// Logic giống MediaGalleryNodeExecutor.GetJsonByKey nhưng dùng trực tiếp cho dialog.
        /// </summary>
        private static string GetJsonByKey(WorkflowNode fromNode, string? key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                var value = NodeDataPanelService.ResolveDynamicValueByKey(fromNode, key);
                if (!string.IsNullOrWhiteSpace(value) && value != "—")
                    return value;
            }
            return string.Empty;
        }

        protected override void OnSaveTitle()
        {
            _galleryNode.NotifyTitleChanged();
            if (double.TryParse(FrameDisplayWidthText?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var w) && w >= 60)
                _galleryNode.FrameDisplayWidth = w;
            if (double.TryParse(FrameDisplayHeightText?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var h) && h >= 40)
                _galleryNode.FrameDisplayHeight = h;
            _galleryNode.TitleKeyTemplate = TitleKeyTemplate ?? "";
            _galleryNode.ImageUrlKeyTemplate = ImageUrlKeyTemplate ?? "";
            _galleryNode.VideoUrlKeyTemplate = VideoUrlKeyTemplate ?? "";
            _galleryNode.GroupArrayKey = GroupArrayKey ?? "";
            _galleryNode.GroupTitleKey = GroupTitleKey ?? "";
            _galleryNode.GroupItemsKey = GroupItemsKey ?? "";
            _galleryNode.FolderSaveImages = FolderSaveImages ?? "";
            _galleryNode.FolderSourceNodeId = string.IsNullOrWhiteSpace(FolderSourceNodeId) ? null : FolderSourceNodeId;
            _galleryNode.FolderSourceOutputKey = string.IsNullOrWhiteSpace(FolderSourceOutputKey) ? null : FolderSourceOutputKey;
            _galleryNode.FolderSaveVideos = FolderSaveVideos ?? "";
            _galleryNode.FolderSourceNodeIdVideo = string.IsNullOrWhiteSpace(FolderSourceNodeIdVideo) ? null : FolderSourceNodeIdVideo;
            _galleryNode.FolderSourceOutputKeyVideo = string.IsNullOrWhiteSpace(FolderSourceOutputKeyVideo) ? null : FolderSourceOutputKeyVideo;
            _galleryNode.JsonSourceNodeId = string.IsNullOrWhiteSpace(JsonSourceNodeId) ? null : JsonSourceNodeId;
            _galleryNode.JsonSourceOutputKey = string.IsNullOrWhiteSpace(JsonSourceOutputKey) ? null : JsonSourceOutputKey;
            _galleryNode.ItemClickPreviewMode = ItemClickPreviewMode;
            _galleryNode.DisplayMode = DisplayMode;
            _galleryNode.CanReexecuteSourceNode = CanReexecuteSourceNode;
            _host.RequestSyncDataPanels(immediate: true);
        }
    }
}
