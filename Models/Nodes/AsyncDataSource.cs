using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Cấu hình 1 nguồn dữ liệu async push vào HTML UI.
    /// SourceNodeId + SourceOutputKey → resolve value từ node nguồn (ví dụ: AsyncTask branch output).
    /// ReceiverKey → tên key khi push vào JS (window.__acAsync.data[ReceiverKey]).
    /// Nếu ReceiverKey trống → dùng tên SourceOutputKey của node nguồn.
    /// </summary>
    public sealed class AsyncDataSource : INotifyPropertyChanged
    {
        private string? _sourceNodeId;
        private string? _sourceOutputKey;
        private string? _receiverKey;

        /// <summary>Id của node nguồn (ví dụ: AsyncTaskNode hoặc bất kỳ node nào có DynamicOutputs).</summary>
        public string? SourceNodeId
        {
            get => _sourceNodeId;
            set { if (_sourceNodeId != value) { _sourceNodeId = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveKey)); } }
        }

        /// <summary>Output key của node nguồn để lấy giá trị.</summary>
        public string? SourceOutputKey
        {
            get => _sourceOutputKey;
            set { if (_sourceOutputKey != value) { _sourceOutputKey = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveKey)); } }
        }

        /// <summary>
        /// Tên key mới nhập bởi user — dùng làm key khi push vào JS.
        /// Nếu trống → dùng SourceOutputKey.
        /// </summary>
        public string? ReceiverKey
        {
            get => _receiverKey;
            set { if (_receiverKey != value) { _receiverKey = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveKey)); } }
        }

        /// <summary>Key thực tế dùng trong JS: ReceiverKey > SourceOutputKey > "asyncData".</summary>
        public string EffectiveKey =>
            !string.IsNullOrWhiteSpace(_receiverKey) ? _receiverKey!.Trim()
            : !string.IsNullOrWhiteSpace(_sourceOutputKey) ? _sourceOutputKey!.Trim()
            : "asyncData";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
