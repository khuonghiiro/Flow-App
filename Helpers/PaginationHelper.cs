using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowMy.Helpers
{
    public class PageNumberInfo
    {
        public int Number { get; set; }
        public bool IsActive { get; set; }
    }

    public static class PaginationHelper
    {
        public static ObservableCollection<PageNumberInfo> GeneratePageNumbers(int currentPage, int totalPages, int maxVisible = 5)
        {
            var pageNumbers = new ObservableCollection<PageNumberInfo>();

            if (totalPages <= 1) return pageNumbers;

            int startPage = Math.Max(1, currentPage - maxVisible / 2);
            int endPage = Math.Min(totalPages, startPage + maxVisible - 1);

            // Adjust start page if we're near the end
            if (endPage - startPage < maxVisible - 1)
            {
                startPage = Math.Max(1, endPage - maxVisible + 1);
            }

            for (int i = startPage; i <= endPage; i++)
            {
                pageNumbers.Add(new PageNumberInfo
                {
                    Number = i,
                    IsActive = i == currentPage
                });
            }

            return pageNumbers;
        }
    }
}
