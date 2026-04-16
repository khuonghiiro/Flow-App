using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace FlowMy.Services.Interfaces
{
    public interface IViewFactoryService
    {
        UserControl? CreateView(string path);
        object? CreateViewModel(string path);
        (UserControl? view, object? viewModel) CreateViewWithViewModel(string path);
        bool IsValidPath(string path);
        IEnumerable<string> GetAvailablePaths();
    }
}
