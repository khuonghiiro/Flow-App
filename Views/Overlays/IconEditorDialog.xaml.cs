using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlowMy.Services.Workflow;

namespace FlowMy.Views.Overlays
{
    public partial class IconEditorDialog : Window
    {
        // Custom structures for SVG path parsing & editing
        public class PathCommand
        {
            public char Command { get; set; }
            public List<double> Parameters { get; set; } = new();

            public override string ToString()
            {
                if (Parameters.Count == 0) return Command.ToString();
                return Command + " " + string.Join(" ", Parameters.Select(p => p.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        public enum PointType
        {
            Anchor,
            Control1,
            Control2
        }

        public class CanvasPoint
        {
            public PathCommand OwnerCommand { get; set; } = null!;
            public int XIndex { get; set; }
            public int YIndex { get; set; }
            public PointType Type { get; set; }
            public double PrevX { get; set; }
            public double PrevY { get; set; }
        }

        // Editor state variables
        private List<PathCommand> _pathCommands = new();
        private CanvasPoint? _selectedPoint;
        private CanvasPoint? _draggedPoint;
        private bool _isDragging = false;
        private bool _isUpdatingFromCanvas = false;
        private bool _isSyncingSelection = false;

        private double _scaleX = 1.0;
        private double _scaleY = 1.0;
        private double _offsetX = 0.0;
        private double _offsetY = 0.0;

        // Full SVG editing state
        private System.Xml.Linq.XDocument? _currentSvg;
        private System.Xml.Linq.XElement? _editingPathElement;
        private readonly List<System.Windows.UIElement> _svgPreviewShapes = new();
        private string? _svgTempFilePath;

        // WebView2 variables
        private Microsoft.Web.WebView2.Wpf.WebView2? _webView;
        private string _activeProfileName = "Shared";

        // Static cache to keep WebView2 alive across dialog reopenings
        private static Microsoft.Web.WebView2.Wpf.WebView2? _cachedWebView;
        private static string? _cachedProfileName;
        private static System.Threading.CancellationTokenSource? _disposeCts;

        public IconEditorDialog()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                LoadAvailableIcons();
                LoadWebProfiles();
                InitializeWebView(_activeProfileName);
                
                // Initialize with a default template (Square)
                LoadTemplateSquare();
            };
        }

        #region Window Drag & Close
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            CleanupTempSvgFile();
            DetachAndCacheWebView();
        }

        private void CleanupTempSvgFile()
        {
            if (_svgTempFilePath != null)
            {
                try { if (File.Exists(_svgTempFilePath)) File.Delete(_svgTempFilePath); } catch { }
                _svgTempFilePath = null;
            }
        }
        #endregion

        #region Preview Background Color
        private void BtnBgColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                try
                {
                    CanvasPreviewBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                }
                catch { }
            }
        }

        private void BtnBgChecked_Click(object sender, RoutedEventArgs e)
        {
            CanvasPreviewBorder.Background = FindResource("PsDarkCheckeredBrush") as Brush;
        }

        private void BtnBgLightChecked_Click(object sender, RoutedEventArgs e)
        {
            CanvasPreviewBorder.Background = FindResource("PsLightCheckeredBrush") as Brush;
        }

