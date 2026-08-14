using FlowMy.Routers;
using FlowMy.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace FlowMy.Services
{
    public class OptimizedViewFactoryService : IViewFactoryService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OptimizedViewFactoryService> _logger;
        private readonly Dictionary<string, (Type viewType, Type? viewModelType)> _pathMappings;

        public OptimizedViewFactoryService(IServiceProvider serviceProvider, ILogger<OptimizedViewFactoryService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            // Copy static mappings - INSTANT performance!
            _pathMappings = new Dictionary<string, (Type viewType, Type? viewModelType)>(ManualViewMappings.Mappings);

            _logger.LogInformation($"Loaded {_pathMappings.Count} view mappings instantly");
            _logger.LogDebug($"Build info: {ManualViewMappings.BuildInfo}");

            // Validate mappings at startup (optional - catch config errors early)
            ValidateMappingsAtStartup();
        }

        private void ValidateMappingsAtStartup()
        {
            try
            {
                var (isValid, errors) = ManualViewMappings.ValidateAllMappings();
                if (!isValid)
                {
                    _logger.LogWarning($"Found {errors.Count} mapping validation errors:");
                    foreach (var error in errors.Take(5)) // Log first 5 errors
                    {
                        _logger.LogWarning($"  - {error}");
                    }
                    if (errors.Count > 5)
                    {
                        _logger.LogWarning($"  ... and {errors.Count - 5} more errors");
                    }
                }
                else
                {
                    _logger.LogDebug("All view mappings validated successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating view mappings");
            }
        }

        public UserControl? CreateView(string path)
        {
            try
            {
                var normalizedPath = NormalizePath(path);

                // Fast O(1) dictionary lookup
                if (_pathMappings.TryGetValue(normalizedPath, out var mapping))
                {
                    return CreateViewInstance(mapping.viewType, path);
                }

                // Fallback: Fuzzy search
                var fuzzyMatch = FindFuzzyMatch(normalizedPath);
                if (fuzzyMatch.HasValue)
                {
                    return CreateViewInstance(fuzzyMatch.Value.viewType, path, isFuzzy: true);
                }

                _logger.LogWarning($"No view found for path: {path}. Available paths: {string.Join(", ", GetAvailablePaths().Take(10))}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating view for path: {path}");
                return null;
            }
        }

        private UserControl? CreateViewInstance(Type viewType, string path, bool isFuzzy = false)
        {
            try
            {
                var view = _serviceProvider.GetService(viewType) as UserControl;
                if (view != null)
                {
                    var matchType = isFuzzy ? "fuzzy match" : "exact match";
                    _logger.LogDebug($"Created view via {matchType} for path '{path}': {viewType.Name}");
                    return view;
                }
                else
                {
                    _logger.LogWarning($"Failed to resolve view from DI container: {viewType.Name}. Is it registered?");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating view instance: {viewType.Name}");
                return null;
            }
        }

        public object? CreateViewModel(string path)
        {
            try
            {
                var normalizedPath = NormalizePath(path);

                if (_pathMappings.TryGetValue(normalizedPath, out var mapping) && mapping.viewModelType != null)
                {
                    return CreateViewModelInstance(mapping.viewModelType, path);
                }

                var fuzzyMatch = FindFuzzyMatch(normalizedPath);
                if (fuzzyMatch?.viewModelType != null)
                {
                    return CreateViewModelInstance(fuzzyMatch.Value.viewModelType, path, isFuzzy: true);
                }

                _logger.LogDebug($"No viewmodel found for path: {path}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating viewmodel for path: {path}");
                return null;
            }
        }

        private object? CreateViewModelInstance(Type viewModelType, string path, bool isFuzzy = false)
        {
            try
            {
                var viewModel = _serviceProvider.GetService(viewModelType);
                if (viewModel != null)
                {
                    var matchType = isFuzzy ? "fuzzy match" : "exact match";
                    _logger.LogDebug($"Created viewmodel via {matchType} for path '{path}': {viewModelType.Name}");
                    return viewModel;
                }
                else
                {
                    _logger.LogWarning($"Failed to resolve viewmodel from DI container: {viewModelType.Name}. Is it registered?");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating viewmodel instance: {viewModelType.Name}");
                return null;
            }
        }

        public (UserControl? view, object? viewModel) CreateViewWithViewModel(string path)
        {
            var view = CreateView(path);
            var viewModel = CreateViewModel(path);

            if (view != null && viewModel != null)
            {
                view.DataContext = viewModel;
                _logger.LogDebug($"Successfully created and linked view-viewmodel for path: {path}");
            }
            else if (view != null)
            {
                _logger.LogDebug($"Created view without viewmodel for path: {path}");
            }

            return (view, viewModel);
        }

        public bool IsValidPath(string path)
        {
            var normalizedPath = NormalizePath(path);
            return _pathMappings.ContainsKey(normalizedPath) || FindFuzzyMatch(normalizedPath).HasValue;
        }

        public IEnumerable<string> GetAvailablePaths()
        {
            return _pathMappings.Keys.OrderBy(k => k);
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            path = path.Trim('/').ToLower();

            // Clean up multiple slashes
            while (path.Contains("//"))
            {
                path = path.Replace("//", "/");
            }

            return path;
        }

        private (Type viewType, Type? viewModelType)? FindFuzzyMatch(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var lastPart = pathParts.LastOrDefault();

            if (string.IsNullOrEmpty(lastPart))
                return null;

            // Strategy 1: Exact match on last part
            var exactMatch = _pathMappings.FirstOrDefault(kvp =>
                kvp.Key.EndsWith(lastPart, StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.EndsWith($"/{lastPart}", StringComparison.OrdinalIgnoreCase));

            if (!exactMatch.Equals(default(KeyValuePair<string, (Type, Type?)>)))
            {
                _logger.LogDebug($"Fuzzy match found for '{path}': {exactMatch.Key}");
                return exactMatch.Value;
            }

            // Strategy 2: Contains any path part
            var partialMatch = _pathMappings.FirstOrDefault(kvp =>
                pathParts.Any(part => kvp.Key.Contains(part, StringComparison.OrdinalIgnoreCase)));

            if (!partialMatch.Equals(default(KeyValuePair<string, (Type, Type?)>)))
            {
                _logger.LogDebug($"Partial fuzzy match found for '{path}': {partialMatch.Key}");
                return partialMatch.Value;
            }

            return null;
        }

        public void LogAllMappings()
        {
            _logger.LogInformation("=== Static View Mappings ===");
            foreach (var mapping in _pathMappings.OrderBy(kvp => kvp.Key))
            {
                _logger.LogInformation($"  {mapping.Key} -> View: {mapping.Value.viewType.Name}, ViewModel: {mapping.Value.viewModelType?.Name ?? "None"}");
            }
            _logger.LogInformation($"=== Total: {_pathMappings.Count} mappings ===");
            _logger.LogInformation($"Info: {ManualViewMappings.BuildInfo}");
        }

        /// <summary>
        /// Add mapping at runtime (for dynamic scenarios)
        /// </summary>
        public void AddMapping(string path, Type viewType, Type? viewModelType = null)
        {
            var normalizedPath = NormalizePath(path);
            _pathMappings[normalizedPath] = (viewType, viewModelType);
            _logger.LogDebug($"Added runtime mapping: {path} -> {viewType.Name}");
        }

        /// <summary>
        /// Remove mapping at runtime
        /// </summary>
        public bool RemoveMapping(string path)
        {
            var normalizedPath = NormalizePath(path);
            var removed = _pathMappings.Remove(normalizedPath);
            if (removed)
            {
                _logger.LogDebug($"Removed mapping for path: {path}");
            }
            return removed;
        }

        /// <summary>
        /// Get mapping info for debugging
        /// </summary>
        public (Type? viewType, Type? viewModelType) GetMappingInfo(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (_pathMappings.TryGetValue(normalizedPath, out var mapping))
            {
                return (mapping.viewType, mapping.viewModelType);
            }
            return (null, null);
        }
    }
}
