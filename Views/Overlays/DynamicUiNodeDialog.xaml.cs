using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FlowMy.Controls;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.IO;
using FlowMy.Services.Rendering;

namespace FlowMy.Views.Overlays
{
    public partial class DynamicUiNodeDialog : BaseNodeDialog
    {
        private readonly DynamicUiNodeDialogViewModel _viewModel;

        private readonly List<SyntaxHighlightCodeEditor> _jsEditors = new();
        private readonly Dictionary<SyntaxHighlightCodeEditor, JsTabMeta> _jsTabMeta = new();
        private const string MultiJsTabMarkerPrefix = "// [FLOW_JS_TAB:";
        private bool _isSyncingJsTabsToViewModel;

        private sealed class JsTabMeta
        {
            public int Priority { get; set; }
            public string Title { get; set; } = string.Empty;
        }

        public DynamicUiNodeDialog(DynamicUiNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new DynamicUiNodeDialogViewModel(node, host);
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            InitializeBase(_viewModel, owner);

            // If we have color picker previews, update them
            UpdateTitleColorPreview();

            Closed += DynamicUiNodeDialog_ClosedCleanup;
        }

        private void DynamicUiNodeDialog_ClosedCleanup(object? sender, EventArgs e)
        {
            Closed -= DynamicUiNodeDialog_ClosedCleanup;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DynamicUiNodeDialogViewModel.JsCode) || _isSyncingJsTabsToViewModel)
                return;

