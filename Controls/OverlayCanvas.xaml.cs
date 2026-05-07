using FlowMy.Models.Nodes;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlowMy.Controls
{
    public partial class OverlayCanvas : UserControl
    {
        public OverlayCanvas()
        {
            InitializeComponent();
            Loaded += (_, _) => RebuildVisuals();
            SizeChanged += (_, _) => RefreshAllLayouts();
            PART_Surface.MouseLeftButtonDown += Surface_MouseLeftButtonDown;
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(ObservableCollection<OverlayItem>), typeof(OverlayCanvas),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(OverlayItem), typeof(OverlayCanvas),
                new PropertyMetadata(null, OnSelectedItemChanged));

        public ObservableCollection<OverlayItem>? ItemsSource
        {
            get => (ObservableCollection<OverlayItem>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public OverlayItem? SelectedItem
        {
            get => (OverlayItem?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public event EventHandler<OverlayItem?>? SelectionChanged;

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (OverlayCanvas)d;
            if (e.OldValue is ObservableCollection<OverlayItem> oldItems)
            {
                oldItems.CollectionChanged -= control.ItemsSource_CollectionChanged;
            }

            if (e.NewValue is ObservableCollection<OverlayItem> newItems)
            {
                newItems.CollectionChanged += control.ItemsSource_CollectionChanged;
            }

            control.RebuildVisuals();
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((OverlayCanvas)d).SyncSelectionToModel((OverlayItem?)e.NewValue);
        }

        private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildVisuals();
        }

        private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == PART_Surface)
            {
                SelectedItem = null;
                SyncSelectionToModel(null);
                SelectionChanged?.Invoke(this, null);
            }
        }

        private void RebuildVisuals()
        {
            PART_Surface.Children.Clear();
            if (ItemsSource == null) return;

            foreach (var item in ItemsSource)
            {
                var itemControl = new OverlayItemControl
                {
                    Item = item,
                    ParentSurfaceWidth = Math.Max(1, PART_Surface.ActualWidth),
                    ParentSurfaceHeight = Math.Max(1, PART_Surface.ActualHeight)
                };
                itemControl.ItemChanged += OverlayItemControl_ItemChanged;
                itemControl.ItemSelected += OverlayItemControl_ItemSelected;
                item.PropertyChanged += OverlayItem_PropertyChanged;
                PART_Surface.Children.Add(itemControl);
                UpdateLayoutForItem(itemControl);
            }
        }

        private void OverlayItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not OverlayItem item) return;
            var control = PART_Surface.Children.OfType<OverlayItemControl>().FirstOrDefault(c => ReferenceEquals(c.Item, item));
            if (control == null) return;
            UpdateLayoutForItem(control);
            // Layout-only changes should not trigger full visual refresh (e.g., reloading images) during drag.
            var prop = e.PropertyName ?? string.Empty;
            if (prop is nameof(OverlayItem.X) or nameof(OverlayItem.Y) or nameof(OverlayItem.Width) or nameof(OverlayItem.Height))
                return;
            control.RefreshViewFromItem();
        }

        private void OverlayItemControl_ItemChanged(object? sender, EventArgs e)
        {
            if (sender is not OverlayItemControl control) return;
            UpdateLayoutForItem(control);
        }

        private void OverlayItemControl_ItemSelected(object? sender, EventArgs e)
        {
            if (sender is not OverlayItemControl control || control.Item == null) return;
            SelectedItem = control.Item;
            SyncSelectionToModel(control.Item);
            SelectionChanged?.Invoke(this, control.Item);
        }

        private void SyncSelectionToModel(OverlayItem? selected)
        {
            if (ItemsSource == null) return;
            foreach (var item in ItemsSource)
            {
                item.IsSelected = ReferenceEquals(item, selected);
            }

            foreach (var child in PART_Surface.Children.OfType<OverlayItemControl>())
            {
                child.RefreshViewFromItem();
            }
        }

        private void RefreshAllLayouts()
        {
            foreach (var itemControl in PART_Surface.Children.OfType<OverlayItemControl>())
            {
                itemControl.ParentSurfaceWidth = Math.Max(1, PART_Surface.ActualWidth);
                itemControl.ParentSurfaceHeight = Math.Max(1, PART_Surface.ActualHeight);
                UpdateLayoutForItem(itemControl);
            }
        }

        private void UpdateLayoutForItem(OverlayItemControl itemControl)
        {
            if (itemControl.Item == null) return;

            var surfaceWidth = Math.Max(1, PART_Surface.ActualWidth);
            var surfaceHeight = Math.Max(1, PART_Surface.ActualHeight);
            var width = Math.Max(18, itemControl.Item.Width * surfaceWidth);
            var height = Math.Max(18, itemControl.Item.Height * surfaceHeight);
            var x = itemControl.Item.X * surfaceWidth;
            var y = itemControl.Item.Y * surfaceHeight;

            itemControl.Width = width;
            itemControl.Height = height;
            Canvas.SetLeft(itemControl, x);
            Canvas.SetTop(itemControl, y);
        }
    }
}
