using FlowMy.Interfaces;
using FlowMy.Models.Enums;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Controls
{
    public partial class DateTimePickerUserControl : UserControl, IDateTimePickerConfig
    {
        #region Dependency Properties

        public static readonly RoutedEvent SelectedDateTimeChangedEvent =
     EventManager.RegisterRoutedEvent(
         nameof(SelectedDateTimeChanged),
         RoutingStrategy.Bubble,
         typeof(RoutedPropertyChangedEventHandler<DateTime?>),
         typeof(DateTimePickerUserControl));

        public static readonly DependencyProperty ControlHeightProperty =
    DependencyProperty.Register(nameof(ControlHeight), typeof(double),
        typeof(DateTimePickerUserControl),
        new PropertyMetadata(45.0));

        public static readonly DependencyProperty ShowClearButtonProperty =
            DependencyProperty.Register(nameof(ShowClearButton), typeof(bool),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(true, OnShowClearButtonChanged));

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(nameof(PlaceholderText), typeof(string),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata("Chọn ngày giờ...", OnPlaceholderTextChanged));

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty MinDateProperty =
            DependencyProperty.Register(nameof(MinDate), typeof(DateTime?),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(null, OnMinMaxDateChanged));

        public static readonly DependencyProperty MaxDateProperty =
            DependencyProperty.Register(nameof(MaxDate), typeof(DateTime?),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(null, OnMinMaxDateChanged));

        public static readonly DependencyProperty AllowNullProperty =
            DependencyProperty.Register(nameof(AllowNull), typeof(bool),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty PopupPlacementProperty =
            DependencyProperty.Register(nameof(PopupPlacement), typeof(System.Windows.Controls.Primitives.PlacementMode),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(System.Windows.Controls.Primitives.PlacementMode.Bottom));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(new CornerRadius(6)));

        public static readonly DependencyProperty ShowTodayButtonProperty =
            DependencyProperty.Register(nameof(ShowTodayButton), typeof(bool),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(true, OnShowTodayButtonChanged));

        public static readonly DependencyProperty AutoCloseOnSelectProperty =
            DependencyProperty.Register(nameof(AutoCloseOnSelect), typeof(bool),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty DefaultModeProperty =
    DependencyProperty.Register(nameof(DefaultMode), typeof(DateTimePickerDefaultModeEnum),
        typeof(DateTimePickerUserControl),
        new PropertyMetadata(DateTimePickerDefaultModeEnum.Empty));

        public static readonly DependencyProperty DefaultDateTimeProperty =
            DependencyProperty.Register(nameof(DefaultDateTime), typeof(DateTime?),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedDateTimeProperty =
            DependencyProperty.Register(nameof(SelectedDateTime), typeof(DateTime?),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(null, OnSelectedDateTimeChanged));

        public static readonly DependencyProperty PickerModeProperty =
            DependencyProperty.Register(nameof(PickerMode), typeof(DateTimePickerModeEnum),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(DateTimePickerModeEnum.DateTime, OnPickerModeChanged));

        public static readonly DependencyProperty DisplayFormatProperty =
            DependencyProperty.Register(nameof(DisplayFormat), typeof(string),
                typeof(DateTimePickerUserControl),
                new PropertyMetadata(string.Empty, OnDisplayFormatChanged));

        #endregion

        #region Properties

        public double ControlHeight
        {
            get => (double)GetValue(ControlHeightProperty);
            set => SetValue(ControlHeightProperty, value);
        }

        public bool ShowClearButton
        {
            get => (bool)GetValue(ShowClearButtonProperty);
            set => SetValue(ShowClearButtonProperty, value);
        }

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public DateTime? MinDate
        {
            get => (DateTime?)GetValue(MinDateProperty);
            set => SetValue(MinDateProperty, value);
        }

        public DateTime? MaxDate
        {
            get => (DateTime?)GetValue(MaxDateProperty);
            set => SetValue(MaxDateProperty, value);
        }

        public bool AllowNull
        {
            get => (bool)GetValue(AllowNullProperty);
            set => SetValue(AllowNullProperty, value);
        }

        public System.Windows.Controls.Primitives.PlacementMode PopupPlacement
        {
            get => (System.Windows.Controls.Primitives.PlacementMode)GetValue(PopupPlacementProperty);
            set => SetValue(PopupPlacementProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public bool ShowTodayButton
        {
            get => (bool)GetValue(ShowTodayButtonProperty);
            set => SetValue(ShowTodayButtonProperty, value);
        }

        public bool AutoCloseOnSelect
        {
            get => (bool)GetValue(AutoCloseOnSelectProperty);
            set => SetValue(AutoCloseOnSelectProperty, value);
        }

        public DateTimePickerDefaultModeEnum DefaultMode
        {
            get => (DateTimePickerDefaultModeEnum)GetValue(DefaultModeProperty);
            set => SetValue(DefaultModeProperty, value);
        }

        public DateTime? DefaultDateTime
        {
            get => (DateTime?)GetValue(DefaultDateTimeProperty);
            set => SetValue(DefaultDateTimeProperty, value);
        }

        public DateTime? SelectedDateTime
        {
            get => (DateTime?)GetValue(SelectedDateTimeProperty);
            set => SetValue(SelectedDateTimeProperty, value);
        }

        public DateTimePickerModeEnum PickerMode
        {
            get => (DateTimePickerModeEnum)GetValue(PickerModeProperty);
            set => SetValue(PickerModeProperty, value);
        }

        public string DisplayFormat
        {
            get => (string)GetValue(DisplayFormatProperty);
            set => SetValue(DisplayFormatProperty, value);
        }

        private DateTime? pendingDateTime;
        private DateTime? originalDateTime;

        #endregion

        #region Constructor

        public DateTimePickerUserControl()
        {
            InitializeComponent();
            InitializeTimePickers();
            SetVietnameseCulture();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #endregion

        #region Initialization

        private void SetVietnameseCulture()
        {
            var culture = new CultureInfo("vi-VN");

            CalendarControl.Language = System.Windows.Markup.XmlLanguage.GetLanguage("vi-VN");
            CalendarControl.FirstDayOfWeek = culture.DateTimeFormat.FirstDayOfWeek;

            DateCalendar.Language = System.Windows.Markup.XmlLanguage.GetLanguage("vi-VN");
            DateCalendar.FirstDayOfWeek = culture.DateTimeFormat.FirstDayOfWeek;
        }

        private void InitializeTimePickers()
        {
            // Giờ (0-23)
            for (int i = 0; i < 24; i++)
            {
                HourItemsControl.Items.Add(i.ToString("D2"));
                TimeHourItemsControl.Items.Add(i.ToString("D2"));
            }

            // Phút và Giây (0-59)
            for (int i = 0; i < 60; i++)
            {
                MinuteItemsControl.Items.Add(i.ToString("D2"));
                SecondItemsControl.Items.Add(i.ToString("D2"));
                TimeMinuteItemsControl.Items.Add(i.ToString("D2"));
                TimeSecondItemsControl.Items.Add(i.ToString("D2"));
            }
        }

        #endregion

        #region Event Handlers

        public event RoutedPropertyChangedEventHandler<DateTime?> SelectedDateTimeChanged
        {
            add { AddHandler(SelectedDateTimeChangedEvent, value); }
            remove { RemoveHandler(SelectedDateTimeChangedEvent, value); }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (!AllowNull) return;

            SelectedDateTime = null;
            e.Handled = true;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (SelectedDateTime == null)
            {
                InitializeDefaultValue();
            }
            // Update placeholder brush when control is loaded
            UpdatePlaceholderBrush();
        }

        private void InitializeDefaultValue()
        {
            switch (DefaultMode)
            {
                case DateTimePickerDefaultModeEnum.Empty:
                    SelectedDateTime = null;
                    MainTextBox.Text = string.Empty;
                    break;

                case DateTimePickerDefaultModeEnum.CurrentDateTime:
                    // ✅ Sửa: Đảm bảo lấy giờ địa phương
                    SelectedDateTime = DateTime.Now;
                    break;

                case DateTimePickerDefaultModeEnum.CustomDateTime:
                    // ✅ Sửa: Nếu DefaultDateTime là UTC, chuyển sang Local
                    if (DefaultDateTime.HasValue)
                    {
                        SelectedDateTime = DefaultDateTime.Value.Kind == DateTimeKind.Utc
                            ? DefaultDateTime.Value.ToLocalTime()
                            : DefaultDateTime.Value;
                    }
                    else
                    {
                        SelectedDateTime = DateTime.Now;
                    }
                    break;

                case DateTimePickerDefaultModeEnum.StartOfDay:
                    SelectedDateTime = DateTime.Now.Date;
                    break;

                case DateTimePickerDefaultModeEnum.EndOfDay:
                    SelectedDateTime = DateTime.Now.Date.AddDays(1).AddSeconds(-1);
                    break;

            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
        }

        private void MainButton_Click(object sender, RoutedEventArgs e)
        {
            if (!PickerPopup.IsOpen)
            {
                OpenPopup();
            }
        }

        //private void Today_Click(object sender, RoutedEventArgs e)
        //{
        //    // ✅ Sửa: Đảm bảo lấy giờ địa phương
        //    var now = DateTime.Now;
        //    pendingDateTime = now;

        //    CalendarControl.SelectedDate = now;
        //    DateCalendar.SelectedDate = now;

        //    ScrollToTime(HourScrollViewer, TimeHourScrollViewer, now.Hour);
        //    ScrollToTime(MinuteScrollViewer, TimeMinuteScrollViewer, now.Minute);
        //    ScrollToTime(SecondScrollViewer, TimeSecondScrollViewer, now.Second);
        //}

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            // ✅ Sửa: Đảm bảo lấy giờ địa phương
            var now = DateTime.Now;

            // Kiểm tra ràng buộc MinDate và MaxDate
            if (MinDate.HasValue && now < MinDate.Value)
            {
                MessageBox.Show($"Ngày giờ hiện tại không được nhỏ hơn {MinDate.Value.ToString(GetDisplayFormat())}",
                               "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MaxDate.HasValue && now > MaxDate.Value)
            {
                MessageBox.Show($"Ngày giờ hiện tại không được lớn hơn {MaxDate.Value.ToString(GetDisplayFormat())}",
                               "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Set giá trị và đóng popup luôn (giống button Chọn)
            SelectedDateTime = now;
            ClosePopup();
        }

        private void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            var calendar = sender as System.Windows.Controls.Calendar;
            if (calendar?.SelectedDate == null) return;

            var newDate = calendar.SelectedDate.Value;
            // ✅ Sửa: Đảm bảo lấy thời gian địa phương
            var time = pendingDateTime?.TimeOfDay ?? DateTime.Now.TimeOfDay;
            pendingDateTime = newDate.Date.Add(time);

            if (PickerMode == DateTimePickerModeEnum.Date && calendar == DateCalendar)
            {
                if (AutoCloseOnSelect)
                {
                    SelectedDateTime = pendingDateTime;
                    ClosePopup();
                }
            }
        }

        private void TimeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            string type = button.Tag.ToString();
            int value = int.Parse(button.Content.ToString());

            // ✅ Sửa: Đảm bảo khởi tạo với giờ địa phương
            if (pendingDateTime == null)
                pendingDateTime = DateTime.Now;

            var date = pendingDateTime.Value.Date;
            int hour = type == "Hour" ? value : pendingDateTime.Value.Hour;
            int minute = type == "Minute" ? value : pendingDateTime.Value.Minute;
            int second = type == "Second" ? value : pendingDateTime.Value.Second;

            pendingDateTime = new DateTime(date.Year, date.Month, date.Day, hour, minute, second, DateTimeKind.Local);

            // ✅ PHẦN QUAN TRỌNG: Scroll cho cả DateTime Mode và Time Only Mode
            ScrollViewer scrollViewer = null;

            if (type == "Hour")
            {
                // Kiểm tra cả 2 ScrollViewer
                scrollViewer = HourScrollViewer?.IsVisible == true ? HourScrollViewer : TimeHourScrollViewer;
            }
            else if (type == "Minute")
            {
                scrollViewer = MinuteScrollViewer?.IsVisible == true ? MinuteScrollViewer : TimeMinuteScrollViewer;
            }
            else if (type == "Second")
            {
                scrollViewer = SecondScrollViewer?.IsVisible == true ? SecondScrollViewer : TimeSecondScrollViewer;
            }

            if (scrollViewer != null)
            {
                double targetOffset = value * 40;
                scrollViewer.ScrollToVerticalOffset(targetOffset);

                System.Diagnostics.Debug.WriteLine($"[TimeButton_Click] Scrolled {type} to value {value}, offset: {targetOffset}");
            }
        }

        private void TimeScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;

            double offset = scrollViewer.VerticalOffset - (e.Delta / 3.0);
            scrollViewer.ScrollToVerticalOffset(offset);

            e.Handled = true;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[OK_Click] BEFORE UpdateTimeFromScroll - pendingDateTime: {pendingDateTime}");

            if (PickerMode == DateTimePickerModeEnum.Time || PickerMode == DateTimePickerModeEnum.DateTime)
            {
                UpdateTimeFromScroll();
            }

            System.Diagnostics.Debug.WriteLine($"[OK_Click] AFTER UpdateTimeFromScroll - pendingDateTime: {pendingDateTime}");

            if (pendingDateTime.HasValue)
            {
                if (MinDate.HasValue && pendingDateTime.Value < MinDate.Value)
                {
                    MessageBox.Show($"Ngày giờ không được nhỏ hơn {MinDate.Value.ToString(GetDisplayFormat())}",
                                   "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MaxDate.HasValue && pendingDateTime.Value > MaxDate.Value)
                {
                    MessageBox.Show($"Ngày giờ không được lớn hơn {MaxDate.Value.ToString(GetDisplayFormat())}",
                                   "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[OK_Click] Setting SelectedDateTime to: {pendingDateTime}");
                SelectedDateTime = pendingDateTime;
            }
            ClosePopup();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            pendingDateTime = originalDateTime;
            ClosePopup();
        }

        #endregion

        #region Private Methods

        private void UpdateCalendarRestrictions()
        {
            if (MinDate.HasValue)
            {
                CalendarControl.DisplayDateStart = MinDate.Value;
                DateCalendar.DisplayDateStart = MinDate.Value;
            }

            if (MaxDate.HasValue)
            {
                CalendarControl.DisplayDateEnd = MaxDate.Value;
                DateCalendar.DisplayDateEnd = MaxDate.Value;
            }
        }

        private void OpenPopup()
        {
            originalDateTime = SelectedDateTime;

            // ✅ Sửa: Nếu SelectedDateTime null, dùng DateTime.Now
            pendingDateTime = SelectedDateTime ?? DateTime.Now;

            UpdatePanelVisibility();
            UpdateSelection();

            PickerPopup.IsOpen = true;
        }

        private void ClosePopup()
        {
            PickerPopup.IsOpen = false;
        }

        private void UpdatePanelVisibility()
        {
            DateTimePanel.Visibility = Visibility.Collapsed;
            DatePanel.Visibility = Visibility.Collapsed;
            TimePanel.Visibility = Visibility.Collapsed;

            switch (PickerMode)
            {
                case DateTimePickerModeEnum.DateTime:
                    DateTimePanel.Visibility = Visibility.Visible;
                    break;
                case DateTimePickerModeEnum.Date:
                    DatePanel.Visibility = Visibility.Visible;
                    break;
                case DateTimePickerModeEnum.Time:
                    TimePanel.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void UpdatePlaceholderBrush()
        {
            if (MainTextBox == null) return;

            // Create new VisualBrush with updated placeholder text
            var visualBrush = new VisualBrush
            {
                Stretch = Stretch.None
            };

            var placeholderTextBlock = new TextBlock
            {
                Text = PlaceholderText ?? "Chọn ngày giờ...",
                Foreground = Application.Current.TryFindResource("PlaceholderBrush") as Brush ?? new SolidColorBrush(Colors.Gray),
                FontStyle = FontStyles.Normal,
                FontWeight = FontWeights.Normal,
                Padding = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            visualBrush.Visual = placeholderTextBlock;

            // Create or update the style
            Style style;
            if (MainTextBox.Style != null)
            {
                // Clone the existing style
                style = new Style(typeof(TextBox), MainTextBox.Style);
                // Remove existing DataTrigger for empty text
                var triggersToRemove = style.Triggers.OfType<DataTrigger>()
                    .Where(t => t.Binding is Binding binding &&
                                binding.Path != null &&
                                binding.Path.Path == "Text" &&
                                t.Value?.ToString() == "")
                    .ToList();
                foreach (var trigger2 in triggersToRemove)
                {
                    style.Triggers.Remove(trigger2);
                }
            }
            else
            {
                style = new Style(typeof(TextBox));
            }

            // Add new DataTrigger with updated VisualBrush
            var trigger = new DataTrigger
            {
                Binding = new Binding("Text") { RelativeSource = new RelativeSource(RelativeSourceMode.Self) },
                Value = ""
            };
            trigger.Setters.Add(new Setter(TextBox.BackgroundProperty, visualBrush));
            style.Triggers.Add(trigger);

            MainTextBox.Style = style;
        }

        private void UpdateDisplay()
        {
            if (SelectedDateTime.HasValue)
            {
                MainTextBox.Text = SelectedDateTime.Value.ToString(GetDisplayFormat());
            }
            else
            {
                MainTextBox.Text = string.Empty;
                MainTextBox.FontStyle = FontStyles.Italic;
                MainTextBox.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private string GetDisplayFormat()
        {
            if (!string.IsNullOrEmpty(DisplayFormat))
                return DisplayFormat;

            return PickerMode switch
            {
                DateTimePickerModeEnum.DateTime => "dd/MM/yyyy HH:mm:ss",
                DateTimePickerModeEnum.Date => "dd/MM/yyyy",
                DateTimePickerModeEnum.Time => "HH:mm:ss",
                _ => "dd/MM/yyyy"
            };
        }

        private void UpdateSelection()
        {
            var dateTime = pendingDateTime ?? SelectedDateTime ?? DateTime.Now;

            // Cập nhật Calendar
            CalendarControl.SelectedDate = dateTime;
            DateCalendar.SelectedDate = dateTime;

            // ✅ Đợi UI render xong rồi mới scroll
            Dispatcher.InvokeAsync(() =>
            {
                ScrollToTime(HourScrollViewer, TimeHourScrollViewer, dateTime.Hour);
                ScrollToTime(MinuteScrollViewer, TimeMinuteScrollViewer, dateTime.Minute);
                ScrollToTime(SecondScrollViewer, TimeSecondScrollViewer, dateTime.Second);

                System.Diagnostics.Debug.WriteLine($"[UpdateSelection] Scrolled to {dateTime.Hour:D2}:{dateTime.Minute:D2}:{dateTime.Second:D2}");
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ScrollToTime(ScrollViewer sv1, ScrollViewer sv2, int value)
        {
            // Để item ở vị trí center, cần scroll đến: value * 40 - 20
            // Nhưng vì có margin 80 top, nên: (value * 40) là đúng vị trí
            double targetOffset = value * 40;

            System.Diagnostics.Debug.WriteLine($"[ScrollToTime] Scrolling to value {value}, offset: {targetOffset}");

            sv1?.ScrollToVerticalOffset(targetOffset);
            sv2?.ScrollToVerticalOffset(targetOffset);
        }

        private void UpdateTimeFromScroll()
        {
            if (pendingDateTime == null)
                pendingDateTime = DateTime.Now;

            ScrollViewer hourSV = PickerMode == DateTimePickerModeEnum.DateTime ? HourScrollViewer : TimeHourScrollViewer;
            ScrollViewer minuteSV = PickerMode == DateTimePickerModeEnum.DateTime ? MinuteScrollViewer : TimeMinuteScrollViewer;
            ScrollViewer secondSV = PickerMode == DateTimePickerModeEnum.DateTime ? SecondScrollViewer : TimeSecondScrollViewer;

            int hour = GetTimeFromScroll(hourSV, 23);
            int minute = GetTimeFromScroll(minuteSV, 59);
            int second = GetTimeFromScroll(secondSV, 59);

            System.Diagnostics.Debug.WriteLine($"[UpdateTimeFromScroll] BEFORE - pendingDateTime: {pendingDateTime}");
            System.Diagnostics.Debug.WriteLine($"[UpdateTimeFromScroll] Calculated - Hour: {hour}, Minute: {minute}, Second: {second}");

            var date = pendingDateTime.Value.Date;
            pendingDateTime = new DateTime(date.Year, date.Month, date.Day, hour, minute, second, DateTimeKind.Local);

            System.Diagnostics.Debug.WriteLine($"[UpdateTimeFromScroll] AFTER - pendingDateTime: {pendingDateTime}");
        }

        private int GetTimeFromScroll(ScrollViewer scrollViewer, int maxValue)
        {
            if (scrollViewer == null) return 0;

            // Mỗi button cao 40px
            // Vùng selection ở giữa ScrollViewer có chiều cao 40px
            // ScrollViewer hiển thị 5 items (200px), selection ở giữa (item thứ 3)
            // Công thức: offset / 40 = index chính xác
            int index = (int)Math.Round(scrollViewer.VerticalOffset / 40.0);

            // Giới hạn index
            index = Math.Max(0, Math.Min(maxValue, index));

            System.Diagnostics.Debug.WriteLine($"[GetTimeFromScroll] Offset: {scrollViewer.VerticalOffset}, Index: {index}, MaxValue: {maxValue}");

            return index;
        }

        #endregion

        #region Property Changed Callbacks

        private static void OnPlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (DateTimePickerUserControl)d;
            picker.UpdatePlaceholderBrush();
        }

        private static void OnShowClearButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (DateTimePickerUserControl)d;
        }

        private static void OnMinMaxDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (DateTimePickerUserControl)d;
            picker.UpdateCalendarRestrictions();
        }

        private static void OnShowTodayButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (DateTimePickerUserControl)d;
        }

        private static void OnSelectedDateTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (DateTimePickerUserControl)d;
            picker.UpdateDisplay();

            picker.RaiseEvent(new RoutedPropertyChangedEventArgs<DateTime?>(
                (DateTime?)e.OldValue,
                (DateTime?)e.NewValue,
                SelectedDateTimeChangedEvent));

            System.Diagnostics.Debug.WriteLine(
                $"[DateTimePickerUserControl] SelectedDateTime changed: {e.OldValue} -> {e.NewValue}");
        }

        private static void OnPickerModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (DateTimePickerUserControl)d;
            picker.UpdateDisplay();
        }

        private static void OnDisplayFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (DateTimePickerUserControl)d;
            picker.UpdateDisplay();
        }

        #endregion
    }

    public enum DateTimePickerDefaultModeEnum
    {
        Empty = 0,
        CurrentDateTime = 1,
        CustomDateTime = 2,
        StartOfDay = 3, // 00:00:00
        EndOfDay = 4    // 23:59:59
    }
}

//<!-- Ví dụ đầy đủ với tất cả tùy chỉnh -->
//<controls:DateTimePickerUserControl 
//    x:Name="MyDateTimePicker"
//    ControlHeight="50"
//    PickerMode="DateTime"
//    DefaultMode="Empty"
//    ShowClearButton="True"
//    ShowTodayButton="True"
//    PlaceholderText="Vui lòng chọn ngày giờ..."
//    IsReadOnly="False"
//    AllowNull="True"
//    AutoCloseOnSelect="False"
//    MinDate="2020-01-01"
//    MaxDate="2030-12-31"
//    PopupPlacement="Bottom"
//    CornerRadius="8"
//    DisplayFormat="dd/MM/yyyy HH:mm"/>

//<!-- Ví dụ đơn giản -->
//<controls:DateTimePickerUserControl 
//    ControlHeight="45"
//    ShowClearButton="True"
//    PlaceholderText="Chọn ngày..."/>

//<!-- Ví dụ chỉ cho phép chọn ngày trong tương lai -->
//<controls:DateTimePickerUserControl 
//    MinDate="{x:Static sys:DateTime.Now}"
//    ShowTodayButton="True"
//    DefaultMode="CurrentDateTime"/>