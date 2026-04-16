using FlowMy.Converters;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;

namespace FlowMy.Controls
{
    /// <summary>
    /// Định nghĩa button động trong ActionColumn
    /// </summary>
    public class ActionButtonDefinition
    {
        /// <summary>
        /// Nội dung hiển thị trên button (text hoặc icon)
        /// </summary>
        public object Content { get; set; }

        /// <summary>
        /// Tên command trong ViewModel (ví dụ: "ViewCommand", "CopyCommand")
        /// </summary>
        public string CommandName { get; set; }

        /// <summary>
        /// Có hiển thị button này hay không
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Style name của button (ví dụ: "EditButtonStyle", "DeleteButtonStyle", hoặc null để dùng style mặc định)
        /// </summary>
        public string StyleName { get; set; }

        /// <summary>
        /// Tooltip cho button
        /// </summary>
        public string ToolTip { get; set; }

        /// <summary>
        /// Margin của button
        /// </summary>
        public Thickness Margin { get; set; } = new Thickness(2, 0, 2, 0);

        /// <summary>
        /// Thứ tự sắp xếp button (0 = trái nhất, số lớn hơn = bên phải hơn)
        /// Hỗ trợ số thập phân để sắp xếp chi tiết (ví dụ: 0.01, 1.2)
        /// Mặc định: 0
        /// </summary>
        public double Order { get; set; } = 0;
    }

    /// <summary>
    /// Helper class để chứa thông tin button và Order để sắp xếp
    /// </summary>
    internal class ButtonInfo
    {
        public double Order { get; set; }
        public FrameworkElementFactory ButtonFactory { get; set; }
    }

    /// <summary>
    /// Interaction logic for DataGridUserControl.xaml
    /// </summary>
    [ContentProperty("AdditionalContent")]
    public partial class DataGridUserControl : UserControl
    {
        private DataTemplate _actionCellTemplate;

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(object), typeof(DataGridUserControl),
                new PropertyMetadata(null, OnViewModelChanged));

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register("Columns", typeof(ObservableCollection<DataGridColumn>), typeof(DataGridUserControl),
                new PropertyMetadata(null, OnColumnsChanged));

        // Thêm TitleText Property
        public static readonly DependencyProperty TitleTextProperty =
            DependencyProperty.Register("TitleText", typeof(string), typeof(DataGridUserControl),
                new PropertyMetadata("Danh sách dữ liệu", OnTitleTextChanged));

        // Thêm AdditionalContent Property để truyền UI từ bên ngoài
        public static readonly DependencyProperty AdditionalContentProperty =
            DependencyProperty.Register("AdditionalContent", typeof(object), typeof(DataGridUserControl),
                new PropertyMetadata(null, OnAdditionalContentChanged));

        // Thêm AdditionalButtonNew Property để truyền UI button từ bên ngoài
        public static readonly DependencyProperty AdditionalButtonNewProperty =
            DependencyProperty.Register("AdditionalButtonNew", typeof(object), typeof(DataGridUserControl),
                new PropertyMetadata(null, OnAdditionalButtonNewChanged));

        // Property để theo dõi trạng thái hiển thị
        public static readonly DependencyProperty IsAdditionalContentVisibleProperty =
            DependencyProperty.Register("IsAdditionalContentVisible", typeof(bool), typeof(DataGridUserControl),
                new PropertyMetadata(false, OnIsAdditionalContentVisibleChanged));

        // Properties để điều khiển hiển thị các button
        public static readonly DependencyProperty ShowAddButtonProperty =
            DependencyProperty.Register("ShowAddButton", typeof(bool), typeof(DataGridUserControl),
                new PropertyMetadata(true)); // Mặc định hiển thị

        public static readonly DependencyProperty ShowImportButtonProperty =
            DependencyProperty.Register("ShowImportButton", typeof(bool), typeof(DataGridUserControl),
                new PropertyMetadata(true)); // Mặc định hiển thị

        public static readonly DependencyProperty ShowExportButtonProperty =
            DependencyProperty.Register("ShowExportButton", typeof(bool), typeof(DataGridUserControl),
                new PropertyMetadata(true)); // Mặc định hiển thị

