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
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace FlowMy.Views.Overlays
{
    public partial class LayerAiDialog : Window
    {
        public static readonly ConcurrentQueue<string> PendingExecutionIds = new ConcurrentQueue<string>();

        public class LayerAiExecutionScope
        {
            public string ExecutionId { get; set; } = string.Empty;
            public EditorLayer MainLayer { get; set; } = null!;
            public int AspectRatioIndex { get; set; }
            public List<SecondaryImageItem> SecondaryImages { get; set; } = new();
            public List<EditorLayer> Placeholders { get; set; } = new();
        }

        public static readonly ConcurrentDictionary<string, LayerAiExecutionScope> ActiveExecutionScopes = new();

        public class CodeCropMappingInfo
        {
            public string CodeId { get; set; } = string.Empty;
            public EditorLayer TargetLayer { get; set; } = null!;
            public SecondaryImageItem? SecondaryImage { get; set; }
            public int AspectRatioIndex { get; set; }
            public string ExecutionId { get; set; } = string.Empty;
            public int SecondaryImageIndex { get; set; } = -1;
        }

        public static readonly ConcurrentDictionary<string, CodeCropMappingInfo> CropGuidRegistry = new();

        private EditorLayer _activeLayer;
        private readonly System.Collections.Generic.List<EditorLayer> _selectedLayers;
        private readonly System.Collections.Generic.Dictionary<EditorLayer, LayerAiState> _layerStates = new();
        private bool _isSyncingUI = false;

        private readonly ImageProcessingNode _node;
        private readonly IWorkflowEditorHost _host;
        private readonly EditorDocument _doc;
        private Window? _ownerWindow;

        // Secondary images management
        public static SecondaryImageItem? DraggedHistoryItem { get; set; }

        public class SecondaryImageItem
        {
            public string CodeId { get; set; } = Guid.NewGuid().ToString("N");
            public string? ImageId { get; set; }
            public BitmapSource? Bitmap { get; set; }
            public string? FilePath { get; set; }
            public bool IsSelected { get; set; } = true;
            public bool HasImage => Bitmap != null;
            public System.Collections.Generic.Dictionary<int, string> AspectRatioIds { get; } = new System.Collections.Generic.Dictionary<int, string>();
            public System.Collections.Generic.List<SecondaryImageItem> SavedChildImages { get; } = new System.Collections.Generic.List<SecondaryImageItem>();

            public string? GetImageId(int aspectIndex = 0)
            {
                if (!string.IsNullOrWhiteSpace(ImageId)) return ImageId;
                if (AspectRatioIds.TryGetValue(aspectIndex, out var id) && !string.IsNullOrWhiteSpace(id))
                    return id;
                return null;
            }

            public void SetImageId(int aspectIndex, string? id)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    ImageId = null;
                    AspectRatioIds.Remove(aspectIndex);
                }
                else
                {
                    ImageId = id.Trim();
                    AspectRatioIds[aspectIndex] = id.Trim();
                }
            }

            public void ResetImageIdAndCodeId()
            {
                ImageId = null;
                AspectRatioIds.Clear();
                CodeId = Guid.NewGuid().ToString("N");
            }

            public void SetNewImage(BitmapSource? newBitmap, string? filePath = null)
            {
                ArchiveCurrentIfHasId();
                Bitmap = newBitmap;
                FilePath = filePath;
                ResetImageIdAndCodeId();
                IsSelected = newBitmap != null;
            }

            /// <summary>
            /// Lưu ảnh hiện tại (nếu đã có ID) vào danh sách ô ảnh con nhỏ trước khi thay bằng ảnh mới.
            /// </summary>
            public void ArchiveCurrentIfHasId()
            {
                if (HasImage && (!string.IsNullOrWhiteSpace(ImageId) || AspectRatioIds.Count > 0))
                {
                    string activeId = GetImageId(0) ?? ImageId ?? string.Empty;
                    bool exists = !string.IsNullOrEmpty(activeId) && SavedChildImages.Any(c => string.Equals(c.ImageId, activeId, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                    {
                        var child = new SecondaryImageItem
                        {
                            CodeId = CodeId,
                            ImageId = activeId,
                            Bitmap = Bitmap,
                            FilePath = FilePath,
                            IsSelected = IsSelected
                        };
                        foreach (var kvp in AspectRatioIds)
                        {
                            child.AspectRatioIds[kvp.Key] = kvp.Value;
                        }
                        SavedChildImages.Add(child);
                    }
                }
            }
        }

        private class LayerAiState
        {
            public string Prompt { get; set; } = string.Empty;
            public int BatchSizeIndex { get; set; } = 2; // Default size index (usually 3)
            public int AspectRatioIndex { get; set; } = 3; // Default ratio (1:1)
            public string CustomWidth { get; set; } = string.Empty;
            public string CustomHeight { get; set; } = string.Empty;
            public int SlotCount { get; set; } = 4;
            public List<SecondaryImageItem> SecondaryImages { get; } = new List<SecondaryImageItem>
            {
                new SecondaryImageItem(),
                new SecondaryImageItem(),
                new SecondaryImageItem(),
                new SecondaryImageItem()
            };
        }

        private readonly List<SecondaryImageItem> _secondaryImages = new List<SecondaryImageItem>
        {
            new SecondaryImageItem(),
            new SecondaryImageItem(),
            new SecondaryImageItem(),
            new SecondaryImageItem()
        };
        private int _secondarySlotCount = 4;
        private bool _isUpdatingSlotCount = false;

        // Cached references to UI elements for each slot (dynamically built)
        private List<Border> _slotBorders = new();
        private List<Image> _slotImages = new();
        private List<TextBlock> _slotPlaceholders = new();
        private List<Border> _slotChecks = new();
        private List<Border> _slotRemoves = new();
        private List<Border> _slotBordersWv = new();
        private List<Image> _slotImagesWv = new();
        private List<TextBlock> _slotPlaceholdersWv = new();
        private List<Border> _slotChecksWv = new();
        private List<Border> _slotRemovesWv = new();
        private List<StackPanel> _slotChildPanels = new();
        private List<StackPanel> _slotChildPanelsWv = new();

        // Tab + dialog state
        private double _originalWidth;
        private double _originalHeight;
        private FrameworkElement? _hoveredImageContainer = null;
        private bool _isMouseDownOnImage = false;
        private bool _isAiLoading = false;
        private bool _sendModeOn = true;
        private bool _isCombinedMode = true;

        public LayerAiDialog(System.Collections.Generic.List<EditorLayer> selectedLayers, EditorLayer activeLayer, ImageProcessingNode node, IWorkflowEditorHost host, EditorDocument doc, Window? owner)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            InitializeComponent();
            _ownerWindow = owner;
            Owner = owner;

            // Handle Activated and Deactivated to dynamically control Topmost
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
                IsClosed = true;
                SaveActiveLayerState();
                if (_ownerWindow != null)
                {
                    // Đưa owner lên trước (nếu đang active) rồi tắt Topmost để không che các dialog/app khác
                    if (_ownerWindow.IsActive) _ownerWindow.Activate();
                    _ownerWindow.Topmost = false;
                }
                UnsubscribeFromViewModelEvents();
                if (_node != null)
                {
                    LayerAiDialogManager.RemoveFromCache(_node.Id);
                }
                try
                {
                    LayerAiWebViewCache.DisposeAll(_node.Id);
                }
                catch { }
            };

            this.Loaded += (s, e) => ReactivateActiveWebBrowsers();
            this.IsVisibleChanged += (s, e) =>
            {
                if (this.IsVisible)
                {
                    ReactivateActiveWebBrowsers();
                }
            };

            _node = node ?? throw new ArgumentNullException(nameof(node));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));

            _selectedLayers = selectedLayers ?? new System.Collections.Generic.List<EditorLayer> { activeLayer };
            _activeLayer = activeLayer ?? _selectedLayers[0];

            _originalWidth = Width;
            _originalHeight = Height;

            // Build dynamic slot grids (default 4 slots)
            _secondarySlotCount = _activeLayer.LayerAiSecondarySlotCount > 0 ? _activeLayer.LayerAiSecondarySlotCount : 4;
            RebuildSecondaryGrid(_secondarySlotCount);
            RebuildSecondaryGridWv(_secondarySlotCount);
            UpdateSlotCountUI(_secondarySlotCount);

            // Pre-create and initialize AI state cache for all selected layers
            foreach (var layer in _selectedLayers)
            {
                _layerStates[layer] = CreateStateForLayer(layer);
            }

            // Load saved settings for active layer
            LoadSavedSettings();

            _isCombinedMode = _node.LayerAiIsCombinedMode;
            UpdateImageModeButtonUI();

            // Sync prompt from node to prompt boxes
            LoadActiveLayerState();
            ClearPromptOnOpen();

            // Setup two-way drag and drop between WPF and WebView2
            SetupDragAndDrop();

            // Listen to Ctrl+V paste event when hovering over images/slots
            this.PreviewKeyDown += LayerAiDialog_PreviewKeyDown;

            // Hook activity events to reset the owner FloatingWidgetWindow's idle timer upon interaction
            HookActivityEvents(this);

            SubscribeToViewModelEvents();

            // Ensure required dynamic outputs always exist on the node so downstream connections aren't lost on load
            GetOrAddDynamicOutputPort("prompt", "Layer AI - Prompt");
            GetOrAddDynamicOutputPort("promptJson", "Layer AI - Prompt JSON (Multimodal Parts)");
            GetOrAddDynamicOutputPort("mainCodeId", "Layer AI - Main Code ID");
            GetOrAddDynamicOutputPort("cropListObjects", "Layer AI - Crops List Objects (JSON)", FlowMy.Models.WorkflowDataType.ArrayDynamic, isMultiple: true);


            // Populate horizontal and vertical lists of selected layers
            UpdateSelectedLayersLists();

            EventHandler onProfilesChanged = (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(() => LoadWebProfiles()), System.Windows.Threading.DispatcherPriority.Normal);
            };
            WebNodeCacheHelper.ProfilesChanged += onProfilesChanged;
            Unloaded += (s, e) =>
            {
                WebNodeCacheHelper.ProfilesChanged -= onProfilesChanged;
            };
        }

        public LayerAiDialog(EditorLayer activeLayer, ImageProcessingNode node, IWorkflowEditorHost host, EditorDocument doc, Window? owner)
            : this(new System.Collections.Generic.List<EditorLayer> { activeLayer }, activeLayer, node, host, doc, owner)
        {
        }

        public bool IsClosed { get; private set; }

        private bool _isForceClosing = false;

        public void ForceClose()
        {
            _isForceClosing = true;
            try { Close(); } catch { }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isForceClosing)
            {
                e.Cancel = true;
                Hide();
                SaveActiveLayerState();
                if (_ownerWindow != null)
                {
                    if (_ownerWindow.IsActive) _ownerWindow.Activate();
                    _ownerWindow.Topmost = false;
                }
                LayerAiDialogManager.OnDialogHidden(_node.Id);
                return;
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            IsClosed = true;
            base.OnClosed(e);
            if (_node != null)
            {
                LayerAiDialogManager.RemoveFromCache(_node.Id);
            }
        }

        public void ReinitializeSession(System.Collections.Generic.List<EditorLayer> selectedLayers, EditorLayer activeLayer, ImageProcessingNode node, IWorkflowEditorHost host, EditorDocument doc, Window? owner)
        {
            Owner = owner;
            _ownerWindow = owner;

            _selectedLayers.Clear();
            if (selectedLayers != null && selectedLayers.Count > 0)
            {
                _selectedLayers.AddRange(selectedLayers);
            }
            else if (activeLayer != null)
            {
                _selectedLayers.Add(activeLayer);
            }

            _activeLayer = activeLayer ?? _selectedLayers[0];

            foreach (var layer in _selectedLayers)
            {
                if (!_layerStates.ContainsKey(layer) || (layer.LayerAiSecondaryImages != null && layer.LayerAiSecondaryImages.Count > 0))
                {
                    _layerStates[layer] = CreateStateForLayer(layer);
                }
            }

            _secondarySlotCount = _activeLayer.LayerAiSecondarySlotCount > 0 ? _activeLayer.LayerAiSecondarySlotCount : 4;
            RebuildSecondaryGrid(_secondarySlotCount);
            RebuildSecondaryGridWv(_secondarySlotCount);
            UpdateSlotCountUI(_secondarySlotCount);

            LoadActiveLayerState();
            UpdateSelectedLayersLists();
            ClearPromptOnOpen();

            if (_activeTab == ActiveTab.WebBrowser || _activeTab == ActiveTab.WebView)
            {
                ReactivateActiveWebBrowsers();
            }

            SubscribeToViewModelEvents();
        }

        private void ClearPromptOnOpen()
        {
            _node.ProcessorPrompt = string.Empty;
            if (_activeLayer != null)
            {
                _activeLayer.LayerAiPrompt = string.Empty;
            }
            foreach (var state in _layerStates.Values)
            {
                if (state != null)
                {
                    state.Prompt = string.Empty;
                }
            }
            try { SetRichText(TxtPrompt, string.Empty); } catch { }
            try { if (TxtPromptWv != null) SetRichText(TxtPromptWv, string.Empty); } catch { }
            try { if (TxtPromptWeb != null) SetRichText(TxtPromptWeb, string.Empty); } catch { }
        }

        private LayerAiState CreateStateForLayer(EditorLayer layer)
        {
            var state = new LayerAiState();
            
            // 1. If the layer already has saved secondary images or prompt, restore state from it!
            if ((layer.LayerAiSecondaryImages != null && layer.LayerAiSecondaryImages.Count > 0) || !string.IsNullOrEmpty(layer.LayerAiPrompt))
            {
                state.Prompt = layer.LayerAiPrompt;
                state.BatchSizeIndex = layer.LayerAiBatchSizeIndex;
                state.AspectRatioIndex = layer.LayerAiAspectRatioIndex;
                state.CustomWidth = layer.LayerAiCustomWidth;
                state.CustomHeight = layer.LayerAiCustomHeight;
                state.SlotCount = layer.LayerAiSecondarySlotCount > 0 ? layer.LayerAiSecondarySlotCount : 4;
                state.SecondaryImages.Clear();
                foreach (var src in layer.LayerAiSecondaryImages)
                {
                    var secItem = new SecondaryImageItem
                    {
                        ImageId = src.ImageId,
                        FilePath = src.FilePath,
                        IsSelected = src.Bitmap != null && src.IsSelected,
                        Bitmap = src.Bitmap
                    };
                    if (src.AspectRatioIds != null)
                    {
                        foreach (var kvp in src.AspectRatioIds)
                        {
                            secItem.AspectRatioIds[kvp.Key] = kvp.Value;
                        }
                    }
                    if (src.SavedChildImages != null)
                    {
                        foreach (var child in src.SavedChildImages)
                        {
                            var childItem = new SecondaryImageItem
                            {
                                ImageId = child.ImageId,
                                FilePath = child.FilePath,
                                IsSelected = child.IsSelected,
                                Bitmap = child.Bitmap
                            };
                            if (child.AspectRatioIds != null)
                            {
                                foreach (var kvp in child.AspectRatioIds)
                                {
                                    childItem.AspectRatioIds[kvp.Key] = kvp.Value;
                                }
                            }
                            secItem.SavedChildImages.Add(childItem);
                        }
                    }
                    state.SecondaryImages.Add(secItem);
                }
                // Ensure at least slotCount items
                while (state.SecondaryImages.Count < state.SlotCount)
                    state.SecondaryImages.Add(new SecondaryImageItem());
                return state;
            }

            // 2. Otherwise, fallback to dynamic output values or node-level configurations
            var savedPrompt = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "prompt", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            state.Prompt = !string.IsNullOrEmpty(savedPrompt) ? savedPrompt : (_node.ProcessorPrompt ?? string.Empty);

            // Default batch size
            var savedSize = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "promptSize", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedSize) && int.TryParse(savedSize, out var bSize))
            {
                state.BatchSizeIndex = Math.Clamp(bSize - 1, 0, 3);
            }
            else
            {
                state.BatchSizeIndex = Math.Clamp(_node.PromptSize - 1, 0, 3);
            }

            // Default aspect ratio
            var savedAspect = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "aspectRatio", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            if (!string.IsNullOrEmpty(savedAspect))
            {
                state.AspectRatioIndex = savedAspect switch
                {
                    "16:9" => 1,
                    "4:3" => 2,
                    "1:1" => 3,
                    "3:4" => 4,
                    "9:16" => 5,
                    "Free" => 6,
                    _ => 3
                };
            }
            else
            {
                state.AspectRatioIndex = 3; // 1:1
            }

            // Default custom width/height
            BitmapSource sourceImg = layer.OriginalTransformBitmap ?? layer.Bitmap;
            var bounds = GetLayerContentBounds(sourceImg);

            var savedWidth = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropWidth", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            state.CustomWidth = !string.IsNullOrEmpty(savedWidth) ? savedWidth : (!bounds.IsEmpty && bounds.Width > 0 ? ((int)bounds.Width).ToString() : "512");

            var savedHeight = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "cropHeight", StringComparison.OrdinalIgnoreCase))?.UserValueOverride;
            state.CustomHeight = !string.IsNullOrEmpty(savedHeight) ? savedHeight : (!bounds.IsEmpty && bounds.Height > 0 ? ((int)bounds.Height).ToString() : "512");

            return state;
        }

        private void SaveActiveLayerState()
        {
            if (_activeLayer == null || !_layerStates.TryGetValue(_activeLayer, out var state)) return;

            state.Prompt = GetRichText(TxtPrompt);
            state.BatchSizeIndex = CmbBatchSize.SelectedIndex;
            state.AspectRatioIndex = CmbAspectRatio.SelectedIndex;
            state.CustomWidth = TxtCustomWidth.Text;
            state.CustomHeight = TxtCustomHeight.Text;
            state.SlotCount = _secondarySlotCount;

            state.SecondaryImages.Clear();
            foreach (var img in _secondaryImages)
            {
                var secItem = new SecondaryImageItem
                {
                    ImageId = img.ImageId,
                    Bitmap = img.Bitmap,
                    FilePath = img.FilePath,
                    IsSelected = img.IsSelected
                };
                foreach (var kvp in img.AspectRatioIds)
                {
                    secItem.AspectRatioIds[kvp.Key] = kvp.Value;
                }
                foreach (var child in img.SavedChildImages)
                {
                    secItem.SavedChildImages.Add(new SecondaryImageItem
                    {
                        ImageId = child.ImageId,
                        Bitmap = child.Bitmap,
                        FilePath = child.FilePath,
                        IsSelected = child.IsSelected
                    });
                }
                state.SecondaryImages.Add(secItem);
            }

            // Sync to the EditorLayer properties!
            _activeLayer.LayerAiPrompt = state.Prompt;
            _activeLayer.LayerAiBatchSizeIndex = state.BatchSizeIndex;
            _activeLayer.LayerAiAspectRatioIndex = state.AspectRatioIndex;
            _activeLayer.LayerAiCustomWidth = state.CustomWidth;
            _activeLayer.LayerAiCustomHeight = state.CustomHeight;
            _activeLayer.LayerAiSecondarySlotCount = _secondarySlotCount;

            _activeLayer.LayerAiSecondaryImages.Clear();
            foreach (var img in state.SecondaryImages)
            {
                var layerImg = new EditorLayer.LayerAiSecondaryImage
                {
                    ImageId = img.ImageId,
                    FilePath = img.FilePath,
                    IsSelected = img.IsSelected,
                    Bitmap = img.Bitmap
                };
                foreach (var kvp in img.AspectRatioIds)
                {
                    layerImg.AspectRatioIds[kvp.Key] = kvp.Value;
                }
                foreach (var child in img.SavedChildImages)
                {
                    var childLayerImg = new EditorLayer.LayerAiSecondaryImage
                    {
                        ImageId = child.ImageId,
                        FilePath = child.FilePath,
                        IsSelected = child.IsSelected,
                        Bitmap = child.Bitmap
                    };
                    if (child.AspectRatioIds != null)
                    {
                        foreach (var kvp in child.AspectRatioIds)
                        {
                            childLayerImg.AspectRatioIds[kvp.Key] = kvp.Value;
                        }
                    }
                    if (child.Bitmap is BitmapSource childBmp)
                    {
                        try
                        {
                            using (var ms = new MemoryStream())
                            {
                                var enc = new PngBitmapEncoder();
                                enc.Frames.Add(BitmapFrame.Create(childBmp));
                                enc.Save(ms);
                                childLayerImg.PngBytes = ms.ToArray();
                            }
                        }
                        catch { }
                    }
                    layerImg.SavedChildImages.Add(childLayerImg);
                }

                // Sync PNG bytes
                if (img.Bitmap is BitmapSource bmp)
                {
                    try
                    {
                        using (var ms = new MemoryStream())
                        {
                            var enc = new PngBitmapEncoder();
                            enc.Frames.Add(BitmapFrame.Create(bmp));
                            enc.Save(ms);
                            layerImg.PngBytes = ms.ToArray();
                        }
                    }
                    catch
                    {
                        layerImg.PngBytes = null;
                    }
                }
                _activeLayer.LayerAiSecondaryImages.Add(layerImg);
            }
        }

        private void LoadActiveLayerState()
        {
            if (_activeLayer == null || !_layerStates.TryGetValue(_activeLayer, out var state)) return;

            _isSyncingUI = true;
            try
            {
                SetRichText(TxtPrompt, state.Prompt);
                if (TxtPromptWv != null) SetRichText(TxtPromptWv, state.Prompt);
                if (TxtPromptWeb != null) SetRichText(TxtPromptWeb, state.Prompt);

                CmbBatchSize.SelectedIndex = state.BatchSizeIndex;
                CmbAspectRatio.SelectedIndex = state.AspectRatioIndex;
                if (PanelCustomSize != null)
                {
                    PanelCustomSize.Visibility = (state.AspectRatioIndex == 6) ? Visibility.Visible : Visibility.Collapsed;
                }
                TxtCustomWidth.Text = state.CustomWidth;
                TxtCustomHeight.Text = state.CustomHeight;

                // Restore slot count and rebuild grids if needed
                int newSlotCount = state.SlotCount > 0 ? state.SlotCount : 4;
                if (newSlotCount != _secondarySlotCount)
                {
                    _secondarySlotCount = newSlotCount;
                    EnsureSecondaryImagesCount(_secondarySlotCount);
                    RebuildSecondaryGrid(_secondarySlotCount);
                    RebuildSecondaryGridWv(_secondarySlotCount);
                    UpdateSlotCountUI(_secondarySlotCount);
                }

                _secondaryImages.Clear();
                foreach (var src in state.SecondaryImages)
                {
                    var secItem = new SecondaryImageItem
                    {
                        ImageId = src.ImageId,
                        Bitmap = src.Bitmap,
                        FilePath = src.FilePath,
                        IsSelected = src.IsSelected
                    };
                    foreach (var kvp in src.AspectRatioIds)
                    {
                        secItem.AspectRatioIds[kvp.Key] = kvp.Value;
                    }
                    foreach (var child in src.SavedChildImages)
                    {
                        secItem.SavedChildImages.Add(new SecondaryImageItem
                        {
                            ImageId = child.ImageId,
                            Bitmap = child.Bitmap,
                            FilePath = child.FilePath,
                            IsSelected = child.IsSelected
                        });
                    }
                    _secondaryImages.Add(secItem);
                }
                // Ensure at least slotCount items
                while (_secondaryImages.Count < _secondarySlotCount)
                    _secondaryImages.Add(new SecondaryImageItem());

                UpdatePreviewImage();
                RefreshAllSlotsUI();
                UpdateSelectedLayersListsHighlight();
            }
            finally
            {
                _isSyncingUI = false;
            }
        }

        private void UpdateSelectedLayersLists()
        {
            if (SelectedImagesHorizontalList == null || SelectedImagesVerticalList == null) return;

            SelectedImagesHorizontalList.Children.Clear();
            SelectedImagesVerticalList.Children.Clear();

            var visibility = _selectedLayers.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            BottomSelectedLayersPanel.Visibility = visibility;
            RightSelectedLayersPanel.Visibility = visibility;

            if (_selectedLayers.Count <= 1) return;

            for (int i = 0; i < _selectedLayers.Count; i++)
            {
                var layer = _selectedLayers[i];
                int index = i + 1;

                var itemH = CreateLayerListItem(layer, index, isVertical: false);
                SelectedImagesHorizontalList.Children.Add(itemH);

                var itemV = CreateLayerListItem(layer, index, isVertical: true);
                SelectedImagesVerticalList.Children.Add(itemV);
            }

            UpdateSelectedLayersListsHighlight();
        }

        private FrameworkElement CreateLayerListItem(EditorLayer layer, int index, bool isVertical)
        {
            var border = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1.5),
                Background = new SolidColorBrush(Color.FromRgb(21, 23, 30)),
                Cursor = Cursors.Hand,
                Margin = isVertical ? new Thickness(0, 3, 0, 3) : new Thickness(3, 0, 3, 0),
                Tag = layer
            };

            border.ToolTip = $"Ảnh {index}: {layer.Name}";

            var grid = new Grid();
            var rect = new System.Windows.Shapes.Rectangle
            {
                Fill = TryFindResource("PsDarkCheckeredBrush") as Brush ?? Brushes.Black,
                SnapsToDevicePixels = true
            };
            grid.Children.Add(rect);

            var img = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(1.5),
                Source = layer.Bitmap
            };
            grid.Children.Add(img);

            var badgeBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Color.FromArgb(170, 17, 19, 24)),
                CornerRadius = new CornerRadius(0, 0, 4, 0),
                Padding = new Thickness(3, 1, 3, 1)
            };
            var badgeText = new TextBlock
            {
                Text = index.ToString(),
                Foreground = new SolidColorBrush(Color.FromRgb(221, 227, 239)),
                FontSize = 8,
                FontWeight = FontWeights.Bold
            };
            badgeBorder.Child = badgeText;
            grid.Children.Add(badgeBorder);

            border.Child = grid;

            border.MouseLeftButtonDown += (s, e) =>
            {
                if (s is Border b && b.Tag is EditorLayer clickedLayer)
                {
                    if (clickedLayer != _activeLayer)
                    {
                        SaveActiveLayerState();
                        _activeLayer = clickedLayer;
                        LoadActiveLayerState();
                    }
                }
                e.Handled = true;
            };

            return border;
        }

        private void UpdateSelectedLayersListsHighlight()
        {
            int activeIndex = _selectedLayers.IndexOf(_activeLayer);
            if (activeIndex < 0) return;

            string countText = $"Ảnh {activeIndex + 1}/{_selectedLayers.Count}";
            string countTextWv = $"{activeIndex + 1}/{_selectedLayers.Count}";

            if (TxtSelectedCount != null) TxtSelectedCount.Text = countText;
            if (TxtSelectedCountWv != null) TxtSelectedCountWv.Text = countTextWv;

            var accentColor = TryFindResource("AccentColor") as Brush ?? new SolidColorBrush(Color.FromRgb(79, 255, 176));
            var borderColor = TryFindResource("BorderColor") as Brush ?? new SolidColorBrush(Color.FromRgb(42, 46, 61));

            if (SelectedImagesHorizontalList != null)
            {
                foreach (FrameworkElement item in SelectedImagesHorizontalList.Children)
                {
                    if (item is Border b && b.Tag is EditorLayer layer)
                    {
                        b.BorderBrush = (layer == _activeLayer) ? accentColor : borderColor;
                        b.BorderThickness = (layer == _activeLayer) ? new Thickness(2.0) : new Thickness(1.5);
                    }
                }
            }

            if (SelectedImagesVerticalList != null)
            {
                foreach (FrameworkElement item in SelectedImagesVerticalList.Children)
                {
                    if (item is Border b && b.Tag is EditorLayer layer)
                    {
                        b.BorderBrush = (layer == _activeLayer) ? accentColor : borderColor;
                        b.BorderThickness = (layer == _activeLayer) ? new Thickness(2.0) : new Thickness(1.5);
                    }
                }
            }
        }

        private void HookActivityEvents(UIElement element)
        {
            if (element == null) return;
            element.PreviewMouseMove += (s, e) => MarkOwnerActivity();
            element.PreviewMouseDown += (s, e) => MarkOwnerActivity();
            element.PreviewKeyDown += (s, e) => MarkOwnerActivity();
            element.PreviewMouseWheel += (s, e) => MarkOwnerActivity();
        }

        private void MarkOwnerActivity()
        {
            if (_ownerWindow is FloatingWidgetWindow widget)
            {
                widget.ResetIdleTimer();
            }
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
            try { DialogResult = false; } catch { }
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            try { DialogResult = false; } catch { }
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

            try
            {
                _host?.RequestSyncDataPanels(immediate: true);
            }
            catch { }
        }

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
            return _activeTab switch
            {
                ActiveTab.WebView => GetRichText(TxtPromptWv),
                ActiveTab.WebBrowser => GetRichText(TxtPromptWeb),
                _ => GetRichText(TxtPrompt)
            };
        }

        private void SyncPromptTo(ActiveTab target)
        {
            var text = GetActivePromptText();
            if (target != ActiveTab.Prompt) SetRichText(TxtPrompt, text);
            if (target != ActiveTab.WebView) SetRichText(TxtPromptWv, text);
            if (target != ActiveTab.WebBrowser) SetRichText(TxtPromptWeb, text);
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
