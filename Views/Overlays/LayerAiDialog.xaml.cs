using FlowMy.Helpers;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using FlowMy.Views.NodeControls;
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace FlowMy.Views.Overlays
{
    public partial class LayerAiDialog : Window
    {
        public static readonly ConcurrentQueue<string> PendingExecutionIds = new ConcurrentQueue<string>();

        private readonly EditorLayer _activeLayer;
        private readonly ImageProcessingNode _node;
        private readonly IWorkflowEditorHost _host;
        private readonly EditorDocument _doc;
        private readonly Window? _ownerWindow;

        // Secondary images management
        private class SecondaryImageItem
        {
            public BitmapSource? Bitmap { get; set; }
            public string? FilePath { get; set; }
            public bool IsSelected { get; set; } = true;
            public bool HasImage => Bitmap != null;
        }

        private readonly SecondaryImageItem[] _secondaryImages = new SecondaryImageItem[4]
        {
            new SecondaryImageItem(),
            new SecondaryImageItem(),
            new SecondaryImageItem(),
            new SecondaryImageItem()
        };

        // Cached references to UI elements for each slot
        private Border[] _slotBorders = null!;
        private Image[] _slotImages = null!;
        private TextBlock[] _slotPlaceholders = null!;
        private Border[] _slotChecks = null!;
        private Border[] _slotRemoves = null!;
        private Border[] _slotBordersWv = null!;
        private Image[] _slotImagesWv = null!;
        private TextBlock[] _slotPlaceholdersWv = null!;
        private Border[] _slotRemovesWv = null!;

        // Tab + dialog state
        private double _originalWidth;
        private double _originalHeight;
        private FrameworkElement? _hoveredImageContainer = null;

        public LayerAiDialog(EditorLayer activeLayer, ImageProcessingNode node, IWorkflowEditorHost host, EditorDocument doc, Window? owner)
        {
            InitializeComponent();
            _ownerWindow = owner;
            Owner = owner;

            // Handle Activated and Deactivated to dynamically control Topmost,
            // preventing this dialog from permanently overlapping other applications (like Chrome)
            // while ensuring it stays on top of the topmost FloatingWidgetWindow when active.
            this.Activated += (s, e) =>
            {
                if (Owner != null) Owner.Topmost = true;
                this.Topmost = true;
            };

            this.Deactivated += (s, e) =>
            {
                this.Topmost = false;
                if (Owner != null) Owner.Topmost = false;
            };

            this.Closed += (s, e) =>
            {
                if (_ownerWindow != null)
                {
                    _ownerWindow.Topmost = true;
                }
                try
                {
                    LayerAiWebViewCache.ReleaseToSleep(_node.Id);
                }
                catch { }
            };

            _activeLayer = activeLayer ?? throw new ArgumentNullException(nameof(activeLayer));
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));

            _originalWidth = Width;
            _originalHeight = Height;

            if (owner == null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // Initialize slot references
            _slotBorders = new[] { SlotBorder0, SlotBorder1, SlotBorder2, SlotBorder3 };
            _slotImages = new[] { SlotImage0, SlotImage1, SlotImage2, SlotImage3 };
            _slotPlaceholders = new[] { SlotPlaceholder0, SlotPlaceholder1, SlotPlaceholder2, SlotPlaceholder3 };
            _slotChecks = new[] { SlotCheck0, SlotCheck1, SlotCheck2, SlotCheck3 };
            _slotRemoves = new[] { SlotRemove0, SlotRemove1, SlotRemove2, SlotRemove3 };
            _slotBordersWv = new[] { SlotBorderWv0, SlotBorderWv1, SlotBorderWv2, SlotBorderWv3 };
            _slotImagesWv = new[] { SlotImageWv0, SlotImageWv1, SlotImageWv2, SlotImageWv3 };
            _slotPlaceholdersWv = new[] { SlotPlaceholderWv0, SlotPlaceholderWv1, SlotPlaceholderWv2, SlotPlaceholderWv3 };
            _slotRemovesWv = new[] { SlotRemoveWv0, SlotRemoveWv1, SlotRemoveWv2, SlotRemoveWv3 };

            // Load saved settings
            LoadSavedSettings();

            // Load preview image
            UpdatePreviewImage();

            // Refresh all slots UI
            RefreshAllSlotsUI();

            // Setup two-way drag and drop between WPF and WebView2
            SetupDragAndDrop();

            // Listen to Ctrl+V paste event when hovering over images/slots
            this.PreviewKeyDown += LayerAiDialog_PreviewKeyDown;
        }

        #region Header & Window Actions

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        #region Tab Switching (Prompt / WebView / WebBrowser)

        private enum ActiveTab { Prompt, WebView, WebBrowser }
        private ActiveTab _activeTab = ActiveTab.Prompt;

        // WebView2 browser (lazy init)
        private Microsoft.Web.WebView2.Wpf.WebView2? _webBrowser;
        private bool _webBrowserInitialized = false;
        private Microsoft.Web.WebView2.Wpf.WebView2? _dynamicWebView;
        private System.Windows.Controls.Primitives.Popup? _suggestPopup;
        private ListBox? _suggestListBox;
        private System.Windows.Threading.DispatcherTimer? _suggestDebounceTimer;
        private Point _dragStartPoint;

        private string GetActivePromptText()
        {
            return _activeTab switch
            {
                ActiveTab.WebView => TxtPromptWv.Text,
                ActiveTab.WebBrowser => TxtPromptWeb.Text,
                _ => TxtPrompt.Text
            };
        }

        private void SyncPromptTo(ActiveTab target)
        {
            var text = GetActivePromptText();
            if (target != ActiveTab.Prompt) TxtPrompt.Text = text;
            if (target != ActiveTab.WebView) TxtPromptWv.Text = text;
            if (target != ActiveTab.WebBrowser) TxtPromptWeb.Text = text;
        }

        private void SetTabStyles(ActiveTab active)
        {
            var accent = FindResource("AccentColor") as Brush ?? Brushes.Lime;
            var border = FindResource("BorderColor") as Brush ?? Brushes.DimGray;
            var muted = FindResource("TextMuted") as Brush ?? Brushes.Gray;
            var activeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a4fffb0"));

            // Prompt tab
            TabHeaderPrompt.Background = active == ActiveTab.Prompt ? activeBg : Brushes.Transparent;
            TabHeaderPrompt.BorderBrush = active == ActiveTab.Prompt ? accent : border;

            // WebView tab
            TabHeaderWebView.Background = active == ActiveTab.WebView ? activeBg : Brushes.Transparent;
            TabHeaderWebView.BorderBrush = active == ActiveTab.WebView ? accent : border;
            TabHeaderWebViewText.Foreground = active == ActiveTab.WebView ? accent : muted;

            // Web Browser tab
            TabHeaderWebBrowser.Background = active == ActiveTab.WebBrowser ? activeBg : Brushes.Transparent;
            TabHeaderWebBrowser.BorderBrush = active == ActiveTab.WebBrowser ? accent : border;
            TabHeaderWebBrowserText.Foreground = active == ActiveTab.WebBrowser ? accent : muted;
        }

        private void ExpandDialogToScreen()
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            var screenW = SystemParameters.PrimaryScreenWidth;
            var screenH = SystemParameters.PrimaryScreenHeight;
            var targetW = Math.Min(screenW * 0.90, screenW - 60);
            var targetH = Math.Min(screenH * 0.85, screenH - 80);
            Width = Math.Max(_originalWidth, targetW);
            Height = Math.Max(_originalHeight, targetH);
            Left = (screenW - Width) / 2;
            Top = (screenH - Height) / 2;
        }

        private void SwitchToTab(ActiveTab newTab)
        {
            if (_activeTab == newTab) return;

            // Sync prompt from current tab to all others
            SyncPromptTo(newTab);
            _activeTab = newTab;

            if (_node != null)
            {
                _node.LayerAiActiveTab = newTab.ToString();
            }

            // Toggle layout visibility and widths
            if (newTab == ActiveTab.Prompt)
            {
                ColLeft.Width = new GridLength(6, GridUnitType.Star);
                ColRight.Width = new GridLength(3, GridUnitType.Star);
                GridLeftNormal.Visibility = Visibility.Visible;
                GridLeftExpanded.Visibility = Visibility.Collapsed;
            }
            else
            {
                ColLeft.Width = new GridLength(1, GridUnitType.Star);
                ColRight.Width = new GridLength(2, GridUnitType.Star);
                GridLeftNormal.Visibility = Visibility.Collapsed;
                GridLeftExpanded.Visibility = Visibility.Visible;
            }

            TabContentPrompt.Visibility = newTab == ActiveTab.Prompt ? Visibility.Visible : Visibility.Collapsed;
            TabContentWebView.Visibility = newTab == ActiveTab.WebView ? Visibility.Visible : Visibility.Collapsed;
            TabContentWebBrowser.Visibility = newTab == ActiveTab.WebBrowser ? Visibility.Visible : Visibility.Collapsed;

            // Tab header styling
            SetTabStyles(newTab);

            if (newTab == ActiveTab.Prompt)
            {
                // Restore original dialog size
                Width = _originalWidth;
                Height = _originalHeight;
                CenterOnScreen();
            }
            else
            {
                // Sync images to the active expanded layout
                SyncImagesToLayout(newTab);
                ExpandDialogToScreen();
            }

            // Lazy-init WebView2 browser when Web tab first activated
            if (newTab == ActiveTab.WebBrowser && !_webBrowserInitialized)
            {
                InitWebBrowserAsync();
            }

            // Lazy-init/refresh Dynamic UI when tab activated
            if (newTab == ActiveTab.WebView)
            {
                InitDynamicWebViewAsync();
            }
        }

        private void TabPrompt_Click(object sender, MouseButtonEventArgs e) => SwitchToTab(ActiveTab.Prompt);
        private void TabWebView_Click(object sender, MouseButtonEventArgs e) => SwitchToTab(ActiveTab.WebView);
        private void TabWebBrowser_Click(object sender, MouseButtonEventArgs e) => SwitchToTab(ActiveTab.WebBrowser);

        /// <summary>Sync ảnh chính + ảnh phụ sang layout mở rộng (GridLeftExpanded).</summary>
        private void SyncImagesToLayout(ActiveTab tab)
        {
            if (tab != ActiveTab.Prompt)
            {
                ImgPreviewWv.Source = ImgPreview.Source;
                var wvImages = new[] { SlotImageWv0, SlotImageWv1, SlotImageWv2, SlotImageWv3 };
                var wvPlaceholders = new[] { SlotPlaceholderWv0, SlotPlaceholderWv1, SlotPlaceholderWv2, SlotPlaceholderWv3 };
                for (int i = 0; i < 4; i++)
                {
                    wvImages[i].Source = _slotImages[i].Source;
                    wvPlaceholders[i].Visibility = _secondaryImages[i]?.HasImage == true ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }

        private void CenterOnScreen()
        {
            if (Owner != null && !double.IsNaN(Owner.Left) && !double.IsNaN(Owner.Top))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = Owner.Left + (Owner.Width - Width) / 2;
                Top = Owner.Top + (Owner.Height - Height) / 2;
            }
            else
            {
                if (this.IsLoaded)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    var screenW = SystemParameters.PrimaryScreenWidth;
                    var screenH = SystemParameters.PrimaryScreenHeight;
                    Left = (screenW - Width) / 2;
                    Top = (screenH - Height) / 2;
                }
                else
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
            }
        }

        #endregion

        #region Web Browser (WebView2 + Search + Profile)

        private Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions GetBrowserEnvironmentOptions()
        {
            var options = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions();
            var browserArgs = new System.Text.StringBuilder();

            browserArgs.Append("--disable-background-timer-throttling ");
            browserArgs.Append("--disable-backgrounding-occluded-windows ");
            browserArgs.Append("--disable-renderer-backgrounding ");
            browserArgs.Append("--calculate-native-win-occlusion=false ");

            if (GpuDetectionHelper.IsGpuAvailable)
            {
                browserArgs.Append("--enable-gpu-rasterization ");
                browserArgs.Append("--enable-zero-copy ");
                browserArgs.Append("--enable-features=VaapiVideoDecoder ");
                browserArgs.Append("--ignore-gpu-blacklist ");
                browserArgs.Append("--enable-accelerated-2d-canvas ");
                browserArgs.Append("--enable-accelerated-video-decode ");
            }
            else
            {
                browserArgs.Append("--disable-gpu ");
            }

            options.AdditionalBrowserArguments = browserArgs.ToString().Trim();
            return options;
        }

        private async void InitDynamicWebViewAsync()
        {
            try
            {
                var cacheState = LayerAiWebViewCache.GetOrCreateState(_node.Id);
                if (cacheState.DynamicWebView == null)
                {
                    var webView = new Microsoft.Web.WebView2.Wpf.WebView2();
                    
                    // Add WebView2 to container FIRST so it's in the visual tree!
                    WebViewContainer.Child = webView;

                    var cachePath = WebNodeCacheHelper.GetProfileCachePath("DynamicUi_" + _node.Id);
                    Directory.CreateDirectory(cachePath);
                    var options = GetBrowserEnvironmentOptions();
                    var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, cachePath, options);

                    await webView.EnsureCoreWebView2Async(env);
                    await InjectDragDropInterceptorScriptAsync(webView);
                    cacheState.DynamicWebView = webView;
                }
                else
                {
                    var webView = cacheState.DynamicWebView;
                    if (webView.Parent is Border parentBorder)
                    {
                        parentBorder.Child = null;
                    }
                    WebViewContainer.Child = webView;
                    try { webView.CoreWebView2?.Resume(); } catch { }
                }

                _dynamicWebView = cacheState.DynamicWebView;
                RenderDynamicUi();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LayerAI DynamicWebView init failed: {ex.Message}");
            }
        }

        private void RenderDynamicUi()
        {
            if (_dynamicWebView?.CoreWebView2 == null) return;

            try
            {
                var htmlCode = _node.LayerAiHtmlCode ?? "";
                var cssCode = _node.LayerAiCssCode ?? "";
                var jsCode = _node.LayerAiJsCode ?? "";

                var builder = new System.Text.StringBuilder();
                builder.AppendLine("<!DOCTYPE html>");
                builder.AppendLine("<html>");
                builder.AppendLine("<head>");
                builder.AppendLine("<meta charset=\"utf-8\" />");
                builder.AppendLine("<style>");
                builder.AppendLine("body { margin: 0; padding: 12px; background-color: #111318; color: #dde3ef; font-family: sans-serif; }");
                builder.AppendLine(cssCode);
                builder.AppendLine("</style>");
                builder.AppendLine("</head>");
                builder.AppendLine("<body>");
                builder.AppendLine(htmlCode);
                builder.AppendLine("<script>");
                builder.AppendLine(jsCode);
                builder.AppendLine("</script>");
                builder.AppendLine("</body>");
                builder.AppendLine("</html>");

                var fullHtml = builder.ToString();
                _dynamicWebView.CoreWebView2.NavigateToString(fullHtml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to render dynamic UI: {ex.Message}");
            }
        }

        private async void InitWebBrowserAsync()
        {
            _webBrowserInitialized = true;

            try
            {
                // Load profile combo
                LoadWebProfiles();

                var cacheState = LayerAiWebViewCache.GetOrCreateState(_node.Id);
                if (cacheState.WebBrowser == null)
                {
                    var webBrowser = new Microsoft.Web.WebView2.Wpf.WebView2();

                    // Add WebView2 to container FIRST so it's in the visual tree!
                    WebBrowserContainer.Children.Clear();
                    WebBrowserContainer.Children.Add(webBrowser);

                    var profileName = _node.LayerAiCacheProfileName ?? "Shared";
                    Microsoft.Web.WebView2.Core.CoreWebView2Environment env;

                    if (string.Equals(profileName, "Shared", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            env = await WebView2EnvironmentManager.GetSharedEnvironmentAsync();
                        }
                        catch (Exception exShared)
                        {
                            System.Diagnostics.Debug.WriteLine($"Shared env failed, falling back: {exShared.Message}");
                            var cachePath = WebNodeCacheHelper.GetProfileCachePath("SharedFallback");
                            Directory.CreateDirectory(cachePath);
                            var options = GetBrowserEnvironmentOptions();
                            env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, cachePath, options);
                        }
                    }
                    else
                    {
                        var cachePath = WebNodeCacheHelper.GetProfileCachePath(profileName);
                        Directory.CreateDirectory(cachePath);
                        var options = GetBrowserEnvironmentOptions();
                        env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, cachePath, options);
                    }

                    await webBrowser.EnsureCoreWebView2Async(env);
                    await InjectDragDropInterceptorScriptAsync(webBrowser);
                    cacheState.WebBrowser = webBrowser;

                    // Navigate to saved URL
                    var url = _node.LayerAiWebUrl;
                    if (string.IsNullOrWhiteSpace(url)) url = "https://google.com";
                    TxtWebUrl.Text = url;
                    webBrowser.CoreWebView2.Navigate(url);

                    // Track navigation
                    webBrowser.CoreWebView2.NavigationCompleted += (s, args) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                var currentUrl = webBrowser.CoreWebView2?.Source;
                                if (!string.IsNullOrEmpty(currentUrl))
                                {
                                    TxtWebUrl.Text = currentUrl;
                                    _node.LayerAiWebUrl = currentUrl;
                                }
                            }
                            catch { }
                        });
                    };
                }
                else
                {
                    var webBrowser = cacheState.WebBrowser;
                    if (webBrowser.Parent is Panel parentPanel)
                    {
                        parentPanel.Children.Remove(webBrowser);
                    }
                    WebBrowserContainer.Children.Clear();
                    WebBrowserContainer.Children.Add(webBrowser);

                    try { webBrowser.CoreWebView2?.Resume(); } catch { }

                    var currentUrl = webBrowser.CoreWebView2?.Source;
                    if (!string.IsNullOrEmpty(currentUrl))
                    {
                        TxtWebUrl.Text = currentUrl;
                        _node.LayerAiWebUrl = currentUrl;
                    }
                }

                _webBrowser = cacheState.WebBrowser;

                // Setup Google Suggest popup
                SetupSuggestPopup();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LayerAI WebBrowser init failed: {ex.Message}");
                MessageBox.Show($"Lỗi khởi tạo trình duyệt Web: {ex.Message}\n\nChi tiết: {ex.InnerException?.Message}\n\nStack: {ex.StackTrace}", "Lỗi WebView2", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadWebProfiles()
        {
            CmbWebProfile.Items.Clear();
            var profiles = WebNodeCacheHelper.GetAvailableCacheProfiles();
            foreach (var p in profiles)
            {
                CmbWebProfile.Items.Add(new ComboBoxItem { Content = p, Tag = p });
            }

            // Select current profile
            var current = _node.LayerAiCacheProfileName ?? "Shared";
            for (int i = 0; i < CmbWebProfile.Items.Count; i++)
            {
                if (CmbWebProfile.Items[i] is ComboBoxItem item && string.Equals(item.Tag as string, current, StringComparison.OrdinalIgnoreCase))
                {
                    CmbWebProfile.SelectedIndex = i;
                    break;
                }
            }
            if (CmbWebProfile.SelectedIndex < 0 && CmbWebProfile.Items.Count > 0)
                CmbWebProfile.SelectedIndex = 0;
        }

        private async void CmbWebProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbWebProfile.SelectedItem is ComboBoxItem item && item.Tag is string profileName)
            {
                if (_node.LayerAiCacheProfileName == profileName) return;
                _node.LayerAiCacheProfileName = profileName;

                // Recreate WebView2 with new profile
                if (_webBrowser != null && _webBrowserInitialized)
                {
                    try
                    {
                        var oldUrl = _webBrowser.CoreWebView2?.Source ?? "https://google.com";
                        WebBrowserContainer.Children.Clear();
                        _webBrowser.Dispose();
                        _webBrowser = null;

                        _webBrowser = new Microsoft.Web.WebView2.Wpf.WebView2();

                        // Add WebView2 to container FIRST so it's in the visual tree!
                        WebBrowserContainer.Children.Clear();
                        WebBrowserContainer.Children.Add(_webBrowser);

                        Microsoft.Web.WebView2.Core.CoreWebView2Environment env;

                        if (string.Equals(profileName, "Shared", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                env = await WebView2EnvironmentManager.GetSharedEnvironmentAsync();
                            }
                            catch (Exception exShared)
                            {
                                System.Diagnostics.Debug.WriteLine($"Shared env failed, falling back: {exShared.Message}");
                                var cachePath = WebNodeCacheHelper.GetProfileCachePath("SharedFallback");
                                Directory.CreateDirectory(cachePath);
                                var options = GetBrowserEnvironmentOptions();
                                env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, cachePath, options);
                            }
                        }
                        else
                        {
                            var cachePath = WebNodeCacheHelper.GetProfileCachePath(profileName);
                            Directory.CreateDirectory(cachePath);
                            var options = GetBrowserEnvironmentOptions();
                            env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, cachePath, options);
                        }

                        await _webBrowser.EnsureCoreWebView2Async(env);
                        await InjectDragDropInterceptorScriptAsync(_webBrowser);
 
                        // Update cache
                        var cacheState = LayerAiWebViewCache.GetOrCreateState(_node.Id);
                        cacheState.WebBrowser = _webBrowser;

                        _webBrowser.CoreWebView2.Navigate(oldUrl);
                        _webBrowser.CoreWebView2.NavigationCompleted += (s2, args) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    var currentUrl = _webBrowser.CoreWebView2?.Source;
                                    if (!string.IsNullOrEmpty(currentUrl))
                                    {
                                        TxtWebUrl.Text = currentUrl;
                                        _node.LayerAiWebUrl = currentUrl;
                                    }
                                }
                                catch { }
                            });
                        };
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Profile switch failed: {ex.Message}");
                        MessageBox.Show($"Lỗi chuyển đổi Profile trình duyệt: {ex.Message}\n\nStack: {ex.StackTrace}", "Lỗi WebView2", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
        {
            // Simple inline input dialog
            var dialog = new Window
            {
                Title = "Tạo Profile mới",
                Width = 320,
                Height = 190,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1c23"))
            };

            var sp = new StackPanel { Margin = new Thickness(16) };
            var lbl = new TextBlock { Text = "Tên profile:", Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 0, 0, 6) };
            var txt = new TextBox
            {
                Height = 28,
                FontSize = 12,
                Padding = new Thickness(6, 4, 6, 4),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1e222d")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d")),
                CaretBrush = Brushes.Lime
            };
            var btnOk = new Button { Content = "Tạo", Width = 80, Height = 28, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.Hand };
            btnOk.Click += (s2, e2) => { dialog.DialogResult = true; dialog.Close(); };
            txt.KeyDown += (s2, e2) => { if (e2.Key == Key.Enter) { dialog.DialogResult = true; dialog.Close(); } };

            sp.Children.Add(lbl);
            sp.Children.Add(txt);
            sp.Children.Add(btnOk);
            dialog.Content = sp;

            if (dialog.ShowDialog() == true)
            {
                var name = txt.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(name) && !System.Text.RegularExpressions.Regex.IsMatch(name, @"[\\/:*?""<>|]"))
                {
                    // Create directory
                    var path = WebNodeCacheHelper.GetProfileCachePath(name);
                    Directory.CreateDirectory(path);

                    // Refresh combo and select new
                    LoadWebProfiles();
                    for (int i = 0; i < CmbWebProfile.Items.Count; i++)
                    {
                        if (CmbWebProfile.Items[i] is ComboBoxItem ci && string.Equals(ci.Tag as string, name, StringComparison.OrdinalIgnoreCase))
                        {
                            CmbWebProfile.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }

        private void NavigateWebBrowser(string input)
        {
            if (_webBrowser?.CoreWebView2 == null) return;
            var trimmed = input?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmed)) return;

            string url;
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                url = trimmed;
            }
            else if (trimmed.Contains('.') && !trimmed.Contains(' '))
            {
                url = "https://" + trimmed;
            }
            else
            {
                url = $"https://www.google.com/search?q={Uri.EscapeDataString(trimmed)}";
            }

            TxtWebUrl.Text = url;
            _webBrowser.CoreWebView2.Navigate(url);
        }

        private void BtnWebGo_Click(object sender, RoutedEventArgs e) => NavigateWebBrowser(TxtWebUrl.Text);

        private void TxtWebUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // If suggest popup is open and item selected, use that
                if (_suggestPopup?.IsOpen == true && _suggestListBox?.SelectedItem is string selectedSuggest)
                {
                    TxtWebUrl.Text = selectedSuggest;
                    _suggestPopup.IsOpen = false;
                }
                NavigateWebBrowser(TxtWebUrl.Text);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _suggestPopup?.IsOpen == true)
            {
                _suggestPopup.IsOpen = false;
                e.Handled = true;
            }
            else if (_suggestPopup?.IsOpen == true && _suggestListBox != null)
            {
                if (e.Key == Key.Down)
                {
                    _suggestListBox.SelectedIndex = Math.Min(_suggestListBox.SelectedIndex + 1, _suggestListBox.Items.Count - 1);
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    _suggestListBox.SelectedIndex = Math.Max(_suggestListBox.SelectedIndex - 1, 0);
                    e.Handled = true;
                }
            }
        }

        private void TxtWebUrl_GotFocus(object sender, RoutedEventArgs e)
        {
            TxtWebUrl.SelectAll();
        }

        private void SetupSuggestPopup()
        {
            _suggestListBox = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x28, 0x2C, 0x34)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                MaxHeight = 240,
                FontSize = 12,
                Padding = new Thickness(0)
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(_suggestListBox, ScrollBarVisibility.Disabled);

            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(60, 100, 180, 255))));
            itemStyle.Triggers.Add(hoverTrigger);

            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(90, 100, 180, 255))));
            itemStyle.Triggers.Add(selectedTrigger);
            _suggestListBox.ItemContainerStyle = itemStyle;

            // Click on suggestion → navigate
            _suggestListBox.MouseLeftButtonUp += (s, e) =>
            {
                if (_suggestListBox.SelectedItem is string sel)
                {
                    TxtWebUrl.Text = sel;
                    _suggestPopup!.IsOpen = false;
                    NavigateWebBrowser(sel);
                }
            };

            _suggestPopup = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = TxtWebUrl,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade,
                Child = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x28, 0x2C, 0x34)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(0, 0, 8, 8),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 12, ShadowDepth = 3, Opacity = 0.3 },
                    Child = _suggestListBox
                }
            };

            // Bind popup width
            TxtWebUrl.SizeChanged += (s, e) =>
            {
                if (_suggestPopup != null)
                    _suggestPopup.Width = TxtWebUrl.ActualWidth + 40; // +40 for border padding
            };

            // Debounced TextChanged for Google Suggest
            _suggestDebounceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _suggestDebounceTimer.Tick += async (s, e) =>
            {
                _suggestDebounceTimer.Stop();
                await FetchGoogleSuggestionsAsync(TxtWebUrl.Text);
            };

            TxtWebUrl.TextChanged += (s, e) =>
            {
                if (TxtWebUrl.IsFocused && !string.IsNullOrWhiteSpace(TxtWebUrl.Text))
                {
                    _suggestDebounceTimer?.Stop();
                    _suggestDebounceTimer?.Start();
                }
                else
                {
                    if (_suggestPopup != null) _suggestPopup.IsOpen = false;
                }
            };
        }

        private async Task FetchGoogleSuggestionsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || _suggestListBox == null || _suggestPopup == null) return;

            try
            {
                using var http = new System.Net.Http.HttpClient();
                http.Timeout = TimeSpan.FromSeconds(3);
                var response = await http.GetStringAsync($"https://suggestqueries.google.com/complete/search?client=firefox&q={Uri.EscapeDataString(query)}");

                // Parse JSON: ["query", ["suggestion1", "suggestion2", ...]]
                var suggestions = new List<string>();
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(response);
                    if (doc.RootElement.GetArrayLength() > 1)
                    {
                        foreach (var item in doc.RootElement[1].EnumerateArray())
                        {
                            var s = item.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) suggestions.Add(s);
                            if (suggestions.Count >= 8) break;
                        }
                    }
                }
                catch { }

                Dispatcher.Invoke(() =>
                {
                    _suggestListBox.Items.Clear();
                    if (suggestions.Count > 0 && TxtWebUrl.IsFocused)
                    {
                        foreach (var s in suggestions) _suggestListBox.Items.Add(s);
                        _suggestPopup.IsOpen = true;
                    }
                    else
                    {
                        _suggestPopup.IsOpen = false;
                    }
                });
            }
            catch
            {
                Dispatcher.Invoke(() => { if (_suggestPopup != null) _suggestPopup.IsOpen = false; });
            }
        }

        #endregion

        #region Secondary Images Slots

        private void BtnAddSecondary_Click(object sender, RoutedEventArgs e)
        {
            // Find first empty slot
            int emptySlot = -1;
            for (int i = 0; i < 4; i++)
            {
                if (!_secondaryImages[i].HasImage)
                {
                    emptySlot = i;
                    break;
                }
            }

            if (emptySlot == -1)
            {
                MessageBox.Show("Đã đủ 4 ảnh phụ. Hãy xóa ảnh cũ trước.", "Ảnh phụ", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Title = "Chọn ảnh phụ",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*",
                CheckFileExists = true,
                Multiselect = true
            };

            if (dlg.ShowDialog(this) == true)
            {
                int slotIdx = emptySlot;
                foreach (var file in dlg.FileNames)
                {
                    if (slotIdx >= 4) break;

                    // Find next empty slot
                    while (slotIdx < 4 && _secondaryImages[slotIdx].HasImage)
                        slotIdx++;
                    if (slotIdx >= 4) break;

                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(file);
                        bmp.EndInit();
                        bmp.Freeze();

                        _secondaryImages[slotIdx].Bitmap = bmp;
                        _secondaryImages[slotIdx].FilePath = file;
                        _secondaryImages[slotIdx].IsSelected = true;
                        slotIdx++;
                    }
                    catch { }
                }

                RefreshAllSlotsUI();
            }
        }

        private void Slot_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                border.Focus();
                if (idx < 0 || idx >= 4) return;

                if (_secondaryImages[idx].HasImage)
                {
                    // Toggle selection
                    _secondaryImages[idx].IsSelected = !_secondaryImages[idx].IsSelected;
                    RefreshSlotUI(idx);
                    UpdateSecondaryInfo();
                }
                else
                {
                    // Empty slot — open file dialog for this specific slot
                    var dlg = new OpenFileDialog
                    {
                        Title = "Chọn ảnh phụ",
                        Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All Files|*.*",
                        CheckFileExists = true,
                        Multiselect = false
                    };

                    if (dlg.ShowDialog(this) == true)
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.UriSource = new Uri(dlg.FileName);
                            bmp.EndInit();
                            bmp.Freeze();

                            _secondaryImages[idx].Bitmap = bmp;
                            _secondaryImages[idx].FilePath = dlg.FileName;
                            _secondaryImages[idx].IsSelected = true;
                        }
                        catch { }
                        RefreshAllSlotsUI();
                    }
                }

                e.Handled = true;
            }
        }

        private void SlotRemove_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                if (idx >= 0 && idx < 4)
                {
                    _secondaryImages[idx].Bitmap = null;
                    _secondaryImages[idx].FilePath = null;
                    _secondaryImages[idx].IsSelected = false;
                    RefreshAllSlotsUI();
                }
                e.Handled = true;
            }
        }

        private void Slot_MouseEnter(object sender, MouseEventArgs e)
        {
            _hoveredImageContainer = sender as FrameworkElement;
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                if (idx >= 0 && idx < 4)
                {
                    // Hover glow effect
                    border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
                    border.BorderThickness = new Thickness(2);

                    // Show remove button if has image
                    if (_secondaryImages[idx].HasImage)
                    {
                        _slotRemoves[idx].Visibility = Visibility.Visible;
                        _slotRemovesWv[idx].Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void Slot_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_hoveredImageContainer == sender)
            {
                _hoveredImageContainer = null;
            }
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                if (idx >= 0 && idx < 4)
                {
                    // Reset border based on selection state
                    if (_secondaryImages[idx].HasImage && _secondaryImages[idx].IsSelected)
                    {
                        border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
                        border.BorderThickness = new Thickness(2);
                    }
                    else
                    {
                        border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"));
                        border.BorderThickness = new Thickness(1.5);
                    }

                    // Hide remove button
                    _slotRemoves[idx].Visibility = Visibility.Collapsed;
                    _slotRemovesWv[idx].Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ImgPreview_MouseEnter(object sender, MouseEventArgs e)
        {
            _hoveredImageContainer = sender as FrameworkElement;
        }

        private void ImgPreview_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_hoveredImageContainer == sender)
            {
                _hoveredImageContainer = null;
            }
        }

        private async void LayerAiDialog_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (_hoveredImageContainer != null)
                {
                    var bmp = await GetImageFromClipboardAsync();
                    if (bmp != null)
                    {
                        e.Handled = true;
                        ProcessHoveredSlotPaste(bmp);
                    }
                }
            }
        }

        private void ProcessHoveredSlotPaste(BitmapSource bitmap)
        {
            if (_hoveredImageContainer == null) return;
            FlashSlotBorder(_hoveredImageContainer);

            string name = _hoveredImageContainer.Name ?? "";
            bool isMainImage = name == "ImgPreview" || name == "ImgPreviewWv";

            if (isMainImage)
            {
                try
                {
                    int layerW = _activeLayer.Width;
                    int layerH = _activeLayer.Height;
                    var resized = ResizeBitmapHighQuality(bitmap, layerW, layerH, uniformToFill: true);
                    
                    var stride = layerW * 4;
                    var pixels = new byte[stride * layerH];
                    resized.CopyPixels(pixels, stride, 0);
                    
                    _activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, layerW, layerH), pixels, stride, 0);
                    _activeLayer.InvalidateThumbnail();
                    
                    UpdatePreviewImage();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to update main image from paste: {ex.Message}");
                }
            }
            else
            {
                // It is a slot border
                int idx = -1;
                if (_hoveredImageContainer is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int tagIdx))
                {
                    idx = tagIdx;
                }
                else
                {
                    if (name.EndsWith("0")) idx = 0;
                    else if (name.EndsWith("1")) idx = 1;
                    else if (name.EndsWith("2")) idx = 2;
                    else if (name.EndsWith("3")) idx = 3;
                }

                if (idx >= 0 && idx < 4)
                {
                    _secondaryImages[idx].Bitmap = bitmap;
                    _secondaryImages[idx].FilePath = null;
                    _secondaryImages[idx].IsSelected = true;
                    RefreshAllSlotsUI();
                }
            }
        }

        private async Task<BitmapSource?> GetImageFromClipboardAsync()
        {
            try
            {
                var dataObject = Clipboard.GetDataObject();
                if (dataObject == null) return null;

                // 0a. Check FileContents (direct original compressed file data from clipboard)
                if (dataObject.GetDataPresent("FileContents"))
                {
                    var bmp = GetImageFromFileContentsCOM(dataObject);
                    if (bmp != null) return bmp;
                }

                // 0. Check DeviceIndependentBitmap / DeviceIndependentBitmapV5
                if (dataObject.GetDataPresent("DeviceIndependentBitmap"))
                {
                    var data = dataObject.GetData("DeviceIndependentBitmap");
                    if (data is MemoryStream ms)
                    {
                        var bmp = GetImageFromDIB(ms);
                        if (bmp != null) return bmp;
                    }
                }
                if (dataObject.GetDataPresent("DeviceIndependentBitmapV5"))
                {
                    var data = dataObject.GetData("DeviceIndependentBitmapV5");
                    if (data is MemoryStream ms)
                    {
                        var bmp = GetImageFromDIB(ms);
                        if (bmp != null) return bmp;
                    }
                }

                // 1. Check Bitmap directly
                if (dataObject.GetDataPresent(DataFormats.Bitmap))
                {
                    if (dataObject.GetData(DataFormats.Bitmap) is BitmapSource bmp)
                    {
                        return bmp;
                    }
                }

                // 2. Check FileDrop
                if (dataObject.GetDataPresent(DataFormats.FileDrop))
                {
                    if (dataObject.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    {
                        var filePath = files[0];
                        if (File.Exists(filePath))
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                            bmp.EndInit();
                            bmp.Freeze();
                            return bmp;
                        }
                    }
                }

                // 3. Check HTML Format / URL
                string? url = null;
                string? sourcePageUrl = null;
                if (dataObject.GetDataPresent(DataFormats.Html))
                {
                    if (dataObject.GetData(DataFormats.Html) is string htmlText)
                    {
                        var sourceUrlMatch = System.Text.RegularExpressions.Regex.Match(htmlText, @"SourceURL:\s*([^\r\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (sourceUrlMatch.Success)
                        {
                            sourcePageUrl = sourceUrlMatch.Groups[1].Value.Trim();
                        }

                        string? foundUrl = null;
                        var attributes = new[] { "data-src", "data-original", "data-srcset", "srcset", "src" };
                        foreach (var attr in attributes)
                        {
                            var regex = new System.Text.RegularExpressions.Regex(
                                attr + @"\s*=\s*[""']([^""' >]+)[""']", 
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            var match = regex.Match(htmlText);
                            if (match.Success)
                            {
                                foundUrl = match.Groups[1].Value;
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(foundUrl))
                        {
                            url = foundUrl;
                            url = System.Net.WebUtility.HtmlDecode(url);
                        }
                    }
                }

                if (string.IsNullOrEmpty(url) && dataObject.GetDataPresent(DataFormats.Text))
                {
                    url = dataObject.GetData(DataFormats.Text) as string;
                }

                if (!string.IsNullOrWhiteSpace(url))
                {
                    url = url.Trim();
                    if (url.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                    {
                        return CreateBitmapFromBase64(url);
                    }

                    Uri? uri = null;
                    if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
                    {
                        uri = absoluteUri;
                    }
                    else
                    {
                        string? pageUrl = sourcePageUrl;
                        if (string.IsNullOrWhiteSpace(pageUrl))
                        {
                            Microsoft.Web.WebView2.Wpf.WebView2? activeWv = null;
                            if (_activeTab == ActiveTab.WebBrowser) activeWv = _webBrowser;
                            else if (_activeTab == ActiveTab.WebView) activeWv = _dynamicWebView;
                            if (activeWv?.Source != null)
                            {
                                pageUrl = activeWv.Source.ToString();
                            }
                        }
                        if (string.IsNullOrWhiteSpace(pageUrl)) pageUrl = _node?.LayerAiWebUrl;
                        if (string.IsNullOrWhiteSpace(pageUrl)) pageUrl = TxtWebUrl?.Text;

                        if (!string.IsNullOrWhiteSpace(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri))
                        {
                            if (Uri.TryCreate(baseUri, url, out var resolvedUri))
                            {
                                uri = resolvedUri;
                            }
                        }
                    }

                    if (uri != null)
                    {
                        if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                        {
                            return await DownloadImageAsync(uri);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get image from clipboard: {ex.Message}");
            }
            return null;
        }

        private void RefreshAllSlotsUI()
        {
            for (int i = 0; i < 4; i++)
                RefreshSlotUI(i);
            UpdateSecondaryInfo();
        }

        private void RefreshSlotUI(int idx)
        {
            if (idx < 0 || idx >= 4) return;

            var item = _secondaryImages[idx];

            if (item.HasImage)
            {
                // Normal slots
                _slotImages[idx].Source = item.Bitmap;
                _slotPlaceholders[idx].Visibility = Visibility.Collapsed;
                _slotChecks[idx].Visibility = item.IsSelected ? Visibility.Visible : Visibility.Collapsed;

                // Expanded slots
                _slotImagesWv[idx].Source = item.Bitmap;
                _slotPlaceholdersWv[idx].Visibility = Visibility.Collapsed;

                var activeBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
                var normalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"));

                if (item.IsSelected)
                {
                    _slotBorders[idx].BorderBrush = activeBrush;
                    _slotBorders[idx].BorderThickness = new Thickness(2);
                    _slotBordersWv[idx].BorderBrush = activeBrush;
                    _slotBordersWv[idx].BorderThickness = new Thickness(2);
                }
                else
                {
                    _slotBorders[idx].BorderBrush = normalBrush;
                    _slotBorders[idx].BorderThickness = new Thickness(1.5);
                    _slotBordersWv[idx].BorderBrush = normalBrush;
                    _slotBordersWv[idx].BorderThickness = new Thickness(1.5);
                }
            }
            else
            {
                // Normal slots
                _slotImages[idx].Source = null;
                _slotPlaceholders[idx].Visibility = Visibility.Visible;
                _slotChecks[idx].Visibility = Visibility.Collapsed;
                _slotRemoves[idx].Visibility = Visibility.Collapsed;

                // Expanded slots
                _slotImagesWv[idx].Source = null;
                _slotPlaceholdersWv[idx].Visibility = Visibility.Visible;
                _slotRemovesWv[idx].Visibility = Visibility.Collapsed;

                var normalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"));
                _slotBorders[idx].BorderBrush = normalBrush;
                _slotBorders[idx].BorderThickness = new Thickness(1.5);
                _slotBordersWv[idx].BorderBrush = normalBrush;
                _slotBordersWv[idx].BorderThickness = new Thickness(1.5);
            }
        }

        private void UpdateSecondaryInfo()
        {
            int total = _secondaryImages.Count(s => s.HasImage);
            int selected = _secondaryImages.Count(s => s.HasImage && s.IsSelected);
            TxtSecondaryInfo.Text = total > 0 ? $"Ảnh phụ: {selected}/{total} đã chọn" : "";
        }

        #endregion

        #region Aspect Ratio & Preview

        private void CmbAspectRatio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelCustomSize == null) return;
            PanelCustomSize.Visibility = (CmbAspectRatio.SelectedIndex == 6) ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreviewImage();
        }

        private void TxtCustomSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreviewImage();
        }

        private void UpdatePreviewImage()
        {
            if (ImgPreview == null || _activeLayer == null) return;

            try
            {
                BitmapSource sourceImg = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;

                // Luôn tính bounds trực tiếp từ sourceImg (tránh mismatch giữa ContentBounds cache và OriginalTransformBitmap)
                var bounds = GetLayerContentBounds(sourceImg);
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    int x = Math.Clamp((int)bounds.X, 0, sourceImg.PixelWidth - 1);
                    int y = Math.Clamp((int)bounds.Y, 0, sourceImg.PixelHeight - 1);
                    int w = Math.Clamp((int)Math.Ceiling(bounds.Width), 1, sourceImg.PixelWidth - x);
                    int h = Math.Clamp((int)Math.Ceiling(bounds.Height), 1, sourceImg.PixelHeight - y);
                    if (w > 0 && h > 0 && (x > 0 || y > 0 || w < sourceImg.PixelWidth || h < sourceImg.PixelHeight))
                    {
                        sourceImg = new CroppedBitmap(sourceImg, new Int32Rect(x, y, w, h));
                    }
                }
                BitmapSource processedImg;

                int selectedIndex = CmbAspectRatio.SelectedIndex;
                if (selectedIndex == 0)
                {
                    processedImg = DrawPreviewImage(sourceImg, null, null, null, drawCheckerboard: true);
                }
                else if (selectedIndex == 6)
                {
                    int targetW = int.TryParse(TxtCustomWidth.Text, out var w) ? w : 512;
                    int targetH = int.TryParse(TxtCustomHeight.Text, out var h) ? h : 512;
                    processedImg = DrawPreviewImage(sourceImg, null, targetW, targetH, drawCheckerboard: true);
                }
                else
                {
                    double ratio = selectedIndex switch
                    {
                        1 => 16.0 / 9.0,
                        2 => 4.0 / 3.0,
                        3 => 1.0,
                        4 => 3.0 / 4.0,
                        5 => 9.0 / 16.0,
                        _ => 1.0
                    };
                    processedImg = DrawPreviewImage(sourceImg, ratio, null, null, drawCheckerboard: true);
                }

                ImgPreview.Source = processedImg;
                if (ImgPreviewWv != null)
                {
                    ImgPreviewWv.Source = processedImg;
                }
            }
            catch { }
        }

        #endregion

        #region Send AI (BtnSend_Click)

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            BtnSend.IsEnabled = false;
            BtnCancel.IsEnabled = false;
            BtnSend.Content = "Đang xử lý...";

            var destinationParent = _activeLayer.ParentLayer ?? _activeLayer;
            var placeholders = new List<EditorLayer>();

            try
            {
                BitmapSource sourceImg = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;
                var bounds = GetLayerContentBounds(sourceImg);
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    int x = Math.Clamp((int)bounds.X, 0, sourceImg.PixelWidth - 1);
                    int y = Math.Clamp((int)bounds.Y, 0, sourceImg.PixelHeight - 1);
                    int w = Math.Clamp((int)Math.Ceiling(bounds.Width), 1, sourceImg.PixelWidth - x);
                    int h = Math.Clamp((int)Math.Ceiling(bounds.Height), 1, sourceImg.PixelHeight - y);
                    if (w > 0 && h > 0 && (x > 0 || y > 0 || w < sourceImg.PixelWidth || h < sourceImg.PixelHeight))
                    {
                        sourceImg = new CroppedBitmap(sourceImg, new Int32Rect(x, y, w, h));
                    }
                }
                BitmapSource processedImg;

                double? targetRatio = null;
                int? customW = null;
                int? customH = null;

                int selectedIndex = CmbAspectRatio.SelectedIndex;
                if (selectedIndex == 0)
                {
                    processedImg = DrawPreviewImage(sourceImg, null, null, null, drawCheckerboard: false);
                }
                else if (selectedIndex == 6)
                {
                    customW = int.TryParse(TxtCustomWidth.Text, out var w) ? w : 512;
                    customH = int.TryParse(TxtCustomHeight.Text, out var h) ? h : 512;
                    processedImg = DrawPreviewImage(sourceImg, null, customW, customH, drawCheckerboard: false);
                }
                else
                {
                    targetRatio = selectedIndex switch
                    {
                        1 => 16.0 / 9.0,
                        2 => 4.0 / 3.0,
                        3 => 1.0,
                        4 => 3.0 / 4.0,
                        5 => 9.0 / 16.0,
                        _ => 1.0
                    };
                    processedImg = DrawPreviewImage(sourceImg, targetRatio, null, null, drawCheckerboard: false);
                }

                // Convert main image to base64
                var b64 = await Task.Run(() => ImageProcessorHelper.ToBase64(processedImg));

                // Bind main image base64 output
                var cropBase64Port = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropBase64", StringComparison.OrdinalIgnoreCase));
                if (cropBase64Port != null) cropBase64Port.UserValueOverride = b64;

                var activePromptText = GetActivePromptText();
                _node.ProcessorPrompt = activePromptText;
                var promptPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "prompt", StringComparison.OrdinalIgnoreCase));
                if (promptPort != null) promptPort.UserValueOverride = activePromptText;

                int batchSize = CmbBatchSize.SelectedIndex + 1;
                _node.PromptSize = batchSize;
                var sizePort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "promptSize", StringComparison.OrdinalIgnoreCase));
                if (sizePort != null) sizePort.UserValueOverride = batchSize.ToString();

                var widthPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropWidth", StringComparison.OrdinalIgnoreCase));
                if (widthPort != null) widthPort.UserValueOverride = processedImg.PixelWidth.ToString();

                var heightPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropHeight", StringComparison.OrdinalIgnoreCase));
                if (heightPort != null) heightPort.UserValueOverride = processedImg.PixelHeight.ToString();

                string execId = Guid.NewGuid().ToString("N");
                _node.LastExecutionId = execId;
                var execIdPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "executionId", StringComparison.OrdinalIgnoreCase));
                if (execIdPort != null) execIdPort.UserValueOverride = execId;

                PendingExecutionIds.Enqueue(execId);

                _node.IsVerticalMode = (selectedIndex == 4 || selectedIndex == 5);
                var aspectPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "aspectRatio", StringComparison.OrdinalIgnoreCase));
                if (aspectPort != null)
                {
                    string aspectStr = selectedIndex switch
                    {
                        1 => "16:9",
                        2 => "4:3",
                        3 => "1:1",
                        4 => "3:4",
                        5 => "9:16",
                        6 => "Free",
                        _ => "Default"
                    };
                    aspectPort.UserValueOverride = aspectStr;
                }

                // *** NEW: Collect secondary images base64 and set listBase64 output ***
                await CollectAndSetListBase64Async();

                // Refresh outputs list in node dialog immediately to reflect the generated overrides
                RefreshRelatedNodeDialogs();

                // Create variant placeholders in parent's ChildLayers before starting workflow execution
                for (int i = 0; i < batchSize; i++)
                {
                    var placeholder = new EditorLayer(destinationParent.Width, destinationParent.Height, $"Layer AI {destinationParent.ChildLayers.Count + 1}");
                    placeholder.ParentLayer = destinationParent;
                    placeholder.IsLoading = true;
                    placeholder.StartLoadingTimer();
                    destinationParent.ChildLayers.Add(placeholder);
                    placeholders.Add(placeholder);
                }

                // Notify HasChildren changed on parent so collapse toggle appears
                destinationParent.OnPropertyChanged(nameof(EditorLayer.HasChildren));

                // Refresh main panel immediately to render loading placeholders in the layers ListBox
                var editorPanel = FindVisualChild<ImageEditorPanel>(this.Owner);
                editorPanel?.RefreshLayersList();

                // Close dialog immediately — workflow runs in background, results applied to placeholders
                DialogResult = true;
                Close();

                // Capture references needed for background processing
                var activeLayerRef = _activeLayer;
                var docRef = _doc;
                var nodeRef = _node;
                var hostRef = _host;
                var ownerRef = this.Owner;

                // Fire-and-forget: run workflow, then process results on UI thread
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Run workflow on background thread via reflection
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            var vm = hostRef.ViewModel;
                            if (vm != null)
                            {
                                var vmType = vm.GetType();
                                var startTestMethod = vmType.GetMethod("StartTest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (startTestMethod != null)
                                {
                                    if (startTestMethod.Invoke(vm, null) is Task t)
                                    {
                                        await t;
                                    }
                                }
                            }
                        }).Task.Unwrap();

                        // Process results on UI thread
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                // Refresh outputs list again to show final outputs/execution IDs
                                RefreshRelatedNodeDialogs();

                                // Resolve AI outputs
                                if (string.IsNullOrWhiteSpace(nodeRef.RenderNodeId) || string.IsNullOrWhiteSpace(nodeRef.RenderNodeOutputKey))
                                {
                                    CleanupPlaceholders(placeholders, destinationParent, ownerRef);
                                    return;
                                }

                                // Tìm executionId thực tế được chạy trong workflow
                                string actualRunId = execId;
                                if (WorkflowExecutionService.ExecutionIdMapping.TryGetValue(execId, out var mappedRunId))
                                {
                                    actualRunId = mappedRunId;
                                }

                                var raw = ResolveFromHistoricalCache(nodeRef.RenderNodeId, nodeRef.RenderNodeOutputKey, actualRunId);
                                if (string.IsNullOrWhiteSpace(raw))
                                {
                                    raw = ResolveFromNodeIfAny(hostRef, nodeRef.RenderNodeId, nodeRef.RenderNodeOutputKey);
                                }

                                // Dọn dẹp cache của lần chạy này để tránh rò rỉ RAM
                                WorkflowExecutionService.ExecutionIdMapping.TryRemove(execId, out _);
                                WorkflowExecutionService.ScopedOutputsHistoricalCache.TryRemove(actualRunId, out _);

                                var childPrefix = actualRunId + ":";
                                var childrenKeys = WorkflowExecutionService.ScopedOutputsHistoricalCache.Keys
                                    .Where(k => k.StartsWith(childPrefix, StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                                foreach (var childKey in childrenKeys)
                                {
                                    WorkflowExecutionService.ScopedOutputsHistoricalCache.TryRemove(childKey, out _);
                                }

                                if (string.IsNullOrWhiteSpace(raw))
                                {
                                    CleanupPlaceholders(placeholders, destinationParent, ownerRef);
                                    return;
                                }

                                raw = raw.Trim();
                                List<string> list = new List<string>();
                                if (raw.StartsWith("["))
                                {
                                    try
                                    {
                                        list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
                                    }
                                    catch
                                    {
                                        var inner = raw.Trim();
                                        if (inner.StartsWith("[")) inner = inner.Substring(1);
                                        if (inner.EndsWith("]")) inner = inner.Substring(0, inner.Length - 1);
                                        var parts = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                         .Select(p => p.Trim().Trim('"'))
                                                         .Where(p => !string.IsNullOrWhiteSpace(p))
                                                         .ToList();
                                        list = parts.Count > 0 ? parts : new List<string> { raw };
                                    }
                                }
                                else
                                {
                                    list = new List<string> { raw };
                                }

                                if (list.Count == 0)
                                {
                                    CleanupPlaceholders(placeholders, destinationParent, ownerRef);
                                    return;
                                }

                                int countAdded = 0;
                                int placeholderIndex = 0;
                                foreach (var entry in list)
                                {
                                    if (string.IsNullOrWhiteSpace(entry)) continue;

                                    BitmapImage? bmp = CreateBitmapFromUrlOrFile(entry.Trim());
                                    if (bmp == null)
                                    {
                                        bmp = CreateBitmapFromBase64(entry.Trim());
                                    }

                                    if (bmp != null)
                                    {
                                        EditorLayer childLayer;
                                        if (placeholderIndex < placeholders.Count)
                                        {
                                            childLayer = placeholders[placeholderIndex];
                                            childLayer.IsLoading = false;
                                            childLayer.StopLoadingTimer();
                                        }
                                        else
                                        {
                                            childLayer = new EditorLayer(destinationParent.Width, destinationParent.Height, $"Layer AI {destinationParent.ChildLayers.Count + 1}");
                                            childLayer.ParentLayer = destinationParent;
                                            destinationParent.ChildLayers.Add(childLayer);
                                        }

                                        ProcessAndApplyAiImage(childLayer, bmp, activeLayerRef, bounds, targetRatio, customW, customH);
                                        countAdded++;
                                        placeholderIndex++;
                                    }
                                }

                                // Remove any unused placeholders
                                for (int i = placeholders.Count - 1; i >= placeholderIndex; i--)
                                {
                                    destinationParent.ChildLayers.Remove(placeholders[i]);
                                }

                                if (countAdded > 0)
                                {
                                    destinationParent.ActiveChildLayer = destinationParent.ChildLayers.Last();
                                    docRef.ActiveLayer = destinationParent.ActiveChildLayer;

                                    foreach (var child in destinationParent.ChildLayers)
                                    {
                                        child.IsActive = (child == destinationParent.ActiveChildLayer);
                                        child.IsSelected = (child == destinationParent.ActiveChildLayer);
                                    }
                                    destinationParent.IsActive = false;
                                    destinationParent.IsSelected = false;
                                }

                                // Refresh panel to show AI results and trigger re-composite
                                var panel = FindVisualChild<ImageEditorPanel>(ownerRef);
                                panel?.RefreshLayersList();
                                panel?.OnDocumentModified();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine("AI result processing error: " + ex.Message);
                                CleanupPlaceholders(placeholders, destinationParent, ownerRef);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("AI workflow error: " + ex.Message);
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            CleanupPlaceholders(placeholders, destinationParent, ownerRef);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                // Pre-workflow error (e.g. image processing) — mark placeholders as error
                CleanupPlaceholders(placeholders, destinationParent, this.Owner);

                MessageBox.Show("Lỗi thực thi AI: " + ex.Message, "AI Layer Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetButtons();
            }
        }

        /// <summary>
        /// Collect selected secondary images, convert to base64, and set the listBase64 output.
        /// </summary>
        private async Task CollectAndSetListBase64Async()
        {
            var selectedImages = _secondaryImages
                .Where(s => s.HasImage && s.IsSelected && s.Bitmap != null)
                .Select(s => s.Bitmap!)
                .ToList();

            if (selectedImages.Count == 0)
            {
                // Set empty array
                var listBase64Port = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "listBase64", StringComparison.OrdinalIgnoreCase));
                if (listBase64Port != null) listBase64Port.UserValueOverride = "[]";
                return;
            }

            var base64List = new List<string>();
            foreach (var bmp in selectedImages)
            {
                var b64 = await Task.Run(() => ImageProcessorHelper.ToBase64(bmp));
                base64List.Add(b64);
            }

            // Serialize as JSON array
            var jsonArray = System.Text.Json.JsonSerializer.Serialize(base64List);
            var port = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "listBase64", StringComparison.OrdinalIgnoreCase));
            if (port != null) port.UserValueOverride = jsonArray;
        }

        #endregion

        #region Helpers

        private void CleanupPlaceholders(List<EditorLayer> placeholders, EditorLayer parent, Window? owner)
        {
            foreach (var placeholder in placeholders)
            {
                // Mark as error state instead of removing — user can delete manually
                placeholder.IsLoading = false;
                placeholder.StopLoadingTimer(isError: true);
                placeholder.IsLoadingError = true;
                placeholder.Name = placeholder.Name + " (Lỗi)";
            }
            if (owner != null)
            {
                var panel = FindVisualChild<ImageEditorPanel>(owner);
                panel?.RefreshLayersList();
            }
        }

        private void ResetButtons()
        {
            BtnSend.IsEnabled = true;
            BtnCancel.IsEnabled = true;
            BtnSend.Content = "✨ Gửi AI";
        }

        private static BitmapSource DrawPreviewImage(BitmapSource src, double? targetRatio, int? customW, int? customH, bool drawCheckerboard)
        {
            int srcW = src.PixelWidth;
            int srcH = src.PixelHeight;

            int newW = srcW;
            int newH = srcH;
            double currentRatio = (double)srcW / srcH;

            BitmapSource imageToDraw = src;

            if (customW.HasValue && customH.HasValue)
            {
                newW = customW.Value;
                newH = customH.Value;
                var scale = new ScaleTransform((double)newW / srcW, (double)newH / srcH);
                imageToDraw = new TransformedBitmap(src, scale);
            }
            else if (targetRatio.HasValue)
            {
                double ratio = targetRatio.Value;
                if (currentRatio > ratio)
                {
                    newH = (int)Math.Ceiling(srcW / ratio);
                }
                else if (currentRatio < ratio)
                {
                    newW = (int)Math.Ceiling(srcH * ratio);
                }
            }

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                if (drawCheckerboard)
                {
                    var brush = Application.Current.TryFindResource("PsDarkCheckeredBrush") as Brush ?? Brushes.Black;
                    dc.DrawRectangle(brush, null, new Rect(0, 0, newW, newH));
                }
                else
                {
                    dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, newW, newH));
                }

                // Center the image within the padded dimensions
                double x = (newW - imageToDraw.PixelWidth) / 2.0;
                double y = (newH - imageToDraw.PixelHeight) / 2.0;
                dc.DrawImage(imageToDraw, new Rect(x, y, imageToDraw.PixelWidth, imageToDraw.PixelHeight));
            }

            var rtb = new RenderTargetBitmap(newW, newH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        private static string? ResolveFromNodeIfAny(IWorkflowEditorHost host, string? nodeId, string? key)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key)) return null;
            var src = host.ViewModel?.Nodes?.FirstOrDefault(n =>
                string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (src == null) return null;
            var value = NodeDataPanelService.ResolveDynamicValueByKey(src, key);
            if (string.IsNullOrWhiteSpace(value) || value == "—") return null;
            return value;
        }

        private static BitmapImage? CreateBitmapFromUrlOrFile(string value)
        {
            try
            {
                value = value.Trim();
                if (value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    value = new Uri(value).LocalPath;
                }

                if (File.Exists(value))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(value);
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                else if (Uri.TryCreate(value, UriKind.Absolute, out var uriResult) &&
                         (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = uriResult;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { }
            return null;
        }

        private static BitmapImage? CreateBitmapFromBase64(string base64)
        {
            try
            {
                string data = base64.Contains(',') ? base64.Split(',')[1] : base64;
                byte[] bytes = Convert.FromBase64String(data);
                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { }
            return null;
        }

        private static string? ResolveFromHistoricalCache(string nodeId, string key, string executionId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(executionId)) return null;

            // 1. Thử lấy trực tiếp bằng executionId chính xác
            if (WorkflowExecutionService.ScopedOutputsHistoricalCache.TryGetValue(executionId, out var byNode) &&
                byNode.TryGetValue(nodeId, out var byKey) &&
                byKey.TryGetValue(key, out var value))
            {
                if (value != "—" && !string.IsNullOrWhiteSpace(value)) return value;
            }

            // 2. Nếu không thấy, duyệt qua cache tìm các run con (ví dụ: executionId + ":dispatch-..." hoặc executionId + ":at-manual-...")
            var prefix = executionId + ":";
            foreach (var kv in WorkflowExecutionService.ScopedOutputsHistoricalCache)
            {
                if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.Value.TryGetValue(nodeId, out var childKey) &&
                        childKey.TryGetValue(key, out var childVal))
                    {
                        if (childVal != "—" && !string.IsNullOrWhiteSpace(childVal)) return childVal;
                    }
                }
            }

            return null;
        }

        private void LoadSavedSettings()
        {
            if (_node == null) return;

            // Load prompt
            var savedPrompt = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "prompt", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedPrompt))
            {
                TxtPrompt.Text = savedPrompt;
            }
            else
            {
                TxtPrompt.Text = _node.ProcessorPrompt ?? string.Empty;
            }

            // Load batch size (promptSize)
            var savedSize = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "promptSize", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedSize) && int.TryParse(savedSize, out var bSize))
            {
                CmbBatchSize.SelectedIndex = Math.Clamp(bSize - 1, 0, 3);
            }
            else
            {
                CmbBatchSize.SelectedIndex = Math.Clamp(_node.PromptSize - 1, 0, 3);
            }

            // Load aspect ratio
            var savedAspect = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "aspectRatio", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedAspect))
            {
                CmbAspectRatio.SelectedIndex = savedAspect switch
                {
                    "16:9" => 1,
                    "4:3" => 2,
                    "1:1" => 3,
                    "3:4" => 4,
                    "9:16" => 5,
                    "Free" => 6,
                    _ => 0
                };
            }

            // Load custom width and height
            BitmapSource sourceImg = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;
            var bounds = GetLayerContentBounds(sourceImg);

            var savedWidth = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropWidth", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedWidth))
            {
                TxtCustomWidth.Text = savedWidth;
            }
            else
            {
                if (!bounds.IsEmpty && bounds.Width > 0)
                {
                    TxtCustomWidth.Text = ((int)bounds.Width).ToString();
                }
            }

            var savedHeight = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropHeight", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedHeight))
            {
                TxtCustomHeight.Text = savedHeight;
            }
            else
            {
                if (!bounds.IsEmpty && bounds.Height > 0)
                {
                    TxtCustomHeight.Text = ((int)bounds.Height).ToString();
                }
            }

            // Restore active tab selection
            var savedTabStr = _node.LayerAiActiveTab ?? "Prompt";
            var savedTab = savedTabStr switch
            {
                "WebView" => ActiveTab.WebView,
                "WebBrowser" => ActiveTab.WebBrowser,
                _ => ActiveTab.Prompt
            };
            
            // Force SwitchToTab to run fully by setting _activeTab to a different state first
            _activeTab = (savedTab == ActiveTab.Prompt) ? ActiveTab.WebBrowser : ActiveTab.Prompt;
            SwitchToTab(savedTab);
        }

        private void RefreshRelatedNodeDialogs()
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win is BaseNodeDialog baseDialog && baseDialog.DataContext is FlowMy.ViewModels.BaseNodeDialogViewModel dialogVm && dialogVm.Node == _node)
                {
                    baseDialog.Dispatcher.Invoke(() => baseDialog.RefreshOutputsUI());
                }
            }
        }

        private static Rect GetLayerContentBounds(BitmapSource bitmap)
        {
            if (bitmap == null) return Rect.Empty;
            try
            {
                int w = bitmap.PixelWidth;
                int h = bitmap.PixelHeight;
                if (w <= 0 || h <= 0) return Rect.Empty;

                int stride = w * 4;
                byte[] pixels = new byte[stride * h];
                bitmap.CopyPixels(pixels, stride, 0);

                int minX = w, maxX = 0, minY = h, maxY = 0;
                bool found = false;

                for (int y = 0; y < h; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        byte alpha = pixels[rowOffset + x * 4 + 3];
                        if (alpha > 5) // Ignore transparent edges
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                            found = true;
                        }
                    }
                }

                if (!found)
                {
                    return new Rect(0, 0, w, h);
                }

                return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            catch
            {
                return new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                {
                    return t;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private void ProcessAndApplyAiImage(
            EditorLayer childLayer,
            BitmapSource aiBmp,
            EditorLayer activeLayer,
            Rect originalBounds,
            double? targetRatio,
            int? customW,
            int? customH)
        {
            // 1. Get original crop dimensions
            BitmapSource sourceImg = activeLayer.OriginalTransformBitmap ?? activeLayer.Bitmap;
            int srcW = (int)originalBounds.Width;
            int srcH = (int)originalBounds.Height;
            if (srcW <= 0) srcW = sourceImg.PixelWidth;
            if (srcH <= 0) srcH = sourceImg.PixelHeight;

            // 2. Compute the newW and newH (the size of the image sent to AI)
            int newW = srcW;
            int newH = srcH;
            double currentRatio = (double)srcW / srcH;

            if (customW.HasValue && customH.HasValue)
            {
                newW = customW.Value;
                newH = customH.Value;
            }
            else if (targetRatio.HasValue)
            {
                double ratio = targetRatio.Value;
                if (currentRatio > ratio)
                {
                    newH = (int)Math.Ceiling(srcW / ratio);
                }
                else if (currentRatio < ratio)
                {
                    newW = (int)Math.Ceiling(srcH * ratio);
                }
            }

            // 3. Resize AI image to newW x newH with High Quality
            BitmapSource resizedAi = ResizeBitmapHighQuality(aiBmp, newW, newH, uniformToFill: !customW.HasValue);

            // 4. Calculate crop offsets (where the original crop region is located in the newW x newH image)
            double xOffset = 0;
            double yOffset = 0;
            BitmapSource croppedAiRegion;

            if (customW.HasValue && customH.HasValue)
            {
                // For custom size, the image was scaled (stretched) to customW x customH.
                // So we just take the whole image, and it will be scaled back to srcW x srcH.
                croppedAiRegion = resizedAi;
            }
            else
            {
                xOffset = (newW - srcW) / 2.0;
                yOffset = (newH - srcH) / 2.0;
                int cropX = Math.Clamp((int)Math.Round(xOffset), 0, newW - 1);
                int cropY = Math.Clamp((int)Math.Round(yOffset), 0, newH - 1);
                int cropW = Math.Clamp(srcW, 1, newW - cropX);
                int cropH = Math.Clamp(srcH, 1, newH - cropY);

                croppedAiRegion = new CroppedBitmap(resizedAi, new Int32Rect(cropX, cropY, cropW, cropH));
            }

            // 5. If it needs to be scaled back to original bounds (for custom size, etc.)
            if (croppedAiRegion.PixelWidth != srcW || croppedAiRegion.PixelHeight != srcH)
            {
                croppedAiRegion = ResizeBitmapHighQuality(croppedAiRegion, srcW, srcH, uniformToFill: false);
            }

            // 6. Mask the AI pixels using the original layer's alpha channel to preserve lasso/polygon shapes
            var converted = croppedAiRegion;
            if (croppedAiRegion.Format != PixelFormats.Bgra32)
            {
                converted = new FormatConvertedBitmap(croppedAiRegion, PixelFormats.Bgra32, null, 0);
            }

            double parentX = 0;
            double parentY = 0;
            if (activeLayer.OriginalTransformBitmap != null)
            {
                parentX = activeLayer.ContentBounds.X;
                parentY = activeLayer.ContentBounds.Y;
            }
            int posX = (int)Math.Clamp(parentX + originalBounds.X, 0, childLayer.Width - 1);
            int posY = (int)Math.Clamp(parentY + originalBounds.Y, 0, childLayer.Height - 1);
            int finalW = Math.Clamp(srcW, 1, childLayer.Width - posX);
            int finalH = Math.Clamp(srcH, 1, childLayer.Height - posY);

            // Resize converted to match final clamped bounds if needed
            if (converted.PixelWidth != finalW || converted.PixelHeight != finalH)
            {
                converted = ResizeBitmapHighQuality(converted, finalW, finalH, uniformToFill: false);
            }

            var aiPixels = new byte[finalW * 4 * finalH];
            converted.CopyPixels(aiPixels, finalW * 4, 0);

            var maskPixels = new byte[finalW * 4 * finalH];
            activeLayer.Bitmap.CopyPixels(new Int32Rect(posX, posY, finalW, finalH), maskPixels, finalW * 4, 0);

            for (int i = 0; i < aiPixels.Length; i += 4)
            {
                aiPixels[i + 3] = maskPixels[i + 3];
            }

            var maskedBmp = new WriteableBitmap(finalW, finalH, 96, 96, PixelFormats.Bgra32, null);
            maskedBmp.WritePixels(new Int32Rect(0, 0, finalW, finalH), aiPixels, finalW * 4, 0);

            // Render into the destination layer's WriteableBitmap
            var drawingVisual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(drawingVisual, BitmapScalingMode.HighQuality);
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // Draw the transparent background (clear the layer)
                drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, childLayer.Width, childLayer.Height));
                // Draw the masked processed AI cropped region at the exact original position
                drawingContext.DrawImage(maskedBmp, new Rect(posX, posY, finalW, finalH));
            }

            var rtb = new RenderTargetBitmap(childLayer.Width, childLayer.Height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);

            var finalBmp = new FormatConvertedBitmap(rtb, PixelFormats.Bgra32, null, 0);
            var stride = childLayer.Width * 4;
            var pixels = new byte[stride * childLayer.Height];
            finalBmp.CopyPixels(pixels, stride, 0);

            childLayer.Bitmap.WritePixels(new Int32Rect(0, 0, childLayer.Width, childLayer.Height), pixels, stride, 0);

            // Set OriginalTransformBitmap and ContentBounds so that transform tool works properly
            childLayer.OriginalTransformBitmap = new WriteableBitmap(maskedBmp);
            childLayer.ContentBounds = new Rect(posX, posY, finalW, finalH);

            childLayer.InvalidateThumbnail();
        }

        private static BitmapSource ResizeBitmapHighQuality(BitmapSource source, int targetWidth, int targetHeight, bool uniformToFill = false)
        {
            if (source.PixelWidth == targetWidth && source.PixelHeight == targetHeight)
            {
                return source;
            }

            int drawW = targetWidth;
            int drawH = targetHeight;
            double x = 0;
            double y = 0;

            if (uniformToFill)
            {
                double scale = Math.Max((double)targetWidth / source.PixelWidth, (double)targetHeight / source.PixelHeight);
                drawW = (int)Math.Ceiling(source.PixelWidth * scale);
                drawH = (int)Math.Ceiling(source.PixelHeight * scale);
                x = (targetWidth - drawW) / 2.0;
                y = (targetHeight - drawH) / 2.0;
            }

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
                dc.DrawImage(source, new Rect(x, y, drawW, drawH));
            }

            var rtb = new RenderTargetBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        #region Two-Way Drag and Drop (WPF <-> WebView2)

        private void SetupDragAndDrop()
        {
            // --- WPF-to-WebView Drag Source Setup ---
            var dragSources = new FrameworkElement[]
            {
                ImgPreview, ImgPreviewWv,
                SlotBorder0, SlotBorder1, SlotBorder2, SlotBorder3,
                SlotBorderWv0, SlotBorderWv1, SlotBorderWv2, SlotBorderWv3
            };

            foreach (var src in dragSources)
            {
                if (src == null) continue;
                src.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    _dragStartPoint = e.GetPosition(null);
                };
                src.MouseMove += (s, e) =>
                {
                    if (e.LeftButton == MouseButtonState.Pressed)
                    {
                        var currentPosition = e.GetPosition(null);
                        if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                            Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                        {
                            StartDragDrop(s);
                        }
                    }
                };
            }

            // --- WebView-to-WPF Drop Target Setup ---
            var dropTargets = new FrameworkElement[]
            {
                ImgPreview, ImgPreviewWv,
                SlotBorder0, SlotBorder1, SlotBorder2, SlotBorder3,
                SlotBorderWv0, SlotBorderWv1, SlotBorderWv2, SlotBorderWv3
            };

            foreach (var target in dropTargets)
            {
                if (target == null) continue;
                target.AllowDrop = true;
                target.DragOver += (s, e) =>
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                };
                target.Drop += Control_Drop;
            }
        }

        private void StartDragDrop(object sender)
        {
            try
            {
                BitmapSource? bitmap = null;
                string tempFileName = "dragged_image";

                if (sender is Image img)
                {
                    bitmap = img.Source as BitmapSource;
                    if (img.Name == "ImgPreview" || img.Name == "ImgPreviewWv")
                    {
                        tempFileName = "Main";
                    }
                    else
                    {
                        var name = img.Name ?? "";
                        int slotNum = 1;
                        if (name.EndsWith("0")) slotNum = 1;
                        else if (name.EndsWith("1")) slotNum = 2;
                        else if (name.EndsWith("2")) slotNum = 3;
                        else if (name.EndsWith("3")) slotNum = 4;
                        tempFileName = $"{slotNum}.Slot";
                    }
                }
                else if (sender is Border border)
                {
                    if (border.Name == "ImgPreview" || border.Name == "ImgPreviewWv")
                    {
                        bitmap = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;
                        tempFileName = "Main";
                    }
                    else if (border.Tag is string tagStr && int.TryParse(tagStr, out int idx) && idx >= 0 && idx < 4)
                    {
                        bitmap = _secondaryImages[idx].Bitmap;
                        tempFileName = $"{idx + 1}.Slot";
                    }
                }

                if (bitmap == null) return;

                // Save bitmap to unique temporary file to prevent access conflicts
                var uniqueName = $"{tempFileName}_{Guid.NewGuid():N}.png";
                var tempPath = Path.Combine(Path.GetTempPath(), uniqueName);
                using (var fileStream = new FileStream(tempPath, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(fileStream);
                }

                // Create DataObject
                var data = new DataObject();
                var fileList = new System.Collections.Specialized.StringCollection { tempPath };
                data.SetFileDropList(fileList);

                // Set beautiful drag ghost image!
                SetDragImage(data, bitmap);

                // Execute drag drop
                DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting drag drop: {ex.Message}");
            }
        }

        private void SlotBorder_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
                {
                    if (idx >= 0 && idx < 4)
                    {
                        _secondaryImages[idx].Bitmap = null;
                        _secondaryImages[idx].FilePath = null;
                        _secondaryImages[idx].IsSelected = false;
                        RefreshAllSlotsUI();
                    }
                    e.Handled = true;
                }
            }
        }

        private async void Control_Drop(object sender, DragEventArgs e)
        {
            try
            {
                BitmapSource? droppedBitmap = await GetImageFromDragEventArgsAsync(e);

                if (droppedBitmap == null) return;

                Dispatcher.Invoke(() =>
                {
                    ProcessDroppedImage(sender, droppedBitmap);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error on drop: {ex.Message}");
            }
        }

        private async Task<BitmapSource?> GetImageFromDragEventArgsAsync(DragEventArgs e)
        {
            // 0a. Check FileContents (direct original compressed file data from Chrome/Edge)
            if (e.Data.GetDataPresent("FileContents"))
            {
                var bmp = GetImageFromFileContentsCOM(e.Data);
                if (bmp != null) return bmp;
            }

            // 0. Check DeviceIndependentBitmap / DeviceIndependentBitmapV5 (direct raw pixels from Chrome/Edge)
            if (e.Data.GetDataPresent("DeviceIndependentBitmap"))
            {
                var data = e.Data.GetData("DeviceIndependentBitmap");
                if (data is MemoryStream ms)
                {
                    var bmp = GetImageFromDIB(ms);
                    if (bmp != null) return bmp;
                }
            }
            if (e.Data.GetDataPresent("DeviceIndependentBitmapV5"))
            {
                var data = e.Data.GetData("DeviceIndependentBitmapV5");
                if (data is MemoryStream ms)
                {
                    var bmp = GetImageFromDIB(ms);
                    if (bmp != null) return bmp;
                }
            }

            // 1. Check Bitmap directly
            if (e.Data.GetDataPresent(DataFormats.Bitmap))
            {
                if (e.Data.GetData(DataFormats.Bitmap) is BitmapSource bmp)
                {
                    return bmp;
                }
            }

            // 2. Check FileDrop
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    var filePath = files[0];
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                            bmp.EndInit();
                            bmp.Freeze();
                            return bmp;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error loading dropped file: {ex.Message}");
                        }
                    }
                }
            }

            // 3. Try to extract URL from various formats
            string? url = null;
            string? sourcePageUrl = null;

            // 3a. Check HTML Format (HTML snippet dragged from WebView2/Browser)
            if (e.Data.GetDataPresent(DataFormats.Html))
            {
                if (e.Data.GetData(DataFormats.Html) is string htmlText)
                {
                    // Try to extract SourceURL from HTML format header
                    var sourceUrlMatch = System.Text.RegularExpressions.Regex.Match(htmlText, @"SourceURL:\s*([^\r\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (sourceUrlMatch.Success)
                    {
                        sourcePageUrl = sourceUrlMatch.Groups[1].Value.Trim();
                    }

                    // Try to find source attributes in order of preference (real lazy-loaded/responsive image url first)
                    string? foundUrl = null;
                    var attributes = new[] { "data-src", "data-original", "data-srcset", "srcset", "src" };
                    foreach (var attr in attributes)
                    {
                        var regex = new System.Text.RegularExpressions.Regex(
                            attr + @"\s*=\s*[""']([^""' >]+)[""']", 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        var match = regex.Match(htmlText);
                        if (match.Success)
                        {
                            foundUrl = match.Groups[1].Value;
                            if (attr == "srcset" || attr == "data-srcset")
                            {
                                var parts = foundUrl.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                var firstUrl = parts.FirstOrDefault(p => p.StartsWith("http") || p.StartsWith("//") || p.StartsWith("/"));
                                if (firstUrl != null) foundUrl = firstUrl;
                            }
                            break;
                        }
                    }

                    // Fallback to href if it points to an image
                    if (string.IsNullOrEmpty(foundUrl))
                    {
                        var linkMatch = System.Text.RegularExpressions.Regex.Match(htmlText, @"href\s*=\s*[""']([^""' >]+\.(?:png|jpg|jpeg|gif|webp|bmp))[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (linkMatch.Success)
                        {
                            foundUrl = linkMatch.Groups[1].Value;
                        }
                    }

                    // Fallback to CSS background-image url()
                    if (string.IsNullOrEmpty(foundUrl))
                    {
                        var bgMatch = System.Text.RegularExpressions.Regex.Match(htmlText, @"url\(\s*['""]?([^'"")]+?)['""]?\s*\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (bgMatch.Success)
                        {
                            foundUrl = bgMatch.Groups[1].Value;
                        }
                    }

                    if (!string.IsNullOrEmpty(foundUrl))
                    {
                        url = foundUrl;
                        url = System.Net.WebUtility.HtmlDecode(url);
                    }
                }
            }

            // 3b. Check UniformResourceLocator (WebView2 drops URL as a MemoryStream)
            if (string.IsNullOrEmpty(url) && e.Data.GetDataPresent("UniformResourceLocator"))
            {
                var data = e.Data.GetData("UniformResourceLocator");
                if (data is MemoryStream ms)
                {
                    byte[] bytes = ms.ToArray();
                    string rawUrl = System.Text.Encoding.ASCII.GetString(bytes).Trim('\0');
                    if (!string.IsNullOrWhiteSpace(rawUrl))
                    {
                        url = rawUrl;
                    }
                }
            }

            // 3c. Check Text / UnicodeText
            if (string.IsNullOrEmpty(url))
            {
                if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    url = e.Data.GetData(DataFormats.Text) as string;
                }
                else if (e.Data.GetDataPresent(DataFormats.UnicodeText))
                {
                    url = e.Data.GetData(DataFormats.UnicodeText) as string;
                }
            }

            // 4. Resolve URL (either Web URL or Data URL)
            if (!string.IsNullOrWhiteSpace(url))
            {
                url = url.Trim();
                if (url.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateBitmapFromBase64(url);
                }

                Uri? uri = null;
                if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
                {
                    uri = absoluteUri;
                }
                else
                {
                    // Relative URL resolution based on base browser page URL
                    string? pageUrl = sourcePageUrl;
                    if (string.IsNullOrWhiteSpace(pageUrl))
                    {
                        Microsoft.Web.WebView2.Wpf.WebView2? activeWv = null;
                        if (_activeTab == ActiveTab.WebBrowser) activeWv = _webBrowser;
                        else if (_activeTab == ActiveTab.WebView) activeWv = _dynamicWebView;
                        if (activeWv?.Source != null)
                        {
                            pageUrl = activeWv.Source.ToString();
                        }
                    }
                    if (string.IsNullOrWhiteSpace(pageUrl)) pageUrl = _node?.LayerAiWebUrl;
                    if (string.IsNullOrWhiteSpace(pageUrl)) pageUrl = TxtWebUrl?.Text;

                    if (!string.IsNullOrWhiteSpace(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri))
                    {
                        if (Uri.TryCreate(baseUri, url, out var resolvedUri))
                        {
                            uri = resolvedUri;
                        }
                    }
                }

                if (uri != null)
                {
                    if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    {
                        return await DownloadImageAsync(uri);
                    }
                    else if (uri.Scheme == "data")
                    {
                        return CreateBitmapFromBase64(url);
                    }
                    else if (uri.Scheme == Uri.UriSchemeFile)
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.UriSource = uri;
                            bmp.EndInit();
                            bmp.Freeze();
                            return bmp;
                        }
                        catch { }
                    }
                }
            }

            return null;
        }

        private static void SetDragImage(DataObject dataObject, BitmapSource source)
        {
            try
            {
                // 1. Determine size (max 128x128)
                int maxW = 128;
                int maxH = 128;
                double ratio = (double)source.PixelWidth / source.PixelHeight;
                int targetW, targetH;
                if (ratio > 1)
                {
                    targetW = maxW;
                    targetH = (int)(maxW / ratio);
                }
                else
                {
                    targetH = maxH;
                    targetW = (int)(maxH * ratio);
                }
                if (targetW <= 0) targetW = 1;
                if (targetH <= 0) targetH = 1;

                // 2. Resize BitmapSource to target size
                var resized = ResizeBitmapHighQuality(source, targetW, targetH, uniformToFill: false);

                // 3. Convert to 32bpp ARGB GDI Bitmap and get Hbitmap
                IntPtr hBitmap = IntPtr.Zero;
                using (var ms = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(resized));
                    encoder.Save(ms);
                    using (var gdiBmp = new System.Drawing.Bitmap(ms))
                    {
                        hBitmap = gdiBmp.GetHbitmap();
                    }
                }

                if (hBitmap != IntPtr.Zero)
                {
                    var helper = (IDragSourceHelper)new DragDropHelper();
                    var pshdi = new SHDRAGIMAGE
                    {
                        sizeDragImage = new SIZE { cx = targetW, cy = targetH },
                        ptOffset = new POINT { x = targetW / 2, y = targetH / 2 },
                        hbmpDragImage = hBitmap,
                        crColorKey = 0x00000000
                    };
                    
                    helper.InitializeFromBitmap(ref pshdi, (System.Runtime.InteropServices.ComTypes.IDataObject)dataObject);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set drag image: {ex.Message}");
            }
        }

        private static BitmapSource? GetImageFromDIB(MemoryStream dibStream)
        {
            try
            {
                byte[] dibBytes = dibStream.ToArray();
                if (dibBytes.Length < 40) return null; // BITMAPINFOHEADER is at least 40 bytes

                // Read BITMAPINFOHEADER fields
                int headerSize = BitConverter.ToInt32(dibBytes, 0);
                int width = BitConverter.ToInt32(dibBytes, 4);
                int height = BitConverter.ToInt32(dibBytes, 8);
                short planes = BitConverter.ToInt16(dibBytes, 12);
                short bitCount = BitConverter.ToInt16(dibBytes, 14);
                int compression = BitConverter.ToInt32(dibBytes, 16);
                int imageSize = BitConverter.ToInt32(dibBytes, 20);
                int colorsUsed = BitConverter.ToInt32(dibBytes, 32);

                // Calculate header sizes and offsets
                int colorTableSize = 0;
                if (bitCount <= 8)
                {
                    colorTableSize = (colorsUsed > 0 ? colorsUsed : (1 << bitCount)) * 4;
                }
                else if (compression == 3) // BI_BITFIELDS
                {
                    colorTableSize = 12; // 3 color masks (4 bytes each)
                }

                int pixelOffset = 14 + headerSize + colorTableSize;
                int totalFileSize = 14 + dibBytes.Length;

                byte[] bmpBytes = new byte[totalFileSize];
                
                // 1. Write BITMAPFILEHEADER
                // bfType (ASCII 'BM')
                bmpBytes[0] = 0x42;
                bmpBytes[1] = 0x4D;
                // bfSize
                Array.Copy(BitConverter.GetBytes(totalFileSize), 0, bmpBytes, 2, 4);
                // bfReserved1, bfReserved2 (0)
                bmpBytes[6] = 0;
                bmpBytes[7] = 0;
                bmpBytes[8] = 0;
                bmpBytes[9] = 0;
                // bfOffBits
                Array.Copy(BitConverter.GetBytes(pixelOffset), 0, bmpBytes, 10, 4);

                // 2. Copy the DIB bytes
                Array.Copy(dibBytes, 0, bmpBytes, 14, dibBytes.Length);

                // 3. Load using BmpBitmapDecoder
                using (var ms = new MemoryStream(bmpBytes))
                {
                    var decoder = new BmpBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count > 0)
                    {
                        var frame = decoder.Frames[0];
                        frame.Freeze();
                        return frame;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse DIB: {ex.Message}");
            }
            return null;
        }

        private void ProcessDroppedImage(object sender, BitmapSource bitmap)
        {
            bool isMainImage = false;
            if (sender is FrameworkElement fe && (fe.Name == "ImgPreview" || fe.Name == "ImgPreviewWv"))
            {
                isMainImage = true;
            }
 
            if (sender is FrameworkElement feContainer)
            {
                FlashSlotBorder(feContainer);
            }

            if (isMainImage)
            {
                try
                {
                    int layerW = _activeLayer.Width;
                    int layerH = _activeLayer.Height;
                    var resized = ResizeBitmapHighQuality(bitmap, layerW, layerH, uniformToFill: true);
                    
                    var stride = layerW * 4;
                    var pixels = new byte[stride * layerH];
                    resized.CopyPixels(pixels, stride, 0);
                    
                    _activeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, layerW, layerH), pixels, stride, 0);
                    _activeLayer.InvalidateThumbnail();
                    
                    UpdatePreviewImage();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to update main image from drop: {ex.Message}");
                }
                return;
            }

            // Otherwise, it is a slot drop
            int idx = -1;
            if (sender is FrameworkElement feSlot && feSlot.Tag is string tagStr && int.TryParse(tagStr, out int tagIdx))
            {
                idx = tagIdx;
            }
            else if (sender is FrameworkElement feSlot2)
            {
                var name = feSlot2.Name ?? "";
                if (name.EndsWith("0")) idx = 0;
                else if (name.EndsWith("1")) idx = 1;
                else if (name.EndsWith("2")) idx = 2;
                else if (name.EndsWith("3")) idx = 3;
            }

            if (idx >= 0 && idx < 4)
            {
                _secondaryImages[idx].Bitmap = bitmap;
                _secondaryImages[idx].FilePath = null;
                _secondaryImages[idx].IsSelected = true;
                RefreshAllSlotsUI();
            }
            else
            {
                int targetIdx = -1;
                for (int i = 0; i < 4; i++)
                {
                    if (!_secondaryImages[i].HasImage)
                    {
                        targetIdx = i;
                        break;
                    }
                }
                if (targetIdx == -1) targetIdx = 0;

                _secondaryImages[targetIdx].Bitmap = bitmap;
                _secondaryImages[targetIdx].FilePath = null;
                _secondaryImages[targetIdx].IsSelected = true;
                RefreshAllSlotsUI();
            }
        }

        private async void FlashSlotBorder(FrameworkElement slotContainer)
        {
            try
            {
                // Find parent border or border itself
                Border? border = slotContainer as Border;
                if (border == null && slotContainer is Image img)
                {
                    // Find the parent Border of the Image
                    border = img.Parent as Border;
                }

                if (border != null)
                {
                    var originalBrush = border.BorderBrush;
                    var originalThickness = border.BorderThickness;

                    // Flash vibrant green
                    var flashBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
                    border.BorderBrush = flashBrush;
                    border.BorderThickness = new Thickness(originalThickness.Left + 1);

                    // Blink: wait 150ms
                    await System.Threading.Tasks.Task.Delay(150);
                    border.BorderBrush = Brushes.Transparent;
                    await System.Threading.Tasks.Task.Delay(150);
                    
                    // Restore original
                    border.BorderBrush = originalBrush;
                    border.BorderThickness = originalThickness;
                }
            }
            catch { }
        }

        private async System.Threading.Tasks.Task<BitmapSource?> DownloadImageAsync(Uri uri)
        {
            try
            {
                // Find the active WebView2 instance to extract session cookies
                Microsoft.Web.WebView2.Wpf.WebView2? activeWv = null;
                if (_activeTab == ActiveTab.WebBrowser) activeWv = _webBrowser;
                else if (_activeTab == ActiveTab.WebView) activeWv = _dynamicWebView;

                using (var client = new System.Net.Http.HttpClient())
                {
                    // Add standard browser headers
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                    // Set Referer to the current page URL
                    string? pageUrl = null;
                    if (activeWv?.Source != null)
                    {
                        pageUrl = activeWv.Source.ToString();
                    }
                    if (string.IsNullOrWhiteSpace(pageUrl))
                    {
                        pageUrl = _node?.LayerAiWebUrl;
                    }
                    if (string.IsNullOrWhiteSpace(pageUrl))
                    {
                        pageUrl = TxtWebUrl?.Text;
                    }
                    
                    if (!string.IsNullOrWhiteSpace(pageUrl))
                    {
                        client.DefaultRequestHeaders.Referrer = new Uri(pageUrl);
                    }

                    // Extract and add cookies as raw header string (avoids CookieContainer exceptions)
                    if (activeWv?.CoreWebView2 != null)
                    {
                        try
                        {
                            var cookieManager = activeWv.CoreWebView2.CookieManager;
                            var cookies = await cookieManager.GetCookiesAsync(uri.ToString());
                            if (cookies != null && cookies.Count > 0)
                            {
                                var cookiePairs = cookies.Select(c => $"{c.Name}={c.Value}");
                                string cookieHeaderValue = string.Join("; ", cookiePairs);
                                client.DefaultRequestHeaders.Add("Cookie", cookieHeaderValue);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to get WebView2 cookies: {ex.Message}");
                        }
                    }

                    var data = await client.GetByteArrayAsync(uri);
                    using (var ms = new System.IO.MemoryStream(data))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to download dropped image: {ex.Message}");
                return null;
            }
        }

        private static BitmapSource? GetImageFromFileContentsCOM(System.Windows.IDataObject dataObject)
        {
            try
            {
                if (!(dataObject is System.Runtime.InteropServices.ComTypes.IDataObject comDataObject))
                    return null;

                // Get format ID for FileContents
                int formatId = System.Windows.DataFormats.GetDataFormat("FileContents").Id;
                if (formatId == 0) return null;

                var formatetc = new System.Runtime.InteropServices.ComTypes.FORMATETC
                {
                    cfFormat = (short)formatId,
                    dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,
                    lindex = 0, // first file
                    tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_ISTREAM | System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL
                };

                System.Runtime.InteropServices.ComTypes.STGMEDIUM medium;
                comDataObject.GetData(ref formatetc, out medium);
                
                if (medium.tymed == System.Runtime.InteropServices.ComTypes.TYMED.TYMED_ISTREAM && medium.unionmember != IntPtr.Zero)
                {
                    var stream = (System.Runtime.InteropServices.ComTypes.IStream)System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(medium.unionmember);
                    using (var ms = new MemoryStream())
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        IntPtr bytesReadPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(int));
                        try
                        {
                            do
                            {
                                stream.Read(buffer, buffer.Length, bytesReadPtr);
                                bytesRead = System.Runtime.InteropServices.Marshal.ReadInt32(bytesReadPtr);
                                if (bytesRead > 0)
                                {
                                    ms.Write(buffer, 0, bytesRead);
                                }
                            } while (bytesRead > 0);
                        }
                        finally
                        {
                            System.Runtime.InteropServices.Marshal.FreeHGlobal(bytesReadPtr);
                        }

                        ms.Position = 0;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
                else if (medium.tymed == System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL && medium.unionmember != IntPtr.Zero)
                {
                    IntPtr hGlobal = medium.unionmember;
                    IntPtr ptr = GlobalLock(hGlobal);
                    try
                    {
                        int size = GlobalSize(hGlobal);
                        if (size > 0)
                        {
                            byte[] bytes = new byte[size];
                            System.Runtime.InteropServices.Marshal.Copy(ptr, bytes, 0, size);
                            using (var ms = new MemoryStream(bytes))
                            {
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.StreamSource = ms;
                                bmp.EndInit();
                                bmp.Freeze();
                                return bmp;
                            }
                        }
                    }
                    finally
                    {
                        GlobalUnlock(hGlobal);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"COM FileContents retrieval failed: {ex.Message}");
            }
            return null;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern int GlobalSize(IntPtr hMem);

        private async System.Threading.Tasks.Task InjectDragDropInterceptorScriptAsync(Microsoft.Web.WebView2.Wpf.WebView2 webView)
        {
            if (webView?.CoreWebView2 == null) return;
            try
            {
                // Inject the JS script to run on every page load
                string script = @"
                    (function() {
                        // 1. Force draggable=true when user starts to drag or click-down on images and links
                        document.addEventListener('mousedown', function(e) {
                            let target = e.target;
                            while (target && target.tagName !== 'IMG' && target.tagName !== 'A') {
                                target = target.parentNode;
                            }
                            if (target) {
                                if (target.tagName === 'IMG' && target.getAttribute('draggable') !== 'true') {
                                    target.setAttribute('draggable', 'true');
                                } else if (target.tagName === 'A') {
                                    const img = target.querySelector('img');
                                    if (img && img.getAttribute('draggable') !== 'true') {
                                        img.setAttribute('draggable', 'true');
                                    }
                                    if (target.getAttribute('draggable') !== 'true') {
                                        target.setAttribute('draggable', 'true');
                                    }
                                }
                            }
                        }, true);

                        // 2. Intercept dragstart in capture phase to bypass target-level blockages
                        document.addEventListener('dragstart', function(e) {
                            let target = e.target;
                            while (target && target.tagName !== 'IMG' && target.tagName !== 'A') {
                                target = target.parentNode;
                            }
                            if (target) {
                                let imageUrl = '';
                                if (target.tagName === 'IMG') {
                                    imageUrl = target.src;
                                } else if (target.tagName === 'A') {
                                    const img = target.querySelector('img');
                                    if (img) {
                                        imageUrl = img.src;
                                    } else if (/\.(png|jpg|jpeg|gif|webp|bmp)/i.test(target.href)) {
                                        imageUrl = target.href;
                                    }
                                }
                                if (imageUrl) {
                                    try {
                                        const absoluteUrl = new URL(imageUrl, window.location.href).href;
                                        // Set data transfer details
                                        e.dataTransfer.effectAllowed = 'copyLink';
                                        e.dataTransfer.setData('text/plain', absoluteUrl);
                                        e.dataTransfer.setData('text/uri-list', absoluteUrl);
                                        
                                        // Stop propagation so React / SPA page scripts cannot block the drag
                                        e.stopPropagation();
                                    } catch (err) {
                                        console.error('Failed to resolve URL on dragstart:', err);
                                    }
                                }
                            }
                        }, true);
                    })();
                ";
                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to inject drag-drop interceptor script: {ex.Message}");
            }
        }
 
        #endregion
 
        #endregion
    }

    public static class LayerAiWebViewCache
    {
        private static readonly System.Collections.Generic.Dictionary<string, CachedWebViewState> _cache = new();

        public class CachedWebViewState
        {
            public Microsoft.Web.WebView2.Wpf.WebView2? DynamicWebView { get; set; }
            public Microsoft.Web.WebView2.Wpf.WebView2? WebBrowser { get; set; }
            public DateTime LastUsed { get; set; } = DateTime.Now;
            public System.Timers.Timer? SleepTimer { get; set; }
        }

        public static CachedWebViewState GetOrCreateState(string nodeId)
        {
            lock (_cache)
            {
                if (!_cache.TryGetValue(nodeId, out var state))
                {
                    state = new CachedWebViewState();
                    _cache[nodeId] = state;
                }
                state.LastUsed = DateTime.Now;

                // Stop sleep timer if it is running
                if (state.SleepTimer != null)
                {
                    state.SleepTimer.Stop();
                    state.SleepTimer.Dispose();
                    state.SleepTimer = null;
                }

                return state;
            }
        }

        public static void ReleaseToSleep(string nodeId)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(nodeId, out var state))
                {
                    state.LastUsed = DateTime.Now;

                    // Set a timer to put WebView2s to sleep after 10 minutes (600,000 ms)
                    state.SleepTimer?.Stop();
                    state.SleepTimer?.Dispose();

                    state.SleepTimer = new System.Timers.Timer(10 * 60 * 1000); // 10 minutes
                    state.SleepTimer.AutoReset = false;
                    state.SleepTimer.Elapsed += (s, e) =>
                    {
                        PutWebViewsToSleep(nodeId);
                    };
                    state.SleepTimer.Start();
                }
            }
        }

        private static void PutWebViewsToSleep(string nodeId)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(nodeId, out var state))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(async () =>
                    {
                        try
                        {
                            if (state.DynamicWebView?.CoreWebView2 != null)
                            {
                                await state.DynamicWebView.CoreWebView2.TrySuspendAsync();
                                System.Diagnostics.Debug.WriteLine($"WebView2 Dynamic UI suspended for node {nodeId}");
                            }
                            if (state.WebBrowser?.CoreWebView2 != null)
                            {
                                await state.WebBrowser.CoreWebView2.TrySuspendAsync();
                                System.Diagnostics.Debug.WriteLine($"WebView2 WebBrowser suspended for node {nodeId}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error suspending WebView2: {ex.Message}");
                        }
                    });
                }
            }
        }

        public static void DisposeAll(string nodeId)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(nodeId, out var state))
                {
                    state.SleepTimer?.Stop();
                    state.SleepTimer?.Dispose();

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            state.DynamicWebView?.Dispose();
                            state.WebBrowser?.Dispose();
                        }
                        catch { }
                    });
                    _cache.Remove(nodeId);
                }
            }
        }

        public static void DisposeAll()
        {
            lock (_cache)
            {
                var keys = System.Linq.Enumerable.ToList(_cache.Keys);
                foreach (var key in keys)
                {
                    DisposeAll(key);
                }
            }
        }
    }

    #region Drag Drop Ghost COM Interfaces & Helpers
    [ComImport]
    [Guid("DE5CB7E3-F38A-4818-B9C3-0D3D322B60CC")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDragSourceHelper
    {
        void InitializeFromBitmap(
            ref SHDRAGIMAGE pshdi,
            System.Runtime.InteropServices.ComTypes.IDataObject pDataObject);

        void InitializeFromWindow(
            IntPtr hwnd,
            ref POINT ppt,
            System.Runtime.InteropServices.ComTypes.IDataObject pDataObject);
    }

    [ComImport]
    [Guid("4657278A-411B-11d2-839A-00C04FD918D0")]
    public class DragDropHelper
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SHDRAGIMAGE
    {
        public SIZE sizeDragImage;
        public POINT ptOffset;
        public IntPtr hbmpDragImage;
        public int crColorKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE
    {
        public int cx;
        public int cy;
    }
    #endregion
}
