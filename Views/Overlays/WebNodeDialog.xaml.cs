using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Views.Overlays
{
    public partial class WebNodeDialog : BaseNodeDialog
    {
        private readonly WebNodeDialogViewModel _viewModel;

        public WebNodeDialog(WebNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new WebNodeDialogViewModel(node, host);
            InitializeBase(_viewModel, owner ?? Application.Current?.MainWindow);
            UpdateTitleColorPreview();

            // Subscribe to collection changes để tự động refresh UI khi collection thay đổi (kể cả khi load từ file)
            if (node is WebNode webNode)
            {
                webNode.ResponseOutputs.CollectionChanged += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"ResponseOutputs.CollectionChanged event fired - Action: {e.Action}, NewItems: {e.NewItems?.Count ?? 0}, OldItems: {e.OldItems?.Count ?? 0}");
                    try { webNode.RebuildResponseOutputs(); } catch { }
                    if (ResponseOutputsPanel != null)
                    {
                        // Sử dụng Dispatcher để đảm bảo UI được update trên UI thread
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            RefreshResponseOutputsUI();
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                };

                webNode.RequestInterceptRules.CollectionChanged += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"RequestInterceptRules.CollectionChanged event fired - Action: {e.Action}, NewItems: {e.NewItems?.Count ?? 0}, OldItems: {e.OldItems?.Count ?? 0}");
                    if (InterceptRulesPanel != null)
                    {
                        // Sử dụng Dispatcher để đảm bảo UI được update trên UI thread
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            RefreshInterceptRulesUI();
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                };
            }

            // Đảm bảo update binding khi dialog đóng (kể cả khi không click Close button)
            Closing += (s, e) =>
            {
                try
                {
                    UpdateAllBindings();
                }
                catch { }
            };
        }

        protected override Panel? GetInputsPanel() => null; // Không dùng InputsPanel nữa, dùng InputMappingsItemsControl riêng
        protected override Panel? GetOutputsPanel() => null; // Không dùng OutputsPanel nữa, dùng ResponseOutputsPanel riêng

        protected override void OnLoaded()
        {
            base.OnLoaded();
            System.Diagnostics.Debug.WriteLine($"=== WebNodeDialog.OnLoaded() called ===");
            if (_viewModel is WebNodeDialogViewModel vm)
            {
                // ⚠️ CRITICAL: KHÔNG gọi RefreshAvailableNodes() ở đây vì đã gọi trong constructor.
                //   Việc gọi lại sẽ clear AvailableNodeOptions và làm ComboBox mất ItemsSource tạm thời,
                //   khiến TwoWay binding tự động set SourceNodeId = null.
                //   Chỉ refresh output key options để đảm bảo combobox Key có đúng options.
                // ⚠️ Error 16: KHÔNG gọi Refresh* trong OnLoaded nếu nó Clear() ItemsSource – ComboBox mất ItemsSource
                // tạm thời → TwoWay binding set SelectedValue = null → corrupt data → mỗi lần mở xóa 1 item.
                // Constructor đã populate; chỉ refresh output key options cho InputMappings (pattern đã fix trước).
                foreach (var item in vm.InputMappingsList)
                {
                    vm.RefreshOutputKeyOptionsFor(item);
                }
                // JsSources: KHÔNG gọi RefreshNodeOptionsForJsSourceItem/RefreshOutputKeyOptionsForJsSource
                // vì cả hai đều Clear() → mất ItemsSource → SourceNodeId/SourceOutputKey = null.

                // Refresh UI ngay lập tức
                if (_viewModel?.Node is WebNode webNode)
                {
                    System.Diagnostics.Debug.WriteLine($"WebNode has {webNode.RequestInterceptRules?.Count ?? 0} RequestInterceptRules");
                    System.Diagnostics.Debug.WriteLine($"WebNode has {webNode.ResponseOutputs?.Count ?? 0} ResponseOutputs");
                }
                RefreshInterceptRulesUI();
                RefreshResponseOutputsUI();

                // Refresh UI lại sau một delay để đảm bảo node đã được deserialize hoàn toàn (nếu load workflow sau khi dialog đã mở)
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"Refreshing UI again in Dispatcher.BeginInvoke (after delay)");
                    if (_viewModel?.Node is WebNode webNode2)
                    {
                        System.Diagnostics.Debug.WriteLine($"WebNode now has {webNode2.RequestInterceptRules?.Count ?? 0} RequestInterceptRules");
                        System.Diagnostics.Debug.WriteLine($"WebNode now has {webNode2.ResponseOutputs?.Count ?? 0} ResponseOutputs");
                    }
                    RefreshInterceptRulesUI();
                    RefreshResponseOutputsUI();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            UpdateTitleColorPreview();
        }

        protected override void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Update tất cả binding trước khi đóng để đảm bảo dữ liệu được lưu
            UpdateAllBindings();
            ViewModel.SaveTitleCommand.Execute(null);
            Close();
        }

        /// <summary>
        /// Update tất cả binding trong dialog. Public để có thể gọi từ NodeDialogManager.
        /// </summary>
        public void UpdateAllBindings()
        {
            System.Diagnostics.Debug.WriteLine($"=== UpdateAllBindings() called ===");

            // Move focus away from any TextBox to trigger LostFocus event
            if (Keyboard.FocusedElement is TextBox focusedTextBox)
            {
                System.Diagnostics.Debug.WriteLine($"Moving focus away from TextBox");
                focusedTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }

            int responseOutputsUpdated = 0;
            // Update tất cả binding trong ResponseOutputsPanel
            if (ResponseOutputsPanel != null)
            {
                System.Diagnostics.Debug.WriteLine($"ResponseOutputsPanel has {ResponseOutputsPanel.Children.Count} children");
                foreach (var child in ResponseOutputsPanel.Children)
                {
                    if (child is Border border && border.Child is StackPanel stack)
                    {
                        UpdateBindingsInStackPanel(stack);
                        responseOutputsUpdated++;
                    }
                }
                System.Diagnostics.Debug.WriteLine($"✓ Updated {responseOutputsUpdated} ResponseOutput bindings");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"⚠ ResponseOutputsPanel is null");
            }

            int interceptRulesUpdated = 0;
            // Update tất cả binding trong InterceptRulesPanel
            if (InterceptRulesPanel != null)
            {
                System.Diagnostics.Debug.WriteLine($"InterceptRulesPanel has {InterceptRulesPanel.Children.Count} children");
                foreach (var child in InterceptRulesPanel.Children)
                {
                    if (child is Border border && border.Child is StackPanel stack)
                    {
                        UpdateBindingsInStackPanel(stack);
                        interceptRulesUpdated++;
                    }
                }
                System.Diagnostics.Debug.WriteLine($"✓ Updated {interceptRulesUpdated} InterceptRule bindings");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"⚠ InterceptRulesPanel is null");
            }

            System.Diagnostics.Debug.WriteLine($"=== UpdateAllBindings() completed ===");
        }

        /// <summary>
        /// Refresh UI từ model data. Public để có thể gọi từ NodeDialogManager sau khi load workflow.
        /// </summary>
        public void RefreshUI()
        {
            System.Diagnostics.Debug.WriteLine($"=== RefreshUI() called ===");

            // Refresh UI trên UI thread
            Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Diagnostics.Debug.WriteLine($"Refreshing UI in Dispatcher.BeginInvoke");
                if (_viewModel is WebNodeDialogViewModel vm)
                {
                    vm.RefreshAvailableNodes();
                }
                RefreshInterceptRulesUI();
                RefreshResponseOutputsUI();
                System.Diagnostics.Debug.WriteLine($"✓ RefreshUI() completed");
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void UpdateBindingsInStackPanel(StackPanel stack)
        {
            if (stack == null) return;

            foreach (var child in stack.Children)
            {
                if (child is TextBox textBox)
                {
                    textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                }
                else if (child is ComboBox comboBox)
                {
                    comboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                }
                else if (child is CheckBox checkBox)
                {
                    checkBox.GetBindingExpression(CheckBox.IsCheckedProperty)?.UpdateSource();
                }
                else if (child is StackPanel nestedStack)
                {
                    UpdateBindingsInStackPanel(nestedStack);
                }
                else if (child is Grid grid)
                {
                    UpdateBindingsInGrid(grid);
                }
            }
        }

        private void UpdateBindingsInGrid(Grid grid)
        {
            if (grid == null) return;

            foreach (var gridChild in grid.Children)
            {
                if (gridChild is TextBox tb)
                {
                    tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                }
                else if (gridChild is ComboBox cb)
                {
                    cb.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                }
                else if (gridChild is CheckBox checkBox)
                {
                    checkBox.GetBindingExpression(CheckBox.IsCheckedProperty)?.UpdateSource();
                }
                else if (gridChild is StackPanel nestedStack)
                {
                    UpdateBindingsInStackPanel(nestedStack);
                }
                else if (gridChild is Grid nestedGrid)
                {
                    UpdateBindingsInGrid(nestedGrid);
                }
            }
        }

        private void TitleColorComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateTitleColorPreview();
        }

        private void UpdateTitleColorPreview()
        {
            if (TitleColorPreview == null || TitleColorComboBox?.SelectedValue == null) return;
            var colorKey = TitleColorComboBox.SelectedValue.ToString();
            Brush? brush = null;
            if (string.IsNullOrEmpty(colorKey) || colorKey == "NodeColor")
            {
                if (_viewModel?.Node != null) brush = _viewModel.Node.NodeBrush;
            }
            else if (colorKey == "LimeGreen")
            {
                brush = new SolidColorBrush(Colors.LimeGreen);
            }
            else
            {
                brush = Application.Current.TryFindResource(colorKey) as Brush;
            }
            TitleColorPreview.Background = brush ?? new SolidColorBrush(Colors.Gray);
        }

        private void AddInterceptRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel?.Node is not WebNode webNode) return;
            webNode.RequestInterceptRules.Add(new WebRequestInterceptRule());
            RefreshInterceptRulesUI();
        }

        private void RefreshInterceptRulesUI()
        {
            System.Diagnostics.Debug.WriteLine($"=== RefreshInterceptRulesUI() called ===");
            if (InterceptRulesPanel == null)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ InterceptRulesPanel is null");
                return;
            }

            InterceptRulesPanel.Children.Clear();
            if (_viewModel?.Node is not WebNode webNode)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ ViewModel.Node is not WebNode");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"WebNode has {webNode.RequestInterceptRules?.Count ?? 0} RequestInterceptRules");
            int added = 0;
            if (webNode.RequestInterceptRules == null) return;
            foreach (var rule in webNode.RequestInterceptRules)
            {
                try
                {
                    if (rule == null) continue;
                    System.Diagnostics.Debug.WriteLine($"Creating UI for RequestInterceptRule: MatchUrl='{rule.MatchUrlPattern}', ReplaceUrl='{rule.ReplaceUrlValue}'");
                    var border = CreateRulePanel(rule, webNode);
                    if (InterceptRulesPanel != null)
                        InterceptRulesPanel.Children.Add(border);
                    added++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating RequestInterceptRule UI: {ex.Message}");
                }
            }
            System.Diagnostics.Debug.WriteLine($"✓ Added {added} RequestInterceptRule panels to UI");
        }

        private Border CreateRulePanel(WebRequestInterceptRule rule, WebNode webNode)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

            var matchLabel = new TextBlock { Text = "Match URL pattern:", FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
            BindThemeResource(matchLabel, TextBlock.ForegroundProperty, "TextBrush");
            var matchTb = new TextBox
            {
                Height = 32,
                Margin = new Thickness(0, 0, 0, 8),
                Style = Application.Current.TryFindResource("BaseTextBoxV2") as Style
            };
            matchTb.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(WebRequestInterceptRule.MatchUrlPattern)) { Source = rule, Mode = System.Windows.Data.BindingMode.TwoWay });
            stack.Children.Add(matchLabel);
            stack.Children.Add(matchTb);

            // Lưu reference đến các phần Replace Params và Replace Body để có thể ẩn/hiện
            var replaceParamsContainer = AddReplaceRow(stack, rule, "Replace Params", nameof(WebRequestInterceptRule.ReplaceParamsSourceNodeId), nameof(WebRequestInterceptRule.ReplaceParamsSourceOutputKey), nameof(WebRequestInterceptRule.ReplaceParamsValue));
            var replaceBodyContainer = AddReplaceRow(stack, rule, "Replace Body", nameof(WebRequestInterceptRule.ReplaceBodySourceNodeId), nameof(WebRequestInterceptRule.ReplaceBodySourceOutputKey), nameof(WebRequestInterceptRule.ReplaceBodyValue));

            // Gọi AddReplaceUrlRow sau để có thể truyền reference vào
            AddReplaceUrlRow(stack, rule, replaceParamsContainer, replaceBodyContainer);

            var removeBtn = new Button
            {
                Content = "Xóa rule",
                Height = 28,
                Margin = new Thickness(0, 4, 0, 0),
                Style = Application.Current.TryFindResource("DangerButton") as Style,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            removeBtn.Click += (s, e) =>
            {
                webNode.RequestInterceptRules.Remove(rule);
                RefreshInterceptRulesUI();
            };
            stack.Children.Add(removeBtn);

            var border = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Child = stack
            };
            BindThemeResource(border, Border.BackgroundProperty, "CardHoverBackground");
            BindThemeResource(border, Border.BorderBrushProperty, "ControlBorderBrush");
            return border;
        }

        private UIElement AddReplaceRow(StackPanel stack, WebRequestInterceptRule rule, string label, string nodeProp, string keyProp, string valueProp)
        {
            // Tạo container để có thể control visibility
            var container = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };

            var rowLabel = new TextBlock { Text = label + " (Node + Key hoặc giá trị tĩnh):", FontSize = 11, Margin = new Thickness(0, 4, 0, 4) };
            BindThemeResource(rowLabel, TextBlock.ForegroundProperty, "TextBrush");
            container.Children.Add(rowLabel);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var nodeCombo = new ComboBox
            {
                Height = 32,
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                ItemsSource = _viewModel.AvailableNodeOptions,
                SelectedValuePath = nameof(WorkflowDataSourceOption.NodeId),
                DisplayMemberPath = nameof(WorkflowDataSourceOption.Title)
            };
            nodeCombo.SetBinding(ComboBox.SelectedValueProperty, new System.Windows.Data.Binding(nodeProp) { Source = rule, Mode = System.Windows.Data.BindingMode.TwoWay });
            Grid.SetColumn(nodeCombo, 0);
            grid.Children.Add(nodeCombo);

            var keyCombo = new ComboBox
            {
                Height = 32,
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                SelectedValuePath = nameof(WorkflowOutputKeyOption.Key),
                DisplayMemberPath = nameof(WorkflowOutputKeyOption.Key)
            };
            keyCombo.SetBinding(ComboBox.SelectedValueProperty, new System.Windows.Data.Binding(keyProp) { Source = rule, Mode = System.Windows.Data.BindingMode.TwoWay });
            Grid.SetColumn(keyCombo, 2);
            grid.Children.Add(keyCombo);

            void RefreshKeyCombo()
            {
                var nodeId = rule.GetType().GetProperty(nodeProp)?.GetValue(rule) as string;
                var keys = _viewModel.GetOutputKeysForNode(nodeId);
                keyCombo.ItemsSource = keys;
            }

            rule.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nodeProp) RefreshKeyCombo();
            };
            RefreshKeyCombo();

            container.Children.Add(grid);
            stack.Children.Add(container);
            return container;
        }

        private void AddReplaceUrlRow(StackPanel stack, WebRequestInterceptRule rule, UIElement? replaceParamsContainer, UIElement? replaceBodyContainer)
        {
            var rowLabel = new TextBlock { Text = "Replace URL:", FontSize = 11, Margin = new Thickness(0, 4, 0, 4) };
            BindThemeResource(rowLabel, TextBlock.ForegroundProperty, "TextBrush");
            stack.Children.Add(rowLabel);

            // Checkbox để chọn dùng node+key (cURL) hay giá trị tĩnh
            var useNodeKeyCheckbox = new CheckBox
            {
                Content = "Dùng Node + Key (cURL) để thay URL",
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            BindThemeResource(useNodeKeyCheckbox, Control.ForegroundProperty, "TextBrush");
            useNodeKeyCheckbox.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding(nameof(WebRequestInterceptRule.ReplaceUrlWithNodeKey))
            {
                Source = rule,
                Mode = System.Windows.Data.BindingMode.TwoWay
            });

            // Khi checkbox thay đổi, ẩn/hiện Replace Params và Replace Body
            void UpdateReplaceSectionsVisibility()
            {
                var useNodeKey = rule.ReplaceUrlWithNodeKey;
                if (replaceParamsContainer != null)
                {
                    replaceParamsContainer.Visibility = useNodeKey ? Visibility.Collapsed : Visibility.Visible;
                }
                if (replaceBodyContainer != null)
                {
                    replaceBodyContainer.Visibility = useNodeKey ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            rule.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WebRequestInterceptRule.ReplaceUrlWithNodeKey))
                {
                    UpdateReplaceSectionsVisibility();
                }
            };

            // Cập nhật visibility ban đầu
            UpdateReplaceSectionsVisibility();

            stack.Children.Add(useNodeKeyCheckbox);

            // Panel chứa node+key combo hoặc textbox giá trị tĩnh
            var contentPanel = new StackPanel();

            // Node + Key combo (hiển thị khi checkbox checked)
            var nodeKeyGrid = new Grid();
            nodeKeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nodeKeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            nodeKeyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var nodeCombo = new ComboBox
            {
                Height = 32,
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                ItemsSource = _viewModel.AvailableNodeOptions,
                SelectedValuePath = nameof(WorkflowDataSourceOption.NodeId),
                DisplayMemberPath = nameof(WorkflowDataSourceOption.Title)
            };
            nodeCombo.SetBinding(ComboBox.SelectedValueProperty, new System.Windows.Data.Binding(nameof(WebRequestInterceptRule.ReplaceUrlSourceNodeId))
            {
                Source = rule,
                Mode = System.Windows.Data.BindingMode.TwoWay
            });
            Grid.SetColumn(nodeCombo, 0);
            nodeKeyGrid.Children.Add(nodeCombo);

            var keyCombo = new ComboBox
            {
                Height = 32,
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                SelectedValuePath = nameof(WorkflowOutputKeyOption.Key),
                DisplayMemberPath = nameof(WorkflowOutputKeyOption.Key)
            };
            keyCombo.SetBinding(ComboBox.SelectedValueProperty, new System.Windows.Data.Binding(nameof(WebRequestInterceptRule.ReplaceUrlSourceOutputKey))
            {
                Source = rule,
                Mode = System.Windows.Data.BindingMode.TwoWay
            });
            Grid.SetColumn(keyCombo, 2);
            nodeKeyGrid.Children.Add(keyCombo);

            void RefreshKeyCombo()
            {
                var nodeId = rule.ReplaceUrlSourceNodeId;
                var keys = _viewModel.GetOutputKeysForNode(nodeId);
                keyCombo.ItemsSource = keys;
            }

            rule.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WebRequestInterceptRule.ReplaceUrlSourceNodeId)) RefreshKeyCombo();
            };
            RefreshKeyCombo();

            // Textbox giá trị tĩnh (hiển thị khi checkbox unchecked)
            var valueTextBox = new TextBox
            {
                Height = 32,
                Style = Application.Current.TryFindResource("BaseTextBoxV2") as Style
            };
            valueTextBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(WebRequestInterceptRule.ReplaceUrlValue))
            {
                Source = rule,
                Mode = System.Windows.Data.BindingMode.TwoWay
            });

            // Update visibility dựa trên checkbox
            void UpdateVisibility()
            {
                var useNodeKey = rule.ReplaceUrlWithNodeKey;
                nodeKeyGrid.Visibility = useNodeKey ? Visibility.Visible : Visibility.Collapsed;
                valueTextBox.Visibility = useNodeKey ? Visibility.Collapsed : Visibility.Visible;
            }

            rule.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WebRequestInterceptRule.ReplaceUrlWithNodeKey)) UpdateVisibility();
            };
            UpdateVisibility();

            contentPanel.Children.Add(nodeKeyGrid);
            contentPanel.Children.Add(valueTextBox);
            stack.Children.Add(contentPanel);
        }

        private void AddResponseOutputButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel?.Node is not WebNode webNode) return;
            webNode.ResponseOutputs.Add(new WebResponseOutput { Key = "output", Url = "https://example.com", RequestMethod = "All", ExtractType = "Response" });
            RefreshResponseOutputsUI();
        }

        private void RefreshResponseOutputsUI()
        {
            System.Diagnostics.Debug.WriteLine($"=== RefreshResponseOutputsUI() called ===");
            if (ResponseOutputsPanel == null)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ ResponseOutputsPanel is null");
                return;
            }

            ResponseOutputsPanel.Children.Clear();
            if (_viewModel?.Node is not WebNode webNode)
            {
                System.Diagnostics.Debug.WriteLine($"⚠ ViewModel.Node is not WebNode");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"WebNode has {webNode.ResponseOutputs?.Count ?? 0} ResponseOutputs");
            int added = 0;
            if (webNode.ResponseOutputs == null) return;
            foreach (var output in webNode.ResponseOutputs)
            {
                try
                {
                    if (output == null) continue;
                    System.Diagnostics.Debug.WriteLine($"Creating UI for ResponseOutput: Key='{output.Key}', Url='{output.Url}', Method='{output.RequestMethod}'");
                    var border = CreateResponseOutputPanel(output, webNode);
                    if (ResponseOutputsPanel != null)
                        ResponseOutputsPanel.Children.Add(border);
                    added++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating ResponseOutput UI: {ex.Message}");
                }
            }
            System.Diagnostics.Debug.WriteLine($"✓ Added {added} ResponseOutput panels to UI");
        }

        private Border CreateResponseOutputPanel(WebResponseOutput output, WebNode webNode)
        {
            // StackPanel bên trong border, không có margin để border control khoảng cách giữa các output
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };

            // Khi user đổi Key/Url/Method, đảm bảo DynamicOutputs được rebuild theo Key mới
            output.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WebResponseOutput.Key))
                {
                    try { webNode.RebuildResponseOutputs(); } catch { }
                }
            };

            // Key và Remove Button cùng một dòng
            var keyLabel = new TextBlock { Text = "Key:", FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
            BindThemeResource(keyLabel, TextBlock.ForegroundProperty, "TextBrush");
            stack.Children.Add(keyLabel);

            var keyRowGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            keyRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            keyRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            keyRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var keyTb = new TextBox
            {
                Height = 32,
                Style = Application.Current.TryFindResource("BaseTextBoxV2") as Style
            };
            keyTb.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(WebResponseOutput.Key))
            {
                Source = output,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });
            Grid.SetColumn(keyTb, 0);
            keyRowGrid.Children.Add(keyTb);

            // Remove button
            var removeBtn = new Button
            {
                Content = "-",
                Height = 32,
                Width = 32,
                ToolTip = "Xoá output",
                Style = Application.Current.TryFindResource("DangerButton") as Style,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            removeBtn.Click += (s, e) =>
            {
                webNode.ResponseOutputs.Remove(output);
                RefreshResponseOutputsUI();
            };
            Grid.SetColumn(removeBtn, 2);
            keyRowGrid.Children.Add(removeBtn);

            stack.Children.Add(keyRowGrid);

            // URL
            var urlLabel = new TextBlock { Text = "URL:", FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
            BindThemeResource(urlLabel, TextBlock.ForegroundProperty, "TextBrush");
            var urlTb = new TextBox
            {
                Height = 32,
                Margin = new Thickness(0, 0, 0, 8),
                Style = Application.Current.TryFindResource("BaseTextBoxV2") as Style
            };
            urlTb.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(WebResponseOutput.Url))
            {
                Source = output,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });
            stack.Children.Add(urlLabel);
            stack.Children.Add(urlTb);

            // Request Method và Extract Type - 2 ComboBox cùng một dòng
            var comboGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            comboGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            comboGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            comboGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Request Method - ComboBox (cột đầu tiên)
            var methodContainer = new StackPanel();
            var methodLabel = new TextBlock { Text = "Request Method:", FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
            BindThemeResource(methodLabel, TextBlock.ForegroundProperty, "TextBrush");
            var methodCombo = new ComboBox
            {
                Height = 36,
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                ItemsSource = new[] { "All", "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" }
            };
            methodCombo.SetBinding(ComboBox.SelectedItemProperty, new System.Windows.Data.Binding(nameof(WebResponseOutput.RequestMethod))
            {
                Source = output,
                Mode = System.Windows.Data.BindingMode.TwoWay
            });
            methodContainer.Children.Add(methodLabel);
            methodContainer.Children.Add(methodCombo);
            Grid.SetColumn(methodContainer, 0);
            comboGrid.Children.Add(methodContainer);

            // Extract Type - ComboBox (cột thứ hai)
            var extractContainer = new StackPanel();
            var extractLabel = new TextBlock { Text = "Lấy dữ liệu:", FontSize = 11, Margin = new Thickness(0, 0, 0, 4) };
            BindThemeResource(extractLabel, TextBlock.ForegroundProperty, "TextBrush");
            var extractCombo = new ComboBox
            {
                Height = 36,
                Style = Application.Current.TryFindResource("BaseComboBox") as Style,
                ItemsSource = new[]
                {
                    new { Value = "Response", Display = "Response (body)" },
                    new { Value = "Headers", Display = "Headers (response headers)" },
                    new { Value = "Params", Display = "Params (query string)" },
                    new { Value = "Payload", Display = "Payload (request body)" },
                    new { Value = "RequestHeaders", Display = "Request Headers" },
                    new { Value = "CurlCmd", Display = "Copy as cURL (cmd)" },
                    new { Value = "CurlBash", Display = "Copy as cURL (bash/Postman)" }
                },
                SelectedValuePath = "Value",
                DisplayMemberPath = "Display"
            };
            extractCombo.SetBinding(ComboBox.SelectedValueProperty, new System.Windows.Data.Binding(nameof(WebResponseOutput.ExtractType))
            {
                Source = output,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
                FallbackValue = "Response",
                TargetNullValue = "Response"
            });
            // Đảm bảo ExtractType có giá trị hợp lệ khi chưa chọn
            var validTypes = new[] { "Response", "Headers", "Params", "Payload", "RequestHeaders", "CurlCmd", "CurlBash" };
            var et = output.ExtractType?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(et) || !validTypes.Any(v => string.Equals(v, et, StringComparison.OrdinalIgnoreCase)))
            {
                output.ExtractType = "Response";
            }
            extractContainer.Children.Add(extractLabel);
            extractContainer.Children.Add(extractCombo);
            Grid.SetColumn(extractContainer, 2);
            comboGrid.Children.Add(extractContainer);

            stack.Children.Add(comboGrid);

            // Checkbox: có đợi key này trước khi chạy node tiếp theo hay không
            var waitCheckBox = new CheckBox
            {
                Content = "Đợi key này trước khi chạy node tiếp theo",
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            BindThemeResource(waitCheckBox, Control.ForegroundProperty, "TextBrush");
            waitCheckBox.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding(nameof(WebResponseOutput.WaitForCompletion))
            {
                Source = output,
                Mode = System.Windows.Data.BindingMode.TwoWay
            });
            stack.Children.Add(waitCheckBox);

            // Border bao ngoài mỗi output, có margin bottom để tạo khoảng cách với output kế tiếp
            var border = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8),
                Child = stack
            };
            BindThemeResource(border, Border.BackgroundProperty, "CardHoverBackground");
            BindThemeResource(border, Border.BorderBrushProperty, "ControlBorderBrush");
            return border;
        }
    }
}