        public static readonly DependencyProperty ShowAddContentButtonProperty =
            DependencyProperty.Register("ShowAddContentButton", typeof(bool), typeof(DataGridUserControl),
                new PropertyMetadata(true)); // Mặc định hiển thị

        // Properties để điều khiển hiển thị các button hành động ở từng dòng
        public static readonly DependencyProperty ShowEditButtonProperty =
            DependencyProperty.Register("ShowEditButton", typeof(bool), typeof(DataGridUserControl),
                new PropertyMetadata(true)); // Mặc định hiển thị

        public static readonly DependencyProperty ShowDeleteButtonProperty =
            DependencyProperty.Register("ShowDeleteButton", typeof(bool), typeof(DataGridUserControl),
                new PropertyMetadata(true)); // Mặc định hiển thị

        public static readonly DependencyProperty ShowActionColumnProperty =
            DependencyProperty.Register("ShowActionColumn", typeof(bool), typeof(DataGridUserControl),
                new PropertyMetadata(true)); // Mặc định hiển thị cột hành động

        // Property để chứa danh sách các button động
        public static readonly DependencyProperty ActionButtonsProperty =
            DependencyProperty.Register("ActionButtons", typeof(ObservableCollection<ActionButtonDefinition>), typeof(DataGridUserControl),
                new PropertyMetadata(null, OnActionButtonsChanged));


        public object ViewModel
        {
            get { return GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }

        public ObservableCollection<DataGridColumn> Columns
        {
            get { return (ObservableCollection<DataGridColumn>)GetValue(ColumnsProperty); }
            set { SetValue(ColumnsProperty, value); }
        }

        public string TitleText
        {
            get { return (string)GetValue(TitleTextProperty); }
            set { SetValue(TitleTextProperty, value); }
        }

        public object AdditionalContent
        {
            get { return GetValue(AdditionalContentProperty); }
            set { SetValue(AdditionalContentProperty, value); }
        }

        public object AdditionalButtonNew
        {
            get { return GetValue(AdditionalButtonNewProperty); }
            set { SetValue(AdditionalButtonNewProperty, value); }
        }

        public bool IsAdditionalContentVisible
        {
            get { return (bool)GetValue(IsAdditionalContentVisibleProperty); }
            set { SetValue(IsAdditionalContentVisibleProperty, value); }
        }

        public bool ShowAddButton
        {
            get { return (bool)GetValue(ShowAddButtonProperty); }
            set
            {
                SetValue(ShowAddButtonProperty, value);
                ClearTemplateCache(); // Clear cache để tạo lại template
            }
        }

        public bool ShowImportButton
        {
            get { return (bool)GetValue(ShowImportButtonProperty); }
            set
            {
                SetValue(ShowImportButtonProperty, value);
                ClearTemplateCache(); // Clear cache để tạo lại template
            }
        }

        public bool ShowExportButton
        {
            get { return (bool)GetValue(ShowExportButtonProperty); }
            set
            {
                SetValue(ShowExportButtonProperty, value);
                ClearTemplateCache(); // Clear cache để tạo lại template
            }
        }

        public bool ShowAddContentButton
        {
            get { return (bool)GetValue(ShowAddContentButtonProperty); }
            set
            {
                SetValue(ShowAddContentButtonProperty, value);
                ClearTemplateCache(); // Clear cache để tạo lại template
            }
        }

        public bool ShowEditButton
        {
            get { return (bool)GetValue(ShowEditButtonProperty); }
            set
            {
                SetValue(ShowEditButtonProperty, value);
                ClearTemplateCache(); // Clear cache để tạo lại template
            }
        }

        public bool ShowDeleteButton
        {
            get { return (bool)GetValue(ShowDeleteButtonProperty); }
            set { SetValue(ShowDeleteButtonProperty, value); }
        }

        public bool ShowActionColumn
        {
            get { return (bool)GetValue(ShowActionColumnProperty); }
            set { SetValue(ShowActionColumnProperty, value); }
        }

