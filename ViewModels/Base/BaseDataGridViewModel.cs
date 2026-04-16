//using FlowMy.Controls;
//using FlowMy.Helpers;
//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using System.Collections.ObjectModel;
//using System.Collections.Specialized;
//using System.ComponentModel;
//using System.Windows.Controls;
//using System.Windows.Threading;

//namespace FlowMy.ViewModels.Base
//{
//    // Tạo BatchObservableCollection class
//    public class BatchObservableCollection<T> : ObservableCollection<T>
//    {
//        private bool _suppressNotification = false;

//        public void BeginUpdate()
//        {
//            _suppressNotification = true;
//        }

//        public void EndUpdate()
//        {
//            _suppressNotification = false;
//            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
//            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
//            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
//        }

//        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
//        {
//            if (!_suppressNotification)
//                base.OnPropertyChanged(e);
//        }

//        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
//        {
//            if (!_suppressNotification)
//                base.OnCollectionChanged(e);
//        }

//        /// <summary>
//        /// Thay thế toàn bộ collection với dữ liệu mới một cách hiệu quả
//        /// </summary>
//        public void ReplaceAll(IEnumerable<T> newItems)
//        {
//            BeginUpdate();
//            try
//            {
//                Clear();
//                if (newItems != null)
//                {
//                    foreach (var item in newItems)
//                    {
//                        Add(item);
//                    }
//                }
//            }
//            finally
//            {
//                EndUpdate();
//            }
//        }

//        /// <summary>
//        /// Thêm nhiều items cùng lúc
//        /// </summary>
//        public void AddRange(IEnumerable<T> items)
//        {
//            if (items == null) return;

//            BeginUpdate();
//            try
//            {
//                foreach (var item in items)
//                {
//                    Add(item);
//                }
//            }
//            finally
//            {
//                EndUpdate();
//            }
//        }
//    }

//    // Cập nhật BaseDataGridViewModel
//    public abstract partial class BaseDataGridViewModel<TDetailDto, TGridDto, TUpdateDto, TCreateDto> : ObservableObject
//        where TDetailDto : class
//        where TGridDto : class
//        where TUpdateDto : class
//        where TCreateDto : class
//    {
//        private readonly Dispatcher _dispatcher;

//        #region Permission Properties

//        private readonly Dictionary<string, HashSet<string>> _dictPrivileges;

//        // Key hiện tại đang được chọn
//        protected string SelectedKey { get; private set; } = string.Empty;

//        // Danh sách string binding ra view (nếu cần)
//        [ObservableProperty]
//        private ObservableCollection<string> items = new();

//        // Các cờ bool để binding quyền
//        [ObservableProperty]
//        private bool isHasAdd = false;

//        [ObservableProperty]
//        private bool isHasView = false;

//        [ObservableProperty]
//        private bool isHasEdit = false;

//        [ObservableProperty]
//        private bool isHasDelete = false;

//        [ObservableProperty]
//        private bool isHasImport = false;

//        [ObservableProperty]
//        private bool isHasExport = false;

//        // Computed property để kiểm tra có nên hiển thị action column không
//        public bool IsShouldShowActionColumn => IsHasEdit || IsHasDelete;

//        #endregion

//        #region DataGrid Configuration

//        public ObservableCollection<DataGridColumn> DataGridColumns { get; set; } = new();

//        [ObservableProperty]
//        private ObservableCollection<ActionButtonDefinition>? actionButtons;

//        #endregion

//        private readonly IUserIdentity<int> _currentUser;

//        protected BaseDataGridViewModel(IUserIdentity<int> currentUser, string permissionKey)
//        {
//            _currentUser = currentUser;
//            _dispatcher = System.Windows.Application.Current?.Dispatcher;
//            _dictPrivileges = currentUser.Privileges;
//            SelectedKey = permissionKey;

//            SetPermissions(permissionKey);
//        }

//        #region Properties

//        // Thay đổi type từ ObservableCollection sang BatchObservableCollection
//        [ObservableProperty]
//        private BatchObservableCollection<TGridDto> pagedItems = new();

//        [ObservableProperty]
//        private TGridDto? selectedItem;

//        [ObservableProperty]
//        protected string displayPlaceholder = "Tìm kiếm dữ liệu... (Ấn Enter để tìm kiếm)";

//        [ObservableProperty]
//        private string searchKeyword = string.Empty;

//        [ObservableProperty]
//        private int currentPage = 1;

//        [ObservableProperty]
//        private int pageSize = 25;

