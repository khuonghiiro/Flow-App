using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace FlowMy.Models
{
    /// <summary>
    /// Đại diện cho một thư viện JS/CSS offline đã được tải về máy tính.
    /// User có thể enable/disable để inject vào HTML UI tự động.
    /// </summary>
    public sealed class HtmlOfflineAsset : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _title = string.Empty;
        private string _description = string.Empty;
        private string _sourceUrl = string.Empty;
        private string _localFileName = string.Empty;
        private string _assetType = "js"; // "js" hoặc "css"
        private bool _isEnabled = true;

        /// <summary>GUID định danh duy nhất của asset.</summary>
        public string Id
        {
            get => _id;
            set { if (_id != value) { _id = value ?? Guid.NewGuid().ToString("N"); OnPropertyChanged(); } }
        }

        /// <summary>Tên hiển thị thân thiện — ví dụ: "Chart.js 4.x".</summary>
        public string Title
        {
            get => _title;
            set { if (_title != value) { _title = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>Mô tả ngắn gọn giúp user hiểu asset dùng để làm gì.</summary>
        public string Description
        {
            get => _description;
            set { if (_description != value) { _description = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>URL nguồn (CDN hoặc link download gốc) — để tải lại nếu cần.</summary>
        public string SourceUrl
        {
            get => _sourceUrl;
            set { if (_sourceUrl != value) { _sourceUrl = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Tên file đã lưu trong thư mục HtmlUiAssets (ví dụ: "chart.min.js").
        /// Không chứa path đầy đủ — path được resolve qua HtmlOfflineAssetService.
        /// </summary>
        public string LocalFileName
        {
            get => _localFileName;
            set { if (_localFileName != value) { _localFileName = value ?? string.Empty; OnPropertyChanged(); } }
        }

        /// <summary>"js" hoặc "css".</summary>
        public string AssetType
        {
            get => _assetType;
            set
            {
                var normalized = (value ?? "js").ToLowerInvariant();
                if (normalized != "js" && normalized != "css") normalized = "js";
                if (_assetType != normalized) { _assetType = normalized; OnPropertyChanged(); }
            }
        }

        /// <summary>Nếu true: asset sẽ được inject vào HTML khi render HtmlUiNode.</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); } }
        }

        /// <summary>Icon badge hiển thị trong UI theo loại asset.</summary>
        [JsonIgnore]
        public string TypeBadge => AssetType == "css" ? "CSS" : "JS";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
