using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Helpers;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

namespace FlowMy.ViewModels
{
    public partial class BorderHighlightNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly BorderHighlightNode _node;

        // Properties đặc thù của BorderHighlightNode
        private string _borderColorHex;
        private int _borderThickness;
        private int _gradientSize;
        private double _opacity;
        private BorderEffectType _effectType;
        private BorderHighlightMode _highlightMode;
        private string _targetProcessName;
        private string _targetWindowTitle;
        private int _durationMs;
        private bool _waitForCompletion;

        // Collection cho combobox chọn node khác
        private List<BorderHighlightNodeSelectionItem> _availableBorderHighlightNodes;

        // Window selection properties
        private WindowInfo? _selectedTargetWindow;

        public ObservableCollection<WindowInfo> ActiveWindows { get; } = new();
        public bool IsTargetAppVisible => HighlightMode == BorderHighlightMode.TargetApp;

        public BorderHighlightNodeDialogViewModel(BorderHighlightNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _node = node;

            // Load từ node
            _borderColorHex = node.BorderColorHex;
            _borderThickness = node.BorderThickness;
            _gradientSize = node.GradientSize;
            _opacity = node.Opacity;
            _effectType = node.EffectType;
            _highlightMode = node.HighlightMode;
            _targetProcessName = node.TargetProcessName;
            _targetWindowTitle = node.TargetWindowTitle;
            _durationMs = node.DurationMs;
            _waitForCompletion = node.WaitForCompletion;

            LoadWindowsCommand = new RelayCommand(ExecuteLoadWindows);
            if (_highlightMode == BorderHighlightMode.TargetApp)
            {
                ExecuteLoadWindows();
            }

            // Load danh sách node BorderHighlight khác
            LoadAvailableBorderHighlightNodes();
        }

        protected override string GetDefaultTitle() => "Border Highlight";

        // ─── Properties đặc thù ─────────────────────────────────────────────────────

        public string BorderColorHex
        {
            get => _borderColorHex;
            set
            {
                if (_borderColorHex == value) return;
                _borderColorHex = value;
                OnPropertyChanged();
                _node.BorderColorHex = value;
            }
        }

        public int BorderThickness
        {
            get => _borderThickness;
            set
            {
                if (_borderThickness == value) return;
                _borderThickness = value;
                OnPropertyChanged();
                _node.BorderThickness = value;
            }
        }

        public int GradientSize
        {
            get => _gradientSize;
            set
            {
                if (_gradientSize == value) return;
                _gradientSize = value;
                OnPropertyChanged();
                _node.GradientSize = value;
            }
        }

        public double Opacity
        {
            get => _opacity;
            set
            {
                if (Math.Abs(_opacity - value) < 0.01) return;
                _opacity = value;
                OnPropertyChanged();
                _node.Opacity = value;
            }
        }

        public BorderEffectType EffectType
        {
            get => _effectType;
            set
            {
                if (_effectType == value) return;
                _effectType = value;
                OnPropertyChanged();
                _node.EffectType = value;
            }
        }

        public BorderHighlightMode HighlightMode
        {
            get => _highlightMode;
            set
            {
                if (_highlightMode == value) return;
                _highlightMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTargetAppVisible));
                _node.HighlightMode = value;

