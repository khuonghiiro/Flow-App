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

            /// <summary>
            /// Lưu ảnh hiện tại (nếu đã có ID) vào danh sách ô ảnh con nhỏ trước khi thay bằng ảnh mới.
            /// </summary>
            public void ArchiveCurrentIfHasId()
            {
                if (HasImage && !string.IsNullOrWhiteSpace(ImageId))
                {
                    bool exists = SavedChildImages.Any(c => string.Equals(c.ImageId, ImageId, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                    {
                        SavedChildImages.Add(new SecondaryImageItem
                        {
                            CodeId = CodeId,
                            ImageId = ImageId,
                            Bitmap = Bitmap,
                            FilePath = FilePath,
                            IsSelected = IsSelected
                        });
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
                SaveActiveLayerState();
                if (_ownerWindow != null)
                {
                    // Đưa owner lên trước (nếu đang active) rồi tắt Topmost để không che các dialog/app khác
                    if (_ownerWindow.IsActive) _ownerWindow.Activate();
                    _ownerWindow.Topmost = false;
                }
                UnsubscribeFromViewModelEvents();
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

            // Setup two-way drag and drop between WPF and WebView2
            SetupDragAndDrop();

            // Listen to Ctrl+V paste event when hovering over images/slots
            this.PreviewKeyDown += LayerAiDialog_PreviewKeyDown;

            // Hook activity events to reset the owner FloatingWidgetWindow's idle timer upon interaction
            HookActivityEvents(this);

            SubscribeToViewModelEvents();

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
                if (!_layerStates.ContainsKey(layer))
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

            if (_activeTab == ActiveTab.WebBrowser || _activeTab == ActiveTab.WebView)
            {
                ReactivateActiveWebBrowsers();
            }

            SubscribeToViewModelEvents();
        }

        private LayerAiState CreateStateForLayer(EditorLayer layer)
        {
            var state = new LayerAiState();
            
            // 1. If the layer already has saved LayerAiPrompt, restore state from it!
            if (!string.IsNullOrEmpty(layer.LayerAiPrompt))
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
                        IsSelected = src.IsSelected,
                        Bitmap = src.Bitmap
                    };
                    foreach (var kvp in src.AspectRatioIds)
                    {
                        secItem.AspectRatioIds[kvp.Key] = kvp.Value;
                    }
                    if (src.SavedChildImages != null)
                    {
                        foreach (var child in src.SavedChildImages)
                        {
                            secItem.SavedChildImages.Add(new SecondaryImageItem
                            {
                                ImageId = child.ImageId,
                                FilePath = child.FilePath,
                                IsSelected = child.IsSelected,
                                Bitmap = child.Bitmap
                            });
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

            state.Prompt = TxtPrompt.Text;
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
                    layerImg.SavedChildImages.Add(new EditorLayer.LayerAiSecondaryImage
                    {
                        ImageId = child.ImageId,
                        FilePath = child.FilePath,
                        IsSelected = child.IsSelected,
                        Bitmap = child.Bitmap
                    });
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
                TxtPrompt.Text = state.Prompt;
                if (TxtPromptWv != null) TxtPromptWv.Text = state.Prompt;
                if (TxtPromptWeb != null) TxtPromptWeb.Text = state.Prompt;

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
                        var webView = new ChromiumWebBrowser();
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

        #region Secondary Images Slots

        private void BtnSlotPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int count) && count > 0)
            {
                SetSlotCount(count);
            }
        }

        private void TxtSlotCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingSlotCount) return;
            if (SecondaryImagesGrid == null || SecondaryImagesGridWv == null) return;
            if (int.TryParse(TxtSlotCount.Text.Trim(), out int count))
            {
                if (count <= 0)
                {
                    // Hide secondary images grid
                    SecondaryImagesGrid.Visibility = Visibility.Collapsed;
                    SecondaryImagesGridWv.Visibility = Visibility.Collapsed;
                    return;
                }
                SecondaryImagesGrid.Visibility = Visibility.Visible;
                SecondaryImagesGridWv.Visibility = Visibility.Visible;
                SetSlotCount(count);
            }
        }

        private void SetSlotCount(int count)
        {
            if (count < 1) count = 1;
            if (count > 100) count = 100; // Reasonable upper limit
            if (count == _secondarySlotCount) return;

            _secondarySlotCount = count;
            EnsureSecondaryImagesCount(count);
            RebuildSecondaryGrid(count);
            RebuildSecondaryGridWv(count);
            UpdateSlotCountUI(count);
            SetupDragAndDrop();
            RefreshAllSlotsUI();

            // Persist to active layer
            _activeLayer.LayerAiSecondarySlotCount = count;
        }

        private void EnsureSecondaryImagesCount(int count)
        {
            // Grow: add empty items
            while (_secondaryImages.Count < count)
                _secondaryImages.Add(new SecondaryImageItem());
            // We do NOT shrink — data is preserved but hidden
        }

        private void UpdateSlotCountUI(int count)
        {
            _isUpdatingSlotCount = true;
            try
            {
                if (TxtSlotCount != null) TxtSlotCount.Text = count.ToString();

                // Update header text
                if (TxtSecondaryHeader != null) TxtSecondaryHeader.Text = $"🖼️ ẢNH PHỤ ({count})";
                if (TxtSecondaryHeaderWv != null) TxtSecondaryHeaderWv.Text = $"🖼️ Ảnh phụ ({count})";

                // Update preset button highlights
                var presetButtons = new[] { BtnSlot4, BtnSlot6, BtnSlot8, BtnSlot10 };
                foreach (var btn in presetButtons)
                {
                    if (btn == null) continue;
                    bool isActive = btn.Tag is string t && int.TryParse(t, out int v) && v == count;
                    btn.Style = null; // Reset to allow direct property setting
                    btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#4fffb0" : "#252a39"));
                    btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#111318" : "#788296"));
                    // Re-apply template with CornerRadius
                    var template = new ControlTemplate(typeof(Button));
                    var borderFactory = new FrameworkElementFactory(typeof(Border));
                    borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                    borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                    borderFactory.SetValue(Border.PaddingProperty, new Thickness(2, 0, 2, 0));
                    var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
                    contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                    borderFactory.AppendChild(contentFactory);
                    template.VisualTree = borderFactory;
                    btn.Template = template;
                }
            }
            finally
            {
                _isUpdatingSlotCount = false;
            }
        }

        private void RebuildSecondaryGrid(int count)
        {
            SecondaryImagesGrid.Children.Clear();
            SecondaryImagesGrid.RowDefinitions.Clear();
            SecondaryImagesGrid.ColumnDefinitions.Clear();
            _slotBorders.Clear();
            _slotImages.Clear();
            _slotPlaceholders.Clear();
            _slotChecks.Clear();
            _slotRemoves.Clear();
            _slotChildPanels.Clear();

            if (count <= 0) return;

            // Calculate a balanced grid layout (square-ish)
            int cols = (int)Math.Ceiling(Math.Sqrt(count));
            int rows = (int)Math.Ceiling((double)count / cols);

            // Create row definitions with gaps
            for (int r = 0; r < rows; r++)
            {
                if (r > 0) SecondaryImagesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
                SecondaryImagesGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            // Create column definitions with gaps
            for (int c = 0; c < cols; c++)
            {
                if (c > 0) SecondaryImagesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                SecondaryImagesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Create slot borders in row-major order
            for (int i = 0; i < count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                int gridRow = row * 2; // account for gap rows
                int gridCol = col * 2; // account for gap cols

                var border = CreateSlotBorder(i, isCompact: false);
                Grid.SetRow(border, gridRow);
                Grid.SetColumn(border, gridCol);
                SecondaryImagesGrid.Children.Add(border);
            }
        }

        private void RebuildSecondaryGridWv(int count)
        {
            SecondaryImagesGridWv.Children.Clear();
            SecondaryImagesGridWv.RowDefinitions.Clear();
            SecondaryImagesGridWv.ColumnDefinitions.Clear();
            _slotBordersWv.Clear();
            _slotImagesWv.Clear();
            _slotPlaceholdersWv.Clear();
            _slotRemovesWv.Clear();
            _slotChildPanelsWv.Clear();

            if (count <= 0) return;

            // Calculate a balanced grid layout (square-ish)
            int cols = (int)Math.Ceiling(Math.Sqrt(count));
            int rows = (int)Math.Ceiling((double)count / cols);

            for (int r = 0; r < rows; r++)
            {
                if (r > 0) SecondaryImagesGridWv.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3) });
                SecondaryImagesGridWv.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            for (int c = 0; c < cols; c++)
            {
                if (c > 0) SecondaryImagesGridWv.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
                SecondaryImagesGridWv.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            for (int i = 0; i < count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                int gridRow = row * 2;
                int gridCol = col * 2;

                var border = CreateSlotBorder(i, isCompact: true);
                Grid.SetRow(border, gridRow);
                Grid.SetColumn(border, gridCol);
                SecondaryImagesGridWv.Children.Add(border);
            }
        }

        /// <summary>Create a single slot Border with all child elements (checkerboard, image, placeholder, check, remove, number badge).</summary>
        private Border CreateSlotBorder(int index, bool isCompact)
        {
            // SecondaryImageBorder style reference
            var borderStyle = (Style)FindResource("SecondaryImageBorder");

            var border = new Border
            {
                Style = borderStyle,
                Focusable = true,
                Tag = index.ToString()
            };
            border.KeyDown += SlotBorder_KeyDown;
            border.MouseEnter += Slot_MouseEnter;
            border.MouseLeave += Slot_MouseLeave;
            border.MouseLeftButtonDown += Slot_Click;

            var grid = new Grid();

            // Checkerboard
            var checkerBrush = (Brush)FindResource("PsDarkCheckeredBrush");
            var rect = new System.Windows.Shapes.Rectangle { Fill = checkerBrush, SnapsToDevicePixels = true };
            grid.Children.Add(rect);

            // Image
            var image = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(isCompact ? 1 : 2) };
            grid.Children.Add(image);

            // Placeholder
            var placeholder = new TextBlock
            {
                Text = "＋",
                FontSize = isCompact ? 16 : 24,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3a3f52")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            grid.Children.Add(placeholder);

            // Check badge (only normal, not compact)
            Border? checkBadge = null;
            if (!isCompact)
            {
                checkBadge = new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0")),
                    CornerRadius = new CornerRadius(0, 0, 6, 0),
                    Padding = new Thickness(4, 2, 4, 2),
                    Visibility = Visibility.Collapsed
                };
                checkBadge.Child = new TextBlock
                {
                    Text = "✓",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111318")),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };
                grid.Children.Add(checkBadge);
            }

            // Remove button
            var removeBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#cc3333")),
                CornerRadius = new CornerRadius(0, 0, 0, 6),
                Padding = isCompact ? new Thickness(4, 1, 4, 1) : new Thickness(5, 2, 5, 2),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                Tag = index.ToString()
            };
            removeBorder.Child = new TextBlock
            {
                Text = "✕",
                Foreground = Brushes.White,
                FontSize = isCompact ? 8 : 9,
                FontWeight = FontWeights.Bold
            };
            removeBorder.MouseLeftButtonDown += SlotRemove_Click;
            grid.Children.Add(removeBorder);

            // Number badge
            var numberBadge = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#aa111318")),
                CornerRadius = new CornerRadius(0, 4, 0, 0),
                Padding = new Thickness(5, 2, 5, 2),
                IsHitTestVisible = false
            };
            numberBadge.Child = new TextBlock
            {
                Text = (index + 1).ToString(),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dde3ef")),
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            grid.Children.Add(numberBadge);

            // Child thumbnails panel (ô ảnh con nhỏ khi ảnh phụ cũ có ID)
            var childScrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = isCompact ? new Thickness(0, 0, 2, 2) : new Thickness(0, 0, 4, 2),
                MaxHeight = isCompact ? 22 : 28,
                MaxWidth = isCompact ? 100 : 160
            };

            var childStackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            childScrollViewer.Content = childStackPanel;
            grid.Children.Add(childScrollViewer);

            border.Child = grid;

            // Register in the appropriate lists
            if (isCompact)
            {
                _slotBordersWv.Add(border);
                _slotImagesWv.Add(image);
                _slotPlaceholdersWv.Add(placeholder);
                _slotRemovesWv.Add(removeBorder);
                _slotChildPanelsWv.Add(childStackPanel);
            }
            else
            {
                _slotBorders.Add(border);
                _slotImages.Add(image);
                _slotPlaceholders.Add(placeholder);
                _slotChecks.Add(checkBadge!);
                _slotRemoves.Add(removeBorder);
                _slotChildPanels.Add(childStackPanel);
            }

            return border;
        }

        private void BtnAddSecondary_Click(object sender, RoutedEventArgs e)
        {
            // Find first empty slot
            int emptySlot = -1;
            for (int i = 0; i < _secondarySlotCount && i < _secondaryImages.Count; i++)
            {
                if (!_secondaryImages[i].HasImage)
                {
                    emptySlot = i;
                    break;
                }
            }

            if (emptySlot == -1)
            {
                MessageBox.Show($"Đã đủ {_secondarySlotCount} ảnh phụ. Hãy xóa ảnh cũ trước.", "Ảnh phụ", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    if (slotIdx >= _secondarySlotCount) break;

                    // Find next empty slot
                    while (slotIdx < _secondarySlotCount && slotIdx < _secondaryImages.Count && _secondaryImages[slotIdx].HasImage)
                        slotIdx++;
                    if (slotIdx >= _secondarySlotCount) break;

                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(file);
                        bmp.EndInit();
                        bmp.Freeze();

                        _secondaryImages[slotIdx].ArchiveCurrentIfHasId();
                        _secondaryImages[slotIdx].Bitmap = bmp;
                        _secondaryImages[slotIdx].FilePath = file;
                        _secondaryImages[slotIdx].ImageId = null;
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
                if (idx < 0 || idx >= _secondarySlotCount || idx >= _secondaryImages.Count) return;

                if (_secondaryImages[idx].HasImage)
                {
                    if (_sendModeOn)
                    {
                        // ON mode: toggle selection, allow multi-select
                        _secondaryImages[idx].IsSelected = !_secondaryImages[idx].IsSelected;
                        RefreshSlotUI(idx);
                        UpdateSecondaryInfo();
                        UpdatePreviewImage();
                    }
                    else
                    {
                        // OFF mode: toggle selection, enforce single-select
                        bool wasSelected = _secondaryImages[idx].IsSelected;
                        for (int i = 0; i < _secondaryImages.Count; i++)
                        {
                            _secondaryImages[i].IsSelected = false;
                        }
                        _secondaryImages[idx].IsSelected = !wasSelected;
                        
                        RefreshAllSlotsUI();
                        UpdateSecondaryInfo();
                        UpdatePreviewImage();
                    }
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

                            _secondaryImages[idx].ArchiveCurrentIfHasId();
                            if (_sendModeOn)
                            {
                                // ON mode: just add and select it
                                _secondaryImages[idx].Bitmap = bmp;
                                _secondaryImages[idx].FilePath = dlg.FileName;
                                _secondaryImages[idx].ImageId = null;
                                _secondaryImages[idx].IsSelected = true;
                            }
                            else
                            {
                                // OFF mode: deselect all others, select this one
                                for (int i = 0; i < _secondaryImages.Count; i++)
                                {
                                    _secondaryImages[i].IsSelected = false;
                                }
                                _secondaryImages[idx].Bitmap = bmp;
                                _secondaryImages[idx].FilePath = dlg.FileName;
                                _secondaryImages[idx].ImageId = null;
                                _secondaryImages[idx].IsSelected = true;
                            }
                        }
                        catch { }
                        RefreshAllSlotsUI();
                        UpdateSecondaryInfo();
                        UpdatePreviewImage();
                    }
                }

                e.Handled = true;
            }
        }

        private void SlotRemove_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
                {
                    _secondaryImages[idx].Bitmap = null;
                    _secondaryImages[idx].FilePath = null;
                    _secondaryImages[idx].IsSelected = false;
                    RefreshAllSlotsUI();
                    UpdateSecondaryInfo();
                    UpdatePreviewImage();
                }
                e.Handled = true;
            }
        }

        private void Slot_MouseEnter(object sender, MouseEventArgs e)
        {
            _hoveredImageContainer = sender as FrameworkElement;
            if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
            {
                if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
                {
                    // Hover glow effect
                    border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
                    border.BorderThickness = new Thickness(2);

                    // Show remove button if has image
                    if (_secondaryImages[idx].HasImage)
                    {
                        if (idx < _slotRemoves.Count) _slotRemoves[idx].Visibility = Visibility.Visible;
                        if (idx < _slotRemovesWv.Count) _slotRemovesWv[idx].Visibility = Visibility.Visible;
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
                if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
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
                    if (idx < _slotRemoves.Count) _slotRemoves[idx].Visibility = Visibility.Collapsed;
                    if (idx < _slotRemovesWv.Count) _slotRemovesWv[idx].Visibility = Visibility.Collapsed;
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

                if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
                {
                    _secondaryImages[idx].ArchiveCurrentIfHasId();
                    _secondaryImages[idx].Bitmap = bitmap;
                    _secondaryImages[idx].FilePath = null;
                    _secondaryImages[idx].ImageId = null;
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
                            ChromiumWebBrowser? activeWv = null;
                            if (_activeTab == ActiveTab.WebBrowser && _activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count) activeWv = _webTabs[_activeTabIdx].WebView;
                            else if (_activeTab == ActiveTab.WebView) activeWv = _dynamicWebView;
                            if (activeWv != null && !string.IsNullOrWhiteSpace(activeWv.Address))
                            {
                                pageUrl = activeWv.Address;
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
            for (int i = 0; i < _secondarySlotCount && i < _secondaryImages.Count; i++)
                RefreshSlotUI(i);
            UpdateSecondaryInfo();
        }

        private void RefreshSlotUI(int idx)
        {
            if (idx < 0 || idx >= _secondarySlotCount || idx >= _secondaryImages.Count) return;
            if (idx >= _slotBorders.Count || idx >= _slotImages.Count) return;

            var item = _secondaryImages[idx];

            if (item.HasImage)
            {
                // Normal slots
                _slotImages[idx].Source = item.Bitmap;
                _slotPlaceholders[idx].Visibility = Visibility.Collapsed;
                if (idx < _slotChecks.Count) _slotChecks[idx].Visibility = item.IsSelected ? Visibility.Visible : Visibility.Collapsed;

                // Expanded slots
                if (idx < _slotImagesWv.Count) _slotImagesWv[idx].Source = item.Bitmap;
                if (idx < _slotPlaceholdersWv.Count) _slotPlaceholdersWv[idx].Visibility = Visibility.Collapsed;

                var activeBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4fffb0"));
                var normalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"));

                if (item.IsSelected)
                {
                    _slotBorders[idx].BorderBrush = activeBrush;
                    _slotBorders[idx].BorderThickness = new Thickness(2);
                    if (idx < _slotBordersWv.Count)
                    {
                        _slotBordersWv[idx].BorderBrush = activeBrush;
                        _slotBordersWv[idx].BorderThickness = new Thickness(2);
                    }
                }
                else
                {
                    _slotBorders[idx].BorderBrush = normalBrush;
                    _slotBorders[idx].BorderThickness = new Thickness(1.5);
                    if (idx < _slotBordersWv.Count)
                    {
                        _slotBordersWv[idx].BorderBrush = normalBrush;
                        _slotBordersWv[idx].BorderThickness = new Thickness(1.5);
                    }
                }
            }
            else
            {
                // Normal slots
                _slotImages[idx].Source = null;
                _slotPlaceholders[idx].Visibility = Visibility.Visible;
                if (idx < _slotChecks.Count) _slotChecks[idx].Visibility = Visibility.Collapsed;
                if (idx < _slotRemoves.Count) _slotRemoves[idx].Visibility = Visibility.Collapsed;

                // Expanded slots
                if (idx < _slotImagesWv.Count) _slotImagesWv[idx].Source = null;
                if (idx < _slotPlaceholdersWv.Count) _slotPlaceholdersWv[idx].Visibility = Visibility.Visible;
                if (idx < _slotRemovesWv.Count) _slotRemovesWv[idx].Visibility = Visibility.Collapsed;

                var normalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d"));
                _slotBorders[idx].BorderBrush = normalBrush;
                _slotBorders[idx].BorderThickness = new Thickness(1.5);
                if (idx < _slotBordersWv.Count)
                {
                    _slotBordersWv[idx].BorderBrush = normalBrush;
                    _slotBordersWv[idx].BorderThickness = new Thickness(1.5);
                }
            }

            RefreshChildThumbnailsUI(idx);
        }

        private void RefreshChildThumbnailsUI(int idx)
        {
            if (idx < 0 || idx >= _secondarySlotCount || idx >= _secondaryImages.Count) return;
            var item = _secondaryImages[idx];

            var panels = new List<StackPanel>();
            if (idx < _slotChildPanels.Count) panels.Add(_slotChildPanels[idx]);
            if (idx < _slotChildPanelsWv.Count) panels.Add(_slotChildPanelsWv[idx]);

            foreach (var panel in panels)
            {
                panel.Children.Clear();
                if (item.SavedChildImages != null && item.SavedChildImages.Count > 0)
                {
                    bool isCompact = (idx < _slotChildPanelsWv.Count && panel == _slotChildPanelsWv[idx]);
                    double size = isCompact ? 18 : 22;

                    foreach (var child in item.SavedChildImages)
                    {
                        var border = new Border
                        {
                            Width = size,
                            Height = size,
                            CornerRadius = new CornerRadius(3),
                            BorderThickness = new Thickness(1.2),
                            Margin = new Thickness(1, 0, 1, 0),
                            Cursor = Cursors.Hand,
                            ToolTip = !string.IsNullOrEmpty(child.ImageId) ? $"ID: #{child.ImageId}" : "Ảnh con"
                        };

                        bool isActive = !string.IsNullOrEmpty(child.ImageId) && string.Equals(child.ImageId, item.ImageId, StringComparison.OrdinalIgnoreCase);
                        border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isActive ? "#4fffb0" : "#2a2e3d"));
                        border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15171e"));

                        var childImg = new Image
                        {
                            Stretch = Stretch.Uniform,
                            Source = child.Bitmap
                        };
                        border.Child = childImg;

                        var capturedChild = child;
                        border.MouseLeftButtonDown += (s, e) =>
                        {
                            e.Handled = true;
                            // Lưu ảnh hiện tại (nếu có ID) vào SavedChildImages
                            item.ArchiveCurrentIfHasId();

                            // Chọn ảnh con này lên ô ảnh phụ to
                            item.Bitmap = capturedChild.Bitmap;
                            item.FilePath = capturedChild.FilePath;
                            item.ImageId = capturedChild.ImageId;
                            item.IsSelected = true;

                            RefreshAllSlotsUI();
                            UpdateSecondaryInfo();
                            UpdatePreviewImage();
                        };

                        panel.Children.Add(border);
                    }
                }
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
            if (_isSyncingUI) return;
            if (PanelCustomSize == null) return;
            if (_activeLayer != null)
            {
                _activeLayer.LayerAiAspectRatioIndex = CmbAspectRatio.SelectedIndex;
            }
            PanelCustomSize.Visibility = (CmbAspectRatio.SelectedIndex == 6) ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreviewImage();
        }

        private void TxtCustomSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingUI) return;
            UpdatePreviewImage();
        }

        private int GetSelectedSlotIndex()
        {
            if (_secondaryImages == null) return -1;
            for (int i = 0; i < _secondaryImages.Count; i++)
            {
                if (_secondaryImages[i] != null && _secondaryImages[i].HasImage && _secondaryImages[i].IsSelected)
                {
                    return i;
                }
            }
            return -1;
        }

        private BitmapSource MaskAndCropSecondaryImage(BitmapSource secondary, BitmapSource croppedOriginal)
        {
            try
            {
                int srcW = croppedOriginal.PixelWidth;
                int srcH = croppedOriginal.PixelHeight;

                // Compute high-resolution dimensions matching the croppedOriginal aspect ratio
                double scale = Math.Max(1.0, Math.Max((double)secondary.PixelWidth / srcW, (double)secondary.PixelHeight / srcH));
                int hiW = (int)Math.Round(srcW * scale);
                int hiH = (int)Math.Round(srcH * scale);

                // 1. Resize secondary image to match high-resolution dimensions (crop/fill aspect ratio) using SkiaSharp-backed Resize
                BitmapSource resizedSecondary = ResizeBitmapHighQuality(secondary, hiW, hiH, uniformToFill: true);

                // 2. Resize original cropped image (which contains the mask) using SkiaSharp-backed Resize
                BitmapSource resizedOriginal = ResizeBitmapHighQuality(croppedOriginal, hiW, hiH, uniformToFill: false);

                // 3. Perform native masking via SkiaSharp DstIn blend mode (extremely fast!)
                var maskedBmp = new WriteableBitmap(hiW, hiH, 96, 96, PixelFormats.Bgra32, null);
                maskedBmp.Lock();
                try
                {
                    var info = new SkiaSharp.SKImageInfo(hiW, hiH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                    using (var surface = SkiaSharp.SKSurface.Create(info, maskedBmp.BackBuffer, maskedBmp.BackBufferStride))
                    {
                        var canvas = surface.Canvas;
                        canvas.Clear(SkiaSharp.SKColors.Transparent);

                        // Draw secondary image
                        var secStride = resizedSecondary.PixelWidth * 4;
                        var secPixels = new byte[secStride * resizedSecondary.PixelHeight];
                        resizedSecondary.CopyPixels(secPixels, secStride, 0);

                        using (var secSkBmp = new SkiaSharp.SKBitmap())
                        {
                            var handleSec = System.Runtime.InteropServices.GCHandle.Alloc(secPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                            try
                            {
                                secSkBmp.InstallPixels(info, handleSec.AddrOfPinnedObject(), secStride);
                                canvas.DrawBitmap(secSkBmp, 0, 0);
                            }
                            finally
                            {
                                handleSec.Free();
                            }
                        }

                        // Apply original alpha mask via DstIn
                        var origStride = resizedOriginal.PixelWidth * 4;
                        var origPixels = new byte[origStride * resizedOriginal.PixelHeight];
                        resizedOriginal.CopyPixels(origPixels, origStride, 0);

                        using (var origSkBmp = new SkiaSharp.SKBitmap())
                        {
                            var handleOrig = System.Runtime.InteropServices.GCHandle.Alloc(origPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                            try
                            {
                                origSkBmp.InstallPixels(info, handleOrig.AddrOfPinnedObject(), origStride);
                                using (var paint = new SkiaSharp.SKPaint())
                                {
                                    paint.BlendMode = SkiaSharp.SKBlendMode.DstIn;
                                    canvas.DrawBitmap(origSkBmp, 0, 0, paint);
                                }
                            }
                            finally
                            {
                                handleOrig.Free();
                            }
                        }
                    }
                    maskedBmp.AddDirtyRect(new Int32Rect(0, 0, hiW, hiH));
                }
                finally
                {
                    maskedBmp.Unlock();
                }

                maskedBmp.Freeze();
                return maskedBmp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to mask secondary image: {ex.Message}");
                return secondary;
            }
        }

        private void UpdatePreviewImage()
        {
            if (ImgPreview == null || _activeLayer == null) return;

            try
            {
                // 1. Get the cropped original image first (as the base and alpha template)
                BitmapSource baseImg = _activeLayer.OriginalTransformBitmap ?? _activeLayer.Bitmap;
                var bounds = GetLayerContentBounds(baseImg);
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    int x = Math.Clamp((int)bounds.X, 0, baseImg.PixelWidth - 1);
                    int y = Math.Clamp((int)bounds.Y, 0, baseImg.PixelHeight - 1);
                    int w = Math.Clamp((int)Math.Ceiling(bounds.Width), 1, baseImg.PixelWidth - x);
                    int h = Math.Clamp((int)Math.Ceiling(bounds.Height), 1, baseImg.PixelHeight - y);
                    if (w > 0 && h > 0 && (x > 0 || y > 0 || w < baseImg.PixelWidth || h < baseImg.PixelHeight))
                    {
                        baseImg = new CroppedBitmap(baseImg, new Int32Rect(x, y, w, h));
                    }
                }

                BitmapSource sourceImg;
                
                // 2. In OFF mode, check if a slot is selected. If so, override preview source with masked secondary image.
                int selectedSlotIdx = GetSelectedSlotIndex();
                if (!_sendModeOn && selectedSlotIdx >= 0 && _secondaryImages[selectedSlotIdx].HasImage)
                {
                    sourceImg = MaskAndCropSecondaryImage(_secondaryImages[selectedSlotIdx].Bitmap, baseImg);
                }
                else
                {
                    sourceImg = baseImg;
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
            SetButtonsLoading(true);

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
                var aspectPort = _node.DynamicOutputs?.FirstOrDefault(o => string.Equals(o.Key, "aspectRatio", StringComparison.OrdinalIgnoreCase));
                if (aspectPort != null)
                {
                    aspectPort.UserValueOverride = aspectStr;
                }

                var execSvc = _host?.ViewModel?.WorkflowExecutionService;
                string? existingMainId = _activeLayer.GetImageId(selectedIndex);
                var selectedSecItems = _secondaryImages.Where(s => s.HasImage && s.IsSelected).ToList();
                var existingSecIds = selectedSecItems.Select(s => s.GetImageId(selectedIndex)).Where(id => !string.IsNullOrEmpty(id)).ToList();

                // mainCodeId = Layer CodeId (identity của layer trên canvas, để downstream biết ảnh thuộc layer nào)
                string mainCodeId = string.IsNullOrWhiteSpace(_activeLayer.CodeId) ? (_activeLayer.CodeId = Guid.NewGuid().ToString("N")) : _activeLayer.CodeId;

                // mainCropCodeId = GUID mới cho ảnh crop mỗi lần gửi (tránh trùng với layer CodeId)
                // Dùng trong cropListObjects JSON và CropGuidRegistry để map kết quả render về đúng crop
                string mainCropCodeId = Guid.NewGuid().ToString("N");

                CropGuidRegistry[mainCropCodeId] = new CodeCropMappingInfo
                {
                    CodeId = mainCropCodeId,
                    TargetLayer = _activeLayer,
                    AspectRatioIndex = selectedIndex,
                    ExecutionId = execId
                };

                // Output port: mainCodeId = layer CodeId (để biết ảnh render thuộc layer nào)
                GetOrAddDynamicOutputPort("mainCodeId", "Layer AI - Main Code ID").UserValueOverride = mainCodeId;

                if (execSvc != null)
                {
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "mainCodeId", mainCodeId);
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "prompt", activePromptText);
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "promptSize", batchSize.ToString());
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "cropWidth", processedImg.PixelWidth.ToString());
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "cropHeight", processedImg.PixelHeight.ToString());
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "aspectRatio", aspectStr);
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "isCombinedImage", _isCombinedMode.ToString().ToLowerInvariant());
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "executionId", execId);
                }

                // *** Collect main and secondary images into cropListObjects array (index [0] = main image) ***
                // Dùng mainCropCodeId (GUID riêng cho crop) thay vì mainCodeId (layer CodeId)
                await CollectAndSetListBase64Async(b64, mainCropCodeId, existingMainId, execId, selectedIndex);

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

                // Register execution scope for thread-safe multi-execution mapping
                var scope = new LayerAiExecutionScope
                {
                    ExecutionId = execId,
                    MainLayer = _activeLayer,
                    AspectRatioIndex = selectedIndex,
                    SecondaryImages = selectedSecItems,
                    Placeholders = placeholders
                };
                ActiveExecutionScopes[execId] = scope;

                // Notify HasChildren changed on parent so collapse toggle appears
                destinationParent.OnPropertyChanged(nameof(EditorLayer.HasChildren));

                // Refresh main panel immediately to render loading placeholders in the layers ListBox
                var editorPanel = FindVisualChild<ImageEditorPanel>(this.Owner);
                editorPanel?.RefreshLayersList();

                // Close dialog immediately — workflow runs in background, results applied to placeholders
                try { DialogResult = true; } catch { }
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
                    var filledPlaceholders = new HashSet<EditorLayer>();
                    Action<string, string, string, string?> realtimeHandler = (runId, targetNodeId, targetKey, valStr) =>
                    {
                        if (string.IsNullOrWhiteSpace(valStr) || valStr == "—") return;
                        
                        // Check if payload contains codeId and return ID object/array
                        ProcessCodeIdResult(valStr, nodeRef.ReturnCodeIdKeys, nodeRef.ReturnImageIdKeys, nodeRef.ReturnImageLinkKeys);
                        if (!string.IsNullOrWhiteSpace(nodeRef.RenderNodeId))
                        {
                            ProcessCodeIdResult(valStr, nodeRef.RenderCodeIdKeys, nodeRef.RenderImageIdKeys, nodeRef.RenderImageLinkKeys);
                        }

                        bool isIdOutput = string.Equals(targetKey, "mainImageId", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(targetKey, "imageId", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(targetKey, "mediaId", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(targetKey, "uploadedId", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(targetKey, "listImageIds", StringComparison.OrdinalIgnoreCase);

                        bool isRenderOutput = string.Equals(targetNodeId, nodeRef.RenderNodeId, StringComparison.OrdinalIgnoreCase) &&
                                              string.Equals(targetKey, nodeRef.RenderNodeOutputKey, StringComparison.OrdinalIgnoreCase);

                        if (!isIdOutput && !isRenderOutput) return;

                        string actualRunId = execId;
                        if (WorkflowExecutionService.ExecutionIdMapping.TryGetValue(execId, out var mappedRunId))
                        {
                            actualRunId = mappedRunId;
                        }

                        bool isMatch = string.Equals(runId, execId, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(runId, actualRunId, StringComparison.OrdinalIgnoreCase) ||
                                       runId.StartsWith(execId + ":", StringComparison.OrdinalIgnoreCase) ||
                                       runId.StartsWith(actualRunId + ":", StringComparison.OrdinalIgnoreCase);

                        if (!isMatch) return;

                        if (isIdOutput)
                        {
                            Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                if (ActiveExecutionScopes.TryGetValue(execId, out var execScope) && execScope != null)
                                {
                                    if (string.Equals(targetKey, "listImageIds", StringComparison.OrdinalIgnoreCase))
                                    {
                                        try
                                        {
                                            var idList = System.Text.Json.JsonSerializer.Deserialize<List<string>>(valStr);
                                            if (idList != null)
                                            {
                                                for (int i = 0; i < idList.Count && i < execScope.SecondaryImages.Count; i++)
                                                {
                                                    execScope.SecondaryImages[i].SetImageId(execScope.AspectRatioIndex, idList[i]);
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                    else
                                    {
                                        execScope.MainLayer.SetImageId(execScope.AspectRatioIndex, valStr);
                                    }
                                }
                            });
                            return;
                        }

                        var links = ParseImageLinksFromOutput(valStr);
                        if (links.Count == 0) return;

                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            foreach (var entry in links)
                            {
                                if (string.IsNullOrWhiteSpace(entry)) continue;

                                EditorLayer? placeholder = null;
                                lock (placeholders)
                                {
                                    placeholder = placeholders.FirstOrDefault(p => !filledPlaceholders.Contains(p));
                                    if (placeholder != null)
                                    {
                                        filledPlaceholders.Add(placeholder);
                                    }
                                }

                                if (placeholder != null)
                                {
                                    BitmapImage? bmp = CreateBitmapFromUrlOrFile(entry.Trim()) ?? CreateBitmapFromBase64(entry.Trim());
                                    if (bmp != null)
                                    {
                                        placeholder.IsLoading = false;
                                        placeholder.StopLoadingTimer();
                                        ProcessAndApplyAiImage(placeholder, bmp, activeLayerRef, bounds, targetRatio, customW, customH);

                                        // Extract returned ID and assign to placeholder & main layer for this aspect ratio
                                        string returnedId = ExtractOrGenerateImageId(entry);
                                        placeholder.SetImageId(selectedIndex, returnedId);
                                        if (activeLayerRef != null)
                                        {
                                            activeLayerRef.SetImageId(selectedIndex, returnedId);
                                        }

                                        destinationParent.ActiveChildLayer = placeholder;
                                        docRef.ActiveLayer = placeholder;

                                        foreach (var child in destinationParent.ChildLayers)
                                        {
                                            child.IsActive = (child == placeholder);
                                            child.IsSelected = (child == placeholder);
                                        }

                                        var panel = FindVisualChild<ImageEditorPanel>(ownerRef);
                                        panel?.RefreshLayersList();
                                        panel?.OnDocumentModified();
                                    }
                                }
                            }
                        });
                    };

                    try
                    {
                        WorkflowExecutionService.OnScopedOutputSetGlobal += realtimeHandler;

                        // Run workflow on background thread via reflection
                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            var vm = hostRef.ViewModel;
                            if (vm != null)
                            {
                                var vmType = vm.GetType();
                                var startTestMethod = vmType.GetMethod("StartTest", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (startTestMethod != null)
                                {
                                    var parameters = startTestMethod.GetParameters();
                                    object?[] args = parameters.Length > 0 ? new object?[] { execId } : null;
                                    if (startTestMethod.Invoke(vm, args) is Task t)
                                    {
                                        await t;
                                    }
                                }
                            }
                        }).Task.Unwrap();

                        // Process results on UI thread (fallback for any remaining unfilled placeholders)
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                WorkflowExecutionService.OnScopedOutputSetGlobal -= realtimeHandler;

                                // Refresh outputs list again to show final outputs/execution IDs
                                RefreshRelatedNodeDialogs();

                                // Resolve AI outputs
                                if (string.IsNullOrWhiteSpace(nodeRef.RenderNodeId) || string.IsNullOrWhiteSpace(nodeRef.RenderNodeOutputKey))
                                {
                                    CleanupPlaceholders(placeholders, destinationParent, ownerRef);
                                    return;
                                }

                                string actualRunId = execId;
                                if (WorkflowExecutionService.ExecutionIdMapping.TryGetValue(execId, out var mappedRunId))
                                {
                                    actualRunId = mappedRunId;
                                }

                                var allLinks = ResolveAllFromHistoricalCache(nodeRef.RenderNodeId, nodeRef.RenderNodeOutputKey, actualRunId);
                                if (allLinks.Count == 0)
                                {
                                    var rawFallback = ResolveFromNodeIfAny(hostRef, nodeRef.RenderNodeId, nodeRef.RenderNodeOutputKey);
                                    allLinks = ParseImageLinksFromOutput(rawFallback);
                                }

                                // Fill any remaining unfilled placeholders from fallback
                                foreach (var entry in allLinks)
                                {
                                    if (string.IsNullOrWhiteSpace(entry)) continue;

                                    EditorLayer? placeholder = null;
                                    lock (placeholders)
                                    {
                                        placeholder = placeholders.FirstOrDefault(p => !filledPlaceholders.Contains(p));
                                        if (placeholder != null)
                                        {
                                            filledPlaceholders.Add(placeholder);
                                        }
                                    }

                                    if (placeholder != null)
                                    {
                                        BitmapImage? bmp = CreateBitmapFromUrlOrFile(entry.Trim()) ?? CreateBitmapFromBase64(entry.Trim());
                                        if (bmp != null)
                                        {
                                            placeholder.IsLoading = false;
                                            placeholder.StopLoadingTimer();
                                            ProcessAndApplyAiImage(placeholder, bmp, activeLayerRef, bounds, targetRatio, customW, customH);
                                        }
                                    }
                                }

                                // Dọn dẹp các placeholders chưa được dùng
                                var unfilledList = placeholders.Where(p => !filledPlaceholders.Contains(p)).ToList();
                                foreach (var p in unfilledList)
                                {
                                    p.StopLoadingTimer();
                                    destinationParent.ChildLayers.Remove(p);
                                }

                                if (destinationParent.ChildLayers.Count > 0)
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
                        WorkflowExecutionService.OnScopedOutputSetGlobal -= realtimeHandler;
                        System.Diagnostics.Debug.WriteLine("AI execution error: " + ex.Message);
                        await Application.Current.Dispatcher.InvokeAsync(() =>
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
        /// Collect main image (index [0]) and selected secondary images (index [1..n]), convert to base64 if no ID present, and set cropListObjects output.
        /// </summary>
        private async Task CollectAndSetListBase64Async(string mainB64, string mainCodeId, string? existingMainId, string? execId = null, int aspectRatioIndex = 3)
        {
            var selectedSecItems = _secondaryImages
                .Where(s => s.HasImage && s.IsSelected && s.Bitmap != null)
                .ToList();

            var cropListObjects = new List<object>();

            // 1. Main image at index [0]
            bool mainHasId = !string.IsNullOrWhiteSpace(existingMainId);
            cropListObjects.Add(new
            {
                codeId = mainCodeId,
                base64 = mainHasId ? "" : mainB64,
                id = mainHasId ? existingMainId : (string?)null
            });

            // 2. Secondary images at index [1..n]
            foreach (var secItem in selectedSecItems)
            {
                string secCodeId = string.IsNullOrWhiteSpace(secItem.CodeId) ? (secItem.CodeId = Guid.NewGuid().ToString("N")) : secItem.CodeId;
                string? existingId = secItem.GetImageId(aspectRatioIndex);

                if (!string.IsNullOrEmpty(execId))
                {
                    CropGuidRegistry[secCodeId] = new CodeCropMappingInfo
                    {
                        CodeId = secCodeId,
                        TargetLayer = _activeLayer,
                        SecondaryImage = secItem,
                        AspectRatioIndex = aspectRatioIndex,
                        ExecutionId = execId
                    };
                }

                bool secHasId = !string.IsNullOrWhiteSpace(existingId);
                string b64 = secHasId ? "" : await Task.Run(() => ImageProcessorHelper.ToBase64(secItem.Bitmap!));

                cropListObjects.Add(new
                {
                    codeId = secCodeId,
                    base64 = b64,
                    id = secHasId ? existingId : (string?)null
                });
            }

            string cropListObjectsJson = System.Text.Json.JsonSerializer.Serialize(cropListObjects);

            GetOrAddDynamicOutputPort("cropListObjects", "Layer AI - Crops List Objects (JSON)", FlowMy.Models.WorkflowDataType.ArrayDynamic, isMultiple: true).UserValueOverride = cropListObjectsJson;

            if (!string.IsNullOrEmpty(execId))
            {
                var execSvc = _host?.ViewModel?.WorkflowExecutionService;
                if (execSvc != null)
                {
                    execSvc.SetScopedNodeStringOutput(execId, _node.Id, "cropListObjects", cropListObjectsJson);
                }
            }
        }

        private FlowMy.Models.WorkflowDynamicDataPort GetOrAddDynamicOutputPort(string key, string displayName, FlowMy.Models.WorkflowDataType dataType = FlowMy.Models.WorkflowDataType.String, bool isMultiple = false)
        {
            if (_node.DynamicOutputs == null)
            {
                _node.DynamicOutputs = new System.Collections.Generic.List<FlowMy.Models.WorkflowDynamicDataPort>();
            }
            var port = _node.DynamicOutputs.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
            if (port == null)
            {
                port = new FlowMy.Models.WorkflowDynamicDataPort
                {
                    Key = key,
                    DisplayName = displayName,
                    OutputType = dataType,
                    IsMultiple = isMultiple
                };
                _node.DynamicOutputs.Add(port);
            }
            else
            {
                port.OutputType = dataType;
                port.IsMultiple = isMultiple;
            }
            return port;
        }

        private static string ExtractOrGenerateImageId(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry)) return Guid.NewGuid().ToString("N");
            var trimmed = entry.Trim();
            if (trimmed.Length > 200 || trimmed.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(trimmed.Substring(0, Math.Min(trimmed.Length, 1000))));
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                if (!string.IsNullOrEmpty(query["id"])) return query["id"]!;
                if (!string.IsNullOrEmpty(query["media_id"])) return query["media_id"]!;
                var fileName = System.IO.Path.GetFileNameWithoutExtension(uri.LocalPath);
                if (!string.IsNullOrWhiteSpace(fileName)) return fileName;
            }
            return trimmed;
        }

        private static HashSet<string> ParseKeySet(string? input, string defaultKeys)
        {
            var raw = string.IsNullOrWhiteSpace(input) ? defaultKeys : input;
            var keys = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        }

        public static bool ProcessCodeIdResult(string valStr, string? codeIdKeys = null, string? imageIdKeys = null, string? imageLinkKeys = null)
        {
            if (string.IsNullOrWhiteSpace(valStr)) return false;

            var codeIdSet = ParseKeySet(codeIdKeys, "codeId, CodeId, code_id");
            var imageIdSet = ParseKeySet(imageIdKeys, "id, Id, ID, mediaId, imageId, assetId");
            var imageLinkSet = ParseKeySet(imageLinkKeys, "linkImage, linkImg, link_image, imageUrl, url, src, link, path");

            try
            {
                using (var doc = System.Text.Json.JsonDocument.Parse(valStr))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        return TryApplyCodeIdObject(root, codeIdSet, imageIdSet, imageLinkSet);
                    }
                    else if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        bool anyApplied = false;
                        foreach (var element in root.EnumerateArray())
                        {
                            if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
                            {
                                if (TryApplyCodeIdObject(element, codeIdSet, imageIdSet, imageLinkSet)) anyApplied = true;
                            }
                        }
                        return anyApplied;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool TryApplyCodeIdObject(System.Text.Json.JsonElement element, HashSet<string> codeIdSet, HashSet<string> imageIdSet, HashSet<string> imageLinkSet)
        {
            if (element.ValueKind != System.Text.Json.JsonValueKind.Object) return false;

            string? codeId = null;
            string? returnedId = null;
            string? returnedLink = null;

            foreach (var prop in element.EnumerateObject())
            {
                var name = prop.Name;
                var val = prop.Value.ValueKind == System.Text.Json.JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
                if (string.IsNullOrWhiteSpace(val)) continue;

                if (codeIdSet.Contains(name))
                {
                    codeId = val;
                }
                else if (imageIdSet.Contains(name))
                {
                    returnedId = val;
                }
                else if (imageLinkSet.Contains(name))
                {
                    returnedLink = val;
                }
            }

            if (!string.IsNullOrWhiteSpace(codeId))
            {
                if (CropGuidRegistry.TryGetValue(codeId, out var info) && info != null)
                {
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(returnedId))
                        {
                            if (info.SecondaryImage != null)
                            {
                                info.SecondaryImage.SetImageId(info.AspectRatioIndex, returnedId);
                            }
                            else if (info.TargetLayer != null)
                            {
                                info.TargetLayer.SetImageId(info.AspectRatioIndex, returnedId);
                            }
                        }
                    });
                    return true;
                }
            }
            return false;
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

        private FlowMy.ViewModels.WorkflowEditorViewModel? _subscribedVm;

        private void SubscribeToViewModelEvents()
        {
            UnsubscribeFromViewModelEvents();
            if (_host?.ViewModel is FlowMy.ViewModels.WorkflowEditorViewModel vm)
            {
                _subscribedVm = vm;
                vm.PropertyChanged += HostViewModel_PropertyChanged;
                CheckWorkflowExecutionState();
            }
        }

        private void UnsubscribeFromViewModelEvents()
        {
            if (_subscribedVm != null)
            {
                _subscribedVm.PropertyChanged -= HostViewModel_PropertyChanged;
                _subscribedVm = null;
            }
        }

        private void HostViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FlowMy.ViewModels.WorkflowEditorViewModel.IsExecuting) ||
                e.PropertyName == nameof(FlowMy.ViewModels.WorkflowEditorViewModel.HasRunningNodes))
            {
                CheckWorkflowExecutionState();
            }
        }

        private void CheckWorkflowExecutionState()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_subscribedVm != null && !_subscribedVm.IsExecuting && !_subscribedVm.HasRunningNodes)
                {
                    SetButtonsLoading(false);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ResetButtons()
        {
            SetButtonsLoading(false);
        }

        private void SetButtonsLoading(bool isLoading)
        {
            _isAiLoading = false;
            BtnCancel.IsEnabled = true;
            if (BtnApply != null) BtnApply.IsEnabled = true;
            UpdateSendButtonsState();

            BtnSend.Content = CreatePlayIconPath();
            if (BtnSendWv != null) BtnSendWv.Content = CreatePlayIconPath();
            if (BtnSendWeb != null) BtnSendWeb.Content = CreatePlayIconPath();
        }

        private System.Windows.Shapes.Path CreatePlayIconPath()
        {
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 3 2 L 13 8 L 3 14 Z"),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111318")),
                Width = 10,
                Height = 10,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(2, 0, 0, 0)
            };
            return path;
        }

        private void TxtPrompt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingUI) return;
            _isSyncingUI = true;
            try
            {
                if (sender is TextBox textBox)
                {
                    string text = textBox.Text;
                    if (TxtPrompt != null && TxtPrompt.Text != text) TxtPrompt.Text = text;
                    if (TxtPromptWv != null && TxtPromptWv.Text != text) TxtPromptWv.Text = text;
                    if (TxtPromptWeb != null && TxtPromptWeb.Text != text) TxtPromptWeb.Text = text;
                }
                UpdateSendButtonsState();
            }
            finally
            {
                _isSyncingUI = false;
            }
        }

        private void UpdateSendButtonsState()
        {
            BtnSend.IsEnabled = true;
            if (BtnSendWv != null) BtnSendWv.IsEnabled = true;
            if (BtnSendWeb != null) BtnSendWeb.IsEnabled = true;
        }

        private void ApplySendModeUi()
        {
            var onStyle = FindResource("SuccessButton") as Style;
            var offStyle = FindResource("DangerButton") as Style;
            string text = _sendModeOn ? "Gửi AI: ON" : "Gửi AI: OFF";
            var style = _sendModeOn ? onStyle : offStyle;

            if (BtnToggleSendMode != null)
            {
                BtnToggleSendMode.Content = text;
                BtnToggleSendMode.Style = style;
            }
            if (BtnToggleSendModeExpanded != null)
            {
                BtnToggleSendModeExpanded.Content = text;
                BtnToggleSendModeExpanded.Style = style;
            }

            var sendVisibility = _sendModeOn ? Visibility.Visible : Visibility.Collapsed;
            var applyVisibility = _sendModeOn ? Visibility.Collapsed : Visibility.Visible;
            var cancelMargin = _sendModeOn ? new Thickness(0) : new Thickness(0, 0, 8, 0);

            if (BtnSend != null) BtnSend.Visibility = sendVisibility;
            if (BtnSendWv != null) BtnSendWv.Visibility = sendVisibility;
            if (BtnSendWeb != null) BtnSendWeb.Visibility = sendVisibility;

            if (BtnApply != null) BtnApply.Visibility = applyVisibility;
            if (BtnCancel != null) BtnCancel.Margin = cancelMargin;

            // Toggle batch size vs slot count selector in bottom bar
            if (PanelBatchSize != null) PanelBatchSize.Visibility = _sendModeOn ? Visibility.Visible : Visibility.Collapsed;
            if (PanelSlotCount != null) PanelSlotCount.Visibility = _sendModeOn ? Visibility.Collapsed : Visibility.Visible;

            if (!_sendModeOn)
            {
                // Enforce single select for slots in OFF mode: deselect all but the first selected slot
                int firstSelectedIdx = -1;
                for (int i = 0; i < _secondaryImages.Count; i++)
                {
                    if (_secondaryImages[i] != null && _secondaryImages[i].HasImage && _secondaryImages[i].IsSelected)
                    {
                        if (firstSelectedIdx == -1)
                        {
                            firstSelectedIdx = i;
                        }
                        else
                        {
                            _secondaryImages[i].IsSelected = false;
                        }
                    }
                }
                RefreshAllSlotsUI();
                UpdateSecondaryInfo();
            }

            UpdateSendButtonsState();
            UpdatePreviewImage();
        }

        private void BtnToggleSendMode_Click(object sender, RoutedEventArgs e)
        {
            _sendModeOn = !_sendModeOn;
            if (_node != null)
            {
                _node.LayerAiSendModeOn = _sendModeOn;
            }
            ApplySendModeUi();
        }

        private bool _promptHidden = false;

        private void ApplyPromptHiddenUi()
        {
            if (BtnTogglePrompt != null)
            {
                BtnTogglePrompt.Content = _promptHidden ? "Hiện Prompt" : "Ẩn Prompt";
            }

            // Update WebView Prompt Layout
            if (RowWvBrowser != null && RowWvGap != null && RowWvPrompt != null && GridPromptWvContainer != null)
            {
                if (_promptHidden)
                {
                    RowWvBrowser.Height = new GridLength(1, GridUnitType.Star);
                    RowWvGap.Height = new GridLength(0);
                    RowWvPrompt.Height = new GridLength(0);
                    GridPromptWvContainer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    RowWvBrowser.Height = new GridLength(4, GridUnitType.Star);
                    RowWvGap.Height = new GridLength(8);
                    RowWvPrompt.Height = new GridLength(1, GridUnitType.Star);
                    GridPromptWvContainer.Visibility = Visibility.Visible;
                }
            }

            // Update WebBrowser Prompt Layout
            if (RowWebBrowser != null && RowWebGap != null && RowWebPrompt != null && GridPromptWebContainer != null)
            {
                if (_promptHidden)
                {
                    RowWebBrowser.Height = new GridLength(1, GridUnitType.Star);
                    RowWebGap.Height = new GridLength(0);
                    RowWebPrompt.Height = new GridLength(0);
                    GridPromptWebContainer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    RowWebBrowser.Height = new GridLength(4, GridUnitType.Star);
                    RowWebGap.Height = new GridLength(8);
                    RowWebPrompt.Height = new GridLength(1, GridUnitType.Star);
                    GridPromptWebContainer.Visibility = Visibility.Visible;
                }
            }
        }

        private void BtnTogglePrompt_Click(object sender, RoutedEventArgs e)
        {
            _promptHidden = !_promptHidden;
            if (_node != null)
            {
                _node.LayerAiPromptHidden = _promptHidden;
            }
            ApplyPromptHiddenUi();
        }


        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            // First, make sure the current active layer's state is saved from UI
            SaveActiveLayerState();

            bool atLeastOneApplied = false;

            foreach (var layer in _selectedLayers)
            {
                if (!_layerStates.TryGetValue(layer, out var state)) continue;

                // Check if this layer has any secondary images in its state
                int countSlotsWithImages = state.SecondaryImages.Count(s => s.HasImage);
                if (countSlotsWithImages == 0) continue;

                var destinationParent = layer.ParentLayer ?? layer;
                BitmapSource sourceImg = layer.OriginalTransformBitmap ?? layer.Bitmap;
                var bounds = GetLayerContentBounds(sourceImg);
                if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                {
                    bounds = new Rect(0, 0, sourceImg.PixelWidth, sourceImg.PixelHeight);
                }

                double? targetRatio = null;
                int? customW = null;
                int? customH = null;

                int selectedIndex = state.AspectRatioIndex;
                if (selectedIndex == 6)
                {
                    customW = int.TryParse(state.CustomWidth, out var w) ? w : 512;
                    customH = int.TryParse(state.CustomHeight, out var h) ? h : 512;
                }
                else if (selectedIndex > 0)
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
                }

                EditorLayer? activeChild = null;

                for (int i = 0; i < state.SecondaryImages.Count; i++)
                {
                    var slot = state.SecondaryImages[i];
                    if (slot.HasImage)
                    {
                        var childLayer = new EditorLayer(destinationParent.Width, destinationParent.Height, $"Layer AI {destinationParent.ChildLayers.Count + 1}");
                        childLayer.ParentLayer = destinationParent;
                        destinationParent.ChildLayers.Add(childLayer);

                        ProcessAndApplyAiImage(childLayer, slot.Bitmap, layer, bounds, targetRatio, customW, customH);

                        if (slot.IsSelected)
                        {
                            activeChild = childLayer;
                        }
                    }
                }

                if (destinationParent.ChildLayers.Count > 0)
                {
                    destinationParent.ActiveChildLayer = activeChild ?? destinationParent.ChildLayers.Last();
                    
                    // Set radio indicators
                    foreach (var child in destinationParent.ChildLayers)
                    {
                        child.IsActive = (child == destinationParent.ActiveChildLayer);
                        child.IsSelected = (child == destinationParent.ActiveChildLayer);
                    }
                    destinationParent.IsActive = false;
                    destinationParent.IsSelected = false;
                    
                    // Update active document focus to the new active child
                    if (layer == _activeLayer)
                    {
                        _doc.ActiveLayer = destinationParent.ActiveChildLayer;
                    }
                    
                    destinationParent.OnPropertyChanged(nameof(EditorLayer.HasChildren));
                    atLeastOneApplied = true;
                }
            }

            if (atLeastOneApplied)
            {
                // Refresh panel
                var ownerRef = this.Owner;
                if (ownerRef != null)
                {
                    var panel = FindVisualChild<ImageEditorPanel>(ownerRef);
                    panel?.RefreshLayersList();
                    panel?.OnDocumentModified();
                }
            }

            try { DialogResult = true; } catch { }
            Close();
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
            return ImageProcessingNodeControl.CreateBitmapFromUrlOrFile(value);
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

        private static List<string> ParseImageLinksFromOutput(string? raw)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(raw) || raw == "—") return list;

            raw = raw.Trim();

            var linkSet = ParseKeySet(null, "linkImage, linkImg, link_image, imageUrl, url, src, link, path, base64, b64, data");

            try
            {
                using (var doc = System.Text.Json.JsonDocument.Parse(raw))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        var link = ExtractImageLinkFromJsonObj(root, linkSet);
                        if (!string.IsNullOrWhiteSpace(link)) list.Add(link);
                        return list;
                    }
                    else if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var elem in root.EnumerateArray())
                        {
                            if (elem.ValueKind == System.Text.Json.JsonValueKind.Object)
                            {
                                var link = ExtractImageLinkFromJsonObj(elem, linkSet);
                                if (!string.IsNullOrWhiteSpace(link)) list.Add(link);
                            }
                            else if (elem.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var str = elem.GetString();
                                if (!string.IsNullOrWhiteSpace(str)) list.Add(str.Trim());
                            }
                        }
                        if (list.Count > 0) return list;
                    }
                }
            }
            catch { }

            if (raw.StartsWith("["))
            {
                try
                {
                    var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw);
                    if (deserialized != null)
                    {
                        foreach (var item in deserialized)
                        {
                            if (!string.IsNullOrWhiteSpace(item))
                                list.Add(item.Trim());
                        }
                    }
                }
                catch
                {
                    var inner = raw.Trim();
                    if (inner.StartsWith("[")) inner = inner.Substring(1);
                    if (inner.EndsWith("]")) inner = inner.Substring(0, inner.Length - 1);
                    var parts = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(p => p.Trim().Trim('"'))
                                     .Where(p => !string.IsNullOrWhiteSpace(p));
                    foreach (var p in parts)
                    {
                        list.Add(p);
                    }
                }
            }
            else
            {
                list.Add(raw);
            }
            return list;
        }

        private static string? ExtractImageLinkFromJsonObj(System.Text.Json.JsonElement obj, HashSet<string> linkSet)
        {
            if (obj.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            foreach (var prop in obj.EnumerateObject())
            {
                if (linkSet.Contains(prop.Name))
                {
                    var val = prop.Value.ValueKind == System.Text.Json.JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
                    if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
                }
            }
            return null;
        }

        private static string? ResolveFromHistoricalCache(string nodeId, string key, string executionId)
        {
            var list = ResolveAllFromHistoricalCache(nodeId, key, executionId);
            return list.Count > 0 ? list[0] : null;
        }

        private static List<string> ResolveAllFromHistoricalCache(string nodeId, string key, string executionId)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(executionId)) return result;

            string actualRunId = executionId;
            if (WorkflowExecutionService.ExecutionIdMapping.TryGetValue(executionId, out var mapped))
                actualRunId = mapped;

            void AddValue(string? val)
            {
                var links = ParseImageLinksFromOutput(val);
                foreach (var link in links)
                {
                    if (!string.IsNullOrWhiteSpace(link) && !result.Contains(link, StringComparer.OrdinalIgnoreCase))
                        result.Add(link);
                }
            }

            // 1. Root runId & actualRunId
            if (WorkflowExecutionService.ScopedOutputsHistoricalCache.TryGetValue(executionId, out var byNode1) &&
                byNode1.TryGetValue(nodeId, out var byKey1) &&
                byKey1.TryGetValue(key, out var val1))
            {
                AddValue(val1);
            }

            if (!string.Equals(actualRunId, executionId, StringComparison.OrdinalIgnoreCase) &&
                WorkflowExecutionService.ScopedOutputsHistoricalCache.TryGetValue(actualRunId, out var byNode2) &&
                byNode2.TryGetValue(nodeId, out var byKey2) &&
                byKey2.TryGetValue(key, out var val2))
            {
                AddValue(val2);
            }

            // 2. Child runs (executionId:* hoặc actualRunId:*)
            var prefix1 = executionId + ":";
            var prefix2 = actualRunId + ":";
            foreach (var kv in WorkflowExecutionService.ScopedOutputsHistoricalCache)
            {
                if (kv.Key.StartsWith(prefix1, StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.Value.TryGetValue(nodeId, out var childKey) &&
                        childKey.TryGetValue(key, out var childVal))
                    {
                        AddValue(childVal);
                    }
                }
            }

            return result;
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
                    _ => 3
                };
            }
            else
            {
                CmbAspectRatio.SelectedIndex = 3;
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

            // Restore prompt hidden state
            _promptHidden = _node.LayerAiPromptHidden;
            ApplyPromptHiddenUi();

            // Restore AI Send Mode
            _sendModeOn = _node.LayerAiSendModeOn;
            ApplySendModeUi();
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

            // 3. Compute resolution scale to preserve the high quality of the AI/slot image
            double resolutionScale = Math.Max(1.0, Math.Max((double)aiBmp.PixelWidth / newW, (double)aiBmp.PixelHeight / newH));
            int hiNewW = (int)Math.Round(newW * resolutionScale);
            int hiNewH = (int)Math.Round(newH * resolutionScale);
            int hiSrcW = (int)Math.Round(srcW * resolutionScale);
            int hiSrcH = (int)Math.Round(srcH * resolutionScale);

            // Resize to high-resolution target dimensions
            BitmapSource resizedAi = ResizeBitmapHighQuality(aiBmp, hiNewW, hiNewH, uniformToFill: !customW.HasValue);

            // 4. Calculate crop offsets in high resolution
            BitmapSource croppedAiRegion;
            if (customW.HasValue && customH.HasValue)
            {
                croppedAiRegion = resizedAi;
            }
            else
            {
                double xOffset = (hiNewW - hiSrcW) / 2.0;
                double yOffset = (hiNewH - hiSrcH) / 2.0;
                int cropX = Math.Clamp((int)Math.Round(xOffset), 0, hiNewW - 1);
                int cropY = Math.Clamp((int)Math.Round(yOffset), 0, hiNewH - 1);
                int cropW = Math.Clamp(hiSrcW, 1, hiNewW - cropX);
                int cropH = Math.Clamp(hiSrcH, 1, hiNewH - cropY);

                croppedAiRegion = new CroppedBitmap(resizedAi, new Int32Rect(cropX, cropY, cropW, cropH));
            }

            // Calculate parent positioning
            double parentX = 0;
            double parentY = 0;
            if (activeLayer != null)
            {
                if (!activeLayer.ContentBounds.IsEmpty)
                {
                    parentX = activeLayer.ContentBounds.X;
                    parentY = activeLayer.ContentBounds.Y;
                }
                else
                {
                    parentX = activeLayer.OffsetX;
                    parentY = activeLayer.OffsetY;
                }
            }
            int posX = (int)Math.Clamp(parentX + originalBounds.X, 0, childLayer.Width - 1);
            int posY = (int)Math.Clamp(parentY + originalBounds.Y, 0, childLayer.Height - 1);
            int finalW = Math.Clamp(srcW, 1, childLayer.Width - posX);
            int finalH = Math.Clamp(srcH, 1, childLayer.Height - posY);

            // 5. Get original mask template and resize it to match high-resolution cropped AI region
            BitmapSource maskTemplate = activeLayer.OriginalTransformBitmap ?? activeLayer.Bitmap;
            var maskBounds = GetLayerContentBounds(maskTemplate);
            if (!maskBounds.IsEmpty && maskBounds.Width > 0 && maskBounds.Height > 0)
            {
                int mx = Math.Clamp((int)maskBounds.X, 0, maskTemplate.PixelWidth - 1);
                int my = Math.Clamp((int)maskBounds.Y, 0, maskTemplate.PixelHeight - 1);
                int mw = Math.Clamp((int)Math.Ceiling(maskBounds.Width), 1, maskTemplate.PixelWidth - mx);
                int mh = Math.Clamp((int)Math.Ceiling(maskBounds.Height), 1, maskTemplate.PixelHeight - my);
                if (mw > 0 && mh > 0 && (mx > 0 || my > 0 || mw < maskTemplate.PixelWidth || mh < maskTemplate.PixelHeight))
                {
                    maskTemplate = new CroppedBitmap(maskTemplate, new Int32Rect(mx, my, mw, mh));
                }
            }

            BitmapSource resizedMask = ResizeBitmapHighQuality(maskTemplate, croppedAiRegion.PixelWidth, croppedAiRegion.PixelHeight, uniformToFill: false);
            int hiW = croppedAiRegion.PixelWidth;
            int hiH = croppedAiRegion.PixelHeight;

            // Create the masked cropped AI region using SkiaSharp (100% in native memory, no slow CPU loops!)
            var maskedBmp = new WriteableBitmap(hiW, hiH, 96, 96, PixelFormats.Bgra32, null);
            maskedBmp.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(hiW, hiH, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, maskedBmp.BackBuffer, maskedBmp.BackBufferStride))
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(SkiaSharp.SKColors.Transparent);

                    // 1. Draw the cropped AI image
                    var aiStride = croppedAiRegion.PixelWidth * 4;
                    var aiPixels = new byte[aiStride * croppedAiRegion.PixelHeight];
                    croppedAiRegion.CopyPixels(aiPixels, aiStride, 0);

                    using (var aiSkBmp = new SkiaSharp.SKBitmap())
                    {
                        var handleAi = System.Runtime.InteropServices.GCHandle.Alloc(aiPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                        try
                        {
                            aiSkBmp.InstallPixels(info, handleAi.AddrOfPinnedObject(), aiStride);
                            canvas.DrawBitmap(aiSkBmp, 0, 0);
                        }
                        finally
                        {
                            handleAi.Free();
                        }
                    }

                    // 2. Blend with the mask using DstIn (dest alpha * source alpha)
                    var maskStride = resizedMask.PixelWidth * 4;
                    var maskPixels = new byte[maskStride * resizedMask.PixelHeight];
                    resizedMask.CopyPixels(maskPixels, maskStride, 0);

                    using (var maskSkBmp = new SkiaSharp.SKBitmap())
                    {
                        var handleMask = System.Runtime.InteropServices.GCHandle.Alloc(maskPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                        try
                        {
                            maskSkBmp.InstallPixels(info, handleMask.AddrOfPinnedObject(), maskStride);
                            using (var paint = new SkiaSharp.SKPaint())
                            {
                                paint.BlendMode = SkiaSharp.SKBlendMode.DstIn;
                                canvas.DrawBitmap(maskSkBmp, 0, 0, paint);
                            }
                        }
                        finally
                        {
                            handleMask.Free();
                        }
                    }
                }
                maskedBmp.AddDirtyRect(new Int32Rect(0, 0, hiW, hiH));
            }
            finally
            {
                maskedBmp.Unlock();
            }

            // Draw masked cropped AI region onto childLayer.Bitmap using SkiaSharp
            childLayer.Bitmap.Lock();
            try
            {
                var info = new SkiaSharp.SKImageInfo(childLayer.Width, childLayer.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                using (var surface = SkiaSharp.SKSurface.Create(info, childLayer.Bitmap.BackBuffer, childLayer.Bitmap.BackBufferStride))
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(SkiaSharp.SKColors.Transparent);

                    maskedBmp.Lock();
                    try
                    {
                        var maskedInfo = new SkiaSharp.SKImageInfo(maskedBmp.PixelWidth, maskedBmp.PixelHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                        using (var maskedSkBmp = new SkiaSharp.SKBitmap())
                        {
                            maskedSkBmp.InstallPixels(maskedInfo, maskedBmp.BackBuffer, maskedBmp.BackBufferStride);
                            using (var paint = new SkiaSharp.SKPaint())
                            {
                                paint.FilterQuality = SkiaSharp.SKFilterQuality.High;
                                paint.IsAntialias = true;
                                canvas.DrawBitmap(maskedSkBmp, new SkiaSharp.SKRect(posX, posY, posX + finalW, posY + finalH), paint);
                            }
                        }
                    }
                    finally
                    {
                        maskedBmp.Unlock();
                    }
                }
                childLayer.Bitmap.AddDirtyRect(new Int32Rect(0, 0, childLayer.Width, childLayer.Height));
            }
            finally
            {
                childLayer.Bitmap.Unlock();
            }

            // Set OriginalTransformBitmap and ContentBounds so that transform tool works properly
            childLayer.OriginalTransformBitmap = maskedBmp;
            childLayer.ContentBounds = new Rect(posX, posY, finalW, finalH);
            childLayer.PngBytes = null;

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
            float x = 0;
            float y = 0;

            if (uniformToFill)
            {
                double scale = Math.Max((double)targetWidth / source.PixelWidth, (double)targetHeight / source.PixelHeight);
                drawW = (int)Math.Ceiling(source.PixelWidth * scale);
                drawH = (int)Math.Ceiling(source.PixelHeight * scale);
                x = (float)((targetWidth - drawW) / 2.0);
                y = (float)((targetHeight - drawH) / 2.0);
            }

            // Copy pixels from WPF BitmapSource to a byte array
            int stride = source.PixelWidth * 4;
            byte[] pixels = new byte[stride * source.PixelHeight];
            
            BitmapSource formattedSource = source;
            if (source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Pbgra32)
            {
                formattedSource = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            }
            formattedSource.CopyPixels(pixels, stride, 0);

            // Create target WriteableBitmap and perform high-quality scaling using SkiaSharp (up to 50x faster)
            var targetBmp = new WriteableBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Bgra32, null);
            targetBmp.Lock();
            try
            {
                var srcInfo = new SkiaSharp.SKImageInfo(source.PixelWidth, source.PixelHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
                var dstInfo = new SkiaSharp.SKImageInfo(targetWidth, targetHeight, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);

                using (var srcBitmap = new SkiaSharp.SKBitmap())
                {
                    var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                    try
                    {
                        srcBitmap.InstallPixels(srcInfo, handle.AddrOfPinnedObject(), stride);

                        using (var surface = SkiaSharp.SKSurface.Create(dstInfo, targetBmp.BackBuffer, targetBmp.BackBufferStride))
                        {
                            if (surface != null)
                            {
                                var canvas = surface.Canvas;
                                canvas.Clear(SkiaSharp.SKColors.Transparent);
                                using (var paint = new SkiaSharp.SKPaint())
                                {
                                    paint.FilterQuality = SkiaSharp.SKFilterQuality.High;
                                    paint.IsAntialias = true;
                                    canvas.DrawBitmap(srcBitmap, new SkiaSharp.SKRect(x, y, x + drawW, y + drawH), paint);
                                }
                            }
                        }
                    }
                    finally
                    {
                        handle.Free();
                    }
                }
                targetBmp.AddDirtyRect(new Int32Rect(0, 0, targetWidth, targetHeight));
            }
            finally
            {
                targetBmp.Unlock();
            }

            targetBmp.Freeze();
            return targetBmp;
        }

        #region Two-Way Drag and Drop (WPF <-> WebView2)

        private void SetupDragAndDrop()
        {
            // --- WPF-to-WebView Drag Source Setup ---
            var dragSources = new List<FrameworkElement>();
            if (ImgPreview != null) dragSources.Add(ImgPreview);
            if (ImgPreviewWv != null) dragSources.Add(ImgPreviewWv);
            dragSources.AddRange(_slotBorders);
            dragSources.AddRange(_slotBordersWv);

            foreach (var src in dragSources)
            {
                if (src == null) continue;

                src.PreviewMouseLeftButtonDown -= Src_PreviewMouseLeftButtonDown;
                src.PreviewMouseLeftButtonUp -= Src_PreviewMouseLeftButtonUp;
                src.PreviewMouseMove -= Src_PreviewMouseMove;

                src.PreviewMouseLeftButtonDown += Src_PreviewMouseLeftButtonDown;
                src.PreviewMouseLeftButtonUp += Src_PreviewMouseLeftButtonUp;
                src.PreviewMouseMove += Src_PreviewMouseMove;
            }

            // --- WebView-to-WPF Drop Target Setup ---
            var dropTargets = new List<FrameworkElement>();
            if (ImgPreview != null) dropTargets.Add(ImgPreview);
            if (ImgPreviewWv != null) dropTargets.Add(ImgPreviewWv);
            dropTargets.AddRange(_slotBorders);
            dropTargets.AddRange(_slotBordersWv);

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

        private void Src_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isMouseDownOnImage = true;
        }

        private void Src_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseDownOnImage = false;
        }

        private void Src_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isMouseDownOnImage && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPosition = e.GetPosition(null);
                if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isMouseDownOnImage = false;
                    StartDragDrop(sender);
                }
            }
        }

        private static TParent? FindParentBorderOrTarget<TParent>(DependencyObject? child) where TParent : DependencyObject
        {
            if (child == null) return null;
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is TParent target) return target;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void StartDragDrop(object sender)
        {
            try
            {
                BitmapSource? bitmap = null;
                string tempFileName = "dragged_image";

                FrameworkElement? element = sender as FrameworkElement;
                if (element != null)
                {
                    if (element.Name == "ImgPreview" || element.Name == "ImgPreviewWv")
                    {
                        bitmap = _activeLayer?.OriginalTransformBitmap ?? _activeLayer?.Bitmap ?? (ImgPreview?.Source as BitmapSource) ?? (ImgPreviewWv?.Source as BitmapSource);
                        tempFileName = "Main";
                    }
                    else if (element is Image img && (img.Name == "ImgPreview" || img.Name == "ImgPreviewWv"))
                    {
                        bitmap = _activeLayer?.OriginalTransformBitmap ?? _activeLayer?.Bitmap ?? (img.Source as BitmapSource);
                        tempFileName = "Main";
                    }
                    else
                    {
                        Border? border = element as Border ?? FindParentBorderOrTarget<Border>(element);
                        if (border != null && (border.Name == "ImgPreview" || border.Name == "ImgPreviewWv"))
                        {
                            bitmap = _activeLayer?.OriginalTransformBitmap ?? _activeLayer?.Bitmap ?? (ImgPreview?.Source as BitmapSource) ?? (ImgPreviewWv?.Source as BitmapSource);
                            tempFileName = "Main";
                        }
                        else if (border != null && border.Tag is string tagStr && int.TryParse(tagStr, out int idx) && idx >= 0 && idx < _secondaryImages.Count)
                        {
                            bitmap = _secondaryImages[idx].Bitmap;
                            tempFileName = $"{idx + 1}.Slot";
                        }
                        else if (element is Image slotImg && slotImg.Source is BitmapSource srcBmp)
                        {
                            bitmap = srcBmp;
                            tempFileName = "Slot";
                        }
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

                var fileUri = new Uri(tempPath).AbsoluteUri;

                // Create DataObject supporting all standard WPF, Windows OLE, CefSharp, and Web Browser formats
                var data = new DataObject();

                // 1. FileDrop (CF_HDROP)
                var fileList = new System.Collections.Specialized.StringCollection { tempPath };
                data.SetFileDropList(fileList);

                // 2. Text & UnicodeText formats (file path + file URL for web drag handlers)
                data.SetData(DataFormats.Text, tempPath);
                data.SetData(DataFormats.UnicodeText, tempPath);

                // 3. Standard Web URI List format (text/uri-list)
                data.SetData("text/uri-list", fileUri);

                // 4. HTML format for web drop targets
                string htmlContent = $"<img src=\"{fileUri}\"/>";
                data.SetData(DataFormats.Html, GetHtmlDataFormatString(htmlContent));

                // 5. Raw Bitmap format
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        var enc = new PngBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(bitmap));
                        enc.Save(ms);
                        using (var sysBmp = new System.Drawing.Bitmap(ms))
                        {
                            data.SetData(DataFormats.Bitmap, sysBmp, true);
                        }
                    }
                }
                catch { }

                // Set drag ghost image
                SetDragImage(data, bitmap);

                // Execute drag drop
                DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting drag drop: {ex.Message}");
            }
        }

        private static string GetHtmlDataFormatString(string html)
        {
            string header =
                "Version:0.9\r\n" +
                "StartHTML:0000000000\r\n" +
                "EndHTML:0000000000\r\n" +
                "StartFragment:0000000000\r\n" +
                "EndFragment:0000000000\r\n";
            string fragmentStart = "<!--StartFragment-->";
            string fragmentEnd = "<!--EndFragment-->";

            string fullHtml = "<html><body>" + fragmentStart + html + fragmentEnd + "</body></html>";

            int startHtml = header.Length;
            int startFragment = startHtml + "<html><body>".Length + fragmentStart.Length;
            int endFragment = startFragment + System.Text.Encoding.UTF8.GetByteCount(html);
            int endHtml = startFragment + System.Text.Encoding.UTF8.GetByteCount(html + fragmentEnd + "</body></html>");

            string formattedHeader =
                $"Version:0.9\r\n" +
                $"StartHTML:{startHtml:D10}\r\n" +
                $"EndHTML:{endHtml:D10}\r\n" +
                $"StartFragment:{startFragment:D10}\r\n" +
                $"EndFragment:{endFragment:D10}\r\n";

            return formattedHeader + fullHtml;
        }

        private void SlotBorder_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (sender is Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int idx))
                {
                    if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
                    {
                        _secondaryImages[idx].Bitmap = null;
                        _secondaryImages[idx].FilePath = null;
                        _secondaryImages[idx].IsSelected = false;
                        RefreshAllSlotsUI();
                        UpdateSecondaryInfo();
                        UpdatePreviewImage();
                    }
                    e.Handled = true;
                }
            }
        }

        private async void Control_Drop(object sender, DragEventArgs e)
        {
            try
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;

                BitmapSource? droppedBitmap = await GetImageFromDragEventArgsAsync(e);

                if (droppedBitmap != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProcessDroppedImage(sender, droppedBitmap);
                    });
                }

                // Reset CefSharp drag state asynchronously without blocking UI updates
                _ = Task.Run(() => ResetWebView2DragStateAsync());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error on drop: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ResetWebView2DragStateAsync()
        {
            try
            {
                var cacheState = LayerAiWebViewCache.GetOrCreateState(_node.Id);
                foreach (var cachedTab in cacheState.WebBrowsers)
                {
                    var activeWv = cachedTab.WebView;
                    if (activeWv != null)
                    {
                        try { await activeWv.EvaluateScriptAsync("if (window.resetDragState) window.resetDragState();"); } catch { }
                    }
                }
                var dynamicWv = cacheState.DynamicWebView;
                if (dynamicWv != null)
                {
                    await dynamicWv.EvaluateScriptAsync("if (window.resetDragState) window.resetDragState();");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to reset drag state: {ex.Message}");
            }
        }

        private async Task<BitmapSource?> GetImageFromDragEventArgsAsync(DragEventArgs e)
        {
            // 0a. Check FileContents (direct original compressed file data from Chrome/Edge)
            if (e.Data.GetDataPresent("FileContents"))
            {
                try
                {
                    var raw = e.Data.GetData("FileContents");
                    if (raw is MemoryStream ms)
                    {
                        ms.Position = 0;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                    else if (raw is Stream stm)
                    {
                        using var msCopy = new MemoryStream();
                        stm.CopyTo(msCopy);
                        msCopy.Position = 0;
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = msCopy;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
                catch { }

                var comBmp = GetImageFromFileContentsCOM(e.Data);
                if (comBmp != null) return comBmp;
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
                        ChromiumWebBrowser? activeWv = null;
                        if (_activeTab == ActiveTab.WebBrowser && _activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count) activeWv = _webTabs[_activeTabIdx].WebView;
                        else if (_activeTab == ActiveTab.WebView) activeWv = _dynamicWebView;
                        if (activeWv != null && !string.IsNullOrWhiteSpace(activeWv.Address))
                        {
                            pageUrl = activeWv.Address;
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
                    try
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
                    catch (InvalidCastException)
                    {
                        // Interface not supported on this thread/apartment, ignore silently
                    }
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

            if (idx >= 0 && idx < _secondarySlotCount && idx < _secondaryImages.Count)
            {
                _secondaryImages[idx].Bitmap = bitmap;
                _secondaryImages[idx].FilePath = null;
                _secondaryImages[idx].IsSelected = true;
                RefreshAllSlotsUI();
            }
            else
            {
                int targetIdx = -1;
                for (int i = 0; i < _secondaryImages.Count; i++)
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
                ChromiumWebBrowser? activeWv = null;
                if (_activeTab == ActiveTab.WebBrowser && _activeTabIdx >= 0 && _activeTabIdx < _webTabs.Count) activeWv = _webTabs[_activeTabIdx].WebView;
                else if (_activeTab == ActiveTab.WebView) activeWv = _dynamicWebView;

                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                    string? pageUrl = null;
                    if (activeWv != null && !string.IsNullOrWhiteSpace(activeWv.Address))
                    {
                        pageUrl = activeWv.Address;
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

                    if (activeWv != null)
                    {
                        try
                        {
                            var cookieManager = Cef.GetGlobalCookieManager();
                            var cookieTask = cookieManager.VisitUrlCookiesAsync(uri.ToString(), true);
                            if (await System.Threading.Tasks.Task.WhenAny(cookieTask, System.Threading.Tasks.Task.Delay(150)) == cookieTask)
                            {
                                var cookies = await cookieTask;
                                if (cookies != null && cookies.Count > 0)
                                {
                                    var cookiePairs = cookies.Select(c => $"{c.Name}={c.Value}");
                                    string cookieHeaderValue = string.Join("; ", cookiePairs);
                                    client.DefaultRequestHeaders.Add("Cookie", cookieHeaderValue);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to get CefSharp cookies: {ex.Message}");
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
 
        #endregion
 
        #endregion
    }

    public static class LayerAiWebViewCache
    {
        private static readonly System.Collections.Generic.Dictionary<string, CachedWebViewState> _cache = new();

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
            public System.Collections.Generic.List<CachedTabState> WebBrowsers { get; set; } = new();
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
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"ChromiumWebBrowser idle check for node {nodeId}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error putting browser to sleep: {ex.Message}");
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
                var keys = System.Linq.Enumerable.ToList(_cache.Keys);
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

        private static readonly System.Collections.Generic.Dictionary<string, DialogCacheItem> _cache = new();
        private static readonly object _lock = new();

        public static LayerAiDialog OpenDialog(System.Collections.Generic.List<EditorLayer> selectedLayers, EditorLayer activeLayer, ImageProcessingNode node, IWorkflowEditorHost host, EditorDocument doc, Window? owner)
        {
            lock (_lock)
            {
                string nodeId = node.Id;
                if (_cache.TryGetValue(nodeId, out var item) && item.Dialog != null)
                {
                    // Cancel 3-minute idle timer
                    item.IdleTimer?.Dispose();
                    item.IdleTimer = null;

                    item.Dialog.Dispatcher.Invoke(() =>
                    {
                        item.Dialog.ReinitializeSession(selectedLayers, activeLayer, node, host, doc, owner);
                        if (!item.Dialog.IsVisible)
                        {
                            item.Dialog.Show();
                        }
                        item.Dialog.Activate();
                        item.Dialog.Topmost = true;
                    });
                    return item.Dialog;
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
                var keys = System.Linq.Enumerable.ToList(_cache.Keys);
                foreach (var key in keys)
                {
                    CloseAndDisposeDialog(key);
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
