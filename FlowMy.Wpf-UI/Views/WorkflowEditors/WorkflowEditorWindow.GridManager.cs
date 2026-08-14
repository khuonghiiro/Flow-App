// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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

            // Use theme-aware grid colors and canvas background so the pattern stays subtle/readable.
            var gridColor = _colorThemeService.GetColor("CanvasGridBrush") ?? Colors.LightGray;
            var canvasBackgroundColor = _colorThemeService.GetColor("CanvasBackgroundBrush");
            _gridPatternService.UpdatePattern(_currentGridType, gridColor, canvasBackgroundColor);
        }


    }
}