//        [ObservableProperty]
//        private int totalPages = 1;

//        [ObservableProperty]
//        private long totalRecords;

//        [ObservableProperty]
//        private bool isLoading;

//        [ObservableProperty]
//        private string sortColumn = string.Empty;

//        [ObservableProperty]
//        private ListSortDirection sortDirection = ListSortDirection.Ascending;

//        ///
//        /// // Cách 1 Gán trực tiếp: PageSizeOptions = new ObservableCollection<int> { 10, 20, 50, 100, 200 };
//        /// // Cách 2 Sử dụng method helper với params: SetPageSizeOptions(10, 20, 50, 100, 200);
//        /// // Cách 3 Sử dụng method helper với collection:  SetPageSizeOptions(new List<int> { 10, 20, 50, 100, 200 });
//        /// </summary>
//        public ObservableCollection<int> PageSizeOptions { get; set; } = new() { 25, 50, 100, 250 };

//        public ObservableCollection<PageNumberInfo> PageNumbers { get; } = new();

//        // Computed properties for UI binding
//        public long StartRecord
//        {
//            get
//            {
//                if (TotalRecords <= 0 || CurrentPage <= 0 || PageSize <= 0)
//                    return 0;
//                return (CurrentPage - 1) * PageSize + 1;
//            }
//        }

//        public long EndRecord
//        {
//            get
//            {
//                if (TotalRecords <= 0 || CurrentPage <= 0 || PageSize <= 0)
//                    return 0;
//                return Math.Min(CurrentPage * PageSize, TotalRecords);
//            }
//        }

//        #endregion

//        #region Permission Methods

//        /// <summary>
//        /// Thiết lập quyền dựa trên key
//        /// </summary>
//        /// <param name="key">Permission key</param>
//        protected virtual void SetPermissions(string key)
//        {
//            SelectedKey = key;
//            OnSetKeyPrivileges(key);
//        }

//        /// <summary>
//        /// Xử lý thiết lập quyền khi đổi key
//        /// </summary>
//        /// <param name="value">Permission key</param>
//        protected virtual void OnSetKeyPrivileges(string value)
//        {
//            if (_currentUser.IsAdmin)
//            {
//                IsHasAdd = true;
//                IsHasEdit = true;
//                IsHasDelete = true;
//                IsHasView = true;
//                IsHasImport = true;
//                IsHasExport = true;
//            }
//            else if (!string.IsNullOrWhiteSpace(value) && _dictPrivileges.TryGetValue(value, out var permissions))
//            {
//                IsHasAdd = permissions.Contains(PermissionZipConst.Add);
//                IsHasEdit = permissions.Contains(PermissionZipConst.Edit);
//                IsHasDelete = permissions.Contains(PermissionZipConst.Delete);
//                IsHasView = permissions.Contains(PermissionZipConst.View);
//                IsHasImport = permissions.Contains(PermissionZipConst.ImportExcel);
//                IsHasExport = permissions.Contains(PermissionZipConst.ExportExcel);
//            }
//            else
//            {
//                // Reset lại quyền nếu không tìm thấy key
//                IsHasAdd = false;
//                IsHasEdit = false;
//                IsHasDelete = false;
//                IsHasView = false;
//                IsHasImport = false;
//                IsHasExport = false;
//            }

//            // Notify UI về việc thay đổi computed property
//            OnPropertyChanged(nameof(IsShouldShowActionColumn));

//            // Cấu hình lại columns sau khi quyền thay đổi
//            LoadConfigureColumns();
//            OnPropertyChanged(nameof(DataGridColumns));
//        }

//        /// <summary>
//        /// Kiểm tra quyền cho một hành động cụ thể
//        /// </summary>
//        /// <param name="permission">Permission cần kiểm tra</param>
//        /// <returns>True nếu có quyền</returns>
//        protected bool HasPermission(string permission)
//        {
//            return _dictPrivileges.TryGetValue(SelectedKey, out var permissions) &&
//                   permissions.Contains(permission);
//        }

//        /// <summary>
//        /// Kiểm tra có quyền Add không
//        /// </summary>
//        protected bool CanAdd => IsHasAdd;

//        /// <summary>
//        /// Kiểm tra có quyền Edit không
//        /// </summary>
//        protected bool CanEdit => IsHasEdit;

//        /// <summary>
//        /// Kiểm tra có quyền Delete không
//        /// </summary>
//        protected bool CanDelete => IsHasDelete;

//        /// <summary>
//        /// Kiểm tra có quyền View không
//        /// </summary>
//        protected bool CanView => IsHasView;

