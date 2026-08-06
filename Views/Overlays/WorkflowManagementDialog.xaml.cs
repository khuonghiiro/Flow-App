using FlowMy.Services.Workflow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.VisualBasic;

namespace FlowMy.Views.Overlays
{
    public partial class WorkflowManagementDialog : Window
    {
        public ObservableCollection<WorkflowItem> Workflows { get; } = new();

        private ICollectionView? _collectionView;
        private string _searchText = string.Empty;
        private string _sortColumn = "Name";
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;
        private string _workflowsDir = string.Empty;
        private bool _isUpdatingSelection = false;
        private bool _isGridView = true;

        public WorkflowManagementDialog()
        {
            InitializeComponent();
            DataContext = this;
            LoadWorkflows();
            SetupCollectionView();
            SetViewMode(true); // Default to Grid view
            UpdateCountText();
            UpdateSortArrows();
            UpdateEmptyState();
            UpdateSelectionState();
        }

        private void LoadWorkflows()
        {
            foreach (var item in Workflows)
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }
            Workflows.Clear();

            _workflowsDir = FileWorkflowPersistenceService.GetDefaultWorkflowsDirectory();
            if (!Directory.Exists(_workflowsDir))
            {
                WorkflowFolderPathText.Text = _workflowsDir;
                return;
            }

            WorkflowFolderPathText.Text = _workflowsDir;

            var files = Directory.GetFiles(_workflowsDir, "*.json")
                .OrderBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
                .ToList();

            int index = 1;
            foreach (var file in files)
            {
                try
                {
                    var fi = new FileInfo(file);
                    var name = Path.GetFileNameWithoutExtension(file);
                    var item = new WorkflowItem
                    {
                        Index = index++,
                        Name = name,
                        FileName = fi.Name,
                        FilePath = file,
                        FolderPath = fi.DirectoryName ?? string.Empty,
                        FileSize = fi.Exists ? fi.Length : 0,
                        LastModified = fi.Exists ? fi.LastWriteTime : DateTime.MinValue,
                        LastModifiedFull = fi.Exists
                            ? fi.LastWriteTime.ToString("dd/MM/yyyy HH:mm:ss")
                            : "—"
                    };
                    item.PropertyChanged += Item_PropertyChanged;
                    Workflows.Add(item);
                }
                catch
                {
                    // skip unreadable files
                }
            }
        }

        private void SetupCollectionView()
        {
            _collectionView = CollectionViewSource.GetDefaultView(Workflows);
            _collectionView.Filter = FilterWorkflow;

            // Default sort by name
            _collectionView.SortDescriptions.Clear();
            _collectionView.SortDescriptions.Add(
                new SortDescription(_sortColumn, _sortDirection));

            WorkflowItemsControl.ItemsSource = _collectionView;
        }

