// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.ImageEditor.Commands
{
    /// <summary>Command: Gộp các layer đã chọn thành 1 layer (hoặc gộp layer hiện tại xuống layer dưới).</summary>
    public sealed class MergeLayersCommand : IEditorCommand
    {
        private readonly EditorDocument _doc;
        private readonly List<EditorLayer> _selectedLayers; // Sắp xếp từ dưới lên trên (chỉ số tăng dần)
        private readonly EditorLayer _targetLayer; // Layer dưới cùng của nhóm được chọn (vẽ đè lên layer này)
        private readonly List<EditorLayer> _layersToRemove; // Các layer còn lại trong nhóm gộp (bị xoá đi)
        
        // Trạng thái gốc của target layer để phục vụ Undo
        private readonly byte[] _targetLayerOriginalPixels;
        private readonly double _targetLayerOriginalOpacity;
        private readonly BlendMode _targetLayerOriginalBlendMode;
        private readonly bool _targetLayerOriginalIsText;

        // Lưu thông tin index ban đầu của các layer bị xoá
        private readonly List<(EditorLayer layer, int index)> _removedLayersInfo = new List<(EditorLayer, int)>();
        private readonly EditorLayer? _previousActiveLayer;

        public MergeLayersCommand(EditorDocument doc, IEnumerable<EditorLayer> layers)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            
            // Sắp xếp các layer được chọn theo thứ tự trong document (dưới cùng -> trên cùng)
            _selectedLayers = layers
                .Where(l => doc.Layers.Contains(l))
                .OrderBy(l => doc.Layers.IndexOf(l))
                .ToList();

            if (_selectedLayers.Count < 2)
            {
                throw new InvalidOperationException("Need at least 2 layers to merge.");
            }

            _targetLayer = _selectedLayers[0];
            _layersToRemove = _selectedLayers.Skip(1).ToList();
            _previousActiveLayer = doc.ActiveLayer;

            // Backup pixel data và các thuộc tính của target layer
            var stride = _targetLayer.Width * 4;
            _targetLayerOriginalPixels = new byte[stride * _targetLayer.Height];
            _targetLayer.Bitmap.CopyPixels(_targetLayerOriginalPixels, stride, 0);
            _targetLayerOriginalOpacity = _targetLayer.Opacity;
            _targetLayerOriginalBlendMode = _targetLayer.BlendMode;
            _targetLayerOriginalIsText = _targetLayer.IsTextLayer;

            // Lưu index các layer sẽ bị xoá
            foreach (var layer in _layersToRemove)
            {
                int index = _doc.Layers.IndexOf(layer);
                _removedLayersInfo.Add((layer, index));
            }
        }

        public string Description => $"Merge {_selectedLayers.Count} layers";

        public void Execute()
        {
            // Thực hiện composite riêng nhóm layer được chọn
            // Tạm ẩn các layer không tham gia gộp
            var originalTempHidden = _doc.Layers.ToDictionary(l => l, l => l.IsTempHidden);
            var originalIsVisible = _doc.Layers.ToDictionary(l => l, l => l.IsVisible);

            try
            {
                foreach (var l in _doc.Layers)
                {
                    if (!_selectedLayers.Contains(l))
                    {
                        l.IsTempHidden = true;
                    }
                    else
                    {
                        l.IsTempHidden = false;
                        l.IsVisible = true; // Chắc chắn hiển thị các layer gộp
                    }
                }

                // Lấy kết quả composite của nhóm gộp
                BitmapSource composited = _doc.Composite();

                // Copy kết quả vào target layer
                _targetLayer.CopyFrom(composited);
                
                // Reset/bỏ transform và chế độ text trên target layer vì đã gộp phẳng pixel
                _targetLayer.OriginalTransformBitmap = null;
                _targetLayer.IsTextLayer = false;
                _targetLayer.TextContent = string.Empty;

                // Reset opacity & blend mode về mặc định
                _targetLayer.Opacity = 1.0;
                _targetLayer.BlendMode = BlendMode.Normal;
                _targetLayer.InvalidateThumbnail();
            }
            finally
            {
                // Khôi phục lại trạng thái hiển thị của các layer khác
                foreach (var kvp in originalTempHidden)
                {
                    kvp.Key.IsTempHidden = kvp.Value;
                }
                foreach (var kvp in originalIsVisible)
                {
                    kvp.Key.IsVisible = kvp.Value;
                }
            }

            // Xoá các layer tham gia gộp khác khỏi document (thứ tự index giảm dần)
            var sortedToRemove = _removedLayersInfo.OrderByDescending(x => x.index).ToList();
            foreach (var item in sortedToRemove)
            {
                _doc.Layers.Remove(item.layer);
            }

            // Chọn target layer làm active và selected
            _doc.ActiveLayer = _targetLayer;
            foreach (var l in _doc.Layers)
            {
                l.IsSelected = (l == _targetLayer);
            }
        }

        public void Undo()
        {
            // Phục hồi target layer về pixel và thuộc tính ban đầu
            var stride = _targetLayer.Width * 4;
            _targetLayer.Bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, _targetLayer.Width, _targetLayer.Height), _targetLayerOriginalPixels, stride, 0);
            _targetLayer.Opacity = _targetLayerOriginalOpacity;
            _targetLayer.BlendMode = _targetLayerOriginalBlendMode;
            _targetLayer.IsTextLayer = _targetLayerOriginalIsText;
            _targetLayer.InvalidateThumbnail();

            // Chèn lại các layer đã bị xoá theo index ban đầu
            var sortedToInsert = _removedLayersInfo.OrderBy(x => x.index).ToList();
            foreach (var item in sortedToInsert)
            {
                int idx = Math.Clamp(item.index, 0, _doc.Layers.Count);
                _doc.Layers.Insert(idx, item.layer);
            }

            // Khôi phục active layer và selection ban đầu
            _doc.ActiveLayer = _previousActiveLayer;
            foreach (var l in _doc.Layers)
            {
                l.IsSelected = _selectedLayers.Contains(l);
            }
        }
    }
}
