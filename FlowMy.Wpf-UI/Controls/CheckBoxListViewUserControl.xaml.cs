using CommunityToolkit.Mvvm.Input;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Linq;

namespace FlowMy.Controls
{
    /// <summary>
    /// Interaction logic for CheckBoxListViewUserControl.xaml
    /// </summary>
    public partial class CheckBoxListViewUserControl : UserControl
    {
        private bool _isUpdatingSelectAll = false;

        public CheckBoxListViewUserControl()
        {
            InitializeComponent();
            Loaded += CheckBoxListViewUserControl_Loaded;
        }


        [RelayCommand]
        private void ToggleItem(object item)
        {
            if (item == null) return;

            var property = item.GetType().GetProperty(IsSelectedPath);
            if (property != null && property.CanRead && property.CanWrite)
            {
                var currentValue = property.GetValue(item);
                if (currentValue is bool isSelected)
                {
                    property.SetValue(item, !isSelected);
                    MainListBox?.Items.Refresh();
                    UpdateSelectAllState();
                    CheckedItemsChangedCommand?.Execute(GetCheckedItems());
                }
            }
        }

        [RelayCommand]
        private void SelectAll()
        {
            if (_isUpdatingSelectAll) return;
            SetAllItemsSelected(true);
        }

        [RelayCommand]
        private void UnselectAll()
        {
            if (_isUpdatingSelectAll) return;
            SetAllItemsSelected(false);
        }

        [RelayCommand]
        private void ItemCheckChanged(object parameter)
        {
            UpdateSelectAllState();
            CheckedItemsChangedCommand?.Execute(GetCheckedItems());
        }

        private void CheckBoxListViewUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSelectAllState();
        }

        #region Dependency Properties

        public object AdditionalButtonNew
        {
            get { return GetValue(AdditionalButtonNewProperty); }
            set { SetValue(AdditionalButtonNewProperty, value); }
        }

        // Thêm AdditionalButtonNew Property để truyền UI button từ bên ngoài
        public static readonly DependencyProperty AdditionalButtonNewProperty =
            DependencyProperty.Register("AdditionalButtonNew", typeof(object), typeof(CheckBoxListViewUserControl),
                new PropertyMetadata(null, OnAdditionalButtonNewChanged));

