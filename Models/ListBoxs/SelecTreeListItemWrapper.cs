using CommunityToolkit.Mvvm.ComponentModel;
using FlowMy.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace FlowMy.Models.ListBoxs
{
    public partial class SelecTreeListItemWrapper : ObservableObject
    {
        [ObservableProperty]
        private string id = string.Empty;

        [ObservableProperty]
        private string displayText = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private string additionalInfo = string.Empty;

        [ObservableProperty]
        private bool isSelected = false;

        // THÊM CÁC PROPERTIES MỚI CHO TREE:
        [ObservableProperty]
        private string parentId = string.Empty;

        [ObservableProperty]
        private int level = 0;

        [ObservableProperty]
        private bool hasChildren = false;

        [ObservableProperty]
        private bool isExpanded = true;

        [ObservableProperty]
        private ObservableCollection<SelecTreeListItemWrapper> children = new();

        public object OriginalObject { get; set; }

        public SolidColorBrush NodeTypeColor => HasChildren ?
       new SolidColorBrush(Color.FromRgb(59, 130, 246)) : // Blue for folders
       new SolidColorBrush(Color.FromRgb(34, 197, 94));   // Green for files

        public string NodeTypeIcon => HasChildren ?
            "M7,3A4,4 0 0,1 11,7C11,8.86 9.73,10.43 8,10.87V13.13C8.37,13.22 8.72,13.37 9.04,13.56L13.56,9.04C13.2,8.44 13,7.75 13,7A4,4 0 0,1 17,3A4,4 0 0,1 21,7A4,4 0 0,1 17,11C16.26,11 15.57,10.8 15,10.45L10.45,15C10.8,15.57 11,16.26 11,17A4,4 0 0,1 7,21A4,4 0 0,1 3,17C3,15.14 4.27,13.57 6,13.13V10.87C4.27,10.43 3,8.86 3,7A4,4 0 0,1 7,3M17,13A4,4 0 0,1 21,17A4,4 0 0,1 17,21A4,4 0 0,1 13,17A4,4 0 0,1 17,13M17,15A2,2 0 0,0 15,17A2,2 0 0,0 17,19A2,2 0 0,0 19,17A2,2 0 0,0 17,15Z" : // Nút cha
            "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2Z"; // icon nút con

        public SolidColorBrush TextColor => IsSelected ?
            new SolidColorBrush(Color.FromRgb(59, 130, 246)) :
             DynamicColorHelper.GetResourceBrush("TextSecondary", Color.FromRgb(211, 211, 211));

        // Tree path for drawing connection lines
        public List<bool> TreePath
        {
            get
            {
                var path = new List<bool>();
                for (int i = 0; i < Level; i++)
                {
                    path.Add(true);
                }
                return path;
            }
        }
    }
}