        private bool FilterWorkflow(object obj)
        {
            if (obj is not WorkflowItem item)
                return false;

            if (string.IsNullOrWhiteSpace(_searchText))
                return true;

            return item.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WorkflowItem.IsSelected))
            {
                UpdateSelectionState();
            }
        }

        // ─── View Mode Switch ─────────────────────────────────────────

        private void ViewModeGrid_Click(object sender, RoutedEventArgs e)
        {
            SetViewMode(true);
        }

        private void ViewModeList_Click(object sender, RoutedEventArgs e)
        {
            SetViewMode(false);
        }

        private void SetViewMode(bool isGrid)
        {
            _isGridView = isGrid;

            var activeBg = FindResource("PrimaryGlowBrush") as System.Windows.Media.Brush;
            var normalBg = FindResource("ButtonBackgroundBrush") as System.Windows.Media.Brush;
            var activeBorder = FindResource("PrimaryBrush") as System.Windows.Media.Brush;
            var normalBorder = FindResource("ButtonBorderBrush") as System.Windows.Media.Brush;
            var primaryIconFill = FindResource("PrimaryBrush") as System.Windows.Media.Brush;
            var mutedIconFill = FindResource("TextMuted") as System.Windows.Media.Brush;

            GridViewButton.Background = isGrid ? activeBg : normalBg;
            GridViewButton.BorderBrush = isGrid ? activeBorder : normalBorder;

            ListViewButton.Background = !isGrid ? activeBg : normalBg;
            ListViewButton.BorderBrush = !isGrid ? activeBorder : normalBorder;

            if (isGrid)
            {
                var wrapPanelFactory = new FrameworkElementFactory(typeof(WrapPanel));
                wrapPanelFactory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
                WorkflowItemsControl.ItemsPanel = new ItemsPanelTemplate(wrapPanelFactory);
                WorkflowItemsControl.ItemTemplate = (DataTemplate)Resources["GridItemTemplate"];
            }
            else
            {
                var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
                WorkflowItemsControl.ItemsPanel = new ItemsPanelTemplate(stackPanelFactory);
                WorkflowItemsControl.ItemTemplate = (DataTemplate)Resources["ListItemTemplate"];
            }
        }

        // ─── Search ───────────────────────────────────────────────────

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = SearchBox.Text.Trim();
            _collectionView?.Refresh();
            ReindexWorkflows();
            UpdateCountText();
            UpdateEmptyState();
            UpdateSelectionState();

            ClearSearchButton.Visibility = string.IsNullOrWhiteSpace(_searchText)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            SearchBox.Focus();
        }

        // ─── Sort (dedicated buttons) ─────────────────────────────────

        private void SortByName_Click(object sender, RoutedEventArgs e)
        {
            ApplySort("Name");
        }

        private void SortByDate_Click(object sender, RoutedEventArgs e)
        {
            ApplySort("LastModified");
        }

        private void SortBySize_Click(object sender, RoutedEventArgs e)
        {
            ApplySort("FileSize");
        }

        private void ApplySort(string column)
        {
            // Toggle direction if clicking same column
            if (_sortColumn == column)
            {
                _sortDirection = _sortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                _sortColumn = column;
                _sortDirection = column == "LastModified"
                    ? ListSortDirection.Descending  // newest first by default
                    : ListSortDirection.Ascending;
            }

            _collectionView?.SortDescriptions.Clear();
            _collectionView?.SortDescriptions.Add(
                new SortDescription(_sortColumn, _sortDirection));

            UpdateSortArrows();
            ReindexWorkflows();
        }

        private void UpdateSortArrows()
        {
            string arrow = _sortDirection == ListSortDirection.Ascending ? " ▲" : " ▼";

            SortArrowName.Text = _sortColumn == "Name" ? arrow : "";
            SortArrowFileSize.Text = _sortColumn == "FileSize" ? arrow : "";
            SortArrowLastModified.Text = _sortColumn == "LastModified" ? arrow : "";

            // Highlight active sort button background
            var activeBg = FindResource("PrimaryGlowBrush") as System.Windows.Media.Brush;
            var normalBg = FindResource("ButtonBackgroundBrush") as System.Windows.Media.Brush;
            var activeBorder = FindResource("PrimaryBrush") as System.Windows.Media.Brush;
            var normalBorder = FindResource("ButtonBorderBrush") as System.Windows.Media.Brush;

            SortByNameButton.Background = _sortColumn == "Name" ? activeBg : normalBg;
            SortByNameButton.BorderBrush = _sortColumn == "Name" ? activeBorder : normalBorder;
            SortByDateButton.Background = _sortColumn == "LastModified" ? activeBg : normalBg;
            SortByDateButton.BorderBrush = _sortColumn == "LastModified" ? activeBorder : normalBorder;
            SortBySizeButton.Background = _sortColumn == "FileSize" ? activeBg : normalBg;
            SortBySizeButton.BorderBrush = _sortColumn == "FileSize" ? activeBorder : normalBorder;
        }

        // ─── Selection & Batch Delete ─────────────────────────────────

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingSelection) return;

            bool isChecked = SelectAllCheckBox.IsChecked == true;
            var visibleItems = _collectionView?.Cast<WorkflowItem>().ToList() ?? Workflows.ToList();

            _isUpdatingSelection = true;
            try
            {
                foreach (var item in visibleItems)
                {
                    item.IsSelected = isChecked;
                }
            }
            finally
            {
                _isUpdatingSelection = false;
            }

            UpdateSelectionState();
        }

        private void UpdateSelectionState()
        {
            if (_isUpdatingSelection) return;
            _isUpdatingSelection = true;

            try
            {
                var visibleItems = _collectionView?.Cast<WorkflowItem>().ToList() ?? Workflows.ToList();
                int totalVisible = visibleItems.Count;
                int selectedVisible = visibleItems.Count(w => w.IsSelected);
                int totalSelected = Workflows.Count(w => w.IsSelected);

                if (totalSelected > 0)
                {
                    DeleteSelectedButton.Visibility = Visibility.Visible;
                    DeleteSelectedText.Text = $"Xóa đã chọn ({totalSelected})";
                }
                else
                {
                    DeleteSelectedButton.Visibility = Visibility.Collapsed;
                }

                if (totalVisible > 0 && selectedVisible == totalVisible)
                {
                    SelectAllCheckBox.IsChecked = true;
                }
                else if (selectedVisible == 0)
                {
                    SelectAllCheckBox.IsChecked = false;
                }
                else
                {
                    SelectAllCheckBox.IsChecked = null;
                }
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = Workflows.Where(w => w.IsSelected).ToList();
            if (selectedItems.Count == 0) return;

            var result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa {selectedItems.Count} workflow đã chọn?\n\nHành động này không thể hoàn tác!",
                "Xác nhận xóa hàng loạt",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                int deletedCount = 0;
                var failedItems = new List<string>();

                foreach (var item in selectedItems)
                {
                    try
                    {
                        if (File.Exists(item.FilePath))
                        {
                            File.Delete(item.FilePath);
                        }
                        item.PropertyChanged -= Item_PropertyChanged;
                        Workflows.Remove(item);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        failedItems.Add($"{item.Name}: {ex.Message}");
                    }
                }

                if (failedItems.Count > 0)
                {
                    MessageBox.Show(
                        $"Đã xóa {deletedCount} workflow.\nKhông thể xóa {failedItems.Count} workflow:\n" + string.Join("\n", failedItems),
                        "Thông báo",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                ReindexWorkflows();
                UpdateCountText();
                UpdateEmptyState();
                UpdateSelectionState();
            }
        }

        // ─── Individual Actions ───────────────────────────────────────

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not WorkflowItem item)
                return;

            var oldName = item.Name;
            var newName = Interaction.InputBox(
                "Nhập tên mới cho workflow:",
                "Sửa tên workflow",
                oldName).Trim();

            if (string.IsNullOrWhiteSpace(newName) || newName == oldName)
                return;

            // Sanitize file name
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                newName = newName.Replace(c, '_');
            }

            if (string.IsNullOrWhiteSpace(newName))
                return;

            try
            {
                var oldPath = item.FilePath;
                var newPath = Path.Combine(Path.GetDirectoryName(oldPath)!, $"{newName}.json");

                // Check if new name already exists
                if (File.Exists(newPath) && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        $"Workflow với tên '{newName}' đã tồn tại!",
                        "Lỗi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Rename file
                File.Move(oldPath, newPath);

                // Update item
                var fi = new FileInfo(newPath);
                item.Name = newName;
                item.FileName = fi.Name;
                item.FilePath = newPath;
                item.FolderPath = fi.DirectoryName ?? string.Empty;
                item.LastModified = fi.LastWriteTime;
                item.LastModifiedFull = fi.LastWriteTime.ToString("dd/MM/yyyy HH:mm:ss");

                // Refresh view to re-sort
                _collectionView?.Refresh();
                ReindexWorkflows();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Lỗi khi đổi tên workflow: {ex.Message}",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not WorkflowItem item)
                return;

            var result = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa workflow '{item.Name}'?\n\nHành động này không thể hoàn tác!",
                "Xác nhận xóa",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (File.Exists(item.FilePath))
                    {
                        File.Delete(item.FilePath);
                    }

                    item.PropertyChanged -= Item_PropertyChanged;
                    Workflows.Remove(item);
                    ReindexWorkflows();
                    UpdateCountText();
                    UpdateEmptyState();
                    UpdateSelectionState();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Lỗi khi xóa workflow: {ex.Message}",
                        "Lỗi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not WorkflowItem item)
                return;

            try
            {
                if (File.Exists(item.FilePath))
                {
                    // Select the file in Explorer
                    Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
                }
                else if (Directory.Exists(item.FolderPath))
                {
                    Process.Start("explorer.exe", $"\"{item.FolderPath}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Không thể mở thư mục: {ex.Message}",
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenWorkflowsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(_workflowsDir))
                {
                    Process.Start("explorer.exe", $"\"{_workflowsDir}\"");
                }
            }
            catch { }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        // ─── Helpers ──────────────────────────────────────────────────

        private void ReindexWorkflows()
        {
            if (_collectionView == null) return;

            int index = 1;
            foreach (var item in _collectionView.OfType<WorkflowItem>())
            {
                item.Index = index++;
            }
        }

        private void UpdateCountText()
        {
            int total = Workflows.Count;
            int filtered = _collectionView?.Cast<WorkflowItem>().Count() ?? total;

            if (string.IsNullOrWhiteSpace(_searchText) || filtered == total)
            {
                WorkflowCountText.Text = $"{total} workflow{(total != 1 ? "s" : "")}";
            }
            else
            {
                WorkflowCountText.Text = $"{filtered} / {total} workflows";
            }
        }

        private void UpdateEmptyState()
        {
            int visibleCount = _collectionView?.Cast<WorkflowItem>().Count() ?? 0;

            EmptyStateOverlay.Visibility = visibleCount == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (visibleCount == 0 && !string.IsNullOrWhiteSpace(_searchText))
            {
                EmptyStateText.Text = $"Không tìm thấy workflow \"{_searchText}\"";
            }
            else
            {
                EmptyStateText.Text = "Không có workflow nào";
            }
        }
    }

    public class WorkflowItem : INotifyPropertyChanged
    {
        private int _index;
        private bool _isSelected;
        private string _name = string.Empty;
        private string _fileName = string.Empty;
        private string _filePath = string.Empty;
        private string _folderPath = string.Empty;
        private long _fileSize;
        private DateTime _lastModified;
        private string _lastModifiedFull = string.Empty;

        public int Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(nameof(Index)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(nameof(FileName)); }
        }

        public string FilePath
        {
            get => _filePath;
            set { _filePath = value; OnPropertyChanged(nameof(FilePath)); }
        }

        public string FolderPath
        {
            get => _folderPath;
            set { _folderPath = value; OnPropertyChanged(nameof(FolderPath)); }
        }

        public long FileSize
        {
            get => _fileSize;
            set { _fileSize = value; OnPropertyChanged(nameof(FileSize)); }
        }

        public DateTime LastModified
        {
            get => _lastModified;
            set { _lastModified = value; OnPropertyChanged(nameof(LastModified)); }
        }

        public string LastModifiedFull
        {
            get => _lastModifiedFull;
            set { _lastModifiedFull = value; OnPropertyChanged(nameof(LastModifiedFull)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
