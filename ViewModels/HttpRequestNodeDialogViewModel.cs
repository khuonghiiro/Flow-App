using FlowMy.Models;
using FlowMy.Models.Nodes;
using NodeHttpMethod = FlowMy.Models.Nodes.HttpMethod;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// ViewModel for a single key-value pair with dynamic binding support.
    /// </summary>
    public partial class HttpKeyValueItemViewModel : ObservableObject
    {
        private readonly IWorkflowEditorHost _host;
        private readonly WorkflowNode _ownerNode;

        [ObservableProperty]
        private string _key = string.Empty;

        [ObservableProperty]
        private string _value = string.Empty;

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private string? _sourceNodeId;

        [ObservableProperty]
        private string? _sourceOutputKey;

        public ObservableCollection<WorkflowDataSourceOption> AvailableSources { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeys { get; } = new();

        public HttpKeyValueItemViewModel(WorkflowNode ownerNode, IWorkflowEditorHost host)
        {
            _ownerNode = ownerNode;
            _host = host;

            RefreshAvailableSources();
        }

        partial void OnSourceNodeIdChanged(string? value)
        {
            RefreshAvailableOutputKeys();
        }

        public void RefreshAvailableSources()
        {
            AvailableSources.Clear();

            if (_host.ViewModel == null) return;

            var connections = _host.ViewModel.Connections;
            if (connections == null || connections.Count == 0) return;

            // Find upstream nodes with DynamicOutputs
            var upstream = new HashSet<WorkflowNode>();
            var stack = new Stack<WorkflowNode>();
            stack.Push(_ownerNode);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                var incoming = connections
                    .Where(c => c.ToNode == current && c.FromNode != null)
                    .ToList();

                foreach (var conn in incoming)
                {
                    var src = conn.FromNode;
                    if (src == null || ReferenceEquals(src, _ownerNode)) continue;

                    if (upstream.Add(src))
                    {
                        stack.Push(src);
                    }
                }
            }

            var producerNodes = upstream
                .Where(n => n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
                .ToList();

            foreach (var node in producerNodes)
            {
                AvailableSources.Add(new WorkflowDataSourceOption
                {
                    NodeId = node.Id,
                    Title = string.IsNullOrWhiteSpace(node.Title) ? node.Id : node.Title
                });
            }
        }

        public void RefreshAvailableOutputKeys()
        {
            AvailableOutputKeys.Clear();

            if (string.IsNullOrWhiteSpace(SourceNodeId) || _host.ViewModel == null) return;

            var sourceNode = _host.ViewModel.Nodes.FirstOrDefault(n => n.Id == SourceNodeId);
            if (sourceNode?.DynamicOutputs == null) return;

            foreach (var output in sourceNode.DynamicOutputs)
            {
                AvailableOutputKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = output.Key,
                    DisplayName = output.DisplayName ?? output.Key,
                    Type = output.OutputType
                });
            }
        }

        /// <summary>
        /// Convert to HttpKeyValuePair for storage in node.
        /// </summary>
        public HttpKeyValuePair ToModel()
        {
            return new HttpKeyValuePair
            {
                Key = Key,
                Value = Value,
                IsEnabled = IsEnabled,
                SourceNodeId = SourceNodeId,
                SourceOutputKey = SourceOutputKey
            };
        }

        /// <summary>
        /// Load from HttpKeyValuePair model.
        /// </summary>
        public static HttpKeyValueItemViewModel FromModel(HttpKeyValuePair model, WorkflowNode ownerNode, IWorkflowEditorHost host)
        {
            var vm = new HttpKeyValueItemViewModel(ownerNode, host)
            {
                Key = model.Key,
                Value = model.Value,
                IsEnabled = model.IsEnabled,
                SourceNodeId = model.SourceNodeId,
                SourceOutputKey = model.SourceOutputKey
            };
            return vm;
        }
    }

    /// <summary>
    /// ViewModel for HTTP Request Node Dialog.
    /// </summary>
    public partial class HttpRequestNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly HttpRequestNode _httpRequestNode;

        /// <summary>
        /// Expose node for code-behind access (e.g., color preview).
        /// </summary>
        public WorkflowNode Node => _httpRequestNode;

        #region Properties

        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private NodeHttpMethod _httpMethod = NodeHttpMethod.GET;

        [ObservableProperty]
        private HttpAuthType _authType = HttpAuthType.None;

        [ObservableProperty]
        private HttpBodyType _bodyType = HttpBodyType.None;

        [ObservableProperty]
        private string _rawBody = string.Empty;

        [ObservableProperty]
        private string _authUsername = string.Empty;

        [ObservableProperty]
        private string _authPassword = string.Empty;

        [ObservableProperty]
        private string _authToken = string.Empty;

        [ObservableProperty]
        private string _apiKeyName = "X-API-Key";

        [ObservableProperty]
        private string _apiKeyValue = string.Empty;

        [ObservableProperty]
        private bool _apiKeyInHeader = true;

        [ObservableProperty]
        private int _timeoutSeconds = 30;

        // URL dynamic binding
        [ObservableProperty]
        private string? _urlSourceNodeId;

        [ObservableProperty]
        private string? _urlSourceOutputKey;

        // Body dynamic binding
        [ObservableProperty]
        private string? _bodySourceNodeId;

        [ObservableProperty]
        private string? _bodySourceOutputKey;

        // Bearer token dynamic binding
        [ObservableProperty]
        private string? _tokenSourceNodeId;

        [ObservableProperty]
        private string? _tokenSourceOutputKey;

        // API Key value dynamic binding
        [ObservableProperty]
        private string? _apiKeyValueSourceNodeId;

        [ObservableProperty]
        private string? _apiKeyValueSourceOutputKey;

        // cURL command dynamic binding
        [ObservableProperty]
        private string? _curlSourceNodeId;

        [ObservableProperty]
        private string? _curlSourceOutputKey;

        // Anti-bot / bypass settings
        [ObservableProperty]
        private bool _useCurl = false;

        [ObservableProperty]
        private string _curlPath = string.Empty;

        [ObservableProperty]
        private string _impersonateBrowser = string.Empty;

        [ObservableProperty]
        private bool _autoAppendCurlWriteOut = true;

        #endregion

        #region Collections

        public ObservableCollection<HttpKeyValueItemViewModel> HeaderItems { get; } = new();
        public ObservableCollection<HttpKeyValueItemViewModel> ParamItems { get; } = new();
        public ObservableCollection<HttpKeyValueItemViewModel> FormDataItems { get; } = new();

        public ObservableCollection<WorkflowDataSourceOption> AvailableSources { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> UrlAvailableOutputKeys { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> BodyAvailableOutputKeys { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> TokenAvailableOutputKeys { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> ApiKeyValueAvailableOutputKeys { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> CurlAvailableOutputKeys { get; } = new();

        #endregion

        #region Options for ComboBoxes

        public ObservableCollection<TitleDisplayModeOption> TitleDisplayModeOptions { get; } = new()
        {
            new TitleDisplayModeOption(TitleDisplayMode.Hidden, "Ẩn tiêu đề"),
            new TitleDisplayModeOption(TitleDisplayMode.Hover, "Hiện khi hover"),
            new TitleDisplayModeOption(TitleDisplayMode.Always, "Luôn hiện")
        };

        public IEnumerable<NodeHttpMethod> HttpMethodOptions => Enum.GetValues(typeof(NodeHttpMethod)).Cast<NodeHttpMethod>();
        public IEnumerable<HttpAuthType> AuthTypeOptions => Enum.GetValues(typeof(HttpAuthType)).Cast<HttpAuthType>();
        public IEnumerable<HttpBodyType> BodyTypeOptions => Enum.GetValues(typeof(HttpBodyType)).Cast<HttpBodyType>();

        #endregion

        #region Visibility Properties

        public bool IsRawBodyVisible => BodyType == HttpBodyType.Raw || BodyType == HttpBodyType.Json;
        public bool IsFormDataVisible => BodyType == HttpBodyType.FormData || BodyType == HttpBodyType.FormUrlEncoded;
        public bool IsBasicAuthVisible => AuthType == HttpAuthType.Basic;
        public bool IsBearerAuthVisible => AuthType == HttpAuthType.Bearer;
        public bool IsApiKeyAuthVisible => AuthType == HttpAuthType.ApiKey;

        #endregion

        public HttpRequestNodeDialogViewModel(HttpRequestNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _httpRequestNode = node ?? throw new ArgumentNullException(nameof(node));

            // Initialize from node
            _url = node.Url;
            _httpMethod = node.HttpMethod;
            _authType = node.AuthType;
            _bodyType = node.BodyType;
            _rawBody = node.RawBody;
            _authUsername = node.AuthUsername;
            _authPassword = node.AuthPassword;
            _authToken = node.AuthToken;
            _apiKeyName = node.ApiKeyName;
            _apiKeyValue = node.ApiKeyValue;
            _apiKeyInHeader = node.ApiKeyInHeader;
            _timeoutSeconds = node.TimeoutSeconds;
            _urlSourceNodeId = node.UrlSourceNodeId;
            _urlSourceOutputKey = node.UrlSourceOutputKey;
            _bodySourceNodeId = node.BodySourceNodeId;
            _bodySourceOutputKey = node.BodySourceOutputKey;
            _tokenSourceNodeId = node.TokenSourceNodeId;
            _tokenSourceOutputKey = node.TokenSourceOutputKey;
            _apiKeyValueSourceNodeId = node.ApiKeyValueSourceNodeId;
            _apiKeyValueSourceOutputKey = node.ApiKeyValueSourceOutputKey;
            _curlSourceNodeId = node.CurlSourceNodeId;
            _curlSourceOutputKey = node.CurlSourceOutputKey;
            _useCurl = node.UseCurl;
            _curlPath = node.CurlPath;
            _impersonateBrowser = node.ImpersonateBrowser;
            _autoAppendCurlWriteOut = node.AutoAppendCurlWriteOut;

            // Load key-value pairs
            LoadHeaders();
            LoadParams();
            LoadFormData();

            // Refresh available sources
            RefreshAvailableSources();
            RefreshUrlOutputKeys();
            RefreshBodyOutputKeys();
            RefreshTokenOutputKeys();
            RefreshApiKeyValueOutputKeys();
            RefreshCurlOutputKeys();

            // Subscribe to changes
            PropertyChanged += OnViewModelPropertyChanged;

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    OnNodePropertyChanged(e.PropertyName ?? string.Empty);
                };
            }
        }

        protected override string GetDefaultTitle() => "HTTP Request";

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Sync to node on property change
            switch (e.PropertyName)
            {
                case nameof(Url):
                    _httpRequestNode.Url = Url;
                    break;
                case nameof(HttpMethod):
                    _httpRequestNode.HttpMethod = HttpMethod;
                    break;
                case nameof(AuthType):
                    _httpRequestNode.AuthType = AuthType;
                    OnPropertyChanged(nameof(IsBasicAuthVisible));
                    OnPropertyChanged(nameof(IsBearerAuthVisible));
                    OnPropertyChanged(nameof(IsApiKeyAuthVisible));
                    break;
                case nameof(BodyType):
                    _httpRequestNode.BodyType = BodyType;
                    OnPropertyChanged(nameof(IsRawBodyVisible));
                    OnPropertyChanged(nameof(IsFormDataVisible));
                    break;
                case nameof(RawBody):
                    _httpRequestNode.RawBody = RawBody;
                    break;
                case nameof(AuthUsername):
                    _httpRequestNode.AuthUsername = AuthUsername;
                    break;
                case nameof(AuthPassword):
                    _httpRequestNode.AuthPassword = AuthPassword;
                    break;
                case nameof(AuthToken):
                    _httpRequestNode.AuthToken = AuthToken;
                    break;
                case nameof(ApiKeyName):
                    _httpRequestNode.ApiKeyName = ApiKeyName;
                    break;
                case nameof(ApiKeyValue):
                    _httpRequestNode.ApiKeyValue = ApiKeyValue;
                    break;
                case nameof(ApiKeyInHeader):
                    _httpRequestNode.ApiKeyInHeader = ApiKeyInHeader;
                    break;
                case nameof(TimeoutSeconds):
                    _httpRequestNode.TimeoutSeconds = TimeoutSeconds;
                    break;
                case nameof(UrlSourceNodeId):
                    _httpRequestNode.UrlSourceNodeId = UrlSourceNodeId;
                    RefreshUrlOutputKeys();
                    break;
                case nameof(UrlSourceOutputKey):
                    _httpRequestNode.UrlSourceOutputKey = UrlSourceOutputKey;
                    break;
                case nameof(BodySourceNodeId):
                    _httpRequestNode.BodySourceNodeId = BodySourceNodeId;
                    RefreshBodyOutputKeys();
                    break;
                case nameof(BodySourceOutputKey):
                    _httpRequestNode.BodySourceOutputKey = BodySourceOutputKey;
                    break;
                case nameof(TokenSourceNodeId):
                    _httpRequestNode.TokenSourceNodeId = TokenSourceNodeId;
                    RefreshTokenOutputKeys();
                    break;
                case nameof(TokenSourceOutputKey):
                    _httpRequestNode.TokenSourceOutputKey = TokenSourceOutputKey;
                    break;
                case nameof(ApiKeyValueSourceNodeId):
                    _httpRequestNode.ApiKeyValueSourceNodeId = ApiKeyValueSourceNodeId;
                    RefreshApiKeyValueOutputKeys();
                    break;
                case nameof(ApiKeyValueSourceOutputKey):
                    _httpRequestNode.ApiKeyValueSourceOutputKey = ApiKeyValueSourceOutputKey;
                    break;
                case nameof(CurlSourceNodeId):
                    _httpRequestNode.CurlSourceNodeId = CurlSourceNodeId;
                    RefreshCurlOutputKeys();
                    // NOTE: Không tự động parse cURL khi chọn binding nguồn.
                    // Executor sẽ resolve và parse cURL command tại runtime khi node chạy.
                    break;
                case nameof(CurlSourceOutputKey):
                    _httpRequestNode.CurlSourceOutputKey = CurlSourceOutputKey;
                    // NOTE: Không tự động parse cURL khi chọn binding nguồn.
                    // Executor sẽ resolve và parse cURL command tại runtime khi node chạy.
                    break;
                case nameof(UseCurl):
                    _httpRequestNode.UseCurl = UseCurl;
                    break;
                case nameof(CurlPath):
                    _httpRequestNode.CurlPath = CurlPath;
                    break;
                case nameof(ImpersonateBrowser):
                    _httpRequestNode.ImpersonateBrowser = ImpersonateBrowser;
                    break;
                case nameof(AutoAppendCurlWriteOut):
                    _httpRequestNode.AutoAppendCurlWriteOut = AutoAppendCurlWriteOut;
                    break;
            }
        }

        protected override void OnSaveTitle()
        {
            _httpRequestNode.NotifyTitleChanged();

            // Save headers
            _httpRequestNode.Headers.Clear();
            foreach (var item in HeaderItems)
            {
                _httpRequestNode.Headers.Add(item.ToModel());
            }

            // Save params
            _httpRequestNode.QueryParams.Clear();
            foreach (var item in ParamItems)
            {
                _httpRequestNode.QueryParams.Add(item.ToModel());
            }

            // Save form data
            _httpRequestNode.FormData.Clear();
            foreach (var item in FormDataItems)
            {
                _httpRequestNode.FormData.Add(item.ToModel());
            }

            _host.RequestSyncDataPanels(immediate: true);
        }

        #region Load Methods

        private void LoadHeaders()
        {
            HeaderItems.Clear();
            foreach (var header in _httpRequestNode.Headers)
            {
                HeaderItems.Add(HttpKeyValueItemViewModel.FromModel(header, _httpRequestNode, _host));
            }
        }

        private void LoadParams()
        {
            ParamItems.Clear();
            foreach (var param in _httpRequestNode.QueryParams)
            {
                ParamItems.Add(HttpKeyValueItemViewModel.FromModel(param, _httpRequestNode, _host));
            }
        }

        private void LoadFormData()
        {
            FormDataItems.Clear();
            foreach (var formData in _httpRequestNode.FormData)
            {
                FormDataItems.Add(HttpKeyValueItemViewModel.FromModel(formData, _httpRequestNode, _host));
            }
        }

        private void RefreshAvailableSources()
        {
            AvailableSources.Clear();

            if (_host.ViewModel == null) return;

            var connections = _host.ViewModel.Connections;
            if (connections == null || connections.Count == 0) return;

            var upstream = new HashSet<WorkflowNode>();
            var stack = new Stack<WorkflowNode>();
            stack.Push(_httpRequestNode);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                var incoming = connections
                    .Where(c => c.ToNode == current && c.FromNode != null)
                    .ToList();

                foreach (var conn in incoming)
                {
                    var src = conn.FromNode;
                    if (src == null || ReferenceEquals(src, _httpRequestNode)) continue;

                    if (upstream.Add(src))
                    {
                        stack.Push(src);
                    }
                }
            }

            var producerNodes = upstream
                .Where(n => n.DynamicOutputs != null && n.DynamicOutputs.Count > 0)
                .ToList();

            foreach (var node in producerNodes)
            {
                AvailableSources.Add(new WorkflowDataSourceOption
                {
                    NodeId = node.Id,
                    Title = string.IsNullOrWhiteSpace(node.Title) ? node.Id : node.Title
                });
            }
        }

        private void RefreshUrlOutputKeys()
        {
            UrlAvailableOutputKeys.Clear();

            if (string.IsNullOrWhiteSpace(UrlSourceNodeId) || _host.ViewModel == null) return;

            var sourceNode = _host.ViewModel.Nodes.FirstOrDefault(n => n.Id == UrlSourceNodeId);
            if (sourceNode?.DynamicOutputs == null) return;

            foreach (var output in sourceNode.DynamicOutputs)
            {
                UrlAvailableOutputKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = output.Key,
                    DisplayName = output.DisplayName ?? output.Key,
                    Type = output.OutputType
                });
            }
        }

        private void RefreshBodyOutputKeys()
        {
            BodyAvailableOutputKeys.Clear();

            if (string.IsNullOrWhiteSpace(BodySourceNodeId) || _host.ViewModel == null) return;

            var sourceNode = _host.ViewModel.Nodes.FirstOrDefault(n => n.Id == BodySourceNodeId);
            if (sourceNode?.DynamicOutputs == null) return;

            foreach (var output in sourceNode.DynamicOutputs)
            {
                BodyAvailableOutputKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = output.Key,
                    DisplayName = output.DisplayName ?? output.Key,
                    Type = output.OutputType
                });
            }
        }

        private void RefreshTokenOutputKeys()
        {
            TokenAvailableOutputKeys.Clear();

            if (string.IsNullOrWhiteSpace(TokenSourceNodeId) || _host.ViewModel == null) return;

            var sourceNode = _host.ViewModel.Nodes.FirstOrDefault(n => n.Id == TokenSourceNodeId);
            if (sourceNode?.DynamicOutputs == null) return;

            foreach (var output in sourceNode.DynamicOutputs)
            {
                TokenAvailableOutputKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = output.Key,
                    DisplayName = output.DisplayName ?? output.Key,
                    Type = output.OutputType
                });
            }
        }

        private void RefreshApiKeyValueOutputKeys()
        {
            ApiKeyValueAvailableOutputKeys.Clear();

            if (string.IsNullOrWhiteSpace(ApiKeyValueSourceNodeId) || _host.ViewModel == null) return;

            var sourceNode = _host.ViewModel.Nodes.FirstOrDefault(n => n.Id == ApiKeyValueSourceNodeId);
            if (sourceNode?.DynamicOutputs == null) return;

            foreach (var output in sourceNode.DynamicOutputs)
            {
                ApiKeyValueAvailableOutputKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = output.Key,
                    DisplayName = output.DisplayName ?? output.Key,
                    Type = output.OutputType
                });
            }
        }

        private void RefreshCurlOutputKeys()
        {
            CurlAvailableOutputKeys.Clear();

            if (string.IsNullOrWhiteSpace(CurlSourceNodeId) || _host.ViewModel == null) return;

            var sourceNode = _host.ViewModel.Nodes.FirstOrDefault(n => n.Id == CurlSourceNodeId);
            if (sourceNode?.DynamicOutputs == null) return;

            foreach (var output in sourceNode.DynamicOutputs)
            {
                CurlAvailableOutputKeys.Add(new WorkflowOutputKeyOption
                {
                    Key = output.Key,
                    DisplayName = output.DisplayName ?? output.Key,
                    Type = output.OutputType
                });
            }
        }

        /// <summary>
        /// Khi người dùng chọn Node/Key cho cURL binding, tự động resolve giá trị cURL
        /// từ node nguồn và parse vào dialog, giống như khi dán cURL vào UrlTextBox.
        /// </summary>
        private void TryAutoParseCurlFromBinding()
        {
            if (_host.ViewModel == null) return;
            if (string.IsNullOrWhiteSpace(CurlSourceNodeId) || string.IsNullOrWhiteSpace(CurlSourceOutputKey))
                return;

            var vm = _host.ViewModel;
            var sourceNode = vm.Nodes.FirstOrDefault(n => n.Id == CurlSourceNodeId);
            if (sourceNode == null) return;

            // Lấy giá trị runtime từ node nguồn (ví dụ HttpRequestNode -> output "cURL")
            var curlValue = NodeDataPanelService.ResolveDynamicValueByKey(sourceNode, CurlSourceOutputKey);

            // "—" hoặc chuỗi rỗng nghĩa là chưa có dữ liệu thực sự
            if (string.IsNullOrWhiteSpace(curlValue) || curlValue.Trim() == "—")
                return;

            // Chỉ parse nếu thực sự là cURL command
            if (!IsCurlCommand(curlValue))
                return;

            // Parse và áp dụng vào Url/Headers/Params/Body/Auth...
            // ParseAndApplyCurl sẽ tự reset state cần thiết và cập nhật collections,
            // code-behind đang lắng CollectionChanged nên sẽ tự re-render UI.
            ParseAndApplyCurl(curlValue, out _);
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void AddHeader()
        {
            var item = new HttpKeyValueItemViewModel(_httpRequestNode, _host);
            HeaderItems.Add(item);
        }

        [RelayCommand]
        private void AddParam()
        {
            var item = new HttpKeyValueItemViewModel(_httpRequestNode, _host);
            ParamItems.Add(item);
        }

        [RelayCommand]
        private void AddFormData()
        {
            var item = new HttpKeyValueItemViewModel(_httpRequestNode, _host);
            FormDataItems.Add(item);
        }

        /// <summary>
        /// Check if the given text is a cURL command.
        /// </summary>
        public bool IsCurlCommand(string text)
        {
            return CurlParser.IsCurlCommand(text);
        }

        /// <summary>
        /// Parse a cURL command and populate all fields.
        /// Returns true if parsing was successful.
        /// </summary>
        public bool ParseAndApplyCurl(string curlCommand)
        {
            return ParseAndApplyCurl(curlCommand, out _);
        }

        /// <summary>
        /// Parse a cURL command and populate all fields.
        /// Returns true if parsing was successful, with error message if failed.
        /// </summary>
        public bool ParseAndApplyCurl(string curlCommand, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!CurlParser.IsCurlCommand(curlCommand))
            {
                errorMessage = "Text doesn't start with 'curl'";
                return false;
            }

            // IMPORTANT: Reset to initial state first to avoid old data interfering
            ResetToInitialState();

            var result = CurlParser.Parse(curlCommand);
            if (!result.IsValid)
            {
                errorMessage = result.ErrorMessage;
                return false;
            }

            // Apply URL (clean special characters)
            Url = CleanSpecialCharacters(result.Url);

            // Apply Method
            HttpMethod = result.Method;

            // Apply Headers (clean special characters from values)
            HeaderItems.Clear();
            foreach (var header in result.Headers)
            {
                var vm = new HttpKeyValueItemViewModel(_httpRequestNode, _host)
                {
                    Key = CleanSpecialCharacters(header.Key),
                    Value = CleanSpecialCharacters(header.Value),
                    IsEnabled = header.IsEnabled
                };
                HeaderItems.Add(vm);
            }

            // Apply Query Params (clean special characters from values)
            ParamItems.Clear();
            foreach (var param in result.QueryParams)
            {
                var vm = new HttpKeyValueItemViewModel(_httpRequestNode, _host)
                {
                    Key = CleanSpecialCharacters(param.Key),
                    Value = CleanSpecialCharacters(param.Value),
                    IsEnabled = param.IsEnabled
                };
                ParamItems.Add(vm);
            }

            // Apply Auth (clean special characters)
            AuthType = result.AuthType;
            AuthUsername = CleanSpecialCharacters(result.AuthUsername);
            AuthPassword = CleanSpecialCharacters(result.AuthPassword);
            AuthToken = CleanSpecialCharacters(result.AuthToken);

            // Apply Body (clean special characters)
            BodyType = result.BodyType;
            RawBody = CleanSpecialCharacters(result.RawBody);

            // Apply Form Data (if body type is FormData or FormUrlEncoded) (clean special characters)
            if (result.BodyType == HttpBodyType.FormData || result.BodyType == HttpBodyType.FormUrlEncoded)
            {
                FormDataItems.Clear();
                foreach (var formItem in result.FormData)
                {
                    var vm = new HttpKeyValueItemViewModel(_httpRequestNode, _host)
                    {
                        Key = CleanSpecialCharacters(formItem.Key),
                        Value = CleanSpecialCharacters(formItem.Value),
                        IsEnabled = formItem.IsEnabled
                    };
                    FormDataItems.Add(vm);
                }
            }

            return true;
        }

        /// <summary>
        /// Clear all parsed data (Headers, Params, Body, Auth, FormData).
        /// </summary>
        [RelayCommand]
        private void ClearAll()
        {
            Url = string.Empty;
            HttpMethod = NodeHttpMethod.GET;
            HeaderItems.Clear();
            ParamItems.Clear();
            FormDataItems.Clear();
            AuthType = HttpAuthType.None;
            AuthUsername = string.Empty;
            AuthPassword = string.Empty;
            AuthToken = string.Empty;
            ApiKeyName = "X-API-Key";
            ApiKeyValue = string.Empty;
            BodyType = HttpBodyType.None;
            RawBody = string.Empty;
        }

        /// <summary>
        /// Reset to initial state (before parsing new cURL).
        /// This ensures old cURL data doesn't interfere with new parse.
        /// </summary>
        public void ResetToInitialState()
        {
            // Reset all fields to default/empty values
            Url = string.Empty;
            HttpMethod = NodeHttpMethod.GET;
            HeaderItems.Clear();
            ParamItems.Clear();
            FormDataItems.Clear();
            AuthType = HttpAuthType.None;
            AuthUsername = string.Empty;
            AuthPassword = string.Empty;
            AuthToken = string.Empty;
            ApiKeyName = "X-API-Key";
            ApiKeyValue = string.Empty;
            ApiKeyInHeader = true;
            BodyType = HttpBodyType.None;
            RawBody = string.Empty;
            
            // Reset dynamic bindings
            UrlSourceNodeId = null;
            UrlSourceOutputKey = null;
            BodySourceNodeId = null;
            BodySourceOutputKey = null;
            TokenSourceNodeId = null;
            TokenSourceOutputKey = null;
            ApiKeyValueSourceNodeId = null;
            ApiKeyValueSourceOutputKey = null;
        }

        /// <summary>
        /// Clean special characters from a string value.
        /// Removes Windows CMD escape sequences and other problematic characters.
        /// </summary>
        private static string CleanSpecialCharacters(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var cleaned = value;

            // Remove Windows CMD escape sequences: ^%^XX -> %XX
            for (int i = 0; i < 5; i++)
            {
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\^%\^([0-9A-Fa-f]{2})", "%$1");
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"%\^([0-9A-Fa-f]{2})", "%$1");
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\^%([0-9A-Fa-f]{2})", "%$1");
            }

            // Remove remaining ^ escape characters (but preserve ^ in actual content)
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\^([&|<>()@^""'%])", "$1");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\^(?=[a-zA-Z0-9;,=\$\.\-_])", "");

            // Remove any stray ^ in URL-encoded sequences
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"(%[0-9A-Fa-f]?)\^([0-9A-Fa-f])", "$1$2");

            // Remove quotes if they wrap the entire value
            cleaned = cleaned.Trim('"', '\'');

            return cleaned;
        }

        /// <summary>
        /// Generate cURL command from current node configuration.
        /// Includes all configured parameters, headers, body, auth, etc.
        /// Uses helper class with connections from host for dynamic value resolution.
        /// </summary>
        public string GenerateCurlCommand()
        {
            // Get connections from host for dynamic value resolution
            var connections = _host.ViewModel?.Connections;

            // Convert ObservableCollection to List if needed
            List<WorkflowConnection>? connectionsList = null;
            if (connections != null)
            {
                connectionsList = connections.ToList();
            }

            // Use helper class to generate cURL
            return HttpRequestCurlGenerator.GenerateCurlCommand(_httpRequestNode, connectionsList);
        }

        #endregion
    }
}

