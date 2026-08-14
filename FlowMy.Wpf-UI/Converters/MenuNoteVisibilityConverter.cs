using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlowMy.Converters
{
    /// <summary>
    /// Converter để xác định hiển thị ghi chú Menu dựa trên điều kiện thời gian
    /// </summary>
    public class MenuNoteVisibilityConverter : IMultiValueConverter
    {
        /// <summary>
        /// Chuyển đổi giá trị Note và DateTimeEnd thành Visibility
        /// </summary>
        /// <param name="values">Mảng chứa Note (string) và DateTimeEnd (DateTime?)</param>
        /// <param name="targetType">Kiểu mục tiêu (Visibility)</param>
        /// <param name="parameter">Tham số (không sử dụng)</param>
        /// <param name="culture">Thông tin văn hóa</param>
        /// <returns>Visibility.Visible nếu thỏa mãn điều kiện, ngược lại Visibility.Collapsed</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                // Kiểm tra đầu vào
                if (values == null || values.Length < 2)
                    return Visibility.Collapsed;

                // Lấy giá trị Note và DateTimeEnd
                string note = values[0] as string;
                DateTime? dateTimeEnd = values[1] as DateTime?;

                // Kiểm tra điều kiện hiển thị
                // 1. Note không được null hoặc rỗng
                // 2. DateTimeEnd không được null
                // 3. DateTimeEnd phải lớn hơn thời gian hiện tại
                if (!string.IsNullOrWhiteSpace(note) &&
                    dateTimeEnd.HasValue &&
                    dateTimeEnd.Value > DateTime.Now)
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần thiết
                System.Diagnostics.Debug.WriteLine($"MenuNoteVisibilityConverter Error: {ex.Message}");
                return Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Chuyển đổi ngược (không được hỗ trợ)
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("MenuNoteVisibilityConverter không hỗ trợ chuyển đổi ngược.");
        }
    }

    /// <summary>
    /// Converter bổ sung để định dạng thời gian còn lại của ghi chú
    /// </summary>
    public class NoteTimeRemainingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is DateTime dateTimeEnd && dateTimeEnd > DateTime.Now)
                {
                    TimeSpan remaining = dateTimeEnd - DateTime.Now;

                    if (remaining.TotalDays > 1)
                        return $"Còn {remaining.Days} ngày";
                    else if (remaining.TotalHours > 1)
                        return $"Còn {remaining.Hours} giờ";
                    else if (remaining.TotalMinutes > 1)
                        return $"Còn {remaining.Minutes} phút";
                    else
                        return "Sắp hết hạn";
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter để xác định màu sắc của badge dựa trên thời gian còn lại
    /// </summary>
    public class NoteBadgeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is DateTime dateTimeEnd && dateTimeEnd > DateTime.Now)
                {
                    TimeSpan remaining = dateTimeEnd - DateTime.Now;

                    // Màu đỏ nếu còn ít hơn 1 giờ
                    if (remaining.TotalHours < 1)
                        return "#FFDC2626"; // Đỏ

                    // Màu cam nếu còn ít hơn 1 ngày
                    if (remaining.TotalDays < 1)
                        return "#FFEA580C"; // Cam

                    // Màu xanh lá nếu còn nhiều thời gian
                    return "#FF16A34A"; // Xanh lá
                }

                // Mặc định màu cam
                return "#FFFF6B35";
            }
            catch
            {
                return "#FFFF6B35";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}