using FlowMy.Helpers;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using FlowMy.Views.NodeControls;
using CefSharp;
using CefSharp.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.Overlays
{
    public partial class LayerAiDialog : Window
    {
        #region Web Browser (CefSharp + Search + Profile)

        private static void InjectDragDropInterceptorScriptAsync(ChromiumWebBrowser webView)
        {
            if (webView == null) return;
            webView.FrameLoadEnd += (s, e) =>
            {
                if (e.Frame.IsMain)
                {
                    try
                    {
                        string script = @"
                            (function() {
                                window._isMouseDownOnImage = false;
                                
                                window.resetDragState = function() {
                                    window._isMouseDownOnImage = false;
                                    const lastEl = document.activeElement || document.body;
                                    const eventOptions = { bubbles: true, cancelable: true, view: window };
                                    
                                    const mouseUpEv = new MouseEvent('mouseup', eventOptions);
                                    const pointerUpEv = new PointerEvent('pointerup', eventOptions);
                                    const dragEndEv = new DragEvent('dragend', eventOptions);
                                    const dragLeaveEv = new DragEvent('dragleave', eventOptions);
                                    
                                    if (lastEl) {
                                        lastEl.dispatchEvent(mouseUpEv);
                                        lastEl.dispatchEvent(pointerUpEv);
                                        lastEl.dispatchEvent(dragLeaveEv);
                                        lastEl.dispatchEvent(dragEndEv);
                                    }
                                    
                                    document.dispatchEvent(mouseUpEv);
                                    document.dispatchEvent(pointerUpEv);
                                    document.dispatchEvent(dragLeaveEv);
                                    document.dispatchEvent(dragEndEv);
                                    
                                    window.dispatchEvent(mouseUpEv);
                                    window.dispatchEvent(pointerUpEv);
                                    
                                    const escDown = new KeyboardEvent('keydown', { key: 'Escape', code: 'Escape', keyCode: 27, which: 27, bubbles: true, cancelable: true });
                                    const escUp = new KeyboardEvent('keyup', { key: 'Escape', code: 'Escape', keyCode: 27, which: 27, bubbles: true, cancelable: true });
                                    
                                    if (lastEl) {
                                        lastEl.dispatchEvent(escDown);
                                        lastEl.dispatchEvent(escUp);
                                    }
                                    document.dispatchEvent(escDown);
                                    document.dispatchEvent(escUp);
                                    window.dispatchEvent(escDown);
                                    window.dispatchEvent(escUp);
                                };

                                function findTargetImage(el) {
                                    let curr = el;
                                    while (curr && curr !== document.body) {
                                        if (curr.tagName === 'IMG') return curr;
                                        if (curr.tagName === 'A') return curr;
                                        try {
                                            let bg = window.getComputedStyle(curr).backgroundImage;
                                            if (bg && bg !== 'none' && bg.includes('url')) return curr;
                                        } catch(err) {}
                                        curr = curr.parentNode;
                                    }
                                    return null;
                                }

                                function getImageUrl(target) {
                                    if (!target) return null;
                                    if (target.tagName === 'IMG') {
                                        return target.getAttribute('data-src') || target.getAttribute('data-original') || target.getAttribute('data-srcset') || target.src;
                                    }
                                    if (target.tagName === 'A') {
                                        return target.href;
                                    }
                                    try {
                                        let bg = window.getComputedStyle(target).backgroundImage;
                                        if (bg && bg !== 'none') {
                                            let m = bg.match(/url\((.*?)\)/i);
                                            if (m && m[1]) return m[1].replace(/['']/g, '');
                                        }
                                    } catch(err) {}
                                    return null;
                                }

                                function convertTargetToBase64(target) {
                                    try {
                                        if (target.tagName === 'IMG' && target.complete && target.naturalWidth > 0) {
                                            let canvas = document.createElement('canvas');
                                            canvas.width = target.naturalWidth;
                                            canvas.height = target.naturalHeight;
                                            let ctx = canvas.getContext('2d');
                                            ctx.drawImage(target, 0, 0);
                                            let dataUrl = canvas.toDataURL('image/png');
                                            if (dataUrl && dataUrl.startsWith('data:image')) {
                                                window.__lastDraggedBase64 = dataUrl;
                                                return dataUrl;
                                            }
                                        }
                                    } catch(err) {}
                                    return null;
                                }

                                document.addEventListener('mousedown', function(e) {
                                    let target = findTargetImage(e.target);
                                    if (target) {
                                        window._isMouseDownOnImage = true;
                                        if (target.tagName === 'IMG' && target.getAttribute('draggable') !== 'true') {
                                            target.setAttribute('draggable', 'true');
                                        } else if (target.tagName === 'A') {
                                            target.setAttribute('draggable', 'true');
                                        } else {
                                            target.setAttribute('draggable', 'true');
                                        }
                                        if (target.style.pointerEvents === 'none') {
                                            target.style.pointerEvents = 'auto';
                                        }
                                    }
                                }, true);

                                const resetFlag = function() {
                                    window._isMouseDownOnImage = false;
                                };
                                document.addEventListener('mouseup', resetFlag, true);
                                document.addEventListener('pointerup', resetFlag, true);
                                document.addEventListener('dragend', resetFlag, true);

                                const blockMoveEvents = function(e) {
                                    if (window._isMouseDownOnImage) {
                                        e.stopImmediatePropagation();
                                    }
                                };
                                document.addEventListener('mousemove', blockMoveEvents, true);
                                document.addEventListener('pointermove', blockMoveEvents, true);

                                document.addEventListener('dragstart', function(e) {
                                    let target = findTargetImage(e.target);
                                    if (target) {
                                        e.stopImmediatePropagation();
                                        
                                        let dataUrl = convertTargetToBase64(target);
                                        let imageUrl = getImageUrl(target);
                                        if (imageUrl) {
                                            try {
                                                let absoluteUrl = new URL(imageUrl, window.location.href).href;
                                                if (e.dataTransfer) {
                                                    e.dataTransfer.effectAllowed = 'copyLink';
                                                    e.dataTransfer.setData('text/plain', absoluteUrl);
                                                    e.dataTransfer.setData('text/uri-list', absoluteUrl);
                                                    e.dataTransfer.setData('URL', absoluteUrl);
                                                    if (dataUrl) {
                                                        e.dataTransfer.setData('text/html', '<img src=\'' + dataUrl + '\'/>');
                                                    }
                                                    e.preventDefault = function() {};
                                                }
                                            } catch (err) {
                                                console.error('Failed to resolve URL on dragstart:', err);
                                            }
                                        }
                                    }
                                }, true);

                                document.addEventListener('drag', function(e) {
                                    let target = findTargetImage(e.target);
                                    if (target) {
                                        e.stopImmediatePropagation();
                                    }
                                }, true);
                            })();
                        ";
                        e.Frame.ExecuteJavaScriptAsync(script);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to inject drag-drop interceptor script: {ex.Message}");
                    }
                }
            };
        }

        private void InitDynamicWebViewAsync()
        {
            try
            {
                var cacheState = LayerAiWebViewCache.GetOrCreateState(_node.Id);
                if (cacheState.DynamicWebView == null)
                {
                    if (!CefSharpEnvironmentManager.IsInitialized)
                    {
                        CefSharpEnvironmentManager.EnsureInitialized();
                    }

                    var webView = new ChromiumWebBrowser
                    {
                        RequestContext = CefSharpEnvironmentManager.CreateProfileRequestContext("DynamicUi_" + _node.Id),
                        AllowDrop = true
                    };
                    
                    WebViewContainer.Child = webView;
                    InjectDragDropInterceptorScriptAsync(webView);
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
                }

                _dynamicWebView = cacheState.DynamicWebView;
                HookActivityEvents(_dynamicWebView);
                RenderDynamicUi();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LayerAI DynamicWebView init failed: {ex.Message}");
            }
        }

        private void RenderDynamicUi()
        {
            if (_dynamicWebView == null) return;

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
                _dynamicWebView.LoadHtml(fullHtml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to render dynamic UI: {ex.Message}");
            }
        }

        private void InitWebBrowserAsync()
        {
            _webBrowserInitialized = true;

            try
            {
                // Load profile combo
                LoadWebProfiles();

                // Setup suggestion popup
                SetupSuggestPopup();

                // Load saved tabs
                LoadSavedWebTabs();

                // Render tab strip UI
                RefreshWebTabStrip();

                // Build initial split layout
                UpdateWebBrowserLayout();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LayerAI WebBrowser init failed: {ex.Message}");
                MessageBox.Show($"Lỗi khởi tạo trình duyệt Web: {ex.Message}", "Lỗi WebView2", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSavedWebTabs()
        {
            _webTabs.Clear();
            _splitMode = _node.LayerAiWebSplitMode ?? "Single";

            UpdateSplitButtonsHighlight();

            var cacheState = LayerAiWebViewCache.GetOrCreateState(_node.Id);
            if (cacheState.WebBrowsers != null && cacheState.WebBrowsers.Count > 0)
            {
                _splitMode = cacheState.SplitMode;
                _activeTabIdx = cacheState.ActiveTabIdx;
                foreach (var cachedTab in cacheState.WebBrowsers)
                {
                    var tab = new WebTabItem
                    {
                        WebView = cachedTab.WebView,
                        Url = cachedTab.Url,
                        Title = cachedTab.Title,
                        ProfileName = cachedTab.ProfileName
                    };
                    if (tab.WebView != null)
                    {
                        BindWebViewEvents(tab, tab.WebView);
                    }
                    _webTabs.Add(tab);
                }
            }
            else
            {
                try
                {
                    var json = _node.LayerAiWebTabsJson;
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var list = System.Text.Json.JsonSerializer.Deserialize<List<SerializedWebTab>>(json);
                        if (list != null && list.Count > 0)
                        {
                            foreach (var sTab in list)
                            {
                                _webTabs.Add(new WebTabItem
                                {
                                    Url = sTab.Url,
                                    ProfileName = sTab.ProfileName,
                                    Title = sTab.Title
                                });
                            }
                        }
                    }
                }
                catch { }

                if (_webTabs.Count == 0)
                {
                    var defaultUrl = _node.LayerAiWebUrl;
                    if (string.IsNullOrWhiteSpace(defaultUrl)) defaultUrl = "https://google.com";
                    _webTabs.Add(new WebTabItem
                    {
                        Url = defaultUrl,
                        ProfileName = _node.LayerAiCacheProfileName ?? "Shared",
                        Title = "New Tab"
                    });
                }
                _activeTabIdx = 0;
            }

            if (_activeTabIdx < 0 || _activeTabIdx >= _webTabs.Count)
            {
                _activeTabIdx = 0;
            }
        }

        private void SaveWebTabsState()
        {
            if (_node == null) return;

            _node.LayerAiWebSplitMode = _splitMode;
            
            var list = new List<SerializedWebTab>();
            foreach (var tab in _webTabs)
            {
                list.Add(new SerializedWebTab
                {
                    Url = tab.Url,
                    ProfileName = tab.ProfileName,
                    Title = tab.Title
                });
            }
            try
            {
                _node.LayerAiWebTabsJson = System.Text.Json.JsonSerializer.Serialize(list);
            }
            catch { }

            var cacheState = LayerAiWebViewCache.GetOrCreateState(_node.Id);
            cacheState.SplitMode = _splitMode;
            cacheState.ActiveTabIdx = _activeTabIdx;
            cacheState.WebBrowsers.Clear();
            foreach (var tab in _webTabs)
            {
                cacheState.WebBrowsers.Add(new LayerAiWebViewCache.CachedTabState
                {
                    WebView = tab.WebView,
                    Url = tab.Url,
                    Title = tab.Title,
                    ProfileName = tab.ProfileName
                });
            }
        }

        private void InitializeWebViewAfterLoading(WebTabItem tab, ChromiumWebBrowser webView)
        {
            try
            {
                webView.AllowDrop = true;
                webView.RequestContext = CefSharpEnvironmentManager.CreateProfileRequestContext(tab.ProfileName);
                InjectDragDropInterceptorScriptAsync(webView);

                var url = tab.Url;
                if (string.IsNullOrWhiteSpace(url)) url = "https://google.com";
                webView.LoadUrl(url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Deferred CefSharp initialization failed: {ex.Message}");
            }
        }

        private void BindWebViewEvents(WebTabItem tab, ChromiumWebBrowser webView)
        {
            webView.LoadingStateChanged += (s, e) =>
            {
                tab.IsLoading = e.IsLoading;
                Dispatcher.Invoke(() => {
                    if (!e.IsLoading)
                    {
                        try
                        {
                            tab.Url = webView.Address ?? tab.Url;
                            tab.Title = webView.Title ?? tab.Title;
                            if (string.IsNullOrWhiteSpace(tab.Title)) tab.Title = "New Tab";
                            if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count && _webTabs[_activeTabIdx] == tab)
                            {
                                TxtWebUrl.Text = tab.Url;
                                _node.LayerAiWebUrl = tab.Url;
                            }
                        }
                        catch { }
                    }
                    RefreshWebTabStrip();
                    UpdateNavigationButtons();
                    if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count && _webTabs[_activeTabIdx] == tab)
                    {
                        if (e.IsLoading) UrlLoadingIndicator.Visibility = Visibility.Visible;
                        else UrlLoadingIndicator.Visibility = Visibility.Collapsed;
                    }
                });
            };

            webView.MouseEnter += (s, e) =>
            {
                Dispatcher.Invoke(() => {
                    int idx = _webTabs.IndexOf(tab);
                    if (idx >= 0 && idx < _webTabs.Count)
                    {
                        FocusWebTab(idx);
                    }
                });
            };

            webView.GotFocus += (s, e) =>
            {
                Dispatcher.Invoke(() => {
                    int idx = _webTabs.IndexOf(tab);
                    if (idx >= 0 && idx < _webTabs.Count)
                    {
                        FocusWebTab(idx);
                    }
                });
            };
        }

        private void UpdateWebBrowserLayout()
        {
            WebBrowserContainer.Children.Clear();
            WebBrowserContainer.RowDefinitions.Clear();
            WebBrowserContainer.ColumnDefinitions.Clear();

            int visibleSlots = _splitMode switch
            {
                "Vertical" => 2,
                "Horizontal" => 2,
                "Grid" => 4,
                _ => 1
            };

            if (_splitMode == "Vertical")
            {
                WebBrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                WebBrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            else if (_splitMode == "Horizontal")
            {
                WebBrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                WebBrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }
            else if (_splitMode == "Grid")
            {
                WebBrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                WebBrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                WebBrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                WebBrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            for (int i = 0; i < visibleSlots; i++)
            {
                int tabIdx = (_splitMode == "Single") ? _activeTabIdx : i;
                UIElement content;
                bool isThisSlotActive = false;

                if (tabIdx >= 0 && tabIdx < _webTabs.Count)
                {
                    var tab = _webTabs[tabIdx];
                    bool needsInitialization = (tab.WebView == null);

                    if (needsInitialization)
                    {
                        var webView = new ChromiumWebBrowser
                        {
                            RequestContext = CefSharpEnvironmentManager.CreateProfileRequestContext(tab.ProfileName)
                        };
                        tab.WebView = webView;
                        
                        HookActivityEvents(webView);
                        BindWebViewEvents(tab, webView);
                    }

                    if (tab.WebView!.Parent is Panel parentPanel)
                    {
                        parentPanel.Children.Remove(tab.WebView);
                    }
                    else if (tab.WebView.Parent is Decorator parentDecorator)
                    {
                        parentDecorator.Child = null;
                    }
                    else if (tab.WebView.Parent is ContentControl cc)
                    {
                        cc.Content = null;
                    }

                    content = tab.WebView;
                    isThisSlotActive = (tabIdx == _activeTabIdx);

                    if (needsInitialization)
                    {
                        InitializeWebViewAfterLoading(tab, tab.WebView);
                    }
                }
                else
                {
                    content = CreatePlaceholderSlot();
                }

                var border = new Border
                {
                    BorderBrush = isThisSlotActive ? (FindResource("AccentColor") as Brush ?? Brushes.Lime) : (FindResource("BorderColor") as Brush ?? Brushes.DimGray),
                    BorderThickness = new Thickness(isThisSlotActive ? 2 : 1),
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(3),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15171e")),
                    AllowDrop = true,
                    Child = content
                };

                int targetTabIdx = tabIdx;
                border.PreviewMouseDown += (s, e) =>
                {
                    if (targetTabIdx >= 0 && targetTabIdx < _webTabs.Count)
                    {
                        FocusWebTab(targetTabIdx);
                    }
                };

                if (_splitMode == "Vertical")
                {
                    Grid.SetColumn(border, i);
                }
                else if (_splitMode == "Horizontal")
                {
                    Grid.SetRow(border, i);
                }
                else if (_splitMode == "Grid")
                {
                    Grid.SetRow(border, i / 2);
                    Grid.SetColumn(border, i % 2);
                }

                WebBrowserContainer.Children.Add(border);
            }

            if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count)
            {
                var activeTab = _webTabs[_activeTabIdx];
                TxtWebUrl.Text = activeTab.Url;
                UrlLoadingIndicator.Visibility = activeTab.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                UpdateNavigationButtons();
                SelectProfileInCombo(activeTab.ProfileName);
            }

            SaveWebTabsState();
        }

        private UIElement CreatePlaceholderSlot()
        {
            var grid = new Grid { Cursor = Cursors.Hand, Background = Brushes.Transparent };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            
            var plusText = new TextBlock
            {
                Text = "＋",
                FontSize = 24,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3a3f52")),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            
            var promptText = new TextBlock
            {
                Text = "Mở tab mới tại đây",
                Foreground = FindResource("TextMuted") as Brush ?? Brushes.Gray,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            stack.Children.Add(plusText);
            stack.Children.Add(promptText);
            grid.Children.Add(stack);

            grid.MouseLeftButtonDown += (s, e) =>
            {
                CreateNewWebTab();
            };

            return grid;
        }

        private void SelectProfileInCombo(string profileName)
        {
            if (CmbWebProfile == null) return;
            for (int i = 0; i < CmbWebProfile.Items.Count; i++)
            {
                if (CmbWebProfile.Items[i] is ComboBoxItem item && string.Equals(item.Tag as string, profileName, StringComparison.OrdinalIgnoreCase))
                {
                    CmbWebProfile.SelectedIndex = i;
                    return;
                }
            }
        }

        private void CreateNewWebTab(string url = "https://google.com")
        {
            var profile = _node.LayerAiCacheProfileName ?? "Shared";
            var newTab = new WebTabItem
            {
                Url = url,
                ProfileName = profile,
                Title = "New Tab"
            };
            _webTabs.Add(newTab);
            _activeTabIdx = _webTabs.Count - 1;
            
            RefreshWebTabStrip();
            UpdateWebBrowserLayout();
        }

        private void CloseWebTab(int idx)
        {
            if (idx < 0 || idx >= _webTabs.Count) return;

            var tab = _webTabs[idx];
            try
            {
                tab.WebView?.Dispose();
            }
            catch { }

            _webTabs.RemoveAt(idx);

            if (_webTabs.Count == 0)
            {
                CreateNewWebTab();
                return;
            }

            if (_activeTabIdx >= _webTabs.Count)
            {
                _activeTabIdx = _webTabs.Count - 1;
            }
            else if (_activeTabIdx == idx)
            {
                _activeTabIdx = Math.Max(0, idx - 1);
            }
            else if (_activeTabIdx > idx)
            {
                _activeTabIdx--;
            }

            RefreshWebTabStrip();
            UpdateWebBrowserLayout();
        }

        private void FocusWebTab(int idx)
        {
            if (idx < 0 || idx >= _webTabs.Count || _activeTabIdx == idx) return;

            _activeTabIdx = idx;

            if (_splitMode == "Single")
            {
                RefreshWebTabStrip();
                UpdateWebBrowserLayout();
            }
            else
            {
                UpdateActiveTabHighlightOnly();
            }
        }

        private void UpdateActiveTabHighlightOnly()
        {
            if (_webTabs == null || _activeTabIdx < 0 || _activeTabIdx >= _webTabs.Count) return;

            var activeTab = _webTabs[_activeTabIdx];
            if (TxtWebUrl != null) TxtWebUrl.Text = activeTab.Url;
            if (UrlLoadingIndicator != null) UrlLoadingIndicator.Visibility = activeTab.IsLoading ? Visibility.Visible : Visibility.Collapsed;
            UpdateNavigationButtons();
            SelectProfileInCombo(activeTab.ProfileName);

            RefreshWebTabStrip();

            var activeBrush = FindResource("AccentColor") as Brush ?? Brushes.Lime;
            var normalBrush = FindResource("BorderColor") as Brush ?? Brushes.DimGray;

            for (int i = 0; i < WebBrowserContainer.Children.Count; i++)
            {
                if (WebBrowserContainer.Children[i] is Border border)
                {
                    int tabIdx = (_splitMode == "Single") ? _activeTabIdx : i;
                    bool isThisSlotActive = (tabIdx == _activeTabIdx);

                    border.BorderBrush = isThisSlotActive ? activeBrush : normalBrush;
                    border.BorderThickness = new Thickness(isThisSlotActive ? 2 : 1);
                }
            }

            SaveWebTabsState();
        }

        private void RefreshWebTabStrip()
        {
            if (WebTabStripStackPanel == null) return;
            WebTabStripStackPanel.Children.Clear();

            for (int i = 0; i < _webTabs.Count; i++)
            {
                var tab = _webTabs[i];
                var isActive = (i == _activeTabIdx);
                var tabUi = CreateTabUi(tab, i, isActive);
                WebTabStripStackPanel.Children.Add(tabUi);
            }

            var newTabBtn = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15171e")),
                BorderBrush = FindResource("BorderColor") as Brush ?? Brushes.DimGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Width = 24,
                Height = 22,
                Margin = new Thickness(4, 2, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Bottom,
                ToolTip = "Mở tab mới"
            };
            var newTabTxt = new TextBlock
            {
                Text = "＋",
                Foreground = FindResource("TextMain") as Brush ?? Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            newTabBtn.Child = newTabTxt;
            newTabBtn.MouseLeftButtonDown += (s, e) =>
            {
                CreateNewWebTab();
            };
            newTabBtn.MouseEnter += (s, e) => { newTabBtn.BorderBrush = FindResource("AccentColor") as Brush ?? Brushes.Lime; };
            newTabBtn.MouseLeave += (s, e) => { newTabBtn.BorderBrush = FindResource("BorderColor") as Brush ?? Brushes.DimGray; };
            
            WebTabStripStackPanel.Children.Add(newTabBtn);
        }

        private Border CreateTabUi(WebTabItem tab, int idx, bool isActive)
        {
            var border = new Border
            {
                Background = isActive ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1c23")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15171e")),
                BorderBrush = isActive ? (FindResource("AccentColor") as Brush ?? Brushes.Lime) : (FindResource("BorderColor") as Brush ?? Brushes.DimGray),
                BorderThickness = isActive ? new Thickness(1, 1, 1, 0) : new Thickness(1, 1, 1, 1),
                CornerRadius = new CornerRadius(6, 6, 0, 0),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 2, 4, 0),
                Height = 28,
                MinWidth = 100,
                MaxWidth = 180,
                Cursor = Cursors.Hand,
                ToolTip = tab.Url
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            // Tab-specific Spinner
            var spinner = new Border
            {
                Width = 10,
                Height = 10,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = tab.IsLoading ? Visibility.Visible : Visibility.Collapsed
            };
            var ellipse = new System.Windows.Shapes.Ellipse
            {
                Stroke = FindResource("AccentColor") as Brush ?? Brushes.Lime,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 2, 1 }
            };
            var rotate = new RotateTransform();
            ellipse.RenderTransform = rotate;
            ellipse.RenderTransformOrigin = new Point(0.5, 0.5);
            
            var sb = new System.Windows.Media.Animation.Storyboard();
            var da = new System.Windows.Media.Animation.DoubleAnimation { From = 0, To = 360, Duration = TimeSpan.FromSeconds(1), RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
            System.Windows.Media.Animation.Storyboard.SetTarget(da, rotate);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(da, new PropertyPath("Angle"));
            sb.Children.Add(da);
            spinner.Child = ellipse;
            spinner.Loaded += (s, e) => sb.Begin();
            titleStack.Children.Add(spinner);

            var iconTxt = new TextBlock
            {
                Text = "🌐 ",
                Foreground = isActive ? (FindResource("AccentColor") as Brush ?? Brushes.Lime) : (FindResource("TextMuted") as Brush ?? Brushes.Gray),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = tab.IsLoading ? Visibility.Collapsed : Visibility.Visible
            };
            titleStack.Children.Add(iconTxt);

            var titleTxt = new TextBlock
            {
                Text = string.IsNullOrEmpty(tab.Title) ? "New Tab" : tab.Title,
                Foreground = isActive ? (FindResource("TextMain") as Brush ?? Brushes.White) : (FindResource("TextMuted") as Brush ?? Brushes.Gray),
                FontSize = 10,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 100
            };
            titleStack.Children.Add(titleTxt);
            Grid.SetColumn(titleStack, 0);
            grid.Children.Add(titleStack);

            var closeBtn = new Button
            {
                Content = "✕",
                Width = 14,
                Height = 14,
                Padding = new Thickness(0),
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = FindResource("TextMuted") as Brush ?? Brushes.Gray,
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Style = null;
            closeBtn.MouseEnter += (s, e) => { closeBtn.Foreground = Brushes.Red; };
            closeBtn.MouseLeave += (s, e) => { closeBtn.Foreground = FindResource("TextMuted") as Brush ?? Brushes.Gray; };
            closeBtn.Click += (s, e) =>
            {
                e.Handled = true;
                CloseWebTab(idx);
            };
            Grid.SetColumn(closeBtn, 1);
            grid.Children.Add(closeBtn);

            border.Child = grid;
            border.MouseLeftButtonDown += (s, e) =>
            {
                FocusWebTab(idx);
            };

            return border;
        }

        private void BtnWebBack_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count)
            {
                var tab = _webTabs[_activeTabIdx];
                if (tab.WebView != null && tab.WebView.CanGoBack)
                {
                    tab.WebView.Back();
                }
            }
        }

        private void BtnWebForward_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count)
            {
                var tab = _webTabs[_activeTabIdx];
                if (tab.WebView != null && tab.WebView.CanGoForward)
                {
                    tab.WebView.Forward();
                }
            }
        }

        private void BtnWebRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count)
            {
                var tab = _webTabs[_activeTabIdx];
                if (tab.WebView != null)
                {
                    tab.WebView.Reload();
                }
            }
        }

        private void UpdateNavigationButtons()
        {
            if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count)
            {
                var tab = _webTabs[_activeTabIdx];
                BtnWebBack.IsEnabled = tab.WebView != null && tab.WebView.CanGoBack;
                BtnWebForward.IsEnabled = tab.WebView != null && tab.WebView.CanGoForward;
            }
            else
            {
                BtnWebBack.IsEnabled = false;
                BtnWebForward.IsEnabled = false;
            }
        }

        private void UpdateSplitButtonsHighlight()
        {
            if (BtnSplitSingle == null) return;

            var activeBrush = FindResource("AccentColor") as Brush ?? Brushes.Lime;
            var normalBrush = FindResource("TextMuted") as Brush ?? Brushes.Gray;

            BtnSplitSingle.BorderBrush = (_splitMode == "Single") ? activeBrush : normalBrush;
            BtnSplitVertical.BorderBrush = (_splitMode == "Vertical") ? activeBrush : normalBrush;
            BtnSplitHorizontal.BorderBrush = (_splitMode == "Horizontal") ? activeBrush : normalBrush;
            BtnSplitGrid.BorderBrush = (_splitMode == "Grid") ? activeBrush : normalBrush;
        }

        private void SetSplitMode(string mode)
        {
            if (_splitMode == mode) return;
            _splitMode = mode;
            UpdateSplitButtonsHighlight();
            UpdateWebBrowserLayout();
        }

        private void BtnSplitSingle_Click(object sender, RoutedEventArgs e) => SetSplitMode("Single");
        private void BtnSplitVertical_Click(object sender, RoutedEventArgs e) => SetSplitMode("Vertical");
        private void BtnSplitHorizontal_Click(object sender, RoutedEventArgs e) => SetSplitMode("Horizontal");
        private void BtnSplitGrid_Click(object sender, RoutedEventArgs e) => SetSplitMode("Grid");

        private void LoadWebProfiles()
        {
            CmbWebProfile.Items.Clear();
            var profiles = WebNodeCacheHelper.GetAvailableCacheProfiles();
            foreach (var p in profiles)
            {
                CmbWebProfile.Items.Add(new ComboBoxItem { Content = p, Tag = p });
            }

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

        private void CmbWebProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbWebProfile.SelectedItem is ComboBoxItem item && item.Tag is string profileName)
            {
                if (_activeTabIdx < 0 || _activeTabIdx >= _webTabs.Count) return;
                var tab = _webTabs[_activeTabIdx];

                if (tab.ProfileName == profileName) return;
                tab.ProfileName = profileName;

                if (tab.WebView != null)
                {
                    try
                    {
                        var oldUrl = tab.WebView.Address ?? tab.Url;
                        tab.WebView.Dispose();
                        tab.WebView = null;

                        tab.Url = oldUrl;
                        UpdateWebBrowserLayout();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Profile switch failed: {ex.Message}");
                        MessageBox.Show($"Lỗi chuyển đổi Profile trình duyệt: {ex.Message}", "Lỗi WebView2", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
        {
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
                    var path = WebNodeCacheHelper.GetProfileCachePath(name);
                    Directory.CreateDirectory(path);

                    LoadWebProfiles();
                    for (int i = 0; i < CmbWebProfile.Items.Count; i++)
                    {
                        if (CmbWebProfile.Items[i] is ComboBoxItem ci && string.Equals(ci.Tag as string, name, StringComparison.OrdinalIgnoreCase))
                        {
                            CmbWebProfile.SelectedIndex = i;
                            break;
                        }
                    }
                    WebNodeCacheHelper.NotifyProfilesChanged();
                }
            }
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (CmbWebProfile.SelectedItem is ComboBoxItem item && item.Tag is string current)
            {
                if (string.IsNullOrWhiteSpace(current) || current.Equals("Shared", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Không thể xóa profile 'Shared' dùng chung.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var confirm = MessageBox.Show($"Bạn có chắc chắn muốn xóa vĩnh viễn profile '{current}' khỏi đĩa không?",
                    "Xác nhận xóa Profile", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm == MessageBoxResult.Yes)
                {
                    WebNodeCacheHelper.DeleteProfileCache(current);
                }
            }
        }

        private void NavigateWebBrowser(string input)
        {
            if (_activeTabIdx < 0 || _activeTabIdx >= _webTabs.Count) return;
            var tab = _webTabs[_activeTabIdx];
            if (tab.WebView == null) return;

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
            tab.WebView.LoadUrl(url);
        }

        private void BtnWebGo_Click(object sender, RoutedEventArgs e) => NavigateWebBrowser(TxtWebUrl.Text);

        private void TxtWebUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
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
                if (TxtWebUrl.IsKeyboardFocused && !string.IsNullOrWhiteSpace(TxtWebUrl.Text))
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
                    if (suggestions.Count > 0 && TxtWebUrl.IsKeyboardFocused)
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

    }
}
