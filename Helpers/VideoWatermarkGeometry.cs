using System;
using System.Globalization;

namespace FlowMy.Helpers
{
    /// <summary>
    /// Shared watermark layout: size as a fraction of main frame width, inset as fractions of W/H.
    /// Matches FFmpeg scale2ref + overlay with W/H/w/h expressions.
    /// </summary>
    public static class VideoWatermarkGeometry
    {
        public static double ClampWidthFraction(double value) =>
            Math.Clamp(double.IsNaN(value) || value <= 0 ? 0.20 : value, 0.05, 0.90);

        public static double ClampInsetFraction(double value) =>
            Math.Clamp(double.IsNaN(value) || value < 0 ? 0.05 : value, 0, 0.25);

        /// <summary>FFmpeg scale2ref width expression (reference = main video).</summary>
        public static string BuildScaleWidthExpression(double widthFraction)
        {
            var wf = ClampWidthFraction(widthFraction).ToString("0.######", CultureInfo.InvariantCulture);
            return $"max(1\\,trunc(main_w*{wf}))";
        }

        /// <summary>overlay= x:y string using W,H,w,h; inset is fraction of width/height per edge.</summary>
        public static string BuildOverlayPositionExpression(string? position, double insetFraction)
        {
            var inf = ClampInsetFraction(insetFraction).ToString("0.######", CultureInfo.InvariantCulture);
            var ix = $"trunc(W*{inf})";
            var iy = $"trunc(H*{inf})";
            return (position ?? "BR").Trim().ToUpperInvariant() switch
            {
                "TL" => $"x={ix}:y={iy}",
                "TC" => $"x=(W-w)/2:y={iy}",
                "TR" => $"x=W-w-{ix}:y={iy}",
                "ML" => $"x={ix}:y=(H-h)/2",
                "MC" => "x=(W-w)/2:y=(H-h)/2",
                "MR" => $"x=W-w-{ix}:y=(H-h)/2",
                "BL" => $"x={ix}:y=H-h-{iy}",
                "BC" => $"x=(W-w)/2:y=H-h-{iy}",
                _ => $"x=W-w-{ix}:y=H-h-{iy}"
            };
        }
    }
}
