using FlowMy.ViewModels;
using FlowMy.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class HtmlUiEditorPopupWindow : Window
    {
        private readonly HtmlUiNodeDialogViewModel _viewModel;
        private readonly List<SyntaxHighlightCodeEditor> _jsEditors = new();
        private readonly Dictionary<SyntaxHighlightCodeEditor, JsTabMeta> _jsTabMeta = new();
        private const string MultiJsTabMarkerPrefix = "// [FLOW_JS_TAB:";

        private sealed class JsTabMeta
        {
            public int Priority { get; set; }
            public string Title { get; set; } = string.Empty;
        }

        private sealed class JsSegment
        {
            public string Code { get; set; } = string.Empty;
            public int Priority { get; set; } = 1000;
            public string Title { get; set; } = "JS";
        }

        public HtmlUiEditorPopupWindow(HtmlUiNodeDialogViewModel viewModel, Window owner)
        {
            InitializeComponent();
            Owner = owner;
            _viewModel = viewModel;
            DataContext = viewModel;
            InitializeJsTabsFromViewModel();
        }

        private void ClosePopupButton_Click(object sender, RoutedEventArgs e)
        {
            SyncJsTabsToViewModel();
            Close();
        }

        private void AddJsTabButton_Click(object sender, RoutedEventArgs e)
        {
            AddDynamicJsTab(string.Empty, true);
            SyncJsTabsToViewModel();
        }

        private void MoveJsTabUpButton_Click(object sender, RoutedEventArgs e) => MoveSelectedJsTabBy(-1);
        private void MoveJsTabDownButton_Click(object sender, RoutedEventArgs e) => MoveSelectedJsTabBy(+1);

        private void DeleteJsTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (_jsEditors.Count <= 1)
            {
                MessageBox.Show(this, "Phải giữ lại ít nhất 1 tab JS.", "Không thể xóa", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (PopupJsSubTabsControl.SelectedItem is not TabItem selectedTab || selectedTab.Content is not SyntaxHighlightCodeEditor selectedEditor)
                return;

            var removedIndex = _jsEditors.IndexOf(selectedEditor);
            _jsTabMeta.Remove(selectedEditor);
            _jsEditors.Remove(selectedEditor);
            PopupJsSubTabsControl.Items.Remove(selectedTab);
            NormalizePriorityByCurrentOrder();
            SortJsTabsByPriority();
            PopupJsSubTabsControl.SelectedIndex = System.Math.Max(0, System.Math.Min(PopupJsSubTabsControl.Items.Count - 1, removedIndex));
            SyncJsTabsToViewModel();
        }

        private void MoveSelectedJsTabBy(int delta)
        {
            if (PopupJsSubTabsControl.SelectedItem is not TabItem selectedTab || selectedTab.Content is not SyntaxHighlightCodeEditor selectedEditor)
                return;

            var currentIndex = _jsEditors.IndexOf(selectedEditor);
            if (currentIndex < 0) return;
            var targetIndex = currentIndex + delta;
            if (targetIndex < 0 || targetIndex >= _jsEditors.Count) return;

            var orderedEditors = _jsEditors.ToList();
            (orderedEditors[currentIndex], orderedEditors[targetIndex]) = (orderedEditors[targetIndex], orderedEditors[currentIndex]);
            for (int i = 0; i < orderedEditors.Count; i++)
            {
                if (_jsTabMeta.TryGetValue(orderedEditors[i], out var meta))
                    meta.Priority = i + 1;
            }

            SortJsTabsByPriority();
            var newSelectedTab = PopupJsSubTabsControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(t => ReferenceEquals(t.Content as SyntaxHighlightCodeEditor, selectedEditor));
            if (newSelectedTab != null)
                PopupJsSubTabsControl.SelectedItem = newSelectedTab;

            SyncJsTabsToViewModel();
        }

        private void InitializeJsTabsFromViewModel()
        {
            while (PopupJsSubTabsControl.Items.Count > 1)
                PopupJsSubTabsControl.Items.RemoveAt(PopupJsSubTabsControl.Items.Count - 1);

            _jsEditors.Clear();
            _jsTabMeta.Clear();
            _jsEditors.Add(PopupJsEditor);

            var segments = SplitJsCodeToSegments(_viewModel.JsCode);
            PopupJsEditor.Text = segments.Count > 0 ? segments[0].Code : string.Empty;
            _jsTabMeta[PopupJsEditor] = new JsTabMeta
            {
                Priority = segments.Count > 0 ? segments[0].Priority : 1000,
                Title = segments.Count > 0 ? segments[0].Title : "JS 1"
            };

            for (int i = 1; i < segments.Count; i++)
                AddDynamicJsTab(segments[i].Code, false, segments[i].Priority, segments[i].Title);

            SortJsTabsByPriority();
            PopupJsSubTabsControl.SelectedIndex = 0;
        }

        private void AddDynamicJsTab(string initialCode, bool selectTab, int? priority = null, string? title = null)
        {
            var editor = new SyntaxHighlightCodeEditor
            {
                Text = initialCode ?? string.Empty,
                SyntaxLanguage = "JavaScript",
                IsAutoHighlightEnabled = false,
                MinHeight = 260
            };
            editor.SetBinding(SyntaxHighlightCodeEditor.CodeFontSizeProperty, new Binding("CodeFontSize") { Mode = BindingMode.TwoWay });

            var tab = new TabItem
            {
                Header = "JS",
                Content = editor,
                Style = (Style)FindResource("HttpTabItemStyle"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            PopupJsSubTabsControl.Items.Add(tab);
            _jsEditors.Add(editor);
            _jsTabMeta[editor] = new JsTabMeta
            {
                Priority = priority ?? GetNextJsPriority(),
                Title = string.IsNullOrWhiteSpace(title) ? "JS" : title.Trim()
            };
            SortJsTabsByPriority();
            if (selectTab) PopupJsSubTabsControl.SelectedItem = tab;
        }

        private int GetNextJsPriority() => _jsTabMeta.Count == 0 ? 1000 : _jsTabMeta.Values.Max(x => x.Priority) + 1;

        private void NormalizePriorityByCurrentOrder()
        {
            for (int i = 0; i < _jsEditors.Count; i++)
            {
                if (_jsTabMeta.TryGetValue(_jsEditors[i], out var meta))
                    meta.Priority = i + 1;
            }
        }

        private void SortJsTabsByPriority()
        {
            var selectedEditor = GetActiveJsEditor();
            var orderedTabs = PopupJsSubTabsControl.Items
                .OfType<TabItem>()
                .Select(t => new { Tab = t, Editor = t.Content as SyntaxHighlightCodeEditor })
                .Where(x => x.Editor != null)
                .OrderBy(x => _jsTabMeta.TryGetValue(x.Editor!, out var meta) ? meta.Priority : int.MaxValue)
                .ThenBy(x => _jsEditors.IndexOf(x.Editor!))
                .ToList();

            PopupJsSubTabsControl.Items.Clear();
            _jsEditors.Clear();
            foreach (var x in orderedTabs)
            {
                PopupJsSubTabsControl.Items.Add(x.Tab);
                _jsEditors.Add(x.Editor!);
            }

            RefreshJsSubTabHeaders();
            if (selectedEditor != null)
            {
                var selectedTab = orderedTabs.FirstOrDefault(x => ReferenceEquals(x.Editor, selectedEditor))?.Tab;
                if (selectedTab != null) PopupJsSubTabsControl.SelectedItem = selectedTab;
            }
        }

        private void RefreshJsSubTabHeaders()
        {
            for (int i = 0; i < PopupJsSubTabsControl.Items.Count; i++)
            {
                if (PopupJsSubTabsControl.Items[i] is not TabItem tab || tab.Content is not SyntaxHighlightCodeEditor ed) continue;
                var meta = _jsTabMeta.TryGetValue(ed, out var m) ? m : null;
                var title = meta?.Title;
                var pr = meta?.Priority ?? i + 1;
                if (string.IsNullOrWhiteSpace(title) || title!.StartsWith("JS ", System.StringComparison.OrdinalIgnoreCase))
                    tab.Header = $"#{pr} JS-{i + 1}";
                else
                    tab.Header = $"#{pr} {title}";
            }
        }

        private SyntaxHighlightCodeEditor? GetActiveJsEditor()
        {
            if (PopupJsSubTabsControl.SelectedItem is TabItem tab && tab.Content is SyntaxHighlightCodeEditor selectedEditor)
                return selectedEditor;
            return PopupJsEditor;
        }

        private void SyncJsTabsToViewModel()
        {
            _viewModel.JsCode = BuildCombinedJsCodeFromTabs();
        }

        private string BuildCombinedJsCodeFromTabs()
        {
            var editors = _jsEditors.Where(e => e != null).ToList();
            if (editors.Count <= 1)
                return editors.FirstOrDefault()?.Text ?? string.Empty;

            var parts = new List<string>();
            for (int i = 0; i < editors.Count; i++)
            {
                var code = editors[i].Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                var meta = _jsTabMeta.TryGetValue(editors[i], out var m) ? m : new JsTabMeta { Priority = i + 1, Title = $"JS-{i + 1}" };
                var safeTitle = (meta.Title ?? $"JS-{i + 1}").Replace("]", ")");
                parts.Add($"{MultiJsTabMarkerPrefix}{i + 1}|P:{meta.Priority}|T:{safeTitle}]");
                parts.Add(code);
            }

            return parts.Count == 0 ? string.Empty : string.Join("\n\n", parts);
        }

        private static List<JsSegment> SplitJsCodeToSegments(string? jsCode)
        {
            var text = jsCode ?? string.Empty;
            if (!text.Contains(MultiJsTabMarkerPrefix))
                return new List<JsSegment> { new JsSegment { Code = text, Priority = 1000, Title = "JS 1" } };

            var blocks = new List<JsSegment>();
            var regex = new Regex(@"^\s*//\s*\[FLOW_JS_TAB:(\d+)(?:\|P:(\d+))?(?:\|T:(.*?))?\]\s*$", RegexOptions.Multiline);
            var matches = regex.Matches(text).Cast<Match>().ToList();
            if (matches.Count == 0)
                return new List<JsSegment> { new JsSegment { Code = text, Priority = 1000, Title = "JS 1" } };

            for (int i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index + matches[i].Length;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                var content = text[start..end].Trim();
                var idx = int.TryParse(matches[i].Groups[1].Value, out var parsedIdx) ? parsedIdx : i + 1;
                var pr = int.TryParse(matches[i].Groups[2].Value, out var parsedPr) ? parsedPr : idx;
                var title = matches[i].Groups[3].Success ? matches[i].Groups[3].Value.Trim() : $"JS-{idx}";
                blocks.Add(new JsSegment { Code = content, Priority = pr, Title = title });
            }

            return blocks.Count == 0
                ? new List<JsSegment> { new JsSegment { Code = string.Empty, Priority = 1000, Title = "JS 1" } }
                : blocks.OrderBy(x => x.Priority).ThenBy(x => x.Title, System.StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}

