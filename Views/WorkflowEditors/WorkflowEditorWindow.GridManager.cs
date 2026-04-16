using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        /// <summary>
        /// Khởi tạo grid pattern
        /// </summary>
        private void InitializeGrid()
        {
            UpdateGridPattern();
        }

        /// <summary>
        /// Cập nhật grid pattern dựa trên loại được chọn
        /// </summary>
        private void UpdateGridPattern()
        {
            if (_gridPatternService == null || _colorThemeService == null) return;

            // Use CanvasGridBrush for theme-aware grid colors
            var gridColor = _colorThemeService.GetColor("CanvasGridBrush") ?? Colors.LightGray;
            _gridPatternService.UpdatePattern(_currentGridType, gridColor);
        }


    }
}

