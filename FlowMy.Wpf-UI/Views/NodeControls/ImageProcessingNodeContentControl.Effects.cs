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
using System.Threading;
using System.Threading.Tasks;

namespace FlowMy.Views.NodeControls
{
    public partial class ImageProcessingNodeContentControl
    {
        private record FxParamDef(string Name, double Default, double Min, double Max, double Step = 1);

        private static readonly Dictionary<string, FxParamDef[]> _fxParamMap = new()
        {
            // Blur
            ["GaussianBlur"] = new[] { new FxParamDef("Radius", 3, 0, 30), new FxParamDef("Sigma", 1.5, 0.1, 15, 0.1) },
            ["MotionBlur"] = new[] { new FxParamDef("Radius", 8, 0, 40), new FxParamDef("Sigma", 4, 0.1, 20, 0.1), new FxParamDef("Angle", 0, -180, 180) },
            ["RadialBlur"] = new[] { new FxParamDef("Angle", 5, 0, 45) },
            ["AdaptiveBlur"] = new[] { new FxParamDef("Radius", 0, 0, 20), new FxParamDef("Sigma", 1, 0.1, 10, 0.1) },
            ["Blur"] = new[] { new FxParamDef("Radius", 5, 0, 30), new FxParamDef("Sigma", 2, 0.1, 15, 0.1) },
            // Sharpen
            ["Sharpen"] = new[] { new FxParamDef("Radius", 0, 0, 20), new FxParamDef("Sigma", 1, 0.1, 10, 0.1) },
            ["UnsharpMask"] = new[] { new FxParamDef("Radius", 2, 0, 20), new FxParamDef("Sigma", 1, 0.1, 10, 0.1), new FxParamDef("Amount", 1, 0, 5, 0.1), new FxParamDef("Threshold", 0.05, 0, 1, 0.01) },
            ["AdaptiveSharpen"] = new[] { new FxParamDef("Radius", 0, 0, 20), new FxParamDef("Sigma", 1, 0.1, 10, 0.1) },
            ["Kuwahara"] = new[] { new FxParamDef("Radius", 3, 1, 15), new FxParamDef("Sigma", 1, 0.1, 10, 0.1) },
            // Artistic
            ["OilPaint"] = new[] { new FxParamDef("Radius", 4, 1, 20), new FxParamDef("Sigma", 1, 0.1, 10, 0.1) },
            ["Charcoal"] = new[] { new FxParamDef("Radius", 2, 0, 10), new FxParamDef("Sigma", 1, 0.1, 5, 0.1) },
            ["Sketch"] = new[] { new FxParamDef("Radius", 2, 0, 10), new FxParamDef("Sigma", 1, 0.1, 5, 0.1), new FxParamDef("Angle", 0, -180, 180) },
            ["Emboss"] = new[] { new FxParamDef("Radius", 0, 0, 10), new FxParamDef("Sigma", 1, 0.1, 5, 0.1) },
            ["Vignette"] = new[] { new FxParamDef("Sigma", 10, 1, 50) },
            ["Swirl"] = new[] { new FxParamDef("Degrees", 60, -360, 360) },
            ["Wave"] = new[] { new FxParamDef("Amplitude", 5, 1, 50), new FxParamDef("Length", 50, 5, 300) },
            ["Spread"] = new[] { new FxParamDef("Radius", 4, 1, 30) },
            ["Implode"] = new[] { new FxParamDef("Amount", 0.3, -1, 1, 0.05) },
            ["Explode"] = new[] { new FxParamDef("Amount", 0.3, 0, 1, 0.05) },
            ["Shade"] = new[] { new FxParamDef("Azimuth", 30, 0, 360), new FxParamDef("Elevation", 30, 0, 90) },
            ["Pixelate"] = new[] { new FxParamDef("BlockSize", 8, 2, 64) },
            ["Polaroid"] = new[] { new FxParamDef("Angle", 0, -30, 30) },
            ["Frame"] = new[] { new FxParamDef("Size", 6, 1, 20) },
            ["Raise"] = new[] { new FxParamDef("Size", 8, 1, 20) },
            // Edge
            ["EdgeDetect"] = new[] { new FxParamDef("Radius", 1, 0, 10) },
            ["CannyEdge"] = new[] { new FxParamDef("Radius", 0, 0, 10), new FxParamDef("Sigma", 1, 0.1, 5, 0.1), new FxParamDef("LowPct", 10, 0, 100), new FxParamDef("HighPct", 30, 0, 100) },
            ["Threshold"] = new[] { new FxParamDef("Percent", 50, 0, 100) },
            ["AdaptiveThreshold"] = new[] { new FxParamDef("Width", 10, 3, 30), new FxParamDef("Height", 10, 3, 30), new FxParamDef("Bias", 0, -10, 10, 0.5) },
            // Color / Brightness
            ["BrightnessUp"] = new[] { new FxParamDef("Brightness", 15, 1, 100) },
            ["BrightnessDown"] = new[] { new FxParamDef("Brightness", 15, 1, 100) },
            ["GammaCorrect"] = new[] { new FxParamDef("Gamma", 1.5, 0.1, 5, 0.1) },
            ["SaturationUp"] = new[] { new FxParamDef("Saturation", 140, 101, 300) },
            ["SaturationDown"] = new[] { new FxParamDef("Saturation", 60, 0, 99) },
            ["Posterize"] = new[] { new FxParamDef("Levels", 4, 2, 20) },
            ["Solarize"] = new[] { new FxParamDef("Threshold", 50, 0, 100) },
            ["SepiaTone"] = new[] { new FxParamDef("Threshold", 80, 0, 100) },
            ["SigmoidalContrastUp"] = new[] { new FxParamDef("Contrast", 3, 0.5, 20, 0.5), new FxParamDef("Midpoint", 50, 0, 100) },
            ["LinearStretch"] = new[] { new FxParamDef("BlackPct", 1, 0, 20, 0.5), new FxParamDef("WhitePct", 1, 0, 20, 0.5) },
            ["BlueShift"] = new[] { new FxParamDef("Factor", 1.5, 0.5, 3, 0.1) },
            ["QuantizeColors"] = new[] { new FxParamDef("Colors", 16, 2, 256) },
            ["ContrastDown"] = new[] { new FxParamDef("Contrast", 15, 1, 50) },
            // Noise
            ["AddNoiseGaussian"] = new[] { new FxParamDef("Attenuate", 1, 0.1, 10, 0.1) },
            ["AddNoiseImpulse"] = new[] { new FxParamDef("Attenuate", 1, 0.1, 10, 0.1) },
            ["MedianFilter"] = new[] { new FxParamDef("Radius", 2, 1, 10) },
            ["ReduceNoise"] = new[] { new FxParamDef("Order", 2, 1, 10) },
            // Morphology
            ["Dilate"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["MorphErode"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["Opening"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["Closing"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["EdgeIn"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["EdgeOut"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["TopHat"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            ["BottomHat"] = new[] { new FxParamDef("Iterations", 1, 1, 10) },
            // Transform
            ["Deskew"] = new[] { new FxParamDef("Threshold", 40, 0, 100) },
            ["Shear"] = new[] { new FxParamDef("X", 15, -45, 45), new FxParamDef("Y", 0, -45, 45) },
            ["Roll"] = new[] { new FxParamDef("X", 50, -200, 200), new FxParamDef("Y", 50, -200, 200) },
            ["Shave"] = new[] { new FxParamDef("Pixels", 10, 1, 100) },
            // SkiaSharp-only effects
            ["SkiaDropShadow"] = new[] { new FxParamDef("OffsetX", 5, -30, 30), new FxParamDef("OffsetY", 5, -30, 30), new FxParamDef("SigmaX", 4, 0.5, 20, 0.5), new FxParamDef("SigmaY", 4, 0.5, 20, 0.5), new FxParamDef("ShadowAlpha", 128, 0, 255) },
            ["SkiaHueRotate"] = new[] { new FxParamDef("Degrees", 90, -180, 180) },
            ["SkiaDilate"] = new[] { new FxParamDef("RadiusX", 2, 1, 20), new FxParamDef("RadiusY", 2, 1, 20) },
            ["SkiaErode"] = new[] { new FxParamDef("RadiusX", 2, 1, 20), new FxParamDef("RadiusY", 2, 1, 20) },
            ["SkiaLighting"] = new[] { new FxParamDef("Azimuth", 225, 0, 360), new FxParamDef("Elevation", 45, 0, 90), new FxParamDef("SpecularExponent", 8, 1, 30), new FxParamDef("SpecularConstant", 0.7, 0.1, 2, 0.1) },
            ["SkiaBlendMode"] = new[] { new FxParamDef("BlendModeIndex", 0, 0, 14), new FxParamDef("OverlayR", 128, 0, 255), new FxParamDef("OverlayG", 128, 0, 255), new FxParamDef("OverlayB", 128, 0, 255), new FxParamDef("OverlayA", 100, 0, 100) },
        };

        /// <summary>Show dark-themed parameter dialog. For SkiaSharp effects, applies real-time preview on slider release.</summary>
        private Dictionary<string, double>? ShowFxParamDialog(string effectName, FxParamDef[] paramDefs)
        {
            bool isRealtime = IsSkiaSharpEffect(effectName);

            // For real-time preview, capture the current layer pixels before dialog opens
            byte[]? realtimeOriginal = null;
            Models.ImageEditor.EditorLayer? realtimeLayer = null;
            int rtW = 0, rtH = 0, rtStride = 0;
            if (isRealtime && _node.EditorDoc != null)
            {
                realtimeLayer = _node.EditorDoc.ActiveLayer;
                if (realtimeLayer != null)
                {
                    rtW = realtimeLayer.Width;
                    rtH = realtimeLayer.Height;
                    rtStride = rtW * 4;
                    realtimeOriginal = new byte[rtStride * rtH];
                    realtimeLayer.Bitmap.CopyPixels(realtimeOriginal, rtStride, 0);
                }
            }

            var win = new Window
            {
                Title = effectName,
                Width = 340,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x0a, 0x0c, 0x10)),
                Foreground = System.Windows.Media.Brushes.White,
                WindowStyle = WindowStyle.ToolWindow,
            };

            var stack = new StackPanel { Margin = new Thickness(14) };

            // Title with realtime indicator
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            titlePanel.Children.Add(new TextBlock
            {
                Text = effectName,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x4f, 0xff, 0xb0)),
            });
            if (isRealtime)
            {
                titlePanel.Children.Add(new TextBlock
                {
                    Text = " ⚡ Live",
                    FontSize = 9,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x4f, 0xff, 0xb0)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    Opacity = 0.7
                });
            }
            stack.Children.Add(titlePanel);

            // Load cached values (last-used) nếu có
            var cachedParams = FlowMy.Utils.FxConfigCache.Get(effectName);

            var sliders = new Dictionary<string, Slider>();

            // Debounce helper for real-time preview
            CancellationTokenSource? previewCts = null;
            async void ApplyRealtimePreview()
            {
                if (!isRealtime || realtimeOriginal == null || realtimeLayer == null) return;
                previewCts?.Cancel();
                previewCts = new CancellationTokenSource();
                var token = previewCts.Token;
                var currentParams = new Dictionary<string, double>();
                foreach (var kv in sliders)
                    currentParams[kv.Key] = kv.Value.Value;

                try
                {
                    var preview = await Task.Run(() => ApplySkiaSharpEffect(
                        realtimeOriginal, rtW, rtH, effectName, currentParams, token), token);
                    if (token.IsCancellationRequested) return;
                    realtimeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, rtW, rtH), preview, rtStride, 0);
                    OnEditorDocumentModified();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Realtime preview error: {ex.Message}");
                }
            }

            foreach (var p in paramDefs)
            {
                var dp = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

                var lbl = new TextBlock
                {
                    Text = p.Name,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x90, 0x96, 0xa8)),
                    FontSize = 10,
                    Width = 80,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(lbl, Dock.Left);
                dp.Children.Add(lbl);

                var valBorder = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x11, 0x13, 0x18)),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(4, 1, 4, 1),
                    MinWidth = 42,
                };
                // Dùng cached value nếu có, nếu không dùng default
                double initialValue = p.Default;
                if (cachedParams != null && cachedParams.TryGetValue(p.Name, out var cached))
                    initialValue = Math.Max(p.Min, Math.Min(p.Max, cached));

                var valText = new TextBlock
                {
                    Text = initialValue.ToString(p.Step < 1 ? "F2" : "F0"),
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x00, 0xcf, 0xff)),
                    FontSize = 9,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                valBorder.Child = valText;
                DockPanel.SetDock(valBorder, Dock.Right);
                dp.Children.Add(valBorder);

                var slider = new Slider
                {
                    Minimum = p.Min,
                    Maximum = p.Max,
                    Value = initialValue,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 6, 0),
                    TickFrequency = p.Step,
                    IsSnapToTickEnabled = p.Step >= 1,
                    SmallChange = p.Step,
                    LargeChange = p.Step * 5,
                };
                var capturedP = p; // capture for closure
                slider.ValueChanged += (_, args) =>
                {
                    valText.Text = args.NewValue.ToString(capturedP.Step < 1 ? "F2" : "F0");
                };
                // Real-time preview: trigger on slider mouse up (release)
                if (isRealtime)
                {
                    slider.PreviewMouseLeftButtonUp += (_, __) => ApplyRealtimePreview();
                    // Also on keyboard arrow key release for accessibility
                    slider.PreviewKeyUp += (_, kargs) =>
                    {
                        if (kargs.Key == Key.Left || kargs.Key == Key.Right ||
                            kargs.Key == Key.Up || kargs.Key == Key.Down)
                            ApplyRealtimePreview();
                    };
                }
                dp.Children.Add(slider);
                sliders[p.Name] = slider;

                stack.Children.Add(dp);
            }

            // Buttons row
            Dictionary<string, double>? result = null;
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var btnApply = new Button
            {
                Content = "✓ Apply",
                Padding = new Thickness(14, 5, 14, 5),
                FontSize = 10,
                Cursor = Cursors.Hand,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x4f, 0xff, 0xb0)),
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x0a, 0x0c, 0x10)),
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(4, 0, 0, 0),
            };
            btnApply.Click += (_, __) =>
            {
                result = new Dictionary<string, double>();
                foreach (var kv in sliders)
                    result[kv.Key] = kv.Value.Value;

                // Lưu vào cache để lần sau mở lên sẽ dùng value này
                FlowMy.Utils.FxConfigCache.Set(effectName, result);

                // For real-time: restore original pixels (caller will re-apply with final params)
                if (isRealtime && realtimeOriginal != null && realtimeLayer != null)
                {
                    realtimeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, rtW, rtH), realtimeOriginal, rtStride, 0);
                    OnEditorDocumentModified();
                }

                win.DialogResult = true;
                win.Close();
            };

            // Cancel button with ControlTemplate to fully override WPF chrome hover
            var btnCancel = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(10, 5, 10, 5),
                FontSize = 10,
                Cursor = Cursors.Hand,
            };
            // Build a proper ControlTemplate so WPF does not paint its own white chrome on hover
            var cancelTemplate = new ControlTemplate(typeof(Button));
            var cancelBorderFactory = new FrameworkElementFactory(typeof(Border));
            cancelBorderFactory.Name = "bd";
            cancelBorderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x25, 0x29, 0x32)));
            cancelBorderFactory.SetValue(Border.BorderBrushProperty, new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x3a, 0x3e, 0x4a)));
            cancelBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            cancelBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            cancelBorderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 5, 10, 5));
            var cancelContentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            cancelContentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cancelContentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cancelBorderFactory.AppendChild(cancelContentFactory);
            cancelTemplate.VisualTree = cancelBorderFactory;
            // Normal foreground
            var cancelNormalFgSetter = new Setter(Button.ForegroundProperty, new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xbb, 0xbb, 0xcc)));
            cancelTemplate.Triggers.Add(new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true,
                Setters =
                {
                    new Setter(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x3a, 0x3e, 0x4a)), "bd"),
                    new Setter(Border.BorderBrushProperty, new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x55, 0x5a, 0x6a)), "bd"),
                    new Setter(Button.ForegroundProperty, System.Windows.Media.Brushes.White),
                }
            });
            btnCancel.Template = cancelTemplate;
            btnCancel.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xbb, 0xbb, 0xcc));
            btnCancel.Click += (_, __) =>
            {
                // Restore original pixels if real-time preview was active
                if (isRealtime && realtimeOriginal != null && realtimeLayer != null)
                {
                    realtimeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, rtW, rtH), realtimeOriginal, rtStride, 0);
                    OnEditorDocumentModified();
                }
                win.DialogResult = false;
                win.Close();
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnApply);
            stack.Children.Add(btnPanel);

            // Also restore on window close via X button
            win.Closing += (_, cargs) =>
            {
                previewCts?.Cancel();
                if (win.DialogResult != true && isRealtime && realtimeOriginal != null && realtimeLayer != null)
                {
                    realtimeLayer.Bitmap.WritePixels(new Int32Rect(0, 0, rtW, rtH), realtimeOriginal, rtStride, 0);
                    OnEditorDocumentModified();
                }
            };

            win.Content = stack;
            win.ShowDialog();
            return result;
        }

        private Dictionary<string, double>? _lastFxParams;

        private async void MagickEffect_Click(object sender, MouseButtonEventArgs e)
        {
            if (_node.EditorDoc == null || _isFxRunning) return;
            CommitBrushDrawingSession();
            var layer = _node.EditorDoc.ActiveLayer;
            if (layer == null || layer.IsLocked || !layer.IsVisible) return;

            layer.OriginalTransformBitmap = null;
            layer.ContentBounds = Rect.Empty;

            if (sender is not Border border || border.Tag is not string effectName)
                return;

            e.Handled = true;

            // Intercept rotation and flip preset effects to perform canvas rotation and layer flips directly
            if (effectName == "Rotate90" || effectName == "Rotate180" || effectName == "Rotate270")
            {
                double angle = effectName switch
                {
                    "Rotate90" => 90,
                    "Rotate180" => 180,
                    "Rotate270" => 270,
                    _ => 0
                };
                if (angle != 0)
                {
                    _node.EditorDoc.RotateCanvas(angle);
                    OnEditorDocumentModified();
                    UpdateTransformOverlayDisplay();
                    return;
                }
            }
            else if (effectName == "Flop" || effectName == "Flip")
            {
                FlipActiveLayerImmediate(horizontal: effectName == "Flop");
                return;
            }

            // Show parameter dialog if effect has configurable params
            Dictionary<string, double>? fxParams = null;
            if (_fxParamMap.TryGetValue(effectName, out var paramDefs))
            {
                fxParams = ShowFxParamDialog(effectName, paramDefs);
                if (fxParams == null) return; // User cancelled
            }
            _lastFxParams = fxParams;

            // Snapshot old pixels for undo (on UI thread) — single copy, reused as source
            int w = layer.Width, h = layer.Height;
            int stride = w * 4;
            var oldPixels = new byte[stride * h];
            layer.Bitmap.CopyPixels(oldPixels, stride, 0);

            // Show loading
            _isFxRunning = true;
            _fxCts = new CancellationTokenSource();
            FxLoadingText.Text = $"Đang xử lý: {effectName}...";
            if (FxLoadingCancelHint != null) FxLoadingCancelHint.Visibility = Visibility.Visible;
            FxLoadingOverlay.Visibility = Visibility.Visible;

            // Listen for ESC
            PreviewKeyDown += FxEscHandler;

            byte[]? newPixels = null;
            try
            {
                var token = _fxCts.Token;

                // SkiaSharp fast path: use SkiaSharp engine for supported effects
                if (IsSkiaSharpEffect(effectName))
                {
                    newPixels = await Task.Run(() => ApplySkiaSharpEffect(oldPixels, w, h, effectName, fxParams, token), token);
                }
                else
                {
                    // Run Magick effect on background thread
                    // oldPixels is used as source directly (no extra copy needed)
                    newPixels = await Task.Run(() =>
                    {
                        token.ThrowIfCancellationRequested();

                        var settings = new ImageMagick.MagickReadSettings
                        {
                            Width = (uint)w,
                            Height = (uint)h,
                            Format = ImageMagick.MagickFormat.Bgra,
                            Depth = 8
                        };
                        using var img = new ImageMagick.MagickImage(oldPixels, settings);

                        token.ThrowIfCancellationRequested();

                        // Apply effect
                        bool isTransformOrWarp = effectName.StartsWith("Rotate") || 
                                                 effectName == "Flop" || 
                                                 effectName == "Flip" || 
                                                 effectName == "Deskew" || 
                                                 effectName == "Trim" || 
                                                 effectName == "AutoOrient" || 
                                                 effectName == "Shear" || 
                                                 effectName == "Roll" || 
                                                 effectName == "Shave" || 
                                                 effectName == "Magnify" || 
                                                 effectName == "Swirl" || 
                                                 effectName == "Wave" || 
                                                 effectName == "Implode" || 
                                                 effectName == "Explode" || 
                                                 effectName == "Polaroid";

                        bool useTiles = !isTransformOrWarp && ImageProcessingOptimization.ShouldUseTileProcessing((int)img.Width, (int)img.Height);
                        ImageMagick.MagickImage resultImg;
                        if (useTiles)
                        {
                            resultImg = ImageProcessingOptimization.ProcessLargeImageInTiles(img, (tile) => {
                                ApplyMagickEffectToImage(tile, effectName, fxParams);
                            }, progress: null, cancellationToken: token).GetAwaiter().GetResult();
                        }
                        else
                        {
                            ApplyMagickEffectToImage(img, effectName, fxParams);
                            resultImg = img;
                        }

                        token.ThrowIfCancellationRequested();

                        // Convert back to raw BGRA
                        resultImg.Alpha(ImageMagick.AlphaOption.Set);
                        int rw = (int)resultImg.Width, rh = (int)resultImg.Height;

                        byte[]? finalBytes = null;
                        // Fast path: sizes match → return raw bytes directly
                        if (rw == w && rh == h)
                        {
                            finalBytes = resultImg.ToByteArray(ImageMagick.MagickFormat.Bgra);
                        }
                        else
                        {
                            // Sizes differ: copy into output matching layer size
                            var resultBytes = resultImg.ToByteArray(ImageMagick.MagickFormat.Bgra);
                            var output = new byte[stride * h];
                            int copyW = Math.Min(w, rw);
                            int copyH = Math.Min(h, rh);
                            int rStride = rw * 4;
                            for (int y = 0; y < copyH; y++)
                            {
                                Buffer.BlockCopy(resultBytes, y * rStride, output, y * stride, copyW * 4);
                            }
                            finalBytes = output;
                        }

                        if (useTiles) resultImg.Dispose();
                        return finalBytes;
                    }, token);
                } // end else (Magick path)
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"Magick effect '{effectName}' cancelled by user");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Magick effect '{effectName}' failed: {ex.Message}");
            }
            finally
            {
                PreviewKeyDown -= FxEscHandler;
                FxLoadingOverlay.Visibility = Visibility.Collapsed;
                _isFxRunning = false;
                _fxCts?.Dispose();
                _fxCts = null;
            }

            if (newPixels == null) return;

            // Apply to layer
            layer.Bitmap.WritePixels(new Int32Rect(0, 0, w, h), newPixels, stride, 0);
            layer.InvalidateThumbnail();

            // Undo command
            var cmd = new Models.ImageEditor.Commands.PixelEditCommand(layer, oldPixels, newPixels);
            _node.EditorDoc.History.Execute(cmd);

            OnEditorDocumentModified();
        }

        /// <summary>Dispatch Magick effect to MagickImage (runs on background thread).</summary>
        private static void ApplyMagickEffectToImage(ImageMagick.MagickImage img, string effectName, Dictionary<string, double>? p = null)
        {
            double P(string key, double def) => p != null && p.TryGetValue(key, out var v) ? v : def;
            int PI(string key, int def) => (int)P(key, def);

            switch (effectName)
            {
                // Blur/Sharpen
                case "GaussianBlur": img.GaussianBlur(P("Radius", 3), P("Sigma", 1.5)); break;
                case "Blur": img.Blur(P("Radius", 5), P("Sigma", 2)); break;
                case "MotionBlur": img.MotionBlur(P("Radius", 8), P("Sigma", 4), P("Angle", 0)); break;
                case "RadialBlur": img.RotationalBlur(P("Angle", 5)); break;
                case "AdaptiveBlur": img.AdaptiveBlur(P("Radius", 0), P("Sigma", 1)); break;
                case "Sharpen": img.Sharpen(P("Radius", 0), P("Sigma", 1)); break;
                case "UnsharpMask": img.UnsharpMask(P("Radius", 2), P("Sigma", 1), P("Amount", 1), P("Threshold", 0.05)); break;
                case "AdaptiveSharpen": img.AdaptiveSharpen(P("Radius", 0), P("Sigma", 1)); break;
                case "Kuwahara": img.Kuwahara(P("Radius", 3), P("Sigma", 1)); break;

                // Artistic
                case "OilPaint": img.OilPaint(P("Radius", 4), P("Sigma", 1)); break;
                case "Charcoal": img.Charcoal(P("Radius", 2), P("Sigma", 1)); break;
                case "Sketch": img.Sketch(P("Radius", 2), P("Sigma", 1), P("Angle", 0)); break;
                case "Emboss": img.Emboss(P("Radius", 0), P("Sigma", 1)); break;
                case "Vignette": img.Vignette(0, P("Sigma", 10), 10, 10); break;
                case "Swirl": img.Swirl(P("Degrees", 60)); break;
                case "Wave": img.Wave(ImageMagick.PixelInterpolateMethod.Bilinear, P("Amplitude", 5), P("Length", 50)); img.Trim(); break;
                case "Spread": img.Spread(P("Radius", 4)); break;
                case "Implode": img.Implode(P("Amount", 0.3), ImageMagick.PixelInterpolateMethod.Bilinear); break;
                case "Shade": img.Shade(P("Azimuth", 30), P("Elevation", 30)); break;
                case "Pixelate":
                    int bs = PI("BlockSize", 8);
                    int pw = (int)img.Width, ph = (int)img.Height;
                    img.Scale((uint)Math.Max(1, pw / bs), (uint)Math.Max(1, ph / bs));
                    img.Sample((uint)pw, (uint)ph);
                    break;
                case "Polaroid": img.Polaroid("FlowMy", P("Angle", 0), ImageMagick.PixelInterpolateMethod.Bilinear); break;
                case "Frame": var fs = PI("Size", 6); img.Frame((uint)fs, (uint)fs, 2, 2); break;
                case "Explode": img.Implode(-P("Amount", 0.3), ImageMagick.PixelInterpolateMethod.Bilinear); break;
                case "Raise": img.Raise(PI("Size", 8)); break;

                // Edge
                case "EdgeDetect": img.Edge(P("Radius", 1)); break;
                case "CannyEdge": img.CannyEdge(P("Radius", 0), P("Sigma", 1), new ImageMagick.Percentage(P("LowPct", 10)), new ImageMagick.Percentage(P("HighPct", 30))); break;
                case "Threshold": img.Threshold(new ImageMagick.Percentage(P("Percent", 50))); break;
                case "AdaptiveThreshold": img.AdaptiveThreshold((uint)PI("Width", 10), (uint)PI("Height", 10), new ImageMagick.Percentage(P("Bias", 0.0))); break;
                case "OrderedDither": img.OrderedDither("o8x8"); break;

                // Color
                case "Posterize": img.Posterize(PI("Levels", 4)); break;
                case "Solarize": img.Solarize(new ImageMagick.Percentage(P("Threshold", 50))); break;
                case "AutoLevel": img.AutoLevel(); break;
                case "AutoGamma": img.AutoGamma(); break;
                case "Equalize": img.Equalize(); break;
                case "Normalize": img.Normalize(); break;
                case "Negate": img.Negate(); break;
                case "SepiaTone": img.SepiaTone(new ImageMagick.Percentage(P("Threshold", 80))); break;
                case "Grayscale": img.Grayscale(); break;
                case "BrightnessUp": img.BrightnessContrast(new ImageMagick.Percentage(P("Brightness", 15)), new ImageMagick.Percentage(0)); break;
                case "BrightnessDown": img.BrightnessContrast(new ImageMagick.Percentage(-P("Brightness", 15)), new ImageMagick.Percentage(0)); break;
                case "GammaCorrect": img.GammaCorrect(P("Gamma", 1.5)); break;
                case "SaturationUp": img.Modulate(new ImageMagick.Percentage(100), new ImageMagick.Percentage(P("Saturation", 140)), new ImageMagick.Percentage(100)); break;
                case "SaturationDown": img.Modulate(new ImageMagick.Percentage(100), new ImageMagick.Percentage(P("Saturation", 60)), new ImageMagick.Percentage(100)); break;
                case "Tint": img.Colorize(new ImageMagick.MagickColor(255, 220, 180), new ImageMagick.Percentage(25)); break;
                case "ContrastUp": img.Contrast(); break;
                case "ContrastDown": img.BrightnessContrast(new ImageMagick.Percentage(0), new ImageMagick.Percentage(-P("Contrast", 15))); break;
                case "BlueShift": img.BlueShift(P("Factor", 1.5)); break;
                case "LinearStretch": img.LinearStretch(new ImageMagick.Percentage(P("BlackPct", 1)), new ImageMagick.Percentage(P("WhitePct", 1))); break;
                case "QuantizeColors": img.Quantize(new ImageMagick.QuantizeSettings { Colors = (uint)PI("Colors", 16) }); break;
                case "SigmoidalContrastUp": img.SigmoidalContrast(P("Contrast", 3.0), new ImageMagick.Percentage(P("Midpoint", 50))); break;

                // Noise
                case "AddNoiseGaussian": img.AddNoise(ImageMagick.NoiseType.Gaussian, P("Attenuate", 1.0)); break;
                case "AddNoiseImpulse": img.AddNoise(ImageMagick.NoiseType.Impulse, P("Attenuate", 1.0)); break;
                case "Denoise": img.Enhance(); break;
                case "Despeckle": img.Despeckle(); break;
                case "MedianFilter": img.MedianFilter((uint)PI("Radius", 2)); break;
                case "ReduceNoise": img.ReduceNoise((uint)PI("Order", 2)); break;

                // Morphology
                case "Dilate": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.Dilate, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "MorphErode": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.Erode, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "Opening": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.Open, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "Closing": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.Close, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "EdgeIn": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.EdgeIn, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "EdgeOut": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.EdgeOut, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "TopHat": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.TopHat, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;
                case "BottomHat": img.Morphology(new ImageMagick.MorphologySettings { Method = ImageMagick.MorphologyMethod.BottomHat, Kernel = ImageMagick.Kernel.Diamond, Iterations = PI("Iterations", 1) }); break;

                // Transform
                case "Deskew": img.Deskew(new ImageMagick.Percentage(P("Threshold", 40))); break;
                case "Trim": img.Trim(); break;
                case "AutoOrient": img.AutoOrient(); break;
                case "Rotate90": img.Rotate(90); break;
                case "Rotate180": img.Rotate(180); break;
                case "Rotate270": img.Rotate(270); break;
                case "Flop": img.Flop(); break;
                case "Flip": img.Flip(); break;
                case "Shear": img.Shear(P("X", 15), P("Y", 0)); break;
                case "Roll": img.Roll(PI("X", 50), PI("Y", 50)); break;
                case "Shave": var sv = PI("Pixels", 10); img.Shave((uint)sv, (uint)sv); break;
                case "Magnify": img.Magnify(); break;
                case "Minify": img.Minify(); break;
            }
        }

        public class FxToolItem
        {
            public string Name { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string IconKey { get; set; } = string.Empty;
            public string TextIcon { get; set; } = string.Empty;
            /// <summary>True if this effect uses SkiaSharp (real-time capable).</summary>
            public bool IsSkia { get; set; }

            public string ToolTipText => IsSkia ? $"⚡ {DisplayName} ({Description}) [SkiaSharp]" : $"{DisplayName} ({Description})";

            public System.Windows.Visibility SvgVisibility => string.IsNullOrEmpty(TextIcon) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            public System.Windows.Visibility TextVisibility => string.IsNullOrEmpty(TextIcon) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            public System.Windows.Visibility SkiaBadgeVisibility => IsSkia ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private Border? _activeGroupBorder;

        private void GroupToolBtn_RightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Chặn nổi bọt lên parent để không mở ImageProcessingNodeDialog

            if (sender is not Border border) return;

            // Nếu popup đang mở cho chính nút này, đóng nó lại (tạo hiệu ứng toggle)
            if (FxGroupPopup.IsOpen && FxGroupPopup.PlacementTarget == border)
            {
                FxGroupPopup.IsOpen = false;
                return;
            }

            // Bỏ highlight nút đang active khác trước khi đổi sang nút mới
            if (_activeGroupBorder != null && _activeGroupBorder != border)
            {
                _activeGroupBorder.Background = System.Windows.Media.Brushes.Transparent;
                _activeGroupBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
            }

            _activeGroupBorder = border;

            // Lấy danh sách hiệu ứng thuộc nhóm tương ứng
            var list = GetFxGroupItems(border.Name);
            if (list == null || list.Count == 0) return;

            FxPopupItemsControl.ItemsSource = list;

            // Định vị và hiển thị Popup
            FxGroupPopup.PlacementTarget = border;
            FxGroupPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
            FxGroupPopup.HorizontalOffset = 6;
            FxGroupPopup.VerticalOffset = -4;

            // Highlight nút này với viền xanh ngọc nhạt và nền xám trắng mờ (Active state)
            border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            border.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4fffb0"));

            if (FxGroupPopup.Child is FrameworkElement childBorder)
            {
                childBorder.LayoutTransform = EditorToolbox.LayoutTransform ?? Transform.Identity;
            }
            FxGroupPopup.IsOpen = true;
        }

        private void FxGroupPopup_Closed(object? sender, EventArgs e)
        {
            if (_activeGroupBorder != null)
            {
                // Reset style về mặc định (Transparent)
                _activeGroupBorder.Background = System.Windows.Media.Brushes.Transparent;
                _activeGroupBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
                _activeGroupBorder = null;
            }
        }

        private List<FxToolItem> GetFxGroupItems(string borderName)
        {
            var items = new List<FxToolItem>();
            switch (borderName)
            {
                case "TbxBlurActive":
                    items.Add(new FxToolItem { Name = "GaussianBlur", DisplayName = "Gaussian Blur", Description = "Làm mịn ảnh, giảm nhiễu hạt độ chi tiết cao", IconKey = "droplet-degree duotone", IsSkia = true });
                    items.Add(new FxToolItem { Name = "Blur", DisplayName = "Standard Blur", Description = "Làm mờ ảnh cơ bản nhanh chóng", IconKey = "droplet-slash duotone", IsSkia = true });
                    items.Add(new FxToolItem { Name = "MotionBlur", DisplayName = "Motion Blur", Description = "Làm mờ chuyển động theo một góc nhất định", IconKey = "wind duotone" });
                    items.Add(new FxToolItem { Name = "RadialBlur", DisplayName = "Radial Blur", Description = "Làm mờ xoay quanh tâm ảnh", IconKey = "circle-notch duotone" });
                    items.Add(new FxToolItem { Name = "AdaptiveBlur", DisplayName = "Adaptive Blur", Description = "Làm mờ bảo toàn các đường biên sắc nét", IconKey = "cloud duotone" });
                    items.Add(new FxToolItem { Name = "Sharpen", DisplayName = "Sharpen", Description = "Tăng cường độ sắc nét cho ảnh", IconKey = "diamond duotone", IsSkia = true });
                    items.Add(new FxToolItem { Name = "UnsharpMask", DisplayName = "Unsharp Mask", Description = "Lọc sắc nét nâng cao có kiểm soát", IconKey = "gem duotone", IsSkia = true });
                    items.Add(new FxToolItem { Name = "AdaptiveSharpen", DisplayName = "Adaptive Sharpen", Description = "Tăng sắc nét bảo toàn chi tiết phẳng", IconKey = "bolt duotone" });
                    items.Add(new FxToolItem { Name = "Kuwahara", DisplayName = "Kuwahara Filter", Description = "Lọc nghệ thuật Kuwahara làm mịn ảnh giữ cạnh", IconKey = "aperture duotone" });
                    break;

                case "TbxArtActive":
                    items.Add(new FxToolItem { Name = "OilPaint", DisplayName = "Oil Paint", Description = "Hiệu ứng tranh sơn dầu nghệ thuật", IconKey = "brush duotone" });
                    items.Add(new FxToolItem { Name = "Charcoal", DisplayName = "Charcoal Sketch", Description = "Hiệu ứng vẽ than chì đen trắng nghệ thuật", IconKey = "pen duotone" });
                    items.Add(new FxToolItem { Name = "Sketch", DisplayName = "Pencil Sketch", Description = "Hiệu ứng phác hoạ chì nghệ thuật", IconKey = "pen-fancy duotone" });
                    items.Add(new FxToolItem { Name = "Emboss", DisplayName = "Emboss (Chạm nổi)", Description = "Tạo hiệu ứng bề mặt 3D chạm khắc nổi", IconKey = "palette duotone" });
                    items.Add(new FxToolItem { Name = "Vignette", DisplayName = "Vignette", Description = "Tạo viền mờ tối nghệ thuật bốn góc", IconKey = "circle duotone" });
                    items.Add(new FxToolItem { Name = "Swirl", DisplayName = "Swirl Warp", Description = "Xoắn vặn xoáy tròn tâm hình ảnh", IconKey = "arrows-spin duotone" });
                    items.Add(new FxToolItem { Name = "Wave", DisplayName = "Wave Distortion", Description = "Làm ảnh lượn sóng uốn éo nghệ thuật", IconKey = "water duotone" });
                    items.Add(new FxToolItem { Name = "Spread", DisplayName = "Spread Noise", Description = "Làm phân tán xáo trộn ngẫu nhiên pixel", IconKey = "cube duotone" });
                    items.Add(new FxToolItem { Name = "Implode", DisplayName = "Implode (Hút)", Description = "Hiệu ứng hút lõm hình ảnh vào tâm", IconKey = "down-left-and-up-right-to-center duotone" });
                    items.Add(new FxToolItem { Name = "Explode", DisplayName = "Explode (Phồng)", Description = "Hiệu ứng thổi phồng lồi ảnh ra ngoài", IconKey = "expand duotone" });
                    items.Add(new FxToolItem { Name = "Shade", DisplayName = "3D Shade", Description = "Chiếu sáng bề mặt tạo bóng đổ 3D", IconKey = "lightbulb duotone" });
                    items.Add(new FxToolItem { Name = "Pixelate", DisplayName = "Pixelate (Mosaic)", Description = "Hiệu ứng ô vuông hoá/che mờ pixel", IconKey = "cubes duotone" });
                    items.Add(new FxToolItem { Name = "Polaroid", DisplayName = "Polaroid Frame", Description = "Tạo viền khung ảnh giấy chụp lấy liền", IconKey = "image duotone" });
                    items.Add(new FxToolItem { Name = "Frame", DisplayName = "Decorative Frame", Description = "Thêm khung viền gỗ nổi trang trí quanh ảnh", IconKey = "border-all duotone" });
                    items.Add(new FxToolItem { Name = "Raise", DisplayName = "Raised Edge", Description = "Tạo viền vát nổi 3D xung quanh ảnh", IconKey = "border-top-left duotone" });
                    break;

                case "TbxEdgeActive":
                    items.Add(new FxToolItem { Name = "EdgeDetect", DisplayName = "Edge Detect", Description = "Phát hiện các đường biên, nét vẽ trong ảnh", IconKey = "bezier-curve duotone" });
                    items.Add(new FxToolItem { Name = "CannyEdge", DisplayName = "Canny Edge", Description = "Phát hiện biên thuật toán Canny cao cấp", IconKey = "vector-square duotone" });
                    items.Add(new FxToolItem { Name = "Threshold", DisplayName = "Threshold", Description = "Phân ngưỡng đen trắng nhị phân tuyệt đối", IconKey = "square duotone" });
                    items.Add(new FxToolItem { Name = "AdaptiveThreshold", DisplayName = "Adaptive Threshold", Description = "Phân ngưỡng thích ứng cục bộ theo vùng", IconKey = "table-cells duotone" });
                    items.Add(new FxToolItem { Name = "OrderedDither", DisplayName = "Ordered Dither", Description = "Giả lập tram sắc độ bằng lưới hạt chấm đen trắng", IconKey = "grid duotone" });
                    break;

                case "TbxColorActive":
                    items.Add(new FxToolItem { Name = "Posterize", DisplayName = "Posterize", Description = "Giảm số lượng màu sắc tạo hiệu ứng tranh cổ động", IconKey = "palette duotone" });
                    items.Add(new FxToolItem { Name = "Solarize", DisplayName = "Solarize", Description = "Đảo ngược màu các vùng sáng vượt ngưỡng", IconKey = "sun duotone" });
                    items.Add(new FxToolItem { Name = "AutoLevel", DisplayName = "Auto Level", Description = "Tự động cân bằng histogram tăng tương phản", IconKey = "sliders duotone" });
                    items.Add(new FxToolItem { Name = "AutoGamma", DisplayName = "Auto Gamma", Description = "Tự động sửa chữa gamma cân bằng sáng", IconKey = "bolt duotone" });
                    items.Add(new FxToolItem { Name = "Equalize", DisplayName = "Equalize", Description = "San bằng lược đồ màu sắc phân bổ đều tương phản", IconKey = "chart-simple duotone" });
                    items.Add(new FxToolItem { Name = "Normalize", DisplayName = "Normalize Contrast", Description = "Chuẩn hoá độ tương phản kéo rộng dải màu", IconKey = "align-justify duotone" });
                    items.Add(new FxToolItem { Name = "Negate", DisplayName = "Invert Color", Description = "Đảo ngược tất cả màu sắc (Negative)", IconKey = "circle-half-stroke duotone" });
                    items.Add(new FxToolItem { Name = "SepiaTone", DisplayName = "Sepia Tone", Description = "Tông màu hoài cổ nâu đỏ ấm áp", IconKey = "heart duotone" });
                    items.Add(new FxToolItem { Name = "Grayscale", DisplayName = "Grayscale", Description = "Chuyển ảnh sang trắng đen hoàn toàn", IconKey = "droplet-slash duotone" });
                    items.Add(new FxToolItem { Name = "BrightnessUp", DisplayName = "Brightness Up", Description = "Tăng độ sáng của ảnh lên 15%", IconKey = "sun duotone" });
                    items.Add(new FxToolItem { Name = "BrightnessDown", DisplayName = "Brightness Down", Description = "Giảm độ sáng của ảnh đi 15%", IconKey = "moon duotone" });
                    items.Add(new FxToolItem { Name = "GammaCorrect", DisplayName = "Gamma Correction", Description = "Điều chỉnh gamma cải thiện độ sâu màu", IconKey = "sliders duotone" });
                    items.Add(new FxToolItem { Name = "SaturationUp", DisplayName = "Saturation Up", Description = "Tăng độ rực rỡ tươi tắn của màu sắc", IconKey = "wand-magic-sparkles duotone" });
                    items.Add(new FxToolItem { Name = "SaturationDown", DisplayName = "Saturation Down", Description = "Giảm độ rực, làm bạc nhạt màu sắc", IconKey = "trash duotone" });
                    items.Add(new FxToolItem { Name = "Tint", DisplayName = "Warm Tint", Description = "Áp bộ lọc màu ấm áp kiểu cổ điển", IconKey = "mug-hot duotone" });
                    items.Add(new FxToolItem { Name = "ContrastUp", DisplayName = "Contrast Up", Description = "Tăng độ tương phản sáng tối cơ bản", IconKey = "arrows-maximize duotone" });
                    items.Add(new FxToolItem { Name = "ContrastDown", DisplayName = "Contrast Down", Description = "Giảm tương phản làm dịu nhẹ ảnh", IconKey = "arrows-minimize duotone" });
                    items.Add(new FxToolItem { Name = "BlueShift", DisplayName = "Blue Shift (Cooling)", Description = "Áp bộ lọc màu xanh lạnh kiểu đêm tối", IconKey = "snowflake duotone" });
                    items.Add(new FxToolItem { Name = "LinearStretch", DisplayName = "Linear Stretch", Description = "Kéo giãn tuyến tính tăng cường dải màu trắng đen", IconKey = "arrows-left-right duotone" });
                    items.Add(new FxToolItem { Name = "QuantizeColors", DisplayName = "Color Quantize", Description = "Giới hạn bảng màu của ảnh về số lượng màu cố định", IconKey = "table-cells duotone" });
                    items.Add(new FxToolItem { Name = "SigmoidalContrastUp", DisplayName = "Sigmoidal Contrast", Description = "Tăng tương phản đường cong chữ S tự nhiên bảo vệ highlight", IconKey = "bezier-curve duotone" });

                    // ── SkiaSharp-only color effects ──
                    items.Add(new FxToolItem { Name = "SkiaHueRotate", DisplayName = "⚡ Hue Rotate", Description = "Xoay tông màu HSL siêu nhanh (SkiaSharp)", IconKey = "ring-diamond duotone", IsSkia = true });
                    items.Add(new FxToolItem { Name = "SkiaColorMatrix", DisplayName = "⚡ Color Matrix", Description = "Bộ lọc màu tùy chỉnh 5x4 matrix (SkiaSharp)", IconKey = "table-cells-lock duotone", IsSkia = true });
                    items.Add(new FxToolItem { Name = "SkiaBlendMode", DisplayName = "⚡ Blend Mode", Description = "Overlay màu với chế độ hoà trộn chuyên nghiệp (SkiaSharp)", IconKey = "layer-group duotone", IsSkia = true });
                    break;

                case "TbxNoiseActive":
                    items.Add(new FxToolItem { Name = "AddNoiseGaussian", DisplayName = "Noise (Gaussian)", Description = "Thêm nhiễu ngẫu nhiên Gauss hạt mịn", IconKey = "signal-stream duotone" });
                    items.Add(new FxToolItem { Name = "AddNoiseImpulse", DisplayName = "Noise (Impulse)", Description = "Thêm nhiễu muối tiêu (Impulse)", IconKey = "circle-radiation duotone" });
                    items.Add(new FxToolItem { Name = "Denoise", DisplayName = "Denoise (Enhance)", Description = "Lọc mịn giảm nhiễu hạt cơ bản", IconKey = "broom duotone" });
                    items.Add(new FxToolItem { Name = "Despeckle", DisplayName = "Despeckle", Description = "Khử nhiễu đốm đốm nâng cao", IconKey = "wand duotone" });
                    items.Add(new FxToolItem { Name = "MedianFilter", DisplayName = "Median Filter", Description = "Bộ lọc trung vị khử nhiễu muối tiêu", IconKey = "shield duotone" });
                    items.Add(new FxToolItem { Name = "ReduceNoise", DisplayName = "Reduce Noise", Description = "Giảm nhiễu bảo toàn cấu trúc cạnh", IconKey = "wand-magic-sparkles duotone" });
                    break;

                case "TbxMorphActive":
                    items.Add(new FxToolItem { Name = "Dilate", DisplayName = "Dilate (Phình)", Description = "Giãn nở vùng sáng của ảnh", IconKey = "circle-nodes duotone" });
                    items.Add(new FxToolItem { Name = "MorphErode", DisplayName = "Erode (Co)", Description = "Thu hẹp vùng sáng của ảnh", IconKey = "circle-dot duotone" });
                    items.Add(new FxToolItem { Name = "Opening", DisplayName = "Opening", Description = "Co trước giãn sau (xoá nhiễu sáng nhỏ)", IconKey = "atom duotone" });
                    items.Add(new FxToolItem { Name = "Closing", DisplayName = "Closing", Description = "Giãn trước co sau (lấp lỗ trống tối nhỏ)", IconKey = "fingerprint duotone" });
                    items.Add(new FxToolItem { Name = "EdgeIn", DisplayName = "Edge In", Description = "Phát hiện biên trong vùng đối tượng", IconKey = "diamond-half duotone" });
                    items.Add(new FxToolItem { Name = "EdgeOut", DisplayName = "Edge Out", Description = "Phát hiện biên ngoài vùng đối tượng", IconKey = "diamond-half-stroke duotone" });
                    items.Add(new FxToolItem { Name = "TopHat", DisplayName = "Top Hat", Description = "Chiết xuất các chi tiết sáng nhỏ trên nền tối", IconKey = "sparkle duotone" });
                    items.Add(new FxToolItem { Name = "BottomHat", DisplayName = "Bottom Hat", Description = "Chiết xuất các lỗ/chi tiết tối trên nền sáng", IconKey = "shapes duotone" });
                    // ── SkiaSharp fast morphology ──
                    items.Add(new FxToolItem { Name = "SkiaDilate", DisplayName = "⚡ Dilate (SkiaSharp)", Description = "Giãn nở siêu nhanh bằng SkiaSharp", IconKey = "hexagon-plus duotone", IsSkia = true });
                    items.Add(new FxToolItem { Name = "SkiaErode", DisplayName = "⚡ Erode (SkiaSharp)", Description = "Co rút siêu nhanh bằng SkiaSharp", IconKey = "hexagon-minus duotone", IsSkia = true });
                    break;

                case "TbxXFormActive":
                    items.Add(new FxToolItem { Name = "Deskew", DisplayName = "Deskew", Description = "Tự động chỉnh ảnh bị nghiêng thẳng lại", IconKey = "clock-rotate-left duotone" });
                    items.Add(new FxToolItem { Name = "Trim", DisplayName = "Trim", Description = "Tự động xén các vùng viền thừa", IconKey = "scanner-image duotone" });
                    items.Add(new FxToolItem { Name = "AutoOrient", DisplayName = "Auto Orient", Description = "Tự động xoay ảnh theo EXIF orientation", IconKey = "compass duotone" });
                    items.Add(new FxToolItem { Name = "Rotate90", DisplayName = "Rotate 90°", Description = "Xoay ảnh 90 độ theo chiều kim đồng hồ", IconKey = "rotate duotone", TextIcon = "90°" });
                    items.Add(new FxToolItem { Name = "Rotate180", DisplayName = "Rotate 180°", Description = "Xoay ảnh ngược đầu 180 độ", IconKey = "arrows-repeat duotone", TextIcon = "180°" });
                    items.Add(new FxToolItem { Name = "Rotate270", DisplayName = "Rotate 270°", Description = "Xoay ảnh 270 độ", IconKey = "arrows-spin duotone", TextIcon = "270°" });
                    items.Add(new FxToolItem { Name = "Flop", DisplayName = "Horizontal Flip", Description = "Lật ảnh đối xứng ngang", IconKey = "arrows-left-right duotone" });
                    items.Add(new FxToolItem { Name = "Flip", DisplayName = "Vertical Flip", Description = "Lật ảnh đối xứng dọc", IconKey = "arrows-up-down duotone" });
                    items.Add(new FxToolItem { Name = "Shear", DisplayName = "Shear (15°)", Description = "Nghiêng xiên hình ảnh góc 15 độ", IconKey = "angles-right duotone" });
                    items.Add(new FxToolItem { Name = "Roll", DisplayName = "Roll Offset (50px)", Description = "Dịch cuộn tuần hoàn ảnh 50px sang đối diện", IconKey = "computer-mouse-scrollwheel duotone" });
                    items.Add(new FxToolItem { Name = "Shave", DisplayName = "Shave Margins (10px)", Description = "Xén bớt một dải 10px viền ngoài", IconKey = "eraser duotone" });
                    items.Add(new FxToolItem { Name = "Magnify", DisplayName = "Magnify (Zoom x2)", Description = "Phóng đại kích thước ảnh lên gấp đôi chất lượng cao", IconKey = "magnifying-glass-plus duotone" });
                    items.Add(new FxToolItem { Name = "Minify", DisplayName = "Minify (Shrink /2)", Description = "Thu nhỏ kích thước ảnh đi một nửa sắc nét", IconKey = "magnifying-glass-minus duotone" });
                    break;
            }
            return items;
        }

        private void FxPopupItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is FxToolItem selectedItem)
            {
                if (_activeGroupBorder != null)
                {
                    // 1. Cập nhật Tag của nút cha (chứa tên effect)
                    _activeGroupBorder.Tag = selectedItem.Name;

                    // 2. Cập nhật ToolTip (bao gồm mô tả tiếng Việt trong ngoặc)
                    _activeGroupBorder.ToolTip = selectedItem.ToolTipText;

                    UpdateConfigDotVisibility(_activeGroupBorder, selectedItem.Name);

                    // 3. Cập nhật icon của nút cha
                    if (_activeGroupBorder.Child is Grid grid)
                    {
                        var svg = grid.Children[0] as SvgViewboxEx;
                        var txt = grid.Children[1] as TextBlock;

                        if (svg != null && txt != null)
                        {
                            if (!string.IsNullOrEmpty(selectedItem.TextIcon))
                            {
                                txt.Text = selectedItem.TextIcon;
                                txt.Visibility = System.Windows.Visibility.Visible;
                                svg.Visibility = System.Windows.Visibility.Collapsed;
                            }
                            else
                            {
                                var converter = new IconKeyToPathConverter();
                                svg.Source = (Uri)converter.Convert(null, typeof(Uri), selectedItem.IconKey, null);
                                svg.Visibility = System.Windows.Visibility.Visible;
                                txt.Visibility = System.Windows.Visibility.Collapsed;
                            }
                        }
                    }
                }

                // Đóng Popup
                FxGroupPopup.IsOpen = false;
                e.Handled = true;
            }
        }

        private void InitializeFxDots()
        {
            var fxBorders = new[] { TbxBlurActive, TbxArtActive, TbxEdgeActive, TbxColorActive, TbxNoiseActive, TbxMorphActive, TbxXFormActive };
            foreach (var border in fxBorders)
            {
                if (border != null && border.Tag is string effectName)
                {
                    UpdateConfigDotVisibility(border, effectName);
                }
            }
        }

        private void UpdateConfigDotVisibility(Border border, string effectName)
        {
            if (border == null) return;
            if (border.Child is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is System.Windows.Shapes.Ellipse el && el.Name != null && el.Name.EndsWith("_ConfigDot"))
                    {
                        bool hasConfig = _fxParamMap.ContainsKey(effectName);
                        el.Visibility = hasConfig ? Visibility.Visible : Visibility.Collapsed;
                        break;
                    }
                }
            }
        }

        private void FxEscHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _fxCts != null)
            {
                _fxCts.Cancel();
                e.Handled = true;
            }
        }

        private void UpdateTopOptionsBar(string activeTool)
        {
            if (TopOptionsBar == null) return;

            bool hasOptions = (activeTool == "Brush" || activeTool == "Eraser" || activeTool == "Text" ||
                               activeTool == "Selection" || activeTool == "Lasso" || activeTool == "PolyLasso" ||
                               activeTool == "Eyedropper" || activeTool == "MagicWand" || activeTool == "QuickSelection" ||
                               activeTool == "ObjectSelection" || activeTool == "CropCanvas" || activeTool == "Slice" ||
                               activeTool == "SliceSelect" || activeTool == "Move" || activeTool == "Transform");

            if (_node.ProcessingMode == Models.Nodes.ImageProcessingMode.Manual && hasOptions)
            {
                TopOptionsBar.Visibility = Visibility.Visible;
                OptBrushPanel.Visibility = (activeTool == "Brush" || activeTool == "Eraser" || activeTool == "QuickSelection") ? Visibility.Visible : Visibility.Collapsed;
                OptTextPanel.Visibility = (activeTool == "Text") ? Visibility.Visible : Visibility.Collapsed;
                OptSelectionPanel.Visibility = (activeTool == "Selection" || activeTool == "Lasso" || activeTool == "PolyLasso" ||
                                                activeTool == "MagicWand" || activeTool == "QuickSelection" || activeTool == "ObjectSelection") ? Visibility.Visible : Visibility.Collapsed;
                if (OptSelectionPanel.Visibility == Visibility.Visible)
                {
                    UpdateSelModeVisuals();
                }

                OptCropPanel.Visibility = (activeTool == "CropCanvas") ? Visibility.Visible : Visibility.Collapsed;
                if (activeTool == "CropCanvas")
                {
                    StartCropMode();
                }
                else if (CropOverlayCanvas != null)
                {
                    CropOverlayCanvas.Visibility = Visibility.Collapsed;
                }

                if (OptMovePanel != null)
                {
                    OptMovePanel.Visibility = (activeTool == "Move") ? Visibility.Visible : Visibility.Collapsed;
                    if (activeTool != "Move")
                    {
                        CommitPendingMoveTranslation();
                    }
                }

                if (OptTransformPanel != null)
                {
                    OptTransformPanel.Visibility = (activeTool == "Transform") ? Visibility.Visible : Visibility.Collapsed;
                    if (activeTool == "Transform")
                    {
                        UpdateTransformOverlayDisplay();
                    }
                    else
                    {
                        if (_transformSessionActive)
                        {
                            CancelTransformSession();
                        }
                        if (TransformOverlayCanvas != null) TransformOverlayCanvas.Visibility = Visibility.Collapsed;
                        if (TransformPreviewImage != null) TransformPreviewImage.Visibility = Visibility.Collapsed;
                    }
                }

                OptSlicePanel.Visibility = (activeTool == "Slice" || activeTool == "SliceSelect") ? Visibility.Visible : Visibility.Collapsed;

                if (SlicesCanvas != null)
                {
                    bool showSlices = (activeTool == "Slice" || activeTool == "SliceSelect");
                    SlicesCanvas.Visibility = (showSlices && _slices.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
                    if (showSlices) UpdateSlicesDisplay();
                }

                if (OptColorPanel != null)
                {
                    OptColorPanel.Visibility = (activeTool == "Brush" || activeTool == "Eyedropper") ? Visibility.Visible : Visibility.Collapsed;
                }

                // Sync initial brush properties
                if (activeTool == "Brush" || activeTool == "Eraser")
                {
                    SyncFromEditorPanelBrushProperties();
                }

                if (activeTool == "Brush" || activeTool == "Eyedropper")
                {
                    SyncToolboxColors();
                }

                if (activeTool == "Text")
                {
                    if (OptTextSize != null && OptTextSize.Value != EditorPanel.TextFontSize)
                        OptTextSize.Value = EditorPanel.TextFontSize;
                    if (OptTextColorSwatch != null)
                        OptTextColorSwatch.Background = new SolidColorBrush(EditorPanel.TextColor);
                    if (OptBtnTextColor != null)
                        OptBtnTextColor.Text = $"#{EditorPanel.TextColor.R:X2}{EditorPanel.TextColor.G:X2}{EditorPanel.TextColor.B:X2}";
                }
            }
            else
            {
                TopOptionsBar.Visibility = Visibility.Collapsed;
            }
        }
    }
}
