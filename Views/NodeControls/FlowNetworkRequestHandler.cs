using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CefSharp;
using CefSharp.Handler;
using CefSharp.ResponseFilter;

namespace FlowMy.Views.NodeControls
{
    public class FlowNetworkRequestHandler : RequestHandler
    {
        private readonly FlowResourceRequestHandler _resourceHandler;

        public FlowNetworkRequestHandler(object node)
        {
            _resourceHandler = new FlowResourceRequestHandler(node);
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
            return _resourceHandler;
        }
    }

    public class FlowResourceRequestHandler : ResourceRequestHandler
    {
        private readonly object _node;
        private readonly Dictionary<ulong, MemoryStream> _responseStreams = new();

        public FlowResourceRequestHandler(object node)
        {
            _node = node;
        }

        protected override IResponseFilter? GetResourceResponseFilter(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IResponse response)
        {
            var memoryStream = new MemoryStream();
            _responseStreams[request.Identifier] = memoryStream;
            return new StreamResponseFilter(memoryStream);
        }

        protected override void OnResourceLoadComplete(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IResponse response,
            UrlRequestStatus status,
            long receivedContentLength)
        {
            if (_responseStreams.TryGetValue(request.Identifier, out var stream))
            {
                try
                {
                    byte[] data = stream.ToArray();
                    string bodyText = Encoding.UTF8.GetString(data);

                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (request.Headers != null)
                    {
                        foreach (string key in request.Headers.AllKeys)
                        {
                            if (!string.IsNullOrEmpty(key))
                                headers[key] = request.Headers[key] ?? string.Empty;
                        }
                    }

                    string? postData = null;
                    if (request.PostData != null && request.PostData.Elements.Count > 0)
                    {
                        var sb = new StringBuilder();
                        foreach (var el in request.PostData.Elements)
                        {
                            if (el.Type == PostDataElementType.Bytes && el.Bytes != null)
                            {
                                sb.Append(Encoding.UTF8.GetString(el.Bytes));
                            }
                        }
                        postData = sb.ToString();
                    }

                    if (_node is FlowMy.Models.Nodes.WebNode webNode)
                    {
                        webNode.ProcessInterceptedNetworkResponse(request.Url, request.Method, headers, postData, bodyText, (int)response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FlowResourceRequestHandler] Error: {ex.Message}");
                }
                finally
                {
                    stream.Dispose();
                    _responseStreams.Remove(request.Identifier);
                }
            }
        }
    }
}
