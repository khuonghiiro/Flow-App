using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowMy.Models.ListBoxs
{
    public class LoadDataResponse<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public bool HasMore { get; set; }
        public int CurrentPage { get; set; }
    }

}
