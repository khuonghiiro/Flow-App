using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace FlowMy.Effects
{
    /// <summary>GPU preview grading aligned roughly with FFmpeg eq (brightness/contrast/saturation/gamma + hue).</summary>
    public sealed class VideoEqEffect : ShaderEffect
    {
        public static readonly DependencyProperty InputProperty =
            RegisterPixelShaderSamplerProperty("Input", typeof(VideoEqEffect), 0);

        /// <summary>X=brightness, Y=contrast.</summary>
        public static readonly DependencyProperty BcProperty = DependencyProperty.Register(
            nameof(Bc),
            typeof(Point),
            typeof(VideoEqEffect),
            new UIPropertyMetadata(new Point(0, 1), PixelShaderConstantCallback(0)));

        /// <summary>X=saturation, Y=gamma.</summary>
        public static readonly DependencyProperty SgProperty = DependencyProperty.Register(
            nameof(Sg),
            typeof(Point),
            typeof(VideoEqEffect),
            new UIPropertyMetadata(new Point(1, 1), PixelShaderConstantCallback(1)));

        /// <summary>X=cos(hueRad), Y=sin(hueRad).</summary>
        public static readonly DependencyProperty HueCsProperty = DependencyProperty.Register(
            nameof(HueCs),
            typeof(Point),
            typeof(VideoEqEffect),
            new UIPropertyMetadata(new Point(1, 0), PixelShaderConstantCallback(2)));

        private static readonly PixelShader? ShaderResource = LoadShader();

        private static PixelShader? LoadShader()
        {
            try
            {
                var asmName = Assembly.GetExecutingAssembly().GetName().Name;
                var uri = new Uri($"pack://application:,,,/{asmName};component/Effects/video_eq.ps", UriKind.Absolute);
                return new PixelShader { UriSource = uri };
            }
            catch
            {
                return null;
            }
        }

        public VideoEqEffect()
        {
            if (ShaderResource == null)
                return;
            PixelShader = ShaderResource;
            UpdateShaderValue(InputProperty);
            UpdateShaderValue(BcProperty);
            UpdateShaderValue(SgProperty);
            UpdateShaderValue(HueCsProperty);
        }

        public Brush Input
        {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public Point Bc
        {
            get => (Point)GetValue(BcProperty);
            set => SetValue(BcProperty, value);
        }

        public Point Sg
        {
            get => (Point)GetValue(SgProperty);
            set => SetValue(SgProperty, value);
        }

        public Point HueCs
        {
            get => (Point)GetValue(HueCsProperty);
            set => SetValue(HueCsProperty, value);
        }

        public static bool ShaderAvailable => ShaderResource != null;
    }
}
