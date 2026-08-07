// =========================================================================================================
// AI / DEVELOPER ARCHITECTURAL DIRECTIVE:
// This file is part of the partial class LayerAiDialog (Views/Overlays/LayerAiDialog).
// CRITICAL RULE: DO NOT BLOAT OR STUFF EXCESSIVE LOGIC INTO A SINGLE FILE.
// Maintain each partial class file at a maximum size of ~1000 - 1500 lines of code.
// When adding new features or handlers, refactor and create dedicated partial class files per module.
// =========================================================================================================

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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlowMy.Views.Overlays
{
    public partial class LayerAiDialog : Window
    {
        public static readonly ConcurrentQueue<string> PendingExecutionIds = new ConcurrentQueue<string>();

        public void ForceClose()
        {
            try { Close(); } catch { }
        }

        #region Scope & Registry
        public class LayerExecutionScope
        {
            public string NodeId { get; set; } = string.Empty;
            public Dictionary<string, SecondaryImageItem> CropGuidRegistry { get; } = new();
        }

        public static readonly ConcurrentDictionary<string, LayerExecutionScope> ActiveExecutionScopes = new();

        public class SecondaryCropGuidInfo
        {
            public SecondaryImageItem? SecondaryImage { get; set; }
            public EditorLayer? TargetLayer { get; set; }
            public int AspectRatioIndex { get; set; }
        }

        public Dictionary<string, SecondaryCropGuidInfo> CropGuidRegistry { get; } = new();

        private static LayerExecutionScope? GetCurrentExecutionScope(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return null;
            ActiveExecutionScopes.TryGetValue(nodeId, out var scope);
            return scope;
        }

        public class ActiveLayerState
        {
            public string Prompt { get; set; } = string.Empty;
            public int BatchSizeIndex { get; set; } = 0;
            public int AspectRatioIndex { get; set; } = 0;
            public string CustomWidth { get; set; } = "512";
            public string CustomHeight { get; set; } = "512";
        }

        private readonly Dictionary<EditorLayer, ActiveLayerState> _layerStates = new();
        #endregion

        #region Secondary Image Item Model
        public class SecondaryImageItem : System.ComponentModel.INotifyPropertyChanged
        {
            private BitmapSource? _bitmap;
            private string? _filePath;
            private bool _isSelected;
            private string _codeId = Guid.NewGuid().ToString("N");
            private readonly Dictionary<int, string> _ratioImageIds = new();

            public BitmapSource? Bitmap
            {
                get => _bitmap;
                set
                {
                    _bitmap = value;
                    OnPropertyChanged(nameof(Bitmap));
                    OnPropertyChanged(nameof(HasImage));
                }
            }

            public string? FilePath
            {
                get => _filePath;
                set
                {
                    _filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                }
            }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }

            public string CodeId
            {
                get => _codeId;
                set
                {
                    _codeId = value;
                    OnPropertyChanged(nameof(CodeId));
                }
            }

            public bool HasImage => _bitmap != null;

            public string? GetImageId(int aspectRatioIndex)
            {
                if (_ratioImageIds.TryGetValue(aspectRatioIndex, out var id)) return id;
                return _codeId;
            }

            public void SetImageId(int aspectRatioIndex, string imageId)
            {
                if (!string.IsNullOrWhiteSpace(imageId))
                {
                    _ratioImageIds[aspectRatioIndex] = imageId;
                }
            }

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
        #endregion

        #region Dialog Fields
        private List<EditorLayer> _selectedLayers;
        private EditorLayer _activeLayer;
        private ImageProcessingNode _node;
        private IWorkflowEditorHost _host;
        private EditorDocument _doc;

        private readonly List<SecondaryImageItem> _secondaryImages = new();
        private int _secondarySlotCount = 4;
        private bool _isUpdatingSlotCount = false;
        private bool _isCombinedMode = true;

        private readonly List<Image> _slotImages = new();
        private readonly List<StackPanel> _slotPlaceholders = new();
        private readonly List<Border> _slotBorders = new();

        private readonly List<Image> _slotImagesWv = new();
        private readonly List<StackPanel> _slotPlaceholdersWv = new();
        private readonly List<Border> _slotBordersWv = new();

        private double _originalWidth;
        private double _originalHeight;

        private bool _isSyncingUI = false;
        private bool _isAiLoading = false;
        private bool _isMouseDownOnImage = false;
        #endregion

        public LayerAiDialog(List<EditorLayer> selectedLayers, EditorLayer activeLayer, ImageProcessingNode node, IWorkflowEditorHost host, EditorDocument doc, Window? owner)
        {
            InitializeComponent();
            Owner = owner;

            _selectedLayers = selectedLayers ?? new List<EditorLayer>();
            _activeLayer = activeLayer;
            _node = node;
            _host = host;
            _doc = doc;

            _originalWidth = Width;
            _originalHeight = Height;

            InitializeSession();
        }

        public void ReinitializeSession(List<EditorLayer> selectedLayers, EditorLayer activeLayer, ImageProcessingNode node, IWorkflowEditorHost host, EditorDocument doc, Window? owner)
        {
            Owner = owner;
            _selectedLayers = selectedLayers ?? new List<EditorLayer>();
            _activeLayer = activeLayer;
            _node = node;
            _host = host;
            _doc = doc;

            InitializeSession();
        }

        private void InitializeSession()
        {
            // Sync active execution scope for node
            if (_node != null && !string.IsNullOrWhiteSpace(_node.Id))
            {
                var scope = GetOrCreateExecutionScope(_node.Id);
                ActiveExecutionScopes[_node.Id] = scope;
            }

            // Sync slot count
            SetSlotCount(_secondarySlotCount > 0 ? _secondarySlotCount : 4);

            // Populate initial layer states dictionary
            _layerStates.Clear();
            if (_selectedLayers != null)
            {
                foreach (var layer in _selectedLayers)
                {
                    _layerStates[layer] = new ActiveLayerState
                    {
                        Prompt = layer.LayerAiPrompt ?? string.Empty,
                        BatchSizeIndex = layer.LayerAiBatchSizeIndex,
                        AspectRatioIndex = layer.LayerAiAspectRatioIndex,
                        CustomWidth = layer.LayerAiCustomWidth ?? "512",
                        CustomHeight = layer.LayerAiCustomHeight ?? "512"
                    };
                }
            }

            // Load saved settings for active layer
            LoadSavedSettings();

            if (_node != null)
            {
                _isCombinedMode = _node.LayerAiIsCombinedMode;
            }
            UpdateImageModeButtonUI();

            // Setup @ image suggest popup & hook prompt RichTextBox controls
            SetupPromptSuggestPopup();
            HookPromptRichTextBoxEvents(TxtPrompt);
            HookPromptRichTextBoxEvents(TxtPromptWv);
            HookPromptRichTextBoxEvents(TxtPromptWeb);

            // Sync prompt from node to prompt boxes
            LoadActiveLayerState();

            // Setup two-way drag and drop between WPF and WebView2
            SetupDragAndDrop();

            // Listen to Ctrl+V paste event when hovering over images/slots
            this.PreviewKeyDown += LayerAiDialog_PreviewKeyDown;
        }

        private LayerExecutionScope GetOrCreateExecutionScope(string nodeId)
        {
            if (!ActiveExecutionScopes.TryGetValue(nodeId, out var scope))
            {
                scope = new LayerExecutionScope { NodeId = nodeId };
                ActiveExecutionScopes[nodeId] = scope;
            }
            return scope;
        }

        #region State Persistence (Load & Save)

        private void SaveSavedSettings()
        {
            if (_node == null) return;
            _node.LayerAiIsCombinedMode = _isCombinedMode;
        }

        private void LoadSavedSettings()
        {
            if (_node == null) return;
            SetSlotCount(_secondarySlotCount > 0 ? _secondarySlotCount : 4);
        }

        private void SaveActiveLayerState()
        {
            if (_activeLayer == null || !_layerStates.TryGetValue(_activeLayer, out var state)) return;

            state.Prompt = GetActivePromptText();
            state.BatchSizeIndex = CmbBatchSize.SelectedIndex;
            state.AspectRatioIndex = CmbAspectRatio.SelectedIndex;
            state.CustomWidth = TxtCustomWidth.Text;
            state.CustomHeight = TxtCustomHeight.Text;

            _activeLayer.LayerAiPrompt = state.Prompt;
            _activeLayer.LayerAiBatchSizeIndex = state.BatchSizeIndex;
            _activeLayer.LayerAiAspectRatioIndex = state.AspectRatioIndex;
            _activeLayer.LayerAiCustomWidth = state.CustomWidth;
            _activeLayer.LayerAiCustomHeight = state.CustomHeight;

            if (_node != null)
            {
                _node.ProcessorPrompt = state.Prompt;
            }
        }

        private void LoadActiveLayerState()
        {
            if (_activeLayer == null) return;

            if (!_layerStates.TryGetValue(_activeLayer, out var state))
            {
                state = new ActiveLayerState
                {
                    Prompt = _activeLayer.LayerAiPrompt ?? string.Empty,
                    BatchSizeIndex = _activeLayer.LayerAiBatchSizeIndex,
                    AspectRatioIndex = _activeLayer.LayerAiAspectRatioIndex,
                    CustomWidth = _activeLayer.LayerAiCustomWidth ?? "512",
                    CustomHeight = _activeLayer.LayerAiCustomHeight ?? "512"
                };
                _layerStates[_activeLayer] = state;
            }

            _isSyncingUI = true;
            try
            {
                SetPromptText(TxtPrompt, state.Prompt);
                if (TxtPromptWv != null) SetPromptText(TxtPromptWv, state.Prompt);
                if (TxtPromptWeb != null) SetPromptText(TxtPromptWeb, state.Prompt);

                CmbBatchSize.SelectedIndex = state.BatchSizeIndex;
                CmbAspectRatio.SelectedIndex = state.AspectRatioIndex;
                if (PanelCustomSize != null)
                {
                    bool isCustom = state.AspectRatioIndex == 6;
                    PanelCustomSize.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
                }
                TxtCustomWidth.Text = state.CustomWidth;
                TxtCustomHeight.Text = state.CustomHeight;
            }
            finally
            {
                _isSyncingUI = false;
            }

            UpdatePreviewImage();
        }

        #endregion

        #region Helpers

        private void TxtPrompt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingUI) return;
            if (sender is RichTextBox rtb && rtb == GetActivePromptRichTextBox())
            {
                var text = GetPromptText(rtb);
                if (_activeLayer != null && _layerStates.TryGetValue(_activeLayer, out var state))
                {
                    state.Prompt = text;
                    _activeLayer.LayerAiPrompt = text;
                }
                UpdateSendButtonsState();
            }
        }

        private void UpdateSendButtonsState()
        {
            BtnSend.IsEnabled = true;
            if (BtnSendWv != null) BtnSendWv.IsEnabled = true;
            if (BtnSendWeb != null) BtnSendWeb.IsEnabled = true;
        }

        private static void CleanupPlaceholders(List<EditorLayer> placeholders, EditorLayer parent, Window? owner)
        {
            if (placeholders == null || placeholders.Count == 0) return;
            try
            {
                foreach (var layer in placeholders)
                {
                    parent?.ChildLayers.Remove(layer);
                }
            }
            catch { }
        }

        private static void ProcessAndApplyAiImage(EditorLayer childLayer, BitmapSource aiBmp, EditorLayer activeLayer, Rect originalBounds, double? targetRatio, int? customW, int? customH)
        {
            if (childLayer == null || aiBmp == null || activeLayer == null) return;

            try
            {
                int layerW = activeLayer.Width;
                int layerH = activeLayer.Height;

                BitmapSource resizedAi;
                if (customW.HasValue && customH.HasValue && customW.Value > 0 && customH.Value > 0)
                {
                    resizedAi = ResizeBitmapHighQuality(aiBmp, customW.Value, customH.Value, uniformToFill: true);
                }
                else if (targetRatio.HasValue && targetRatio.Value > 0)
                {
                    int targetW = layerW;
                    int targetH = (int)Math.Round(layerW / targetRatio.Value);
                    resizedAi = ResizeBitmapHighQuality(aiBmp, targetW, targetH, uniformToFill: true);
                }
                else
                {
                    resizedAi = ResizeBitmapHighQuality(aiBmp, layerW, layerH, uniformToFill: true);
                }

                int stride = resizedAi.PixelWidth * 4;
                var pixels = new byte[stride * resizedAi.PixelHeight];
                resizedAi.CopyPixels(pixels, stride, 0);

                childLayer.Bitmap = new WriteableBitmap(resizedAi.PixelWidth, resizedAi.PixelHeight, 96, 96, PixelFormats.Bgra32, null);
                childLayer.Bitmap.WritePixels(new Int32Rect(0, 0, resizedAi.PixelWidth, resizedAi.PixelHeight), pixels, stride, 0);
                childLayer.InvalidateThumbnail();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to process AI image for layer: {ex.Message}");
            }
        }

        #endregion
    }

    #region Layer AI WebView Caching & Dialog Management

    public static class LayerAiWebViewCache
    {
        private static readonly Dictionary<string, CachedWebViewState> _cache = new();

        public class CachedTabState
        {
            public ChromiumWebBrowser? WebView { get; set; }
            public string Url { get; set; } = "https://google.com";
            public string Title { get; set; } = "New Tab";
            public string ProfileName { get; set; } = "Shared";
        }

        public class CachedWebViewState
        {
            public ChromiumWebBrowser? DynamicWebView { get; set; }
            public List<CachedTabState> WebBrowsers { get; set; } = new();
            public string SplitMode { get; set; } = "Single";
            public int ActiveTabIdx { get; set; } = 0;
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

                // Stop sleep timer if running
                state.SleepTimer?.Stop();
                state.SleepTimer?.Dispose();
                state.SleepTimer = null;

                return state;
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
                            foreach (var tabState in state.WebBrowsers)
                            {
                                tabState.WebView?.Dispose();
                            }
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
                var keys = Enumerable.ToList(_cache.Keys);
                foreach (var key in keys)
                {
                    DisposeAll(key);
                }
            }
        }
    }

    public static class LayerAiDialogManager
    {
        private class DialogCacheItem
        {
            public LayerAiDialog Dialog { get; set; }
            public System.Threading.Timer? IdleTimer { get; set; }
            public string NodeId { get; set; }
        }

        private static readonly Dictionary<string, DialogCacheItem> _cache = new();
        private static readonly object _lock = new();

        public static LayerAiDialog OpenDialog(List<EditorLayer> selectedLayers, EditorLayer activeLayer, ImageProcessingNode node, IWorkflowEditorHost host, EditorDocument doc, Window? owner)
        {
            lock (_lock)
            {
                string nodeId = node.Id;
                if (_cache.TryGetValue(nodeId, out var item) && item.Dialog != null)
                {
                    // Cancel 3-minute idle timer
                    item.IdleTimer?.Dispose();
                    item.IdleTimer = null;

                    bool isClosedWindow = false;
                    try
                    {
                        item.Dialog.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                item.Dialog.ReinitializeSession(selectedLayers, activeLayer, node, host, doc, owner);
                                if (!item.Dialog.IsVisible)
                                {
                                    item.Dialog.Show();
                                }
                                item.Dialog.Activate();
                                item.Dialog.Topmost = true;
                            }
                            catch (InvalidOperationException)
                            {
                                isClosedWindow = true;
                            }
                        });
                    }
                    catch
                    {
                        isClosedWindow = true;
                    }

                    if (!isClosedWindow)
                    {
                        return item.Dialog;
                    }

                    _cache.Remove(nodeId);
                }

                // Create a fresh dialog instance
                var newDialog = new LayerAiDialog(selectedLayers, activeLayer, node, host, doc, owner);
                var newItem = new DialogCacheItem
                {
                    Dialog = newDialog,
                    NodeId = nodeId
                };
                _cache[nodeId] = newItem;

                newDialog.Show();
                newDialog.Activate();
                return newDialog;
            }
        }

        public static void OnDialogHidden(string nodeId)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(nodeId, out var item))
                {
                    // Start or reset 3-minute (180,000 ms) idle timer
                    item.IdleTimer?.Dispose();
                    item.IdleTimer = new System.Threading.Timer(OnIdleTimerExpired, nodeId, TimeSpan.FromMinutes(3), System.Threading.Timeout.InfiniteTimeSpan);
                }
            }
        }

        private static void OnIdleTimerExpired(object? state)
        {
            if (state is string nodeId)
            {
                CloseAndDisposeDialog(nodeId);
            }
        }

        public static void CloseAndDisposeDialog(string nodeId)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(nodeId, out var item))
                {
                    item.IdleTimer?.Dispose();
                    item.IdleTimer = null;

                    try
                    {
                        item.Dialog?.Dispatcher.Invoke(() =>
                        {
                            item.Dialog.ForceClose();
                        });
                    }
                    catch { }

                    _cache.Remove(nodeId);
                }
                try
                {
                    LayerAiWebViewCache.DisposeAll(nodeId);
                }
                catch { }
            }
        }

        public static void CloseAll()
        {
            lock (_lock)
            {
                var keys = Enumerable.ToList(_cache.Keys);
                foreach (var key in keys)
                {
                    CloseAndDisposeDialog(key);
                }
            }
        }
    }

    #region Converters
    public class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }

    public class InverseBoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Collapsed;
        }
    }
    #endregion

    #endregion
}