        /// <summary>
        /// ItemsSource cho ListBox
        /// </summary>
        public IEnumerable ItemsSource
        {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(CheckBoxListViewUserControl),
                new PropertyMetadata(null, OnItemsSourceChanged));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as CheckBoxListViewUserControl;
            control?.UpdateSelectAllState();
        }

        private static void OnAdditionalButtonNewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CheckBoxListViewUserControl)d;
        }

        /// <summary>
        /// Tiêu đề hiển thị ở header
        /// </summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(CheckBoxListViewUserControl),
                new PropertyMetadata("Chọn tất cả"));

        /// <summary>
        /// Path để lấy DisplayText từ object
        /// </summary>
        public string DisplayTextPath
        {
            get { return (string)GetValue(DisplayTextPathProperty); }
            set { SetValue(DisplayTextPathProperty, value); }
        }

        public static readonly DependencyProperty DisplayTextPathProperty =
            DependencyProperty.Register("DisplayTextPath", typeof(string), typeof(CheckBoxListViewUserControl),
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
            DependencyProperty.Register("DescriptionPath", typeof(string), typeof(CheckBoxListViewUserControl),
                new PropertyMetadata("Description"));

        /// <summary>
        /// Path để lấy AdditionalInfo từ object
        /// </summary>
        public string AdditionalInfoPath
        {
            get { return (string)GetValue(AdditionalInfoPathProperty); }
            set { SetValue(AdditionalInfoPathProperty, value); }
        }

        public string AdditionalInfoPath2
        {
            get { return (string)GetValue(AdditionalInfoPath2Property); }
            set { SetValue(AdditionalInfoPath2Property, value); }
        }

        public static readonly DependencyProperty AdditionalInfoPathProperty =
            DependencyProperty.Register("AdditionalInfoPath", typeof(string), typeof(CheckBoxListViewUserControl),
                new PropertyMetadata("AdditionalInfo"));

        public static readonly DependencyProperty AdditionalInfoPath2Property =
            DependencyProperty.Register("AdditionalInfoPath2", typeof(string), typeof(CheckBoxListViewUserControl),
                new PropertyMetadata("AdditionalInfo2"));

        /// <summary>
        /// Path để lấy thuộc tính IsSelected từ object
        /// </summary>
        public string IsSelectedPath
        {
            get { return (string)GetValue(IsSelectedPathProperty); }
            set { SetValue(IsSelectedPathProperty, value); }
        }

        public static readonly DependencyProperty IsSelectedPathProperty =
            DependencyProperty.Register("IsSelectedPath", typeof(string), typeof(CheckBoxListViewUserControl),
                new PropertyMetadata("IsSelected"));

        /// <summary>
        /// Item được chọn hiện tại
        /// </summary>
        public object SelectedItem
        {
            get { return GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(object), typeof(CheckBoxListViewUserControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>
        /// Command khi selection thay đổi
        /// </summary>
        public ICommand SelectionChangedCommand
        {
            get { return (ICommand)GetValue(SelectionChangedCommandProperty); }
            set { SetValue(SelectionChangedCommandProperty, value); }
        }

        public static readonly DependencyProperty SelectionChangedCommandProperty =
            DependencyProperty.Register("SelectionChangedCommand", typeof(ICommand), typeof(CheckBoxListViewUserControl),
                new PropertyMetadata(null));

        /// <summary>
        /// Command khi có sự thay đổi về checked items
        /// </summary>
        public ICommand CheckedItemsChangedCommand
        {
            get { return (ICommand)GetValue(CheckedItemsChangedCommandProperty); }
            set { SetValue(CheckedItemsChangedCommandProperty, value); }
        }

        public static readonly DependencyProperty CheckedItemsChangedCommandProperty =
            DependencyProperty.Register("CheckedItemsChangedCommand", typeof(ICommand), typeof(CheckBoxListViewUserControl),
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
            DependencyProperty.Register("MaxHeight", typeof(double), typeof(CheckBoxListViewUserControl),
                new PropertyMetadata(200.0));

        /// <summary>
        /// Chiều cao tối thiểu
        /// </summary>
        public new double MinHeight
        {
            get { return (double)GetValue(MinHeightProperty); }
            set { SetValue(MinHeightProperty, value); }
        }

        public new static readonly DependencyProperty MinHeightProperty =
            DependencyProperty.Register("MinHeight", typeof(double), typeof(CheckBoxListViewUserControl),
                new PropertyMetadata(50.0));

        #endregion

        #region Event Handlers

        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingSelectAll) return;
            SetAllItemsSelected(true);
        }

        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingSelectAll) return;
            SetAllItemsSelected(false);
        }

        private void ItemCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSelectAllState();
            CheckedItemsChangedCommand?.Execute(GetCheckedItems());
        }

        #endregion

        #region Private Methods

        private void SetAllItemsSelected(bool isSelected)
        {
            if (ItemsSource == null) return;

            foreach (var item in ItemsSource)
            {
                if (item != null)
                {
                    var property = item.GetType().GetProperty(IsSelectedPath);
                    if (property != null && property.CanWrite)
                    {
                        property.SetValue(item, isSelected);
                    }
                }
            }

            // Trigger UI update
            MainListBox?.Items.Refresh();
            CheckedItemsChangedCommand?.Execute(GetCheckedItems());
        }

        private void UpdateSelectAllState()
        {
            if (ItemsSource == null)
            {
                _isUpdatingSelectAll = true;
                SelectAllCheckBox.IsChecked = false;
                _isUpdatingSelectAll = false;
                return;
            }

            var items = ItemsSource.Cast<object>().ToList();
            if (!items.Any())
            {
                _isUpdatingSelectAll = true;
                SelectAllCheckBox.IsChecked = false;
                _isUpdatingSelectAll = false;
                return;
            }

            var checkedCount = 0;
            var totalCount = items.Count;

            foreach (var item in items)
            {
                if (item != null)
                {
                    var property = item.GetType().GetProperty(IsSelectedPath);
                    if (property != null && property.CanRead)
                    {
                        var isSelected = property.GetValue(item);
                        if (isSelected is bool selected && selected)
                        {
                            checkedCount++;
                        }
                    }
                }
            }

            _isUpdatingSelectAll = true;
            if (checkedCount == 0)
            {
                SelectAllCheckBox.IsChecked = false;
            }
            else if (checkedCount == totalCount)
            {
                SelectAllCheckBox.IsChecked = true;
            }
            else
            {
                SelectAllCheckBox.IsChecked = null; // Indeterminate state
            }
            _isUpdatingSelectAll = false;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Lấy danh sách các items được check
        /// </summary>
        public IList<object> GetCheckedItems()
        {
            var checkedItems = new List<object>();

            if (ItemsSource != null)
            {
                foreach (var item in ItemsSource)
                {
                    if (item != null)
                    {
                        var property = item.GetType().GetProperty(IsSelectedPath);
                        if (property != null && property.CanRead)
                        {
                            var isSelected = property.GetValue(item);
                            if (isSelected is bool selected && selected)
                            {
                                checkedItems.Add(item);
                            }
                        }
                    }
                }
            }

            return checkedItems;
        }

        /// <summary>
        /// Lấy số lượng items được check
        /// </summary>
        public int GetCheckedItemsCount()
        {
            return GetCheckedItems().Count;
        }

        /// <summary>
        /// Set trạng thái selected cho một item cụ thể
        /// </summary>
        public void SetItemSelected(object item, bool isSelected)
        {
            if (item == null) return;

            var property = item.GetType().GetProperty(IsSelectedPath);
            if (property != null && property.CanWrite)
            {
                property.SetValue(item, isSelected);
                MainListBox?.Items.Refresh();
                UpdateSelectAllState();
                CheckedItemsChangedCommand?.Execute(GetCheckedItems());
            }
        }

        /// <summary>
        /// Clear tất cả selections
        /// </summary>
        [RelayCommand]
        public void ClearAllSelections()
        {
            SetAllItemsSelected(false);
        }

        /// <summary>
        /// Select tất cả items
        /// </summary>
        [RelayCommand]
        public void SelectAllItems()
        {
            SetAllItemsSelected(true);
        }

        /// <summary>
        /// Refresh items
        /// </summary>
        [RelayCommand]
        public void RefreshItems()
        {
            MainListBox?.Items.Refresh();
            UpdateSelectAllState();
        }

        /// <summary>
        /// Lấy ListBox bên trong để truy cập thêm tính năng nếu cần
        /// </summary>
        public ListBox GetInternalListBox()
        {
            return MainListBox;
        }

        /// <summary>
        /// Scroll đến item được chỉ định
        /// </summary>
        [RelayCommand]
        public void ScrollIntoView(object item)
        {
            MainListBox?.ScrollIntoView(item);
        }

        /// <summary>
        /// Toggle selection của một item với validation
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanToggleSelection))]
        public void ToggleSelection(object item)
        {
            if (item == null) return;

            var property = item.GetType().GetProperty(IsSelectedPath);
            if (property != null && property.CanRead && property.CanWrite)
            {
                var currentValue = property.GetValue(item);
                if (currentValue is bool isSelected)
                {
                    property.SetValue(item, !isSelected);
                    MainListBox?.Items.Refresh();
                    UpdateSelectAllState();
                    CheckedItemsChangedCommand?.Execute(GetCheckedItems());
                }
            }
        }

        private bool CanToggleSelection(object item)
        {
            if (item == null) return false;
            var property = item.GetType().GetProperty(IsSelectedPath);
            return property != null && property.CanRead && property.CanWrite;
        }

        /// <summary>
        /// Batch update - Set multiple items selected/unselected
        /// </summary>
        public void BatchUpdateSelection(IEnumerable<object> items, bool isSelected)
        {
            if (items == null) return;

            var hasChanges = false;
            foreach (var item in items)
            {
                if (item != null)
                {
                    var property = item.GetType().GetProperty(IsSelectedPath);
                    if (property != null && property.CanWrite)
                    {
                        property.SetValue(item, isSelected);
                        hasChanges = true;
                    }
                }
            }

            if (hasChanges)
            {
                MainListBox?.Items.Refresh();
                UpdateSelectAllState();
                CheckedItemsChangedCommand?.Execute(GetCheckedItems());
            }
        }

        [RelayCommand]
        private void SetItemSelectedCommand(object parameter)
        {
            // Expect parameter as object[] {item, isSelected}
            if (parameter is object[] args && args.Length == 2)
            {
                SetItemSelected(args[0], (bool)args[1]);
            }
        }

        [RelayCommand]
        private void BatchUpdateSelectionCommand(object parameter)
        {
            // Expect parameter as object[] {items, isSelected}
            if (parameter is object[] args && args.Length == 2)
            {
                BatchUpdateSelection((IEnumerable<object>)args[0], (bool)args[1]);
            }
        }

        /// <summary>
        /// Get checked items with type safety
        /// </summary>
        public IList<T> GetCheckedItems<T>() where T : class
        {
            var checkedItems = new List<T>();

            if (ItemsSource != null)
            {
                foreach (var item in ItemsSource)
                {
                    if (item is T typedItem)
                    {
                        var property = item.GetType().GetProperty(IsSelectedPath);
                        if (property != null && property.CanRead)
                        {
                            var isSelected = property.GetValue(item);
                            if (isSelected is bool selected && selected)
                            {
                                checkedItems.Add(typedItem);
                            }
                        }
                    }
                }
            }

            return checkedItems;
        }

        #endregion
    }
}