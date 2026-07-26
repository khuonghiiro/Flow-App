using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Configuration for output mapping from inner nodes inside AsyncTaskBody to dynamic output keys on AsyncTaskNode.
    /// </summary>
    public class AsyncTaskBodyOutputMapping : INotifyPropertyChanged
    {
        private string? _sourceNodeId;
        private string? _sourceOutputKey;
        private string? _outputKey;
        private bool _isCollectArray = true;

        /// <summary>
        /// ID of the source node located inside the AsyncTaskBody.
        /// </summary>
        public string? SourceNodeId
        {
            get => _sourceNodeId;
            set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Output key name from the source node to read.
        /// </summary>
        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Dynamic output key name exposed on the parent AsyncTaskNode.
        /// </summary>
        public string? OutputKey
        {
            get => _outputKey;
            set { if (_outputKey != value) { _outputKey = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// If true ("Gom Item"): Aggregates output items across all iterations into a JSON array list ["item0", "item1", ...].
        /// If false: Pushes single per-iteration value as it completes.
        /// </summary>
        public bool IsCollectArray
        {
            get => _isCollectArray;
            set { if (_isCollectArray != value) { _isCollectArray = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
