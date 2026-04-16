//using FlowMy.Services.Interfaces;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using System.Text;
//using System.Text.Json;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Controls;

//namespace FlowMy.Services
//{
//    public class ViewFactoryService : IViewFactoryService
//    {
//        private readonly IServiceProvider _serviceProvider;
//        private readonly ILogger<ViewFactoryService> _logger;
//        private readonly Dictionary<string, (Type viewType, Type? viewModelType)> _pathMappings;

//        public ViewFactoryService(IServiceProvider serviceProvider, ILogger<ViewFactoryService> logger)
//        {
//            _serviceProvider = serviceProvider;
//            _logger = logger;
//            _pathMappings = new Dictionary<string, (Type, Type?)>();

//            InitializePathMappings();
//        }

//        private void InitializePathMappings()
//        {
//            try
//            {
//                var assembly = Assembly.GetExecutingAssembly();

//                // Tìm tất cả các View types
//                var viewTypes = assembly.GetTypes()
//                    .Where(t => t.IsClass && !t.IsAbstract &&
//                               (t.IsSubclassOf(typeof(UserControl)) || t.IsSubclassOf(typeof(Window))) &&
//                               t.Name.EndsWith("View"))
//                    .ToList();

//                // Tìm tất cả các ViewModel types
//                var viewModelTypes = assembly.GetTypes()
//                    .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("ViewModel"))
//                    .ToList();

//                foreach (var viewType in viewTypes)
//                {
//                    var path = ExtractPathFromType(viewType);
//                    if (!string.IsNullOrEmpty(path))
//                    {
//                        // Tìm ViewModel tương ứng
//                        var viewModelName = viewType.Name.Replace("View", "ViewModel");
//                        var viewModelType = viewModelTypes.FirstOrDefault(vm => vm.Name == viewModelName);

//                        _pathMappings[path] = (viewType, viewModelType);

//                        _logger.LogDebug($"Đã map path '{path}' -> View: {viewType.Name}, ViewModel: {viewModelType?.Name ?? "None"}");
//                    }
//                }

//                _logger.LogInformation($"Đã khởi tạo {_pathMappings.Count} path mappings");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Lỗi khi khởi tạo path mappings");
//            }
//        }

//        private string ExtractPathFromType(Type viewType)
//        {
//            try
//            {
//                var namespaceParts = viewType.Namespace?.Split('.') ?? Array.Empty<string>();
//                var viewName = viewType.Name.Replace("View", "").ToLower();

//                // Tìm index của "Views" trong namespace
//                var viewsIndex = Array.FindIndex(namespaceParts, part =>
//                    string.Equals(part, "Views", StringComparison.OrdinalIgnoreCase));

//                if (viewsIndex >= 0 && viewsIndex < namespaceParts.Length - 1)
//                {
//                    // Lấy các phần sau "Views"
//                    var pathParts = namespaceParts
//                        .Skip(viewsIndex + 1)
//                        .Select(p => p.ToLower())
//                        .ToList();

//                    // Thêm tên view vào cuối
//                    pathParts.Add(viewName);

//                    return string.Join("/", pathParts);
//                }

//                // Fallback: chỉ dùng tên view
//                return viewName;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogWarning(ex, $"Không thể extract path từ type {viewType.Name}");
//                return viewType.Name.Replace("View", "").ToLower();
//            }
//        }

//        public UserControl? CreateView(string path)
//        {
//            try
//            {
//                var normalizedPath = NormalizePath(path);

//                if (_pathMappings.TryGetValue(normalizedPath, out var mapping))
//                {
//                    var view = _serviceProvider.GetService(mapping.viewType) as UserControl;
//                    if (view != null)
//                    {
//                        _logger.LogDebug($"Đã tạo view cho path '{path}': {mapping.viewType.Name}");
//                        return view;
//                    }
//                }

//                // Thử tìm kiếm tương đối (fuzzy search)
//                var fuzzyMatch = FindFuzzyMatch(normalizedPath);
//                if (fuzzyMatch.HasValue)
//                {
//                    var view = _serviceProvider.GetService(fuzzyMatch.Value.viewType) as UserControl;
//                    if (view != null)
//                    {
//                        _logger.LogDebug($"Đã tạo view qua fuzzy match cho path '{path}': {fuzzyMatch.Value.viewType.Name}");
//                        return view;
//                    }
//                }

