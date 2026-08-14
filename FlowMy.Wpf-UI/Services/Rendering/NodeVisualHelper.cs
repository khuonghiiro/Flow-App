// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models;
using FlowMy.Models.Nodes;
using System;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Services.Rendering
{
    public static class NodeVisualHelper
    {
        /// <summary>
        /// Image: bóng trên shadowPlate; không cache Border ngoài (tránh mờ toolbar/ảnh).
        /// Video: cùng nguyên tắc — không gắn DropShadow/BitmapCache lên Border bọc toàn bộ UI (làm mờ như design vs canvas).
        /// </summary>
        public static void ApplyEditorGpuChrome(WorkflowNode node, Border border, bool hostWantsNodeCache)
        {
            if (border == null) return;
            if (node is ImageProcessingNode)
            {
                border.Effect = null;
                GpuOptimizationHelper.ApplyToBorder(border, isDragging: false, forceCache: false);
                FlowMy.Views.NodeControls.ImageProcessingNodeControl.RefreshImageWorkflowChromeDropShadow(border);
                return;
            }

            if (node is VideoProcessingNode || node is FlowMy.Models.Nodes.BodyContainerNode || node is FlowMy.Models.Nodes.ActionCanVasNode)
            {
                border.Effect = null;
                GpuOptimizationHelper.ApplyToBorder(border, isDragging: false, forceCache: false);
                return;
            }

            GpuOptimizationHelper.ApplyToBorder(border, isDragging: false, forceCache: hostWantsNodeCache);
            border.Effect = GpuOptimizationHelper.CreateDropShadowEffect();
        }
    }
}
