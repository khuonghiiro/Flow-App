// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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
        private void OnCropRegionSelectedForIp(ImageCropRegion reg)
        {
            if (!_ipColumnVisible)
                ToggleIPColumn();

            ImageProcessingNodeControl._currentCropRegionForIp[_node] = reg;
            if (reg.Thumbnail is BitmapSource thumb)
                _setIpImage?.Invoke(thumb);
        }

        private void OnCropsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems == null) return;
            foreach (ImageCropRegion removedReg in e.OldItems)
                ImageProcessingNodeControl.DetachCropPolygon(removedReg, ImageAreaGrid);
        }

        private void UpdateColorCropButtonBackground()
        {
            // ColorCropButton đã được ẩn đi, không cần cập nhật UI background
        }

        private void ColorCropButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void CropToolButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void CropThumbBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ImageCropRegion reg)
            {
                _onCropClickForIp?.Invoke(reg);
                e.Handled = true;
            }
        }

        private void RemoveCropItem(ImageCropRegion reg)
        {
            reg.RenderedImages.Clear();
            reg.Thumbnail = null;

            if (ImageProcessingNodeControl._currentCropRegionForIp.TryGetValue(_node, out var ipSel) &&
                ReferenceEquals(ipSel, reg))
            {
                ImageProcessingNodeControl._currentCropRegionForIp[_node] = null;
                _setIpImage?.Invoke(null);
            }

            ImageProcessingNodeControl.DetachCropPolygon(reg, ImageAreaGrid);

            _node.Crops.Remove(reg);
            if (ImageProcessingNodeControl._activeCropRegion.TryGetValue(_node, out var active) &&
                ReferenceEquals(active, reg))
            {
                ImageProcessingNodeControl._activeCropRegion[_node] = null;
            }
        }

        private void DeleteCropButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button b && b.DataContext is ImageCropRegion reg)
            {
                RemoveCropItem(reg);
                e.Handled = true;
            }
        }

        private void DeleteCropButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is ImageCropRegion reg)
            {
                RemoveCropItem(reg);
                e.Handled = true;
            }
        }
    }
}
