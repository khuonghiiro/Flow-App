using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Controls
{
    public partial class LoadingUserControl : UserControl
    {
        public LoadingUserControl()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        // IsVisible Property
        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register("IsVisible", typeof(bool), typeof(LoadingUserControl),
                new PropertyMetadata(false));

        public bool IsVisible
        {
            get { return (bool)GetValue(IsVisibleProperty); }
            set { SetValue(IsVisibleProperty, value); }
        }

        // LoadingText Property (thay thế cho Message)
        public static readonly DependencyProperty LoadingTextProperty =
            DependencyProperty.Register("LoadingText", typeof(string), typeof(LoadingUserControl),
                new PropertyMetadata("Đang tải dữ liệu"));

        public string LoadingText
        {
            get { return (string)GetValue(LoadingTextProperty); }
            set { SetValue(LoadingTextProperty, value); }
        }

        // SubText Property
        public static readonly DependencyProperty SubTextProperty =
            DependencyProperty.Register("SubText", typeof(string), typeof(LoadingUserControl),
                new PropertyMetadata("Vui lòng chờ trong giây lát..."));

        public string SubText
        {
            get { return (string)GetValue(SubTextProperty); }
            set { SetValue(SubTextProperty, value); }
        }

        // SpinnerSize Property
        public static readonly DependencyProperty SpinnerSizeProperty =
            DependencyProperty.Register("SpinnerSize", typeof(double), typeof(LoadingUserControl),
                new PropertyMetadata(40.0));

        public double SpinnerSize
        {
            get { return (double)GetValue(SpinnerSizeProperty); }
            set { SetValue(SpinnerSizeProperty, value); }
        }

        // SpinnerColor Property
        public static readonly DependencyProperty SpinnerColorProperty =
            DependencyProperty.Register("SpinnerColor", typeof(Brush), typeof(LoadingUserControl),
                new PropertyMetadata(Brushes.DodgerBlue));

        public Brush SpinnerColor
        {
            get { return (Brush)GetValue(SpinnerColorProperty); }
            set { SetValue(SpinnerColorProperty, value); }
        }

        // MessageFontSize Property
        public static readonly DependencyProperty MessageFontSizeProperty =
            DependencyProperty.Register("MessageFontSize", typeof(double), typeof(LoadingUserControl),
                new PropertyMetadata(16.0));

        public double MessageFontSize
        {
            get { return (double)GetValue(MessageFontSizeProperty); }
            set { SetValue(MessageFontSizeProperty, value); }
        }

        // CornerRadius Property
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register("CornerRadius", typeof(double), typeof(LoadingUserControl),
                new PropertyMetadata(12.0));

        public double CornerRadius
        {
            get { return (double)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }

        #endregion

        #region Public Methods

        public void Show(string loadingText = null, string subText = null)
        {
            if (!string.IsNullOrEmpty(loadingText))
                LoadingText = loadingText;

            if (!string.IsNullOrEmpty(subText))
                SubText = subText;

            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
        }

        public void UpdateText(string loadingText, string subText = null)
        {
            LoadingText = loadingText;
            if (!string.IsNullOrEmpty(subText))
                SubText = subText;
        }

        #endregion
    }
}



 //<controls:LoadingUserControl 
 //                   IsVisible="{Binding IsLoading}"
 //                   x:Name="loadingControl"
 //                        LoadingText="Đang xử lý dữ liệu"
 //                        SubText="Vui lòng chờ đợi trong giây lát..."
 //                        SpinnerSize="50"
 //                        MessageFontSize="18"/>