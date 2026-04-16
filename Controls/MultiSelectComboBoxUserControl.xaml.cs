using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FlowMy.Controls
{
    /// <summary>
    /// MultiSelectComboBoxUserControl - ComboBox cho phép chọn nhiều item với tính năng tìm kiếm
    /// </summary>
    public partial class MultiSelectComboBoxUserControl : UserControl, INotifyPropertyChanged
    {
        private bool _isUpdatingSelection = false;
        private DispatcherTimer _debounceTimer;

        public MultiSelectComboBoxUserControl()
        {
            InitializeComponent();
            ListItems = new ObservableCollection<SelectableItemViewModel>();
            SelectedValues = new ObservableCollection<object>();

            // Khởi tạo FilteredItems và set vào DependencyProperty
            var filteredItems = new ObservableCollection<SelectableItemViewModel>();
            SetValue(FilteredItemsProperty, filteredItems);

            // Setup debounce timer for search
            _debounceTimer = new DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(300);
            _debounceTimer.Tick += DebounceTimer_Tick;

            this.Loaded += MultiSelectComboBoxUserControl_Loaded;
            this.Unloaded += MultiSelectComboBoxUserControl_Unloaded;
        }

        private void MultiSelectComboBoxUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            MainComboBox.PreviewMouseDown += MainComboBox_PreviewMouseDown;
            this.PreviewMouseWheel += MultiSelectComboBoxUserControl_PreviewMouseWheel;

            // Ensure FilteredItems is properly initialized
            if (FilteredItems == null)
            {
                FilteredItems = new ObservableCollection<SelectableItemViewModel>();
            }

            FilterItems(); // Apply initial filter
        }

        private void MultiSelectComboBoxUserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (MainComboBox != null)
            {
                MainComboBox.PreviewMouseDown -= MainComboBox_PreviewMouseDown;
            }
            this.PreviewMouseWheel -= MultiSelectComboBoxUserControl_PreviewMouseWheel;

            // Cleanup timer
            _debounceTimer?.Stop();
            _debounceTimer = null;
        }

        #region Mouse Events

        private void MainComboBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (!MainComboBox.IsDropDownOpen)
                {
                    var toggleButton = FindVisualChild<ToggleButton>(MainComboBox);
                    if (toggleButton != null)
                    {
                        var position = e.GetPosition(toggleButton);
                        var rect = new Rect(0, 0, toggleButton.ActualWidth, toggleButton.ActualHeight);

                        if (rect.Contains(position))
                        {
                            return;
                        }
                    }

                    MainComboBox.IsDropDownOpen = true;
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MainComboBox_PreviewMouseDown: {ex.Message}");
            }
        }

        private void MultiSelectComboBoxUserControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (MainComboBox.IsDropDownOpen)
            {
                var popup = FindVisualChild<Popup>(MainComboBox);
                if (popup != null)
                {
                    var scrollViewer = FindVisualChild<ScrollViewer>(popup.Child);
                    if (scrollViewer != null)
                    {
                        if (e.Delta > 0)
                        {
                            scrollViewer.LineUp();
                            scrollViewer.LineUp();
                            scrollViewer.LineUp();
                        }
                        else
                        {
                            scrollViewer.LineDown();
                            scrollViewer.LineDown();
                            scrollViewer.LineDown();
                        }
                        e.Handled = true;
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            try
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);

                    if (child is T typedChild)
                    {
                        return typedChild;
                    }

                    var foundChild = FindVisualChild<T>(child);
                    if (foundChild != null)
                        return foundChild;
                }
            }
            catch
            {
                // Ignore errors when traversing visual tree
            }

            return null;
        }

        #endregion

        #region Dependency Properties

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(MultiSelectComboBoxUserControl),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty DisplayMemberPathProperty =
            DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(MultiSelectComboBoxUserControl),
                new PropertyMetadata("Name", OnDisplayMemberPathChanged));

        public static readonly DependencyProperty SelectedValuesProperty =
            DependencyProperty.Register(nameof(SelectedValues), typeof(ObservableCollection<object>), typeof(MultiSelectComboBoxUserControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedValuesChanged));

        public static readonly DependencyProperty SelectedValuePathProperty =
            DependencyProperty.Register(nameof(SelectedValuePath), typeof(string), typeof(MultiSelectComboBoxUserControl),
                new PropertyMetadata("Id"));

        public static readonly DependencyProperty ListItemsProperty =
            DependencyProperty.Register(nameof(ListItems), typeof(ObservableCollection<SelectableItemViewModel>), typeof(MultiSelectComboBoxUserControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(MultiSelectComboBoxUserControl),
                new PropertyMetadata("Chọn các mục..."));

        public static readonly DependencyProperty SeparatorProperty =
            DependencyProperty.Register(nameof(Separator), typeof(string), typeof(MultiSelectComboBoxUserControl),
                new PropertyMetadata(", "));

        public static readonly DependencyProperty MaxDisplayItemsProperty =
            DependencyProperty.Register(nameof(MaxDisplayItems), typeof(int), typeof(MultiSelectComboBoxUserControl),
                new PropertyMetadata(3));

        public static readonly DependencyProperty FilteredItemsProperty =
            DependencyProperty.Register(nameof(FilteredItems), typeof(ObservableCollection<SelectableItemViewModel>), typeof(MultiSelectComboBoxUserControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(MultiSelectComboBoxUserControl),
                new PropertyMetadata(string.Empty, OnSearchTextChanged));

        #endregion

        #region Properties

        public IEnumerable ItemsSource
        {
            get { return (IEnumerable)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public string DisplayMemberPath
        {
            get { return (string)GetValue(DisplayMemberPathProperty); }
            set { SetValue(DisplayMemberPathProperty, value); }
        }

        public ObservableCollection<object> SelectedValues
        {
            get { return (ObservableCollection<object>)GetValue(SelectedValuesProperty); }
            set { SetValue(SelectedValuesProperty, value); }
        }

        public string SelectedValuePath
        {
            get { return (string)GetValue(SelectedValuePathProperty); }
            set { SetValue(SelectedValuePathProperty, value); }
        }

        public ObservableCollection<SelectableItemViewModel> ListItems
        {
            get { return (ObservableCollection<SelectableItemViewModel>)GetValue(ListItemsProperty); }
            set { SetValue(ListItemsProperty, value); }
        }

        public string PlaceholderText
        {
            get { return (string)GetValue(PlaceholderTextProperty); }
            set { SetValue(PlaceholderTextProperty, value); }
        }

        public string Separator
        {
            get { return (string)GetValue(SeparatorProperty); }
            set { SetValue(SeparatorProperty, value); }
        }

        public int MaxDisplayItems
        {
            get { return (int)GetValue(MaxDisplayItemsProperty); }
            set { SetValue(MaxDisplayItemsProperty, value); }
        }

        public ObservableCollection<SelectableItemViewModel> FilteredItems
        {
            get { return (ObservableCollection<SelectableItemViewModel>)GetValue(FilteredItemsProperty); }
            set { SetValue(FilteredItemsProperty, value); }
        }

        public string SearchText
        {
            get { return (string)GetValue(SearchTextProperty); }
            set { SetValue(SearchTextProperty, value); }
        }

        // Computed Properties
        public string SelectedItemsDisplayText
        {
            get
            {
                var selectedItems = ListItems?.Where(x => x.IsSelected).ToList() ?? new List<SelectableItemViewModel>();

                if (!selectedItems.Any())
                    return string.Empty;

                if (selectedItems.Count == ListItems?.Count)
                {
                    return "Đã chọn toàn bộ";
                }
                else if (selectedItems.Count <= MaxDisplayItems)
                {
                    return string.Join(Separator, selectedItems.Select(x => x.DisplayText));
                }
                else
                {
                    var firstItems = selectedItems.Take(MaxDisplayItems).Select(x => x.DisplayText);
                    return string.Join(Separator, firstItems) + $" và {selectedItems.Count - MaxDisplayItems} mục khác";
                }
            }
        }

        public bool HasSelectedItems
        {
            get { return ListItems?.Any(x => x.IsSelected) == true; }
        }

        public string SelectedItemsCountText
        {
            get
            {
                var count = ListItems?.Count(x => x.IsSelected) ?? 0;
                var total = ListItems?.Count ?? 0;
                return $"Đã chọn: {count}/{total}";
            }
        }

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<MultiSelectChangedEventArgs> SelectionChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnSelectionChanged(List<SelectableItemViewModel> selectedItems)
        {
            SelectionChanged?.Invoke(this, new MultiSelectChangedEventArgs(selectedItems));
        }

        #endregion

        #region Event Handlers

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MultiSelectComboBoxUserControl)d).BuildListItems();
        }

        private static void OnDisplayMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MultiSelectComboBoxUserControl)d).BuildListItems();
        }

        private static void OnSelectedValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MultiSelectComboBoxUserControl)d;
            if (!control._isUpdatingSelection)
            {
                control.UpdateSelectedItemsFromValues();
            }
        }

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MultiSelectComboBoxUserControl)d;
            control._debounceTimer?.Stop();
            control._debounceTimer?.Start();
        }

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer?.Stop();
            FilterItems();
        }

        // Improved item selection handling - uses Border click instead of CheckBox events
        private void ItemBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is SelectableItemViewModel item)
            {
                // Toggle the selection state
                item.IsSelected = !item.IsSelected;

                // Update selection immediately
                UpdateSelectionFromUI();
                NotifyDisplayPropertiesChanged();
                // Prevent event from bubbling up
                e.Handled = true;
            }
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingSelection = true;
            try
            {
                var itemsToSelect = string.IsNullOrWhiteSpace(SearchText)
                    ? ListItems?.ToList() ?? new List<SelectableItemViewModel>()
                    : FilteredItems?.ToList() ?? new List<SelectableItemViewModel>();

                foreach (var item in itemsToSelect)
                {
                    item.IsSelected = true;
                }
                UpdateSelectionFromUI();
                NotifyDisplayPropertiesChanged();
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            _isUpdatingSelection = true;
            try
            {
                var itemsToClear = string.IsNullOrWhiteSpace(SearchText)
                    ? ListItems?.ToList() ?? new List<SelectableItemViewModel>()
                    : FilteredItems?.ToList() ?? new List<SelectableItemViewModel>();

                foreach (var item in itemsToClear)
                {
                    item.IsSelected = false;
                }
                UpdateSelectionFromUI();
                NotifyDisplayPropertiesChanged();
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                SearchText = textBox.Text;
            }
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchText = string.Empty;
        }
        private void NotifyDisplayPropertiesChanged()
        {
            OnPropertyChanged(nameof(SelectedItemsDisplayText));
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(SelectedItemsCountText));
        }


        #endregion

        #region Core Methods

        private void UpdateSelectedItemsFromValues()
        {
            if (SelectedValues == null || ListItems == null)
                return;

            _isUpdatingSelection = true;
            try
            {
                foreach (var item in ListItems)
                {
                    var itemValue = GetSelectedValue(item.OriginalItem);
                    item.IsSelected = SelectedValues.Any(v => Equals(v, itemValue));
                }

                // Notify UI updates immediately
                NotifyDisplayPropertiesChanged();
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }

        private void UpdateSelectionFromUI()
        {
            if (_isUpdatingSelection) return;

            _isUpdatingSelection = true;
            try
            {
                var selectedItems = ListItems?.Where(x => x.IsSelected).ToList() ?? new List<SelectableItemViewModel>();
                var selectedValues = selectedItems.Select(x => GetSelectedValue(x.OriginalItem)).Where(v => v != null).ToList();

                // Ensure SelectedValues is initialized
                if (SelectedValues == null)
                {
                    SelectedValues = new ObservableCollection<object>();
                    SetValue(SelectedValuesProperty, SelectedValues);
                }

                // Update SelectedValues collection efficiently
                SelectedValues.Clear();
                foreach (var value in selectedValues)
                {
                    SelectedValues.Add(value);
                }

                // Notify UI updates using Dispatcher to ensure UI thread
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    OnPropertyChanged(nameof(SelectedItemsDisplayText));
                    OnPropertyChanged(nameof(HasSelectedItems));
                    OnPropertyChanged(nameof(SelectedItemsCountText));
                }), DispatcherPriority.DataBind);

                // Raise event
                OnSelectionChanged(selectedItems);

                System.Diagnostics.Debug.WriteLine($"MultiSelectComboBox: Selected {selectedItems.Count} items");
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }

        private void BuildListItems()
        {
            if (ListItems == null)
            {
                ListItems = new ObservableCollection<SelectableItemViewModel>();
                SetValue(ListItemsProperty, ListItems);
            }

            ListItems.Clear();

            if (ItemsSource != null)
            {
                foreach (var item in ItemsSource)
                {
                    var selectableItem = new SelectableItemViewModel
                    {
                        OriginalItem = item,
                        DisplayText = GetDisplayText(item),
                        IsSelected = false
                    };

                    ListItems.Add(selectableItem);
                }
            }

            // Apply filter after building items
            FilterItems();

            // Restore selection if exists
            if (SelectedValues?.Any() == true)
            {
                UpdateSelectedItemsFromValues();
            }

            // Notify UI updates
            OnPropertyChanged(nameof(SelectedItemsDisplayText));
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(SelectedItemsCountText));
        }

        private void FilterItems()
        {
            if (FilteredItems == null)
            {
                var filteredItems = new ObservableCollection<SelectableItemViewModel>();
                SetValue(FilteredItemsProperty, filteredItems);
            }

            FilteredItems.Clear();

            if (ListItems == null) return;

            var filteredList = string.IsNullOrWhiteSpace(SearchText)
                ? ListItems.ToList()
                : ListItems.Where(item => item.DisplayText.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var item in filteredList)
            {
                FilteredItems.Add(item);
            }

            System.Diagnostics.Debug.WriteLine($"FilterItems: {FilteredItems.Count} items after filter with search text: '{SearchText}'");
        }

        private string GetDisplayText(object item)
        {
            if (item == null) return string.Empty;

            try
            {
                if (DisplayMemberPath == "ToString")
                {
                    return item.ToString();
                }

                var property = item.GetType().GetProperty(DisplayMemberPath);
                return property?.GetValue(item)?.ToString() ?? item.ToString();
            }
            catch
            {
                return item.ToString();
            }
        }

        private object GetSelectedValue(object item)
        {
            if (item == null) return null;

            if (string.IsNullOrEmpty(SelectedValuePath))
            {
                return item;
            }

            try
            {
                var property = item.GetType().GetProperty(SelectedValuePath);
                var value = property?.GetValue(item) ?? item;
                return value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSelectedValue Error: {ex.Message}");
                return item;
            }
        }

        #endregion

        #region Public Methods

        public void SelectAll()
        {
            foreach (var item in ListItems)
            {
                item.IsSelected = true;
            }
            UpdateSelectionFromUI();
        }

        public void ClearSelection()
        {
            foreach (var item in ListItems)
            {
                item.IsSelected = false;
            }
            UpdateSelectionFromUI();
        }

        public void SelectItems(IEnumerable<object> values)
        {
            if (values == null) return;

            foreach (var item in ListItems)
            {
                var itemValue = GetSelectedValue(item.OriginalItem);
                item.IsSelected = values.Any(v => Equals(v, itemValue));
            }
            UpdateSelectionFromUI();
        }

        public List<object> GetSelectedItems()
        {
            return ListItems?.Where(x => x.IsSelected)
                            .Select(x => x.OriginalItem)
                            .ToList() ?? new List<object>();
        }

        public List<T> GetSelectedItems<T>()
        {
            return ListItems?.Where(x => x.IsSelected)
                            .Select(x => x.OriginalItem)
                            .OfType<T>()
                            .ToList() ?? new List<T>();
        }

        public void RefreshItems()
        {
            BuildListItems();
        }

        #endregion
    }

    /// <summary>
    /// ViewModel cho selectable item với MVVM support
    /// </summary>
    public partial class SelectableItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string displayText = string.Empty;

        [ObservableProperty]
        private bool isSelected;

        public object OriginalItem { get; set; }
    }

    /// <summary>
    /// Event args cho khi selection thay đổi
    /// </summary>
    public class MultiSelectChangedEventArgs : EventArgs
    {
        public List<SelectableItemViewModel> SelectedItems { get; }

        public MultiSelectChangedEventArgs(List<SelectableItemViewModel> selectedItems)
        {
            SelectedItems = selectedItems ?? new List<SelectableItemViewModel>();
        }

        public List<object> GetOriginalItems()
        {
            return SelectedItems.Select(x => x.OriginalItem).ToList();
        }

        public List<T> GetOriginalItems<T>()
        {
            return SelectedItems.Select(x => x.OriginalItem).OfType<T>().ToList();
        }
    }
}

// trong viewmode hoặc model cần phải thêm trường (tên trường tuỳ ý đặt)
//public ObservableCollection<object> SelectedMenuIds { get; set; } = new ObservableCollection<object>();

//<controls:MultiSelectComboBoxUserControl
//    ItemsSource = "{Binding DynamicCollections[MenuItemTrees]}"
//    x:Name = "cbbMultiSelect"
//    DisplayMemberPath = "Title"
//    SelectedValues = "{Binding Data.SelectedMenuIds, 
//                           Mode = TwoWay, UpdateSourceTrigger = PropertyChanged}"
//    SelectedValuePath = "Id"
//    PlaceholderText = "-- chọn menu --"
//    Separator = ", "
//    MaxDisplayItems = "3" />