//        #endregion

//        #region Commands

//        [RelayCommand]
//        private async Task LoadDataAsync()
//        {
//            await LoadDataFromServerAsync();
//        }

//        [RelayCommand]
//        private async Task SearchAsync()
//        {
//            CurrentPage = 1;
//            await LoadDataFromServerAsync();
//        }

//        [RelayCommand]
//        public async Task ClearSearchAsync()
//        {
//            SearchKeyword = string.Empty;
//            CurrentPage = 1;
//            await LoadDataFromServerAsync();
//        }

//        [RelayCommand]
//        private async Task FirstPageAsync()
//        {
//            if (CurrentPage != 1)
//            {
//                CurrentPage = 1;
//                await LoadDataFromServerAsync();
//            }
//        }

//        [RelayCommand]
//        private async Task PreviousPageAsync()
//        {
//            if (CurrentPage > 1)
//            {
//                CurrentPage--;
//                await LoadDataFromServerAsync();
//            }
//        }

//        [RelayCommand]
//        private async Task NextPageAsync()
//        {
//            if (CurrentPage < TotalPages)
//            {
//                CurrentPage++;
//                await LoadDataFromServerAsync();
//            }
//        }

//        [RelayCommand]
//        private async Task LastPageAsync()
//        {
//            if (CurrentPage != TotalPages)
//            {
//                CurrentPage = TotalPages;
//                await LoadDataFromServerAsync();
//            }
//        }

//        [RelayCommand]
//        private async Task GoToPageAsync(int pageNumber)
//        {
//            if (pageNumber >= 1 && pageNumber <= TotalPages && pageNumber != CurrentPage)
//            {
//                CurrentPage = pageNumber;
//                await LoadDataFromServerAsync();
//            }
//        }

//        [RelayCommand]
//        private async Task ChangePage(int pageNumber)
//        {
//            await GoToPageAsync(pageNumber);
//        }

//        [RelayCommand(CanExecute = nameof(CanAdd))]
//        private void Add()
//        {
//            OnAdd();
//        }

//        [RelayCommand(CanExecute = nameof(CanEdit))]
//        private void Edit(TGridDto? item)
//        {
//            if (item != null)
//                OnEdit(item);
//        }

//        [RelayCommand]
//        private async Task SearchAdvancedAsync()
//        {
//            CurrentPage = 1;
//            await LoadDataFromServerAsync();
//        }

//        //[RelayCommand]
//        //private async Task AddAsync()
//        //{
//        //    await OnAddAsync();
//        //}

//        //[RelayCommand]
//        //private async Task EditAsync(TGridDto? item)
//        //{
//        //    if (item != null)
//        //        await OnEditAsync(item);
//        //}

//        [RelayCommand(CanExecute = nameof(CanDelete))]
//        private async Task DeleteAsync(TGridDto? item)
//        {
//            if (item != null)
//                await OnDeleteAsync(item);
//        }

//        [RelayCommand]
//        private async Task SortAsync(DataGridSortingEventArgs args)
//        {
//            if (args?.Column?.SortMemberPath is string columnName)
//            {
//                // ✅ Ngăn DataGrid tự động sort - chỉ sort khi data được load lại từ server
//                args.Handled = true;

//                if (SortColumn == columnName)
//                {
//                    SortDirection = SortDirection == ListSortDirection.Ascending
//                        ? ListSortDirection.Descending
//                        : ListSortDirection.Ascending;
//                }
//                else
//                {
//                    SortColumn = columnName;
//                    SortDirection = ListSortDirection.Ascending;
//                }
//                CurrentPage = 1;
//                await LoadDataFromServerAsync();
//            }
//        }

//        #endregion

//        #region Property Changed Handlers


//        partial void OnCurrentPageChanged(int oldValue, int newValue)
//        {
//            // Validate CurrentPage
//            if (newValue < 1)
//            {
//                CurrentPage = 1;
//                return;
//            }

//            if (TotalPages > 0 && newValue > TotalPages)
//            {
//                CurrentPage = TotalPages;
//                return;
//            }

//            UpdatePageNumbers();
//            OnPropertyChanged(nameof(StartRecord));
//            OnPropertyChanged(nameof(EndRecord));
//        }

//        partial void OnPageSizeChanged(int oldValue, int newValue)
//        {
//            if (newValue <= 0) return;

//            CurrentPage = 1;
//            _ = LoadDataFromServerAsync();
//        }

