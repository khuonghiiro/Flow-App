// ========================================================================================
// IMPORTANT FOR AI CODING ASSISTANTS & DEVELOPERS:
// DO NOT ALLOW ANY FILE IN THIS COMPONENT TO EXCEED ~1500 LINES OF CODE!
// To maintain readability, ease of testing, and modularity:
// - If a file grows larger than ~1500 lines, you MUST split/separate the logic into a new
//   partial class file (e.g., ImageProcessingNodeContentControl.<FeatureName>.cs).
// - Always place distinct features, tools, or event groupings in their respective files.
// - Ensure comments and documentation remain clean and structured.
// ========================================================================================
using FlowMy.Controls;
using SkiaSharp;
using FlowMy.Converters;
using FlowMy.Helpers;
using FlowMy.Models;
using FlowMy.Models.ImageEditor;
using FlowMy.Models.ImageEditor.Commands;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl
    {
        private void BtnOpenImage_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            OpenImageAndAddTab();
        }

        private void BtnOpenImageTab_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            OpenImageAndAddTab();
        }

        private void OpenImageAndAddTab()
        {
            // Lưu state tab hiện tại trước khi mở ảnh mới
            if (_activeTabData != null)
            {
                SaveCurrentTabState();
            }

            // Mở file picker
            var oldUrl = _node.ImageUrl;
            ImageProcessingNodeControl.OpenImageFilePicker(_node);

            // Nếu user chọn ảnh mới (URL thay đổi) → tạo tab mới
            if (_node.ImageUrl != oldUrl && !string.IsNullOrWhiteSpace(_node.ImageUrl))
            {
                // Tạo EditorDoc mới cho ảnh mới
                _node.EditorDoc = null;

                // Đánh dấu cần tạo tab mới (không phải update tab cũ)
                _activeTabData = null;
            }
            // Tab sẽ được tạo/sync bởi SyncActiveTab sau khi UpdatePreviewAsync hoàn tất
        }


        /// <summary>Per-tab state lưu trữ ảnh + editor doc riêng cho mỗi tab.</summary>
        private class ImageTabData
        {
            public string Title { get; set; } = "";
            public string ImageUrl { get; set; } = "";
            public ImageInputMode InputMode { get; set; }
            public string ImageBase64 { get; set; } = "";
            public EditorDocument? EditorDoc { get; set; }
            public BitmapSource? CachedMainImage { get; set; }
            public Border? TabBorder { get; set; }

            // Layout & size states
            public double ImageWidth { get; set; } = double.NaN;
            public double ImageHeight { get; set; } = double.NaN;
            public double ZoomScaleX { get; set; } = 1.0;
            public double ZoomScaleY { get; set; } = 1.0;
            public double CanvasTranslateX { get; set; } = 0.0;
            public double CanvasTranslateY { get; set; } = 0.0;

            // Selection state
            public Geometry? ActiveSelectionGeometry { get; set; }
            public bool IsSelecting { get; set; }
            public Point SelectionStartPoint { get; set; }
            public Rect? SelectionRect { get; set; }
            public System.Collections.Generic.List<Point> SelectionPoints { get; set; } = new();
            public bool[,]? CachedSelectionMask { get; set; }
            public int CachedSelectionStartX { get; set; }
            public int CachedSelectionStartY { get; set; }
            public int CachedSelectionEndX { get; set; }
            public int CachedSelectionEndY { get; set; }
            public bool HasCachedSelectionMask { get; set; }
        }

        private readonly List<ImageTabData> _tabs = new();
        private ImageTabData? _activeTabData;
        private bool _isSwitchingTab;

        /// <summary>Kiểm tra title thay đổi và sync vào tab strip.</summary>
        private void SyncActiveTab()
        {
            if (ImageTitleTextBlock == null || ImageTabStrip == null) return;
            string title = ImageTitleTextBlock.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(title) || title == "Chưa có ảnh" || title == "Đang tải ảnh...") return;
            if (_isSwitchingTab) return;

            if (_activeTabData != null)
            {
                // Cập nhật title của tab đang active
                _activeTabData.Title = title;
                if (_activeTabData.TabBorder != null)
                {
                    _activeTabData.TabBorder.Tag = title;
                    // Cập nhật TextBlock title trong tab
                    if (_activeTabData.TabBorder.Child is DockPanel dp)
                    {
                        foreach (var c in dp.Children)
                        {
                            if (c is TextBlock tb && tb.FontSize == 10)
                            {
                                tb.Text = title;
                                break;
                            }
                        }
                    }
                }
                // Lưu state hiện tại
                SaveCurrentTabState();
                return;
            }

            // Chưa có tab nào → tạo tab mới
            var tabData = new ImageTabData { Title = title };
            
            // Clear selection so the new tab starts fresh
            ClearSelection();

            SaveStateToTabData(tabData);
            var tabBorder = BuildTabItem(tabData);
            tabData.TabBorder = tabBorder;
            _tabs.Add(tabData);
            ImageTabStrip.Children.Add(tabBorder);
            _activeTabData = tabData;
            HighlightActiveTab();
        }

        private void SaveCurrentTabState()
        {
            if (_activeTabData == null) return;
            CommitBrushDrawingSession();
            CommitTransformSession();
            CommitActiveText();
            CommitKeyMoveSession();
            ForceClearDrawingOverlay();
            SaveStateToTabData(_activeTabData);
        }

        private void SaveStateToTabData(ImageTabData data)
        {
            data.ImageUrl = _node.ImageUrl ?? "";
            data.InputMode = _node.InputMode;
            data.ImageBase64 = _node.ImageBase64 ?? "";
            data.EditorDoc = _node.EditorDoc;
            data.CachedMainImage = MainImage.Source as BitmapSource;
            data.ImageWidth = MainImage.Width;
            data.ImageHeight = MainImage.Height;
            data.ZoomScaleX = ImageZoomScale.ScaleX;
            data.ZoomScaleY = ImageZoomScale.ScaleY;
            data.CanvasTranslateX = CanvasTranslate.X;
            data.CanvasTranslateY = CanvasTranslate.Y;

            // Selection state
            data.ActiveSelectionGeometry = _activeSelectionGeometry;
            data.IsSelecting = _isSelecting;
            data.SelectionStartPoint = _selectionStartPoint;
            data.SelectionRect = _selectionRect;
            data.SelectionPoints = _selectionPoints.ToList();
            data.CachedSelectionMask = _cachedSelectionMask;
            data.CachedSelectionStartX = _cachedSelectionStartX;
            data.CachedSelectionStartY = _cachedSelectionStartY;
            data.CachedSelectionEndX = _cachedSelectionEndX;
            data.CachedSelectionEndY = _cachedSelectionEndY;
            data.HasCachedSelectionMask = _hasCachedSelectionMask;
        }

        private void LoadTabState(ImageTabData data)
        {
            _isSwitchingTab = true;
            try
            {
                // Khôi phục node state
                _node.ImageUrl = data.ImageUrl;
                _node.InputMode = data.InputMode;
                _node.ImageBase64 = data.ImageBase64;
                _node.EditorDoc = data.EditorDoc;

                // Khôi phục ảnh hiển thị
                if (data.CachedMainImage != null)
                {
                    MainImage.Source = data.CachedMainImage;
                    PlaceholderTextBlock.Visibility = Visibility.Collapsed;
                }

                // Restore image dimensions and layout scale
                MainImage.Width = data.ImageWidth;
                MainImage.Height = data.ImageHeight;
                ImageZoomScale.ScaleX = data.ZoomScaleX;
                ImageZoomScale.ScaleY = data.ZoomScaleY;

                // Reposition ImageAreaGrid on canvas based on image size
                if (!double.IsNaN(data.ImageWidth) && !double.IsNaN(data.ImageHeight) &&
                    data.ImageWidth > 0 && data.ImageHeight > 0)
                {
                    Canvas.SetLeft(ImageAreaGrid, CanvasHalf - data.ImageWidth / 2.0);
                    Canvas.SetTop(ImageAreaGrid, CanvasHalf - data.ImageHeight / 2.0);
                }

                // Sync editor panel
                if (_node.EditorDoc != null)
                {
                    EditorPanel.SetDocument(_node.EditorDoc);
                }

                // Cập nhật title
                ImageTitleTextBlock.Text = data.Title;

                // Khôi phục selection state
                _activeSelectionGeometry = data.ActiveSelectionGeometry;
                _isSelecting = data.IsSelecting;
                _selectionStartPoint = data.SelectionStartPoint;
                _selectionRect = data.SelectionRect;
                _selectionPoints.Clear();
                if (data.SelectionPoints != null)
                {
                    _selectionPoints.AddRange(data.SelectionPoints);
                }
                _cachedSelectionMask = data.CachedSelectionMask;
                _cachedSelectionStartX = data.CachedSelectionStartX;
                _cachedSelectionStartY = data.CachedSelectionStartY;
                _cachedSelectionEndX = data.CachedSelectionEndX;
                _cachedSelectionEndY = data.CachedSelectionEndY;
                _hasCachedSelectionMask = data.HasCachedSelectionMask;

                _activeTabData = data;

                // Redraw selection for this tab
                UpdatePolygonDisplay();

                // Restore canvas translate positions
                CanvasTranslate.X = data.CanvasTranslateX;
                CanvasTranslate.Y = data.CanvasTranslateY;
                _hasUserCentered = true;
            }
            finally
            {
                _isSwitchingTab = false;
            }
        }

        private Border BuildTabItem(ImageTabData tabData)
        {
            var tab = new Border
            {
                Tag = tabData.Title,
                Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Padding = new Thickness(8, 2, 4, 2),
                Margin = new Thickness(0, 0, 1, 0),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0, 0, 0, 2),
                BorderBrush = System.Windows.Media.Brushes.Transparent
            };

            var dock = new DockPanel { LastChildFill = true };

            // Close button (×)
            var closeBtn = new TextBlock
            {
                Text = "×",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x86, 0x96)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            closeBtn.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                CloseTab(tabData);
            };
            closeBtn.MouseEnter += (s, _) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
            closeBtn.MouseLeave += (s, _) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x86, 0x96));
            DockPanel.SetDock(closeBtn, Dock.Right);
            dock.Children.Add(closeBtn);

            // Tab title
            var titleTb = new TextBlock
            {
                Text = tabData.Title,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xE3, 0xEF)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 140
            };
            dock.Children.Add(titleTb);

            tab.Child = dock;

            // Click to switch tab
            tab.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                SwitchToTab(tabData);
            };

            return tab;
        }

        private void SwitchToTab(ImageTabData tabData)
        {
            if (ReferenceEquals(tabData, _activeTabData)) return;

            // Lưu state tab hiện tại
            SaveCurrentTabState();

            // Load state tab được chọn
            LoadTabState(tabData);
            HighlightActiveTab();
        }

        private void HighlightActiveTab()
        {
            var accentColor = Color.FromRgb(0x00, 0xCF, 0xFF);
            var transparentBrush = System.Windows.Media.Brushes.Transparent;
            var activeBg = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            var inactiveBg = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));

            foreach (var td in _tabs)
            {
                if (td.TabBorder == null) continue;
                bool isActive = ReferenceEquals(td, _activeTabData);
                td.TabBorder.Background = isActive ? activeBg : inactiveBg;
                td.TabBorder.BorderBrush = isActive ? new SolidColorBrush(accentColor) : transparentBrush;
            }
        }

        private void CloseTab(ImageTabData tabData)
        {
            if (tabData.TabBorder != null)
                ImageTabStrip.Children.Remove(tabData.TabBorder);
            _tabs.Remove(tabData);

            if (ReferenceEquals(tabData, _activeTabData))
            {
                _activeTabData = null;
                if (_tabs.Count > 0)
                {
                    SwitchToTab(_tabs[_tabs.Count - 1]);
                }
                else
                {
                    // Không còn tab nào
                    MainImage.Source = null;
                    PlaceholderTextBlock.Visibility = Visibility.Visible;
                    PlaceholderTextBlock.Text = "Chưa có ảnh";
                    ImageTitleTextBlock.Text = "Chưa có ảnh";
                }
            }
            HighlightActiveTab();
        }

        /// <summary>UpdatePreviewAsync rồi sync tab title + tạo EditorDoc nếu cần.</summary>
        private async System.Threading.Tasks.Task UpdatePreviewAndSyncTab()
        {
            if (!_isSwitchingTab)
            {
                ForceClearDrawingOverlay();
                _node.EditorDoc = null;
                _activeTabData = null;
            }

            await ImageProcessingNodeControl.UpdatePreviewAsync(
                _node, _host, MainImage, PlaceholderTextBlock, ImageZoomScale,
                ImageAreaGrid, CenterImageOnCanvas, ImageTitleTextBlock,
                _onCropClickForIp);

            // Tạo EditorDoc cho ảnh mới nếu chưa có (quan trọng cho new tabs)
            if (_node.EditorDoc == null && MainImage.Source is BitmapSource bmp)
            {
                _node.EditorDoc = Models.ImageEditor.EditorDocument.FromBitmapSource(bmp);
                EditorPanel.SetDocument(_node.EditorDoc);
            }

            SyncActiveTab();
        }
    }
}
