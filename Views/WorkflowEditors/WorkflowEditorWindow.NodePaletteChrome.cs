using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Media;
using FlowMy.Properties;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private const double NodePaletteSplitterFixedWidth = 5;
        private const double NodePaletteMinOpenTotal = 155;
        private const double NodePaletteMaxOpenTotal = 405;
        /// <summary>Khi tổng rộng cột palette + splitter dưới ngưỡng này thì coi là đã đóng: hiện nút mở (tab trái).</summary>
        private const double NodePaletteExpandShowBelowTotal = 56;
        private static readonly TimeSpan NodePaletteAnimDuration = TimeSpan.FromMilliseconds(300);

        private DispatcherTimer? _nodePaletteAnimTimer;
        private double _nodePaletteAnimFrom;
        private double _nodePaletteAnimTo;
        private DateTime _nodePaletteAnimStartUtc;
        private Action? _nodePaletteAnimCompleted;

        private double _nodePaletteStoredOpenTotalWidth = 205;
        private bool _suppressExpandDuringOpenAnimation;

        private void InitializeNodePaletteFromSettings()
        {
            try
            {
                StopNodePaletteWidthAnimation(applyFinal: false);

                if (!Settings.Default.WorkflowEditorNodePaletteOpen)
                {
                    ApplyPaletteTotalWidth(0, isDuringAnimation: false);
                    PlayExpandButtonEntrance();
                }
                else
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                    {
                        try
                        {
                            var w = ReadCurrentPaletteTotalWidth();
                            if (w >= NodePaletteMinOpenTotal)
                                _nodePaletteStoredOpenTotalWidth = Math.Clamp(w, NodePaletteMinOpenTotal, NodePaletteMaxOpenTotal);
                        }
                        catch { }
                    }));
                }

                if (LeftMenuBorder != null)
                    LeftMenuBorder.SizeChanged -= LeftMenuBorder_OnSizeChanged;
                if (LeftMenuBorder != null)
                    LeftMenuBorder.SizeChanged += LeftMenuBorder_OnSizeChanged;
            }
            catch { }
        }

        private void LeftMenuBorder_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_nodePaletteAnimTimer != null || _isViewportExpandedUiHidden)
                return;
            if (!Settings.Default.WorkflowEditorNodePaletteOpen)
                return;
            var t = ReadCurrentPaletteTotalWidth();
            if (t >= NodePaletteMinOpenTotal)
                _nodePaletteStoredOpenTotalWidth = Math.Clamp(t, NodePaletteMinOpenTotal, NodePaletteMaxOpenTotal);
        }

        private void StopNodePaletteWidthAnimation(bool applyFinal)
        {
            if (_nodePaletteAnimTimer != null)
            {
                _nodePaletteAnimTimer.Stop();
                _nodePaletteAnimTimer.Tick -= NodePaletteAnimTimer_Tick;
                _nodePaletteAnimTimer = null;
            }

            if (applyFinal)
                ApplyPaletteTotalWidth(_nodePaletteAnimTo, isDuringAnimation: false);

            _nodePaletteAnimCompleted = null;
        }

        private void StartPaletteWidthAnimation(double from, double to, Action? onCompleted)
        {
            StopNodePaletteWidthAnimation(applyFinal: false);
            _nodePaletteAnimFrom = from;
            _nodePaletteAnimTo = to;
            _nodePaletteAnimCompleted = onCompleted;
            _nodePaletteAnimStartUtc = DateTime.UtcNow;
            _nodePaletteAnimTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _nodePaletteAnimTimer.Tick += NodePaletteAnimTimer_Tick;
            _nodePaletteAnimTimer.Start();
        }

        private void NodePaletteAnimTimer_Tick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.UtcNow - _nodePaletteAnimStartUtc;
            var t = Math.Min(1, elapsed / NodePaletteAnimDuration);
            var eased = 1 - Math.Pow(1 - t, 3);
            var v = _nodePaletteAnimFrom + (_nodePaletteAnimTo - _nodePaletteAnimFrom) * eased;
            ApplyPaletteTotalWidth(v, isDuringAnimation: true);
            if (t < 1)
                return;

            var done = _nodePaletteAnimCompleted;
            StopNodePaletteWidthAnimation(applyFinal: true);
            done?.Invoke();
        }

        private double ReadCurrentPaletteTotalWidth()
        {
            if (LeftMenuColumn == null || LeftSplitterColumn == null)
                return _nodePaletteStoredOpenTotalWidth;

            var sum = LeftMenuColumn.ActualWidth + LeftSplitterColumn.ActualWidth;
            if (sum > 1)
                return sum;

            var left = LeftMenuColumn.Width.IsAbsolute ? LeftMenuColumn.Width.Value : 0;
            var sp = LeftSplitterColumn.Width.IsAbsolute ? LeftSplitterColumn.Width.Value : 0;
            return left + sp;
        }

        private void ApplyPaletteTotalWidth(double total, bool isDuringAnimation)
        {
            if (_isViewportExpandedUiHidden)
                return;
            if (LeftMenuColumn == null || LeftSplitterColumn == null || LeftPaletteGridSplitter == null)
                return;

            total = Math.Max(0, total);
            var splitW = total <= 0.5
                ? 0
                : (total >= NodePaletteSplitterFixedWidth ? NodePaletteSplitterFixedWidth : total);
            var leftW = Math.Max(0, total - splitW);
            var splitVisible = splitW > 0.5;

            LeftMenuColumn.Width = new GridLength(leftW);
            if (!splitVisible)
            {
                LeftMenuColumn.MinWidth = 0;
                LeftMenuColumn.MaxWidth = 0;
            }
            else if (isDuringAnimation)
            {
                LeftMenuColumn.MinWidth = 0;
                LeftMenuColumn.MaxWidth = 1000;
            }
            else
            {
                LeftMenuColumn.MinWidth = 150;
                LeftMenuColumn.MaxWidth = 400;
            }

            LeftSplitterColumn.Width = new GridLength(splitW);
            LeftSplitterColumn.MinWidth = splitVisible ? NodePaletteSplitterFixedWidth : 0;
            LeftSplitterColumn.MaxWidth = splitVisible ? NodePaletteSplitterFixedWidth : 0;

            LeftPaletteGridSplitter.Visibility = splitVisible ? Visibility.Visible : Visibility.Collapsed;

            if (LeftMenuBorder != null)
                LeftMenuBorder.Visibility = leftW > 0.5 ? Visibility.Visible : Visibility.Collapsed;

            if (!isDuringAnimation && splitVisible && total >= NodePaletteMinOpenTotal)
                _nodePaletteStoredOpenTotalWidth = Math.Clamp(total, NodePaletteMinOpenTotal, NodePaletteMaxOpenTotal);

            UpdateNodePaletteToggleChrome(isDuringAnimation, total);
        }

        private void UpdateNodePaletteToggleChrome(bool isDuringAnimation, double totalWidth)
        {
            if (NodePaletteExpandButton == null || NodePaletteCollapseButton == null)
                return;

            if (_isViewportExpandedUiHidden)
            {
                NodePaletteExpandButton.Visibility = Visibility.Collapsed;
                NodePaletteCollapseButton.Visibility = Visibility.Collapsed;
                return;
            }

            var closed = totalWidth < NodePaletteExpandShowBelowTotal && !_suppressExpandDuringOpenAnimation;
            NodePaletteExpandButton.Visibility = closed ? Visibility.Visible : Visibility.Collapsed;
            if (closed)
            {
                NodePaletteExpandButton.BeginAnimation(UIElement.OpacityProperty, null);
                if (NodePaletteExpandButton.Opacity < 0.05)
                    NodePaletteExpandButton.Opacity = 1;
            }

            var showCollapse = !closed && totalWidth > 56 && !isDuringAnimation;
            NodePaletteCollapseButton.Visibility = showCollapse ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PlayExpandButtonEntrance()
        {
            if (NodePaletteExpandButton == null || NodePaletteExpandButtonSlide == null)
                return;
            if (_isViewportExpandedUiHidden)
                return;
            if (NodePaletteExpandButton.Visibility != Visibility.Visible)
                return;

            NodePaletteExpandButton.BeginAnimation(UIElement.OpacityProperty, null);
            NodePaletteExpandButton.Opacity = 0;
            NodePaletteExpandButtonSlide.X = -16;

            var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            var slideAnim = new DoubleAnimation(-16, 0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            };

            var sb = new Storyboard();
            Storyboard.SetTarget(opacityAnim, NodePaletteExpandButton);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));
            Storyboard.SetTarget(slideAnim, NodePaletteExpandButtonSlide);
            Storyboard.SetTargetProperty(slideAnim, new PropertyPath("X"));
            sb.Children.Add(opacityAnim);
            sb.Children.Add(slideAnim);
            sb.Completed += (_, __) =>
            {
                NodePaletteExpandButton.BeginAnimation(UIElement.OpacityProperty, null);
                NodePaletteExpandButton.Opacity = 1;
                NodePaletteExpandButtonSlide.BeginAnimation(TranslateTransform.XProperty, null);
                NodePaletteExpandButtonSlide.X = 0;
            };
            sb.Begin();
        }

        private void NodePaletteCollapseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isViewportExpandedUiHidden)
                return;

            // User bấm tay thu menu trái => clear flag auto-collapse của trace dock,
            // để không bị tự bung lại khi đổi dock mode sau này.
            _traceDockAutoCollapsedLeftMenu = false;

            var from = ReadCurrentPaletteTotalWidth();
            if (from < NodePaletteMinOpenTotal)
                from = _nodePaletteStoredOpenTotalWidth;
            _nodePaletteStoredOpenTotalWidth = Math.Clamp(from, NodePaletteMinOpenTotal, NodePaletteMaxOpenTotal);

            StartPaletteWidthAnimation(from, 0, () =>
            {
                Settings.Default.WorkflowEditorNodePaletteOpen = false;
                Settings.Default.Save();
                PlayExpandButtonEntrance();
            });
        }

        private void NodePaletteExpandButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isViewportExpandedUiHidden)
                return;

            // User bấm tay mở menu trái => clear flag auto-collapse của trace dock,
            // tránh việc khi chuyển sang mode khác lại lặp bung thêm lần nữa.
            _traceDockAutoCollapsedLeftMenu = false;

            _suppressExpandDuringOpenAnimation = true;
            NodePaletteExpandButton.BeginAnimation(UIElement.OpacityProperty, null);
            NodePaletteExpandButton.Opacity = 1;
            NodePaletteExpandButton.Visibility = Visibility.Collapsed;

            var target = Math.Clamp(_nodePaletteStoredOpenTotalWidth, NodePaletteMinOpenTotal, NodePaletteMaxOpenTotal);
            StartPaletteWidthAnimation(0, target, () =>
            {
                _suppressExpandDuringOpenAnimation = false;
                Settings.Default.WorkflowEditorNodePaletteOpen = true;
                Settings.Default.Save();
            });
        }

        /// <summary>
        /// Đồng bộ trạng thái menu trái theo dock mode của panel Execution Log.
        /// - Khi user chuyển panel sang dock Left: nếu menu trái đang mở, tự thu gọn (remember = true)
        ///   để tránh tranh chấp vùng hiển thị (menu + panel log cùng bên trái).
        /// - Khi user chuyển sang Bottom/Right/Detached: nếu trước đó ta đã auto-collapse, tự mở lại.
        /// - Nếu menu trái vốn đã đóng sẵn (Settings=false) thì không đụng tới: giữ nguyên ý muốn của user.
        /// </summary>
        private void SyncLeftMenuForExecutionTraceDockMode()
        {
            if (ViewModel == null) return;
            if (_isViewportExpandedUiHidden) return;

            // Panel log phải thực sự ở chế độ dock-Left và đang hiện mới coi là tranh vùng.
            bool panelLeftActive = ViewModel.IsTraceDockLeft
                                   && ViewModel.IsExecutionTracePanelExpanded
                                   && ViewModel.EnableExecutionTraceLog;

            if (panelLeftActive)
            {
                // Chỉ auto-thu nếu menu đang mở. Width > ngưỡng mới được coi là "đang mở".
                bool menuOpen = Settings.Default.WorkflowEditorNodePaletteOpen
                                && ReadCurrentPaletteTotalWidth() > NodePaletteExpandShowBelowTotal;
                if (!menuOpen) return;
                if (_traceDockAutoCollapsedLeftMenu) return; // đã auto-thu rồi

                _traceDockAutoCollapsedLeftMenu = true;

                var from = ReadCurrentPaletteTotalWidth();
                if (from < NodePaletteMinOpenTotal)
                    from = _nodePaletteStoredOpenTotalWidth;
                _nodePaletteStoredOpenTotalWidth = Math.Clamp(from, NodePaletteMinOpenTotal, NodePaletteMaxOpenTotal);

                // KHÔNG đổi Settings.WorkflowEditorNodePaletteOpen: giữ intent "user muốn mở"
                // để khi panel log rời dock Left ta bung lại đúng trạng thái cũ.
                StartPaletteWidthAnimation(from, 0, onCompleted: null);
                PlayExpandButtonEntrance();
                return;
            }

            // Không còn ở dock Left. Nếu trước đó ta auto-thu, thì tự bung lại.
            if (!_traceDockAutoCollapsedLeftMenu) return;
            _traceDockAutoCollapsedLeftMenu = false;

            // Chỉ bung nếu user vẫn còn muốn menu trái mở (Settings intent = true).
            if (!Settings.Default.WorkflowEditorNodePaletteOpen) return;

            _suppressExpandDuringOpenAnimation = true;
            NodePaletteExpandButton.BeginAnimation(UIElement.OpacityProperty, null);
            NodePaletteExpandButton.Opacity = 1;
            NodePaletteExpandButton.Visibility = Visibility.Collapsed;

            var target = Math.Clamp(_nodePaletteStoredOpenTotalWidth, NodePaletteMinOpenTotal, NodePaletteMaxOpenTotal);
            StartPaletteWidthAnimation(0, target, () =>
            {
                _suppressExpandDuringOpenAnimation = false;
            });
        }
    }
}
