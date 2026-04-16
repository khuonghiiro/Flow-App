using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Rendering;
using System.Collections.Generic;
using System.Linq;

namespace FlowMy.Services.Workflow
{
    /// <summary>
    /// Folder lưu ảnh / folder lưu video: nếu textbox không trống thì dùng textbox;
    /// nếu trống thì lấy từ node + key tương ứng (ảnh: FolderSourceNodeId/Key, video: FolderSourceNodeIdVideo/Key).
    /// </summary>
    public static class MediaGalleryFolderHelper
    {
        /// <summary>Trả về đường dẫn folder hiệu dụng. forVideo = true → folder lưu video, false → folder lưu ảnh.</summary>
        public static string GetEffectiveFolderPath(MediaGalleryNode node, IList<WorkflowNode>? nodes, bool forVideo = false)
        {
            if (node == null) return string.Empty;
            if (forVideo)
            {
                if (!string.IsNullOrWhiteSpace(node.FolderSaveVideos))
                    return node.FolderSaveVideos.Trim();
                if (string.IsNullOrWhiteSpace(node.FolderSourceNodeIdVideo) || string.IsNullOrWhiteSpace(node.FolderSourceOutputKeyVideo))
                    return string.Empty;
                if (nodes == null) return string.Empty;
                var src = nodes.FirstOrDefault(n => string.Equals(n.Id, node.FolderSourceNodeIdVideo, System.StringComparison.OrdinalIgnoreCase));
                if (src == null) return string.Empty;
                var value = NodeDataPanelService.ResolveDynamicValueByKey(src, node.FolderSourceOutputKeyVideo.Trim());
                return string.IsNullOrWhiteSpace(value) || value == "—" ? string.Empty : value.Trim();
            }
            if (!string.IsNullOrWhiteSpace(node.FolderSaveImages))
                return node.FolderSaveImages.Trim();
            if (string.IsNullOrWhiteSpace(node.FolderSourceNodeId) || string.IsNullOrWhiteSpace(node.FolderSourceOutputKey))
                return string.Empty;
            if (nodes == null) return string.Empty;
            var srcImg = nodes.FirstOrDefault(n => string.Equals(n.Id, node.FolderSourceNodeId, System.StringComparison.OrdinalIgnoreCase));
            if (srcImg == null) return string.Empty;
            var valueImg = NodeDataPanelService.ResolveDynamicValueByKey(srcImg, node.FolderSourceOutputKey.Trim());
            return string.IsNullOrWhiteSpace(valueImg) || valueImg == "—" ? string.Empty : valueImg.Trim();
        }
    }
}
