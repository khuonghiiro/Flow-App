// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Globalization;
using System.Windows.Data;

namespace FlowMy.Converters
{
    /// <summary>
    /// Converts a DateTime to a relative time string like "5 phút trước", "2 giờ trước", "Hôm qua".
    /// </summary>
    public class RelativeTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not DateTime dateTime)
                return "—";

            if (dateTime == DateTime.MinValue)
                return "—";

            var now = DateTime.Now;
            var diff = now - dateTime;

            if (diff.TotalSeconds < 60)
                return "Vừa xong";

            if (diff.TotalMinutes < 60)
            {
                int minutes = (int)diff.TotalMinutes;
                return $"{minutes} phút trước";
            }

            if (diff.TotalHours < 24)
            {
                int hours = (int)diff.TotalHours;
                return $"{hours} giờ trước";
            }

            if (diff.TotalDays < 2)
                return "Hôm qua";

            if (diff.TotalDays < 7)
            {
                int days = (int)diff.TotalDays;
                return $"{days} ngày trước";
            }

            if (diff.TotalDays < 30)
            {
                int weeks = (int)(diff.TotalDays / 7);
                return $"{weeks} tuần trước";
            }

            if (diff.TotalDays < 365)
            {
                int months = (int)(diff.TotalDays / 30);
                return $"{months} tháng trước";
            }

            int years = (int)(diff.TotalDays / 365);
            return $"{years} năm trước";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
