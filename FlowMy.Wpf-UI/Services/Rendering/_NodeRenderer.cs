// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace FlowMy.Services.Rendering
{
    public sealed class NodeRenderer : INodeRenderer
    {
        private readonly IWorkflowEditorHostAccessor _hostAccessor;
        private readonly PortRenderer _portRenderer;
        private readonly ConditionalNodeRenderer _conditionalNodeRenderer;
        private readonly AsyncTaskNodeRenderer _asyncTaskNodeRenderer;
        private readonly ScreenPositionNodeRenderer _screenPositionNodeRenderer;
        private readonly ScreenCaptureNodeRenderer _screenCaptureNodeRenderer;
        private readonly TextScanNodeRenderer _textScanNodeRenderer;
        private readonly LoopNodeRenderer _loopNodeRenderer;
        private readonly InputNodeRenderer _inputNodeRenderer;
        private readonly DelayNodeRenderer _delayNodeRenderer;
        private readonly KeyPressEventNodeRenderer _keyPressEventNodeRenderer;
        private readonly HotkeyPressEventNodeRenderer _hotkeyPressEventNodeRenderer;
        private readonly MouseEventNodeRenderer _mouseEventNodeRenderer;
        private readonly StringSplitNodeRenderer _stringSplitNodeRenderer;
        private readonly ListOutNodeRenderer _listOutNodeRenderer;
        private readonly OutputNodeRenderer _outputNodeRenderer;
        private readonly NotificationNodeRenderer _notificationNodeRenderer;
        private readonly HttpRequestNodeRenderer _httpRequestNodeRenderer;
        private readonly AssignDataNodeRenderer _assignDataNodeRenderer;
        private readonly MediaGalleryNodeRenderer _mediaGalleryNodeRenderer;
        private readonly ImageProcessingNodeRenderer _imageProcessingNodeRenderer;
        private readonly VideoProcessingNodeRenderer _videoProcessingNodeRenderer;
        private readonly DataFetcherNodeRenderer _dataFetcherNodeRenderer;
        private readonly KeyValueBridgeNodeRenderer _keyValueBridgeNodeRenderer;
        private readonly WebNodeRenderer _webNodeRenderer;
        private readonly CodeNodeRenderer _codeNodeRenderer;
        private readonly FolderNodeRenderer _folderNodeRenderer;
        private readonly FileDownloadNodeRenderer _fileDownloadNodeRenderer;
        private readonly FolderFilePathsNodeRenderer _folderFilePathsNodeRenderer;
        private readonly HtmlUiNodeRenderer _htmlUiNodeRenderer;
        private readonly StorageNodeRenderer _storageNodeRenderer;
        private readonly CallbackNodeRenderer _callbackNodeRenderer;
        private readonly FlowOverwriteNodeRenderer _flowOverwriteNodeRenderer;
        private readonly BodyContainerNodeRenderer _bodyContainerNodeRenderer;
        private readonly GitSourceNodeRenderer _gitSourceNodeRenderer;
        private readonly MacroRecorderNodeRenderer _macroRecorderNodeRenderer;
        private readonly BorderHighlightNodeRenderer _borderHighlightNodeRenderer;
        private readonly ActionCanVasNodeRenderer _actionCanVasNodeRenderer;
        private readonly ShowInputMsgNodeRenderer _showInputMsgNodeRenderer;
        private readonly VideoEditorNodeRenderer _videoEditorNodeRenderer;
        private readonly EmbedApplicationNodeRenderer _embedApplicationNodeRenderer;
        private readonly DynamicUiNodeRenderer _dynamicUiNodeRenderer;

        // Dispatch map: node concrete type → renderer
        // Dùng cho các node chỉ cần delegate thuần túy (không có inline logic đặc biệt).
        private Dictionary<Type, INodeRenderer> _rendererMap = null!;

        private IWorkflowEditorHost _host => _hostAccessor.GetRequiredHost();

        public NodeRenderer(
            IWorkflowEditorHostAccessor hostAccessor,
            PortRenderer portRenderer,
            ConditionalNodeRenderer conditionalNodeRenderer,
            AsyncTaskNodeRenderer asyncTaskNodeRenderer,
            ScreenPositionNodeRenderer screenPositionNodeRenderer,
            ScreenCaptureNodeRenderer screenCaptureNodeRenderer,
            TextScanNodeRenderer textScanNodeRenderer,
            LoopNodeRenderer loopNodeRenderer,
            InputNodeRenderer inputNodeRenderer,
            DelayNodeRenderer delayNodeRenderer,
            KeyPressEventNodeRenderer keyPressEventNodeRenderer,
            HotkeyPressEventNodeRenderer hotkeyPressEventNodeRenderer,
            MouseEventNodeRenderer mouseEventNodeRenderer,
            StringSplitNodeRenderer stringSplitNodeRenderer,
            ListOutNodeRenderer listOutNodeRenderer,
            OutputNodeRenderer outputNodeRenderer,
            NotificationNodeRenderer notificationNodeRenderer,
            HttpRequestNodeRenderer httpRequestNodeRenderer,
            AssignDataNodeRenderer assignDataNodeRenderer,
            MediaGalleryNodeRenderer mediaGalleryNodeRenderer,
            ImageProcessingNodeRenderer imageProcessingNodeRenderer,
            VideoProcessingNodeRenderer videoProcessingNodeRenderer,
            DataFetcherNodeRenderer dataFetcherNodeRenderer,
            KeyValueBridgeNodeRenderer keyValueBridgeNodeRenderer,
            WebNodeRenderer webNodeRenderer,
            CodeNodeRenderer codeNodeRenderer,
            FolderNodeRenderer folderNodeRenderer,
            FileDownloadNodeRenderer fileDownloadNodeRenderer,
            FolderFilePathsNodeRenderer folderFilePathsNodeRenderer,
            HtmlUiNodeRenderer htmlUiNodeRenderer,
            StorageNodeRenderer storageNodeRenderer,
            CallbackNodeRenderer callbackNodeRenderer,
            FlowOverwriteNodeRenderer flowOverwriteNodeRenderer,
            BodyContainerNodeRenderer bodyContainerNodeRenderer,
            GitSourceNodeRenderer gitSourceNodeRenderer,
            MacroRecorderNodeRenderer macroRecorderNodeRenderer,
            BorderHighlightNodeRenderer borderHighlightNodeRenderer,
            ActionCanVasNodeRenderer actionCanVasNodeRenderer,
            ShowInputMsgNodeRenderer showInputMsgNodeRenderer,
            VideoEditorNodeRenderer videoEditorNodeRenderer,
            EmbedApplicationNodeRenderer embedApplicationNodeRenderer,
            DynamicUiNodeRenderer dynamicUiNodeRenderer
            )
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _conditionalNodeRenderer = conditionalNodeRenderer ?? throw new ArgumentNullException(nameof(conditionalNodeRenderer));
            _asyncTaskNodeRenderer = asyncTaskNodeRenderer ?? throw new ArgumentNullException(nameof(asyncTaskNodeRenderer));
            _screenPositionNodeRenderer = screenPositionNodeRenderer ?? throw new ArgumentNullException(nameof(screenPositionNodeRenderer));
            _screenCaptureNodeRenderer = screenCaptureNodeRenderer ?? throw new ArgumentNullException(nameof(screenCaptureNodeRenderer));
            _textScanNodeRenderer = textScanNodeRenderer ?? throw new ArgumentNullException(nameof(textScanNodeRenderer));
            _loopNodeRenderer = loopNodeRenderer ?? throw new ArgumentNullException(nameof(loopNodeRenderer));
            _inputNodeRenderer = inputNodeRenderer ?? throw new ArgumentNullException(nameof(inputNodeRenderer));
            _delayNodeRenderer = delayNodeRenderer ?? throw new ArgumentNullException(nameof(delayNodeRenderer));
            _keyPressEventNodeRenderer = keyPressEventNodeRenderer ?? throw new ArgumentNullException(nameof(keyPressEventNodeRenderer));
            _hotkeyPressEventNodeRenderer = hotkeyPressEventNodeRenderer ?? throw new ArgumentNullException(nameof(hotkeyPressEventNodeRenderer));
            _mouseEventNodeRenderer = mouseEventNodeRenderer ?? throw new ArgumentNullException(nameof(mouseEventNodeRenderer));
            _stringSplitNodeRenderer = stringSplitNodeRenderer ?? throw new ArgumentNullException(nameof(stringSplitNodeRenderer));
            _listOutNodeRenderer = listOutNodeRenderer ?? throw new ArgumentNullException(nameof(listOutNodeRenderer));
            _outputNodeRenderer = outputNodeRenderer ?? throw new ArgumentNullException(nameof(outputNodeRenderer));
            _notificationNodeRenderer = notificationNodeRenderer ?? throw new ArgumentNullException(nameof(notificationNodeRenderer));
            _httpRequestNodeRenderer = httpRequestNodeRenderer ?? throw new ArgumentNullException(nameof(httpRequestNodeRenderer));
            _assignDataNodeRenderer = assignDataNodeRenderer ?? throw new ArgumentNullException(nameof(assignDataNodeRenderer));
            _mediaGalleryNodeRenderer = mediaGalleryNodeRenderer ?? throw new ArgumentNullException(nameof(mediaGalleryNodeRenderer));
            _imageProcessingNodeRenderer = imageProcessingNodeRenderer ?? throw new ArgumentNullException(nameof(imageProcessingNodeRenderer));
            _videoProcessingNodeRenderer = videoProcessingNodeRenderer ?? throw new ArgumentNullException(nameof(videoProcessingNodeRenderer));
            _dataFetcherNodeRenderer = dataFetcherNodeRenderer ?? throw new ArgumentNullException(nameof(dataFetcherNodeRenderer));
            _keyValueBridgeNodeRenderer = keyValueBridgeNodeRenderer ?? throw new ArgumentNullException(nameof(keyValueBridgeNodeRenderer));
            _webNodeRenderer = webNodeRenderer ?? throw new ArgumentNullException(nameof(webNodeRenderer));
            _codeNodeRenderer = codeNodeRenderer ?? throw new ArgumentNullException(nameof(codeNodeRenderer));
            _folderNodeRenderer = folderNodeRenderer ?? throw new ArgumentNullException(nameof(folderNodeRenderer));
            _fileDownloadNodeRenderer = fileDownloadNodeRenderer ?? throw new ArgumentNullException(nameof(fileDownloadNodeRenderer));
            _folderFilePathsNodeRenderer = folderFilePathsNodeRenderer ?? throw new ArgumentNullException(nameof(folderFilePathsNodeRenderer));
            _htmlUiNodeRenderer = htmlUiNodeRenderer ?? throw new ArgumentNullException(nameof(htmlUiNodeRenderer));
            _storageNodeRenderer = storageNodeRenderer ?? throw new ArgumentNullException(nameof(storageNodeRenderer));
            _callbackNodeRenderer = callbackNodeRenderer ?? throw new ArgumentNullException(nameof(callbackNodeRenderer));
            _flowOverwriteNodeRenderer = flowOverwriteNodeRenderer ?? throw new ArgumentNullException(nameof(flowOverwriteNodeRenderer));
            _bodyContainerNodeRenderer = bodyContainerNodeRenderer ?? throw new ArgumentNullException(nameof(bodyContainerNodeRenderer));
            _gitSourceNodeRenderer = gitSourceNodeRenderer ?? throw new ArgumentNullException(nameof(gitSourceNodeRenderer));
            _macroRecorderNodeRenderer = macroRecorderNodeRenderer ?? throw new ArgumentNullException(nameof(macroRecorderNodeRenderer));
            _borderHighlightNodeRenderer = borderHighlightNodeRenderer ?? throw new ArgumentNullException(nameof(borderHighlightNodeRenderer));
            _actionCanVasNodeRenderer = actionCanVasNodeRenderer ?? throw new ArgumentNullException(nameof(actionCanVasNodeRenderer));
            _showInputMsgNodeRenderer = showInputMsgNodeRenderer ?? throw new ArgumentNullException(nameof(showInputMsgNodeRenderer));
            _videoEditorNodeRenderer = videoEditorNodeRenderer ?? throw new ArgumentNullException(nameof(videoEditorNodeRenderer));
            _embedApplicationNodeRenderer = embedApplicationNodeRenderer ?? throw new ArgumentNullException(nameof(embedApplicationNodeRenderer));
            _dynamicUiNodeRenderer = dynamicUiNodeRenderer ?? throw new ArgumentNullException(nameof(dynamicUiNodeRenderer));

            BuildRendererMap();
        }

        /// <summary>
        /// Xây dựng dispatch map: concrete node type → renderer.
        /// Chỉ đăng ký các node có thể delegate hoàn toàn (không cần inline logic).
        /// Các node đặc biệt (Start/End, BreakNode, ContinueNode, Conditional, LoopBody, AsyncTaskBody,
        /// ScreenPositionPicker, AsyncTaskDispatchCollect) vẫn xử lý thủ công trong từng method.
        /// </summary>
        private void BuildRendererMap()
        {
            _rendererMap = new Dictionary<Type, INodeRenderer>
            {
                [typeof(AsyncTaskNode)]         = _asyncTaskNodeRenderer,
                [typeof(LoopNode)]              = _loopNodeRenderer,
                [typeof(MouseEventNode)]        = _mouseEventNodeRenderer,
                [typeof(StringSplitNode)]       = _stringSplitNodeRenderer,
                [typeof(ListOutNode)]           = _listOutNodeRenderer,
                [typeof(AssignDataNode)]        = _assignDataNodeRenderer,
                [typeof(MediaGalleryNode)]      = _mediaGalleryNodeRenderer,
                [typeof(ImageProcessingNode)]   = _imageProcessingNodeRenderer,
                [typeof(VideoProcessingNode)]   = _videoProcessingNodeRenderer,
                [typeof(DataFetcherNode)]       = _dataFetcherNodeRenderer,
                [typeof(KeyValueBridgeNode)]    = _keyValueBridgeNodeRenderer,
                [typeof(WebNode)]               = _webNodeRenderer,
                [typeof(CodeNode)]              = _codeNodeRenderer,
                [typeof(HtmlUiNode)]            = _htmlUiNodeRenderer,
                [typeof(FolderNode)]            = _folderNodeRenderer,
                [typeof(FileDownloadNode)]      = _fileDownloadNodeRenderer,
                [typeof(FolderFilePathsNode)]   = _folderFilePathsNodeRenderer,
                [typeof(StorageNode)]           = _storageNodeRenderer,
                [typeof(CallbackNode)]          = _callbackNodeRenderer,
                [typeof(FlowOverwriteNode)]     = _flowOverwriteNodeRenderer,
                [typeof(BodyContainerNode)]     = _bodyContainerNodeRenderer,
                [typeof(OutputNode)]            = _outputNodeRenderer,
                [typeof(NotificationNode)]      = _notificationNodeRenderer,
                [typeof(HttpRequestNode)]       = _httpRequestNodeRenderer,
                [typeof(ScreenCaptureNode)]     = _screenCaptureNodeRenderer,
                [typeof(TextScanNode)]          = _textScanNodeRenderer,
                [typeof(InputNode)]             = _inputNodeRenderer,
                [typeof(DelayNode)]             = _delayNodeRenderer,
                [typeof(KeyPressEventNode)]     = _keyPressEventNodeRenderer,
                [typeof(HotkeyPressEventNode)]  = _hotkeyPressEventNodeRenderer,
                [typeof(GitSourceNode)]         = _gitSourceNodeRenderer,
                [typeof(MacroRecorderNode)]     = _macroRecorderNodeRenderer,
                [typeof(BorderHighlightNode)]   = _borderHighlightNodeRenderer,
                [typeof(FlowMy.Models.Nodes.ActionCanVasNode)] = _actionCanVasNodeRenderer,
                [typeof(FlowMy.Models.Nodes.ShowInputMsgNode)] = _showInputMsgNodeRenderer,
                [typeof(FlowMy.Models.Nodes.VideoEditorNode)] = _videoEditorNodeRenderer,
                [typeof(EmbedApplicationNode)]  = _embedApplicationNodeRenderer,
                [typeof(FlowMy.Models.Nodes.DynamicUiNode)] = _dynamicUiNodeRenderer,
            };
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (canvas == null || node == null) return;

            // ✅ Gỡ sạch UI cũ của node (nếu đã từng được render trên canvas) để tránh dính ghost border khi re-render/thay đổi property
            if (node.Border != null && canvas.Children.Contains(node.Border))
            {
                RemoveNode(node, canvas);
            }

            // ── Các node có inline logic đặc biệt — xử lý trước dictionary ──

            if (node is ScreenPositionPickerNode screenNode)
            {
                _screenPositionNodeRenderer.RenderNode(screenNode, canvas);
                return;
            }

            if (node is AsyncTaskDispatchCollectNode collectNode)
            {
                node.Border = FlowMy.Views.NodeControls.AsyncTaskDispatchCollectNodeControl.CreateBorder(
                    collectNode, _host.OwnerWindow, _host);
                NodeChrome.Apply(node.Border, node, _host);
                AttachNodeBorderHandlers(node.Border, node);
                node.Border.ContextMenu = null;
                Canvas.SetLeft(node.Border, node.X);
                Canvas.SetTop(node.Border, node.Y);
                canvas.Children.Add(node.Border);
                _host.ZIndexManager.InitializeNodeZIndex(node, node.Border);
                RenderNodePorts(node, canvas);
                return;
            }

            if (node.IsConditionalNode)
            {
                _conditionalNodeRenderer.RenderConditionalNode(node);
                return;
            }

            if (node is BreakNode breakNode)
            {
                node.Border = FlowMy.Views.NodeControls.BreakNodeControl.CreateBorder(breakNode, null, _host);
                NodeChrome.Apply(node.Border, node, _host);
                AttachNodeBorderHandlers(node.Border, node);
                node.Border.ContextMenu = _host.CreateNodeContextMenu(node);
                Canvas.SetLeft(node.Border, node.X);
                Canvas.SetTop(node.Border, node.Y);
                canvas.Children.Add(node.Border);
                _host.ZIndexManager.InitializeNodeZIndex(node, node.Border);
                RenderNodePorts(node, canvas);
                return;
            }

            if (node is ContinueNode continueNode)
            {
                node.Border = FlowMy.Views.NodeControls.ContinueNodeControl.CreateBorder(continueNode, null, _host);
                NodeChrome.Apply(node.Border, node, _host);
                AttachNodeBorderHandlers(node.Border, node);
                node.Border.ContextMenu = _host.CreateNodeContextMenu(node);
                Canvas.SetLeft(node.Border, node.X);
                Canvas.SetTop(node.Border, node.Y);
                canvas.Children.Add(node.Border);
                _host.ZIndexManager.InitializeNodeZIndex(node, node.Border);
                RenderNodePorts(node, canvas);
                return;
            }

            if (node.Type == NodeType.Start)
            {
                node.Border = FlowMy.Views.NodeControls.StartNodeControl.CreateBorder(node, _host.OwnerWindow, _host);
                NodeChrome.Apply(node.Border, node, _host);
                AttachNodeBorderHandlers(node.Border, node);
                node.Border.ContextMenu = _host.CreateNodeContextMenu(node);
                Canvas.SetLeft(node.Border, node.X);
                Canvas.SetTop(node.Border, node.Y);
                canvas.Children.Add(node.Border);
                _host.ZIndexManager.InitializeNodeZIndex(node, node.Border);
                RenderNodePorts(node, canvas);
                return;
            }

            if (node.Type == NodeType.End)
            {
                node.Border = FlowMy.Views.NodeControls.EndNodeControl.CreateBorder(node, _host.OwnerWindow, _host);
                NodeChrome.Apply(node.Border, node, _host);
                AttachNodeBorderHandlers(node.Border, node);
                node.Border.ContextMenu = _host.CreateNodeContextMenu(node);
                Canvas.SetLeft(node.Border, node.X);
                Canvas.SetTop(node.Border, node.Y);
                canvas.Children.Add(node.Border);
                _host.ZIndexManager.InitializeNodeZIndex(node, node.Border);
                RenderNodePorts(node, canvas);
                return;
            }

            // ── Dictionary dispatch cho các node delegate thuần túy ──
            if (_rendererMap.TryGetValue(node.GetType(), out var renderer))
            {
                renderer.RenderNode(node, canvas);
                return;
            }

            // ── Fallback: generic border ──
            node.Border = CreateNodeBorder(node);
            NodeChrome.Apply(node.Border, node, _host);
            AttachNodeBorderHandlers(node.Border, node);
            node.Border.ContextMenu = _host.CreateNodeContextMenu(node);
            Canvas.SetLeft(node.Border, node.X);
            Canvas.SetTop(node.Border, node.Y);
            canvas.Children.Add(node.Border);
            _host.ZIndexManager.InitializeNodeZIndex(node, node.Border);
            RenderNodePorts(node, canvas);
        }

        public void UpdateNodePosition(WorkflowNode node, double x, double y)
        {
            // ✅ Update Start/End node position
            if (node.Type == NodeType.Start || node.Type == NodeType.End)
            {
                node.X = x;
                node.Y = y;

                if (node.Border != null)
                {
                    Canvas.SetLeft(node.Border, x);
                    Canvas.SetTop(node.Border, y);
                }

                UpdateFloatingTitlePosition(node, x, y);
                UpdateSimpleNodePorts(node);
                return;
            }

            // ✅ Delegate ScreenPositionPickerNode to its own renderer
            if (node is ScreenPositionPickerNode screenPosNode)
            {
                _screenPositionNodeRenderer.UpdateNodePosition(screenPosNode, x, y);
                return;
            }

            // ── Dictionary dispatch cho các node delegate thuần túy ──
            if (_rendererMap.TryGetValue(node.GetType(), out var posRenderer))
            {
                posRenderer.UpdateNodePosition(node, x, y);
                return;
            }

            // ✅ Delegate ConditionalNode to its own renderer
            if (node.IsConditionalNode)
            {
                _conditionalNodeRenderer.UpdateNodePosition(node, x, y);
                return;
            }

            // LoopBodyNode: border container + 3 port canvas — không dùng UpdatePortsPositionOnSide chuẩn
            if (node is LoopBodyNode loopBodyForPos)
            {
                loopBodyForPos.X = x;
                loopBodyForPos.Y = y;
                if (loopBodyForPos.Border != null)
                {
                    var transform = loopBodyForPos.Border.RenderTransform as TranslateTransform;
                    if (transform == null || (transform.X == 0 && transform.Y == 0))
                    {
                        Canvas.SetLeft(loopBodyForPos.Border, x);
                        Canvas.SetTop(loopBodyForPos.Border, y);
                    }
                }
                if (loopBodyForPos.ParentLoopNode != null)
                {
                    _loopNodeRenderer.UpdateLoopBodyPortsPosition(loopBodyForPos.ParentLoopNode);
                    _host.SyncAllPortsZIndex(loopBodyForPos.ParentLoopNode);
                }
                return;
            }

            if (node is AsyncTaskBodyNode asyncTaskBodyForPos && asyncTaskBodyForPos.ParentAsyncTaskNode != null)
            {
                asyncTaskBodyForPos.X = x;
                asyncTaskBodyForPos.Y = y;
                if (asyncTaskBodyForPos.Border != null)
                {
                    var transform = asyncTaskBodyForPos.Border.RenderTransform as TranslateTransform;
                    if (transform == null || (transform.X == 0 && transform.Y == 0))
                    {
                        Canvas.SetLeft(asyncTaskBodyForPos.Border, x);
                        Canvas.SetTop(asyncTaskBodyForPos.Border, y);
                    }
                }
                _asyncTaskNodeRenderer.UpdateAsyncTaskBodyPortsPosition(asyncTaskBodyForPos.ParentAsyncTaskNode);
                _host.SyncAllPortsZIndex(asyncTaskBodyForPos.ParentAsyncTaskNode);
                return;
            }

            // Default implementation cho các node khác
            node.X = x;
            node.Y = y;

            if (node.Border != null)
            {
                // TỐI ƯU GPU: Khi đang drag, không update Canvas position mỗi frame
                // RenderTransform đã xử lý việc di chuyển mượt mà
                // Chỉ update Canvas position khi không drag hoặc khi cần sync
                var transform = node.Border.RenderTransform as TranslateTransform;
                if (transform == null || (transform.X == 0 && transform.Y == 0))
                {
                    // Không đang drag - update Canvas position bình thường
                    Canvas.SetLeft(node.Border, x);
                    Canvas.SetTop(node.Border, y);
                }
                else
                {
                    // Đang drag - chỉ update base position, transform đã xử lý offset
                    // Không cần update Canvas position mỗi frame
                }
                
                // Không invalidate khi đang drag - RenderTransform đã xử lý
                // Chỉ invalidate khi không drag để đảm bảo cache được update
                if (GpuDetectionHelper.IsGpuAvailable && (transform == null || (transform.X == 0 && transform.Y == 0)))
                {
                    // Không đang drag - có thể invalidate nếu cần
                    // Nhưng không cần thiết vì đã có cache
                }
            }

            if (node.Type == NodeType.Start || node.Type == NodeType.End)
            {
                NodeAppearanceHelper.SyncStartEndPortVisibility(node);
            }

            // Keep ports in sync with node position (required for Phase 5 layout algorithms).
            if (node.IsConditionalNode)
            {
                _conditionalNodeRenderer.RenderConditionalNodePorts(node);
            }
            else
            {
                // Ensure port UI exists and positions are recalculated on each side.
                foreach (var port in node.Ports.Where(p => p.IsVisible))
                {
                    if (port.PortUI == null)
                    {
                        var portColor = port.IsInput
                            ? (GetColorFromTheme("CoralBrush") ?? Colors.Orange)
                            : (GetColorFromTheme("AtlassianBrush") ?? Colors.Cyan);
                        port.PortUI = _portRenderer.CreatePort(portColor);
                    }
                }

                var positions = node.Ports
                    .Where(p => p.IsVisible)
                    .Select(p => p.Position)
                    .Distinct();

                foreach (var position in positions)
                {
                    _portRenderer.UpdatePortsPositionOnSide(node, position);
                }

                foreach (var port in node.Ports.Where(p => p.IsVisible && p.PortUI != null))
                {
                    _portRenderer.EnsurePortAddedToCanvas(port);
                    _host.ZIndexManager.SetPortZIndex(node, port.PortUI);
                }
            }

            _host.SyncAllPortsZIndex(node);
        }

        public void RemoveNode(WorkflowNode node, Canvas canvas)
        {
            // Start/End: inline cleanup (title + border + ports)
            if (node.Type == NodeType.Start || node.Type == NodeType.End)
            {
                RemoveSimpleNode(node, canvas);
                return;
            }

            // ✅ Delegate ScreenPositionPickerNode to its own renderer
            if (node is ScreenPositionPickerNode screenPosNode)
            {
                _screenPositionNodeRenderer.RemoveNode(screenPosNode, canvas);
                return;
            }

            // LoopNode/AsyncTaskNode: có cleanup phức tạp (body containers, ports đặc biệt)
            if (node is LoopNode loopNode)
            {
                _loopNodeRenderer.RemoveNode(loopNode, canvas);
                return;
            }

            if (node is AsyncTaskNode asyncTaskRemove)
            {
                _asyncTaskNodeRenderer.RemoveNode(asyncTaskRemove, canvas);
                return;
            }

            // ── Dictionary dispatch cho các node delegate thuần túy ──
            if (_rendererMap.TryGetValue(node.GetType(), out var removeRenderer))
            {
                removeRenderer.RemoveNode(node, canvas);
                return;
            }

            if (node.IsConditionalNode)
            {
                _conditionalNodeRenderer.RemoveNode(node, canvas);
                return;
            }

            // Fallback: cleanup cơ bản
            if (node.Border != null && canvas.Children.Contains(node.Border))
                canvas.Children.Remove(node.Border);

            foreach (var port in node.Ports)
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                    canvas.Children.Remove(port.PortUI);
            }
        }

        public void RemoveAllNodeVisuals(Canvas canvas)
        {
            // ── Single-pass: thu thập tất cả elements cần xóa trong 1 lần duyệt ──
            var toRemove = new List<UIElement>(canvas.Children.Count);

            foreach (UIElement child in canvas.Children)
            {
                switch (child)
                {
                    case Border b:
                        if (b.Tag is WorkflowNode) { toRemove.Add(b); break; }
                        if (b.Tag is NodePort) { toRemove.Add(b); break; }
                        if (b.Tag is string s && s.StartsWith("AddSatellite:", StringComparison.Ordinal))
                        { toRemove.Add(b); break; }
                        // Port rectangle wrapper: Border chứa Rectangle child
                        if (b.Child is Rectangle rect)
                        {
                            if (rect.Tag is Size) { toRemove.Add(b); break; }
                            if ((rect.Width == 12 && rect.Height == 25) ||
                                (rect.Width == 10 && rect.Height == 18) ||
                                (rect.Width == 14 && rect.Height == 27) ||
                                (rect.Width == 12 && rect.Height == 20))
                            { toRemove.Add(b); break; }
                        }
                        break;

                    case Ellipse e:
                        if (e.Tag is NodePort || (e.Width == 18 && e.Height == 18))
                            toRemove.Add(e);
                        break;

                    case System.Windows.Controls.TextBlock tb:
                        var zIndex = Panel.GetZIndex(tb);
                        if (zIndex == 20000 || zIndex > 10000)
                            toRemove.Add(tb);
                        break;
                }
            }

            // Batch remove
            foreach (var item in toRemove)
            {
                canvas.Children.Remove(item);
            }

            // Dọn visual phụ của Conditional Diamond mode
            if (_host.ViewModel != null)
            {
                foreach (var conditionalNode in _host.ViewModel.Nodes.Where(n => n.IsConditionalNode))
                {
                    foreach (var branch in conditionalNode.ConditionalBranches)
                    {
                        if (branch.SatelliteBorder != null && canvas.Children.Contains(branch.SatelliteBorder))
                            canvas.Children.Remove(branch.SatelliteBorder);
                        if (branch.SatelliteLine != null && canvas.Children.Contains(branch.SatelliteLine))
                            canvas.Children.Remove(branch.SatelliteLine);
                        if (branch.SatelliteDeleteButton != null && canvas.Children.Contains(branch.SatelliteDeleteButton))
                            canvas.Children.Remove(branch.SatelliteDeleteButton);
                        if (branch.SatelliteInputVisual != null && canvas.Children.Contains(branch.SatelliteInputVisual))
                            canvas.Children.Remove(branch.SatelliteInputVisual);
                        if (branch.DiamondOutputVisual != null && canvas.Children.Contains(branch.DiamondOutputVisual))
                            canvas.Children.Remove(branch.DiamondOutputVisual);
                        if (branch.SatelliteArrowHead != null && canvas.Children.Contains(branch.SatelliteArrowHead))
                            canvas.Children.Remove(branch.SatelliteArrowHead);

                        branch.SatelliteBorder = null;
                        branch.SatelliteLine = null;
                        branch.SatelliteDeleteButton = null;
                        branch.SatelliteInputVisual = null;
                        branch.DiamondOutputVisual = null;
                        branch.SatelliteArrowHead = null;
                    }
                }

                // Xóa port UI còn sót lại + clear TitleTextBlockUI references
                foreach (var node in _host.ViewModel.Nodes)
                {
                    foreach (var port in node.Ports)
                    {
                        if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                            canvas.Children.Remove(port.PortUI);
                    }
                    if (node.TitleTextBlockUI != null)
                    {
                        if (canvas.Children.Contains(node.TitleTextBlockUI))
                            canvas.Children.Remove(node.TitleTextBlockUI);
                        node.TitleTextBlockUI = null;
                    }
                }
            }
        }

        /// <summary>
        /// Quét dọn các UIElement mồ côi (ghost borders/ports) trên canvas mà không thuộc bất kỳ node active nào trong ViewModel
        /// hoặc là bản sao UI border cũ đã bị thay thế (tránh dính ghost node trên canvas).
        /// </summary>
        public static void CleanOrphanedGhostNodes(Canvas canvas, System.Collections.Generic.IEnumerable<WorkflowNode>? activeNodes)
        {
            if (canvas == null) return;

            var activeSet = activeNodes != null
                ? new System.Collections.Generic.HashSet<WorkflowNode>(activeNodes)
                : new System.Collections.Generic.HashSet<WorkflowNode>();

            var toRemove = new System.Collections.Generic.List<UIElement>();
            foreach (UIElement child in canvas.Children)
            {
                if (child is FrameworkElement fe && fe.Tag is WorkflowNode taggedNode)
                {
                    // Xóa nếu node này không còn trong danh sách activeNodes của workflow,
                    // hoặc nếu border của node đã được gán sang instance UI mới (element này là ghost border cũ).
                    if (!activeSet.Contains(taggedNode) || (taggedNode.Border != null && taggedNode.Border != fe))
                    {
                        toRemove.Add(fe);
                    }
                }
            }

            foreach (var elem in toRemove)
            {
                canvas.Children.Remove(elem);
            }
        }

        public void RenderAllNodes()
        {
            var viewModel = _host.ViewModel;
            if (viewModel == null) return;

            _host.WorkflowCanvas.Children.Clear();
            foreach (var node in viewModel.Nodes)
            {
                RenderNode(node, _host.WorkflowCanvas);
            }
        }

        public void RenderNodePorts(WorkflowNode node, Canvas canvas)
        {
            if (node.Type == NodeType.Start || node.Type == NodeType.End)
            {
                NodeAppearanceHelper.SyncStartEndPortVisibility(node);
            }

            foreach (var port in node.Ports)
            {
                if (!port.IsVisible) continue;

                if (port.PortUI == null)
                {
                    var portColor = port.IsInput
                        ? (GetColorFromTheme("CoralBrush") ?? Colors.Orange)
                        : (GetColorFromTheme("AtlassianBrush") ?? Colors.Cyan);
                    port.PortUI = _portRenderer.CreatePort(portColor);
                }

                _portRenderer.UpdatePortsPositionOnSide(node, port.Position);
                _portRenderer.EnsurePortAddedToCanvas(port);
                _host.ZIndexManager.SetPortZIndex(node, port.PortUI);
            }

            _host.SyncAllPortsZIndex(node);
        }

        private Border CreateNodeBorder(WorkflowNode node)
        {
            var baseColor = GetColorFromBrush(node.NodeBrush);
            var style = NodeAppearanceHelper.GetStyle(_host.NodeAppearanceMode, baseColor);

            var border = new Border
            {
                Width = 150,
                Height = 80,
                Background = style.Background,
                BorderBrush = style.BorderBrush,
                BorderThickness = style.BorderThickness,
                CornerRadius = style.CornerRadius,
                Cursor = Cursors.Hand,
                Effect = style.Effect,
                Tag = node
            };

            Brush textColor = style.TextColor;
            if (string.Equals(_host.NodeAppearanceMode, "Solid", System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(node.ColorKey))
            {
                var textBrush = Application.Current.TryFindResource($"TextOn{node.ColorKey}Brush") as Brush;
                if (textBrush != null) textColor = textBrush;
            }

            var textBlock = new TextBlock
            {
                Text = node.Title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = textColor,
                Effect = style.TextEffect
            };

            border.Child = textBlock;
            return border;
        }

        private void AttachNodeBorderHandlers(Border border, WorkflowNode node)
        {
            border.MouseDown += _host.NodeMouseDown;
            border.MouseMove += _host.NodeMouseMove;
            border.MouseUp += _host.NodeMouseUp;
            border.MouseEnter += _host.NodeBorderMouseEnter;
            border.MouseLeave += _host.NodeBorderMouseLeave;
        }

        private Brush GetTextColorFromBrush(Brush brush)
        {
            Color color = GetColorFromBrush(brush);
            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            return luminance > 0.5 ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(Colors.White);
        }

        private static Color GetColorFromBrush(Brush brush)
        {
            if (brush is SolidColorBrush solid) return solid.Color;
            if (brush is LinearGradientBrush linear && linear.GradientStops.Count > 0) return linear.GradientStops[0].Color;
            if (brush is RadialGradientBrush radial && radial.GradientStops.Count > 0) return radial.GradientStops[0].Color;
            return Colors.Gray;
        }

        private static Color? GetColorFromTheme(string resourceKey)
        {
            try
            {
                var brush = Application.Current.TryFindResource(resourceKey) as SolidColorBrush;
                return brush?.Color;
            }
            catch
            {
                return null;
            }
        }

        private bool IsNodeInsideAnyLoopBody(WorkflowNode node)
        {
            var vm = _host.ViewModel;
            if (vm == null) return false;

            var probe = new Point(node.X + 50, node.Y + 20);
            foreach (var loop in vm.Nodes.OfType<LoopNode>())
            {
                var body = loop.LoopBodyNode;
                var rect = new Rect(body.X, body.Y, body.Width, body.Height);
                if (rect.Contains(probe)) return true;
            }
            return false;
        }

        /// <summary>
        /// Update vị trí floating title TextBlock khi node di chuyển.
        /// Dùng cho Start/End node — title nằm trên canvas, không nằm trong border.
        /// </summary>
        private void UpdateFloatingTitlePosition(WorkflowNode node, double x, double y)
        {
            if (node.TitleTextBlockUI == null || _host.WorkflowCanvas == null) return;

            var title = node.TitleTextBlockUI;
            if (!_host.WorkflowCanvas.Children.Contains(title))
            {
                _host.WorkflowCanvas.Children.Add(title);
                Panel.SetZIndex(title, 20000);
            }

            if (node.Border == null) return;

            if (title.ActualWidth == 0 || title.ActualHeight == 0)
            {
                title.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                title.Arrange(new Rect(title.DesiredSize));
            }

            var borderWidth = node.Border.ActualWidth > 0 ? node.Border.ActualWidth : node.Border.Width;
            var titleWidth  = title.ActualWidth  > 0 ? title.ActualWidth  : title.DesiredSize.Width;
            var titleHeight = title.ActualHeight > 0 ? title.ActualHeight : title.DesiredSize.Height;

            Canvas.SetLeft(title, x + (borderWidth / 2) - (titleWidth / 2));
            Canvas.SetTop(title,  y - titleHeight - 4);
        }

        /// <summary>
        /// Update ports cho node đơn giản (Start/End) — không có logic đặc biệt.
        /// </summary>
        private void UpdateSimpleNodePorts(WorkflowNode node)
        {
            RenderNodePorts(node, _host.WorkflowCanvas);
        }

        /// <summary>
        /// Xóa node đơn giản (Start/End) khỏi canvas — title + border + ports.
        /// </summary>
        private static void RemoveSimpleNode(WorkflowNode node, Canvas canvas)
        {
            if (node.TitleTextBlockUI != null)
            {
                if (canvas.Children.Contains(node.TitleTextBlockUI))
                    canvas.Children.Remove(node.TitleTextBlockUI);
                node.TitleTextBlockUI = null;
            }

            if (node.Border != null && canvas.Children.Contains(node.Border))
                canvas.Children.Remove(node.Border);

            foreach (var port in node.Ports)
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                    canvas.Children.Remove(port.PortUI);
            }
        }

    }
}

