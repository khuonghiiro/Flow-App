using FlowMy.Models;
using FlowMy.Models.Nodes;

namespace FlowMy.Services.Rendering
{
    public sealed class NodeRendererFactory
    {
        private readonly IReadOnlyDictionary<NodeType, INodeRenderer> _rendererByType;
        private readonly NodeRenderer _defaultRenderer;
        private readonly ConditionalNodeRenderer _conditionalNodeRenderer;
        private readonly AsyncTaskNodeRenderer _asyncTaskNodeRenderer;
        private readonly ScreenPositionNodeRenderer _screenPositionNodeRenderer;

        public NodeRendererFactory(
            Dictionary<NodeType, INodeRenderer> rendererByType,
            NodeRenderer defaultRenderer,
            ConditionalNodeRenderer conditionalNodeRenderer,
            AsyncTaskNodeRenderer asyncTaskNodeRenderer,
            ScreenPositionNodeRenderer screenPositionNodeRenderer)
        {
            _rendererByType = rendererByType ?? throw new ArgumentNullException(nameof(rendererByType));
            _defaultRenderer = defaultRenderer ?? throw new ArgumentNullException(nameof(defaultRenderer));
            _conditionalNodeRenderer = conditionalNodeRenderer ?? throw new ArgumentNullException(nameof(conditionalNodeRenderer));
            _asyncTaskNodeRenderer = asyncTaskNodeRenderer ?? throw new ArgumentNullException(nameof(asyncTaskNodeRenderer));
            _screenPositionNodeRenderer = screenPositionNodeRenderer ?? throw new ArgumentNullException(nameof(screenPositionNodeRenderer));
        }

        public INodeRenderer GetRenderer(WorkflowNode node)
        {
            if (node is ScreenPositionPickerNode)
            {
                return _screenPositionNodeRenderer;
            }

            if (node.IsConditionalNode)
            {
                return _conditionalNodeRenderer;
            }

            if (node is AsyncTaskNode)
            {
                return _asyncTaskNodeRenderer;
            }

            if (_rendererByType.TryGetValue(node.Type, out var renderer) && renderer != null)
            {
                return renderer;
            }

            return _defaultRenderer;
        }
    }
}