                // Load windows when switching to TargetApp mode
                if (value == BorderHighlightMode.TargetApp)
                {
                    ExecuteLoadWindows();
                }
            }
        }

        public string TargetProcessName
        {
            get => _targetProcessName;
            set
            {
                if (_targetProcessName == value) return;
                _targetProcessName = value;
                OnPropertyChanged();
                _node.TargetProcessName = value;
            }
        }

        public string TargetWindowTitle
        {
            get => _targetWindowTitle;
            set
            {
                if (_targetWindowTitle == value) return;
                _targetWindowTitle = value;
                OnPropertyChanged();
                _node.TargetWindowTitle = value;
            }
        }

        public int DurationMs
        {
            get => _durationMs;
            set
            {
                if (_durationMs == value) return;
                _durationMs = value;
                OnPropertyChanged();
                _node.DurationMs = value;
            }
        }

        // ─── Window selection properties ────────────────────────────────────────────────

        public WindowInfo? SelectedTargetWindow
        {
            get => _selectedTargetWindow;
            set
            {
                if (_selectedTargetWindow == value) return;
                _selectedTargetWindow = value;
                OnPropertyChanged();

                // Save selected window to node
                if (value != null)
                {
                    _node.SelectedWindowJson = JsonSerializer.Serialize(value);
                    _node.TargetProcessName = value.ProcessName;
                    _node.TargetWindowTitle = value.Title;
                }
                else
                {
                    _node.SelectedWindowJson = "";
                    _node.TargetProcessName = "";
                    _node.TargetWindowTitle = "";
                }
            }
        }

        public ICommand LoadWindowsCommand { get; }

        // ─── WaitForCompletion property ───────────────────────────────────────────────────

        public bool WaitForCompletion
        {
            get => _waitForCompletion;
            set
            {
                if (_waitForCompletion == value) return;
                _waitForCompletion = value;
                OnPropertyChanged();
                _node.WaitForCompletion = value;
            }
        }

        // ─── Collections cho combobox ────────────────────────────────────────────────

        public List<BorderHighlightNodeSelectionItem> AvailableBorderHighlightNodes
        {
            get => _availableBorderHighlightNodes;
            set
            {
                if (_availableBorderHighlightNodes == value) return;
                _availableBorderHighlightNodes = value;
                OnPropertyChanged();
            }
        }

        // ─── Options cho combobox ─────────────────────────────────────────────────────

        public List<DisplayValuePair<BorderEffectType>> EffectTypeOptions => new()
        {
            new DisplayValuePair<BorderEffectType>(BorderEffectType.None, "Không có hiệu ứng"),
            new DisplayValuePair<BorderEffectType>(BorderEffectType.Pulse, "Nhấp nháy"),
            new DisplayValuePair<BorderEffectType>(BorderEffectType.Glow, "Phát sáng"),
            new DisplayValuePair<BorderEffectType>(BorderEffectType.Rainbow, "Cầu vồng")
        };

        public List<DisplayValuePair<BorderHighlightMode>> HighlightModeOptions => new()
        {
            new DisplayValuePair<BorderHighlightMode>(BorderHighlightMode.Fullscreen, "Toàn màn hình"),
            new DisplayValuePair<BorderHighlightMode>(BorderHighlightMode.TargetApp, "Cửa sổ cụ thể")
        };

        // ─── Helper methods ───────────────────────────────────────────────────────────

        private void ExecuteLoadWindows()
        {
            ActiveWindows.Clear();
            var windows = WindowHelper.GetActiveWindows();
            foreach (var w in windows)
            {
                ActiveWindows.Add(w);
            }

            // Resolve selected window from saved JSON
            if (!string.IsNullOrWhiteSpace(_node.SelectedWindowJson))
            {
                try
                {
                    var savedWindow = JsonSerializer.Deserialize<WindowInfo>(_node.SelectedWindowJson);
                    if (savedWindow != null)
                    {
                        SelectedTargetWindow = ActiveWindows.FirstOrDefault(w => w.Handle == savedWindow.Handle);
                        // Fallback: match by ProcessName and Title
                        if (SelectedTargetWindow == null)
                        {
                            SelectedTargetWindow = ActiveWindows.FirstOrDefault(w =>
                                w.ProcessName == savedWindow.ProcessName && w.Title == savedWindow.Title);
                        }
                    }
                }
                catch { }
            }
        }

        private void LoadAvailableBorderHighlightNodes()
        {
            _availableBorderHighlightNodes = new List<BorderHighlightNodeSelectionItem>();

            // Lấy tất cả node trong workflow hiện tại
            var nodes = _host?.ViewModel?.Nodes;
            if (nodes == null) return;

            // Lấy danh sách node ID đã chọn từ JSON
            List<string>? selectedNodeIds = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(_node.NodesToDisableJson))
                {
                    selectedNodeIds = JsonSerializer.Deserialize<List<string>>(_node.NodesToDisableJson);
                }
            }
            catch { }

            selectedNodeIds ??= new List<string>();

            // Tìm tất cả BorderHighlightNode khác (trừ node hiện tại)
            foreach (var node in nodes)
            {
                if (node is BorderHighlightNode bhNode && bhNode.Id != _node.Id)
                {
                    _availableBorderHighlightNodes.Add(new BorderHighlightNodeSelectionItem
                    {
                        NodeId = bhNode.Id,
                        Title = bhNode.Title ?? "Border Highlight",
                        IsSelected = selectedNodeIds.Contains(bhNode.Id)
                    });
                }
            }
        }

        // ─── Save nodes to disable ────────────────────────────────────────────────────

        public void SaveNodesToDisable()
        {
            var selectedIds = _availableBorderHighlightNodes
                .Where(x => x.IsSelected)
                .Select(x => x.NodeId)
                .ToList();

            _node.NodesToDisableJson = JsonSerializer.Serialize(selectedIds);
        }
    }

    // ─── Helper class cho selection item ─────────────────────────────────────────────

    public class BorderHighlightNodeSelectionItem
    {
        public string NodeId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    // ─── Helper class cho display/value pair ─────────────────────────────────────────

    public class DisplayValuePair<T>
    {
        public DisplayValuePair(T value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public T Value { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
