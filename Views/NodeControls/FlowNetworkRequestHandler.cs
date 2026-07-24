using System;
using CefSharp;
using CefSharp.Handler;

namespace FlowMy.Views.NodeControls
{
    public class FlowNetworkRequestHandler : RequestHandler
    {
        private readonly object _node;

        public FlowNetworkRequestHandler(object node)
        {
            _node = node;
        }

        protected override IResourceRequestHandler GetResourceRequestHandler(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            bool isNavigation,
            bool isDownload,
            string requestInitiator,
            ref bool disableDefaultHandling)
        {
            return base.GetResourceRequestHandler(chromiumWebBrowser, browser, frame, request, isNavigation, isDownload, requestInitiator, ref disableDefaultHandling);
        }
    }
}
