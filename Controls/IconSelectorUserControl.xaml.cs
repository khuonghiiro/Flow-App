using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using SharpVectors.Runtime;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlowMy.Controls
{
    public partial class IconSelectorUserControl : UserControl, INotifyPropertyChanged
    {
        // Ưu tiên manifest động (available_icons.txt) tạo bởi ExtractIcons.ps1, fallback
        // AvailableIcons dictionary tĩnh. Dùng qua IconResources.EffectiveIcons.
        private IReadOnlyDictionary<string, string> availableIcons => IconResources.EffectiveIcons;
        private string selectedIcon;
        private Dictionary<string, string> filteredIcons;

        // Pagination properties
        private int currentPage = 0;
        private int itemsPerPage = 56; // 8 columns x 7 rows

        // Dependency Property cho icon Ä‘Ã£ chá»n
        public static readonly DependencyProperty SelectedIconProperty =
            DependencyProperty.Register("SelectedIcon", typeof(string), typeof(IconSelectorUserControl),
                new FrameworkPropertyMetadata(string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedIconChanged));

        public string SelectedIcon
        {
            get { return (string)GetValue(SelectedIconProperty); }
            set { SetValue(SelectedIconProperty, value); }
        }

        // Dependency Property cho chiá»u cao button
        public static readonly DependencyProperty ButtonHeightProperty =
            DependencyProperty.Register("ButtonHeight", typeof(double), typeof(IconSelectorUserControl),
                new PropertyMetadata(44.0));

        public double ButtonHeight
        {
            get { return (double)GetValue(ButtonHeightProperty); }
            set { SetValue(ButtonHeightProperty, value); }
        }

        // Event callback khi SelectedIcon thay Ä‘á»•i
        private static void OnSelectedIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is IconSelectorUserControl control)
            {
                control.selectedIcon = e.NewValue as string;
                control.UpdateClearButtonVisibility();
                control.UpdateIconButtonStyles();
                control.UpdateIconDisplay();
                control.OnPropertyChanged("SelectedIcon");
            }
        }

        public IconSelectorUserControl()
        {
            InitializeComponent();
            selectedIcon = string.Empty;
            filteredIcons = availableIcons.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            Loaded += (s, e) =>
            {
                UpdateClearButtonVisibility();
                InitializeIconGrid(); // Load trang Ä‘áº§u tiÃªn
            };
        }

        private void InitializeIconGrid()
        {
            // TÃ­nh toÃ¡n pagination
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)filteredIcons.Count / itemsPerPage));

            // Äáº£m báº£o currentPage há»£p lá»‡
            if (currentPage >= totalPages)
                currentPage = Math.Max(0, totalPages - 1);

            // Láº¥y icons cho trang hiá»‡n táº¡i
            var pagedIcons = filteredIcons
                .Skip(currentPage * itemsPerPage)
                .Take(itemsPerPage);

            IconGrid.Children.Clear();

            foreach (var iconPair in pagedIcons)
            {
                Button iconButton = new Button
                {
                    Tag = iconPair.Key,
                    ToolTip = iconPair.Key,
                    Width = 40,
                    Height = 40,
                    Padding = new Thickness(5)
                };

                // Dùng SvgViewboxEx; Fill fallback về TextBrush nếu theme không có TextOnPrimaryBrush.
                var svgViewbox = new SvgViewboxEx
                {
                    Source = new Uri(iconPair.Value, UriKind.RelativeOrAbsolute),
                    Width = 18,
                    Height = 18
                };

                iconButton.Content = svgViewbox;
                iconButton.Style = TryFindResource("ModernIconButtonStyle") as Style;
                iconButton.Click += IconButton_Click;

                IconGrid.Children.Add(iconButton);
            }

            UpdateIconButtonStyles();
            UpdatePaginationUI();
        }

        private void UpdatePaginationUI()
        {
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)filteredIcons.Count / itemsPerPage));

            // Update page info
            PageInfoText.Text = $"Trang {currentPage + 1} / {totalPages}";
            TotalIconsText.Text = $"({filteredIcons.Count} icons)";

            // Enable/Disable buttons
            PrevPageButton.IsEnabled = currentPage > 0;
            NextPageButton.IsEnabled = currentPage < totalPages - 1;
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 0)
            {
                currentPage--;
                InitializeIconGrid();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling((double)filteredIcons.Count / itemsPerPage);
            if (currentPage < totalPages - 1)
            {
                currentPage++;
                InitializeIconGrid();
            }
        }

        // XÃ“A METHOD NÃ€Y
        // private void IconDisplayBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }

        // XÃ“A METHOD NÃ€Y
        // private void IconDisplayBorder_MouseEnter(object sender, MouseEventArgs e) { }

        // XÃ“A METHOD NÃ€Y
        // private void IconDisplayBorder_MouseLeave(object sender, MouseEventArgs e) { }

        private void IconButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string iconName)
            {
                SelectedIcon = iconName;

                var binding = BindingOperations.GetBindingExpression(this, SelectedIconProperty);
                binding?.UpdateSource();

                IconPopup.IsOpen = false;
            }
        }

        private void UpdateIconButtonStyles()
        {
            var normalStyle   = TryFindResource("ModernIconButtonStyle")  as Style;
            var selectedStyle = TryFindResource("SelectedIconButtonStyle") as Style;

            foreach (Button button in IconGrid.Children.OfType<Button>())
            {
                button.Style = button.Tag?.ToString() == SelectedIcon
                    ? selectedStyle
                    : normalStyle;
            }
        }

        private void UpdateClearButtonVisibility()
        {
            if (ClearButton != null)
            {
                // LUÃ”N HIá»†N khi cÃ³ icon, khÃ´ng cáº§n hover
                ClearButton.Visibility = !string.IsNullOrEmpty(SelectedIcon)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void SelectIcon_Click(object sender, RoutedEventArgs e)
        {
            ResetSearch();
            IconPopup.IsOpen = true;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedIcon = string.Empty;

            var binding = BindingOperations.GetBindingExpression(this, SelectedIconProperty);
            binding?.UpdateSource();

            // KhÃ´ng cáº§n set Visibility á»Ÿ Ä‘Ã¢y vÃ¬ UpdateClearButtonVisibility sáº½ xá»­ lÃ½
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox searchBox)
            {
                string searchText = searchBox.Text?.Trim().ToLower() ?? "";

                if (string.IsNullOrEmpty(searchText))
                {
                    filteredIcons = availableIcons.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
                else
                {
                    filteredIcons = availableIcons
                        .Where(kvp => kvp.Key.ToLower().Contains(searchText))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }

                // Reset vá» trang Ä‘áº§u khi search
                currentPage = 0;
                InitializeIconGrid();
            }
        }

        private void ResetSearch()
        {
            if (SearchIconKeywordBox != null)
            {
                SearchIconKeywordBox.Text = "";
                filteredIcons = availableIcons.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                currentPage = 0;
                InitializeIconGrid();
            }
        }

        private void UpdateIconDisplay()
        {
            if (PlaceholderText == null || SelectedIconPanel == null || IconDisplaySvg == null)
                return;

            if (!string.IsNullOrEmpty(SelectedIcon))
            {
                string iconPath = IconResources.GetIconPath(SelectedIcon);
                if (!string.IsNullOrEmpty(iconPath))
                {
                    IconDisplaySvg.Source = new Uri(iconPath, UriKind.RelativeOrAbsolute);
                }
                else
                {
                    IconDisplaySvg.Source = null;
                }

                if (SelectedIconNameText != null)
                    SelectedIconNameText.Text = SelectedIcon;

                SelectedIconPanel.Visibility = Visibility.Visible;
                PlaceholderText.Visibility = Visibility.Collapsed;
            }
            else
            {
                IconDisplaySvg.Source = null;
                SelectedIconPanel.Visibility = Visibility.Collapsed;
                PlaceholderText.Visibility = Visibility.Visible;
            }
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Public methods
        public string GetSelectedIcon() => SelectedIcon;

        public void SetSelectedIcon(string icon) => SelectedIcon = icon ?? string.Empty;

        public void ClearIcon()
        {
            SelectedIcon = string.Empty;
            var binding = BindingOperations.GetBindingExpression(this, SelectedIconProperty);
            binding?.UpdateSource();
        }

        public string GetIconName(string iconName)
        {
            return availableIcons.ContainsKey(iconName) ? availableIcons[iconName] : "Unknown";
        }

    }


    /// <summary>
    /// SVG icon control hỗ trợ Fill color và tự inherit Foreground.
    /// Dùng FileSvgReader để load trực tiếp → apply màu trước freeze → đảm bảo màu đúng.
    /// </summary>
    public class SvgViewboxEx : Viewbox
    {
        private readonly System.Windows.Controls.Image _image;

        // ── Source ─────────────────────────────────────────────────────────
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(object), typeof(SvgViewboxEx),
                new PropertyMetadata(null, OnSourceOrFillChanged));

        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        // ── Fill ────────────────────────────────────────────────────────────
        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(SvgViewboxEx),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnSourceOrFillChanged));

        public Brush Fill
        {
            get => (Brush)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        // ── Stroke (API compat) ─────────────────────────────────────────────
        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(SvgViewboxEx),
                new PropertyMetadata(null));

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        private static void OnSourceOrFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SvgViewboxEx ctrl) ctrl.ReloadIcon();
        }

        // ── Constructor ─────────────────────────────────────────────────────
        public SvgViewboxEx()
        {
            Stretch = Stretch.Uniform;
            _image = new System.Windows.Controls.Image { Stretch = Stretch.Uniform };
            Child = _image;
        }

        // ── Auto-inherit Foreground khi Fill chưa set ───────────────────────
        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);

            var local = ReadLocalValue(FillProperty);
            bool notSet = local == DependencyProperty.UnsetValue
                       || (local == null && BindingOperations.GetBinding(this, FillProperty) == null);

            if (notSet)
            {
                DependencyObject p = VisualTreeHelper.GetParent(this);
                while (p != null)
                {
                    if (p is Control || p is TextBlock)
                    {
                        BindingOperations.SetBinding(this, FillProperty,
                            new Binding("Foreground") { Source = p, Mode = BindingMode.OneWay });
                        break;
                    }
                    p = VisualTreeHelper.GetParent(p);
                }
            }
        }

        // ── Core: load SVG → apply color → set ImageSource ─────────────────
        private void ReloadIcon()
        {
            var src = Source;
            if (src == null) { _image.Source = null; return; }

            string srcStr = src is Uri u ? u.OriginalString : src.ToString();
            if (string.IsNullOrEmpty(srcStr)) { _image.Source = null; return; }

            try
            {
                // Resolve to absolute path (same pattern as IconResources.GetSvgImage)
                string path = srcStr;
                if (!System.IO.Path.IsPathRooted(path))
                {
                    string norm = path.TrimStart('/', '\\')
                                     .Replace('/', System.IO.Path.DirectorySeparatorChar)
                                     .Replace('\\', System.IO.Path.DirectorySeparatorChar);
                    string abs = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, norm);
                    if (System.IO.File.Exists(abs)) path = abs;
                }

                if (!System.IO.File.Exists(path)) { _image.Source = null; return; }

                var settings = new WpfDrawingSettings
                {
                    IncludeRuntime = true,
                    TextAsGeometry = false,
                    OptimizePath = true
                };
                // Use Read(string) — URI overload causes NullRef inside SharpVectors
                var reader = new FileSvgReader(settings);
                DrawingGroup drawing = reader.Read(path);
                if (drawing == null) { _image.Source = null; return; }

                // Apply fill BEFORE any freeze
                var fill = GetEffectiveFill();
                if (fill != null) ApplyFillToDrawing(drawing, fill);

                _image.Source = new DrawingImage(drawing);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SvgViewboxEx] {ex.Message}");
                _image.Source = null;
            }
        }

        private static void ApplyFillToDrawing(Drawing drawing, Brush fill)
        {
            if (drawing is GeometryDrawing gd)
            {
                // Chỉ thay nếu brush hiện tại không phải transparent/none
                if (gd.Brush != null) gd.Brush = fill;
            }
            else if (drawing is DrawingGroup dg)
            {
                foreach (Drawing child in dg.Children)
                    ApplyFillToDrawing(child, fill);
            }
        }

        private Brush GetEffectiveFill()
        {
            if (Fill != null) return Fill;

            DependencyObject p = VisualTreeHelper.GetParent(this);
            while (p != null)
            {
                if (p is Control c && c.Foreground != null) return c.Foreground;
                if (p is TextBlock t && t.Foreground != null) return t.Foreground;
                p = VisualTreeHelper.GetParent(p);
            }

            return Application.Current?.Resources?["TextBrush"] as Brush ?? Brushes.Black;
        }
    }
}


