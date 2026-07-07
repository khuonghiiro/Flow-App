using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>Command: Nhân bản nhiều layer cùng lúc, có hỗ trợ hoàn tác.</summary>
    public sealed class DuplicateLayersCommand : IEditorCommand
    {
        private readonly EditorDocument _doc;
        private readonly List<EditorLayer> _sourceLayers;
        private readonly List<EditorLayer> _duplicatedLayers = new List<EditorLayer>();
        private readonly EditorLayer? _previousActiveLayer;

        public DuplicateLayersCommand(EditorDocument doc, IEnumerable<EditorLayer> layers)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _sourceLayers = layers.Where(l => doc.Layers.Contains(l)).ToList();
            _previousActiveLayer = doc.ActiveLayer;
        }

        public string Description => _sourceLayers.Count == 1
            ? $"Duplicate layer \"{_sourceLayers[0].Name}\""
            : $"Duplicate {_sourceLayers.Count} layers";

        public List<EditorLayer> DuplicatedLayers => _duplicatedLayers;

        public void Execute()
        {
            if (_sourceLayers.Count == 0) return;

            bool isFirstTime = _duplicatedLayers.Count == 0;
            var tempDuplicates = new List<(EditorLayer duplicate, int insertIndex)>();

            for (int i = 0; i < _sourceLayers.Count; i++)
            {
                var src = _sourceLayers[i];
                int srcIdx = _doc.Layers.IndexOf(src);
                if (srcIdx < 0) continue;

                EditorLayer dup;
                if (isFirstTime)
                {
                    dup = src.Duplicate();
                    dup.Name = GenerateCopyName(src.Name);
                    _duplicatedLayers.Add(dup);
                }
                else
                {
                    dup = _duplicatedLayers[i];
                }

                tempDuplicates.Add((dup, srcIdx + 1));
            }

            // Chèn theo thứ tự chỉ số giảm dần của vị trí gốc để tránh lệch index
            var descendingDuplicates = tempDuplicates.OrderByDescending(x => x.insertIndex).ToList();
            foreach (var item in descendingDuplicates)
            {
                int idx = Math.Clamp(item.insertIndex, 0, _doc.Layers.Count);
                _doc.Layers.Insert(idx, item.duplicate);
            }

            // Chọn layer nhân bản cao nhất làm active layer
            if (_duplicatedLayers.Count > 0)
            {
                _doc.ActiveLayer = _duplicatedLayers[^1];
            }
        }

        public void Undo()
        {
            foreach (var dup in _duplicatedLayers)
            {
                _doc.Layers.Remove(dup);
            }
            _doc.ActiveLayer = _previousActiveLayer;
            if (_previousActiveLayer != null)
            {
                _previousActiveLayer.IsSelected = true;
            }
        }

        private string GenerateCopyName(string baseName)
        {
            string prefix = baseName;
            int baseNum = 0;
            int i = baseName.Length - 1;
            while (i >= 0 && char.IsDigit(baseName[i])) i--;

            if (i < baseName.Length - 1)
            {
                prefix = baseName.Substring(0, i + 1);
                if (int.TryParse(baseName.Substring(i + 1), out int parsed))
                    baseNum = parsed;
            }
            else
            {
                prefix = baseName + " ";
                baseNum = 0;
            }

            int num = baseNum + 1;
            while (true)
            {
                string candidate = $"{prefix}{num}";
                bool exists = false;
                foreach (var l in _doc.Layers)
                {
                    if (string.Equals(l.Name, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    return candidate;
                num++;
            }
        }
    }
}
