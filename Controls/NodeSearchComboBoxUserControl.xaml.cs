using FlowMy.Models;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Controls
{
    public partial class NodeSearchComboBoxUserControl : UserControl, INotifyPropertyChanged
    {
        private INotifyCollectionChanged? _notifyItemsSource;

        public NodeSearchComboBoxUserControl()
        {
            InitializeComponent();
            FilteredItems = new ObservableCollection<NodeSearchItemViewModel>();
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(NodeSearchComboBoxUserControl),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty SelectedValueProperty =
            DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(NodeSearchComboBoxUserControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedValueChanged));

        public static readonly DependencyProperty SelectedValuePathProperty =
            DependencyProperty.Register(nameof(SelectedValuePath), typeof(string), typeof(NodeSearchComboBoxUserControl),
                new PropertyMetadata("NodeId"));

        public static readonly DependencyProperty DisplayMemberPathProperty =
            DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(NodeSearchComboBoxUserControl),
                new PropertyMetadata("Title"));

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(NodeSearchComboBoxUserControl),
                new PropertyMetadata("Chọn node..."));

        public static readonly DependencyProperty IsDropDownOpenProperty =
            DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(NodeSearchComboBoxUserControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(NodeSearchComboBoxUserControl),
                new PropertyMetadata(string.Empty, OnSearchTextChanged));

        public static readonly DependencyProperty FilteredItemsProperty =
            DependencyProperty.Register(nameof(FilteredItems), typeof(ObservableCollection<NodeSearchItemViewModel>), typeof(NodeSearchComboBoxUserControl),
                new PropertyMetadata(null));

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public object? SelectedValue
        {
            get => GetValue(SelectedValueProperty);
            set => SetValue(SelectedValueProperty, value);
        }

        public string SelectedValuePath
        {
            get => (string)GetValue(SelectedValuePathProperty);
            set => SetValue(SelectedValuePathProperty, value);
        }

        public string DisplayMemberPath
        {
            get => (string)GetValue(DisplayMemberPathProperty);
            set => SetValue(DisplayMemberPathProperty, value);
        }

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public bool IsDropDownOpen
        {
            get => (bool)GetValue(IsDropDownOpenProperty);
            set => SetValue(IsDropDownOpenProperty, value);
        }

        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        public ObservableCollection<NodeSearchItemViewModel> FilteredItems
        {
            get => (ObservableCollection<NodeSearchItemViewModel>)GetValue(FilteredItemsProperty);
            set => SetValue(FilteredItemsProperty, value);
        }

        public bool HasSelection => SelectedItem != null;
        public string SelectedDisplayText => SelectedItem?.Title ?? string.Empty;
        public string SelectedIconKey => string.IsNullOrWhiteSpace(SelectedItem?.IconKey) ? "cog" : SelectedItem!.IconKey;
        public Brush SelectedNodeBrush => SelectedItem?.NodeBrush ?? (Application.Current.TryFindResource("SecondaryBrush") as Brush ?? Brushes.Gray);
        public Brush SelectedNodeTextBrush => SelectedItem?.IconFillBrush ?? (Application.Current.TryFindResource("TextOnPrimaryBrush") as Brush ?? Brushes.White);

        private NodeSearchItemViewModel? SelectedItem { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NodeSearchComboBoxUserControl)d;
            control.AttachItemsSourceChanged(e.OldValue as IEnumerable, e.NewValue as IEnumerable);
            control.RebuildItems();
        }

        private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NodeSearchComboBoxUserControl)d;
            control.SyncSelectionFromValue(e.NewValue);
        }

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NodeSearchComboBoxUserControl)d;
            control.ApplyFilter();
        }

        private void AttachItemsSourceChanged(IEnumerable? oldSource, IEnumerable? newSource)
        {
            if (_notifyItemsSource != null)
            {
                _notifyItemsSource.CollectionChanged -= OnItemsSourceCollectionChanged;
                _notifyItemsSource = null;
            }

            if (newSource is INotifyCollectionChanged notify)
            {
                _notifyItemsSource = notify;
                _notifyItemsSource.CollectionChanged += OnItemsSourceCollectionChanged;
            }
        }

        private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildItems();
        }

        private void RebuildItems()
        {
            if (FilteredItems == null)
            {
                FilteredItems = new ObservableCollection<NodeSearchItemViewModel>();
            }

            var items = ItemsSource?.Cast<object>()
                .Select(CreateViewModel)
                .Where(x => x != null)
                .Cast<NodeSearchItemViewModel>()
                .ToList() ?? new();

            _allItems = items;
            ApplyFilter();
            SyncSelectionFromValue(SelectedValue);
        }

        private List<NodeSearchItemViewModel> _allItems = new();

        private NodeSearchItemViewModel? CreateViewModel(object source)
        {
            if (source == null) return null;

            var title = GetPropertyValue(source, DisplayMemberPath);
            var nodeId = GetPropertyValue(source, SelectedValuePath);
            var type = GetPropertyValue(source, "NodeTypeDisplayName");
            var iconKey = GetPropertyValue(source, "IconKey");

            var nodeBrush = GetRawPropertyValue(source, "NodeBrush") as Brush;
            var nodeTextBrush = GetRawPropertyValue(source, "NodeTextBrush") as Brush;
            var nodeHoverBrush = GetRawPropertyValue(source, "NodeHoverBrush") as Brush;
            var nodeSelectedBrush = GetRawPropertyValue(source, "NodeSelectedBrush") as Brush;

            var subtitle = string.IsNullOrWhiteSpace(type)
                ? nodeId
                : $"{type} • {nodeId}";

            var resolvedIconFillBrush = nodeTextBrush ?? ResolveDefaultIconBrush();

            return new NodeSearchItemViewModel
            {
                OriginalItem = source,
                Value = GetRawPropertyValue(source, SelectedValuePath),
                Title = string.IsNullOrWhiteSpace(title) ? nodeId : title,
                Subtitle = subtitle,
                SearchText = $"{title} {nodeId} {type}".Trim(),
                IconKey = string.IsNullOrWhiteSpace(iconKey) ? "cog" : iconKey,
                NodeBrush = nodeBrush ?? (Application.Current.TryFindResource("SecondaryBrush") as Brush ?? Brushes.Gray),
                NodeTextBrush = resolvedIconFillBrush,
                IconFillBrush = resolvedIconFillBrush,
                NodeHoverBrush = nodeHoverBrush ?? nodeBrush ?? (Application.Current.TryFindResource("ComboBoxItemHoverBrush") as Brush ?? Brushes.LightGray),
                NodeSelectedBrush = nodeSelectedBrush ?? nodeBrush ?? (Application.Current.TryFindResource("ComboBoxItemSelectedBrush") as Brush ?? Brushes.Gray)
            };
        }

        private static Brush ResolveDefaultIconBrush()
        {
            return Application.Current?.TryFindResource("TextOnPrimaryBrush") as Brush
                ?? Brushes.White;
        }

        private static string GetPropertyValue(object source, string propertyName)
        {
            var value = GetRawPropertyValue(source, propertyName);
            return value?.ToString() ?? string.Empty;
        }

        private static object? GetRawPropertyValue(object source, string propertyName)
        {
            if (source == null || string.IsNullOrWhiteSpace(propertyName)) return source;

            var prop = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(source);
        }

        private void ApplyFilter()
        {
            if (FilteredItems == null) return;

            var keyword = (SearchText ?? string.Empty).Trim();
            var filtered = string.IsNullOrWhiteSpace(keyword)
                ? _allItems
                : _allItems.Where(x => x.SearchText.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

            FilteredItems.Clear();
            foreach (var item in filtered)
            {
                FilteredItems.Add(item);
            }
        }

        private void SyncSelectionFromValue(object? selectedValue)
        {
            SelectedItem = _allItems.FirstOrDefault(x => Equals(x.Value, selectedValue));
            NotifySelectionChanged();
        }

        private void NotifySelectionChanged()
        {
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedDisplayText));
            OnPropertyChanged(nameof(SelectedIconKey));
            OnPropertyChanged(nameof(SelectedNodeBrush));
            OnPropertyChanged(nameof(SelectedNodeTextBrush));
        }

        private void ItemsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItemsListBox.SelectedItem is not NodeSearchItemViewModel item) return;

            SelectedItem = item;
            SelectedValue = item.Value;
            IsDropDownOpen = false;
            SearchText = string.Empty;
            NotifySelectionChanged();
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedItem = null;
            SelectedValue = null;
            SearchText = string.Empty;
            IsDropDownOpen = false;

            if (ItemsListBox != null)
            {
                ItemsListBox.SelectedItem = null;
            }

            NotifySelectionChanged();
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class NodeSearchItemViewModel
    {
        public object? OriginalItem { get; set; }
        public object? Value { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public string IconKey { get; set; } = "cog";
        public Brush? NodeBrush { get; set; }
        public Brush? NodeTextBrush { get; set; }
        public Brush? IconFillBrush { get; set; }
        public Brush? NodeHoverBrush { get; set; }
        public Brush? NodeSelectedBrush { get; set; }
    }
}