        public ObservableCollection<ActionButtonDefinition> ActionButtons
        {
            get { return (ObservableCollection<ActionButtonDefinition>)GetValue(ActionButtonsProperty); }
            set { SetValue(ActionButtonsProperty, value); }
        }


        public DataGridUserControl()
        {
            InitializeComponent();

            // Đăng ký event khi DataContext thay đổi
            this.DataContextChanged += DataGridUserControl_DataContextChanged;

            // TÙY CHỌN: Đảm bảo binding hoạt động ngay từ đầu
            this.Loaded += (s, e) =>
            {
                if (this.DataContext != null)
                {
                    this.UpdateLayout();
                }
            };
        }

        private void DataGridUserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Hủy đăng ký event cũ nếu có
            if (e.OldValue != null && MainDataGrid?.ItemsSource is System.Collections.Specialized.INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= PagedItems_CollectionChanged;
            }

            // Khi DataContext được set (ViewModel được bind), update columns
            if (e.NewValue != null)
            {
                // Sử dụng một delay nhỏ để đảm bảo tất cả binding hoàn tất
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateColumns();

                    // Đăng ký event mới cho PagedItems collection
                    if (MainDataGrid?.ItemsSource is System.Collections.Specialized.INotifyCollectionChanged newCollection)
                    {
                        newCollection.CollectionChanged += PagedItems_CollectionChanged;
                    }
                }), System.Windows.Threading.DispatcherPriority.DataBind);
            }
        }

        private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DataGridUserControl)d;

            // Set DataContext trước
            control.DataContext = e.NewValue;

            // Đảm bảo binding được thiết lập đúng
            if (e.NewValue != null)
            {
                control.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Force refresh binding cho Loading overlay
                    control.RefreshLoadingBinding();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void RefreshLoadingBinding()
        {
            // Tìm Loading overlay
            var loadingGrid = this.FindName("LoadingOverlay") as FrameworkElement;
            if (loadingGrid != null && this.DataContext != null)
            {
                // Tạo binding mới cho Visibility
                var binding = new System.Windows.Data.Binding("IsLoading")
                {
                    Source = this.DataContext,
                    Converter = this.Resources["BoolToVisibilityConverter"] as IValueConverter
                };

                // Clear và set lại binding
                BindingOperations.ClearBinding(loadingGrid, UIElement.VisibilityProperty);
                BindingOperations.SetBinding(loadingGrid, UIElement.VisibilityProperty, binding);
            }
        }

        private void UpdateBinding()
        {
            // Tìm Grid loading overlay và force update binding
            var loadingGrid = this.FindName("LoadingOverlay") as Grid;
            if (loadingGrid != null)
            {
                var visibilityBinding = BindingOperations.GetBinding(loadingGrid, UIElement.VisibilityProperty);
                if (visibilityBinding != null)
                {
                    BindingOperations.ClearBinding(loadingGrid, UIElement.VisibilityProperty);
                    BindingOperations.SetBinding(loadingGrid, UIElement.VisibilityProperty, visibilityBinding);
                }
            }

            // Force update DataContext binding
            this.UpdateLayout();
        }

        private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DataGridUserControl)d;
            control.UpdateColumns();
        }

        private static void OnTitleTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DataGridUserControl)d;
            // Không cần làm gì đặc biệt vì XAML sẽ tự động update thông qua binding
        }

        private static void OnAdditionalContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DataGridUserControl)d;
            control.UpdateToggleButtonVisibility(); // Gọi method tổng hợp
        }

        // Thêm method này để kiểm tra CẢ HAI điều kiện
        private void UpdateToggleButtonVisibility()
        {
            if (ToggleAdditionalContentButton != null)
            {
                // ✅ CHỈ HIỂN THỊ KHI:
                // 1. ShowAddContentButton = true
                // 2. VÀ AdditionalContent != null
                ToggleAdditionalContentButton.Visibility =
                    (ShowAddContentButton && AdditionalContent != null)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private static void OnAdditionalButtonNewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DataGridUserControl)d;
            // AdditionalButtonNew không cần xử lý đặc biệt vì nó luôn hiển thị khi có content
            // Không cần toggle button cho AdditionalButtonNew
        }

        private static void OnIsAdditionalContentVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DataGridUserControl)d;
            control.UpdateAdditionalContentVisibility();
        }

        private static void OnActionButtonsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DataGridUserControl)d;
            // Clear cache để tạo lại template với button mới
            control.ClearTemplateCache();
            // Update columns nếu đã được khởi tạo
            if (control.MainDataGrid != null)
            {
                control.Dispatcher.BeginInvoke(new Action(() =>
                {
                    control.UpdateColumns();
                }), System.Windows.Threading.DispatcherPriority.DataBind);
            }
        }

        private void UpdateColumns()
        {
            if (Columns == null) return;

            // ✅ SỬA: Clear và add trong batch để giảm UI updates
            MainDataGrid.BeginInit();
            try
            {
                MainDataGrid.Columns.Clear();

                foreach (var column in Columns)
                {
                    if (column.Header == null) continue;

                    column.Header = column.Header.ToString();
                    MainDataGrid.Columns.Add(column);
                }

                AddActionColumn();
            }
            finally
            {
                MainDataGrid.EndInit();
            }

            // ✅ Clear sort descriptions sau khi columns được update
            // Sử dụng BeginInvoke để đảm bảo DataGrid đã hoàn tất việc update
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ClearDataGridSort();

                // Đăng ký event cho PagedItems collection nếu chưa đăng ký
                if (MainDataGrid?.ItemsSource is System.Collections.Specialized.INotifyCollectionChanged collection)
                {
                    collection.CollectionChanged -= PagedItems_CollectionChanged; // Hủy đăng ký cũ trước
                    collection.CollectionChanged += PagedItems_CollectionChanged;
                }
            }), System.Windows.Threading.DispatcherPriority.DataBind);
        }

        /// <summary>
        /// Xóa tất cả sort descriptions của DataGrid để đảm bảo dữ liệu hiển thị đúng thứ tự từ server
        /// Server đã sort dữ liệu rồi, nên không cần DataGrid tự động sort lại
        /// Khi user click header, SortCommand sẽ được gọi và data sẽ được load lại từ server với sort mới
        /// </summary>
        private void ClearDataGridSort()
        {
            if (MainDataGrid?.Items == null) return;

            // Clear sort descriptions của tất cả columns
            foreach (var column in MainDataGrid.Columns)
            {
                column.SortDirection = null;
            }

            // Clear sort descriptions của Items collection view
            // Điều này đảm bảo DataGrid không tự động sort lại dữ liệu đã được sort từ server
            var itemsView = System.Windows.Data.CollectionViewSource.GetDefaultView(MainDataGrid.ItemsSource);
            if (itemsView != null && itemsView.SortDescriptions != null)
            {
                itemsView.SortDescriptions.Clear();
            }
        }

        /// <summary>
        /// Đặt các cột là width = "*" thì nó sẽ khiến các cột căn đều trong hiển thị nhưng không scroll ngang được 
        /// </summary>
        private void AdjustColumnWidths()
        {
            if (MainDataGrid.Columns.Count == 0) return;

            //// Đặt tất cả cột có width tỷ lệ đều nhau
            //foreach (var column in MainDataGrid.Columns)
            //{
            //    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            //}
        }

        private void AddIndexColumn()
        {
            var indexColumn = new DataGridTemplateColumn
            {
                Header = "STT",
                Width = 50,
                IsReadOnly = true
            };

            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            // AlternationIndex + 1 (vì nó bắt đầu từ 0)
            textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding
            {
                Path = new PropertyPath("(ItemsControl.AlternationIndex)"),
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridRow), 1),
                Converter = new IndexConverter() // custom converter +1
            });

            indexColumn.CellTemplate = new DataTemplate { VisualTree = textBlockFactory };

            // Luôn chèn STT vào đầu thứ 2
            MainDataGrid.Columns.Insert(1, indexColumn);

            // Để AlternationIndex chạy đúng
            MainDataGrid.AlternationCount = int.MaxValue;
        }


        private void AddActionColumn()
        {
            // Kiểm tra xem có button nào để hiển thị không
            bool hasEditOrDelete = ShowEditButton || ShowDeleteButton;
            bool hasActionButtons = ActionButtons != null && ActionButtons.Any(b => b.IsVisible);

            if (!ShowActionColumn || (!hasEditOrDelete && !hasActionButtons))
                return;

            // Tính toán width dựa trên số lượng button
            var widthAction = 150;
            if (!ShowEditButton) widthAction -= 50;
            if (!ShowDeleteButton) widthAction -= 50;

            // Thêm width cho các button động (mỗi button ~50px)
            if (ActionButtons != null)
            {
                var visibleActionButtons = ActionButtons.Count(b => b.IsVisible);
                widthAction += visibleActionButtons * 50;
            }

            var actionsColumn = new DataGridTemplateColumn
            {
                Header = "HÀNH ĐỘNG",
                Width = widthAction,
                CanUserSort = false,
                CellStyle = FindResource("ActionCellStyle") as Style
            };

            // Header style
            var baseHeaderStyle = FindResource(typeof(DataGridColumnHeader)) as Style;
            var headerStyle = new Style(typeof(DataGridColumnHeader), baseHeaderStyle);
            headerStyle.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            headerStyle.Setters.Add(new Setter(VerticalContentAlignmentProperty, VerticalAlignment.Center));
            actionsColumn.HeaderStyle = headerStyle;

            // Sử dụng cached template
            actionsColumn.CellTemplate = CreateActionCellTemplate();

            if (actionsColumn.CellTemplate != null)
            {
                MainDataGrid.Columns.Insert(0, actionsColumn);
                MainDataGrid.FrozenColumnCount = 1;
            }
        }

        private DataTemplate CreateActionCellTemplate()
        {
            if (_actionCellTemplate != null) return _actionCellTemplate;

            var template = new DataTemplate();

            // Tạo StackPanel container
            var stackPanel = new FrameworkElementFactory(typeof(StackPanel));
            stackPanel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackPanel.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            stackPanel.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            stackPanel.SetValue(StackPanel.MarginProperty, new Thickness(0));

            // Danh sách để chứa tất cả buttons với Order để sắp xếp
            var buttonList = new List<ButtonInfo>();

            // Edit button - chỉ thêm nếu ShowEditButton = true, Order mặc định = 1
            if (ShowEditButton)
            {
                var editButton = new FrameworkElementFactory(typeof(Button));
                editButton.SetValue(Button.ContentProperty, "Sửa");
                editButton.SetValue(Button.StyleProperty, FindResource("EditButtonStyle"));
                editButton.SetValue(Button.MarginProperty, new Thickness(0, 0, 2, 0));
                editButton.SetValue(Button.VerticalAlignmentProperty, VerticalAlignment.Center);
                editButton.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Center);

                // Binding cho Edit command
                editButton.SetBinding(Button.CommandProperty, new System.Windows.Data.Binding("DataContext.EditCommand")
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(UserControl), 1)
                });
                editButton.SetBinding(Button.CommandParameterProperty, new System.Windows.Data.Binding("."));

                buttonList.Add(new ButtonInfo { Order = 1.0, ButtonFactory = editButton });
            }

            // Delete button - chỉ thêm nếu ShowDeleteButton = true, Order mặc định = 2
            if (ShowDeleteButton)
            {
                var deleteButton = new FrameworkElementFactory(typeof(Button));
                deleteButton.SetValue(Button.ContentProperty, "Xóa");
                deleteButton.SetValue(Button.StyleProperty, FindResource("DeleteButtonStyle"));
                deleteButton.SetValue(Button.MarginProperty, new Thickness(2, 0, 0, 0));
                deleteButton.SetValue(Button.VerticalAlignmentProperty, VerticalAlignment.Center);
                deleteButton.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Center);

                // Binding cho Delete command
                deleteButton.SetBinding(Button.CommandProperty, new System.Windows.Data.Binding("DataContext.DeleteCommand")
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(UserControl), 1)
                });
                deleteButton.SetBinding(Button.CommandParameterProperty, new System.Windows.Data.Binding("."));

                buttonList.Add(new ButtonInfo { Order = 2.0, ButtonFactory = deleteButton });
            }

            // Thêm các button động
            if (ActionButtons != null)
            {
                foreach (var actionButton in ActionButtons)
                {
                    if (!actionButton.IsVisible) continue;

                    var dynamicButton = new FrameworkElementFactory(typeof(Button));
                    dynamicButton.SetValue(Button.ContentProperty, actionButton.Content ?? "");
                    dynamicButton.SetValue(Button.MarginProperty, actionButton.Margin);
                    dynamicButton.SetValue(Button.VerticalAlignmentProperty, VerticalAlignment.Center);
                    dynamicButton.SetValue(Button.HorizontalAlignmentProperty, HorizontalAlignment.Center);

                    // Set style nếu có
                    if (!string.IsNullOrEmpty(actionButton.StyleName))
                    {
                        var style = FindResource(actionButton.StyleName) as Style;
                        if (style != null)
                        {
                            dynamicButton.SetValue(Button.StyleProperty, style);
                        }
                    }
                    else
                    {
                        // Dùng style mặc định nếu không chỉ định
                        dynamicButton.SetValue(Button.StyleProperty, FindResource("EditButtonStyle"));
                    }

                    // Set tooltip nếu có
                    if (!string.IsNullOrEmpty(actionButton.ToolTip))
                    {
                        dynamicButton.SetValue(Button.ToolTipProperty, actionButton.ToolTip);
                    }

                    // Binding cho command - sử dụng CommandName để bind tới command trong ViewModel
                    if (!string.IsNullOrEmpty(actionButton.CommandName))
                    {
                        dynamicButton.SetBinding(Button.CommandProperty, new System.Windows.Data.Binding($"DataContext.{actionButton.CommandName}")
                        {
                            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(UserControl), 1)
                        });
                    }

                    // CommandParameter luôn là item hiện tại
                    dynamicButton.SetBinding(Button.CommandParameterProperty, new System.Windows.Data.Binding("."));

                    buttonList.Add(new ButtonInfo { Order = actionButton.Order, ButtonFactory = dynamicButton });
                }
            }

            // Sắp xếp buttons theo Order (tăng dần)
            var sortedButtons = buttonList.OrderBy(b => b.Order).ToList();

            // Thêm các button đã sắp xếp vào StackPanel
            foreach (var buttonInfo in sortedButtons)
            {
                stackPanel.AppendChild(buttonInfo.ButtonFactory);
            }

            // Chỉ tạo template nếu có ít nhất một button
            if (sortedButtons.Count > 0)
            {
                template.VisualTree = stackPanel;
                _actionCellTemplate = template;
            }

            return _actionCellTemplate;
        }

        // Optional: Method để update tất cả row heights nếu cần
        private void UpdateRowHeights()
        {
            foreach (var item in MainDataGrid.Items)
            {
                var row = MainDataGrid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                if (row != null)
                {
                    //row.Height = 36;  // Đảm bảo tất cả rows có cùng chiều cao
                    row.Height = double.NaN;
                }
            }
        }

        /// <summary>
        /// Thực thi lệnh tìm kiếm khi nhấn Enter
        /// </summary>
        private void ExecuteSearchCommand()
        {
            //// Tìm ViewModel hiện tại và thực hiện search command
            //if (DataContext is FlowMy.ViewModels.Base.BaseDataGridViewModel<object, object, object, object> viewModel
            //    && viewModel.SearchCommand?.CanExecute(null) == true)
            //{
            //    viewModel.SearchCommand.Execute(null);
            //}
            //else
            //{
            //    // Fallback: sử dụng reflection để gọi SearchCommand
            //    var vmType = DataContext?.GetType();
            //    var searchCommand = vmType?.GetProperty("SearchCommand")?.GetValue(DataContext);
            //    if (searchCommand is System.Windows.Input.ICommand command && command.CanExecute(null))
            //    {
            //        command.Execute(null);
            //    }
            //}
        }

        /// <summary>
        /// Xử lý khi nhấn Enter trong ô tìm kiếm
        /// </summary>
        private void SearchDataGridKeywordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ExecuteSearchCommand();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Xử lý click button toggle Additional Content
        /// </summary>
        private void ToggleAdditionalContentButton_Click(object sender, RoutedEventArgs e)
        {
            IsAdditionalContentVisible = !IsAdditionalContentVisible;
        }

        /// <summary>
        /// Cập nhật visibility của Additional Content và icon button
        /// </summary>
        private void UpdateAdditionalContentVisibility()
        {
            if (AdditionalContentPresenter != null)
            {
                AdditionalContentPresenter.Visibility = IsAdditionalContentVisible ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ToggleAdditionalContentButton != null)
            {
                // Thay đổi icon dựa trên trạng thái
                ToggleAdditionalContentButton.Content = IsAdditionalContentVisible ? "▲" : "▼";
                ToggleAdditionalContentButton.ToolTip = IsAdditionalContentVisible ? "Ẩn nội dung bổ sung" : "Hiện nội dung bổ sung";
            }
        }

        /// <summary>
        /// Override OnApplyTemplate để đảm bảo các control được khởi tạo
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Cập nhật visibility ban đầu
            UpdateAdditionalContentVisibility();

            // Ẩn/hiện button toggle dựa trên việc có AdditionalContent hay không
            if (ToggleAdditionalContentButton != null)
            {
                ToggleAdditionalContentButton.Visibility = (ShowAddContentButton && AdditionalContent != null) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Đăng ký event để clear sort khi ItemsSource thay đổi
            if (MainDataGrid != null)
            {
                MainDataGrid.Loaded += MainDataGrid_Loaded;
                MainDataGrid.Sorting -= MainDataGrid_Sorting;
                MainDataGrid.Sorting += MainDataGrid_Sorting;
            }
        }

        /// <summary>
        /// Xử lý khi DataGrid được load - đăng ký event để monitor ItemsSource changes
        /// </summary>
        private void MainDataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (MainDataGrid?.ItemsSource is System.Collections.Specialized.INotifyCollectionChanged collection)
            {
                collection.CollectionChanged += PagedItems_CollectionChanged;
            }
        }

        /// <summary>
        /// Xử lý khi PagedItems collection thay đổi - clear sort để đảm bảo thứ tự từ server
        /// </summary>
        private void PagedItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Không clear SortDirection nữa để giữ lại icon sort cho header
        }

        // method để clear cache khi cần thiết
        private void ClearTemplateCache()
        {
            _actionCellTemplate = null;
        }

        /// <summary>
        /// Giữ icon sort khi click header bằng cách tự đặt SortDirection
        /// và chặn sort mặc định (để ViewModel tự xử lý server-side).
        /// </summary>
        private void MainDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // Ngăn sort mặc định, nhưng vẫn cập nhật SortDirection để style hiển thị icon
            e.Handled = true;

            var targetColumn = e.Column;

            // Toggle hướng sort
            var nextDirection = targetColumn.SortDirection != ListSortDirection.Ascending
                ? ListSortDirection.Ascending
                : ListSortDirection.Descending;

            // Xóa sort icon các cột khác
            foreach (var column in MainDataGrid.Columns)
            {
                if (!ReferenceEquals(column, targetColumn))
                {
                    column.SortDirection = null;
                }
            }

            targetColumn.SortDirection = nextDirection;
        }
    }
}



// // Trong ViewModel hoặc code-behind
// var actionButtons = new ObservableCollection<ActionButtonDefinition>
// {
//     new ActionButtonDefinition
//     {
//         Content = "Xem",
//         CommandName = "ViewCommand",
//         IsVisible = true,
//         StyleName = "EditButtonStyle", // hoặc null để dùng mặc định
//         ToolTip = "Xem chi tiết",
//         Margin = new Thickness(2, 0, 2, 0)
//     },
//     new ActionButtonDefinition
//     {
//         Content = "Sao chép",
//         CommandName = "CopyCommand",
//         IsVisible = true,
//         StyleName = null, // Dùng style mặc định
//         ToolTip = "Sao chép dữ liệu"
//     }
// };

// // Gán vào DataGridUserControl
// ActionButtons = actionButtons;