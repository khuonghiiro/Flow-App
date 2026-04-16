using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using FlowMy.Models;

namespace FlowMy.Models.Nodes
{
    /// <summary>Khi nhấn vào item: mở xem ảnh hay video.</summary>
    public enum ItemClickPreviewMode
    {
        Image = 0,
        Video = 1
    }

    /// <summary>Option cho combobox "Khi nhấn item: Xem ảnh / Xem video".</summary>
    public sealed class ItemClickPreviewOption
    {
        public ItemClickPreviewMode Mode { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>Hiển thị gallery: theo lưới (flat list) hay theo nhóm (categories/items).</summary>
    public enum GalleryDisplayMode
    {
        Grid = 0,
        Grouped = 1
    }

    /// <summary>Option cho combobox "Ảnh/Video theo lưới" | "Ảnh/Video theo nhóm".</summary>
    public sealed class GalleryDisplayModeOption
    {
        public GalleryDisplayMode Mode { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>Một nhóm category (vd: categories[].categoryName + items).</summary>
    public sealed class MediaGalleryGroup
    {
        public string Title { get; set; } = string.Empty;
        public ObservableCollection<MediaGalleryItem> Items { get; } = new();
    }

    /// <summary>
    /// Một item trong gallery (ảnh/video) - dùng để hiển thị và chọn tải.
    /// </summary>
    public sealed class MediaGalleryItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Title { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? VideoUrl { get; set; }
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public object? RawItem { get; set; }
    }

    /// <summary>
    /// Node hiển thị gallery ảnh/video, co dãn chiều ngang (kéo từ góc), có checkbox chọn, nút tải ảnh/video, xem video, phóng to ảnh.
    /// Cấu hình: size khung, tiêu đề (key JSON), url ảnh (key), url video (key), folder lưu ảnh; có thể dùng combobox node+key để lấy folder từ node khác.
    /// </summary>
    public sealed class MediaGalleryNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;
        private double _width = 320;
        private double _height = 280;
        private double _frameDisplayWidth = 120;
        private double _frameDisplayHeight = 90;
        private string _titleKeyTemplate = "title";
        private string _imageUrlKeyTemplate = "imageUrl";
        private string _videoUrlKeyTemplate = "videoUrl";
        private string _groupArrayKey = "workflows";
        private string _groupTitleKey = "workflowId";
        private string _groupItemsKey = "videos";
        private string _folderSaveImages = "";
        private string? _folderSourceNodeId;
        private string? _folderSourceOutputKey;
        private string _folderSaveVideos = "";
        private string? _folderSourceNodeIdVideo;
        private string? _folderSourceOutputKeyVideo;
        private string? _jsonSourceNodeId;
        private string? _jsonSourceOutputKey;
        private ItemClickPreviewMode _itemClickPreviewMode = ItemClickPreviewMode.Image;
        private GalleryDisplayMode _displayMode = GalleryDisplayMode.Grid;
        private string? _lastJson;
        private bool _canReexecuteSourceNode = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>JSON lần parse gần nhất (để re-parse khi đổi DisplayMode).</summary>
        public string? LastJson { get => _lastJson; set => _lastJson = value; }

        /// <summary>Hiển thị theo lưới (data flat) hay theo nhóm (categories/items).</summary>
        public GalleryDisplayMode DisplayMode
        {
            get => _displayMode;
            set { if (_displayMode != value) { _displayMode = value; OnPropertyChanged(); } }
        }

        /// <summary>Nhóm gallery (khi DisplayMode = Grouped).</summary>
        public ObservableCollection<MediaGalleryGroup> GalleryGroups { get; } = new();

        /// <summary>Chiều rộng node (resize từ góc).</summary>
        public double Width
        {
            get => _width;
            set
            {
                if (Math.Abs(_width - value) > 0.01 && value >= 200)
                {
                    _width = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Chiều cao node.</summary>
        public double Height
        {
            get => _height;
            set
            {
                if (Math.Abs(_height - value) > 0.01 && value >= 180)
                {
                    _height = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Chiều rộng mỗi khung hiển thị ảnh/video trong node.</summary>
        public double FrameDisplayWidth
        {
            get => _frameDisplayWidth;
            set
            {
                if (Math.Abs(_frameDisplayWidth - value) > 0.01 && value >= 60)
                {
                    _frameDisplayWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Chiều cao mỗi khung hiển thị ảnh/video trong node.</summary>
        public double FrameDisplayHeight
        {
            get => _frameDisplayHeight;
            set
            {
                if (Math.Abs(_frameDisplayHeight - value) > 0.01 && value >= 40)
                {
                    _frameDisplayHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Template key cho tiêu đề (vd: title).</summary>
        public string TitleKeyTemplate
        {
            get => _titleKeyTemplate;
            set { _titleKeyTemplate = value ?? ""; OnPropertyChanged(); }
        }

        /// <summary>Template key cho URL ảnh (vd: imageUrl).</summary>
        public string ImageUrlKeyTemplate
        {
            get => _imageUrlKeyTemplate;
            set { _imageUrlKeyTemplate = value ?? ""; OnPropertyChanged(); }
        }

        /// <summary>Template key cho URL video (vd: videoUrl).</summary>
        public string VideoUrlKeyTemplate
        {
            get => _videoUrlKeyTemplate;
            set { _videoUrlKeyTemplate = value ?? ""; OnPropertyChanged(); }
        }

        /// <summary>Key mảng nhóm ở root (vd: workflows). Chỉ dùng khi DisplayMode = Grouped.</summary>
        public string GroupArrayKey
        {
            get => _groupArrayKey;
            set { _groupArrayKey = value ?? ""; OnPropertyChanged(); }
        }

        /// <summary>Key tiêu đề trong mỗi nhóm (vd: workflowId, toolName). Chỉ dùng khi DisplayMode = Grouped.</summary>
        public string GroupTitleKey
        {
            get => _groupTitleKey;
            set { _groupTitleKey = value ?? ""; OnPropertyChanged(); }
        }

        /// <summary>Key mảng item trong mỗi nhóm (vd: videos). Chỉ dùng khi DisplayMode = Grouped.</summary>
        public string GroupItemsKey
        {
            get => _groupItemsKey;
            set { _groupItemsKey = value ?? ""; OnPropertyChanged(); }
        }

        /// <summary>Folder mặc định lưu ảnh.</summary>
        public string FolderSaveImages
        {
            get => _folderSaveImages;
            set { _folderSaveImages = value ?? ""; OnPropertyChanged(); }
        }

        /// <summary>Node nguồn để lấy folder thay thế (nếu chọn).</summary>
        public string? FolderSourceNodeId
        {
            get => _folderSourceNodeId;
            set { _folderSourceNodeId = value; OnPropertyChanged(); }
        }

        /// <summary>Key output của node nguồn chứa đường dẫn folder ảnh.</summary>
        public string? FolderSourceOutputKey
        {
            get => _folderSourceOutputKey;
            set { _folderSourceOutputKey = value; OnPropertyChanged(); }
        }

        /// <summary>Folder mặc định lưu video.</summary>
        public string FolderSaveVideos
        {
            get => _folderSaveVideos;
            set { _folderSaveVideos = value ?? ""; OnPropertyChanged(); }
        }

        /// <summary>Node nguồn để lấy folder video (khi textbox folder video để trống).</summary>
        public string? FolderSourceNodeIdVideo
        {
            get => _folderSourceNodeIdVideo;
            set { _folderSourceNodeIdVideo = value; OnPropertyChanged(); }
        }

        /// <summary>Key output của node nguồn chứa đường dẫn folder video.</summary>
        public string? FolderSourceOutputKeyVideo
        {
            get => _folderSourceOutputKeyVideo;
            set { _folderSourceOutputKeyVideo = value; OnPropertyChanged(); }
        }

        /// <summary>Node nguồn chứa JSON (đã chọn trong dialog).</summary>
        public string? JsonSourceNodeId
        {
            get => _jsonSourceNodeId;
            set { _jsonSourceNodeId = value; OnPropertyChanged(); }
        }

        /// <summary>Key output của node nguồn chứa chuỗi JSON (đã chọn trong dialog).</summary>
        public string? JsonSourceOutputKey
        {
            get => _jsonSourceOutputKey;
            set { _jsonSourceOutputKey = value; OnPropertyChanged(); }
        }

        /// <summary>Khi nhấn vào item: Xem ảnh hay Xem video.</summary>
        public ItemClickPreviewMode ItemClickPreviewMode
        {
            get => _itemClickPreviewMode;
            set { if (_itemClickPreviewMode != value) { _itemClickPreviewMode = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Cho phép dialog Gallery khi ấn nút Play chạy lại logic node nguồn hay không.
        /// True (mặc định): hành vi cũ – gọi lại logic node nguồn.
        /// False: chỉ lấy output JSON hiện tại từ node nguồn, không thực thi lại node đó.
        /// </summary>
        public bool CanReexecuteSourceNode
        {
            get => _canReexecuteSourceNode;
            set { if (_canReexecuteSourceNode != value) { _canReexecuteSourceNode = value; OnPropertyChanged(); } }
        }

        public TitleDisplayMode TitleDisplayMode
        {
            get => _titleDisplayMode;
            set { if (_titleDisplayMode != value) { _titleDisplayMode = value; OnPropertyChanged(); } }
        }

        public TitleColorMode TitleColorMode
        {
            get => _titleColorMode;
            set { if (_titleColorMode != value) { _titleColorMode = value; OnPropertyChanged(); } }
        }

        public string? TitleColorKey
        {
            get => _titleColorKey;
            set { if (_titleColorKey != value) { _titleColorKey = value; OnPropertyChanged(); } }
        }

        /// <summary>Reference đến TextBlock hiển thị title trên canvas.</summary>
        public TextBlock? TitleTextBlockUI { get; set; }

        /// <summary>Danh sách item gallery (runtime: từ data input).</summary>
        public ObservableCollection<MediaGalleryItem> GalleryItems { get; } = new();

        public MediaGalleryNode()
        {
            Type = NodeType.MediaGallery;
            Title = "Gallery ảnh/video";
            ColorKey = "CharcoalMist";
            Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"           // Port IN: dùng màu Info theo guideline
            });
            Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"   // Port OUT: dùng màu SunsetOrange theo guideline
            });
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));
    }
}
