using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Controls
{
    /// <summary>
    /// TreeComboBoxUserControl - Modern ComboBox with hierarchical data display
    /// </summary>
    public partial class TreeComboBoxUserControl : UserControl, INotifyPropertyChanged
    {
        private bool _isUpdatingSelection = false;

        public TreeComboBoxUserControl()
        {
            InitializeComponent();
            HierarchicalItems = new ObservableCollection<TreeItemViewModel>();
            //DataContext = this; // comment dòng này vì nó làm mất binding data vào
            this.Loaded += TreeComboBoxUserControl_Loaded;
            this.Unloaded += TreeComboBoxUserControl_Unloaded;
        }

        private void TreeComboBoxUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            MainComboBox.PreviewMouseDown += MainComboBox_PreviewMouseDown;
            this.PreviewMouseWheel += TreeComboBoxUserControl_PreviewMouseWheel;
        }

        private void TreeComboBoxUserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (MainComboBox != null)
            {
                MainComboBox.PreviewMouseDown -= MainComboBox_PreviewMouseDown;
            }
            this.PreviewMouseWheel -= TreeComboBoxUserControl_PreviewMouseWheel;
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

        private void TreeComboBoxUserControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
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

        public static readonly DependencyProperty PathMemberPathProperty =
    DependencyProperty.Register(nameof(PathMemberPath), typeof(string), typeof(TreeComboBoxUserControl),
        new PropertyMetadata(string.Empty, OnPathMemberPathChanged));

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(TreeComboBoxUserControl),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty DisplayMemberPathProperty =
            DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(TreeComboBoxUserControl),
                new PropertyMetadata("Name", OnDisplayMemberPathChanged));

        public static readonly DependencyProperty ChildrenMemberPathProperty =
            DependencyProperty.Register(nameof(ChildrenMemberPath), typeof(string), typeof(TreeComboBoxUserControl),
                new PropertyMetadata("Children", OnChildrenMemberPathChanged));

        public static readonly DependencyProperty SelectedValueProperty =
            DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(TreeComboBoxUserControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedValueChanged));

        public static readonly DependencyProperty SelectedValuePathProperty =
            DependencyProperty.Register(nameof(SelectedValuePath), typeof(string), typeof(TreeComboBoxUserControl),
                new PropertyMetadata("Id"));

        public static readonly DependencyProperty HierarchicalItemsProperty =
            DependencyProperty.Register(nameof(HierarchicalItems), typeof(ObservableCollection<TreeItemViewModel>), typeof(TreeComboBoxUserControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedTreeItemProperty =
            DependencyProperty.Register(nameof(SelectedTreeItem), typeof(TreeItemViewModel), typeof(TreeComboBoxUserControl),
                new PropertyMetadata(null, OnSelectedTreeItemChanged));

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(TreeComboBoxUserControl),
                new PropertyMetadata("Chọn một mục..."));

        public static readonly DependencyProperty IsExpandedByDefaultProperty =
            DependencyProperty.Register(nameof(IsExpandedByDefault), typeof(bool), typeof(TreeComboBoxUserControl),
                new PropertyMetadata(true));

        #endregion

        #region Properties

        public string PathMemberPath
        {
            get { return (string)GetValue(PathMemberPathProperty); }
            set { SetValue(PathMemberPathProperty, value); }
        }

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

        public string ChildrenMemberPath
        {
            get { return (string)GetValue(ChildrenMemberPathProperty); }
            set { SetValue(ChildrenMemberPathProperty, value); }
        }

        public object SelectedValue
        {
            get { return GetValue(SelectedValueProperty); }
            set { SetValue(SelectedValueProperty, value); }
        }

        public string SelectedValuePath
        {
            get { return (string)GetValue(SelectedValuePathProperty); }
            set { SetValue(SelectedValuePathProperty, value); }
        }

        public ObservableCollection<TreeItemViewModel> HierarchicalItems
        {
            get { return (ObservableCollection<TreeItemViewModel>)GetValue(HierarchicalItemsProperty); }
            set { SetValue(HierarchicalItemsProperty, value); }
        }

        public TreeItemViewModel SelectedTreeItem
        {
            get { return (TreeItemViewModel)GetValue(SelectedTreeItemProperty); }
            set { SetValue(SelectedTreeItemProperty, value); }
        }

        public string PlaceholderText
        {
            get { return (string)GetValue(PlaceholderTextProperty); }
            set { SetValue(PlaceholderTextProperty, value); }
        }

        public bool IsExpandedByDefault
        {
            get { return (bool)GetValue(IsExpandedByDefaultProperty); }
            set { SetValue(IsExpandedByDefaultProperty, value); }
        }

        public string SelectedDisplayText
        {
            get { return SelectedTreeItem?.DisplayText ?? string.Empty; }
        }

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<TreeItemSelectedEventArgs> ItemSelected;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnItemSelected(TreeItemViewModel item)
        {
            ItemSelected?.Invoke(this, new TreeItemSelectedEventArgs(item));
        }

        #endregion

        #region Event Handlers

        private static void OnPathMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeComboBoxUserControl)d).BuildHierarchicalItems();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeComboBoxUserControl)d).BuildHierarchicalItems();
        }

        private static void OnDisplayMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeComboBoxUserControl)d).BuildHierarchicalItems();
        }

        private static void OnChildrenMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TreeComboBoxUserControl)d).BuildHierarchicalItems();
        }

        private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (TreeComboBoxUserControl)d;
            if (!control._isUpdatingSelection)
            {
                control.UpdateSelectedTreeItemFromValue(e.NewValue);
            }
        }

        private static void OnSelectedTreeItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (TreeComboBoxUserControl)d;
            var newItem = e.NewValue as TreeItemViewModel;
            var oldItem = e.OldValue as TreeItemViewModel;

            // Update UI states
            if (oldItem != null)
            {
                oldItem.IsSelected = false;
            }
            if (newItem != null)
            {
                newItem.IsSelected = true;
            }

            control.OnPropertyChanged(nameof(SelectedDisplayText));
            control.OnItemSelected(newItem);

            // Update SelectedValue if not already updating
            if (!control._isUpdatingSelection)
            {
                control._isUpdatingSelection = true;
                try
                {
                    var selectedValue = newItem != null ? control.GetSelectedValue(newItem.OriginalItem) : null;

                    // Debug log
                    System.Diagnostics.Debug.WriteLine($"TreeComboBox: Setting SelectedValue to {selectedValue}");

                    // SỬA: Dùng SetValue thay vì SetCurrentValue để trigger binding
                    control.SetValue(SelectedValueProperty, selectedValue);
                }
                finally
                {
                    control._isUpdatingSelection = false;
                }
            }
        }

        private void TreeViewItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem treeViewItem && treeViewItem.DataContext is TreeItemViewModel item)
            {
                SelectItem(item);
                MainComboBox.IsDropDownOpen = false;
                e.Handled = true;
            }
        }

        private void TreeViewItem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TreeViewItem treeViewItem &&
                treeViewItem.DataContext is TreeItemViewModel item)
            {
                SelectItem(item);
                MainComboBox.IsDropDownOpen = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                MainComboBox.IsDropDownOpen = false;
                e.Handled = true;
            }
        }

        #endregion

        #region Core Methods

        private void UpdateSelectedTreeItemFromValue(object newValue)
        {
            _isUpdatingSelection = true;
            try
            {
                var foundItem = newValue != null ? FindTreeItem(HierarchicalItems, newValue) : null;

                if (foundItem != SelectedTreeItem)
                {
                    SetCurrentValue(SelectedTreeItemProperty, foundItem);

                    if (foundItem != null)
                    {
                        ExpandParentNodes(foundItem);
                    }
                }
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }

        private void SelectItem(TreeItemViewModel item)
        {
            _isUpdatingSelection = true;
            try
            {
                ClearAllSelections(HierarchicalItems);

                // Set SelectedTreeItem trước
                SetValue(SelectedTreeItemProperty, item);

                // Sau đó set SelectedValue
                var selectedValue = item != null ? GetSelectedValue(item.OriginalItem) : null;
                System.Diagnostics.Debug.WriteLine($"SelectItem: Setting SelectedValue to {selectedValue}");
                SetValue(SelectedValueProperty, selectedValue);
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }

        private void BuildHierarchicalItems()
        {
            HierarchicalItems.Clear();

            if (ItemsSource != null)
            {
                foreach (var item in ItemsSource)
                {
                    var treeItem = CreateTreeItem(item, 0);
                    HierarchicalItems.Add(treeItem);
                }
            }

            // Restore selection if exists
            if (SelectedValue != null)
            {
                UpdateSelectedTreeItemFromValue(SelectedValue);
            }
        }

        private TreeItemViewModel CreateTreeItem(object item, int level)
        {
            var treeItem = new TreeItemViewModel
            {
                OriginalItem = item,
                DisplayText = GetDisplayText(item),
                Level = level,
                PathInfo = GetPathInfo(item),
                IsExpanded = IsExpandedByDefault
            };

            var children = GetChildren(item);
            if (children != null)
            {
                foreach (var child in children)
                {
                    treeItem.Children.Add(CreateTreeItem(child, level + 1));
                }
            }

            return treeItem;
        }

        private string GetPathInfo(object item)
        {
            if (item == null || string.IsNullOrEmpty(PathMemberPath)) return string.Empty;

            try
            {
                var property = item.GetType().GetProperty(PathMemberPath);
                return property?.GetValue(item)?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
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

        private IEnumerable GetChildren(object item)
        {
            if (item == null || string.IsNullOrEmpty(ChildrenMemberPath)) return null;

            try
            {
                var property = item.GetType().GetProperty(ChildrenMemberPath);
                return property?.GetValue(item) as IEnumerable;
            }
            catch
            {
                return null;
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

                // Debug log
                System.Diagnostics.Debug.WriteLine($"GetSelectedValue: Item={item}, Path={SelectedValuePath}, Value={value}");

                return value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetSelectedValue Error: {ex.Message}");
                return item;
            }
        }

        private TreeItemViewModel FindTreeItem(IEnumerable<TreeItemViewModel> items, object targetValue)
        {
            foreach (var item in items)
            {
                var itemValue = GetSelectedValue(item.OriginalItem);

                if (Equals(itemValue, targetValue))
                {
                    return item;
                }

                var found = FindTreeItem(item.Children, targetValue);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void ExpandParentNodes(TreeItemViewModel item)
        {
            if (item == null) return;

            var parent = FindParent(HierarchicalItems, item);
            while (parent != null)
            {
                parent.IsExpanded = true;
                parent = FindParent(HierarchicalItems, parent);
            }
        }

        private TreeItemViewModel FindParent(IEnumerable<TreeItemViewModel> items, TreeItemViewModel child)
        {
            foreach (var item in items)
            {
                if (item.Children.Contains(child))
                {
                    return item;
                }

                var found = FindParent(item.Children, child);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void ClearAllSelections(IEnumerable<TreeItemViewModel> items)
        {
            foreach (var item in items)
            {
                item.IsSelected = false;
                ClearAllSelections(item.Children);
            }
        }

        #endregion

        #region Public Methods

        public void ExpandAll()
        {
            SetExpandedState(HierarchicalItems, true);
        }

        public void CollapseAll()
        {
            SetExpandedState(HierarchicalItems, false);
        }

        public TreeItemViewModel FindItem(string searchText, bool caseSensitive = false)
        {
            return FindItemRecursive(HierarchicalItems, searchText, caseSensitive);
        }

        public void ClearSelection()
        {
            SetCurrentValue(SelectedValueProperty, null);
        }

        public void RefreshItems()
        {
            BuildHierarchicalItems();
        }

        #endregion

        #region Private Utility Methods

        private void SetExpandedState(IEnumerable<TreeItemViewModel> items, bool isExpanded)
        {
            foreach (var item in items)
            {
                item.IsExpanded = isExpanded;
                SetExpandedState(item.Children, isExpanded);
            }
        }

        private TreeItemViewModel FindItemRecursive(IEnumerable<TreeItemViewModel> items, string searchText, bool caseSensitive)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            foreach (var item in items)
            {
                if (item.DisplayText.Contains(searchText, comparison))
                {
                    return item;
                }

                var found = FindItemRecursive(item.Children, searchText, caseSensitive);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        #endregion
    }

    /// <summary>
    /// ViewModel cho tree item với MVVM support
    /// </summary>
    public partial class TreeItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string displayText = string.Empty;

        [ObservableProperty]
        private bool isExpanded = true;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private int level;

        [ObservableProperty]
        private string pathInfo = string.Empty;

        public object OriginalItem { get; set; }

        public ObservableCollection<TreeItemViewModel> Children { get; set; } = new ObservableCollection<TreeItemViewModel>();

        public bool HasChildren => Children.Count > 0;

        public string IndentedDisplayText => new string(' ', Level * 2) + DisplayText;
    }

    /// <summary>
    /// Event args cho khi item được chọn
    /// </summary>
    public class TreeItemSelectedEventArgs : EventArgs
    {
        public TreeItemViewModel SelectedItem { get; }

        public TreeItemSelectedEventArgs(TreeItemViewModel selectedItem)
        {
            SelectedItem = selectedItem;
        }
    }
}