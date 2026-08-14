using FlowMy.Models;
using FlowMy.Services.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowMy.ViewModels
{
    public partial class OutputItemViewModel : ObservableObject
    {
        private readonly WorkflowNode _node;
        private readonly WorkflowDynamicDataPort _output;

        [ObservableProperty]
        private string _key;

        [ObservableProperty]
        private string _value;

        public OutputItemViewModel(WorkflowNode node, WorkflowDynamicDataPort output)
        {
            _node = node;
            _output = output;

            Key = output.Key ?? "—";
            Value = NodeChrome.GetOutputResolvedValue(node, output);
        }
    }

}
