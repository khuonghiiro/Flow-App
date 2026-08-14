using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowMy.Models.Persistence
{
    public class WorkflowDto
    {
        /// <summary>
        /// Format version: 2 = simplified AI-friendly (omit defaults, auto-generate ports/IDs),
        /// 1 hoặc thiếu = legacy full format. Luôn backward compatible.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Version { get; set; }

        public string Name { get; set; } = string.Empty;
        public List<NodeDto> Nodes { get; set; } = new();
        public List<ConnectionDto> Connections { get; set; } = new();

        // View state properties
        public double ZoomLevel { get; set; } = 1.0;
        public double PanX { get; set; } = 0.0;
        public double PanY { get; set; } = 0.0;
        public double? SavedScreenWidth { get; set; }
        public double? SavedScreenHeight { get; set; }
        public double? SavedViewportCenterX { get; set; }
        public double? SavedViewportCenterY { get; set; }
        public bool IsZoomLocked { get; set; }

        /// <summary>
        /// Kiểu vẽ đường kết nối (Bezier/Orthogonal/Straight).
        /// Lưu dưới dạng string để tương thích version cũ.
        /// </summary>
        public string? ConnectionLineStyle { get; set; } = "Bezier";

        /// <summary>
        /// Tên file gói nén (.webpkg.zip) cùng thư mục với JSON — cookie/session/cache WebView2 + offline HtmlUi.
        /// Null hoặc bỏ qua = export chỉ logic.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PortableWebBundleFileName { get; set; }

        /// <summary>
        /// Metadata mô tả cấu hình đã dùng khi export file này.
        /// Import có thể dùng để tự nhận diện hành vi khôi phục phù hợp.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkflowExportOptionsDto? ExportOptions { get; set; }

        /// <summary>
        /// Dùng cho export .flowz có nhúng web bundle (zip bytes, base64).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EmbeddedPortableWebBundleBase64 { get; set; }
    }

    public class WorkflowExportOptionsDto
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IncludeRuntimeData { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Compressed { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IncludeWebBundle { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IncludeWebCookies { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IncludeOfflineHtmlAssets { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PackageKind { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CompressionMode { get; set; }
    }

    public class NodeDto
    {
        /// <summary>
        /// v2: Id có thể trống hoặc thiếu → system tự sinh "Node_{Type}_{Guid}".
        /// v1: bắt buộc phải có.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public string Type { get; set; } = "Generic";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ColorKey { get; set; }

        /// <summary>
        /// Custom properties cho từng loại node. v2: chỉ ghi specific props (≠ default).
        /// Null hoặc rỗng = không có props đặc thù.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Properties { get; set; }

        /// <summary>
        /// v2: Null = auto-generate standard 2-port (Input Left + Output Right).
        /// v1: luôn có đầy đủ port definitions.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<PortDto>? Ports { get; set; }

        /// <summary>
        /// Saved output values per output key (chỉ khi lưu Ctrl+S, không có khi Export).
        /// Key = output key, Value = giá trị đã resolve lúc lưu.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? OutputValues { get; set; }
    }

    public class PortDto
    {
        /// <summary>v2: Id trống = auto-generate GUID.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Id { get; set; } = string.Empty;

        public bool IsInput { get; set; }
        public string Position { get; set; } = "Left";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Index { get; set; }

        /// <summary>Cho ConditionalNode/AsyncTaskNode: index của branch (0=if, 1=else if, ...). Null = file cũ, dùng Index.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BranchIndex { get; set; }
    }

    /// <summary>
    /// v2: FromNodeId/ToNodeId hỗ trợ cả int index (0, 1, 2...) lẫn string GUID.
    /// Nếu là số nguyên → map sang Node ID theo thứ tự trong Nodes[].
    /// </summary>
    public class ConnectionDto
    {
        /// <summary>
        /// v2: Có thể là index ("0", "1"...) hoặc Node ID string.
        /// AI dùng index, user dùng GUID — system tự detect.
        /// </summary>
        public string FromNodeId { get; set; } = string.Empty;
        public string ToNodeId { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FromPortId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToPortId { get; set; }
    }
}
