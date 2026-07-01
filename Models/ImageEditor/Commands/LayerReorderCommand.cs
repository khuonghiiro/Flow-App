using System;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>Undo command: thay đổi thứ tự layer trong stack.</summary>
    public sealed class LayerReorderCommand : IEditorCommand
    {
        private readonly EditorDocument _doc;
        private readonly int _fromIndex;
        private readonly int _toIndex;

        public LayerReorderCommand(EditorDocument doc, int fromIndex, int toIndex)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _fromIndex = fromIndex;
            _toIndex = toIndex;
        }

        public string Description => "Reorder layers";

        public void Execute()
        {
            if (IsValid(_fromIndex, _toIndex))
                _doc.Layers.Move(_fromIndex, _toIndex);
        }

        public void Undo()
        {
            if (IsValid(_toIndex, _fromIndex))
                _doc.Layers.Move(_toIndex, _fromIndex);
        }

        private bool IsValid(int from, int to)
            => from >= 0 && from < _doc.Layers.Count
            && to >= 0 && to < _doc.Layers.Count
            && from != to;
    }
}
