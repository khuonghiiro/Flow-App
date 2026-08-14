// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;

namespace FlowMy.Services.Rendering
{
    public class NodeAppearanceStyle
    {
        public Brush Background { get; set; } = Brushes.Gray;
        public Brush BorderBrush { get; set; } = Brushes.White;
        public Thickness BorderThickness { get; set; } = new Thickness(2);
        public CornerRadius CornerRadius { get; set; } = new CornerRadius(10);
        public Effect? Effect { get; set; }
        public Brush TextColor { get; set; } = Brushes.White;
        public Effect? TextEffect { get; set; }
    }

    public static class NodeAppearanceHelper
    {
        public const string ModeSolid = "Solid";
        public const string ModeLiquidGlass = "LiquidGlass";
        public const string ModeModernGradient = "ModernGradient";

        public static string GetNormalizedMode(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return ModeSolid;
            var trimmed = mode.Trim();
            if (string.Equals(trimmed, ModeLiquidGlass, StringComparison.OrdinalIgnoreCase)) return ModeLiquidGlass;
            if (string.Equals(trimmed, ModeModernGradient, StringComparison.OrdinalIgnoreCase)) return ModeModernGradient;
            return ModeSolid;
        }

        public static NodeAppearanceStyle GetStyle(string? mode, Color baseColor)
        {
            var norm = GetNormalizedMode(mode);
            var style = new NodeAppearanceStyle();
            var isLight = LiquidGlassHelper.IsLightColor(baseColor);

            switch (norm.ToLowerInvariant())
            {
                case "liquidglass":
                    style.Background = LiquidGlassHelper.CreateGlassBackground(baseColor);
                    style.BorderBrush = isLight
                        ? new SolidColorBrush(Color.FromArgb(140, 90, 90, 90))
                        : LiquidGlassHelper.CreateGlassBorderBrush();
                    style.BorderThickness = new Thickness(1.2);
                    style.Effect = LiquidGlassHelper.CreateGlassEffect(baseColor, isLight);
                    style.TextColor = isLight ? new SolidColorBrush(Color.FromRgb(20, 20, 20)) : new SolidColorBrush(Colors.White);
                    style.TextEffect = new DropShadowEffect { Color = isLight ? Colors.White : Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = 0.5 };
                    style.CornerRadius = new CornerRadius(10);
                    break;

                case "moderngradient":
                    Color lighterBase = Color.FromRgb(
                        (byte)Math.Min(255, baseColor.R + 25),
                        (byte)Math.Min(255, baseColor.G + 25),
                        (byte)Math.Min(255, baseColor.B + 25));
                    Color darkerColor = Color.FromRgb(
                        (byte)Math.Max(0, baseColor.R - 55),
                        (byte)Math.Max(0, baseColor.G - 55),
                        (byte)Math.Max(0, baseColor.B - 55));
                    style.Background = new LinearGradientBrush(lighterBase, darkerColor, 90.0);
                    style.BorderBrush = new LinearGradientBrush(
                        Color.FromArgb(200, 255, 255, 255),
                        Color.FromArgb(60, 255, 255, 255),
                        90.0);
                    style.BorderThickness = new Thickness(1.5);
                    style.Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 14,
                        ShadowDepth = 4,
                        Opacity = 0.45,
                        Direction = 270
                    };
                    style.TextColor = isLight ? new SolidColorBrush(Color.FromRgb(20, 20, 20)) : new SolidColorBrush(Colors.White);
                    style.TextEffect = new DropShadowEffect
                    {
                        Color = isLight ? Color.FromArgb(180, 255, 255, 255) : Color.FromArgb(140, 0, 0, 0),
                        BlurRadius = 3,
                        ShadowDepth = 1,
                        Opacity = 0.6
                    };
                    style.CornerRadius = new CornerRadius(12);
                    break;

                case "solid":
                default:
                    style.Background = new SolidColorBrush(baseColor);
                    style.BorderBrush = new SolidColorBrush(Colors.White);
                    style.BorderThickness = new Thickness(2);
                    style.Effect = GpuOptimizationHelper.CreateDropShadowEffect();
                    style.CornerRadius = new CornerRadius(10);
                    style.TextColor = isLight ? new SolidColorBrush(Color.FromRgb(20, 20, 20)) : new SolidColorBrush(Colors.White);
                    break;
            }

