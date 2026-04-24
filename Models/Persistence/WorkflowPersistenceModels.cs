using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlowMy.Models.Persistence
{
    public class WorkflowDto
    {
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
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public string Type { get; set; } = "Generic";
        public string? ColorKey { get; set; }
        
        // Custom properties for different node types
        public Dictionary<string, object> Properties { get; set; } = new();

        public List<PortDto> Ports { get; set; } = new();

        /// <summary>
        /// Saved output values per output key (chỉ khi lưu Ctrl+S, không có khi Export).
        /// Key = output key, Value = giá trị đã resolve lúc lưu.
        /// </summary>
        public Dictionary<string, string>? OutputValues { get; set; }
    }

    public class PortDto
    {
        public string Id { get; set; } = string.Empty;
        public bool IsInput { get; set; }
        public string Position { get; set; } = "Left";
        public int Index { get; set; }
        /// <summary>Cho ConditionalNode/AsyncTaskNode: index của branch (0=if, 1=else if, ...). Null = file cũ, dùng Index.</summary>
        public int? BranchIndex { get; set; }
    }

    public class ConnectionDto
    {
        public string FromNodeId { get; set; } = string.Empty;
        public string ToNodeId { get; set; } = string.Empty;
        public string? FromPortId { get; set; }
        public string? ToPortId { get; set; }
    }
}
