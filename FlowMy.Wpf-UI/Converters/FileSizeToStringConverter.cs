// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Globalization;
using System.Windows.Data;

namespace FlowMy.Converters
{
    /// <summary>
    /// Converts a file size in bytes (long) to a human-readable string like "1.2 KB", "3.5 MB".
    /// </summary>
    public class FileSizeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not long bytes || bytes < 0)
                return "—";

            if (bytes < 1024)
                return $"{bytes} B";

            double kb = bytes / 1024.0;
            if (kb < 1024)
                return $"{kb:F1} KB";

            double mb = kb / 1024.0;
            if (mb < 1024)
                return $"{mb:F1} MB";

            double gb = mb / 1024.0;
            return $"{gb:F2} GB";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