            return style;
        }

        public static void ApplyToBorder(Border border, WorkflowNode node, IWorkflowEditorHost host)
        {
            if (node is ActionCanVasNode || node is LoopBodyNode) return;

            var mode = GetNormalizedMode(host.NodeAppearanceMode);
            if (string.Equals(mode, ModeSolid, StringComparison.OrdinalIgnoreCase)) return;

            var baseColor = LiquidGlassHelper.GetColorFromBrush(node.NodeBrush);

            var isDiamondNode = node is LoopNode
                || (node.IsConditionalNode && node.ConditionalVisualMode == ConditionalVisualMode.Diamond)
                || (node is AsyncTaskNode asyncTask && asyncTask.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch);

            if (isDiamondNode)
            {
                return;
            }

            var style = GetStyle(mode, baseColor);
            border.Background = style.Background;
            border.BorderBrush = style.BorderBrush;
            border.BorderThickness = style.BorderThickness;
            border.CornerRadius = style.CornerRadius;
            border.Effect = style.Effect;
        }

        public static void ApplyHoverToBorder(Border border, WorkflowNode node, IWorkflowEditorHost host)
        {
            if (node is ActionCanVasNode || node is LoopBodyNode) return;

            var mode = GetNormalizedMode(host.NodeAppearanceMode);
            var baseColor = LiquidGlassHelper.GetColorFromBrush(node.NodeBrush);

            if (string.Equals(mode, ModeLiquidGlass, StringComparison.OrdinalIgnoreCase))
            {
                border.Background = LiquidGlassHelper.CreateGlassHoverBackground(baseColor);
                border.BorderBrush = LiquidGlassHelper.CreateGlassHoverBorderBrush();
            }
            else if (string.Equals(mode, ModeModernGradient, StringComparison.OrdinalIgnoreCase))
            {
                Color highlightBase = Color.FromRgb(
                    (byte)Math.Min(255, baseColor.R + 40),
                    (byte)Math.Min(255, baseColor.G + 40),
                    (byte)Math.Min(255, baseColor.B + 40));
                Color darkerColor = Color.FromRgb(
                    (byte)Math.Max(0, baseColor.R - 35),
                    (byte)Math.Max(0, baseColor.G - 35),
                    (byte)Math.Max(0, baseColor.B - 35));
                border.Background = new LinearGradientBrush(highlightBase, darkerColor, 90.0);
                border.BorderBrush = new LinearGradientBrush(
                    Color.FromArgb(235, 255, 255, 255),
                    Color.FromArgb(90, 255, 255, 255),
                    90.0);
                border.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 18,
                    ShadowDepth = 5,
                    Opacity = 0.55,
                    Direction = 270
                };
            }
        }

        public static void SyncStartEndPortVisibility(WorkflowNode node)
        {
            if (node == null || node.Ports == null) return;

            if (node.Type == NodeType.Start)
            {
                var inputPort = node.Ports.FirstOrDefault(p => p.IsInput);
                if (inputPort != null)
                {
                    inputPort.IsVisible = (node.RunMode != FlowRunMode.MainFlow);
                    if (inputPort.PortUI != null)
                    {
                        inputPort.PortUI.Visibility = inputPort.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                var outputPort = node.Ports.FirstOrDefault(p => !p.IsInput);
                if (outputPort != null)
                {
                    outputPort.IsVisible = true;
                    if (outputPort.PortUI != null)
                    {
                        outputPort.PortUI.Visibility = Visibility.Visible;
                    }
                }
            }
            else if (node.Type == NodeType.End)
            {
                var inputPort = node.Ports.FirstOrDefault(p => p.IsInput);
                if (inputPort != null)
                {
                    inputPort.IsVisible = true;
                    if (inputPort.PortUI != null)
                    {
                        inputPort.PortUI.Visibility = Visibility.Visible;
                    }
                }
                var outputPort = node.Ports.FirstOrDefault(p => !p.IsInput);
                if (outputPort != null)
                {
                    outputPort.IsVisible = (node.EndBehavior != EndNodeBehavior.StopCurrentFlow);
                    if (outputPort.PortUI != null)
                    {
                        outputPort.PortUI.Visibility = outputPort.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }
    }
}
