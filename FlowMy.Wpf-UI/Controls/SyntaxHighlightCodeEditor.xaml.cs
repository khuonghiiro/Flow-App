using FlowMy.Helpers;
using ICSharpCode.AvalonEdit.Highlighting;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowMy.Controls
{
    /// <summary>
    /// Code editor dựa trên AvalonEdit với Monokai theme đẹp mắt.
    /// Hỗ trợ C#, JavaScript, HTML, CSS với syntax highlighting tự động.
    /// </summary>
    public partial class SyntaxHighlightCodeEditor : UserControl
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(SyntaxHighlightCodeEditor),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTextPropertyChanged));

        public static readonly DependencyProperty CodeFontSizeProperty = DependencyProperty.Register(
            nameof(CodeFontSize), typeof(double), typeof(SyntaxHighlightCodeEditor),
            new PropertyMetadata(14.0, OnCodeFontSizeChanged));

        public static readonly DependencyProperty IsAutoHighlightEnabledProperty = DependencyProperty.Register(
            nameof(IsAutoHighlightEnabled), typeof(bool), typeof(SyntaxHighlightCodeEditor),
            new PropertyMetadata(true));

        /// <summary>
        /// Loại syntax để highlight: "CSharp", "JavaScript", "HTML", "CSS", hoặc null để tự detect.
        /// </summary>
        public static readonly DependencyProperty SyntaxLanguageProperty = DependencyProperty.Register(
            nameof(SyntaxLanguage), typeof(string), typeof(SyntaxHighlightCodeEditor),
            new PropertyMetadata("JavaScript", OnSyntaxLanguageChanged));

        /// <summary>
        /// Hiển thị line numbers
        /// </summary>
        public static readonly DependencyProperty ShowLineNumbersProperty = DependencyProperty.Register(
            nameof(ShowLineNumbers), typeof(bool), typeof(SyntaxHighlightCodeEditor),
            new PropertyMetadata(true, OnShowLineNumbersChanged));

        private bool _isUpdatingFromEditor;

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public double CodeFontSize
        {
            get => (double)GetValue(CodeFontSizeProperty);
            set => SetValue(CodeFontSizeProperty, value);
        }

        public bool IsAutoHighlightEnabled
        {
            get => (bool)GetValue(IsAutoHighlightEnabledProperty);
            set => SetValue(IsAutoHighlightEnabledProperty, value);
        }

        public string SyntaxLanguage
        {
            get => (string)GetValue(SyntaxLanguageProperty);
            set => SetValue(SyntaxLanguageProperty, value);
        }

        public bool ShowLineNumbers
        {
            get => (bool)GetValue(ShowLineNumbersProperty);
            set => SetValue(ShowLineNumbersProperty, value);
        }

        public SyntaxHighlightCodeEditor()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        /// <summary>
        /// Refresh highlighting - đảm bảo text khớp với binding
        /// </summary>
        public void RefreshHighlight()
        {
            if (Editor == null) return;
            if (Editor.Text != Text)
            {
                _isUpdatingFromEditor = true;
                try
                {
                    Editor.Text = Text ?? string.Empty;
                }
                finally
                {
                    _isUpdatingFromEditor = false;
                }
            }
        }

        /// <summary>
        /// Force update binding source từ Editor.Text về Text property và trigger binding update
        /// Được dùng trong UpdateAllBindings() để đảm bảo binding được update trước khi đóng dialog
        /// (ngay cả khi UpdateSourceTrigger=LostFocus và user đang focus vào editor)
        /// </summary>
        public void ForceUpdateBinding()
        {
            if (Editor == null) return;
            
            // Đảm bảo Text property được sync từ Editor.Text
            var editorText = Editor.Text ?? string.Empty;
            if (Text != editorText)
            {
                SetCurrentValue(TextProperty, editorText);
            }
            
            // Force update binding source (ngay cả khi UpdateSourceTrigger=LostFocus)
            var bindingExpression = GetBindingExpression(TextProperty);
            bindingExpression?.UpdateSource();
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (Editor == null) return;

            // Cấu hình giao diện Monokai
            ConfigureMonokaiAppearance();

            // Áp dụng syntax highlighting
            ApplySyntaxHighlighting();

            // Set text ban đầu
            Editor.Text = Text ?? string.Empty;
            Editor.FontSize = CodeFontSize;
            Editor.ShowLineNumbers = ShowLineNumbers;

            // Cấu hình font - sử dụng monospace font
            Editor.FontFamily = new FontFamily("Consolas, Courier New, monospace");

            // Options cho editor
            Editor.Options.ConvertTabsToSpaces = true;
            Editor.Options.IndentationSize = 4;
            Editor.Options.EnableRectangularSelection = true;
            Editor.Options.EnableTextDragDrop = true;
            Editor.Options.ShowSpaces = false;
            Editor.Options.ShowTabs = false;
            Editor.Options.ShowEndOfLine = false;

            // Event handlers
            Editor.TextChanged += OnEditorTextChanged;
            Editor.PreviewKeyDown += OnEditorPreviewKeyDown;
        }

        private void ConfigureMonokaiAppearance()
        {
            if (Editor == null) return;

            // Background và foreground colors
            var bgColor = AvalonEditMonokaiHelper.GetMonokaiBackground();
            var fgColor = AvalonEditMonokaiHelper.GetMonokaiForeground();

            Editor.Background = new SolidColorBrush(bgColor);
            Editor.Foreground = new SolidColorBrush(fgColor);

            // Line number margin
            Editor.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(0x8F, 0x90, 0x8A)); // Màu xám nhạt cho số dòng

            // Current line highlighting
            Editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)); // Highlight nhẹ
            Editor.TextArea.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)), 1);

            // Selection color
            Editor.TextArea.SelectionBorder = null;
            Editor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x49, 0x48, 0x3E)); // Màu selection Monokai
            Editor.TextArea.SelectionForeground = null; // Giữ màu text gốc khi select

            // Caret (con trỏ)
            Editor.TextArea.Caret.CaretBrush = new SolidColorBrush(fgColor);
        }

        private void ApplySyntaxHighlighting()
        {
            if (Editor == null) return;

            var lang = SyntaxLanguage ?? "JavaScript";
            IHighlightingDefinition? highlighting = null;

            try
            {
                if (string.Equals(lang, "CSharp", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(lang, "C#", System.StringComparison.OrdinalIgnoreCase))
                {
                    highlighting = AvalonEditMonokaiHelper.GetCSharpMonokai();
                }
                else if (string.Equals(lang, "HTML", System.StringComparison.OrdinalIgnoreCase))
                {
                    highlighting = AvalonEditMonokaiHelper.GetHtmlMonokai();
                }
                else if (string.Equals(lang, "CSS", System.StringComparison.OrdinalIgnoreCase))
                {
                    highlighting = AvalonEditMonokaiHelper.GetCssMonokai();
                }
                else if (string.Equals(lang, "JavaScript", System.StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(lang, "JS", System.StringComparison.OrdinalIgnoreCase))
                {
                    highlighting = AvalonEditMonokaiHelper.GetJavaScriptMonokai();
                }
                else
                {
                    // Default: JavaScript
                    highlighting = AvalonEditMonokaiHelper.GetJavaScriptMonokai();
                }
            }
            catch { }

            if (highlighting != null)
            {
                Editor.SyntaxHighlighting = highlighting;
            }
        }

        private void OnEditorTextChanged(object? sender, System.EventArgs e)
        {
            if (_isUpdatingFromEditor) return;
            _isUpdatingFromEditor = true;
            try
            {
                SetCurrentValue(TextProperty, Editor.Text ?? string.Empty);
            }
            finally
            {
                _isUpdatingFromEditor = false;
            }
        }

        private void OnEditorPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            // F5 để refresh highlighting
            if (e.Key == Key.F5)
            {
                RefreshHighlight();
                e.Handled = true;
            }
            // Ctrl+A để select all
            else if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Editor?.SelectAll();
                e.Handled = true;
            }
        }

        private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SyntaxHighlightCodeEditor control) return;
            if (control._isUpdatingFromEditor) return;
            if (control.Editor == null) return;

            var newText = (string)(e.NewValue ?? string.Empty);
            if (control.Editor.Text == newText) return;

            control._isUpdatingFromEditor = true;
            try
            {
                control.Editor.Text = newText;
            }
            finally
            {
                control._isUpdatingFromEditor = false;
            }
        }

        private static void OnCodeFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SyntaxHighlightCodeEditor control && control.Editor != null)
            {
                control.Editor.FontSize = (double)e.NewValue;
            }
        }

        private static void OnSyntaxLanguageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SyntaxHighlightCodeEditor control || control.Editor == null) return;

            // Cập nhật màu sắc
            control.ConfigureMonokaiAppearance();

            // Áp dụng lại syntax highlighting
            control.ApplySyntaxHighlighting();
        }

        private static void OnShowLineNumbersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SyntaxHighlightCodeEditor control && control.Editor != null)
            {
                control.Editor.ShowLineNumbers = (bool)e.NewValue;
            }
        }
    }
}