//        partial void OnTotalPagesChanged(int oldValue, int newValue)
//        {
//            // Validate current page against new total pages
//            if (CurrentPage > newValue && newValue > 0)
//            {
//                CurrentPage = newValue;
//            }

//            UpdatePageNumbers();
//            OnPropertyChanged(nameof(StartRecord));
//            OnPropertyChanged(nameof(EndRecord));
//        }

//        partial void OnTotalRecordsChanged(long oldValue, long newValue)
//        {
//            OnPropertyChanged(nameof(StartRecord));
//            OnPropertyChanged(nameof(EndRecord));
//        }

//        #endregion

//        #region Private Methods

//        private async Task ExecuteOnUIThreadAsync(Func<Task> action)
//        {
//            if (_dispatcher?.CheckAccess() == true)
//            {
//                await action();
//            }
//            else
//            {
//                await _dispatcher?.InvokeAsync(action);
//            }
//        }

//        private async Task LoadDataFromServerAsync()
//        {
//            await ExecuteOnUIThreadAsync(async () =>
//            {
//                IsLoading = true;
//                try
//                {
//                    var pagingRequest = new PagingRequest
//                    {
//                        CurrentPage = CurrentPage,
//                        PageSize = PageSize,
//                        SearchText = SearchKeyword,
//                        SortColumn = SortColumn,
//                        SortDirection = SortDirection
//                    };

//                    var pagingResult = await GetDataFromServerAsync(pagingRequest);

//                    if (pagingResult.Success)
//                    {
//                        // ✅ SỬA: Sử dụng ReplaceAll để giảm UI updates
//                        PagedItems.ReplaceAll(pagingResult.Data);

//                        // ✅ THÊM: Batch update các properties để giảm binding overhead
//                        using (var batch = new PropertyChangeBatch(this))
//                        {
//                            var newTotalRecords = pagingResult.TotalRows;
//                            var newTotalPages = (int)Math.Ceiling((double)newTotalRecords / PageSize);
//                            if (newTotalPages <= 0) newTotalPages = 1;

//                            batch.SetProperty(() => TotalRecords, newTotalRecords);
//                            batch.SetProperty(() => TotalPages, newTotalPages);

//                            if (pagingResult.CurrentPage != CurrentPage)
//                                batch.SetProperty(() => CurrentPage, pagingResult.CurrentPage);
//                        }
//                    }
//                    else
//                    {
//                        PagedItems.ReplaceAll(new List<TGridDto>());
//                        await OnLoadDataErrorAsync(pagingResult);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    PagedItems.ReplaceAll(new List<TGridDto>());
//                    throw;
//                }
//                finally
//                {
//                    IsLoading = false;
//                }
//            });
//        }

//        private void UpdatePageNumbers()
//        {
//            var pageNumbers = PaginationHelper.GeneratePageNumbers(CurrentPage, TotalPages);
//            PageNumbers.Clear();
//            foreach (var pageNumber in pageNumbers)
//            {
//                PageNumbers.Add(pageNumber);
//            }
//        }

//        #endregion

//        #region Abstract/Virtual Methods

//        /// <summary>
//        /// Override để lấy dữ liệu từ server với phân trang - trả về TGridDto
//        /// </summary>
//        protected abstract Task<PagingResult<TGridDto>> GetDataFromServerAsync(PagingRequest request);

//        /// <summary>
//        /// Override để xử lý khi có lỗi trong việc load dữ liệu
//        /// </summary>
//        protected virtual async Task OnLoadDataErrorAsync(PagingResult<TGridDto> result)
//        {
//            await Task.CompletedTask;
//        }

//        /// <summary>
//        /// Override để cấu hình columns cho DataGrid
//        /// </summary>
//        protected abstract void LoadConfigureColumns();

//        /// <summary>
//        /// Override để xử lý exception khi load dữ liệu
//        /// </summary>
//        protected virtual async Task OnLoadDataExceptionAsync(Exception exception)
//        {
//            await Task.CompletedTask;
//        }

//        /// <summary>
//        /// Override để xử lý thêm mới - sử dụng TCreateDto
//        /// </summary>
//        protected virtual void OnAdd()
//        {
//        }

//        /// <summary>
//        /// Override để xử lý chỉnh sửa - nhận TGridDto, sử dụng TUpdateDto
//        /// </summary>
//        protected virtual void OnEdit(TGridDto item)
//        {
//        }


//        ///// <summary>
//        ///// Override để xử lý thêm mới - sử dụng TCreateDto
//        ///// </summary>
//        //protected virtual async Task OnAddAsync()
//        //{
//        //    await Task.CompletedTask;
//        //}

