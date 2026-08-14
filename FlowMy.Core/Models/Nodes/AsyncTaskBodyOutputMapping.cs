// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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
        private System.Collections.Generic.List<AsyncTaskBodySourceItem> _sources = new();

        /// <summary>
        /// Danh sách các cặp nguồn (Node, Key) gộp chung vào OutputKey này.
        /// </summary>
        public System.Collections.Generic.List<AsyncTaskBodySourceItem> Sources
        {
            get => _sources;
            set { _sources = value ?? new(); OnPropertyChanged(); }
        }

        /// <summary>
        /// ID of the primary source node located inside the AsyncTaskBody.
        /// </summary>
        public string? SourceNodeId
        {
            get
            {
                if (_sources.Count > 0 && !string.IsNullOrWhiteSpace(_sources[0].SourceNodeId))
                    return _sources[0].SourceNodeId;
                return _sourceNodeId;
            }
            set
            {
                _sourceNodeId = value;
                if (_sources.Count == 0)
                    _sources.Add(new AsyncTaskBodySourceItem { SourceNodeId = value, SourceOutputKey = _sourceOutputKey });
                else
                    _sources[0].SourceNodeId = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Output key name from the primary source node to read.
        /// </summary>
        public string? SourceOutputKey
        {
            get
            {
                if (_sources.Count > 0 && !string.IsNullOrWhiteSpace(_sources[0].SourceOutputKey))
                    return _sources[0].SourceOutputKey;
                return _sourceOutputKey;
            }
            set
            {
                _sourceOutputKey = value;
                if (_sources.Count == 0)
                    _sources.Add(new AsyncTaskBodySourceItem { SourceNodeId = _sourceNodeId, SourceOutputKey = value });
                else
                    _sources[0].SourceOutputKey = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Lấy tất cả các cặp nguồn đã cấu hình (nếu Sources rỗng thì trả về từ SourceNodeId/SourceOutputKey).
        /// </summary>
        public System.Collections.Generic.IEnumerable<AsyncTaskBodySourceItem> GetEffectiveSources()
        {
            if (_sources != null && _sources.Count > 0)
            {
                foreach (var s in _sources)
                {
                    if (s != null && (!string.IsNullOrWhiteSpace(s.SourceNodeId) || !string.IsNullOrWhiteSpace(s.SourceOutputKey)))
                        yield return s;
                }
            }
            else if (!string.IsNullOrWhiteSpace(SourceNodeId) || !string.IsNullOrWhiteSpace(SourceOutputKey))
            {
                yield return new AsyncTaskBodySourceItem { SourceNodeId = SourceNodeId, SourceOutputKey = SourceOutputKey };
            }
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