        private void BtnPickColor_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
            string existing = TxtIconColor.Text.Trim();
            if (existing.StartsWith("#"))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(existing);
                    dialog.Color = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
                }
                catch { }
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtIconColor.Text = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}".ToLower();
            }
        }
        #endregion

        #region SVG Path Parser & Absolute coordinate converter
        private static List<PathCommand> ParsePathData(string pathData)
        {
            var commands = new List<PathCommand>();
            if (string.IsNullOrEmpty(pathData)) return commands;

            int i = 0;
            while (i < pathData.Length)
            {
                while (i < pathData.Length && (char.IsWhiteSpace(pathData[i]) || pathData[i] == ','))
                {
                    i++;
                }
                if (i >= pathData.Length) break;

                char c = pathData[i];
                if (char.IsLetter(c))
                {
                    var cmd = new PathCommand { Command = c };
                    commands.Add(cmd);
                    i++;

                    while (i < pathData.Length)
                    {
                        while (i < pathData.Length && (char.IsWhiteSpace(pathData[i]) || pathData[i] == ','))
                        {
                            i++;
                        }
                        if (i >= pathData.Length) break;

                        char nextChar = pathData[i];
                        if (char.IsLetter(nextChar) && nextChar != 'e' && nextChar != 'E')
                        {
                            break;
                        }

                        int start = i;
                        if (pathData[i] == '+' || pathData[i] == '-') i++;
                        while (i < pathData.Length && (char.IsDigit(pathData[i]) || pathData[i] == '.' || pathData[i] == 'e' || pathData[i] == 'E' || (i > 0 && (pathData[i - 1] == 'e' || pathData[i - 1] == 'E') && (pathData[i] == '+' || pathData[i] == '-'))))
                        {
                            i++;
                        }

                        if (i > start)
                        {
                            string numStr = pathData.Substring(start, i - start);
                            if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
                            {
                                cmd.Parameters.Add(val);
                            }
                        }
                        else
                        {
                            i++;
                        }
                    }
                }
                else
                {
                    i++;
                }
            }

            return commands;
        }

        private static List<PathCommand> ToAbsolute(List<PathCommand> commands)
        {
            var absCommands = new List<PathCommand>();
            double curX = 0;
            double curY = 0;
            double startX = 0;
            double startY = 0;

            foreach (var cmd in commands)
            {
                char c = cmd.Command;
                var absCmd = new PathCommand { Command = char.ToUpper(c) };
                var pars = new List<double>(cmd.Parameters);

                switch (c)
                {
                    case 'M':
                        if (pars.Count >= 2)
                        {
                            curX = pars[0];
                            curY = pars[1];
                            startX = curX;
                            startY = curY;
                            absCmd.Parameters.AddRange(pars);
                        }
                        break;
                    case 'm':
                        if (pars.Count >= 2)
                        {
                            curX += pars[0];
                            curY += pars[1];
                            startX = curX;
                            startY = curY;
                            absCmd.Parameters.Add(curX);
                            absCmd.Parameters.Add(curY);
                            for (int p = 2; p < pars.Count; p += 2)
                            {
                                if (p + 1 < pars.Count)
                                {
                                    curX += pars[p];
                                    curY += pars[p + 1];
                                    absCmd.Parameters.Add(curX);
                                    absCmd.Parameters.Add(curY);
                                }
                            }
                            absCmd.Command = 'M';
                        }
                        break;
                    case 'L':
                        if (pars.Count >= 2)
                        {
                            curX = pars[0];
                            curY = pars[1];
                            absCmd.Parameters.AddRange(pars);
                        }
                        break;
                    case 'l':
                        for (int p = 0; p < pars.Count; p += 2)
                        {
                            if (p + 1 < pars.Count)
                            {
                                curX += pars[p];
                                curY += pars[p + 1];
                                absCmd.Parameters.Add(curX);
                                absCmd.Parameters.Add(curY);
                            }
                        }
                        break;
                    case 'H':
                        if (pars.Count >= 1)
                        {
                            curX = pars[0];
                            absCmd.Parameters.AddRange(pars);
                        }
                        break;
                    case 'h':
                        for (int p = 0; p < pars.Count; p++)
                        {
                            curX += pars[p];
                            absCmd.Parameters.Add(curX);
                        }
                        break;
                    case 'V':
                        if (pars.Count >= 1)
                        {
                            curY = pars[0];
                            absCmd.Parameters.AddRange(pars);
                        }
                        break;
                    case 'v':
                        for (int p = 0; p < pars.Count; p++)
                        {
                            curY += pars[p];
                            absCmd.Parameters.Add(curY);
                        }
                        break;
                    case 'C':
                        if (pars.Count >= 6)
                        {
                            curX = pars[4];
                            curY = pars[5];
                            absCmd.Parameters.AddRange(pars);
                        }
                        break;
                    case 'c':
                        for (int p = 0; p < pars.Count; p += 6)
                        {
                            if (p + 5 < pars.Count)
                            {
                                absCmd.Parameters.Add(curX + pars[p]);
                                absCmd.Parameters.Add(curY + pars[p + 1]);
                                absCmd.Parameters.Add(curX + pars[p + 2]);
                                absCmd.Parameters.Add(curY + pars[p + 3]);
                                absCmd.Parameters.Add(curX + pars[p + 4]);
                                absCmd.Parameters.Add(curY + pars[p + 5]);
                                curX += pars[p + 4];
                                curY += pars[p + 5];
                            }
                        }
                        break;
                    case 'S':
                        if (pars.Count >= 4)
                        {
                            curX = pars[2];
                            curY = pars[3];
                            absCmd.Parameters.AddRange(pars);
                        }
                        break;
                    case 's':
                        for (int p = 0; p < pars.Count; p += 4)
                        {
                            if (p + 3 < pars.Count)
                            {
                                absCmd.Parameters.Add(curX + pars[p]);
                                absCmd.Parameters.Add(curY + pars[p + 1]);
                                absCmd.Parameters.Add(curX + pars[p + 2]);
                                absCmd.Parameters.Add(curY + pars[p + 3]);
                                curX += pars[p + 2];
                                curY += pars[p + 3];
                            }
                        }
                        break;
                    case 'Q':
                        if (pars.Count >= 4)
                        {
                            curX = pars[2];
                            curY = pars[3];
                            absCmd.Parameters.AddRange(pars);
                        }
                        break;
                    case 'q':
                        for (int p = 0; p < pars.Count; p += 4)
                        {
                            if (p + 3 < pars.Count)
                            {
                                absCmd.Parameters.Add(curX + pars[p]);
                                absCmd.Parameters.Add(curY + pars[p + 1]);
                                absCmd.Parameters.Add(curX + pars[p + 2]);
                                absCmd.Parameters.Add(curY + pars[p + 3]);
                                curX += pars[p + 2];
                                curY += pars[p + 3];
                            }
                        }
                        break;
                    case 'T':
                        if (pars.Count >= 2)
                        {
                            curX = pars[0];
                            curY = pars[1];
                            absCmd.Parameters.AddRange(pars);
                        }
                        break;
                    case 't':
                        for (int p = 0; p < pars.Count; p += 2)
                        {
                            if (p + 1 < pars.Count)
                            {
                                curX += pars[p];
                                curY += pars[p + 1];
                                absCmd.Parameters.Add(curX);
                                absCmd.Parameters.Add(curY);
                            }
                        }
                        break;
                    case 'A':
                        if (pars.Count >= 7)
                        {
                            curX = pars[5];
                            curY = pars[6];
                            absCmd.Parameters.AddRange(pars);
                        }
                        break;
                    case 'a':
                        for (int p = 0; p < pars.Count; p += 7)
                        {
                            if (p + 6 < pars.Count)
                            {
                                absCmd.Parameters.Add(pars[p]);
                                absCmd.Parameters.Add(pars[p + 1]);
                                absCmd.Parameters.Add(pars[p + 2]);
                                absCmd.Parameters.Add(pars[p + 3]);
                                absCmd.Parameters.Add(pars[p + 4]);
                                absCmd.Parameters.Add(curX + pars[p + 5]);
                                absCmd.Parameters.Add(curY + pars[p + 6]);
                                curX += pars[p + 5];
                                curY += pars[p + 6];
                            }
                        }
                        break;
                    case 'Z':
                    case 'z':
                        curX = startX;
                        curY = startY;
                        break;
                }

                absCommands.Add(absCmd);
            }

            return absCommands;
        }

        private static string PathCommandsToString(List<PathCommand> commands)
        {
            return string.Join(" ", commands.Select(c => c.ToString()));
        }
        #endregion

        #region Interactive Canvas Editor & Rendering
        private void RecalculateTransform()
        {
            double gridW = CanvasContainerGrid.ActualWidth;
            double gridH = CanvasContainerGrid.ActualHeight;
            if (gridW == 0 || gridH == 0) return;

            Rect bounds;
            if (_currentSvg?.Root != null)
            {
                string vb = _currentSvg.Root.Attribute("viewBox")?.Value ?? "";
                var parts = vb.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double vx) &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double vy) &&
                    double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double vw) &&
                    double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double vh))
                {
                    bounds = new Rect(vx, vy, Math.Max(1, vw), Math.Max(1, vh));
                }
                else
                {
                    bounds = SvgPreviewPath.Data?.Bounds ?? new Rect(0, 0, 512, 512);
                }
            }
            else
            {
                bounds = SvgPreviewPath.Data?.Bounds ?? new Rect(0, 0, 512, 512);
            }
            if (bounds.Width == 0) bounds.Width = 1;
            if (bounds.Height == 0) bounds.Height = 1;

            double padding = 40;
            double availW = Math.Max(10, gridW - 2 * padding);
            double availH = Math.Max(10, gridH - 2 * padding);

            double scale = Math.Min(availW / bounds.Width, availH / bounds.Height);
            _scaleX = scale;
            _scaleY = scale;

            _offsetX = (gridW - bounds.Width * scale) / 2 - bounds.X * scale;
            _offsetY = (gridH - bounds.Height * scale) / 2 - bounds.Y * scale;

            // Sync SvgPreviewPath transform
            var group = new TransformGroup();
            group.Children.Add(new ScaleTransform(scale, scale));
            group.Children.Add(new TranslateTransform(_offsetX, _offsetY));
            SvgPreviewPath.RenderTransform = group;
        }

        private List<CanvasPoint> GetCanvasPoints(List<PathCommand> absCommands)
        {
            var points = new List<CanvasPoint>();
            double curX = 0;
            double curY = 0;
            double startX = 0;
            double startY = 0;

            foreach (var cmd in absCommands)
            {
                var pars = cmd.Parameters;
                switch (cmd.Command)
                {
                    case 'M':
                        if (pars.Count >= 2)
                        {
                            points.Add(new CanvasPoint
                            {
                                OwnerCommand = cmd,
                                XIndex = 0,
                                YIndex = 1,
                                Type = PointType.Anchor,
                                PrevX = curX,
                                PrevY = curY
                            });
                            curX = pars[0];
                            curY = pars[1];
                            startX = curX;
                            startY = curY;
                        }
                        break;
                    case 'L':
                    case 'T':
                        if (pars.Count >= 2)
                        {
                            points.Add(new CanvasPoint
                            {
                                OwnerCommand = cmd,
                                XIndex = 0,
                                YIndex = 1,
                                Type = PointType.Anchor,
                                PrevX = curX,
                                PrevY = curY
                            });
                            curX = pars[0];
                            curY = pars[1];
                        }
                        break;
                    case 'H':
                        if (pars.Count >= 1)
                        {
                            points.Add(new CanvasPoint
                            {
                                OwnerCommand = cmd,
                                XIndex = 0,
                                YIndex = -1,
                                Type = PointType.Anchor,
                                PrevX = curX,
                                PrevY = curY
                            });
                            curX = pars[0];
                        }
                        break;
                    case 'V':
                        if (pars.Count >= 1)
                        {
                            points.Add(new CanvasPoint
                            {
                                OwnerCommand = cmd,
                                XIndex = -1,
                                YIndex = 0,
                                Type = PointType.Anchor,
                                PrevX = curX,
                                PrevY = curY
                            });
                            curY = pars[0];
                        }
                        break;
                    case 'C':
                        if (pars.Count >= 6)
                        {
                            points.Add(new CanvasPoint { OwnerCommand = cmd, XIndex = 0, YIndex = 1, Type = PointType.Control1, PrevX = curX, PrevY = curY });
                            points.Add(new CanvasPoint { OwnerCommand = cmd, XIndex = 2, YIndex = 3, Type = PointType.Control2, PrevX = curX, PrevY = curY });
                            points.Add(new CanvasPoint { OwnerCommand = cmd, XIndex = 4, YIndex = 5, Type = PointType.Anchor, PrevX = curX, PrevY = curY });
                            curX = pars[4];
                            curY = pars[5];
                        }
                        break;
                    case 'S':
                    case 'Q':
                        if (pars.Count >= 4)
                        {
                            points.Add(new CanvasPoint { OwnerCommand = cmd, XIndex = 0, YIndex = 1, Type = PointType.Control1, PrevX = curX, PrevY = curY });
                            points.Add(new CanvasPoint { OwnerCommand = cmd, XIndex = 2, YIndex = 3, Type = PointType.Anchor, PrevX = curX, PrevY = curY });
                            curX = pars[2];
                            curY = pars[3];
                        }
                        break;
                    case 'A':
                        if (pars.Count >= 7)
                        {
                            points.Add(new CanvasPoint { OwnerCommand = cmd, XIndex = 5, YIndex = 6, Type = PointType.Anchor, PrevX = curX, PrevY = curY });
                            curX = pars[5];
                            curY = pars[6];
                        }
                        break;
                    case 'Z':
                        curX = startX;
                        curY = startY;
                        break;
                }
            }
            return points;
        }

        private void UpdatePreviewPath()
        {
            if (_currentSvg != null)
            {
                RenderFullSvgPreview();
                return;
            }
            try
            {
                SvgPreviewPath.Visibility = Visibility.Visible;
                SvgFullPreview.Visibility = Visibility.Collapsed;
                string pathData = PathCommandsToString(_pathCommands);
                var geometry = Geometry.Parse(pathData);
                SvgPreviewPath.Data = geometry;
                TxtStatus.Text = "Cú pháp path hợp lệ.";
                TxtStatus.Foreground = FindResource("TextMuted") as Brush;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Lỗi path: " + ex.Message;
                TxtStatus.Foreground = Brushes.Tomato;
            }
        }

        private void ClearSvgPreviewShapes()
        {
            foreach (var shape in _svgPreviewShapes)
            {
                EditingCanvas.Children.Remove(shape);
            }
            _svgPreviewShapes.Clear();
        }

        private static Brush? ParseSvgBrush(string? value)
        {
            if (string.IsNullOrEmpty(value) || value == "none") return null;
            if (value == "currentColor") return Brushes.Lime;
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); }
            catch { return null; }
        }

        private static double ParseSvgDouble(string? value, double fallback = 0)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }

        private PenLineCap ParseLineCap(string? value)
        {
            return value switch { "round" => PenLineCap.Round, "square" => PenLineCap.Square, _ => PenLineCap.Flat };
        }

        private PenLineJoin ParseLineJoin(string? value)
        {
            return value switch { "round" => PenLineJoin.Round, "bevel" => PenLineJoin.Bevel, _ => PenLineJoin.Miter };
        }

        private void RenderFullSvgPreview()
        {
            ClearSvgPreviewShapes();
            SvgPreviewPath.Data = null;
            SvgPreviewPath.Visibility = Visibility.Collapsed;
            if (_currentSvg?.Root == null)
            {
                SvgFullPreview.Visibility = Visibility.Collapsed;
                return;
            }

            // Write SVG to temp file and render with SharpVectors (SvgViewboxEx)
            try
            {
                if (_svgTempFilePath == null)
                {
                    var tempDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "svg_editor_temp");
                    Directory.CreateDirectory(tempDir);
                    _svgTempFilePath = System.IO.Path.Combine(tempDir, $"preview_{Guid.NewGuid():N}.svg");
                }
                File.WriteAllText(_svgTempFilePath, _currentSvg.ToString());

                // Force reload: set Source=null first so DependencyProperty fires callback
                // even when path string is the same (same temp file, new content)
                string colorText = TxtIconColor.Text.Trim();
                Brush fillBrush = Brushes.Lime;
                if (colorText.StartsWith("#"))
                {
                    try { fillBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorText)); } catch { }
                }
                else
                {
                    fillBrush = FindResource("AccentColor") as Brush ?? Brushes.Lime;
                }

                SvgFullPreview.Source = null;
                SvgFullPreview.Fill = fillBrush;
                SvgFullPreview.UseOriginalColors = ChkOriginalColor.IsChecked == true;
                SvgFullPreview.Source = _svgTempFilePath;
                SvgFullPreview.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IconEditor] SVG preview error: {ex.Message}");
                SvgFullPreview.Visibility = Visibility.Collapsed;
            }

            TxtStatus.Text = "SVG hợp lệ.";
            TxtStatus.Foreground = FindResource("TextMuted") as Brush;
        }

        private void ApplyStroke(Shape shape, System.Xml.Linq.XElement el)
        {
            var strokeBrush = ParseSvgBrush(el.Attribute("stroke")?.Value);
            if (strokeBrush != null)
            {
                shape.Stroke = strokeBrush;
                double sw = ParseSvgDouble(el.Attribute("stroke-width")?.Value, 1);
                shape.StrokeThickness = sw * Math.Min(_scaleX, _scaleY);
                shape.StrokeStartLineCap = ParseLineCap(el.Attribute("stroke-linecap")?.Value);
                shape.StrokeEndLineCap = ParseLineCap(el.Attribute("stroke-linecap")?.Value);
                shape.StrokeLineJoin = ParseLineJoin(el.Attribute("stroke-linejoin")?.Value);
            }
        }

        private void RenderCanvasHandles()
        {
            EditingCanvas.Children.Clear();

            if (_pathCommands == null || _pathCommands.Count == 0) return;

            var points = GetCanvasPoints(_pathCommands);
            RecalculateTransform();

            // Draw helper dashed lines for Bezier handles first
            foreach (var pt in points)
            {
                if (pt.Type == PointType.Control1 || pt.Type == PointType.Control2)
                {
                    double fromX = pt.PrevX * _scaleX + _offsetX;
                    double fromY = pt.PrevY * _scaleY + _offsetY;
                    
                    if (pt.Type == PointType.Control2)
                    {
                        var pars = pt.OwnerCommand.Parameters;
                        if (pt.OwnerCommand.Command == 'C' && pars.Count >= 6)
                        {
                            fromX = pars[4] * _scaleX + _offsetX;
                            fromY = pars[5] * _scaleY + _offsetY;
                        }
                        else if ((pt.OwnerCommand.Command == 'S' || pt.OwnerCommand.Command == 'Q') && pars.Count >= 4)
                        {
                            fromX = pars[2] * _scaleX + _offsetX;
                            fromY = pars[3] * _scaleY + _offsetY;
                        }
                    }

                    double toX = (pt.XIndex >= 0 ? pt.OwnerCommand.Parameters[pt.XIndex] : pt.PrevX) * _scaleX + _offsetX;
                    double toY = (pt.YIndex >= 0 ? pt.OwnerCommand.Parameters[pt.YIndex] : pt.PrevY) * _scaleY + _offsetY;

                    var line = new Line
                    {
                        X1 = fromX,
                        Y1 = fromY,
                        X2 = toX,
                        Y2 = toY,
                        Stroke = new SolidColorBrush(Color.FromArgb(120, 79, 255, 176)),
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 3, 2 }
                    };
                    EditingCanvas.Children.Add(line);
                }
            }

            // Draw interactive point handles
            foreach (var pt in points)
            {
                double x = (pt.XIndex >= 0 ? pt.OwnerCommand.Parameters[pt.XIndex] : pt.PrevX);
                double y = (pt.YIndex >= 0 ? pt.OwnerCommand.Parameters[pt.YIndex] : pt.PrevY);

                double cx = x * _scaleX + _offsetX;
                double cy = y * _scaleY + _offsetY;

                double size = pt.Type == PointType.Anchor ? 8 : 6;
                var brush = pt.Type == PointType.Anchor 
                    ? (FindResource("AccentColor") as Brush ?? Brushes.Lime)
                    : Brushes.LightGreen;

                // Check selection
                bool isSelected = (_selectedPoint != null && 
                                   _selectedPoint.OwnerCommand == pt.OwnerCommand && 
                                   _selectedPoint.XIndex == pt.XIndex && 
                                   _selectedPoint.YIndex == pt.YIndex);
                if (isSelected)
                {
                    brush = Brushes.Red;
                    size += 2;
                }

                var ellipse = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = brush,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Cursor = Cursors.Hand,
                    Tag = pt
                };

                Canvas.SetLeft(ellipse, cx - size / 2);
                Canvas.SetTop(ellipse, cy - size / 2);

                ellipse.MouseLeftButtonDown += (s, e) =>
                {
                    _selectedPoint = pt;
                    _draggedPoint = pt;
                    _isDragging = true;
                    ellipse.CaptureMouse();
                    e.Handled = true;
                    RenderCanvasHandles();
                };

                ellipse.MouseLeftButtonUp += (s, e) =>
                {
                    _isDragging = false;
                    _draggedPoint = null;
                    ellipse.ReleaseMouseCapture();
                    e.Handled = true;
                };

                EditingCanvas.Children.Add(ellipse);
            }
        }

        private void SyncCanvasToTextBox()
        {
            _isUpdatingFromCanvas = true;
            if (_currentSvg != null && _editingPathElement != null)
            {
                _editingPathElement.SetAttributeValue("d", PathCommandsToString(_pathCommands));
                TxtPathData.Text = _currentSvg.ToString();
            }
            else
            {
                TxtPathData.Text = PathCommandsToString(_pathCommands);
            }
            _isUpdatingFromCanvas = false;
        }

        private void EditingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _draggedPoint != null)
            {
                var pos = e.GetPosition(EditingCanvas);
                double designX = Math.Round((pos.X - _offsetX) / _scaleX, 1);
                double designY = Math.Round((pos.Y - _offsetY) / _scaleY, 1);

                if (_draggedPoint.XIndex >= 0 && _draggedPoint.XIndex < _draggedPoint.OwnerCommand.Parameters.Count)
                {
                    _draggedPoint.OwnerCommand.Parameters[_draggedPoint.XIndex] = designX;
                }
                if (_draggedPoint.YIndex >= 0 && _draggedPoint.YIndex < _draggedPoint.OwnerCommand.Parameters.Count)
                {
                    _draggedPoint.OwnerCommand.Parameters[_draggedPoint.YIndex] = designY;
                }

                SyncCanvasToTextBox();
                UpdatePreviewPath();
                RenderCanvasHandles();
            }
        }

        private void EditingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (RadAddPoint.IsChecked == true)
            {
                var pos = e.GetPosition(EditingCanvas);
                RecalculateTransform();
                double designX = Math.Round((pos.X - _offsetX) / _scaleX, 1);
                double designY = Math.Round((pos.Y - _offsetY) / _scaleY, 1);

                var newCmd = new PathCommand { Command = 'L' };
                newCmd.Parameters.Add(designX);
                newCmd.Parameters.Add(designY);

                if (_pathCommands.Count > 0 && char.ToUpper(_pathCommands[^1].Command) == 'Z')
                {
                    _pathCommands.Insert(_pathCommands.Count - 1, newCmd);
                }
                else
                {
                    _pathCommands.Add(newCmd);
                }

                SyncCanvasToTextBox();

                UpdatePreviewPath();
                RenderCanvasHandles();
            }
            else
            {
                // Click on canvas background clears selection
                _selectedPoint = null;
                RenderCanvasHandles();
            }
        }

        private void EditingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _draggedPoint = null;
        }

        private void CanvasContainerGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePreviewPath();
            RenderCanvasHandles();
        }
        #endregion

        #region SVG/Path Handlers (TextBox synchronizer)
        private void TxtPathData_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFromCanvas) return;

            string text = TxtPathData.Text?.Trim() ?? "";

            // Detect full SVG XML vs raw path data
            if (text.StartsWith("<"))
            {
                try
                {
                    _currentSvg = System.Xml.Linq.XDocument.Parse(text);
                    var ns = _currentSvg.Root?.Name.Namespace ?? System.Xml.Linq.XNamespace.None;
                    _editingPathElement = _currentSvg.Descendants(ns + "path").FirstOrDefault();

                    if (_editingPathElement != null)
                    {
                        string pathD = _editingPathElement.Attribute("d")?.Value ?? "";
                        _pathCommands = ToAbsolute(ParsePathData(pathD));
                    }
                    else
                    {
                        _pathCommands.Clear();
                    }

                    RenderFullSvgPreview();
                    RenderCanvasHandles();
                }
                catch (Exception ex)
                {
                    TxtStatus.Text = "Lỗi SVG: " + ex.Message;
                    TxtStatus.Foreground = Brushes.Tomato;
                }
            }
            else
            {
                // Raw path data (backward compatible)
                _currentSvg = null;
                _editingPathElement = null;
                ClearSvgPreviewShapes();

                try
                {
                    var parsed = ParsePathData(text);
                    _pathCommands = ToAbsolute(parsed);
                    UpdatePreviewPath();
                    RenderCanvasHandles();
                }
                catch (Exception ex)
                {
                    TxtStatus.Text = "Lỗi cú pháp: " + ex.Message;
                    TxtStatus.Foreground = Brushes.Tomato;
                }
            }
        }
        #endregion

        #region SVG Editing Tools
        private void BtnDeletePoint_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPoint != null)
            {
                _pathCommands.Remove(_selectedPoint.OwnerCommand);
                _selectedPoint = null;

                _isUpdatingFromCanvas = true;
                TxtPathData.Text = PathCommandsToString(_pathCommands);
                _isUpdatingFromCanvas = false;

                UpdatePreviewPath();
                RenderCanvasHandles();
            }
            else
            {
                MessageBox.Show("Vui lòng chọn 1 điểm để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnMakeLine_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPoint != null && _selectedPoint.OwnerCommand.Command == 'C')
            {
                var cmd = _selectedPoint.OwnerCommand;
                var pars = cmd.Parameters;
                if (pars.Count >= 6)
                {
                    double x = pars[4];
                    double y = pars[5];
                    cmd.Command = 'L';
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add(x);
                    cmd.Parameters.Add(y);

                    SyncCanvasToTextBox();

                    UpdatePreviewPath();
                    RenderCanvasHandles();
                }
            }
        }

        private void BtnMakeCurve_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPoint != null && _selectedPoint.OwnerCommand.Command == 'L')
            {
                var cmd = _selectedPoint.OwnerCommand;
                var pars = cmd.Parameters;
                if (pars.Count >= 2)
                {
                    double prevX = _selectedPoint.PrevX;
                    double prevY = _selectedPoint.PrevY;
                    double x = pars[0];
                    double y = pars[1];

                    double c1x = Math.Round(prevX + (x - prevX) / 3, 1);
                    double c1y = Math.Round(prevY + (y - prevY) / 3, 1);
                    double c2x = Math.Round(prevX + 2 * (x - prevX) / 3, 1);
                    double c2y = Math.Round(prevY + 2 * (y - prevY) / 3, 1);

                    cmd.Command = 'C';
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add(c1x);
                    cmd.Parameters.Add(c1y);
                    cmd.Parameters.Add(c2x);
                    cmd.Parameters.Add(c2y);
                    cmd.Parameters.Add(x);
                    cmd.Parameters.Add(y);

                    SyncCanvasToTextBox();

                    UpdatePreviewPath();
                    RenderCanvasHandles();
                }
            }
        }

        private void ApplyTranslation(double dx, double dy)
        {
            foreach (var cmd in _pathCommands)
            {
                var pars = cmd.Parameters;
                switch (cmd.Command)
                {
                    case 'M':
                    case 'L':
                    case 'T':
                        if (pars.Count >= 2) { pars[0] += dx; pars[1] += dy; }
                        break;
                    case 'H':
                        if (pars.Count >= 1) { pars[0] += dx; }
                        break;
                    case 'V':
                        if (pars.Count >= 1) { pars[0] += dy; }
                        break;
                    case 'C':
                        if (pars.Count >= 6)
                        {
                            pars[0] += dx; pars[1] += dy;
                            pars[2] += dx; pars[3] += dy;
                            pars[4] += dx; pars[5] += dy;
                        }
                        break;
                    case 'S':
                    case 'Q':
                        if (pars.Count >= 4)
                        {
                            pars[0] += dx; pars[1] += dy;
                            pars[2] += dx; pars[3] += dy;
                        }
                        break;
                    case 'A':
                        if (pars.Count >= 7) { pars[5] += dx; pars[6] += dy; }
                        break;
                }
            }
            SyncCanvasToTextBox();
            UpdatePreviewPath();
            RenderCanvasHandles();
        }

        private void BtnShiftUp_Click(object sender, RoutedEventArgs e) => ApplyTranslation(0, -10);
        private void BtnShiftDown_Click(object sender, RoutedEventArgs e) => ApplyTranslation(0, 10);
        private void BtnShiftLeft_Click(object sender, RoutedEventArgs e) => ApplyTranslation(-10, 0);
        private void BtnShiftRight_Click(object sender, RoutedEventArgs e) => ApplyTranslation(10, 0);

        private void BtnScaleUp_Click(object sender, RoutedEventArgs e) => ApplyScale(1.1);
        private void BtnScaleDown_Click(object sender, RoutedEventArgs e) => ApplyScale(0.9);

        private void ApplyScale(double scale)
        {
            var bounds = SvgPreviewPath.Data?.Bounds ?? new Rect(0, 0, 512, 512);
            double centerX = bounds.X + bounds.Width / 2;
            double centerY = bounds.Y + bounds.Height / 2;

            foreach (var cmd in _pathCommands)
            {
                var pars = cmd.Parameters;
                switch (cmd.Command)
                {
                    case 'M':
                    case 'L':
                    case 'T':
                        if (pars.Count >= 2)
                        {
                            pars[0] = Math.Round(centerX + (pars[0] - centerX) * scale, 1);
                            pars[1] = Math.Round(centerY + (pars[1] - centerY) * scale, 1);
                        }
                        break;
                    case 'H':
                        if (pars.Count >= 1)
                        {
                            pars[0] = Math.Round(centerX + (pars[0] - centerX) * scale, 1);
                        }
                        break;
                    case 'V':
                        if (pars.Count >= 1)
                        {
                            pars[0] = Math.Round(centerY + (pars[0] - centerY) * scale, 1);
                        }
                        break;
                    case 'C':
                        if (pars.Count >= 6)
                        {
                            pars[0] = Math.Round(centerX + (pars[0] - centerX) * scale, 1);
                            pars[1] = Math.Round(centerY + (pars[1] - centerY) * scale, 1);
                            pars[2] = Math.Round(centerX + (pars[2] - centerX) * scale, 1);
                            pars[3] = Math.Round(centerY + (pars[3] - centerY) * scale, 1);
                            pars[4] = Math.Round(centerX + (pars[4] - centerX) * scale, 1);
                            pars[5] = Math.Round(centerY + (pars[5] - centerY) * scale, 1);
                        }
                        break;
                    case 'S':
                    case 'Q':
                        if (pars.Count >= 4)
                        {
                            pars[0] = Math.Round(centerX + (pars[0] - centerX) * scale, 1);
                            pars[1] = Math.Round(centerY + (pars[1] - centerY) * scale, 1);
                            pars[2] = Math.Round(centerX + (pars[2] - centerX) * scale, 1);
                            pars[3] = Math.Round(centerY + (pars[3] - centerY) * scale, 1);
                        }
                        break;
                    case 'A':
                        if (pars.Count >= 7)
                        {
                            pars[0] = Math.Round(pars[0] * scale, 1); // rx
                            pars[1] = Math.Round(pars[1] * scale, 1); // ry
                            pars[5] = Math.Round(centerX + (pars[5] - centerX) * scale, 1);
                            pars[6] = Math.Round(centerY + (pars[6] - centerY) * scale, 1);
                        }
                        break;
                }
            }
            SyncCanvasToTextBox();
            UpdatePreviewPath();
            RenderCanvasHandles();
        }
        #endregion

        #region Base templates loading
        private static string WrapSvg(string body, string viewBox = "0 0 512 512")
            => $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{viewBox}\">\n{body}\n</svg>";

        private void LoadTemplateSquare()
        {
            TxtPathData.Text = WrapSvg("  <path fill=\"currentColor\" d=\"M 100 100 L 400 100 L 400 400 L 100 400 Z\" />");
        }
        private void BtnTemplateSquare_Click(object sender, RoutedEventArgs e) => LoadTemplateSquare();

        private void BtnTemplateCircle_Click(object sender, RoutedEventArgs e)
        {
            TxtPathData.Text = WrapSvg("  <path fill=\"currentColor\" d=\"M 250 100 A 150 150 0 1 1 249.9 100 Z\" />");
        }

        private void BtnTemplateTriangle_Click(object sender, RoutedEventArgs e)
        {
            TxtPathData.Text = WrapSvg("  <path fill=\"currentColor\" d=\"M 250 100 L 400 400 L 100 400 Z\" />");
        }

        private void BtnTemplateStar_Click(object sender, RoutedEventArgs e)
        {
            TxtPathData.Text = WrapSvg("  <path fill=\"currentColor\" d=\"M 250 50 L 310 170 L 440 190 L 350 280 L 370 410 L 250 350 L 130 410 L 150 280 L 60 190 L 190 170 Z\" />");
        }

        private void BtnTemplateGear_Click(object sender, RoutedEventArgs e)
        {
            TxtPathData.Text = WrapSvg("  <path fill=\"currentColor\" d=\"M 250 50 L 280 110 L 340 110 L 360 160 L 410 190 L 390 250 L 410 310 L 360 340 L 340 390 L 280 390 L 250 450 L 220 390 L 160 390 L 140 340 L 90 310 L 110 250 L 90 190 L 140 160 L 160 110 L 220 110 Z M 250 170 A 80 80 0 1 0 250 330 A 80 80 0 1 0 250 170 Z\" />");
        }
        #endregion

        #region ComboBox available list populators & loaders
        private void LoadAvailableIcons()
        {
            CmbIconSelector.Items.Clear();
            CmbIconSelector.Items.Add(new ComboBoxItem { Content = "-- Thêm mới --", Tag = "" });

            var icons = IconResources.EffectiveIcons;
            foreach (var key in icons.Keys.OrderBy(k => k))
            {
                CmbIconSelector.Items.Add(new ComboBoxItem { Content = key, Tag = key });
            }
            CmbIconSelector.SelectedIndex = 0;
        }

        private void CmbIconSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingSelection) return;
            _isSyncingSelection = true;

            if (CmbIconSelector.SelectedItem is ComboBoxItem item && item.Tag is string key && !string.IsNullOrEmpty(key))
            {
                try
                {
                    string? relPath = IconResources.GetIconPath(key);
                    if (!string.IsNullOrEmpty(relPath))
                    {
                        string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relPath.TrimStart('/', '\\').Replace('/', System.IO.Path.DirectorySeparatorChar).Replace('\\', System.IO.Path.DirectorySeparatorChar));
                        if (File.Exists(fullPath))
                        {
                            // Load full SVG file content
                            string svgContent = File.ReadAllText(fullPath).Trim();
                            TxtPathData.Text = svgContent;
                            TxtIconName.Text = key.Split(' ').FirstOrDefault() ?? "";

                            // Detect .color from key's folder part
                            string folderPart = key.Split(' ').LastOrDefault() ?? "";
                            if (folderPart.EndsWith(".color"))
                            {
                                ChkOriginalColor.IsChecked = true;
                                // Try to extract fill color from first path
                                try
                                {
                                    var xmlDoc = System.Xml.Linq.XDocument.Parse(svgContent);
                                    var ns = xmlDoc.Root?.Name.Namespace ?? System.Xml.Linq.XNamespace.None;
                                    var pathEl = xmlDoc.Descendants(ns + "path").FirstOrDefault();
                                    string fill = pathEl?.Attribute("fill")?.Value ?? "currentColor";
                                    TxtIconColor.Text = fill.StartsWith("#") ? fill : "#4fffb0";
                                }
                                catch { TxtIconColor.Text = "#4fffb0"; }
                                TxtIconColor.IsEnabled = true;
                            }
                            else
                            {
                                ChkOriginalColor.IsChecked = false;
                                TxtIconColor.Text = "currentColor";
                                TxtIconColor.IsEnabled = false;
                            }

                            BtnSave.Content = "Cập nhật";
                            BtnSaveAndClose.Content = "Cập nhật & Đóng";
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi load file SVG: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                TxtIconName.Text = "";
                TxtIconColor.Text = "currentColor";
                TxtIconColor.IsEnabled = false;
                ChkOriginalColor.IsChecked = false;
                BtnSave.Content = "Thêm";
                BtnSaveAndClose.Content = "Thêm & Đóng";
                LoadTemplateSquare();
            }

            _isSyncingSelection = false;
        }

        // CmbIconStyle_SelectionChanged removed - folder is auto-determined as custom-icon / custom-icon.color

        private void ChkOriginalColor_Click(object sender, RoutedEventArgs e)
        {
            if (ChkOriginalColor.IsChecked == true)
            {
                TxtIconColor.IsEnabled = true;
                if (TxtIconColor.Text == "currentColor") TxtIconColor.Text = "#4fffb0";
            }
            else
            {
                TxtIconColor.Text = "currentColor";
                TxtIconColor.IsEnabled = false;
            }
            UpdatePreviewColor();
        }

        private void TxtIconColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreviewColor();
        }

        private void UpdatePreviewColor()
        {
            if (SvgPreviewPath == null) return;
            string colorText = TxtIconColor.Text.Trim();
            Brush fillBrush;
            if (colorText.StartsWith("#"))
            {
                try
                {
                    fillBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorText));
                }
                catch
                {
                    fillBrush = FindResource("AccentColor") as Brush ?? Brushes.Lime;
                }
            }
            else
            {
                fillBrush = FindResource("AccentColor") as Brush ?? Brushes.Lime;
            }

            SvgPreviewPath.Fill = fillBrush;

            if (SvgFullPreview != null)
            {
                SvgFullPreview.Fill = fillBrush;
                SvgFullPreview.UseOriginalColors = ChkOriginalColor.IsChecked == true;

                // Force reload to apply the new color if preview source is loaded
                if (SvgFullPreview.Source != null)
                {
                    var oldSrc = SvgFullPreview.Source;
                    SvgFullPreview.Source = null;
                    SvgFullPreview.Source = oldSrc;
                }
            }
        }
        #endregion

        #region WebView2 Browser Integration
        private static readonly string _iconEditorUrlHistoryPath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Cache", "icon_editor_url_history.txt");

        private static string LoadLastUrl()
        {
            try { if (File.Exists(_iconEditorUrlHistoryPath)) return File.ReadAllText(_iconEditorUrlHistoryPath).Trim(); } catch { }
            return "https://www.svgrepo.com";
        }

        private static void SaveLastUrl(string url)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_iconEditorUrlHistoryPath)!);
                File.WriteAllText(_iconEditorUrlHistoryPath, url);
            }
            catch { }
        }

        private void OnWebViewNavigationStarting(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
        {
            Dispatcher.Invoke(() => UrlLoadingIndicator.Visibility = Visibility.Visible);
        }

        private void OnWebViewNavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UrlLoadingIndicator.Visibility = Visibility.Collapsed;
                if (_webView?.CoreWebView2 != null)
                {
                    TxtWebUrl.Text = _webView.CoreWebView2.Source;
                    SaveLastUrl(_webView.CoreWebView2.Source);
                }
            });
        }

        private static void StartCacheExpiryTimer()
        {
            _disposeCts?.Cancel();
            _disposeCts = new System.Threading.CancellationTokenSource();
            var token = _disposeCts.Token;
            Task.Delay(TimeSpan.FromMinutes(3), token).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && !token.IsCancellationRequested)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (_cachedWebView != null)
                        {
                            try { _cachedWebView.Dispose(); } catch { }
                            _cachedWebView = null;
                            _cachedProfileName = null;
                        }
                    });
                }
            });
        }

        private void DetachAndCacheWebView()
        {
            if (_webView != null)
            {
                // Unhook event handlers to prevent leaks
                _webView.NavigationStarting -= OnWebViewNavigationStarting;
                _webView.NavigationCompleted -= OnWebViewNavigationCompleted;

                // Detach from current parent
                if (WebBrowserContainer != null && WebBrowserContainer.Children.Contains(_webView))
                {
                    WebBrowserContainer.Children.Remove(_webView);
                }

                _cachedWebView = _webView;
                _cachedProfileName = _activeProfileName;
                _webView = null;

                // Start 3-minute delayed disposal timer
                StartCacheExpiryTimer();
            }
        }

        private async void InitializeWebView(string profileName)
        {
            // Cancel active cache timer
            _disposeCts?.Cancel();
            _disposeCts = null;

            WebBrowserContainer.Children.Clear();
            
            // Reuse cached WebView2 if profile matches
            if (_cachedWebView != null && string.Equals(_cachedProfileName, profileName, StringComparison.OrdinalIgnoreCase))
            {
                _webView = _cachedWebView;
                _cachedWebView = null;
                _cachedProfileName = null;

                if (_webView.Parent is Panel p)
                {
                    p.Children.Remove(_webView);
                }

                WebBrowserContainer.Children.Add(_webView);
                _webView.NavigationStarting += OnWebViewNavigationStarting;
                _webView.NavigationCompleted += OnWebViewNavigationCompleted;

                if (_webView.CoreWebView2 != null)
                {
                    TxtWebUrl.Text = _webView.CoreWebView2.Source;
                }
                return;
            }

            // Dispose old mismatching WebView2
            if (_cachedWebView != null)
            {
                try { _cachedWebView.Dispose(); } catch { }
                _cachedWebView = null;
                _cachedProfileName = null;
            }
            if (_webView != null)
            {
                try { _webView.Dispose(); } catch { }
                _webView = null;
            }

            _webView = new Microsoft.Web.WebView2.Wpf.WebView2();
            WebBrowserContainer.Children.Add(_webView);

            try
            {
                Microsoft.Web.WebView2.Core.CoreWebView2Environment env;
                if (string.Equals(profileName, "Shared", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        env = await WebView2EnvironmentManager.GetSharedEnvironmentAsync();
                    }
                    catch (Exception exShared)
                    {
                        System.Diagnostics.Debug.WriteLine($"Shared env failed fallback in IconEditor: {exShared.Message}");
                        var cachePath = WebNodeCacheHelper.GetProfileCachePath("SharedFallback");
                        Directory.CreateDirectory(cachePath);
                        env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, cachePath, new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions());
                    }
                }
                else
                {
                    var cachePath = WebNodeCacheHelper.GetProfileCachePath(profileName);
                    Directory.CreateDirectory(cachePath);
                    env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, cachePath, new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions());
                }

                if (_webView == null) return;
                await _webView.EnsureCoreWebView2Async(env);

                // Bind events
                _webView.NavigationStarting += OnWebViewNavigationStarting;
                _webView.NavigationCompleted += OnWebViewNavigationCompleted;

                // Navigate to the last used URL
                string lastUrl = LoadLastUrl();
                TxtWebUrl.Text = lastUrl;
                _webView.CoreWebView2.Navigate(lastUrl);
            }
            catch (Exception ex)
            {
                WebBrowserContainer.Children.Clear();
                var errTb = new TextBlock
                {
                    Text = $"❌ Lỗi khởi tạo WebView2:\n{ex.Message}",
                    Foreground = Brushes.Tomato,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(16)
                };
                WebBrowserContainer.Children.Add(errTb);
            }
        }

        private void LoadWebProfiles()
        {
            CmbWebProfile.Items.Clear();
            var profiles = WebNodeCacheHelper.GetAvailableCacheProfiles();
            foreach (var p in profiles)
            {
                CmbWebProfile.Items.Add(new ComboBoxItem { Content = p, Tag = p });
            }

            for (int i = 0; i < CmbWebProfile.Items.Count; i++)
            {
                if (CmbWebProfile.Items[i] is ComboBoxItem item && string.Equals(item.Tag as string, _activeProfileName, StringComparison.OrdinalIgnoreCase))
                {
                    CmbWebProfile.SelectedIndex = i;
                    break;
                }
            }
            if (CmbWebProfile.SelectedIndex < 0 && CmbWebProfile.Items.Count > 0)
                CmbWebProfile.SelectedIndex = 0;
        }

        private void CmbWebProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbWebProfile.SelectedItem is ComboBoxItem item && item.Tag is string profileName)
            {
                if (_activeProfileName == profileName) return;
                _activeProfileName = profileName;
                InitializeWebView(profileName);
            }
        }

        private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Tạo Profile mới",
                Width = 320,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1c23"))
            };

            var sp = new StackPanel { Margin = new Thickness(16) };
            var lbl = new TextBlock { Text = "Tên profile:", Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 0, 0, 6) };
            var txt = new TextBox
            {
                Height = 28,
                FontSize = 12,
                Padding = new Thickness(6, 4, 6, 4),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1e222d")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2a2e3d")),
                CaretBrush = Brushes.Lime
            };
            var btnOk = new Button { Content = "Tạo", Width = 80, Height = 28, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.Hand };
            btnOk.Click += (s2, e2) => { dialog.DialogResult = true; dialog.Close(); };
            txt.KeyDown += (s2, e2) => { if (e2.Key == Key.Enter) { dialog.DialogResult = true; dialog.Close(); } };

            sp.Children.Add(lbl);
            sp.Children.Add(txt);
            sp.Children.Add(btnOk);
            dialog.Content = sp;

            if (dialog.ShowDialog() == true)
            {
                var name = txt.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(name) && !System.Text.RegularExpressions.Regex.IsMatch(name, @"[\\/:*?""<>|]"))
                {
                    var path = WebNodeCacheHelper.GetProfileCachePath(name);
                    Directory.CreateDirectory(path);
                    LoadWebProfiles();
                    for (int i = 0; i < CmbWebProfile.Items.Count; i++)
                    {
                        if (CmbWebProfile.Items[i] is ComboBoxItem ci && string.Equals(ci.Tag as string, name, StringComparison.OrdinalIgnoreCase))
                        {
                            CmbWebProfile.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }

        private void NavigateWebBrowser()
        {
            if (_webView?.CoreWebView2 == null) return;
            string input = TxtWebUrl.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            string url;
            if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                url = input;
            }
            else if (input.Contains('.') && !input.Contains(' '))
            {
                url = "https://" + input;
            }
            else
            {
                url = $"https://www.google.com/search?q={Uri.EscapeDataString(input)}";
            }

            TxtWebUrl.Text = url;
            _webView.CoreWebView2.Navigate(url);
        }

        private void BtnWebGo_Click(object sender, RoutedEventArgs e) => NavigateWebBrowser();
        private void TxtWebUrl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateWebBrowser();
                e.Handled = true;
            }
        }
        private void TxtWebUrl_GotFocus(object sender, RoutedEventArgs e) => TxtWebUrl.SelectAll();
        private void BtnWebBack_Click(object sender, RoutedEventArgs e) { if (_webView?.CoreWebView2 != null && _webView.CanGoBack) _webView.GoBack(); }
        private void BtnWebForward_Click(object sender, RoutedEventArgs e) { if (_webView?.CoreWebView2 != null && _webView.CanGoForward) _webView.GoForward(); }
        private void BtnWebRefresh_Click(object sender, RoutedEventArgs e) { if (_webView?.CoreWebView2 != null) _webView.Reload(); }
        #endregion

        #region Save SVG & Update Manifest
        private static string DetectProjectRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            for (int i = 0; i < 6; i++)
            {
                if (System.IO.Directory.GetFiles(dir, "*.csproj").Length > 0)
                    return dir;
                var parent = System.IO.Directory.GetParent(dir)?.FullName;
                if (parent == null) break;
                dir = parent;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private bool SaveIcon(out string savedKey)
        {
            savedKey = string.Empty;
            string name = TxtIconName.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(name) || Regex.IsMatch(name, @"[^a-z0-9\-_]"))
            {
                MessageBox.Show("Tên key icon không hợp lệ! (Chỉ chứa chữ thường, số, dấu gạch ngang, gạch dưới).", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Auto-determine folder based on color checkbox
            string folder = ChkOriginalColor.IsChecked == true ? "Custom-icons.color" : "Custom-icons";

            string textContent = TxtPathData.Text.Trim();
            if (string.IsNullOrEmpty(textContent))
            {
                MessageBox.Show("Nội dung SVG không được rỗng!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Determine SVG content: use full SVG if available, otherwise wrap path data
            string svgContent;
            if (textContent.StartsWith("<"))
            {
                // Full SVG markup - use as-is
                svgContent = textContent;
            }
            else
            {
                // Raw path data - wrap in SVG template
                string fill = ChkOriginalColor.IsChecked == true ? TxtIconColor.Text.Trim() : "currentColor";
                if (string.IsNullOrEmpty(fill)) fill = "#4fffb0";
                svgContent = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 512\">\n  <path fill=\"{fill}\" d=\"{textContent}\" />\n</svg>";
            }

            string relPath = $"Assets/{folder}/{name}.svg".Replace('\\', '/');
            string projectRoot = DetectProjectRoot();

            try
            {
                // 1. Write file to project source directory (persistent)
                string srcFilePath = System.IO.Path.Combine(projectRoot, relPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(srcFilePath)!);
                File.WriteAllText(srcFilePath, svgContent);

                // 2. Write file to base directory (immediate runtime load)
                string binFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(binFilePath)!);
                File.WriteAllText(binFilePath, svgContent);

                // 3. Update manifest in project source
                string key = $"{name} {folder}";
                savedKey = key;
                string manifestRelPath = "Assets/Icons/available_icons.txt".Replace('/', System.IO.Path.DirectorySeparatorChar);
                
                UpdateManifestFile(System.IO.Path.Combine(projectRoot, manifestRelPath), key, relPath);
                UpdateManifestFile(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, manifestRelPath), key, relPath);

                // 4. Reload IconResources Manifest
                IconResources.ReloadManifest();

                MessageBox.Show($"Lưu thành công biểu tượng '{key}'!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi lưu biểu tượng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (SaveIcon(out string savedKey))
            {
                // Refresh items
                LoadAvailableIcons();

                // Select newly saved icon in selector ComboBox
                for (int i = 0; i < CmbIconSelector.Items.Count; i++)
                {
                    if (CmbIconSelector.Items[i] is ComboBoxItem item && string.Equals(item.Tag as string, savedKey, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbIconSelector.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void BtnSaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            if (SaveIcon(out _))
            {
                Close();
            }
        }

        private static void UpdateManifestFile(string filePath, string key, string relativePath)
        {
            if (!File.Exists(filePath)) return;

            var lines = File.ReadAllLines(filePath).ToList();
            bool updated = false;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string lineKey = line.Substring(0, eq).Trim();
                if (string.Equals(lineKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"{key}={relativePath}";
                    updated = true;
                    break;
                }
            }

            if (!updated)
            {
                lines.Add($"{key}={relativePath}");
            }

            File.WriteAllLines(filePath, lines);
        }
        #endregion
    }
}
