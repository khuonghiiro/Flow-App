// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>Command: Xoá nhiều layer cùng lúc, có hỗ trợ hoàn tác.</summary>
    public sealed class DeleteLayersCommand : IEditorCommand
    {
        private readonly EditorDocument _doc;
        private readonly List<EditorLayer> _layers;
        private readonly List<(EditorLayer layer, int index)> _removedLayersInfo = new List<(EditorLayer, int)>();
        private readonly EditorLayer? _previousActiveLayer;
        private EditorLayer? _newActiveLayer;

        public DeleteLayersCommand(EditorDocument doc, IEnumerable<EditorLayer> layers)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _layers = layers.ToList();
            _previousActiveLayer = doc.ActiveLayer;
        }

        public string Description => _layers.Count == 1 
            ? $"Remove layer \"{_layers[0].Name}\"" 
            : $"Remove {_layers.Count} layers";

        public void Execute()
        {
            _removedLayersInfo.Clear();
            
            // Lọc ra các layer thực sự tồn tại trong tài liệu
            var toRemove = _layers.Where(l => _doc.Layers.Contains(l)).ToList();
            if (toRemove.Count == _doc.Layers.Count)
            {
                // Không cho phép xoá toàn bộ layer, giữ lại layer đầu tiên
                toRemove.RemoveAt(0);
            }

            if (toRemove.Count == 0) return;

            // Lưu thông tin index ban đầu của các layer trước khi xoá
            foreach (var layer in toRemove)
            {
                int index = _doc.Layers.IndexOf(layer);
                _removedLayersInfo.Add((layer, index));
            }

            // Xoá các layer theo thứ tự index giảm dần để tránh lệch index trong quá trình xoá
            var sortedToRemove = _removedLayersInfo.OrderByDescending(x => x.index).ToList();
            foreach (var item in sortedToRemove)
            {
                _doc.Layers.Remove(item.layer);
            }

            // Chọn active layer mới nếu active layer cũ bị xoá
            if (_previousActiveLayer == null || toRemove.Contains(_previousActiveLayer))
            {
                int minRemovedIndex = _removedLayersInfo.Min(x => x.index);
                int targetIndex = Math.Clamp(minRemovedIndex, 0, _doc.Layers.Count - 1);
                if (targetIndex >= 0 && targetIndex < _doc.Layers.Count)
                {
                    _newActiveLayer = _doc.Layers[targetIndex];
                    _doc.ActiveLayer = _newActiveLayer;
                    _newActiveLayer.IsSelected = true;
                }
            }
        }

        public void Undo()
        {
            // Thêm các layer trở lại theo thứ tự index tăng dần để khôi phục cấu trúc ban đầu
            var sortedToInsert = _removedLayersInfo.OrderBy(x => x.index).ToList();
            foreach (var item in sortedToInsert)
            {
                int idx = Math.Clamp(item.index, 0, _doc.Layers.Count);
                _doc.Layers.Insert(idx, item.layer);
            }

            _doc.ActiveLayer = _previousActiveLayer;
            if (_previousActiveLayer != null)
            {
                _previousActiveLayer.IsSelected = true;
            }
        }
    }
}
