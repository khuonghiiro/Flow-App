using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using MarkdigSyntax = Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace FlowMy.Controls
{
    public partial class MarkdownTextBlock : UserControl
    {
        public static readonly DependencyProperty MarkdownTextProperty =
            DependencyProperty.Register(
                nameof(MarkdownText),
                typeof(string),
                typeof(MarkdownTextBlock),
                new PropertyMetadata(string.Empty, OnMarkdownTextChanged));

        public string MarkdownText
        {
            get => (string)GetValue(MarkdownTextProperty);
            set => SetValue(MarkdownTextProperty, value);
        }

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(
                nameof(FontSize),
                typeof(double),
                typeof(MarkdownTextBlock),
                new PropertyMetadata(13.0, OnFontSizeChanged));

        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register(
                nameof(Foreground),
                typeof(Brush),
                typeof(MarkdownTextBlock),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(85, 85, 85)), OnForegroundChanged));

        public new Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public MarkdownTextBlock()
        {
            InitializeComponent();
        }

        private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownTextBlock control)
            {
                control.UpdateContent();
            }
        }

        private static void OnFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownTextBlock control)
            {
                control.UpdateContent();
            }
        }

        private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarkdownTextBlock control)
            {
                control.UpdateContent();
            }
        }

        private void UpdateContent()
        {
            if (string.IsNullOrWhiteSpace(MarkdownText))
            {
                MarkdownContent.Visibility = Visibility.Collapsed;
                PlainTextContent.Visibility = Visibility.Collapsed;
                return;
            }

            // Kiểm tra xem text có phải markdown không
            bool isMarkdown = IsMarkdownText(MarkdownText);

            if (isMarkdown)
            {
                // Render markdown
                RenderMarkdown(MarkdownText);
                MarkdownContent.Visibility = Visibility.Visible;
                PlainTextContent.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Hiển thị text thường
                PlainTextContent.Text = MarkdownText;
                PlainTextContent.FontSize = FontSize;
                PlainTextContent.Foreground = Foreground;
                PlainTextContent.LineHeight = 22;
                MarkdownContent.Visibility = Visibility.Collapsed;
                PlainTextContent.Visibility = Visibility.Visible;
            }
        }

        private bool IsMarkdownText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Kiểm tra các dấu hiệu markdown phổ biến
            var markdownPatterns = new[]
            {
                @"^#{1,6}\s",                    // Headers (# ## ###)
                @"\*\*.*?\*\*",                   // Bold **text**
                @"(?<!\*)\*(?!\*).*?(?<!\*)\*(?!\*)", // Italic *text* (không phải **)
                @"`[^`]+`",                       // Inline code `code`
                @"```[\s\S]*?```",               // Code blocks ```
                @"\[.*?\]\(.*?\)",               // Links [text](url)
                @"^\s*[-*+]\s",                  // Unordered lists
                @"^\s*\d+\.\s",                  // Ordered lists
                @"^>\s",                         // Blockquotes
                @"^\s*\|.*\|",                   // Tables
                @"^---+$",                       // Horizontal rules
                @"!\[.*?\]\(.*?\)",              // Images ![alt](url)
            };

            foreach (var pattern in markdownPatterns)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void RenderMarkdown(string markdown)
        {
            try
            {
                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                var document = Markdown.Parse(markdown, pipeline);
                
                var flowDoc = new FlowDocument();
                flowDoc.PagePadding = new Thickness(0);
                flowDoc.Background = Brushes.Transparent;

                bool hasContent = false;
                foreach (var block in document)
                {
                    if (block is MarkdigSyntax.ListBlock listBlock)
                    {
                        // Xử lý từng item trong list
                        for (int i = 0; i < listBlock.Count; i++)
                        {
                            if (listBlock[i] is MarkdigSyntax.ListItemBlock listItem)
                            {
                                // ListItemBlock là container, cần xử lý các block con
                                var listPara = new Paragraph();
                                listPara.Margin = new Thickness(20, 2, 0, 2);
                                listPara.FontSize = FontSize;
                                listPara.Foreground = Foreground;
                                listPara.LineHeight = 22;
                                listPara.Inlines.Add(new Run("• "));
                                
                                // Tìm ParagraphBlock đầu tiên trong list item để lấy Inline
                                bool hasItemContent = false;
                                foreach (var childBlock in listItem)
                                {
                                    if (childBlock is MarkdigSyntax.ParagraphBlock paraBlock && paraBlock.Inline != null)
                                    {
                                        AddInlines(listPara, paraBlock.Inline);
                                        hasItemContent = true;
                                        break;
                                    }
                                    else if (childBlock is MarkdigSyntax.LeafBlock childLeafBlock && childLeafBlock.Inline != null)
                                    {
                                        AddInlines(listPara, childLeafBlock.Inline);
                                        hasItemContent = true;
                                        break;
                                    }
                                }
                                
                                if (hasItemContent)
                                {
                                    flowDoc.Blocks.Add(listPara);
                                    hasContent = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        var paragraph = ConvertBlockToParagraph(block);
                        if (paragraph != null)
                        {
                            flowDoc.Blocks.Add(paragraph);
                            hasContent = true;
                        }
                    }
                }

                // Nếu có nội dung, hiển thị markdown, nếu không thì fallback về text thường
                if (hasContent && flowDoc.Blocks.Count > 0)
                {
                    MarkdownContent.Document = flowDoc;
                    MarkdownContent.Visibility = Visibility.Visible;
                    PlainTextContent.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Fallback về text thường nếu không render được
                    PlainTextContent.Text = markdown;
                    PlainTextContent.FontSize = FontSize;
                    PlainTextContent.Foreground = Foreground;
                    PlainTextContent.LineHeight = 22;
                    MarkdownContent.Visibility = Visibility.Collapsed;
                    PlainTextContent.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                // Nếu lỗi, hiển thị text thường
                PlainTextContent.Text = markdown;
                PlainTextContent.FontSize = FontSize;
                PlainTextContent.Foreground = Foreground;
                PlainTextContent.LineHeight = 22;
                MarkdownContent.Visibility = Visibility.Collapsed;
                PlainTextContent.Visibility = Visibility.Visible;
            }
        }

        private Paragraph? ConvertBlockToParagraph(MarkdigSyntax.Block block)
        {
            switch (block)
            {
                case MarkdigSyntax.HeadingBlock heading:
                    var headingPara = new Paragraph();
                    headingPara.FontSize = FontSize + (7 - heading.Level) * 2;
                    headingPara.FontWeight = FontWeights.Bold;
                    headingPara.Margin = new Thickness(0, 8, 0, 4);
                    headingPara.Foreground = Foreground;
                    headingPara.LineHeight = 22;
                    AddInlines(headingPara, heading.Inline);
                    return headingPara;

                case MarkdigSyntax.ParagraphBlock paraBlock:
                    var para = new Paragraph();
                    para.Margin = new Thickness(0, 4, 0, 4);
                    para.FontSize = FontSize;
                    para.Foreground = Foreground;
                    para.LineHeight = 22;
                    AddInlines(para, paraBlock.Inline);
                    return para;

                case MarkdigSyntax.CodeBlock codeBlock:
                    var codePara = new Paragraph();
                    codePara.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                    codePara.Padding = new Thickness(8);
                    codePara.Margin = new Thickness(0, 4, 0, 4);
                    codePara.FontFamily = new FontFamily("Consolas");
                    codePara.Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                    codePara.FontSize = FontSize - 1;
                    codePara.Inlines.Add(new Run(codeBlock.Lines.ToString()));
                    return codePara;

                case MarkdigSyntax.ListBlock listBlock:
                    // Xử lý list block - trả về paragraph đầu tiên, các item khác sẽ được thêm sau
                    if (listBlock.Count > 0 && listBlock[0] is MarkdigSyntax.ListItemBlock firstItem)
                    {
                        var listPara = new Paragraph();
                        listPara.Margin = new Thickness(20, 2, 0, 2);
                        listPara.FontSize = FontSize;
                        listPara.Foreground = Foreground;
                        listPara.LineHeight = 22;
                        listPara.Inlines.Add(new Run("• "));
                        
                        // Tìm ParagraphBlock đầu tiên trong list item để lấy Inline
                        foreach (var childBlock in firstItem)
                        {
                            if (childBlock is MarkdigSyntax.ParagraphBlock paraBlock && paraBlock.Inline != null)
                            {
                                AddInlines(listPara, paraBlock.Inline);
                                break;
                            }
                            else if (childBlock is MarkdigSyntax.LeafBlock childLeafBlock && childLeafBlock.Inline != null)
                            {
                                AddInlines(listPara, childLeafBlock.Inline);
                                break;
                            }
                        }
                        
                        return listPara;
                    }
                    return null;

                default:
                    if (block is MarkdigSyntax.LeafBlock leafBlock && leafBlock.Inline != null)
                    {
                        var defaultPara = new Paragraph();
                        defaultPara.Margin = new Thickness(0, 4, 0, 4);
                        defaultPara.FontSize = FontSize;
                        defaultPara.Foreground = Foreground;
                        defaultPara.LineHeight = 22;
                        AddInlines(defaultPara, leafBlock.Inline);
                        return defaultPara;
                    }
                    return null;
            }
        }

        private void AddInlines(Paragraph paragraph, ContainerInline? container)
        {
            if (container == null) return;

            var current = container.FirstChild;
            while (current != null)
            {
                switch (current)
                {
                    case LiteralInline literal:
                        paragraph.Inlines.Add(new Run(literal.Content.ToString()));
                        break;

                    case EmphasisInline emphasis:
                        // Xử lý nested inlines trong emphasis
                        if (emphasis.FirstChild != null)
                        {
                            var span = new Span();
                            if (emphasis.DelimiterChar == '*' || emphasis.DelimiterChar == '_')
                            {
                                if (emphasis.IsDouble)
                                {
                                    span.FontWeight = FontWeights.Bold;
                                }
                                else
                                {
                                    span.FontStyle = FontStyles.Italic;
                                }
                            }
                            AddInlinesToSpan(span, emphasis);
                            paragraph.Inlines.Add(span);
                        }
                        break;

                    case CodeInline code:
                        var codeRun = new Run(code.Content.ToString());
                        codeRun.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                        codeRun.FontFamily = new FontFamily("Consolas");
                        codeRun.Foreground = new SolidColorBrush(Color.FromRgb(220, 20, 60));
                        paragraph.Inlines.Add(codeRun);
                        break;

                    case LinkInline link:
                        var linkText = link.FirstChild != null ? GetInlineText(link) : (link.Url ?? "");
                        var linkRun = new Run(linkText);
                        linkRun.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                        linkRun.TextDecorations = TextDecorations.Underline;
                        paragraph.Inlines.Add(linkRun);
                        break;

                    case LineBreakInline:
                        paragraph.Inlines.Add(new LineBreak());
                        break;
                }

                current = current.NextSibling;
            }
        }

        private void AddInlinesToSpan(Span span, ContainerInline container)
        {
            var current = container.FirstChild;
            while (current != null)
            {
                if (current is LiteralInline literal)
                {
                    span.Inlines.Add(new Run(literal.Content.ToString()));
                }
                else if (current is EmphasisInline nestedEmphasis)
                {
                    var nestedSpan = new Span();
                    if (nestedEmphasis.IsDouble)
                    {
                        nestedSpan.FontWeight = FontWeights.Bold;
                    }
                    else
                    {
                        nestedSpan.FontStyle = FontStyles.Italic;
                    }
                    AddInlinesToSpan(nestedSpan, nestedEmphasis);
                    span.Inlines.Add(nestedSpan);
                }
                current = current.NextSibling;
            }
        }

        private string GetInlineText(ContainerInline container)
        {
            var text = new System.Text.StringBuilder();
            var current = container.FirstChild;
            while (current != null)
            {
                if (current is LiteralInline literal)
                {
                    text.Append(literal.Content);
                }
                current = current.NextSibling;
            }
            return text.ToString();
        }
    }
}

