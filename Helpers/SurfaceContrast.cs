using System;
using System.Windows.Media;

namespace FlowMy.Helpers
{
    /// <summary>WCAG relative luminance + readable text on arbitrary surfaces.</summary>
    public static class SurfaceContrast
    {
        public static Color CompositeOver(Color top, Color bottom)
        {
            double a = top.A / 255.0;
            if (a <= 0) return bottom;
            if (a >= 1) return Color.FromArgb(255, top.R, top.G, top.B);
            double ia = 1 - a;
            return Color.FromArgb(
                255,
                (byte)Math.Clamp(top.R * a + bottom.R * ia, 0, 255),
                (byte)Math.Clamp(top.G * a + bottom.G * ia, 0, 255),
                (byte)Math.Clamp(top.B * a + bottom.B * ia, 0, 255));
        }

        /// <summary>WCAG 2.1 relative luminance for sRGB (alpha ignored — use CompositeOver first).</summary>
        public static double RelativeLuminance(Color c)
        {
            static double Lin(byte u)
            {
                double s = u / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            double r = Lin(c.R), g = Lin(c.G), b = Lin(c.B);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        /// <summary>Picks near-black or near-white text with ≥ ~4.5:1 on typical UI surfaces.</summary>
        public static Color TextPrimaryOnSurface(Color surface)
        {
            double L = RelativeLuminance(surface);
            // Threshold tuned so light greys get dark text and dark surfaces get light text.
            if (L > 0.42)
                return Color.FromRgb(18, 22, 30);
            return Color.FromRgb(236, 240, 248);
        }

        public static Color TextSecondaryOnSurface(Color surface)
        {
            double L = RelativeLuminance(surface);
            if (L > 0.42)
                return Color.FromRgb(66, 76, 92);
            return Color.FromRgb(168, 182, 202);
        }
    }
}
