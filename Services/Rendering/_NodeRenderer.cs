using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        private IWorkflowEditorHost _host => _hostAccessor.GetRequiredHost();

        public NodeRenderer(
            IWorkflowEditorHostAccessor hostAccessor,
            PortRenderer portRenderer,
            ConditionalNodeRenderer conditionalNodeRenderer,
            AsyncTaskNodeRenderer asyncTaskNodeRenderer,
            ScreenPositionNodeRenderer screenPositionNodeRenderer,
            ScreenCaptureNodeRenderer screenCaptureNodeRenderer,
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
            BodyContainerNodeRenderer bodyContainerNodeRenderer
            )
        {
            _hostAccessor = hostAccessor ?? throw new ArgumentNullException(nameof(hostAccessor));
            _portRenderer = portRenderer ?? throw new ArgumentNullException(nameof(portRenderer));
            _conditionalNodeRenderer = conditionalNodeRenderer ?? throw new ArgumentNullException(nameof(conditionalNodeRenderer));
            _asyncTaskNodeRenderer = asyncTaskNodeRenderer ?? throw new ArgumentNullException(nameof(asyncTaskNodeRenderer));
            _screenPositionNodeRenderer = screenPositionNodeRenderer ?? throw new ArgumentNullException(nameof(screenPositionNodeRenderer));
            _screenCaptureNodeRenderer = screenCaptureNodeRenderer ?? throw new ArgumentNullException(nameof(screenCaptureNodeRenderer));
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
        }

        public void RenderNode(WorkflowNode node, Canvas canvas)
        {
            if (node is ScreenPositionPickerNode screenNode)
            {
                node.Border = _screenPositionNodeRenderer.CreateBorder(screenNode);
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

            // Async task node - chạy nhiều task song song
            if (node is AsyncTaskNode asyncTaskNode)
            {
                _asyncTaskNodeRenderer.RenderNode(asyncTaskNode, canvas);
                return;
            }

            // node lặp
            if (node is LoopNode loopNode)
            {
                _loopNodeRenderer.RenderNode(loopNode, canvas);
                return;
            }

            // Special renderers
            if (node is MouseEventNode mouseNode)
            {
                _mouseEventNodeRenderer.RenderNode(mouseNode, canvas);
                return;
            }

            // StringSplit node với custom UI và dialog riêng
            if (node is StringSplitNode stringSplitNode)
            {
                _stringSplitNodeRenderer.RenderNode(stringSplitNode, canvas);
                return;
            }

            // ListOut node - lọc và đổi tên outputs
            if (node is ListOutNode listOutNode)
            {
                _listOutNodeRenderer.RenderNode(listOutNode, canvas);
                return;
            }

            // AssignData node - gán dữ liệu từ node nguồn sang node đích
            if (node is AssignDataNode assignDataNode)
            {
                _assignDataNodeRenderer.RenderNode(assignDataNode, canvas);
                return;
            }

            // MediaGallery node - gallery ảnh/video, co dãn, checkbox, tải ảnh/video
            if (node is MediaGalleryNode mediaGalleryNode)
            {
                _mediaGalleryNodeRenderer.RenderNode(mediaGalleryNode, canvas);
                return;
            }

            // ImageProcessing node - preview ảnh (URL/base64), zoom/pan, co dãn
            if (node is ImageProcessingNode imageProcessingNode)
            {
                _imageProcessingNodeRenderer.RenderNode(imageProcessingNode, canvas);
                return;
            }

            // DataFetcher node - lấy output từ node khác theo nodeId + key, hỗ trợ Timer/Realtime
            if (node is DataFetcherNode dataFetcherNode)
            {
                _dataFetcherNodeRenderer.RenderNode(dataFetcherNode, canvas);
                return;
            }

            if (node is KeyValueBridgeNode keyValueBridgeNode)
            {
                _keyValueBridgeNodeRenderer.RenderNode(keyValueBridgeNode, canvas);
                return;
            }

            // Web node - WebView2, output cookie/bearer/access_token, input cookie, intercept/block requests
            if (node is WebNode webNode)
            {
                _webNodeRenderer.RenderNode(webNode, canvas);
                return;
            }

            // Code node - chạy JavaScript với input từ node + key, return object = outputs
            if (node is CodeNode codeNode)
            {
                _codeNodeRenderer.RenderNode(codeNode, canvas);
                return;
            }

            // HTML UI node - hiển thị HTML UI cấu hình bằng 4 tab
            if (node is HtmlUiNode htmlUiNode)
            {
                _htmlUiNodeRenderer.RenderNode(htmlUiNode, canvas);
                return;
            }

            // Folder node - chọn thư mục gốc, path con (key/DateTime), tạo folder, output folder + fullPath
            if (node is FolderNode folderNode)
            {
                _folderNodeRenderer.RenderNode(folderNode, canvas);
                return;
            }

            if (node is FileDownloadNode fileDownloadNode)
            {
                _fileDownloadNodeRenderer.RenderNode(fileDownloadNode, canvas);
                return;
            }

            if (node is FolderFilePathsNode folderFilePathsNode)
            {
                _folderFilePathsNodeRenderer.RenderNode(folderFilePathsNode, canvas);
                return;
            }

            // Storage node - lưu trữ dữ liệu toàn cục, không cần flow bắt buộc
            if (node is StorageNode storageNode)
            {
                _storageNodeRenderer.RenderNode(storageNode, canvas);
                return;
            }

            // AsyncTask dispatch collector node (parallel-safe aggregation)
            if (node is AsyncTaskDispatchCollectNode collectNode)
            {
                node.Border = FlowMy.Views.NodeControls.AsyncTaskDispatchCollectNodeControl.CreateBorder(
                    collectNode,
                    _host.OwnerWindow,
                    _host);
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

            // Callback node - chạy lại workflow từ node đã chọn
            if (node is CallbackNode callbackNode)
            {
                _callbackNodeRenderer.RenderNode(callbackNode, canvas);
                return;
            }

            if (node is FlowOverwriteNode flowOverwriteNode)
            {
                _flowOverwriteNodeRenderer.RenderNode(flowOverwriteNode, canvas);
                return;
            }

            if (node is BodyContainerNode bodyContainerNode)
            {
                _bodyContainerNodeRenderer.RenderNode(bodyContainerNode, canvas);
                return;
            }

            // Output node - tạo output text từ format string
            if (node is OutputNode outputNode)
            {
                _outputNodeRenderer.RenderNode(outputNode, canvas);
                return;
            }

            // Notification node - hiển thị toast thông báo
            if (node is NotificationNode notificationNode)
            {
                _notificationNodeRenderer.RenderNode(notificationNode, canvas);
                return;
            }

            // HttpRequest node - HTTP API calls
            if (node is HttpRequestNode httpRequestNode)
            {
                _httpRequestNodeRenderer.RenderNode(httpRequestNode, canvas);
                return;
            }

            // PHẢI CÓ ĐOẠN NÀY Ở ĐẦU, TRƯỚC TẤT CẢ CODE KHÁC
            if (node is ScreenCaptureNode captureNode)
            {
                _screenCaptureNodeRenderer.RenderNode(captureNode, canvas);
                return;  // QUAN TRỌNG: phải return để không chạy code phía dưới
            }

            if (node.IsConditionalNode)
            {
                _conditionalNodeRenderer.RenderConditionalNode(node);
                return;
            }

            if (node is BreakNode breakNode)
            {
                // ✅ Luôn sử dụng UI hình tròn với icon cho Break node
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
                // ✅ Luôn sử dụng UI hình tròn với icon cho Continue node
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

            // ✅ Start node - hình tròn với icon play và title trên top
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

                // Tạo và thêm title TextBlock
                var titleTextBlock = FlowMy.Views.NodeControls.StartNodeControl.CreateTitleTextBlock(node);
                canvas.Children.Add(titleTextBlock);
                FlowMy.Views.NodeControls.StartNodeControl.UpdateTitlePosition(node, canvas);

                RenderNodePorts(node, canvas);
                return;
            }

            // ✅ End node - hình tròn với icon flag-checkered và title trên top
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

                // Tạo và thêm title TextBlock
                var endTitleTextBlock = FlowMy.Views.NodeControls.EndNodeControl.CreateTitleTextBlock(node);
                canvas.Children.Add(endTitleTextBlock);
                FlowMy.Views.NodeControls.EndNodeControl.UpdateTitlePosition(node, canvas);

                RenderNodePorts(node, canvas);
                return;
            }

            if (node is InputNode inputNode)
            {
                _inputNodeRenderer.RenderNode(inputNode, canvas);
                return;
            }

            if (node is DelayNode delayNode)
            {
                _delayNodeRenderer.RenderNode(delayNode, canvas);
                return;
            }

            if (node is KeyPressEventNode keyNode)
            {
                _keyPressEventNodeRenderer.RenderNode(keyNode, canvas);
                return;
            }

            if (node is HotkeyPressEventNode hotkeyNode)
            {
                _hotkeyPressEventNodeRenderer.RenderNode(hotkeyNode, canvas);
                return;
            }

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
            // ✅ Update Start node title position
            if (node.Type == NodeType.Start)
            {
                node.X = x;
                node.Y = y;

                if (node.Border != null)
                {
                    Canvas.SetLeft(node.Border, x);
                    Canvas.SetTop(node.Border, y);
                }

                // Update title position
                if (node.TitleTextBlockUI != null && _host.WorkflowCanvas != null)
                {
                    FlowMy.Views.NodeControls.StartNodeControl.UpdateTitlePosition(node, _host.WorkflowCanvas);
                }

                // Update ports
                foreach (var port in node.Ports.Where(p => p.IsVisible))
                {
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
                return;
            }

            // ✅ Update End node title position
            if (node.Type == NodeType.End)
            {
                node.X = x;
                node.Y = y;

                if (node.Border != null)
                {
                    Canvas.SetLeft(node.Border, x);
                    Canvas.SetTop(node.Border, y);
                }

                // Update title position
                if (node.TitleTextBlockUI != null && _host.WorkflowCanvas != null)
                {
                    FlowMy.Views.NodeControls.EndNodeControl.UpdateTitlePosition(node, _host.WorkflowCanvas);
                }

                // Update ports
                foreach (var port in node.Ports.Where(p => p.IsVisible))
                {
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
                return;
            }

            // ✅ Delegate cho specialized renderers để update title position
            if (node is InputNode inputNode)
            {
                _inputNodeRenderer.UpdateNodePosition(inputNode, x, y);
                return;
            }

            if (node is DelayNode delayNode)
            {
                _delayNodeRenderer.UpdateNodePosition(delayNode, x, y);
                return;
            }

            if (node is KeyPressEventNode keyNode)
            {
                _keyPressEventNodeRenderer.UpdateNodePosition(keyNode, x, y);
                return;
            }

            if (node is HotkeyPressEventNode hotkeyNode)
            {
                _hotkeyPressEventNodeRenderer.UpdateNodePosition(hotkeyNode, x, y);
                return;
            }

            if (node is MouseEventNode mouseNode)
            {
                _mouseEventNodeRenderer.UpdateNodePosition(mouseNode, x, y);
                return;
            }

            if (node is StringSplitNode stringSplitNode)
            {
                _stringSplitNodeRenderer.UpdateNodePosition(stringSplitNode, x, y);
                return;
            }

            if (node is ListOutNode listOutNode)
            {
                _listOutNodeRenderer.UpdateNodePosition(listOutNode, x, y);
                return;
            }

            if (node is AssignDataNode assignDataNode)
            {
                _assignDataNodeRenderer.UpdateNodePosition(assignDataNode, x, y);
                return;
            }

            if (node is MediaGalleryNode mediaGalleryNode)
            {
                _mediaGalleryNodeRenderer.UpdateNodePosition(mediaGalleryNode, x, y);
                return;
            }

            if (node is ImageProcessingNode imageProcessingNode)
            {
                _imageProcessingNodeRenderer.UpdateNodePosition(imageProcessingNode, x, y);
                return;
            }

            if (node is DataFetcherNode dataFetcherNodeForPos)
            {
                _dataFetcherNodeRenderer.UpdateNodePosition(dataFetcherNodeForPos, x, y);
                return;
            }

            if (node is KeyValueBridgeNode keyValueBridgeForPos)
            {
                _keyValueBridgeNodeRenderer.UpdateNodePosition(keyValueBridgeForPos, x, y);
                return;
            }

            if (node is WebNode webNodeForPosition)
            {
                _webNodeRenderer.UpdateNodePosition(webNodeForPosition, x, y);
                return;
            }

            if (node is HtmlUiNode htmlUiNodeForPosition)
            {
                _htmlUiNodeRenderer.UpdateNodePosition(htmlUiNodeForPosition, x, y);
                return;
            }

            if (node is OutputNode outputNode)
            {
                _outputNodeRenderer.UpdateNodePosition(outputNode, x, y);
                return;
            }

            if (node is NotificationNode notificationNode)
            {
                _notificationNodeRenderer.UpdateNodePosition(notificationNode, x, y);
                return;
            }

            if (node is HttpRequestNode httpRequestNode)
            {
                _httpRequestNodeRenderer.UpdateNodePosition(httpRequestNode, x, y);
                return;
            }

            if (node is CodeNode codeNode)
            {
                _codeNodeRenderer.UpdateNodePosition(codeNode, x, y);
                return;
            }

            if (node is FolderNode folderNode)
            {
                _folderNodeRenderer.UpdateNodePosition(folderNode, x, y);
                return;
            }

            if (node is FileDownloadNode fileDownloadNodeForPos)
            {
                _fileDownloadNodeRenderer.UpdateNodePosition(fileDownloadNodeForPos, x, y);
                return;
            }

            if (node is FolderFilePathsNode folderFilePathsNodeForPos)
            {
                _folderFilePathsNodeRenderer.UpdateNodePosition(folderFilePathsNodeForPos, x, y);
                return;
            }

            if (node is StorageNode storageNode)
            {
                _storageNodeRenderer.UpdateNodePosition(storageNode, x, y);
                return;
            }

            if (node is CallbackNode callbackNode)
            {
                _callbackNodeRenderer.UpdateNodePosition(callbackNode, x, y);
                return;
            }

            if (node is FlowOverwriteNode flowOverwriteNode)
            {
                _flowOverwriteNodeRenderer.UpdateNodePosition(flowOverwriteNode, x, y);
                return;
            }

            if (node is BodyContainerNode bodyContainerNode)
            {
                _bodyContainerNodeRenderer.UpdateNodePosition(bodyContainerNode, x, y);
                return;
            }

            // ✅ Delegate ConditionalNode to its own renderer (handles both Diamond and Classic port positioning)
            if (node.IsConditionalNode)
            {
                _conditionalNodeRenderer.UpdateNodePosition(node, x, y);
                return;
            }

            // ⚠️ Skip LoopNode - it has custom diamond shape port positioning in LoopNodeRenderer.UpdateNodePosition
            if (node is LoopNode loopNode)
            {
                _loopNodeRenderer.UpdateNodePosition(loopNode, x, y);
                return;
            }

            if (node is AsyncTaskNode asyncTaskForPos)
            {
                _asyncTaskNodeRenderer.UpdateNodePosition(asyncTaskForPos, x, y);
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
            // ✅ Start node cleanup
            if (node.Type == NodeType.Start)
            {
                if (node.TitleTextBlockUI != null && canvas.Children.Contains(node.TitleTextBlockUI))
                {
                    canvas.Children.Remove(node.TitleTextBlockUI);
                    node.TitleTextBlockUI = null;
                }

                if (node.Border != null && canvas.Children.Contains(node.Border))
                {
                    canvas.Children.Remove(node.Border);
                }

                foreach (var port in node.Ports)
                {
                    if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                    {
                        canvas.Children.Remove(port.PortUI);
                    }
                }
                return;
            }

            // ✅ End node cleanup
            if (node.Type == NodeType.End)
            {
                if (node.TitleTextBlockUI != null && canvas.Children.Contains(node.TitleTextBlockUI))
                {
                    canvas.Children.Remove(node.TitleTextBlockUI);
                    node.TitleTextBlockUI = null;
                }

                if (node.Border != null && canvas.Children.Contains(node.Border))
                {
                    canvas.Children.Remove(node.Border);
                }

                foreach (var port in node.Ports)
                {
                    if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                    {
                        canvas.Children.Remove(port.PortUI);
                    }
                }
                return;
            }

            // ✅ Delegate cho LoopNodeRenderer xử lý
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

            // ✅ Delegate cho InputNodeRenderer để cleanup titleTextBlock
            if (node is InputNode inputNode)
            {
                _inputNodeRenderer.RemoveNode(inputNode, canvas);
                return;
            }

            // ✅ Delegate cho KeyPressEventNodeRenderer để cleanup titleTextBlock
            if (node is KeyPressEventNode keyNode)
            {
                _keyPressEventNodeRenderer.RemoveNode(keyNode, canvas);
                return;
            }

            // ✅ Delegate cho HotkeyPressEventNodeRenderer để cleanup titleTextBlock
            if (node is HotkeyPressEventNode hotkeyNode)
            {
                _hotkeyPressEventNodeRenderer.RemoveNode(hotkeyNode, canvas);
                return;
            }

            // ✅ Delegate cho các renderers khác nếu cần
            if (node is DelayNode delayNode)
            {
                _delayNodeRenderer.RemoveNode(delayNode, canvas);
                return;
            }

            if (node is MouseEventNode mouseNode)
            {
                _mouseEventNodeRenderer.RemoveNode(mouseNode, canvas);
                return;
            }

            if (node is StringSplitNode stringSplitNode)
            {
                _stringSplitNodeRenderer.RemoveNode(stringSplitNode, canvas);
                return;
            }

            if (node is ListOutNode listOutNode)
            {
                _listOutNodeRenderer.RemoveNode(listOutNode, canvas);
                return;
            }

            if (node is AssignDataNode assignDataNode)
            {
                _assignDataNodeRenderer.RemoveNode(assignDataNode, canvas);
                return;
            }

            if (node is MediaGalleryNode mediaGalleryNode)
            {
                _mediaGalleryNodeRenderer.RemoveNode(mediaGalleryNode, canvas);
                return;
            }

            if (node is ImageProcessingNode imageProcessingNode)
            {
                _imageProcessingNodeRenderer.RemoveNode(imageProcessingNode, canvas);
                return;
            }

            if (node is DataFetcherNode dataFetcherNodeForRemove)
            {
                _dataFetcherNodeRenderer.RemoveNode(dataFetcherNodeForRemove, canvas);
                return;
            }

            if (node is KeyValueBridgeNode keyValueBridgeForRemove)
            {
                _keyValueBridgeNodeRenderer.RemoveNode(keyValueBridgeForRemove, canvas);
                return;
            }

            if (node is WebNode webNodeForRemove)
            {
                _webNodeRenderer.RemoveNode(webNodeForRemove, canvas);
                return;
            }

            if (node is CodeNode codeNodeForRemove)
            {
                _codeNodeRenderer.RemoveNode(codeNodeForRemove, canvas);
                return;
            }

            if (node is FolderNode folderNodeForRemove)
            {
                _folderNodeRenderer.RemoveNode(folderNodeForRemove, canvas);
                return;
            }

            if (node is FileDownloadNode fileDownloadNodeForRemove)
            {
                _fileDownloadNodeRenderer.RemoveNode(fileDownloadNodeForRemove, canvas);
                return;
            }

            if (node is FolderFilePathsNode folderFilePathsNodeForRemove)
            {
                _folderFilePathsNodeRenderer.RemoveNode(folderFilePathsNodeForRemove, canvas);
                return;
            }

            if (node is OutputNode outputNode)
            {
                _outputNodeRenderer.RemoveNode(outputNode, canvas);
                return;
            }

            if (node is NotificationNode notificationNode)
            {
                _notificationNodeRenderer.RemoveNode(notificationNode, canvas);
                return;
            }

            if (node is HttpRequestNode httpRequestNode)
            {
                _httpRequestNodeRenderer.RemoveNode(httpRequestNode, canvas);
                return;
            }

            if (node is ScreenCaptureNode captureNode)
            {
                _screenCaptureNodeRenderer.RemoveNode(captureNode, canvas);
                return;
            }

            if (node is ScreenPositionPickerNode screenNode)
            {
                _screenPositionNodeRenderer.RemoveNode(screenNode, canvas);
                return;
            }

            if (node is StorageNode storageNodeForRemove)
            {
                _storageNodeRenderer.RemoveNode(storageNodeForRemove, canvas);
                return;
            }

            if (node is CallbackNode callbackNodeForRemove)
            {
                _callbackNodeRenderer.RemoveNode(callbackNodeForRemove, canvas);
                return;
            }

            if (node is FlowOverwriteNode flowOverwriteNodeForRemove)
            {
                _flowOverwriteNodeRenderer.RemoveNode(flowOverwriteNodeForRemove, canvas);
                return;
            }

            if (node is BodyContainerNode bodyContainerNodeForRemove)
            {
                _bodyContainerNodeRenderer.RemoveNode(bodyContainerNodeForRemove, canvas);
                return;
            }

            if (node.IsConditionalNode)
            {
                _conditionalNodeRenderer.RemoveNode(node, canvas);
                return;
            }

            // Fallback: cleanup cơ bản cho các node types khác
            if (node.Border != null && canvas.Children.Contains(node.Border))
            {
                canvas.Children.Remove(node.Border);
            }

            foreach (var port in node.Ports)
            {
                if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                {
                    canvas.Children.Remove(port.PortUI);
                }
            }
        }

        public void RemoveAllNodeVisuals(Canvas canvas)
        {
            // Xóa tất cả các Border (Node Body) mà có Tag là WorkflowNode
            var borders = canvas.Children.OfType<Border>().Where(b => b.Tag is WorkflowNode).ToList();
            foreach (var border in borders)
            {
                canvas.Children.Remove(border);
            }

            // Dọn sạch visual phụ của Conditional Diamond mode (satellite, add-circle, line, arrow, delete button)
            // để tránh ghost/duplicate visuals khi import workflow mới.
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
            }

            var addCircleBorders = canvas.Children
                .OfType<Border>()
                .Where(b => b.Tag is string s && s.StartsWith("AddSatellite:", StringComparison.Ordinal))
                .ToList();
            foreach (var addCircle in addCircleBorders)
            {
                canvas.Children.Remove(addCircle);
            }

            // Xóa tất cả các Ellipse (Ports tròn).
            // Legacy: nhiều port ellipses trước đây không gắn Tag => fallback remove theo kích thước chuẩn (18x18).
            var ellipsePorts = canvas.Children
                .OfType<Ellipse>()
                .Where(e => e.Tag is NodePort || (e.Width == 18 && e.Height == 18))
                .ToList();
            foreach (var port in ellipsePorts)
            {
                canvas.Children.Remove(port);
            }

            // Xóa các port UI còn sót lại (đặc biệt port hình thoi/rectangle trong Conditional Diamond mode).
            if (_host.ViewModel != null)
            {
                foreach (var node in _host.ViewModel.Nodes)
                {
                    foreach (var port in node.Ports)
                    {
                        if (port.PortUI != null && canvas.Children.Contains(port.PortUI))
                        {
                            canvas.Children.Remove(port.PortUI);
                        }
                    }
                }
            }

            // ✅ Xóa tất cả các Border wrapper chứa port chữ nhật (Rectangle)
            // Rectangle không được add trực tiếp vào canvas, chỉ Border wrapper được add
            var rectangularPortBorders = canvas.Children
                .OfType<Border>()
                .Where(b =>
                {
                    // Bỏ qua Border có Tag là WorkflowNode (đã xóa ở trên)
                    if (b.Tag is WorkflowNode) return false;
                    
                    // Nếu Border có Tag là NodePort, đó là port wrapper
                    if (b.Tag is NodePort) return true;
                    
                    // Nếu Border có child là Rectangle với Tag là Size hoặc kích thước port
                    if (b.Child is Rectangle rect)
                    {
                        // Kiểm tra Tag là Size (port chữ nhật luôn có Tag là Size)
                        if (rect.Tag is Size) return true;
                        
                        // Kiểm tra kích thước port chuẩn và đã highlight
                        if ((rect.Width == 12 && rect.Height == 25) ||
                            (rect.Width == 10 && rect.Height == 18) ||
                            (rect.Width == 14 && rect.Height == 27) ||
                            (rect.Width == 12 && rect.Height == 20))
                            return true;
                    }
                    
                    return false;
                })
                .ToList();

            // Xóa tất cả Border wrapper của port chữ nhật
            foreach (var portBorder in rectangularPortBorders)
            {
                if (portBorder != null && canvas.Children.Contains(portBorder))
                {
                    canvas.Children.Remove(portBorder);
                }
            }

            // Xóa tất cả titleTextBlocks từ các nodes có TitleTextBlockUI
            // ⚠️ CRITICAL: Xóa TẤT CẢ TextBlocks có ZIndex cao (titleTextBlocks thường có ZIndex = 20000)
            // Cần xóa tất cả để tránh ghost titles khi chuyển workflow
            var titleTextBlocks = canvas.Children.OfType<System.Windows.Controls.TextBlock>()
                .Where(tb => 
                {
                    var zIndex = Panel.GetZIndex(tb);
                    // Xóa tất cả TextBlocks có ZIndex = 20000 (titleTextBlocks chuẩn)
                    // HOẶC ZIndex > 10000 (các titleTextBlocks khác có thể có ZIndex khác)
                    return zIndex == 20000 || zIndex > 10000;
                })
                .ToList();
            
            // ⚠️ CRITICAL: Xóa từ canvas TRƯỚC để tránh ghost visuals
            foreach (var titleTextBlock in titleTextBlocks)
            {
                if (canvas.Children.Contains(titleTextBlock))
                {
                    canvas.Children.Remove(titleTextBlock);
                }
            }

            // Clear references từ các nodes trong ViewModel nếu có
            // ⚠️ CRITICAL: Xóa titleTextBlock khỏi canvas TRƯỚC KHI clear reference
            if (_host.ViewModel != null)
            {
                foreach (var node in _host.ViewModel.Nodes)
                {
                    // Start/End nodes
                    if ((node.Type == NodeType.Start || node.Type == NodeType.End) && node.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = node.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        node.TitleTextBlockUI = null;
                    }
                    else if (node is InputNode inputNode && inputNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = inputNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        inputNode.TitleTextBlockUI = null;
                    }
                    else if (node is KeyPressEventNode keyNode && keyNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = keyNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        keyNode.TitleTextBlockUI = null;
                    }
                    else if (node is HotkeyPressEventNode hotkeyNode && hotkeyNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = hotkeyNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        hotkeyNode.TitleTextBlockUI = null;
                    }
                    else if (node is MouseEventNode mouseNode && mouseNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = mouseNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        mouseNode.TitleTextBlockUI = null;
                    }
                    else if (node is ListOutNode listOutNode && listOutNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = listOutNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        listOutNode.TitleTextBlockUI = null;
                    }
                    else if (node is OutputNode outputNode && outputNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = outputNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        outputNode.TitleTextBlockUI = null;
                    }
                    else if (node is StringSplitNode stringSplitNode && stringSplitNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = stringSplitNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        stringSplitNode.TitleTextBlockUI = null;
                    }
                    else if (node is HttpRequestNode httpRequestNode && httpRequestNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = httpRequestNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        httpRequestNode.TitleTextBlockUI = null;
                    }
                    else if (node is AssignDataNode assignDataNode && assignDataNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = assignDataNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        assignDataNode.TitleTextBlockUI = null;
                    }
                    else if (node is MediaGalleryNode mediaGalleryNode && mediaGalleryNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = mediaGalleryNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        mediaGalleryNode.TitleTextBlockUI = null;
                    }
                    else if (node is ImageProcessingNode imageProcessingNode && imageProcessingNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = imageProcessingNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        imageProcessingNode.TitleTextBlockUI = null;
                    }
                    else if (node is CodeNode codeNode && codeNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = codeNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        codeNode.TitleTextBlockUI = null;
                    }
                    else if (node is FolderNode folderNode && folderNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = folderNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        folderNode.TitleTextBlockUI = null;
                    }
                    else if (node is LoopNode loopNode && loopNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = loopNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        loopNode.TitleTextBlockUI = null;
                    }
                    else if (node is WebNode webNode && webNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = webNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        webNode.TitleTextBlockUI = null;
                    }
                    else if (node is CallbackNode callbackNode && callbackNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = callbackNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        callbackNode.TitleTextBlockUI = null;
                    }
                    else if (node is FlowOverwriteNode flowOverwriteNode && flowOverwriteNode.TitleTextBlockUI != null)
                    {
                        var titleTextBlock = flowOverwriteNode.TitleTextBlockUI;
                        if (canvas.Children.Contains(titleTextBlock))
                        {
                            canvas.Children.Remove(titleTextBlock);
                        }
                        flowOverwriteNode.TitleTextBlockUI = null;
                    }
                }
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
        }

        private Border CreateNodeBorder(WorkflowNode node)
        {
            var border = new Border
            {
                Width = 150,
                Height = 80,
                Background = node.NodeBrush,
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand,
                Effect = GpuOptimizationHelper.CreateDropShadowEffect(),
                Tag = node
            };

            var textBlock = new TextBlock
            {
                Text = node.Title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };

            if (!string.IsNullOrEmpty(node.ColorKey))
            {
                var textBrush = Application.Current.TryFindResource($"TextOn{node.ColorKey}Brush") as Brush;
                textBlock.Foreground = textBrush ?? GetTextColorFromBrush(node.NodeBrush);
            }
            else
            {
                textBlock.Foreground = GetTextColorFromBrush(node.NodeBrush);
            }

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

            // Break/Continue UI chỉ dùng trong LoopBody: kiểm tra điểm gần trung tâm node
            // (Break/Continue thường ~100x40)
            var probe = new Point(node.X + 50, node.Y + 20);

            foreach (var loop in vm.Nodes.OfType<LoopNode>())
            {
                var body = loop.LoopBodyNode;
                var rect = new Rect(body.X, body.Y, body.Width, body.Height);
                if (rect.Contains(probe)) return true;
            }

            return false;
        }

    }
}

