using System;
using System.Globalization;
using FlowMy.Models.Nodes;

namespace FlowMy.Helpers
{
    /// <summary>
    /// FFmpeg export filter chain aligned with preview <see cref="Effects.VideoEqEffect"/> (<c>video_eq.fx</c>):
    /// BT.601-ish YCbCr → luminance/contrast plus chroma saturation (single <c>eq</c> filter) →
    /// hue as Cb/Cr rotation (<c>hue</c>, radians via <c>H=</c>) → gamma on RGB (<c>lutrgb</c>).
    /// </summary>
    public static class VideoColorGrading
    {
        public static string BuildEqFilter(VideoProcessingNode node) =>
            $"eq=brightness={node.Brightness:0.###}:contrast={node.Contrast:0.###}:saturation={node.Saturation:0.###}";

        /// <summary>
        /// FFmpeg <c>hue</c>: <c>h</c> is degrees; <c>H</c> is radians. Export must use <c>H=</c> with rad(node.Hue).
        /// </summary>
        public static string? BuildHueFilter(double hueDegrees)
        {
            if (Math.Abs(hueDegrees) < 0.01) return null;
            var rad = (hueDegrees * (Math.PI / 180.0)).ToString("0.######", CultureInfo.InvariantCulture);
            return $"hue=H={rad}";
        }

        public static string? BuildGammaLutRgbFilter(double gamma)
        {
            if (Math.Abs(gamma - 1) < 0.01) return null;
            var g = gamma.ToString("0.###", CultureInfo.InvariantCulture);
            return $"lutrgb=r='pow(val/255,1/{g})*255':g='pow(val/255,1/{g})*255':b='pow(val/255,1/{g})*255'";
        }
    }
}
