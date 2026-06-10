namespace FlowMy.Models
{
    /// <summary>
    /// Enum cho loại node
    /// </summary>
    public enum NodeType
    {
        Generic,
        Start,
        End,
        Input,
        Output,
        Process,
        IfElse,
        Loop,
        Delay,
        Keyboard,
        KeyPressEvent,
        HotkeyPressEvent,
        MouseEvent,
        Variable,
        Function,
        Condition,
        ScreenPosition,
        ScreenCapture,
        Break,
        Continue,
        StringSplit,
        LoopContext,
        ListOut,
        // Collect outputs across AsyncTask parallel dispatch iterations (parallel-safe aggregation)
        AsyncTaskDispatchCollect,
        HttpRequest,
        AssignData,
        MediaGallery,
        ImageProcessing,
        VideoProcessing,
        Code,
        Folder,
        Web,
        HtmlUi,
        AsyncTask,
        Notification,
        Storage,
        Callback,
        DataFetcher,
        /// <summary>Kho key→giá trị scoped theo node (WIP KeyScoped).</summary>
        KeyScopedStore,
        /// <summary>Key/value bridge — kênh runtime chung theo một lần chạy workflow.</summary>
        KeyValueBridge,
        /// <summary>Tải file từ URL hoặc curl vào thư mục đích.</summary>
        FileDownload,
        /// <summary>Body container tự do để gom node theo vùng.</summary>
        BodyContainer,
        /// <summary>Liệt kê (và tuỳ chọn đọc) file trong một thư mục.</summary>
        FolderFilePaths,
        FlowOverwrite,
        /// <summary>Git source — clone/pull repo, mở VSCodium.</summary>
        GitSource,
        /// <summary>Ghi lại và phát lại thao tác chuột/bàn phím.</summary>
        MacroRecorder,
        /// <summary>Hiển thị viền sáng màn hình với cấu hình màu, độ dày, hiệu ứng.</summary>
        BorderHighlight,
        /// <summary>Quét text từ ảnh (OCR) với Windows.Media.Ocr hoặc OpenCV/ML.NET.</summary>
        TextScan,
        /// <summary>Nhúng ứng dụng desktop vào canvas với kích thước và tương tác tùy chỉnh.</summary>
        EmbedApplication
    }
}

