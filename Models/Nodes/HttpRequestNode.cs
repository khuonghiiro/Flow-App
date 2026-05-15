using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// HTTP Method enum for API requests.
    /// </summary>
    public enum HttpMethod
    {
        GET,
        POST,
        PUT,
        DELETE,
        PATCH,
        HEAD,
        OPTIONS
    }

    /// <summary>
    /// Authorization type for HTTP requests.
    /// </summary>
    public enum HttpAuthType
    {
        None,
        Basic,
        Bearer,
        ApiKey
    }

    /// <summary>
    /// Body type for HTTP requests.
    /// </summary>
    public enum HttpBodyType
    {
        None,
        Raw,
        FormData,
        FormUrlEncoded,
        Json
    }

    /// <summary>
    /// Represents a key-value pair with optional dynamic binding.
    /// </summary>
    public class HttpKeyValuePair : INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _value = string.Empty;
        private bool _isEnabled = true;
        private string? _sourceNodeId;
        private string? _sourceOutputKey;

        public string Key
        {
            get => _key;
            set { if (_key != value) { _key = value; OnPropertyChanged(); } }
        }

        public string Value
        {
            get => _value;
            set { if (_value != value) { _value = value; OnPropertyChanged(); } }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Source node ID for dynamic value binding (replaces Value at runtime).
        /// </summary>
        public string? SourceNodeId
        {
            get => _sourceNodeId;
            set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Source output key from the selected node.
        /// </summary>
        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Node for making HTTP requests (similar to Postman).
    /// Supports URL, Method, Headers, Query Params, Body, and Authorization.
    /// </summary>
    public sealed class HttpRequestNode : WorkflowNode
    {
        private string _url = "https://api.example.com";
        private HttpMethod _httpMethod = HttpMethod.GET;
        private HttpAuthType _authType = HttpAuthType.None;
        private HttpBodyType _bodyType = HttpBodyType.None;
        private string _rawBody = string.Empty;
        private string _authUsername = string.Empty;
        private string _authPassword = string.Empty;
        private string _authToken = string.Empty;
        private string _apiKeyName = "X-API-Key";
        private string _apiKeyValue = string.Empty;
        private bool _apiKeyInHeader = true; // true = header, false = query param
        private int _timeoutSeconds = 30;

        // Dynamic binding for URL
        private string? _urlSourceNodeId;
        private string? _urlSourceOutputKey;

        // Dynamic binding for Body
        private string? _bodySourceNodeId;
        private string? _bodySourceOutputKey;

        // Dynamic binding for Bearer token
        private string? _tokenSourceNodeId;
        private string? _tokenSourceOutputKey;

        // Dynamic binding for API Key value
        private string? _apiKeyValueSourceNodeId;
        private string? _apiKeyValueSourceOutputKey;

        // Dynamic binding for cURL command input
        private string? _curlSourceNodeId;
        private string? _curlSourceOutputKey;

        // Anti-bot / bypass settings
        private bool _useCurl = true;
        private string _curlPath = string.Empty;
        private string _impersonateBrowser = string.Empty;
        private bool _autoAppendCurlWriteOut = false;

        public HttpRequestNode()
        {
            Type = NodeType.HttpRequest;
            Title = "HTTP Request";

            // Initialize collections
            Headers = new ObservableCollection<HttpKeyValuePair>();
            QueryParams = new ObservableCollection<HttpKeyValuePair>();
            FormData = new ObservableCollection<HttpKeyValuePair>();

            // Setup default DynamicInputs (for upstream data)
            DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "requestData",
                DisplayName = "Request Data",
                ConvertType = WorkflowDataType.Object
            });

            // Setup DynamicOutputs for response data
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "statusCode",
                DisplayName = "Status Code",
                ConvertType = WorkflowDataType.Number,
                OutputType = WorkflowDataType.Number,
                IsUserAdded = false
            });

            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "responseBody",
                DisplayName = "Response Body",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });

            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "responseHeaders",
                DisplayName = "Response Headers",
                ConvertType = WorkflowDataType.Object,
                OutputType = WorkflowDataType.Object,
                IsUserAdded = false
            });

            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "isSuccess",
                DisplayName = "Is Success",
                ConvertType = WorkflowDataType.Boolean,
                OutputType = WorkflowDataType.Boolean,
                IsUserAdded = false
            });

            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "errorMessage",
                DisplayName = "Error Message",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });

            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "responseTimeMs",
                DisplayName = "Response Time (ms)",
                ConvertType = WorkflowDataType.Number,
                OutputType = WorkflowDataType.Number,
                IsUserAdded = false
            });

            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "cURL",
                DisplayName = "cURL Command",
                ConvertType = WorkflowDataType.String,
                OutputType = WorkflowDataType.String,
                IsUserAdded = false
            });
        }

        #region Properties

        /// <summary>
        /// The URL to send the request to.
        /// </summary>
        public string Url
        {
            get => _url;
            set
            {
                if (_url != value)
                {
                    _url = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Source node ID for dynamic URL binding.
        /// </summary>
        public string? UrlSourceNodeId
        {
            get => _urlSourceNodeId;
            set { if (_urlSourceNodeId != value) { _urlSourceNodeId = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Source output key for dynamic URL binding.
        /// </summary>
        public string? UrlSourceOutputKey
        {
            get => _urlSourceOutputKey;
            set { if (_urlSourceOutputKey != value) { _urlSourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// HTTP method (GET, POST, PUT, DELETE, etc.).
        /// </summary>
        public HttpMethod HttpMethod
        {
            get => _httpMethod;
            set
            {
                if (_httpMethod != value)
                {
                    _httpMethod = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Request headers (key-value pairs).
        /// </summary>
        public ObservableCollection<HttpKeyValuePair> Headers { get; set; }

        /// <summary>
        /// Query parameters (appended to URL).
        /// </summary>
        public ObservableCollection<HttpKeyValuePair> QueryParams { get; set; }

        /// <summary>
        /// Form data (for POST/PUT requests with FormData body type).
        /// </summary>
        public ObservableCollection<HttpKeyValuePair> FormData { get; set; }

        /// <summary>
        /// Authorization type.
        /// </summary>
        public HttpAuthType AuthType
        {
            get => _authType;
            set
            {
                if (_authType != value)
                {
                    _authType = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Body type for the request.
        /// </summary>
        public HttpBodyType BodyType
        {
            get => _bodyType;
            set
            {
                if (_bodyType != value)
                {
                    _bodyType = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Raw body content (for Raw, Json body types).
        /// </summary>
        public string RawBody
        {
            get => _rawBody;
            set
            {
                if (_rawBody != value)
                {
                    _rawBody = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Source node ID for dynamic body binding.
        /// </summary>
        public string? BodySourceNodeId
        {
            get => _bodySourceNodeId;
            set { if (_bodySourceNodeId != value) { _bodySourceNodeId = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Source output key for dynamic body binding.
        /// </summary>
        public string? BodySourceOutputKey
        {
            get => _bodySourceOutputKey;
            set { if (_bodySourceOutputKey != value) { _bodySourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Username for Basic authentication.
        /// </summary>
        public string AuthUsername
        {
            get => _authUsername;
            set
            {
                if (_authUsername != value)
                {
                    _authUsername = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Password for Basic authentication.
        /// </summary>
        public string AuthPassword
        {
            get => _authPassword;
            set
            {
                if (_authPassword != value)
                {
                    _authPassword = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Token for Bearer authentication.
        /// </summary>
        public string AuthToken
        {
            get => _authToken;
            set
            {
                if (_authToken != value)
                {
                    _authToken = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Source node ID for dynamic Bearer token binding.
        /// </summary>
        public string? TokenSourceNodeId
        {
            get => _tokenSourceNodeId;
            set { if (_tokenSourceNodeId != value) { _tokenSourceNodeId = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Source output key for dynamic Bearer token binding.
        /// </summary>
        public string? TokenSourceOutputKey
        {
            get => _tokenSourceOutputKey;
            set { if (_tokenSourceOutputKey != value) { _tokenSourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// API Key name (header name or query param name).
        /// </summary>
        public string ApiKeyName
        {
            get => _apiKeyName;
            set
            {
                if (_apiKeyName != value)
                {
                    _apiKeyName = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// API Key value.
        /// </summary>
        public string ApiKeyValue
        {
            get => _apiKeyValue;
            set
            {
                if (_apiKeyValue != value)
                {
                    _apiKeyValue = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether API Key should be sent in header (true) or query param (false).
        /// </summary>
        public bool ApiKeyInHeader
        {
            get => _apiKeyInHeader;
            set
            {
                if (_apiKeyInHeader != value)
                {
                    _apiKeyInHeader = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Source node ID for dynamic API Key value binding.
        /// </summary>
        public string? ApiKeyValueSourceNodeId
        {
            get => _apiKeyValueSourceNodeId;
            set { if (_apiKeyValueSourceNodeId != value) { _apiKeyValueSourceNodeId = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Source output key for dynamic API Key value binding.
        /// </summary>
        public string? ApiKeyValueSourceOutputKey
        {
            get => _apiKeyValueSourceOutputKey;
            set { if (_apiKeyValueSourceOutputKey != value) { _apiKeyValueSourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Request timeout in seconds.
        /// </summary>
        public int TimeoutSeconds
        {
            get => _timeoutSeconds;
            set
            {
                if (_timeoutSeconds != value)
                {
                    _timeoutSeconds = Math.Max(1, Math.Min(300, value)); // 1-300 seconds
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Source node ID for dynamic cURL command binding (auto-parses and populates fields).
        /// </summary>
        public string? CurlSourceNodeId
        {
            get => _curlSourceNodeId;
            set { if (_curlSourceNodeId != value) { _curlSourceNodeId = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Source output key for dynamic cURL command binding.
        /// </summary>
        public string? CurlSourceOutputKey
        {
            get => _curlSourceOutputKey;
            set { if (_curlSourceOutputKey != value) { _curlSourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Bật chế độ bypass dùng libcurl (CurlThin) thay vì HttpClient.
        /// Bypass CORS, Cloudflare, TLS fingerprint.
        /// </summary>
        public bool UseCurl
        {
            get => _useCurl;
            set { if (_useCurl != value) { _useCurl = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Đường dẫn đến curl.exe tùy chỉnh (để trống = auto-detect).
        /// Dùng khi CurlThin native DLL không load được.
        /// </summary>
        public string CurlPath
        {
            get => _curlPath;
            set { if (_curlPath != value) { _curlPath = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Browser to impersonate cho TLS fingerprint (chrome120, firefox117, safari17_0, ...).
        /// Chỉ hoạt động khi dùng curl-impersonate build.
        /// Để trống = không impersonate.
        /// </summary>
        public string ImpersonateBrowser
        {
            get => _impersonateBrowser;
            set { if (_impersonateBrowser != value) { _impersonateBrowser = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Tự động thêm cờ output cho raw curl (`-D - -w ...`) để parse status code ổn định.
        /// Tắt khi bạn muốn chạy command y nguyên 100% không bị app can thiệp.
        /// </summary>
        public bool AutoAppendCurlWriteOut
        {
            get => _autoAppendCurlWriteOut;
            set { if (_autoAppendCurlWriteOut != value) { _autoAppendCurlWriteOut = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Runtime Results (not serialized)

        /// <summary>
        /// Last HTTP status code received.
        /// </summary>
        [JsonIgnore]
        public int? LastStatusCode { get; set; }

        /// <summary>
        /// Last response body received.
        /// </summary>
        [JsonIgnore]
        public string? LastResponseBody { get; set; }

        /// <summary>
        /// Last response headers received.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, string>? LastResponseHeaders { get; set; }

        /// <summary>
        /// Last request success status.
        /// </summary>
        [JsonIgnore]
        public bool? LastIsSuccess { get; set; }

        /// <summary>
        /// Last error message (if any).
        /// </summary>
        [JsonIgnore]
        public string? LastErrorMessage { get; set; }

        /// <summary>
        /// Last response time in milliseconds.
        /// </summary>
        [JsonIgnore]
        public long? LastResponseTimeMs { get; set; }

        /// <summary>
        /// Generated cURL command for the last request.
        /// </summary>
        [JsonIgnore]
        public string? LastCurlCommand { get; set; }

        #endregion
    }
}

