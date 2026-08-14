namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Một dòng cấu hình: lưu giá trị output của node khác thành file trong thư mục tải về.
    /// </summary>
    public sealed class FileDownloadAdditionalOutputSaveEntry
    {
        public string? SourceNodeId { get; set; }
        public string? SourceOutputKey { get; set; }

        /// <summary>Mẫu tên (placeholder giống FileNameTemplate). Để trống dùng mẫu mặc định của node.</summary>
        public string? NameTemplate { get; set; }

        /// <summary>Đuôi / định dạng: txt, .txt, csv, json…</summary>
        public string? SaveFormat { get; set; }
    }
}
