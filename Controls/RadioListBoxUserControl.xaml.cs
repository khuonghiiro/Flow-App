using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    public partial class RadioListBoxUserControl : UserControl
    {
        public RadioListBoxUserControl()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        /// <summary>
        /// ItemsSource cho ListBox
        /// </summary>
        public IEnumerable ItemsSource
        {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(RadioListBoxUserControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(RadioListBoxUserControl),
                new PropertyMetadata("Danh sách"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty ContainerStyleProperty =
           DependencyProperty.Register("ContainerStyle", typeof(Style), typeof(RadioListBoxUserControl),
               new PropertyMetadata(null));

        public Style ContainerStyle
        {
            get => (Style)GetValue(ContainerStyleProperty);
            set => SetValue(ContainerStyleProperty, value);
        }

        /// <summary>
        /// Path để lấy DisplayText từ object
        /// </summary>
        public string DisplayTextPath
        {
            get { return (string)GetValue(DisplayTextPathProperty); }
            set { SetValue(DisplayTextPathProperty, value); }
        }

        public static readonly DependencyProperty DisplayTextPathProperty =
            DependencyProperty.Register("DisplayTextPath", typeof(string), typeof(RadioListBoxUserControl),
                new PropertyMetadata("DisplayText"));

        /// <summary>
        /// Path để lấy Description từ object
        /// </summary>
        public string DescriptionPath
        {
            get { return (string)GetValue(DescriptionPathProperty); }
            set { SetValue(DescriptionPathProperty, value); }
        }

        public static readonly DependencyProperty DescriptionPathProperty =
            DependencyProperty.Register("DescriptionPath", typeof(string), typeof(RadioListBoxUserControl),
                new PropertyMetadata("Description"));

        /// <summary>
        /// Path để lấy AdditionalInfo từ object
        /// </summary>
        public string AdditionalInfoPath
        {
            get { return (string)GetValue(AdditionalInfoPathProperty); }
            set { SetValue(AdditionalInfoPathProperty, value); }
        }

        public static readonly DependencyProperty AdditionalInfoPathProperty =
            DependencyProperty.Register("AdditionalInfoPath", typeof(string), typeof(RadioListBoxUserControl),
                new PropertyMetadata("AdditionalInfo"));

        /// <summary>
        /// Item được chọn
        /// </summary>
        public object SelectedItem
        {
            get { return GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(object), typeof(RadioListBoxUserControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>
        /// Style tùy chỉnh cho ListBox
        /// </summary>
        public Style ListBoxStyle
        {
            get { return (Style)GetValue(ListBoxStyleProperty); }
            set { SetValue(ListBoxStyleProperty, value); }
        }

        public static readonly DependencyProperty ListBoxStyleProperty =
            DependencyProperty.Register("ListBoxStyle", typeof(Style), typeof(RadioListBoxUserControl),
                new PropertyMetadata(null));

        /// <summary>
        /// Command khi scroll thay đổi
        /// </summary>
        public ICommand ScrollChangedCommand
        {
            get { return (ICommand)GetValue(ScrollChangedCommandProperty); }
            set { SetValue(ScrollChangedCommandProperty, value); }
        }

        public static readonly DependencyProperty ScrollChangedCommandProperty =
            DependencyProperty.Register("ScrollChangedCommand", typeof(ICommand), typeof(RadioListBoxUserControl),
                new PropertyMetadata(null));

        /// <summary>
        /// Command khi selection thay đổi
        /// </summary>
        public ICommand SelectionChangedCommand
        {
            get { return (ICommand)GetValue(SelectionChangedCommandProperty); }
            set { SetValue(SelectionChangedCommandProperty, value); }
        }

        public static readonly DependencyProperty SelectionChangedCommandProperty =
            DependencyProperty.Register("SelectionChangedCommand", typeof(ICommand), typeof(RadioListBoxUserControl),
                new PropertyMetadata(null));

        /// <summary>
        /// Chiều cao tối đa
        /// </summary>
        public new double MaxHeight
        {
            get { return (double)GetValue(MaxHeightProperty); }
            set { SetValue(MaxHeightProperty, value); }
        }

        public new static readonly DependencyProperty MaxHeightProperty =
            DependencyProperty.Register("MaxHeight", typeof(double), typeof(RadioListBoxUserControl),
                new PropertyMetadata(double.PositiveInfinity));

        /// <summary>
        /// Chiều cao tối thiểu
        /// </summary>
        public new double MinHeight
        {
            get { return (double)GetValue(MinHeightProperty); }
            set { SetValue(MinHeightProperty, value); }
        }

        public new static readonly DependencyProperty MinHeightProperty =
            DependencyProperty.Register("MinHeight", typeof(double), typeof(RadioListBoxUserControl),
                new PropertyMetadata(0.0));

        #endregion

        #region Public Methods

        /// <summary>
        /// Lấy ListBox bên trong để truy cập thêm tính năng nếu cần
        /// </summary>
        public ListBox GetInternalListBox()
        {
            return RadioListBox;
        }

        /// <summary>
        /// Scroll đến item được chỉ định
        /// </summary>
        public void ScrollIntoView(object item)
        {
            RadioListBox?.ScrollIntoView(item);
        }

        /// <summary>
        /// Clear selection
        /// </summary>
        public void ClearSelection()
        {
            RadioListBox.SelectedItem = null;
        }

        /// <summary>
        /// Refresh ItemsSource
        /// </summary>
        public void RefreshItems()
        {
            RadioListBox?.Items.Refresh();
        }

        #endregion
    }

}