            Dispatcher.InvokeAsync(() =>
            {
                if (!IsLoaded) return;
                InitializeJsTabsFromViewModel();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        protected override Panel? GetInputsPanel() => null;

        protected override Panel? GetOutputsPanel() => null;

        protected override void OnLoaded()
        {
            base.OnLoaded();
            InitializeJsTabsFromViewModel();
            UpdateTitleColorPreview();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateAllBindings();
            _viewModel.SaveTitleCommand?.Execute(null);
            Close();
        }

        protected override void BeforeSaveOnClose()
        {
            try
            {
                UpdateAllBindings();
                _viewModel.SaveTitleCommand?.Execute(null);
            }
            catch { }
            FlushReuseRoutesComboBoxBindings();
            base.BeforeSaveOnClose();
        }

        public void UpdateAllBindings()
        {
            try
            {
                HtmlEditor?.ForceUpdateBinding();
                foreach (var editor in _jsEditors) editor?.ForceUpdateBinding();
                SyncJsTabsToViewModel();
                CssEditor?.ForceUpdateBinding();
                ParamsEditor?.ForceUpdateBinding();
            }
            catch { }

            if (System.Windows.Input.Keyboard.FocusedElement is UIElement element)
            {
                element.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
            }
        }

        private void AddJsTabButton_Click(object sender, RoutedEventArgs e)
        {
            AddDynamicJsTab(string.Empty, selectTab: true);
            SyncJsTabsToViewModel();
        }

        private void MoveJsTabUpButton_Click(object sender, RoutedEventArgs e)
        {
            MoveSelectedJsTabBy(-1);
        }

        private void MoveJsTabDownButton_Click(object sender, RoutedEventArgs e)
        {
            MoveSelectedJsTabBy(+1);
        }

        private void DeleteJsTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (JsSubTabsControl == null) return;

            if (_jsEditors.Count <= 1)
            {
                MessageBox.Show(
                    this,
                    "Phải giữ lại ít nhất 1 tab JS.",
                    "Không thể xóa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (JsSubTabsControl.SelectedItem is not TabItem selectedTab || selectedTab.Content is not SyntaxHighlightCodeEditor selectedEditor)
                return;

            var removedIndex = _jsEditors.IndexOf(selectedEditor);
            _jsTabMeta.Remove(selectedEditor);
            _jsEditors.Remove(selectedEditor);
            JsSubTabsControl.Items.Remove(selectedTab);

            for (int i = 0; i < _jsEditors.Count; i++)
            {
                if (_jsTabMeta.TryGetValue(_jsEditors[i], out var meta))
                    meta.Priority = i + 1;
            }

            SortJsTabsByPriority();
            JsSubTabsControl.SelectedIndex = Math.Max(0, Math.Min(JsSubTabsControl.Items.Count - 1, removedIndex));
            SyncJsTabsToViewModel();
        }

        private void MoveSelectedJsTabBy(int delta)
        {
            if (JsSubTabsControl?.SelectedItem is not TabItem selectedTab || selectedTab.Content is not SyntaxHighlightCodeEditor selectedEditor)
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
            var newSelectedTab = JsSubTabsControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(t => ReferenceEquals(t.Content as SyntaxHighlightCodeEditor, selectedEditor));
            if (newSelectedTab != null)
                JsSubTabsControl.SelectedItem = newSelectedTab;

            SyncJsTabsToViewModel();
        }

        private void InitializeJsTabsFromViewModel()
        {
            if (JsSubTabsControl == null || JsEditor == null || _viewModel == null) return;

            while (JsSubTabsControl.Items.Count > 1)
                JsSubTabsControl.Items.RemoveAt(JsSubTabsControl.Items.Count - 1);

            _jsEditors.Clear();
            _jsEditors.Add(JsEditor);
            _jsTabMeta.Clear();

            var segments = SplitJsCodeToSegments(_viewModel.JsCode);
            JsEditor.Text = segments.Count > 0 ? segments[0].Code : string.Empty;
            _jsTabMeta[JsEditor] = new JsTabMeta
            {
                Priority = segments.Count > 0 ? segments[0].Priority : 1000,
                Title = segments.Count > 0 ? segments[0].Title : "JS 1"
            };

            for (int i = 1; i < segments.Count; i++)
                AddDynamicJsTab(segments[i].Code, selectTab: false, priority: segments[i].Priority, title: segments[i].Title);

            SortJsTabsByPriority();
            JsSubTabsControl.SelectedIndex = 0;
        }

        private void SetJsTabsFromSingleCode(string jsCode, int? priority = null, string? title = null)
        {
            if (JsSubTabsControl == null || JsEditor == null) return;

            while (JsSubTabsControl.Items.Count > 1)
                JsSubTabsControl.Items.RemoveAt(JsSubTabsControl.Items.Count - 1);

            _jsEditors.Clear();
            _jsEditors.Add(JsEditor);
            _jsTabMeta.Clear();
            JsEditor.Text = jsCode ?? string.Empty;
            _jsTabMeta[JsEditor] = new JsTabMeta
            {
                Priority = priority ?? 1000,
                Title = string.IsNullOrWhiteSpace(title) ? "JS 1" : title.Trim()
            };

            SortJsTabsByPriority();
            JsSubTabsControl.SelectedIndex = 0;
            SyncJsTabsToViewModel();
        }

        private void AddDynamicJsTab(string initialCode, bool selectTab, int? priority = null, string? title = null)
        {
            if (JsSubTabsControl == null) return;

            var editor = new SyntaxHighlightCodeEditor
            {
                Text = initialCode ?? string.Empty,
                SyntaxLanguage = "JavaScript",
                IsAutoHighlightEnabled = false,
                MinHeight = 250,
                Height = 250
            };
            editor.SetBinding(SyntaxHighlightCodeEditor.CodeFontSizeProperty, new System.Windows.Data.Binding("CodeFontSize") { Mode = System.Windows.Data.BindingMode.TwoWay });

            var tab = new TabItem
            {
                Header = "JS",
                Content = editor,
                Style = (Style)FindResource("HttpTabItemStyle"),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };

            JsSubTabsControl.Items.Add(tab);
            _jsEditors.Add(editor);
            _jsTabMeta[editor] = new JsTabMeta
            {
                Priority = priority ?? GetNextJsPriority(),
                Title = string.IsNullOrWhiteSpace(title) ? "JS" : title.Trim()
            };
            SortJsTabsByPriority();

            if (selectTab)
                JsSubTabsControl.SelectedItem = tab;
        }

        private int GetNextJsPriority()
        {
            return _jsTabMeta.Count == 0 ? 1000 : _jsTabMeta.Values.Max(x => x.Priority) + 1;
        }

        private void SortJsTabsByPriority()
        {
            if (JsSubTabsControl == null) return;

            var selectedEditor = GetActiveJsEditor();
            var orderedTabs = JsSubTabsControl.Items
                .OfType<TabItem>()
                .Select(t => new { Tab = t, Editor = t.Content as SyntaxHighlightCodeEditor })
                .Where(x => x.Editor != null)
                .OrderBy(x => _jsTabMeta.TryGetValue(x.Editor!, out var meta) ? meta.Priority : int.MaxValue)
                .ThenBy(x => _jsEditors.IndexOf(x.Editor!))
                .ToList();

            JsSubTabsControl.Items.Clear();
            _jsEditors.Clear();
            foreach (var x in orderedTabs)
            {
                JsSubTabsControl.Items.Add(x.Tab);
                _jsEditors.Add(x.Editor!);
            }

            RefreshJsSubTabHeaders();
            if (selectedEditor != null)
            {
                var selectedTab = orderedTabs.FirstOrDefault(x => ReferenceEquals(x.Editor, selectedEditor))?.Tab;
                if (selectedTab != null) JsSubTabsControl.SelectedItem = selectedTab;
            }
        }

        private void RefreshJsSubTabHeaders()
        {
            if (JsSubTabsControl == null) return;
            for (int i = 0; i < JsSubTabsControl.Items.Count; i++)
            {
                if (JsSubTabsControl.Items[i] is not TabItem tab || tab.Content is not SyntaxHighlightCodeEditor ed) continue;
                var meta = _jsTabMeta.TryGetValue(ed, out var m) ? m : null;
                var title = meta?.Title;
                var pr = meta?.Priority ?? i + 1;
                if (string.IsNullOrWhiteSpace(title) || title!.StartsWith("JS ", StringComparison.OrdinalIgnoreCase))
                    tab.Header = $"#{pr} JS-{i + 1}";
                else
                    tab.Header = $"#{pr} {title}";
            }
        }

        private SyntaxHighlightCodeEditor? GetActiveJsEditor()
        {
            if (JsSubTabsControl?.SelectedItem is TabItem tab && tab.Content is SyntaxHighlightCodeEditor selectedEditor)
                return selectedEditor;
            return JsEditor;
        }

        private void SyncJsTabsToViewModel()
        {
            if (_viewModel == null) return;
            _isSyncingJsTabsToViewModel = true;
            try
            {
                _viewModel.JsCode = BuildCombinedJsCodeFromTabs();
            }
            finally
            {
                _isSyncingJsTabsToViewModel = false;
            }
        }

        private string BuildCombinedJsCodeFromTabs()
        {
            var editors = _jsEditors.Where(e => e != null).ToList();
            if (editors.Count <= 1)
                return editors.FirstOrDefault()?.Text ?? string.Empty;

            var parts = new System.Collections.Generic.List<string>();
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

        private sealed class JsSegment
        {
            public string Code { get; set; } = string.Empty;
            public int Priority { get; set; } = 1000;
            public string Title { get; set; } = "JS";
        }

        private static System.Collections.Generic.List<JsSegment> SplitJsCodeToSegments(string? jsCode)
        {
            var text = jsCode ?? string.Empty;
            if (!text.Contains(MultiJsTabMarkerPrefix))
                return new System.Collections.Generic.List<JsSegment> { new JsSegment { Code = text, Priority = 1000, Title = "JS 1" } };

            var blocks = new System.Collections.Generic.List<JsSegment>();
            var regex = new System.Text.RegularExpressions.Regex(@"^\s*//\s*\[FLOW_JS_TAB:(\d+)(?:\|P:(\d+))?(?:\|T:(.*?))?\]\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);
            var matches = regex.Matches(text).Cast<System.Text.RegularExpressions.Match>().ToList();
            if (matches.Count == 0)
                return new System.Collections.Generic.List<JsSegment> { new JsSegment { Code = text, Priority = 1000, Title = "JS 1" } };

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
                ? new System.Collections.Generic.List<JsSegment> { new JsSegment { Code = string.Empty, Priority = 1000, Title = "JS 1" } }
                : blocks.OrderBy(x => x.Priority).ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void FlushReuseRoutesComboBoxBindings()
        {
            var itemsControl = this.FindName("ReuseRoutesItemsControl") as ItemsControl;
            if (itemsControl != null)
            {
                foreach (var item in itemsControl.Items)
                {
                    if (itemsControl.ItemContainerGenerator.ContainerFromItem(item) is ContentPresenter contentPresenter)
                    {
                        var comboBoxes = FindVisualChildren<ComboBox>(contentPresenter);
                        foreach (var comboBox in comboBoxes)
                        {
                            comboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                            comboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                        }
                    }
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent != null)
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                    if (child is T t)
                    {
                        yield return t;
                    }
                    foreach (var childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void CopyApiDocRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            var text = btn.Tag as string;
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                System.Windows.Clipboard.SetText(text);
            }
            catch { }
        }

        private void CopyApiDocAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var docText = @"Sciter JS ↔ C# Host Functions:
- hostLive.on('key', function(val) { ... })
- hostLive.values
- hostAsync.on('key', function(val) { ... })
- hostAsync.values
- hostSubmit()
- sciterUpdate()
- sciterSubmitAndClose()
- sciterRunSingleNode()
- sciterRunFromNode()
- hostStart()
- hostCancel()
- hostResolvePath(localPath, requestId)
- hostCurl(rawCurl, fileName, key)
- hostPickImages(requestId)";
                System.Windows.Clipboard.SetText(docText);
            }
            catch { }
        }

        private void CopyAllCodeForAiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateAllBindings();
                var html = _viewModel.HtmlCode ?? string.Empty;
                var css = _viewModel.CssCode ?? string.Empty;
                var js = _viewModel.JsCode ?? string.Empty;
                var param = _viewModel.ParamsCode ?? string.Empty;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("#region HTML");
                sb.AppendLine(html);
                sb.AppendLine("#endregion");
                sb.AppendLine();
                sb.AppendLine("#region CSS");
                sb.AppendLine(css);
                sb.AppendLine("#endregion");
                sb.AppendLine();
                sb.AppendLine("#region JS");
                sb.AppendLine(js);
                sb.AppendLine("#endregion");
                sb.AppendLine();
                sb.AppendLine("#region PARAM");
                sb.AppendLine(param);
                sb.AppendLine("#endregion");

                System.Windows.Clipboard.SetText(sb.ToString());
                MessageBox.Show(this, "Đã copy toàn bộ HTML/CSS/JS/PARAM vào clipboard để gửi cho AI.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Copy thất bại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<(string Path, int Priority, string Title, string Code)> ReadJsImportParts(IEnumerable<string> jsFiles)
        {
            return jsFiles
                .Select(f => (
                    Path: f,
                    Priority: 1000,
                    Title: Path.GetFileNameWithoutExtension(f) ?? "JS",
                    Code: File.ReadAllText(f)))
                .ToList();
        }

        private Dictionary<string, SyntaxHighlightCodeEditor> BuildJsEditorMapByTitle()
        {
            var map = new Dictionary<string, SyntaxHighlightCodeEditor>(StringComparer.OrdinalIgnoreCase);
            foreach (var editor in _jsEditors)
            {
                if (editor == null) continue;
                if (!_jsTabMeta.TryGetValue(editor, out var meta)) continue;
                var title = (meta.Title ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;
                if (!map.ContainsKey(title))
                    map[title] = editor;
            }
            return map;
        }

        private void ImportFourPartsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import HTML/CSS/JS/PARAM hoặc Gói Zip",
                    Filter = "UI Package / Code files|*.webpkg.zip;*.zip;*.html;*.htm;*.css;*.js;*.txt;*.*|Zip Package (*.zip;*.webpkg.zip)|*.webpkg.zip;*.zip|All files|*.*",
                    Multiselect = true
                };
                if (dlg.ShowDialog(this) != true) return;

                var files = dlg.FileNames
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (files.Count == 0) return;

                if (files.Count == 1 && (files[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || files[0].EndsWith(".webpkg.zip", StringComparison.OrdinalIgnoreCase)))
                {
                    var bundle = UiNodeBundleService.ImportFromZip(files[0]);
                    if (!string.IsNullOrWhiteSpace(bundle.HtmlCode))
                    {
                        _viewModel.HtmlCode = bundle.HtmlCode;
                        if (HtmlEditor != null) HtmlEditor.Text = bundle.HtmlCode;
                    }
                    if (!string.IsNullOrWhiteSpace(bundle.CssCode))
                    {
                        _viewModel.CssCode = bundle.CssCode;
                        if (CssEditor != null) CssEditor.Text = bundle.CssCode;
                    }
                    if (!string.IsNullOrWhiteSpace(bundle.ParamsCode))
                    {
                        _viewModel.ParamsCode = bundle.ParamsCode;
                        if (ParamsEditor != null) ParamsEditor.Text = bundle.ParamsCode;
                    }
                    if (!string.IsNullOrWhiteSpace(bundle.JsCode))
                    {
                        SetJsTabsFromSingleCode(bundle.JsCode);
                        SortJsTabsByPriority();
                        SyncJsTabsToViewModel();
                    }
                    if (!string.IsNullOrWhiteSpace(bundle.Title) && string.IsNullOrWhiteSpace(_viewModel.NodeTitle))
                    {
                        _viewModel.NodeTitle = bundle.Title;
                    }

                    MessageBox.Show(this, "Đã import thành công trọn bộ UI từ gói nén .zip!", "Import thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var htmlFiles = files.Where(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)).ToList();
                var cssFiles = files.Where(f => f.EndsWith(".css", StringComparison.OrdinalIgnoreCase)).ToList();
                var jsFiles = files.Where(f => f.EndsWith(".js", StringComparison.OrdinalIgnoreCase)).ToList();

                if (htmlFiles.Count > 1 || cssFiles.Count > 1)
                {
                    MessageBox.Show(this, "Mỗi lần import chỉ được chọn 1 file HTML và 1 file CSS.\nJS có thể chọn nhiều file để tách thành các tab con.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (htmlFiles.Count == 1)
                {
                    var content = File.ReadAllText(htmlFiles[0]);
                    _viewModel.HtmlCode = content;
                    if (HtmlEditor != null) HtmlEditor.Text = content;
                }
                if (cssFiles.Count == 1)
                {
                    var content = File.ReadAllText(cssFiles[0]);
                    _viewModel.CssCode = content;
                    if (CssEditor != null) CssEditor.Text = content;
                }
                if (jsFiles.Count > 0)
                {
                    var jsParts = ReadJsImportParts(jsFiles);
                    var existingByTitle = BuildJsEditorMapByTitle();

                    for (int i = 0; i < jsParts.Count; i++)
                    {
                        var part = jsParts[i];
                        var title = (part.Title ?? string.Empty).Trim();

                        if (existingByTitle.TryGetValue(title, out var existingEditor))
                        {
                            existingEditor.Text = part.Code ?? string.Empty;
                        }
                        else
                        {
                            AddDynamicJsTab(part.Code, selectTab: false, priority: part.Priority == int.MaxValue ? 1000 + i : part.Priority, title: part.Title);
                        }
                    }
                    SortJsTabsByPriority();
                    SyncJsTabsToViewModel();
                }

                var paramFiles = files.Where(f => !f.EndsWith(".html", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".css", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".js", StringComparison.OrdinalIgnoreCase)).ToList();
                if (paramFiles.Count > 0)
                {
                    var content = File.ReadAllText(paramFiles[0]);
                    _viewModel.ParamsCode = content;
                    if (ParamsEditor != null) ParamsEditor.Text = content;
                }

                MessageBox.Show(this, "Đã import thành công các file code! (UI sẽ cập nhật khi đóng dialog)", "Import thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Import thất bại: {ex.Message}", "Lỗi Import", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportJsOverwriteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import JS (Ghi đè toàn bộ tab JS)",
                    Filter = "JavaScript files (*.js)|*.js|All files (*.*)|*.*",
                    Multiselect = true
                };
                if (dlg.ShowDialog(this) != true) return;

                var jsFiles = dlg.FileNames
                    .Where(p => !string.IsNullOrWhiteSpace(p) && p.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (jsFiles.Count == 0) return;

                var jsParts = ReadJsImportParts(jsFiles);
                SetJsTabsFromSingleCode(jsParts[0].Code, jsParts[0].Priority == int.MaxValue ? 1000 : jsParts[0].Priority, jsParts[0].Title);

                for (int i = 1; i < jsParts.Count; i++)
                {
                    AddDynamicJsTab(jsParts[i].Code, selectTab: false, priority: jsParts[i].Priority == int.MaxValue ? 1000 + i : jsParts[i].Priority, title: jsParts[i].Title);
                }

                SortJsTabsByPriority();
                SyncJsTabsToViewModel();

                MessageBox.Show(this, "Đã ghi đè thành công toàn bộ tab JS!", "Ghi đè JS", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ghi đè JS thất bại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportBundleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateAllBindings();
                var html = _viewModel.HtmlCode ?? string.Empty;
                var css = _viewModel.CssCode ?? string.Empty;
                var js = BuildCombinedJsCodeFromTabs();
                var param = _viewModel.ParamsCode ?? string.Empty;
                var title = _viewModel.NodeTitle ?? "sciter_ui_bundle";

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export UI Bundle Package",
                    Filter = "Sciter Web Package (*.webpkg.zip)|*.webpkg.zip|Zip Package (*.zip)|*.zip|All files (*.*)|*.*",
                    FileName = $"{title}.webpkg.zip"
                };

                if (dlg.ShowDialog(this) != true) return;

                UiNodeBundleService.ExportToZip(dlg.FileName, title, html, css, js, param);

                MessageBox.Show(
                    this,
                    $"Đã export thành công gói UI Bundle:\n{dlg.FileName}",
                    "Export thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Export thất bại: {ex.Message}", "Lỗi Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

