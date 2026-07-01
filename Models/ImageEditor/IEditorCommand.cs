namespace FlowMy.Models.ImageEditor
{
    /// <summary>
    /// Interface cho mọi command trong hệ thống undo/redo.
    /// Mỗi command phải có khả năng Execute (do) và Undo (revert).
    /// </summary>
    public interface IEditorCommand
    {
        /// <summary>Mô tả ngắn để hiện trong UI (VD: "Brush stroke", "Add layer").</summary>
        string Description { get; }

        /// <summary>Thực thi command.</summary>
        void Execute();

        /// <summary>Hoàn tác command — trả trạng thái về trước khi Execute.</summary>
        void Undo();
    }
}
