using FlowMy.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace FlowMy.ViewModels.Base
{
    public partial class BaseDynamicViewModel<T> : ObservableObject where T : class
    {
        [ObservableProperty]
        private T data;

        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private bool isSaving = false;

        [ObservableProperty]
        private string statusSave = "💾 Lưu";

        // THÊM: ObservableProperty cho ParentWindow để có thể track thay đổi
        [ObservableProperty]
        private Window? parentWindow;

        // Dictionary để lưu trữ các enum collections
        public Dictionary<string, ObservableCollection<EnumItem>> EnumCollections { get; private set; }
        public Dictionary<string, ObservableCollection<object>> DynamicCollections { get; private set; }

        // Delegate để xử lý CreateAsync từ parent
        public Func<T, Task<bool>>? CreateAsyncHandler { get; set; }

        // PHƯƠNG ÁN 1: Delegate để check custom condition
        public Func<bool>? CustomCanSaveCondition { get; set; }

        // PHƯƠNG ÁN 2: Property để bind từ View
        [ObservableProperty]
        private bool isFormValid = true;

        // Logger (optional)
        private readonly ILogger? _logger;

        public BaseDynamicViewModel(T initialData, string windowTitle, ILogger? logger = null)
        {
            Data = initialData;
            Title = windowTitle;
            _logger = logger;
            EnumCollections = new Dictionary<string, ObservableCollection<EnumItem>>();
            DynamicCollections = new Dictionary<string, ObservableCollection<object>>();

            // QUAN TRỌNG: Subscribe to Data's PropertyChanged if it implements INotifyPropertyChanged
            SetupDataPropertyChangedSubscription();
        }

        // Constructor với enum collections
        public BaseDynamicViewModel(T initialData, string windowTitle,
            Dictionary<string, ObservableCollection<EnumItem>>? enumCollections = null,
            ILogger? logger = null) : this(initialData, windowTitle, logger)
        {
            if (enumCollections != null)
            {
                EnumCollections = enumCollections;
            }
        }

        public BaseDynamicViewModel(T initialData, string windowTitle,
           Dictionary<string, ObservableCollection<object>>? dynamicCollections = null,
           ILogger? logger = null) : this(initialData, windowTitle, logger)
        {
            if (dynamicCollections != null)
            {
                DynamicCollections = dynamicCollections;
            }
        }

        // PHƯƠNG ÁN 3: Constructor với custom condition
        public BaseDynamicViewModel(T initialData, string windowTitle,
            Func<bool>? customCanSaveCondition,
            Dictionary<string, ObservableCollection<EnumItem>>? enumCollections = null,
            ILogger? logger = null) : this(initialData, windowTitle, enumCollections, logger)
        {
            CustomCanSaveCondition = customCanSaveCondition;
        }

        // QUAN TRỌNG: Setup subscription to Data's PropertyChanged
        private void SetupDataPropertyChangedSubscription()
        {
            if (Data is INotifyPropertyChanged notifyData)
            {
                notifyData.PropertyChanged += OnDataPropertyChanged;
            }
        }

        // Handle Data property changes to refresh SaveCommand
        private void OnDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Refresh SaveCommand whenever any property in Data changes
            SaveCommand.NotifyCanExecuteChanged();
        }

        // THÊM: Partial method được gọi khi ParentWindow thay đổi
        partial void OnParentWindowChanged(Window? oldValue, Window? newValue)
        {
            // Set title cho window mới khi ParentWindow được assign
            if (newValue != null)
            {
                newValue.Title = Title;
            }
        }

        // THÊM: Partial method được gọi khi Title thay đổi
        partial void OnTitleChanged(string? oldValue, string newValue)
        {
            // Nếu đã có ParentWindow, update title ngay lập tức
            if (ParentWindow != null)
            {
                ParentWindow.Title = newValue;
            }
        }

        // Method để thêm enum collection
        public void AddEnumCollection(string key, ObservableCollection<EnumItem> collection)
        {
            EnumCollections[key] = collection;
        }

        // Method để lấy enum collection theo key
        public ObservableCollection<EnumItem>? GetEnumCollection(string key)
        {
            return EnumCollections.TryGetValue(key, out var collection) ? collection : null;
        }

        // PHƯƠNG ÁN 4: Method để set custom condition từ bên ngoài
        public void SetCustomCanSaveCondition(Func<bool> condition)
        {
            CustomCanSaveCondition = condition;
            SaveCommand.NotifyCanExecuteChanged(); // Refresh button state
        }

        // PHƯƠNG ÁN 5: Method để update form validation state
        public void SetFormValid(bool isValid)
        {
            IsFormValid = isValid;
            SaveCommand.NotifyCanExecuteChanged(); // Refresh button state
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            // KIỂM TRA ĐẦU TIÊN: Không cho phép double-click/multi-click
            if (IsSaving)
            {
                _logger?.LogWarning("Save operation already in progress, ignoring duplicate request");
                return; // Exit ngay lập tức nếu đang saving
            }

            try
            {
                // SET TRẠNG THÁI SAVING NGAY LẬP TỨC
                IsSaving = true;
                StatusSave = "⏳ Đang Lưu...";

                // FORCE NOTIFY COMMAND STATE CHANGE ĐỂ DISABLE BUTTON
                SaveCommand.NotifyCanExecuteChanged();

                // CHO PHÉP UI THREAD UPDATE (optional nhưng tốt cho UX)
                await Task.Yield();

                _logger?.LogInformation("Starting save operation");

                // Validation (optional)
                if (!ValidateData())
                {
                    _logger?.LogWarning("Data validation failed");
                    return;
                }

                // THỰC HIỆN SAVE OPERATION
                if (CreateAsyncHandler != null)
                {
                    _logger?.LogInformation("Executing CreateAsyncHandler");
                    bool success = await CreateAsyncHandler(Data);

                    if (success)
                    {
                        _logger?.LogInformation("Save operation completed successfully");
                        if (ParentWindow != null)
                        {
                            ParentWindow.DialogResult = true;
                            ParentWindow.Close();
                        }
                    }
                    else
                    {
                        _logger?.LogWarning("Save operation completed with failure");
                        // Giữ dialog mở để user có thể sửa
                    }
                }
                else
                {
                    _logger?.LogInformation("No CreateAsyncHandler provided, closing dialog");
                    // Fallback - chỉ đóng dialog nếu không có handler
                    if (ParentWindow != null)
                    {
                        ParentWindow.DialogResult = true;
                        ParentWindow.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error occurred during save operation");
                // Show error message
                throw;
            }
            finally
            {
                // QUAN TRỌNG: LUÔN RESET TRẠNG THÁI TRONG FINALLY
                StatusSave = "💾 Lưu";
                IsSaving = false;

                // FORCE NOTIFY COMMAND STATE CHANGE ĐỂ ENABLE LẠI BUTTON
                SaveCommand.NotifyCanExecuteChanged();

                _logger?.LogInformation("Save operation cleanup completed, button re-enabled");
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            if (ParentWindow != null)
            {
                ParentWindow.DialogResult = false;
                ParentWindow.Close();
            }
        }

        // CẢ 5 PHƯƠNG ÁN KẾT HỢP TRONG 1 METHOD
        private bool CanSave()
        {
            // QUAN TRỌNG: Kiểm tra đầu tiên - không cho phép click khi đang saving
            if (IsSaving) return false;

            // PHƯƠNG ÁN 1: Kiểm tra custom condition qua delegate
            if (CustomCanSaveCondition != null)
            {
                return CustomCanSaveCondition();
            }

            // PHƯƠNG ÁN 2: Kiểm tra form valid property
            if (!IsFormValid) return false;

            // PHƯƠNG ÁN 6: Có thể override trong class con
            return CanSaveOverride();
        }

        // Virtual method để class con có thể override
        protected virtual bool CanSaveOverride()
        {
            return true;
        }

        // CẢI THIỆN: Ensure immediate notification
        partial void OnIsSavingChanged(bool oldValue, bool newValue)
        {
            // Notify tất cả commands liên quan khi loading state thay đổi
            SaveCommand.NotifyCanExecuteChanged();

            // THÊM: Force immediate UI update nếu cần
            OnPropertyChanged(nameof(StatusSave));
        }

        // Auto refresh khi IsFormValid thay đổi
        partial void OnIsFormValidChanged(bool value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        // QUAN TRỌNG: Override OnDataChanged để setup subscription
        partial void OnDataChanged(T? oldValue, T newValue)
        {
            // Unsubscribe from old data
            if (oldValue is INotifyPropertyChanged oldNotify)
            {
                oldNotify.PropertyChanged -= OnDataPropertyChanged;
            }

            // Subscribe to new data
            SetupDataPropertyChangedSubscription();

            // Refresh command state
            SaveCommand.NotifyCanExecuteChanged();
        }

        // Virtual method để các class con có thể override validation
        protected virtual bool ValidateData()
        {
            return true;
        }

        // Dispose pattern để cleanup subscription
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
        }

        ~BaseDynamicViewModel()
        {
            if (Data is INotifyPropertyChanged notifyData)
            {
                notifyData.PropertyChanged -= OnDataPropertyChanged;
            }
        }
    }
}