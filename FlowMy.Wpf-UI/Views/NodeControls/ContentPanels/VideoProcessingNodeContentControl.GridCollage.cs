// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowMy.Models.Nodes;
using FlowMy.Services.Workflow;

namespace FlowMy.Views.NodeControls
{
    public partial class VideoProcessingNodeContentControl
    {
        private bool _isGridCollageSyncing;

        /// <summary>
        /// Wires user interaction events for the Grid Collage / Contact Sheet configuration controls.
        /// </summary>
        private void WireGridCollageEvents()
        {
            if (GridCollageToggle != null)
            {
                GridCollageToggle.Checked += (_, _) => OnGridCollageToggleChanged();
                GridCollageToggle.Unchecked += (_, _) => OnGridCollageToggleChanged();
            }

            if (GridCollageWidthSlider != null)
            {
                GridCollageWidthSlider.ValueChanged += (_, _) =>
                {
                    if (_isGridCollageSyncing || _node == null) return;
                    var val = (int)Math.Round(GridCollageWidthSlider.Value);
                    _node.GridCollageWidth = val;
                    if (GridCollageWidthBox != null && GridCollageWidthBox.Text != val.ToString(CultureInfo.InvariantCulture))
                    {
                        _isGridCollageSyncing = true;
                        GridCollageWidthBox.Text = val.ToString(CultureInfo.InvariantCulture);
                        _isGridCollageSyncing = false;
                    }
                    UpdateGridCollagePreviewUi();
                };
            }

            if (GridCollageWidthBox != null)
            {
                GridCollageWidthBox.TextChanged += (_, _) =>
                {
                    if (_isGridCollageSyncing || _node == null) return;
                    if (int.TryParse(GridCollageWidthBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) && w >= 50)
                    {
                        _node.GridCollageWidth = w;
                        if (GridCollageWidthSlider != null && (int)Math.Round(GridCollageWidthSlider.Value) != w)
                        {
                            _isGridCollageSyncing = true;
                            GridCollageWidthSlider.Value = Math.Clamp(w, GridCollageWidthSlider.Minimum, GridCollageWidthSlider.Maximum);
                            _isGridCollageSyncing = false;
                        }
                        UpdateGridCollagePreviewUi();
                    }
                };
            }

            if (GridCollageHeightSlider != null)
            {
                GridCollageHeightSlider.ValueChanged += (_, _) =>
                {
                    if (_isGridCollageSyncing || _node == null) return;
                    var val = (int)Math.Round(GridCollageHeightSlider.Value);
                    _node.GridCollageHeight = val;
                    if (GridCollageHeightBox != null && GridCollageHeightBox.Text != val.ToString(CultureInfo.InvariantCulture))
                    {
                        _isGridCollageSyncing = true;
                        GridCollageHeightBox.Text = val.ToString(CultureInfo.InvariantCulture);
                        _isGridCollageSyncing = false;
                    }
                    UpdateGridCollagePreviewUi();
                };
            }

            if (GridCollageHeightBox != null)
            {
                GridCollageHeightBox.TextChanged += (_, _) =>
                {
                    if (_isGridCollageSyncing || _node == null) return;
                    if (int.TryParse(GridCollageHeightBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && h >= 50)
                    {
                        _node.GridCollageHeight = h;
                        if (GridCollageHeightSlider != null && (int)Math.Round(GridCollageHeightSlider.Value) != h)
                        {
                            _isGridCollageSyncing = true;
                            GridCollageHeightSlider.Value = Math.Clamp(h, GridCollageHeightSlider.Minimum, GridCollageHeightSlider.Maximum);
                            _isGridCollageSyncing = false;
                        }
                        UpdateGridCollagePreviewUi();
                    }
                };
            }

            if (GridCollageFrameCountSlider != null)
            {
                GridCollageFrameCountSlider.ValueChanged += (_, _) =>
                {
                    if (_isGridCollageSyncing || _node == null) return;
                    var val = (int)Math.Round(GridCollageFrameCountSlider.Value);
                    _node.GridCollageFrameCount = val;
                    if (GridCollageFrameCountBox != null && GridCollageFrameCountBox.Text != val.ToString(CultureInfo.InvariantCulture))
                    {
                        _isGridCollageSyncing = true;
                        GridCollageFrameCountBox.Text = val.ToString(CultureInfo.InvariantCulture);
                        _isGridCollageSyncing = false;
                    }
                    UpdateGridCollagePreviewUi();
                };
            }

            if (GridCollageFrameCountBox != null)
            {
                GridCollageFrameCountBox.TextChanged += (_, _) =>
                {
                    if (_isGridCollageSyncing || _node == null) return;
                    if (int.TryParse(GridCollageFrameCountBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cnt) && cnt >= 1)
                    {
                        _node.GridCollageFrameCount = cnt;
                        if (GridCollageFrameCountSlider != null && (int)Math.Round(GridCollageFrameCountSlider.Value) != cnt)
                        {
                            _isGridCollageSyncing = true;
                            GridCollageFrameCountSlider.Value = Math.Clamp(cnt, GridCollageFrameCountSlider.Minimum, GridCollageFrameCountSlider.Maximum);
                            _isGridCollageSyncing = false;
                        }
                        UpdateGridCollagePreviewUi();
                    }
                };
            }

            if (GridCollageColorCombo != null)
            {
                GridCollageColorCombo.SelectionChanged += (_, _) =>
                {
                    if (_isGridCollageSyncing || _node == null) return;
                    if (GridCollageColorCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                    {
                        if (tag != "custom")
                        {
                            _node.GridCollageBackgroundColor = tag;
                            _node.GridCollageColorKey = tag;
                            UpdateGridCollageColorSwatch(tag);
                            UpdateGridCollagePreviewUi();
                        }
                    }
                };
            }

            if (PickGridCollageBgColorButton != null)
            {
                PickGridCollageBgColorButton.Click += (_, _) => PickGridCollageBgColor();
            }

            if (GridCollageAspectCombo != null)
            {
                GridCollageAspectCombo.SelectionChanged += (_, _) =>
                {
                    if (_isGridCollageSyncing || _node == null) return;
                    if (GridCollageAspectCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                    {
                        _node.GridCollageAspectMode = tag;
                        UpdateGridCollagePreviewUi();
                    }
                };
            }

            if (GridCollagePaddingBox != null)
            {
                GridCollagePaddingBox.TextChanged += (_, _) =>
                {
                    if (_isGridCollageSyncing || _node == null) return;
                    _node.GridCollagePadding = GridCollagePaddingBox.Text;
                    UpdateGridCollagePreviewUi();
                };
            }

            if (GridCollageMarginBox != null)
            {
                GridCollageMarginBox.TextChanged += (_, _) =>
                {
                    if (_isGridCollageSyncing || _node == null) return;
                    _node.GridCollageMargin = GridCollageMarginBox.Text;
                    UpdateGridCollagePreviewUi();
                };
            }
        }

        private void OnGridCollageToggleChanged()
        {
            if (_isGridCollageSyncing || _node == null) return;
            var isEnabled = GridCollageToggle?.IsChecked == true;
            _node.GridCollageEnabled = isEnabled;
            if (GridCollageContainerGrid != null)
                GridCollageContainerGrid.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
            if (isEnabled)
                UpdateGridCollagePreviewUi();
        }

        /// <summary>
        /// Synchronizes UI controls with current values from the VideoProcessingNode model.
        /// </summary>
        public void SyncGridCollageControlsFromModel()
        {
            if (_node == null) return;
            _isGridCollageSyncing = true;
            try
            {
                if (GridCollageToggle != null)
                    GridCollageToggle.IsChecked = _node.GridCollageEnabled;

                if (GridCollageContainerGrid != null)
                    GridCollageContainerGrid.Visibility = _node.GridCollageEnabled ? Visibility.Visible : Visibility.Collapsed;

                if (GridCollageWidthSlider != null)
                    GridCollageWidthSlider.Value = _node.GridCollageWidth > 0 ? _node.GridCollageWidth : 1000;
                if (GridCollageWidthBox != null)
                    GridCollageWidthBox.Text = _node.GridCollageWidth.ToString(CultureInfo.InvariantCulture);

                if (GridCollageHeightSlider != null)
                    GridCollageHeightSlider.Value = _node.GridCollageHeight > 0 ? _node.GridCollageHeight : 1000;
                if (GridCollageHeightBox != null)
                    GridCollageHeightBox.Text = _node.GridCollageHeight.ToString(CultureInfo.InvariantCulture);

                if (GridCollageFrameCountSlider != null)
                    GridCollageFrameCountSlider.Value = _node.GridCollageFrameCount > 0 ? _node.GridCollageFrameCount : 4;
                if (GridCollageFrameCountBox != null)
                    GridCollageFrameCountBox.Text = _node.GridCollageFrameCount.ToString(CultureInfo.InvariantCulture);

                if (GridCollagePaddingBox != null)
                    GridCollagePaddingBox.Text = string.IsNullOrWhiteSpace(_node.GridCollagePadding) ? "10" : _node.GridCollagePadding;

                if (GridCollageMarginBox != null)
                    GridCollageMarginBox.Text = string.IsNullOrWhiteSpace(_node.GridCollageMargin) ? "0" : _node.GridCollageMargin;

                SyncGridCollageAspectCombo();
                SyncGridCollageColorCombo();
                UpdateGridCollageColorSwatch(_node.GridCollageBackgroundColor);
                UpdateGridCollagePreviewUi();
            }
            finally
            {
                _isGridCollageSyncing = false;
            }
        }

        private void SyncGridCollageAspectCombo()
        {
            if (GridCollageAspectCombo == null || _node == null) return;
            var targetTag = (_node.GridCollageAspectMode ?? "auto").Trim().ToLowerInvariant();
            foreach (var item in GridCollageAspectCombo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString(), targetTag, StringComparison.OrdinalIgnoreCase))
                {
                    GridCollageAspectCombo.SelectedItem = item;
                    return;
                }
            }
        }

        private void SyncGridCollageColorCombo()
        {
            if (GridCollageColorCombo == null || _node == null) return;
            var targetTag = (_node.GridCollageColorKey ?? "white").Trim();
            var matched = false;
            foreach (var item in GridCollageColorCombo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString(), targetTag, StringComparison.OrdinalIgnoreCase))
                {
                    GridCollageColorCombo.SelectedItem = item;
                    matched = true;
                    break;
                }
            }
            if (!matched && GridCollageColorCombo.Items.Count > 0)
            {
                var customItem = GridCollageColorCombo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string?)i.Tag == "custom");
                if (customItem != null)
                    GridCollageColorCombo.SelectedItem = customItem;
            }
        }

        private void UpdateGridCollageColorSwatch(string? colorKey)
        {
            if (GridCollageColorPreviewBox == null) return;
            var color = VideoFrameCollageComposer.ParseColor(colorKey, Colors.White);
            GridCollageColorPreviewBox.Background = new SolidColorBrush(color);
        }

        private void PickGridCollageBgColor()
        {
            if (_node == null) return;
            var initialColor = VideoFrameCollageComposer.ParseColor(_node.GridCollageBackgroundColor, Colors.White);
            using var dialog = new System.Windows.Forms.ColorDialog
            {
                Color = System.Drawing.Color.FromArgb(initialColor.A, initialColor.R, initialColor.G, initialColor.B),
                FullOpen = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var c = dialog.Color;
                var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                _node.GridCollageBackgroundColor = hex;
                _node.GridCollageColorKey = "custom";
                UpdateGridCollageColorSwatch(hex);
                SyncGridCollageColorCombo();
                UpdateGridCollagePreviewUi();
            }
        }

        /// <summary>
        /// Redraws the live interactive preview canvas in Column 2.
        /// </summary>
        public void UpdateGridCollagePreviewUi()
        {
            if (GridCollagePreviewCanvas == null || _node == null) return;

            var canvasW = Math.Clamp(_node.GridCollageWidth, 50, 8000);
            var canvasH = Math.Clamp(_node.GridCollageHeight, 50, 8000);
            var count = Math.Clamp(_node.GridCollageFrameCount, 1, 128);
            var margin = VideoFrameCollageComposer.ParseThickness(_node.GridCollageMargin);
            var padding = VideoFrameCollageComposer.ParseThickness(_node.GridCollagePadding);

            var natW = PreviewMedia?.NaturalVideoWidth ?? 0;
            var natH = PreviewMedia?.NaturalVideoHeight ?? 0;
            var probeAspect = natW > 0 && natH > 0 ? (double)natW / natH : 16.0 / 9.0;
            var itemAspect = VideoFrameCollageComposer.ResolveItemAspect(_node, probeAspect);

            GridCollagePreviewCanvas.Width = canvasW;
            GridCollagePreviewCanvas.Height = canvasH;

            var bgColor = VideoFrameCollageComposer.ParseColor(_node.GridCollageBackgroundColor, Colors.White);
            GridCollagePreviewCanvas.Background = new SolidColorBrush(bgColor);

            GridCollagePreviewCanvas.Children.Clear();

            var (cols, rows) = VideoFrameCollageComposer.CalculateGridDimensions(count, canvasW, canvasH, itemAspect);
            var slots = VideoFrameCollageComposer.CalculateSlotLayouts(count, canvasW, canvasH, margin, padding, itemAspect);

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];

                // Outer Cell Border (Margin outline if margin > 0)
                if (margin.Left > 0 || margin.Top > 0 || margin.Right > 0 || margin.Bottom > 0)
                {
                    var cellBorder = new Border
                    {
                        Width = slot.CellRect.Width,
                        Height = slot.CellRect.Height,
                        BorderBrush = new SolidColorBrush(Color.FromArgb(50, 128, 128, 128)),
                        BorderThickness = new Thickness(1),
                        SnapsToDevicePixels = true
                    };
                    Canvas.SetLeft(cellBorder, slot.CellRect.X);
                    Canvas.SetTop(cellBorder, slot.CellRect.Y);
                    GridCollagePreviewCanvas.Children.Add(cellBorder);
                }

                // Inner Container with subtle background
                var slotBorder = new Border
                {
                    Width = Math.Max(2, slot.ImageRect.Width),
                    Height = Math.Max(2, slot.ImageRect.Height),
                    CornerRadius = new CornerRadius(Math.Min(12, Math.Min(slot.ImageRect.Width, slot.ImageRect.Height) * 0.05)),
                    Background = new LinearGradientBrush(
                        Color.FromArgb(230, 30, 41, 59),
                        Color.FromArgb(250, 15, 23, 42),
                        new Point(0, 0),
                        new Point(1, 1)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(140, 59, 130, 246)),
                    BorderThickness = new Thickness(Math.Max(1, canvasW * 0.002)),
                    ClipToBounds = true
                };

                // Badge & Label inside preview slot
                var innerGrid = new Grid();
                var tagBlock = new TextBlock
                {
                    Text = $"#{i + 1}",
                    FontSize = Math.Max(9, Math.Min(36, slot.ImageRect.Height * 0.14)),
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas, Segoe UI"),
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                innerGrid.Children.Add(tagBlock);

                slotBorder.Child = innerGrid;

                Canvas.SetLeft(slotBorder, slot.ImageRect.X);
                Canvas.SetTop(slotBorder, slot.ImageRect.Y);
                GridCollagePreviewCanvas.Children.Add(slotBorder);
            }

            if (GridCollagePreviewBadge != null)
                GridCollagePreviewBadge.Text = $"{canvasW} × {canvasH}";

            if (GridCollageMetricsText != null)
            {
                var duration = Math.Max(0.1, GetNaturalDurationSeconds());
                var estTotalFrames = _node.ExtractByFpsEnabled
                    ? Math.Max(1, (int)Math.Round(duration * Math.Max(0.001, _node.ExtractFps)))
                    : (_node.ExtractFrameCount > 0 ? _node.ExtractFrameCount : 4);
                if (_node.ExtractAllFrames)
                {
                    var fps = _node.SourceFps > 0 ? _node.SourceFps : 24;
                    estTotalFrames = Math.Max(1, (int)Math.Floor(duration * fps));
                }
                var estCollages = (int)Math.Ceiling((double)Math.Max(1, estTotalFrames) / count);
                GridCollageMetricsText.Text = $"Lưới: {cols} cột × {rows} hàng • {count} frame/ảnh cha • Ước tính: ~{estCollages} ảnh ghép (từ {estTotalFrames} frame)";
            }
        }
    }
}
