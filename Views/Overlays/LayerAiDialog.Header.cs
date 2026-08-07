// =========================================================================================================
// AI / DEVELOPER ARCHITECTURAL DIRECTIVE:
// This file is part of the partial class LayerAiDialog (Views/Overlays/LayerAiDialog).
// CRITICAL RULE: DO NOT BLOAT OR STUFF EXCESSIVE LOGIC INTO A SINGLE FILE.
// Maintain each partial class file at a maximum size of ~1000 - 1500 lines of code.
// When adding new features or handlers, refactor and create dedicated partial class files per module.
// =========================================================================================================

using FlowMy.Models.ImageEditor;
using CefSharp;
using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class LayerAiDialog : Window
    {
        #region Header & Window Actions

        private bool _sendModeOn = true;

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            try { DialogResult = false; } catch { }
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            try { DialogResult = false; } catch { }
            Close();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            SaveActiveLayerState();
            try { DialogResult = true; } catch { }
            Close();
        }

        private void UpdateImageModeButtonUI()
        {
            if (BtnToggleImageMode == null) return;

            if (_isCombinedMode)
            {
                BtnToggleImageMode.Content = "Ảnh chung";
                BtnToggleImageMode.Style = TryFindResource("SecondaryButton") as Style;
                BtnToggleImageMode.ToolTip = "Chế độ: Ảnh chung (mặc định)";
            }
            else
            {
                BtnToggleImageMode.Content = "Ảnh đơn";
                BtnToggleImageMode.Style = TryFindResource("PrimaryButton") as Style ?? TryFindResource("SecondaryButton") as Style;
                BtnToggleImageMode.ToolTip = "Chế độ: Ảnh đơn (1 ảnh chính + 1 ảnh phụ)";
            }
        }

        private void BtnToggleImageMode_Click(object sender, RoutedEventArgs e)
        {
            _isCombinedMode = !_isCombinedMode;
            if (_node != null)
            {
                _node.LayerAiIsCombinedMode = _isCombinedMode;

                // Cập nhật UserValueOverride của dynamic output "isCombinedImage" nếu có
                var port = _node.DynamicOutputs?.FirstOrDefault(o =>
                    string.Equals(o.Key, "isCombinedImage", StringComparison.OrdinalIgnoreCase));
                if (port != null)
                {
                    port.UserValueOverride = _isCombinedMode.ToString().ToLowerInvariant();
                }
            }

            UpdateImageModeButtonUI();
            UpdateSecondaryInfo();

            if (!_isCombinedMode)
            {
                var currentPrompt = GetActivePromptText();
                if (!currentPrompt.Contains("@Tat_Ca_Anh_con", StringComparison.OrdinalIgnoreCase))
                {
                    string newPrompt = string.IsNullOrWhiteSpace(currentPrompt) ? "@Tat_Ca_Anh_con" : (currentPrompt + " @Tat_Ca_Anh_con");
                    SetPromptText(TxtPrompt, newPrompt);
                    if (TxtPromptWv != null) SetPromptText(TxtPromptWv, newPrompt);
                    if (TxtPromptWeb != null) SetPromptText(TxtPromptWeb, newPrompt);
                }
            }

            try
            {
                _host?.RequestSyncDataPanels(immediate: true);
            }
            catch { }
        }

        private void BtnToggleSendMode_Click(object sender, RoutedEventArgs e)
        {
            _sendModeOn = !_sendModeOn;
            if (BtnToggleSendMode != null)
            {
                BtnToggleSendMode.Content = _sendModeOn ? "Gửi AI: ON" : "Gửi AI: OFF";
                BtnToggleSendMode.Style = _sendModeOn
                    ? (TryFindResource("SuccessButton") as Style)
                    : (TryFindResource("SecondaryButton") as Style);
            }
            UpdatePreviewImage();
        }

        private void BtnTogglePrompt_Click(object sender, RoutedEventArgs e) { }

        #endregion

        #region Tab Switching (Prompt / WebView / WebBrowser)

        private enum ActiveTab { Prompt, WebView, WebBrowser }
        private ActiveTab _activeTab = ActiveTab.Prompt;

        // WebView2 browser (lazy init)
        public class WebTabItem
        {
            public ChromiumWebBrowser? WebView { get; set; }
            public string Url { get; set; } = "https://google.com";
            public string Title { get; set; } = "New Tab";
            public string ProfileName { get; set; } = "Shared";
            public bool IsLoading { get; set; } = false;
        }

        public class SerializedWebTab
        {
            public string Url { get; set; } = "https://google.com";
            public string ProfileName { get; set; } = "Shared";
            public string Title { get; set; } = "New Tab";
        }

        private readonly List<WebTabItem> _webTabs = new();
        private int _activeTabIdx = -1;
        private string _splitMode = "Single";
        private bool _webBrowserInitialized = false;
        private ChromiumWebBrowser? _dynamicWebView;
        private System.Windows.Controls.Primitives.Popup? _suggestPopup;
        private ListBox? _suggestListBox;
        private System.Windows.Threading.DispatcherTimer? _suggestDebounceTimer;
        private Point _dragStartPoint;

        private string GetActivePromptText()
        {
            var rtb = GetActivePromptRichTextBox();
            return GetPromptText(rtb);
        }

        private void SyncPromptTo(ActiveTab target)
        {
            var text = GetActivePromptText();
            if (target != ActiveTab.Prompt) SetPromptText(TxtPrompt, text);
            if (target != ActiveTab.WebView) SetPromptText(TxtPromptWv, text);
            if (target != ActiveTab.WebBrowser) SetPromptText(TxtPromptWeb, text);
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

            if (BtnToggleImageMode != null)
            {
                BtnToggleImageMode.Visibility = (newTab == ActiveTab.Prompt) ? Visibility.Visible : Visibility.Collapsed;
            }

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

            if (newTab == ActiveTab.WebBrowser || newTab == ActiveTab.WebView)
            {
                ReactivateActiveWebBrowsers();
            }
        }

        private void ReactivateActiveWebBrowsers()
        {
            Dispatcher.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(50);
                try
                {
                    if (_dynamicWebView != null)
                    {
                        ReactivateChromiumBrowser(_dynamicWebView);
                    }
                    foreach (var tab in _webTabs)
                    {
                        if (tab.WebView != null)
                        {
                            ReactivateChromiumBrowser(tab.WebView);
                        }
                    }
                }
                catch { }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static void ReactivateChromiumBrowser(ChromiumWebBrowser? webView)
        {
            if (webView == null) return;
            try
            {
                void ApplyReactivation()
                {
                    try
                    {
                        var host = webView.GetBrowser()?.GetHost();
                        if (host != null)
                        {
                            host.WasHidden(false);
                            host.SendFocusEvent(true);
                            host.Invalidate(CefSharp.PaintElementType.View);
                        }
                        webView.EvaluateScriptAsync(@"
                            if (window.resetDragState) window.resetDragState();
                            window._isMouseDownOnImage = false;
                        ");
                    }
                    catch { }
                }

                if (webView.IsBrowserInitialized)
                {
                    ApplyReactivation();
                }
                else
                {
                    DependencyPropertyChangedEventHandler? handler = null;
                    handler = (s, e) =>
                    {
                        if (webView.IsBrowserInitialized)
                        {
                            webView.IsBrowserInitializedChanged -= handler;
                            ApplyReactivation();
                        }
                    };
                    webView.IsBrowserInitializedChanged += handler;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to reactivate CefSharp browser: {ex.Message}");
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
                for (int i = 0; i < _slotImagesWv.Count && i < _slotImages.Count; i++)
                {
                    _slotImagesWv[i].Source = _slotImages[i].Source;
                    if (i < _slotPlaceholdersWv.Count && i < _secondaryImages.Count)
                        _slotPlaceholdersWv[i].Visibility = _secondaryImages[i]?.HasImage == true ? Visibility.Collapsed : Visibility.Visible;
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
    }
}
