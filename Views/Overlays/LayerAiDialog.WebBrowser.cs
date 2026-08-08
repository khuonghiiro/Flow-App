// =========================================================================================================
// AI / DEVELOPER ARCHITECTURAL DIRECTIVE:
// This file is part of the partial class LayerAiDialog (Views/Overlays/LayerAiDialog).
// CRITICAL RULE: DO NOT BLOAT OR STUFF EXCESSIVE LOGIC INTO A SINGLE FILE.
// Maintain each partial class file at a maximum size of ~1000 - 1500 lines of code.
// When adding new features or handlers, refactor and create dedicated partial class files per module.
// =========================================================================================================

using FlowMy.Helpers;
using FlowMy.Services.Workflow;
using CefSharp;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class LayerAiDialog : Window
    {
        #region Web Browser (CefSharp + Search + Profile)

        private bool _isUpdatingProfileCombo = false;
        private EventHandler? _onProfilesChangedHandler;

        private void UpdateSplitButtonsHighlight()
        {
            if (BtnSplitSingle == null) return;

            var activeBrush = FindResource("AccentColor") as Brush ?? Brushes.Lime;
            var normalBrush = FindResource("TextMuted") as Brush ?? Brushes.Gray;

            BtnSplitSingle.BorderBrush = (_splitMode == "Single") ? activeBrush : normalBrush;
            BtnSplitVertical.BorderBrush = (_splitMode == "Split2V" || _splitMode == "Vertical") ? activeBrush : normalBrush;
            BtnSplitHorizontal.BorderBrush = (_splitMode == "Split2H" || _splitMode == "Horizontal") ? activeBrush : normalBrush;
            BtnSplitGrid.BorderBrush = (_splitMode == "Grid4" || _splitMode == "Grid") ? activeBrush : normalBrush;
        }

        private void SetSplitMode(string mode)
        {
            _splitMode = mode;
            if (_node != null)
            {
                _node.LayerAiWebSplitMode = _splitMode;
            }

            int targetCount = 1;
            if (_splitMode == "Split2V" || _splitMode == "Vertical" || _splitMode == "Split2H" || _splitMode == "Horizontal")
            {
                targetCount = 2;
            }
            else if (_splitMode == "Grid4" || _splitMode == "Grid")
            {
                targetCount = 4;
            }

            while (_webTabs.Count < targetCount)
            {
                AddNewWebTab("https://google.com", "Shared");
            }

            UpdateSplitButtonsHighlight();
            RenderWebTabsInGrid();
            SaveWebTabsState();
        }

        private void BtnSplitSingle_Click(object sender, RoutedEventArgs e) => SetSplitMode("Single");
        private void BtnSplitVertical_Click(object sender, RoutedEventArgs e) => SetSplitMode("Split2V");
        private void BtnSplitHorizontal_Click(object sender, RoutedEventArgs e) => SetSplitMode("Split2H");
        private void BtnSplitGrid_Click(object sender, RoutedEventArgs e) => SetSplitMode("Grid4");

        private void LoadProfileComboItems()
        {
            if (CmbWebProfile == null) return;

            _isUpdatingProfileCombo = true;
            try
            {
                string currentSel = CmbWebProfile.SelectedValue as string ?? "Shared";
                CmbWebProfile.Items.Clear();

                var profiles = WebNodeCacheHelper.GetAvailableCacheProfiles();
                if (!profiles.Contains("Shared", StringComparer.OrdinalIgnoreCase))
                {
                    profiles.Insert(0, "Shared");
                }

                foreach (var p in profiles)
                {
                    CmbWebProfile.Items.Add(p);
                }

                if (profiles.Contains(currentSel, StringComparer.OrdinalIgnoreCase))
                {
                    CmbWebProfile.SelectedValue = currentSel;
                }
                else
                {
                    CmbWebProfile.SelectedValue = "Shared";
                }
            }
            finally
            {
                _isUpdatingProfileCombo = false;
            }
        }

        private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "Nhập tên profile mới (ví dụ: Acc_Gmail_1):",
                "Tạo Profile Mới", "");
            if (string.IsNullOrWhiteSpace(input)) return;

            var name = input.Trim();
            if (name.Equals("Shared", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Tên 'Shared' là profile mặc định.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                WebNodeCacheHelper.EnsureProfileExists(name);
                WebNodeCacheHelper.NotifyProfilesChanged();
                LoadProfileComboItems();
                if (CmbWebProfile != null)
                {
                    CmbWebProfile.SelectedValue = name;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tạo profile: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var current = CmbWebProfile?.SelectedValue as string;
            if (string.IsNullOrWhiteSpace(current) || current.Equals("Shared", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Không thể xóa profile mặc định 'Shared'.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa profile '{current}'?\nToàn bộ cookie và dữ liệu đăng nhập của profile này sẽ bị xóa hoàn toàn.",
                "Xác nhận xóa Profile",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    WebNodeCacheHelper.DeleteProfileCache(current);
                    WebNodeCacheHelper.NotifyProfilesChanged();
                    LoadProfileComboItems();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi xóa profile: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TxtWebUrl_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb) tb.SelectAll();
        }

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
                                        lastEl.dispatchEvent(dragEndEv);
                                        lastEl.dispatchEvent(dragLeaveEv);
                                    }
                                    document.dispatchEvent(mouseUpEv);
                                    document.dispatchEvent(pointerUpEv);
                                    document.dispatchEvent(dragEndEv);
                                    document.dispatchEvent(dragLeaveEv);
                                    window.dispatchEvent(mouseUpEv);
                                    window.dispatchEvent(pointerUpEv);
                                };

                                document.addEventListener('mousedown', function(ev) {
                                    const target = ev.target;
                                    if (target && (target.tagName === 'IMG' || target.style.backgroundImage || target.querySelector('img'))) {
                                        window._isMouseDownOnImage = true;
                                    } else {
                                        window._isMouseDownOnImage = false;
                                    }
                                }, true);

                                document.addEventListener('mouseup', function(ev) {
                                    window._isMouseDownOnImage = false;
                                }, true);

                                document.addEventListener('mouseleave', function(ev) {
                                    window._isMouseDownOnImage = false;
                                }, true);
                            })();
                        ";
                        e.Frame.ExecuteJavaScriptAsync(script);
                    }
                    catch { }
                }
            };
        }

        private void InitWebBrowserAsync()
        {
            if (_webBrowserInitialized) return;
            _webBrowserInitialized = true;

            SetupSuggestPopup();

            LoadProfileComboItems();

            _onProfilesChangedHandler = (s, e) =>
            {
                Dispatcher.InvokeAsync(() => LoadProfileComboItems());
            };
            WebNodeCacheHelper.ProfilesChanged += _onProfilesChangedHandler;

            if (CmbWebProfile != null)
            {
                CmbWebProfile.SelectionChanged -= CmbWebProfile_SelectionChanged;
                CmbWebProfile.SelectionChanged += CmbWebProfile_SelectionChanged;
            }

            LoadSavedWebTabs();
        }

        private void CmbWebProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingProfileCombo) return;
            var selected = CmbWebProfile?.SelectedValue as string;
            if (string.IsNullOrWhiteSpace(selected)) return;

            if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count)
            {
                var tab = _webTabs[_activeTabIdx];
                if (!string.Equals(tab.ProfileName, selected, StringComparison.OrdinalIgnoreCase))
                {
                    tab.ProfileName = selected;
                    if (tab.WebView != null)
                    {
                        var url = tab.WebView.Address ?? tab.Url ?? "https://google.com";
                        RemoveVisualParent(tab.WebView);
                        try { tab.WebView.Dispose(); } catch { }

                        var reqProfile = CefSharpEnvironmentManager.CreateProfileRequestContext(selected);
                        var newWebView = new ChromiumWebBrowser();
                        newWebView.RequestContext = reqProfile;
                        newWebView.Address = url;
                        newWebView.AllowDrop = true;
                        tab.WebView = newWebView;
                        InjectDragDropInterceptorScriptAsync(newWebView);

                        newWebView.MouseEnter += (s, ev) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                int idx = _webTabs.FindIndex(t => t.WebView == newWebView);
                                if (idx >= 0 && idx != _activeTabIdx)
                                {
                                    _activeTabIdx = idx;
                                    RebuildTabBar();
                                    UpdateUrlBarUI();
                                }
                            });
                        };
                    }
                    SaveWebTabsState();
                    RenderWebTabsInGrid();
                }
            }
        }

        private void AddNewWebTab(string initialUrl = "https://google.com", string initialProfile = "Shared", string? initialTitle = null)
        {
            var profileName = initialProfile;
            var req = CefSharpEnvironmentManager.CreateProfileRequestContext(profileName);

            var webView = new ChromiumWebBrowser();
            webView.RequestContext = req;
            webView.Address = initialUrl;
            webView.AllowDrop = true;

            InjectDragDropInterceptorScriptAsync(webView);

            var tabItem = new WebTabItem
            {
                WebView = webView,
                Url = initialUrl,
                Title = string.IsNullOrWhiteSpace(initialTitle) ? "Loading..." : initialTitle,
                ProfileName = profileName,
                IsLoading = true
            };

            webView.MouseEnter += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    int idx = _webTabs.FindIndex(t => t.WebView == webView);
                    if (idx >= 0 && idx != _activeTabIdx)
                    {
                        _activeTabIdx = idx;
                        RebuildTabBar();
                        UpdateUrlBarUI();
                    }
                });
            };

            webView.GotFocus += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    int idx = _webTabs.FindIndex(t => t.WebView == webView);
                    if (idx >= 0 && idx != _activeTabIdx)
                    {
                        _activeTabIdx = idx;
                        RebuildTabBar();
                        UpdateUrlBarUI();
                    }
                });
            };

            webView.TitleChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    tabItem.Title = e.NewValue as string ?? "New Tab";
                    RebuildTabBar();
                    UpdateUrlBarUI();
                });
            };

            webView.AddressChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    tabItem.Url = e.NewValue as string ?? "";
                    if (_webTabs.Count > 0 && _activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count && _webTabs[_activeTabIdx] == tabItem)
                    {
                        if (TxtWebUrl != null && !TxtWebUrl.IsKeyboardFocused)
                        {
                            TxtWebUrl.Text = tabItem.Url;
                        }
                    }
                });
            };

            webView.LoadingStateChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    tabItem.IsLoading = e.IsLoading;
                    if (_webTabs.Count > 0 && _activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count && _webTabs[_activeTabIdx] == tabItem)
                    {
                        BtnWebRefresh.Content = e.IsLoading ? "✕" : "🔄";
                    }
                });
            };

            _webTabs.Add(tabItem);
            _activeTabIdx = _webTabs.Count - 1;

            RebuildTabBar();
            RenderWebTabsInGrid();
            UpdateUrlBarUI();
        }

        private void CloseWebTab(int index)
        {
            if (index < 0 || index >= _webTabs.Count) return;

            var tabItem = _webTabs[index];

            if (_webTabs.Count <= 1)
            {
                tabItem.WebView?.Load("about:blank");
                tabItem.Url = "https://google.com";
                tabItem.Title = "New Tab";
                tabItem.WebView?.Load("https://google.com");
                RebuildTabBar();
                UpdateUrlBarUI();
                return;
            }

            _webTabs.RemoveAt(index);
            try
            {
                tabItem.WebView?.Dispose();
            }
            catch { }

            if (_activeTabIdx >= _webTabs.Count)
            {
                _activeTabIdx = _webTabs.Count - 1;
            }

            RebuildTabBar();
            RenderWebTabsInGrid();
            UpdateUrlBarUI();
        }

        private void RebuildTabBar()
        {
            if (WebTabStripStackPanel == null) return;
            WebTabStripStackPanel.Children.Clear();

            for (int i = 0; i < _webTabs.Count; i++)
            {
                int idx = i;
                var tab = _webTabs[i];
                bool isActive = (i == _activeTabIdx);

                var border = new Border
                {
                    Background = isActive
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15171e")),
                    BorderBrush = isActive
                        ? (FindResource("AccentColor") as Brush ?? Brushes.Lime)
                        : (FindResource("BorderColor") as Brush ?? Brushes.DimGray),
                    BorderThickness = new Thickness(1, 1, 1, 0),
                    CornerRadius = new CornerRadius(4, 4, 0, 0),
                    Margin = new Thickness(0, 0, 2, 0),
                    Padding = new Thickness(6, 2, 6, 2),
                    Cursor = Cursors.Hand,
                    MaxWidth = 130
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal };

                var txtTitle = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(tab.Title) ? "New Tab" : tab.Title,
                    Foreground = isActive ? Brushes.White : (FindResource("TextMuted") as Brush ?? Brushes.Gray),
                    FontSize = 9.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 80
                };
                sp.Children.Add(txtTitle);

                var btnCloseTab = new Button
                {
                    Content = "✕",
                    Background = Brushes.Transparent,
                    Foreground = (FindResource("TextMuted") as Brush ?? Brushes.Gray),
                    BorderThickness = new Thickness(0),
                    FontSize = 8,
                    Margin = new Thickness(4, 0, 0, 0),
                    Padding = new Thickness(2, 0, 2, 0),
                    Cursor = Cursors.Hand
                };
                btnCloseTab.Click += (s, e) =>
                {
                    e.Handled = true;
                    CloseWebTab(idx);
                };
                sp.Children.Add(btnCloseTab);

                border.Child = sp;
                border.MouseLeftButtonDown += (s, e) =>
                {
                    _activeTabIdx = idx;
                    RebuildTabBar();
                    RenderWebTabsInGrid();
                    UpdateUrlBarUI();
                };

                WebTabStripStackPanel.Children.Add(border);
            }
        }

        private void RenderWebTabsInGrid()
        {
            if (WebBrowserContainer == null) return;
            WebBrowserContainer.Children.Clear();
            WebBrowserContainer.RowDefinitions.Clear();
            WebBrowserContainer.ColumnDefinitions.Clear();

            if (_webTabs.Count == 0) return;

            bool isVertical = _splitMode == "Split2V" || _splitMode == "Vertical";
            bool isHorizontal = _splitMode == "Split2H" || _splitMode == "Horizontal";
            bool isGrid = _splitMode == "Grid4" || _splitMode == "Grid";

            if (!isVertical && !isHorizontal && !isGrid)
            {
                if (_activeTabIdx < 0 || _activeTabIdx >= _webTabs.Count) _activeTabIdx = 0;
                var activeTab = _webTabs[_activeTabIdx];
                if (activeTab.WebView != null)
                {
                    RemoveVisualParent(activeTab.WebView);
                    WebBrowserContainer.Children.Add(activeTab.WebView);
                }
            }
            else if (isVertical)
            {
                WebBrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                WebBrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                int count = Math.Min(2, _webTabs.Count);
                for (int i = 0; i < count; i++)
                {
                    var tab = _webTabs[i];
                    if (tab.WebView != null)
                    {
                        RemoveVisualParent(tab.WebView);
                        Grid.SetColumn(tab.WebView, i);
                        WebBrowserContainer.Children.Add(tab.WebView);
                    }
                }
            }
            else if (isHorizontal)
            {
                WebBrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                WebBrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                int count = Math.Min(2, _webTabs.Count);
                for (int i = 0; i < count; i++)
                {
                    var tab = _webTabs[i];
                    if (tab.WebView != null)
                    {
                        RemoveVisualParent(tab.WebView);
                        Grid.SetRow(tab.WebView, i);
                        WebBrowserContainer.Children.Add(tab.WebView);
                    }
                }
            }
            else if (isGrid)
            {
                WebBrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                WebBrowserContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                WebBrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                WebBrowserContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                int count = Math.Min(4, _webTabs.Count);
                for (int i = 0; i < count; i++)
                {
                    var tab = _webTabs[i];
                    if (tab.WebView != null)
                    {
                        RemoveVisualParent(tab.WebView);
                        int r = i / 2;
                        int c = i % 2;
                        Grid.SetRow(tab.WebView, r);
                        Grid.SetColumn(tab.WebView, c);
                        WebBrowserContainer.Children.Add(tab.WebView);
                    }
                }
            }
        }

        private void SaveWebTabsState()
        {
            if (_node == null) return;

            var tabDtos = _webTabs.Select(t => new WebTabDto
            {
                Url = t.WebView?.Address ?? t.Url ?? "https://google.com",
                Title = string.IsNullOrWhiteSpace(t.Title) ? "New Tab" : t.Title,
                ProfileName = string.IsNullOrWhiteSpace(t.ProfileName) ? "Shared" : t.ProfileName
            }).ToList();

            _node.LayerAiWebTabsJson = System.Text.Json.JsonSerializer.Serialize(tabDtos);
            _node.LayerAiWebSplitMode = _splitMode;
        }

        private void LoadSavedWebTabs()
        {
            _webTabs.Clear();

            if (_node != null && !string.IsNullOrWhiteSpace(_node.LayerAiWebSplitMode))
            {
                _splitMode = _node.LayerAiWebSplitMode;
            }
            else
            {
                _splitMode = "Single";
            }

            if (_node != null && !string.IsNullOrWhiteSpace(_node.LayerAiWebTabsJson))
            {
                try
                {
                    var tabDtos = System.Text.Json.JsonSerializer.Deserialize<List<WebTabDto>>(_node.LayerAiWebTabsJson);
                    if (tabDtos != null && tabDtos.Count > 0)
                    {
                        foreach (var dto in tabDtos)
                        {
                            AddNewWebTab(dto.Url ?? "https://google.com", dto.ProfileName ?? "Shared", dto.Title);
                        }
                        _activeTabIdx = 0;
                        RebuildTabBar();
                        RenderWebTabsInGrid();
                        UpdateSplitButtonsHighlight();
                        UpdateUrlBarUI();
                        return;
                    }
                }
                catch { }
            }

            AddNewWebTab(_node?.LayerAiWebUrl ?? "https://google.com", "Shared", "Trình duyệt");
            UpdateSplitButtonsHighlight();
        }

        private class WebTabDto
        {
            public string? Url { get; set; }
            public string? Title { get; set; }
            public string? ProfileName { get; set; }
        }

        private static void RemoveVisualParent(UIElement element)
        {
            if (element is FrameworkElement fe)
            {
                if (fe.Parent is Panel panel)
                {
                    panel.Children.Remove(fe);
                }
                else if (fe.Parent is ContentControl cc)
                {
                    cc.Content = null;
                }
                else if (fe.Parent is Decorator decorator)
                {
                    decorator.Child = null;
                }
            }
        }

        private void UpdateUrlBarUI()
        {
            if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count)
            {
                var tab = _webTabs[_activeTabIdx];
                if (TxtWebUrl != null && !TxtWebUrl.IsKeyboardFocused)
                {
                    TxtWebUrl.Text = tab.Url;
                }
                if (CmbWebProfile != null && !_isUpdatingProfileCombo)
                {
                    CmbWebProfile.SelectedValue = tab.ProfileName;
                }
                if (BtnWebRefresh != null)
                {
                    BtnWebRefresh.Content = tab.IsLoading ? "✕" : "🔄";
                }
            }
        }

        private void BtnWebNewTab_Click(object sender, RoutedEventArgs e)
        {
            AddNewWebTab("https://google.com", "Shared");
        }

        private void BtnWebBack_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count)
            {
                var webView = _webTabs[_activeTabIdx].WebView;
                if (webView != null && webView.CanGoBack) webView.Back();
            }
        }

        private void BtnWebForward_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count)
            {
                var webView = _webTabs[_activeTabIdx].WebView;
                if (webView != null && webView.CanGoForward) webView.Forward();
            }
        }

        private void BtnWebRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count)
            {
                var tab = _webTabs[_activeTabIdx];
                if (tab.WebView != null)
                {
                    if (tab.IsLoading) tab.WebView.Stop();
                    else tab.WebView.Reload();
                }
            }
        }

        private void BtnWebGo_Click(object sender, RoutedEventArgs e)
        {
            NavigateCurrentWebTab();
        }

        private void TxtWebUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_suggestPopup != null) _suggestPopup.IsOpen = false;
                NavigateCurrentWebTab();
            }
            else if (e.Key == Key.Escape)
            {
                if (_suggestPopup != null) _suggestPopup.IsOpen = false;
            }
            else if (e.Key == Key.Down && _suggestPopup != null && _suggestPopup.IsOpen && _suggestListBox != null)
            {
                _suggestListBox.Focus();
                if (_suggestListBox.Items.Count > 0) _suggestListBox.SelectedIndex = 0;
            }
        }

        private void NavigateCurrentWebTab()
        {
            if (_activeTabIdx < 0 || _activeTabIdx >= _webTabs.Count) return;
            var tab = _webTabs[_activeTabIdx];
            if (tab.WebView == null) return;

            string input = TxtWebUrl.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(input)) return;

            string targetUrl;
            if (Uri.TryCreate(input, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                targetUrl = input;
            }
            else if (input.Contains('.') && !input.Contains(' '))
            {
                targetUrl = "https://" + input;
            }
            else
            {
                targetUrl = "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
            }

            tab.WebView.Load(targetUrl);
        }

        private void SetupSuggestPopup()
        {
            _suggestListBox = new ListBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1c23")),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                MaxHeight = 200,
                FontSize = 11
            };

            _suggestListBox.MouseLeftButtonUp += (s, e) =>
            {
                if (_suggestListBox.SelectedItem is string selectedText)
                {
                    TxtWebUrl.Text = selectedText;
                    _suggestPopup.IsOpen = false;
                    NavigateCurrentWebTab();
                }
            };

            _suggestListBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && _suggestListBox.SelectedItem is string selectedText)
                {
                    TxtWebUrl.Text = selectedText;
                    _suggestPopup.IsOpen = false;
                    NavigateCurrentWebTab();
                }
            };

            _suggestPopup = new System.Windows.Controls.Primitives.Popup
            {
                StaysOpen = false,
                PlacementTarget = TxtWebUrl,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                Width = 400,
                AllowsTransparency = true,
                Child = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1c23")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Child = _suggestListBox
                }
            };

            _suggestDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _suggestDebounceTimer.Tick += async (s, e) =>
            {
                _suggestDebounceTimer.Stop();
                await FetchSearchSuggestionsAsync(TxtWebUrl.Text?.Trim() ?? "");
            };

            TxtWebUrl.TextChanged += (s, e) =>
            {
                if (!TxtWebUrl.IsKeyboardFocused) return;
                _suggestDebounceTimer.Stop();
                var text = TxtWebUrl.Text?.Trim() ?? "";
                if (text.Length > 1 && !text.StartsWith("http://") && !text.StartsWith("https://"))
                {
                    _suggestDebounceTimer.Start();
                }
                else
                {
                    if (_suggestPopup != null) _suggestPopup.IsOpen = false;
                }
            };

            TxtWebUrl.LostFocus += (s, e) =>
            {
                Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(150);
                    if (_suggestPopup != null && !_suggestListBox.IsFocused)
                    {
                        _suggestPopup.IsOpen = false;
                    }
                });
            };
        }

        private async Task FetchSearchSuggestionsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                if (_suggestPopup != null) _suggestPopup.IsOpen = false;
                return;
            }

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                var url = $"https://suggestqueries.google.com/complete/search?client=chrome&q={Uri.EscapeDataString(query)}";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;

                var jsonStr = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);

                var suggestions = new List<string>();
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 1)
                {
                    var listEl = root[1];
                    if (listEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in listEl.EnumerateArray())
                        {
                            var s = item.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) suggestions.Add(s);
                            if (suggestions.Count >= 8) break;
                        }
                    }
                }

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
