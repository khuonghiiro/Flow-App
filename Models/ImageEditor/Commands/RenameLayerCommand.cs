using System;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>Undo command: đổi tên 1 layer.</summary>
    public sealed class RenameLayerCommand : IEditorCommand
    {
        private readonly EditorLayer _layer;
        private readonly string _oldName;
        private readonly string _newName;

        public RenameLayerCommand(EditorLayer layer, string oldName, string newName)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _oldName = oldName ?? string.Empty;
            _newName = newName ?? string.Empty;
        }

        public string Description => $"Rename Layer to \"{_newName}\"";

        public void Execute()
        {
            _layer.Name = _newName;
            _layer.InvalidateThumbnail();
        }

        public void Undo()
        {
            _layer.Name = _oldName;
            _layer.InvalidateThumbnail();
        }
    }
}