//                _logger.LogWarning($"Không tìm thấy view cho path: {path}");
//                return null;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Lỗi khi tạo view cho path: {path}");
//                return null;
//            }
//        }

//        public object? CreateViewModel(string path)
//        {
//            try
//            {
//                var normalizedPath = NormalizePath(path);

//                if (_pathMappings.TryGetValue(normalizedPath, out var mapping) && mapping.viewModelType != null)
//                {
//                    var viewModel = _serviceProvider.GetService(mapping.viewModelType);
//                    if (viewModel != null)
//                    {
//                        _logger.LogDebug($"Đã tạo viewmodel cho path '{path}': {mapping.viewModelType.Name}");
//                        return viewModel;
//                    }
//                }

//                // Thử tìm kiếm tương đối
//                var fuzzyMatch = FindFuzzyMatch(normalizedPath);
//                if (fuzzyMatch?.viewModelType != null)
//                {
//                    var viewModel = _serviceProvider.GetService(fuzzyMatch.Value.viewModelType);
//                    if (viewModel != null)
//                    {
//                        _logger.LogDebug($"Đã tạo viewmodel qua fuzzy match cho path '{path}': {fuzzyMatch.Value.viewModelType.Name}");
//                        return viewModel;
//                    }
//                }

//                _logger.LogDebug($"Không tìm thấy viewmodel cho path: {path}");
//                return null;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Lỗi khi tạo viewmodel cho path: {path}");
//                return null;
//            }
//        }

//        public (UserControl? view, object? viewModel) CreateViewWithViewModel(string path)
//        {
//            var view = CreateView(path);
//            var viewModel = CreateViewModel(path);

//            // Tự động gán DataContext nếu có cả view và viewModel
//            if (view != null && viewModel != null)
//            {
//                view.DataContext = viewModel;
//            }

//            return (view, viewModel);
//        }

//        public bool IsValidPath(string path)
//        {
//            var normalizedPath = NormalizePath(path);
//            return _pathMappings.ContainsKey(normalizedPath) || FindFuzzyMatch(normalizedPath).HasValue;
//        }

//        public IEnumerable<string> GetAvailablePaths()
//        {
//            return _pathMappings.Keys.OrderBy(k => k);
//        }

//        private string NormalizePath(string path)
//        {
//            if (string.IsNullOrEmpty(path))
//                return string.Empty;

//            // Loại bỏ ký tự '/' ở đầu và cuối
//            path = path.Trim('/').ToLower();

//            // Thay thế multiple slashes thành single slash
//            while (path.Contains("//"))
//            {
//                path = path.Replace("//", "/");
//            }

//            return path;
//        }

//        private (Type viewType, Type? viewModelType)? FindFuzzyMatch(string path)
//        {
//            if (string.IsNullOrEmpty(path))
//                return null;

//            var pathParts = path.Split('/');
//            var lastPart = pathParts.Last();

//            // Tìm kiếm theo tên cuối cùng trong path
//            var exactMatch = _pathMappings.FirstOrDefault(kvp =>
//                kvp.Key.EndsWith(lastPart) || kvp.Key.EndsWith($"/{lastPart}"));

//            if (!exactMatch.Equals(default(KeyValuePair<string, (Type, Type?)>)))
//            {
//                return exactMatch.Value;
//            }

//            // Tìm kiếm tương đối - chứa một phần của path
//            var partialMatch = _pathMappings.FirstOrDefault(kvp =>
//                pathParts.Any(part => kvp.Key.Contains(part)));

//            if (!partialMatch.Equals(default(KeyValuePair<string, (Type, Type?)>)))
//            {
//                return partialMatch.Value;
//            }

//            return null;
//        }

//        // Phương thức để debug - in ra tất cả mappings
//        public void LogAllMappings()
//        {
//            _logger.LogInformation("=== Tất cả Path Mappings ===");
//            foreach (var mapping in _pathMappings.OrderBy(kvp => kvp.Key))
//            {
//                _logger.LogInformation($"Path: {mapping.Key} -> View: {mapping.Value.viewType.Name}, ViewModel: {mapping.Value.viewModelType?.Name ?? "None"}");
//            }
//            _logger.LogInformation($"=== Tổng cộng: {_pathMappings.Count} mappings ===");
//        }
//    }

//}
