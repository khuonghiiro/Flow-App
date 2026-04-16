using FlowMy.Models;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Collect outputs across AsyncTask parallel dispatch iterations (executionId pattern: {parentExecId}:dispatch-{index}).
    /// Used when AsyncTask is configured to "sau loopOut" (read results after loopOut) in a parallel-safe way.
    /// </summary>
    public sealed class AsyncTaskDispatchCollectNode : WorkflowNode, INotifyPropertyChanged
    {
        private string? _sourceBodyNodeId;
        private string? _sourceOutputKey;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public AsyncTaskDispatchCollectNode()
        {
            Type = NodeType.AsyncTaskDispatchCollect;
            Title = "Collect AsyncTask Results";
            ColorKey = "SkyAzure";

            // Flow: input (left) -> output (right)
            Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });
            Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"
            });

            // Data output: JSON array of collected string results
            DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "results",
                DisplayName = "results",
                OutputType = WorkflowDataType.String,
                ConvertType = WorkflowDataType.String,
                IsMultiple = false,
                IsUserAdded = false
            });
        }

        /// <summary>
        /// The node inside AsyncTask body whose output values will be collected.
        /// </summary>
        public string? SourceBodyNodeId
        {
            get => _sourceBodyNodeId;
            set
            {
                if (string.Equals(_sourceBodyNodeId, value, StringComparison.OrdinalIgnoreCase)) return;
                _sourceBodyNodeId = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The output key of <see cref="SourceBodyNodeId"/> to collect for each dispatch iteration.
        /// </summary>
        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set
            {
                if (string.Equals(_sourceOutputKey, value, StringComparison.OrdinalIgnoreCase)) return;
                _sourceOutputKey = value;
                OnPropertyChanged();
            }
        }
    }
}

