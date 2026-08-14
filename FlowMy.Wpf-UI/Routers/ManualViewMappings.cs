

namespace FlowMy.Routers
{
    /// <summary>
    /// Static view mappings - thay thế reflection để có performance cao
    /// Cập nhật manual khi thêm View/ViewModel mới
    /// Performance: ~1ms thay vì ~100ms reflection
    /// </summary>
    public static class ManualViewMappings
    {
        public static readonly Dictionary<string, (Type viewType, Type? viewModelType)> Mappings = new()
        {
            //// === route để hiển thị màn hình phải khớp với PATH trong model MenuItem ===
            //["dashboard"] = (typeof(DashboardView), typeof(DashboardViewModel)),
            //["user"] = (typeof(UserView), typeof(UserViewModel)),

        };

        public static int Count => Mappings.Count;

        public static string BuildInfo => $"Manual mappings v1.0 - {Count} views - Updated: {GetLastUpdateTime()}";

        private static string GetLastUpdateTime()
        {
            // Simple way to track when this was last modified
            return "2025-08-29"; // Update này khi modify mappings
        }

        /// <summary>
        /// Validate tất cả types tồn tại - call trong startup để catch errors sớm
        /// </summary>
        public static (bool isValid, List<string> errors) ValidateAllMappings()
        {
            var errors = new List<string>();

            foreach (var kvp in Mappings)
            {
                var path = kvp.Key;
                var (viewType, viewModelType) = kvp.Value;

                // Check view type
                try
                {
                    if (viewType == null)
                    {
                        errors.Add($"Path '{path}': ViewType is null");
                        continue;
                    }

                    // Try to get type info (this validates the type exists)
                    var _ = viewType.FullName;
                }
                catch (Exception ex)
                {
                    errors.Add($"Path '{path}': ViewType {viewType?.Name} error - {ex.Message}");
                }

                // Check viewmodel type (if specified)
                if (viewModelType != null)
                {
                    try
                    {
                        var _ = viewModelType.FullName;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Path '{path}': ViewModelType {viewModelType?.Name} error - {ex.Message}");
                    }
                }
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// Get all paths for debugging
        /// </summary>
        public static IEnumerable<string> GetAllPaths() => Mappings.Keys.OrderBy(x => x);

        /// <summary>
        /// Find mappings by pattern
        /// </summary>
        public static IEnumerable<(string path, Type viewType, Type? viewModelType)> FindByPattern(string pattern)
        {
            return Mappings
                .Where(kvp => kvp.Key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => (kvp.Key, kvp.Value.viewType, kvp.Value.viewModelType));
        }
    }
}