using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Helpers
{
    public static class StyleTableHelper
    {
        // Lấy các styles
        public static readonly Style? HeaderLeftStyle = Application.Current.FindResource("HeaderLeftStyle") as Style;
        public static readonly Style? HeaderCenterStyle = Application.Current.FindResource("HeaderCenterStyle") as Style;
        public static readonly Style? HeaderRightStyle = Application.Current.FindResource("HeaderRightStyle") as Style;

        public static readonly Style? CellLeftStyle = Application.Current.FindResource("CellLeftStyle") as Style;
        public static readonly Style? CellCenterStyle = Application.Current.FindResource("CellCenterStyle") as Style;
        public static readonly Style? CellRightStyle = Application.Current.FindResource("CellRightStyle") as Style;

        public static readonly Style? HeaderSortDefaultStyle = Application.Current.FindResource("BaseColumnHeaderStyle") as Style;

        public static readonly Style? HeaderSortLeftStyle = Application.Current.FindResource("SortHeaderLeftStyle") as Style;
        public static readonly Style? HeaderSortCenterStyle = Application.Current.FindResource("SortHeaderCenterStyle") as Style;
        public static readonly Style? HeaderSortRightStyle = Application.Current.FindResource("SortHeaderRightStyle") as Style;



        public static readonly Style CellWrapStyle = new Style(typeof(TextBlock))
        {
            Setters =
            {
                new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap),
                new Setter(TextBlock.TextTrimmingProperty, TextTrimming.None),
                new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Top), // đổi Center → Top
                new Setter(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)),
                new Setter(TextBlock.LineHeightProperty, 1.2),
                new Setter(TextBlock.MarginProperty, new Thickness(0, 2, 0, 2))
            }
        };


    }
}
