using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Represents a single source node + output key pair for an AsyncTaskBody output mapping.
    /// </summary>
    public class AsyncTaskBodySourceItem : INotifyPropertyChanged
    {
        private string? _sourceNodeId;
        private string? _sourceOutputKey;

        public string? SourceNodeId
        {
            get => _sourceNodeId;
            set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); } }
        }

        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
