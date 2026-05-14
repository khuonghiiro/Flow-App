using FlowMy.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes
{
    public enum ImageInputMode
    {
        Url = 0,
        Base64 = 1
    }

    /// <summary>Kiểu crop: tự do (polygon) hoặc theo tỉ lệ chữ nhật chuẩn.</summary>
    public enum ImageCropMode
    {
        FreePolygon = 0,
        RectFromPolygon = 1
    }

    /// <summary>Vùng crop trên ảnh, lưu theo toạ độ pixel ảnh gốc.</summary>
        public sealed class ImageCropRegion : INotifyPropertyChanged
    {
        private bool _isVisible = true;
        private bool _isOutlineOnly = false;
        private string _colorHex = "#FFD700"; // Default: Gold
        private System.Windows.Media.ImageSource? _thumbnail;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>Số thứ tự crop (không thay đổi khi xoá crop khác).</summary>
        public int Order { get; set; }

        /// <summary>Danh sách điểm polygon theo toạ độ ảnh gốc (pixel).</summary>
        public ObservableCollection<Point> Points { get; } = new();

        /// <summary>Bounding box tối thiểu (theo điểm cao nhất/thấp nhất, trái/phải xa nhất).</summary>
        public Rect BoundingBox { get; set; }

        /// <summary>Chiều rộng/chiều cao chuẩn sau khi map sang tỉ lệ gốc (vd 1920x1080 * k).</summary>
        public double TargetWidth { get; set; }
        public double TargetHeight { get; set; }

        /// <summary>Ẩn/hiện vùng crop trên UI + menu phải.</summary>
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
                }
            }
        }

        /// <summary>Màu viền polygon dạng hex (ví dụ "#FFD700"). Dùng để lưu/hiển thị màu nhận dạng của vùng crop.</summary>
        public string ColorHex
        {
            get => _colorHex;
            set
            {
                if (_colorHex != value)
                {
                    _colorHex = string.IsNullOrWhiteSpace(value) ? "#FFD700" : value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorHex)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StrokeBrush)));
                }
            }
        }

        /// <summary>Brush tương ứng với ColorHex, dùng để bind trực tiếp vào UI (thumbnail border, ...).</summary>
        public System.Windows.Media.SolidColorBrush StrokeBrush
        {
            get
            {
                try
                {
                    var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_colorHex);
                    return new System.Windows.Media.SolidColorBrush(c);
                }
                catch
                {
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gold);
                }
            }
        }

        /// <summary>Ẩn nền (fill) polygon và chuyển viền sang nét đứt.</summary>
        public bool IsOutlineOnly
        {
            get => _isOutlineOnly;
            set
            {
                if (_isOutlineOnly != value)
                {
                    _isOutlineOnly = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOutlineOnly)));
                }
            }
        }

        /// <summary>Đường dẫn file ảnh đã cắt (nếu đã lưu).</summary>
        public string? SavedPath { get; set; }
        
        /// <summary>Tên crop theo format Image_{Order}_{DateTime} (ví dụ: Image_1_20260226104010).</summary>
        public string CropName { get; set; } = string.Empty;

        /// <summary>Thumbnail xem nhanh (hiển thị ở menu phải).</summary>
        public System.Windows.Media.ImageSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (!ReferenceEquals(_thumbnail, value))
                {
                    _thumbnail = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
                }
            }
        }
        
        /// <summary>Danh sách ảnh render tương ứng với crop (preview kết quả từ node render ảnh, có thể nhiều ảnh).</summary>
        public ObservableCollection<System.Windows.Media.ImageSource> RenderedImages { get; } = new();

        /// <summary>
        /// ExecutionId của lần chạy workflow gần nhất mà crop này được xử lý.
        /// Dùng để map ảnh render về đúng crop (thay vì map theo thứ tự).
        /// </summary>
        public string LastExecutionId { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Node xử lý ảnh: nhận ảnh từ URL/file hoặc base64 (có thể map từ node+key),
    /// hiển thị preview trong node với zoom/pan, crop tự do, và (optional) dùng ffmpeg để render/xử lý khi execute.
    /// </summary>
    public sealed class ImageProcessingNode : WorkflowNode, INotifyPropertyChanged
    {
        private TitleDisplayMode _titleDisplayMode = TitleDisplayMode.Always;
        private TitleColorMode _titleColorMode = TitleColorMode.NodeColor;
        private string? _titleColorKey;

        private double _width = 360;
        private double _height = 260;

        private ImageInputMode _inputMode = ImageInputMode.Url;

        private string _imageUrl = string.Empty;
        private string? _imageUrlSourceNodeId;
        private string? _imageUrlSourceOutputKey;

        private string _imageBase64 = string.Empty;
        private string? _imageBase64SourceNodeId;
        private string? _imageBase64SourceOutputKey;

        private bool _preferGpu = true;
        private string _ffmpegFilter = string.Empty;

        // Cấu hình crop
        private ImageCropMode _cropMode = ImageCropMode.FreePolygon;

        /// <summary>Danh sách vùng crop hiện có trên ảnh.</summary>
        public ObservableCollection<ImageCropRegion> Crops { get; } = new();

        // Folder lưu ảnh cắt
        private string _croppedFolderPath = string.Empty;
        private string? _croppedFolderSourceNodeId;
        private string? _croppedFolderSourceOutputKey;

        // Image Processor settings
        private int _promptSize = 4; // Số lần gửi (1-4, mặc định 4)
        private string _processorPrompt = string.Empty; // Prompt text từ Image Processor
        private bool _isVerticalMode = false; // Hướng xuất: false = ngang (16:9), true = dọc (9:16)
        private string? _renderNodeId; // Node render ảnh được chọn trong tab Cấu hình
        private string? _renderNodeOutputKey; // Output key của node render ảnh

        private string _lastExecutionId = string.Empty; // Id lần chạy workflow gần nhất đi qua node này

        /// <summary>Danh sách output keys bị skip (checked = true nghĩa là không xử lý output đó).</summary>
        public HashSet<string> SkipOutputs { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler? PropertyChanged;

        public ImageProcessingNode()
        {
            Type = NodeType.ImageProcessing;
            Title = "Xử lý ảnh";
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

        /// <summary>Reference tới UI element (dùng bởi NodeControl và Renderer).</summary>
        public TextBlock? TitleTextBlockUI { get; set; }

        public double Width
        {
            get => _width;
            set
            {
                if (Math.Abs(_width - value) > 0.01 && value >= 260)
                {
                    _width = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Height
        {
            get => _height;
            set
            {
                if (Math.Abs(_height - value) > 0.01 && value >= 200)
                {
                    _height = value;
                    OnPropertyChanged();
                }
            }
        }

        public ImageInputMode InputMode
        {
            get => _inputMode;
            set { if (_inputMode != value) { _inputMode = value; OnPropertyChanged(); } }
        }

        /// <summary>URL/file path ảnh (khi InputMode=Url).</summary>
        public string ImageUrl
        {
            get => _imageUrl;
            set { if (_imageUrl != value) { _imageUrl = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string? ImageUrlSourceNodeId
        {
            get => _imageUrlSourceNodeId;
            set { if (_imageUrlSourceNodeId != value) { _imageUrlSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? ImageUrlSourceOutputKey
        {
            get => _imageUrlSourceOutputKey;
            set { if (_imageUrlSourceOutputKey != value) { _imageUrlSourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>Base64 ảnh (có thể là raw hoặc data URI) (khi InputMode=Base64).</summary>
        public string ImageBase64
        {
            get => _imageBase64;
            set { if (_imageBase64 != value) { _imageBase64 = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string? ImageBase64SourceNodeId
        {
            get => _imageBase64SourceNodeId;
            set { if (_imageBase64SourceNodeId != value) { _imageBase64SourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? ImageBase64SourceOutputKey
        {
            get => _imageBase64SourceOutputKey;
            set { if (_imageBase64SourceOutputKey != value) { _imageBase64SourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>Ưu tiên dùng GPU khi execute với ffmpeg.</summary>
        public bool PreferGpu
        {
            get => _preferGpu;
            set { if (_preferGpu != value) { _preferGpu = value; OnPropertyChanged(); } }
        }

        /// <summary>Filter graph (ví dụ: scale, crop, eq...). Nếu rỗng: copy/convert default.</summary>
        public string FfmpegFilter
        {
            get => _ffmpegFilter;
            set { if (_ffmpegFilter != value) { _ffmpegFilter = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Kiểu crop: polygon tự do hoặc chuẩn hoá sang bounding-rect theo tỉ lệ gốc.</summary>
        public ImageCropMode CropMode
        {
            get => _cropMode;
            set { if (_cropMode != value) { _cropMode = value; OnPropertyChanged(); } }
        }

        /// <summary>Folder đích lưu ảnh đã cắt (nếu không dùng Node+Key sẽ fallback Downloads).</summary>
        public string CroppedFolderPath
        {
            get => _croppedFolderPath;
            set { if (_croppedFolderPath != value) { _croppedFolderPath = value ?? string.Empty; OnPropertyChanged(); } }
        }

        public string? CroppedFolderSourceNodeId
        {
            get => _croppedFolderSourceNodeId;
            set { if (_croppedFolderSourceNodeId != value) { _croppedFolderSourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? CroppedFolderSourceOutputKey
        {
            get => _croppedFolderSourceOutputKey;
            set { if (_croppedFolderSourceOutputKey != value) { _croppedFolderSourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>Số lần gửi prompt (1-4, mặc định 4).</summary>
        public int PromptSize
        {
            get => _promptSize;
            set
            {
                if (_promptSize != value && value >= 1 && value <= 4)
                {
                    _promptSize = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Prompt text từ Image Processor column.</summary>
        public string ProcessorPrompt
        {
            get => _processorPrompt;
            set { if (_processorPrompt != value) { _processorPrompt = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Hướng xuất từ Image Processor: false = ngang (16:9), true = dọc (9:16).</summary>
        public bool IsVerticalMode
        {
            get => _isVerticalMode;
            set { if (_isVerticalMode != value) { _isVerticalMode = value; OnPropertyChanged(); } }
        }

        /// <summary>Id node render ảnh (được chọn trong tab Cấu hình). Dùng để biết ảnh render thuộc crop nào qua ExecutionId.</summary>
        public string? RenderNodeId
        {
            get => _renderNodeId;
            set { if (_renderNodeId != value) { _renderNodeId = value; OnPropertyChanged(); } }
        }

        /// <summary>Output key của node render ảnh.</summary>
        public string? RenderNodeOutputKey
        {
            get => _renderNodeOutputKey;
            set { if (_renderNodeOutputKey != value) { _renderNodeOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Id lần chạy workflow (ExecutionId) gần nhất mà node ảnh này được thực thi.
        /// Dùng để UI/logic xác định đúng ảnh render thuộc lần chạy nào.
        /// </summary>
        public string LastExecutionId
        {
            get => _lastExecutionId;
            set { if (_lastExecutionId != value) { _lastExecutionId = value ?? string.Empty; OnPropertyChanged(); } }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void NotifyTitleChanged() => OnPropertyChanged(nameof(Title));

        /// <summary>
        /// Helper để trigger UI refresh khi Apply trong dialog nhưng giá trị không đổi.
        /// </summary>
        public void RaisePropertyChanged(string propertyName) => OnPropertyChanged(propertyName);
    }
}


