using System;
using System.Windows.Media;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>Undo/Redo command for Text properties editing without copying heavy pixel buffers.</summary>
    public sealed class TextEditCommand : IEditorCommand
    {
        private readonly EditorLayer _layer;
        private readonly Action<EditorLayer> _redrawAction;

        private readonly string _oldText;
        private readonly double _oldX, _oldY, _oldW, _oldH, _oldSize;
        private readonly Color _oldColor;
        private readonly string _oldFamily, _oldStyle, _oldAlign;

        private readonly string _newText;
        private readonly double _newX, _newY, _newW, _newH, _newSize;
        private readonly Color _newColor;
        private readonly string _newFamily, _newStyle, _newAlign;

        public TextEditCommand(
            EditorLayer layer,
            Action<EditorLayer> redrawAction,
            // Old
            string oldText, double oldX, double oldY, double oldW, double oldH, double oldSize, Color oldColor, string oldFamily, string oldStyle, string oldAlign,
            // New
            string newText, double newX, double newY, double newW, double newH, double newSize, Color newColor, string newFamily, string newStyle, string newAlign)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _redrawAction = redrawAction ?? throw new ArgumentNullException(nameof(redrawAction));

            _oldText = oldText; _oldX = oldX; _oldY = oldY; _oldW = oldW; _oldH = oldH; _oldSize = oldSize; _oldColor = oldColor; _oldFamily = oldFamily; _oldStyle = oldStyle; _oldAlign = oldAlign;
            _newText = newText; _newX = newX; _newY = newY; _newW = newW; _newH = newH; _newSize = newSize; _newColor = newColor; _newFamily = newFamily; _newStyle = newStyle; _newAlign = newAlign;
        }

        public string Description => $"Edit Text: \"{(_newText.Length > 15 ? _newText.Substring(0, 12) + "..." : _newText)}\"";

        public void Execute()
        {
            _layer.TextContent = _newText;
            _layer.TextX = _newX;
            _layer.TextY = _newY;
            _layer.TextWidth = _newW;
            _layer.TextHeight = _newH;
            _layer.TextFontSize = _newSize;
            _layer.TextColor = _newColor;
            _layer.TextFontFamily = _newFamily;
            _layer.TextFontStyle = _newStyle;
            _layer.TextAlignment = _newAlign;
            _redrawAction(_layer);
        }

        public void Undo()
        {
            _layer.TextContent = _oldText;
            _layer.TextX = _oldX;
            _layer.TextY = _oldY;
            _layer.TextWidth = _oldW;
            _layer.TextHeight = _oldH;
            _layer.TextFontSize = _oldSize;
            _layer.TextColor = _oldColor;
            _layer.TextFontFamily = _oldFamily;
            _layer.TextFontStyle = _oldStyle;
            _layer.TextAlignment = _oldAlign;
            _redrawAction(_layer);
        }
    }
}
