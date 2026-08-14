// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.ImageEditor
{
    /// <summary>
    /// Quản lý undo/redo theo Command pattern.
    /// Mỗi thao tác (vẽ, thêm layer, xoá layer...) tạo 1 IEditorCommand,
    /// gọi Execute() qua History để tự động push vào undo stack.
    /// </summary>
    public sealed class EditorHistory : INotifyPropertyChanged
    {
        private readonly Stack<IEditorCommand> _undoStack = new();
        private readonly Stack<IEditorCommand> _redoStack = new();

        /// <summary>Số command tối đa trong undo stack (tránh tốn RAM).</summary>
        public int MaxUndoLevels { get; set; } = 50;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        /// <summary>Mô tả command tiếp theo sẽ undo (hiển thị tooltip).</summary>
        public string? NextUndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : null;

        /// <summary>Mô tả command tiếp theo sẽ redo.</summary>
        public string? NextRedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

        /// <summary>
        /// Thực thi command và push vào undo stack.
        /// Xoá redo stack (sau khi thực hiện action mới, không thể redo nữa).
        /// </summary>
        public void Execute(IEditorCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();

            // Trim nếu vượt quá limit
            TrimUndoStack();

            NotifyAll();
        }

        /// <summary>Undo command gần nhất.</summary>
        public void Undo()
        {
            if (_undoStack.Count == 0) return;

            var cmd = _undoStack.Pop();
            cmd.Undo();
            _redoStack.Push(cmd);

            NotifyAll();
        }

        /// <summary>Redo command vừa undo.</summary>
        public void Redo()
        {
            if (_redoStack.Count == 0) return;

            var cmd = _redoStack.Pop();
            cmd.Execute();
            _undoStack.Push(cmd);

            NotifyAll();
        }

        /// <summary>Xoá toàn bộ history.</summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            NotifyAll();
        }

        private void TrimUndoStack()
        {
            if (MaxUndoLevels <= 0 || _undoStack.Count <= MaxUndoLevels) return;

            // Stack không hỗ trợ remove bottom trực tiếp — rebuild
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            // items[0] = top (mới nhất), items[N-1] = bottom (cũ nhất)
            // Giữ MaxUndoLevels item mới nhất
            for (int i = Math.Min(items.Length, MaxUndoLevels) - 1; i >= 0; i--)
                _undoStack.Push(items[i]);
        }

        private void NotifyAll()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(UndoCount));
            OnPropertyChanged(nameof(RedoCount));
            OnPropertyChanged(nameof(NextUndoDescription));
            OnPropertyChanged(nameof(NextRedoDescription));
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}
