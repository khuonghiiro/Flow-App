using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowMy.Models.ListBoxs
{
    /// <summary>
    /// Model mẫu cho Radio List Item (optional)
    /// </summary>
    public class RadioListItem
    {
        public string DisplayText { get; set; }
        public string Description { get; set; }
        public string AdditionalInfo { get; set; }
        public object Tag { get; set; }
    }

    /// <summary>
    /// Generic RadioListItem
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RadioListItem<T> : RadioListItem
    {
        public T Value { get; set; }
    }
}
