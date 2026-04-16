using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FlowMy.Controls
{
    /// <summary>
    /// Interaction logic for FontIconUserControl.xaml
    /// </summary>
    public partial class FontIconUserControl : UserControl
    {
        public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register("Icon", typeof(string), typeof(FontIconUserControl));

        public static readonly DependencyProperty IconBrushProperty =
            DependencyProperty.Register("IconBrush", typeof(Brush), typeof(FontIconUserControl),
            new PropertyMetadata(Brushes.Black));

        public static readonly DependencyProperty IconSizeProperty =
            DependencyProperty.Register("IconSize", typeof(double), typeof(FontIconUserControl),
            new PropertyMetadata(16.0));

        public string Icon
        {
            get { return (string)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public Brush IconBrush
        {
            get { return (Brush)GetValue(IconBrushProperty); }
            set { SetValue(IconBrushProperty, value); }
        }

        public double IconSize
        {
            get { return (double)GetValue(IconSizeProperty); }
            set { SetValue(IconSizeProperty, value); }
        }

        public FontIconUserControl()
        {
            InitializeComponent();
        }
    }
}


//<!-- Sử dụng FontIcon -->
//<controls:FontIcon Icon="{StaticResource fa-home}" 
//                  IconBrush="Blue" 
//                  IconSize="24"/>

//<Button>
//    <controls:FontIcon Icon="{StaticResource fa-user}" 
//                      IconBrush="Green" 
//                      IconSize="16"/>
//</Button>