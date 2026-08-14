using FlowMy.Models;
using FlowMy.Services.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Windows.Media;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// ViewModel cho 1 item trong danh sách offline asset của HtmlUiNode.
    /// </summary>
    public partial class HtmlOfflineAssetItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _id = System.Guid.NewGuid().ToString("N");

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _sourceUrl = string.Empty;

        [ObservableProperty]
        private string _localFileName = string.Empty;

        [ObservableProperty]
        private string _assetType = "js"; // "js" hoặc "css"

        [ObservableProperty]
        private bool _isEnabled = true;

        // ─── Runtime-only (không serialize) ───────────────────────────

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        // ─── Computed ─────────────────────────────────────────────────

        /// <summary>Badge hiển thị type: "JS" hoặc "CSS".</summary>
        public string TypeBadge => AssetType?.ToUpperInvariant() == "CSS" ? "CSS" : "JS";

        /// <summary>Badge background Brush theo type.</summary>
        public SolidColorBrush TypeBadgeBrush => AssetType?.ToUpperInvariant() == "CSS"
            ? new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED))
            : new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06));

        /// <summary>File asset có tồn tại trên disk không.</summary>
        public bool IsLocalAvailable =>
            !string.IsNullOrWhiteSpace(LocalFileName) &&
            HtmlOfflineAssetService.AssetExists(LocalFileName);

        /// <summary>Hiển thị trạng thái có/không có file local.</summary>
        public string LocalStatusText =>
            IsLocalAvailable ? "✓ Có sẵn" : "✗ Chưa tải";

        public SolidColorBrush LocalStatusBrush => IsLocalAvailable
            ? new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80))
            : new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));

        /// <summary>Refresh các computed property sau khi tải xong.</summary>
        public void NotifyLocalAvailabilityChanged()
        {
            OnPropertyChanged(nameof(IsLocalAvailable));
            OnPropertyChanged(nameof(LocalStatusText));
            OnPropertyChanged(nameof(LocalStatusBrush));
        }

        partial void OnLocalFileNameChanged(string value)
        {
            NotifyLocalAvailabilityChanged();
        }

        partial void OnAssetTypeChanged(string value)
        {
            OnPropertyChanged(nameof(TypeBadge));
            OnPropertyChanged(nameof(TypeBadgeBrush));
        }

        /// <summary>Chuyển từ HtmlOfflineAsset model sang ViewModel.</summary>
        public static HtmlOfflineAssetItemViewModel FromModel(HtmlOfflineAsset asset)
        {
            return new HtmlOfflineAssetItemViewModel
            {
                Id = asset.Id,
                Title = asset.Title,
                Description = asset.Description,
                SourceUrl = asset.SourceUrl,
                LocalFileName = asset.LocalFileName,
                AssetType = asset.AssetType,
                IsEnabled = asset.IsEnabled,
                StatusMessage = HtmlOfflineAssetService.AssetExists(asset.LocalFileName)
                    ? "✓ Có sẵn" : "✗ Chưa tải về"
            };
        }

        /// <summary>Chuyển từ ViewModel sang HtmlOfflineAsset model để persist.</summary>
        public HtmlOfflineAsset ToModel()
        {
            return new HtmlOfflineAsset
            {
                Id = Id,
                Title = Title,
                Description = Description,
                SourceUrl = SourceUrl,
                LocalFileName = LocalFileName,
                AssetType = AssetType,
                IsEnabled = IsEnabled
            };
        }

        /// <summary>Danh sách preset phổ biến để user chọn nhanh.</summary>
        public static IReadOnlyList<AssetPreset> WellKnownPresets { get; } = new List<AssetPreset>
        {
            new("Google Fonts — Inter + JetBrains Mono",
                "Font UI + monospace. Khi tải, app gộp cả file .woff2 vào CSS (data URI) để chạy offline trong HtmlUiNode.",
                "https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500&display=swap",
                "google-fonts-inter-jetbrains.css", "css"),
            new("Google Fonts — Inter",
                "Bộ Inter (400–700). Tải offline tương tự (woff2 nhúng trong CSS).",
                "https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap",
                "google-fonts-inter.css", "css"),
            new("Bootstrap 5.3 CSS",
                "Bootstrap v5 stylesheet (grid, utility classes, components).",
                "https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css",
                "bootstrap.min.css", "css"),
            new("Bootstrap Icons 1.11",
                "Icon font (class bi bi-*). Khi tải, app gộp file .woff2 vào CSS (data URI) để chạy offline trong HtmlUi — không cần thẻ &lt;link&gt; CDN.",
                "https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css",
                "bootstrap-icons.min.css", "css"),
            new("Bootstrap 5.3 JS Bundle",
                "Bootstrap v5 JavaScript bundle (bao gồm Popper).",
                "https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js",
                "bootstrap.bundle.min.js", "js"),
            new("Chart.js 4.x",
                "Thư viện vẽ biểu đồ (bar, line, pie, doughnut, radar,...) đẹp và dễ dùng.",
                "https://cdn.jsdelivr.net/npm/chart.js@4.4.3/dist/chart.umd.min.js",
                "chart.umd.min.js", "js"),
            new("Moment.js",
                "Parse, validate, format và tính toán ngày/giờ linh hoạt.",
                "https://cdnjs.cloudflare.com/ajax/libs/moment.js/2.30.1/moment.min.js",
                "moment.min.js", "js"),
            new("Lodash",
                "Utility library: array, object, string manipulation.",
                "https://cdnjs.cloudflare.com/ajax/libs/lodash.js/4.17.21/lodash.min.js",
                "lodash.min.js", "js"),
            new("Alpine.js",
                "Framework JS nhẹ, reactive — dùng trực tiếp trong HTML attribute.",
                "https://cdn.jsdelivr.net/npm/alpinejs@3.14.0/dist/cdn.min.js",
                "alpine.min.js", "js"),
            new("Animate.css",
                "CSS animation library — thêm class là có hiệu ứng animated.",
                "https://cdnjs.cloudflare.com/ajax/libs/animate.css/4.1.1/animate.min.css",
                "animate.min.css", "css"),
            new("Tailwind CSS (Standalone CLI build)",
                "Utility-first CSS framework. Note: file lớn ~3.3MB.",
                "https://cdn.tailwindcss.com",
                "tailwind.min.js", "js"),
            new("D3.js v7",
                "Thư viện visualize dữ liệu mạnh mẽ với SVG/Canvas.",
                "https://cdn.jsdelivr.net/npm/d3@7.9.0/dist/d3.min.js",
                "d3.min.js", "js"),
            new("SortableJS",
                "Drag-and-drop sortable list library — không cần dependency.",
                "https://cdn.jsdelivr.net/npm/sortablejs@1.15.3/Sortable.min.js",
                "sortable.min.js", "js"),
        };
    }

    /// <summary>Preset asset phổ biến để user chọn nhanh.</summary>
    public record AssetPreset(
        string Title,
        string Description,
        string Url,
        string FileName,
        string Type)
    {
        public bool IsInstalled => HtmlOfflineAssetService.AssetExists(FileName);
    }

    /// <summary>
    /// Observable wrapper cho AssetPreset dùng trong UI preset panel.
    /// Có IsInstalled (file trên disk) và IsAdded (đã có trong OfflineAssetsList) — cả 2 đều observable.
    /// </summary>
    public class PresetDisplayItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public AssetPreset Preset { get; }

        public string Title       => Preset.Title;
        public string Description => Preset.Description;
        public string Type        => Preset.Type;
        public string FileName    => Preset.FileName;
        public string Url         => Preset.Url;

        private bool _isInstalled;
        /// <summary>File đã tải về disk chưa.</summary>
        public bool IsInstalled
        {
            get => _isInstalled;
            set => SetProperty(ref _isInstalled, value);
        }

        private bool _isAdded;
        /// <summary>Preset đã được thêm vào danh sách OfflineAssets chưa.</summary>
        public bool IsAdded
        {
            get => _isAdded;
            set => SetProperty(ref _isAdded, value);
        }

        /// <summary>True nếu preset được thêm thủ công bởi user (hiện nhãn NEW).</summary>
        public bool IsCustom { get; }

        public PresetDisplayItem(AssetPreset preset, bool isCustom = false)
        {
            Preset = preset;
            IsCustom = isCustom;
            _isInstalled = preset.IsInstalled;
        }

        /// <summary>Refresh cả IsInstalled và IsAdded từ danh sách hiện tại.</summary>
        public void Refresh(IEnumerable<HtmlOfflineAssetItemViewModel> addedList)
        {
            IsInstalled = Preset.IsInstalled;
            IsAdded = addedList.Any(a =>
                string.Equals(a.LocalFileName, Preset.FileName,
                              System.StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(Preset.Url)
                    && string.Equals(a.SourceUrl, Preset.Url, System.StringComparison.OrdinalIgnoreCase)));
        }
    }
}