//        ///// <summary>
//        ///// Override để xử lý chỉnh sửa - nhận TGridDto, sử dụng TUpdateDto
//        ///// </summary>
//        //protected virtual async Task OnEditAsync(TGridDto item)
//        //{
//        //    await Task.CompletedTask;
//        //}

//        /// <summary>
//        /// Override để xử lý xóa - nhận TGridDto, sử dụng TDeleteDto
//        /// </summary>
//        protected virtual async Task OnDeleteAsync(TGridDto item)
//        {
//            await Task.CompletedTask;
//        }

//        /// <summary>
//        /// Helper method để convert từ TGridDto sang TUpdateDto
//        /// </summary>
//        protected abstract TUpdateDto ConvertToUpdateDto(TGridDto gridItem);

//        /// <summary>
//        /// Helper method để tạo mới TCreateDto
//        /// </summary>
//        protected abstract TCreateDto CreateNewDto();

//        /// <summary>
//        /// Helper method để convert từ TGridDto sang TDeleteDto
//        /// </summary>
//        protected abstract Task ConvertToDeleteDto(TGridDto gridItem);

//        #endregion

//        #region Public Methods

//        public async Task RefreshAsync()
//        {
//            await LoadDataFromServerAsync();
//        }

//        public async Task RefreshToFirstPageAsync()
//        {
//            CurrentPage = 1;

//            await LoadDataFromServerAsync();
//        }

//        public async Task SearchWithKeywordAsync(string keyword)
//        {
//            SearchKeyword = keyword;
//            await SearchAsync();
//        }

//        /// <summary>
//        /// Lấy PagingRequest hiện tại với dữ liệu đã được xử lý
//        /// </summary>
//        public PagingRequest GetCurrentPagingRequest()
//        {
//            return new PagingRequest
//            {
//                CurrentPage = CurrentPage,
//                PageSize = PageSize,
//                SearchText = SearchKeyword,
//                SortColumn = SortColumn,
//                SortDirection = SortDirection
//            };
//        }

//        /// <summary>
//        /// Thiết lập lại các tùy chọn PageSize cho DataGrid
//        /// </summary>
//        /// <param name="options">Danh sách các giá trị PageSize mới</param>
//        public void SetPageSizeOptions(params int[] options)
//        {
//            if (options == null || options.Length == 0)
//            {
//                PageSizeOptions = new ObservableCollection<int> { 25, 50, 100, 250 };
//            }
//            else
//            {
//                PageSizeOptions = new ObservableCollection<int>(options);
//            }
//        }

//        /// <summary>
//        /// Thiết lập lại các tùy chọn PageSize cho DataGrid từ một collection
//        /// </summary>
//        /// <param name="options">Collection chứa các giá trị PageSize mới</param>
//        public void SetPageSizeOptions(IEnumerable<int> options)
//        {
//            if (options == null)
//            {
//                PageSizeOptions = new ObservableCollection<int> { 25, 50, 100, 250 };
//            }
//            else
//            {
//                PageSizeOptions = new ObservableCollection<int>(options);
//            }
//        }

//        #endregion

//        #region Thêm các phương thức tiện ích cho BatchObservableCollection

//        /// <summary>
//        /// Thêm một item vào collection
//        /// </summary>
//        protected void AddItemToGrid(TGridDto item)
//        {
//            PagedItems.Add(item);
//        }

//        /// <summary>
//        /// Thêm nhiều items vào collection
//        /// </summary>
//        protected void AddItemsToGrid(IEnumerable<TGridDto> items)
//        {
//            PagedItems.AddRange(items);
//        }

//        /// <summary>
//        /// Xóa item khỏi collection
//        /// </summary>
//        protected bool RemoveItemFromGrid(TGridDto item)
//        {
//            return PagedItems.Remove(item);
//        }

//        /// <summary>
//        /// Clear all items
//        /// </summary>
//        protected void ClearGrid()
//        {
//            PagedItems.ReplaceAll(new List<TGridDto>());
//        }

//        #endregion
//    }

//    /// <summary>
//    /// Class để gửi request phân trang lên server
//    /// </summary>
//    public class PagingRequest
//    {
//        public int CurrentPage { get; set; } = 1;
//        public int PageSize { get; set; } = 25;
//        public string SearchText { get; set; } = string.Empty;
//        public string SortColumn { get; set; } = string.Empty;
//        public ListSortDirection SortDirection { get; set; } = ListSortDirection.Ascending;
//    }
//}