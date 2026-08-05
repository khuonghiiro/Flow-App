using System;
using FlowMy.Models;

namespace FlowMy.Helpers
{
    /// <summary>
    /// Class dùng chung quản lý việc ánh xạ loại Node (NodeType Enum hoặc string) thành Icon Key SVG.
    /// </summary>
    public static class NodeIconHelper
    {
        /// <summary>
        /// Lấy Icon Key mặc định dựa trên <see cref="NodeType"/> enum.
        /// </summary>
        public static string GetIconKey(NodeType type)
        {
            return type switch
            {
                NodeType.Start => "play duotone-regular",
                NodeType.End => "flag-checkered sharp-duotone-solid",
                NodeType.Input => "left-to-dotted-line duotone-regular",
                NodeType.Output => "right-to-dotted-line duotone-regular",
                NodeType.Process => "cog",
                NodeType.IfElse => "list-tree sharp-light",
                NodeType.Loop => "arrows-spin duotone",
                NodeType.Break => "circle-stop duotone",
                NodeType.Continue => "diagram-predecessor duotone-light",
                NodeType.Delay => "timer regular",
                NodeType.Keyboard => "keyboard duotone",
                NodeType.KeyPressEvent => "key duotone-regular",
                NodeType.HotkeyPressEvent => "keyboard duotone",
                NodeType.MouseEvent => "computer-mouse duotone",
                NodeType.Variable => "square-root-variable",
                NodeType.Function => "calculator",
                NodeType.Condition => "list-tree sharp-light",
                NodeType.ScreenPosition => "crosshairs light",
                NodeType.ScreenCapture => "camera-viewfinder duotone-light",
                NodeType.TextScan => "camera-circle-ellipsis duotone-light",
                NodeType.EmbedApplication => "desktop-arrow-down light",
                NodeType.StringSplit => "scissors light",
                NodeType.ListOut => "list-radio regular",
                NodeType.AssignData => "arrows-left-right duotone",
                NodeType.MediaGallery => "image-stack duotone",
                NodeType.ImageProcessing => "adobe_pts Custom-icons.color",
                NodeType.VideoProcessing => "circle-video sharp-light",
                NodeType.Code => "code duotone-regular",
                NodeType.HtmlUi => "html5 brands",
                NodeType.Folder => "folder-open duotone-thin",
                NodeType.HttpRequest => "globe-pointer sharp-duotone-light",
                NodeType.Web => "internet-explorer brands",
                NodeType.AsyncTask => "diagram-project duotone-light",
                NodeType.MacroRecorder => "chart-network light",
                NodeType.BorderHighlight => "bolt-lightning sharp-light",
                NodeType.DataFetcher => "inbox-out duotone-light",
                NodeType.FolderFilePaths => "file-import duotone-light",
                NodeType.KeyValueBridge => "list-check solid",
                NodeType.FlowOverwrite => "merge sharp-regular",
                NodeType.BodyContainer => "square-dashed duotone-light",
                NodeType.Notification => "bell duotone-regular",
                NodeType.Storage => "arrow-progress sharp-regular",
                NodeType.Callback => "arrows-turn-right regular",
                NodeType.FileDownload => "download solid",
                NodeType.AsyncTaskDispatchCollect => "list-radio regular",
                NodeType.KeyScopedStore => "arrow-progress sharp-regular",
                NodeType.LoopContext => "arrows-spin duotone",
                NodeType.GitSource => "git-alt brands",
                NodeType.ActionCanVas => "square-share-nodes light",
                NodeType.ShowInputMsg => "user-message regular",
                NodeType.DynamicUi => "desktop-designer Custom-icons.color",
                _ => "circle-nodes duotone-regular"
            };
        }

        /// <summary>
        /// Lấy Icon Key mặc định dựa trên tên dạng chuỗi của loại node.
        /// </summary>
        public static string GetIconKey(string? typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return "circle-nodes duotone-regular";

            // Nếu parse trực tiếp được sang Enum NodeType thì ưu tiên dùng
            if (Enum.TryParse<NodeType>(typeName, ignoreCase: true, out var parsedType))
            {
                return GetIconKey(parsedType);
            }

            // Xử lý một số alias chuỗi đặc thù
            return typeName switch
            {
                "EmbedApplicationNode" => "desktop-arrow-down light",
                "ActionCanVasNode" => "square-share-nodes light",
                "ShowInputMsgNode" => "user-message regular",
                "DynamicUiNode" => "desktop-designer Custom-icons.color",
                _ => "circle-nodes duotone-regular"
            };
        }

        /// <summary>
        /// Lấy Icon Key theo ưu tiên: Icon tùy chỉnh -> Icon mặc định của nodeType (Enum hoặc string).
        /// </summary>
        public static string ResolveIconKey(string? customIconKey, object? nodeType)
        {
            if (!string.IsNullOrWhiteSpace(customIconKey)) return customIconKey;
            if (nodeType == null) return "cog";

            if (nodeType is NodeType typeEnum)
            {
                return GetIconKey(typeEnum);
            }

            return GetIconKey(nodeType.ToString());
        }
    }
}
