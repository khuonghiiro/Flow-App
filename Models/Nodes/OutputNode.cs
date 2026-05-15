using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Represents a single input variable configuration for OutputNode.
    /// Maps a variable name to a source node output key.
    /// </summary>
    public class InputVariable : INotifyPropertyChanged
    {
        private string _variableKey = string.Empty;
        private string _sourceNodeId = string.Empty;
        private string _sourceOutputKey = string.Empty;

        /// <summary>
        /// The variable key name (e.g., "input1", "input2").
        /// </summary>
        public string VariableKey
        {
            get => _variableKey;
            set
            {
                if (_variableKey != value)
                {
                    _variableKey = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The ID of the source node to get value from.
        /// </summary>
        public string SourceNodeId
        {
            get => _sourceNodeId;
            set
            {
                if (_sourceNodeId != value)
                {
                    _sourceNodeId = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The output key from the source node.
        /// </summary>
        public string SourceOutputKey
        {
            get => _sourceOutputKey;
            set
            {
                if (_sourceOutputKey != value)
                {
                    _sourceOutputKey = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Node để tạo output text từ format string và các biến input.
    /// Ví dụ: FormatString = "{input1} abc {input2}" sẽ tạo chuỗi từ các biến input1, input2.
    /// </summary>
    public sealed class OutputNode : WorkflowNode
    {
        private string _outputKey = "output";
        private List<InputVariable> _inputVariables = new();
        private string _formatString = string.Empty;
        private string _outputText = string.Empty;

        [System.Text.Json.Serialization.JsonIgnore]
        private readonly object _parallelDispatchAccumulateGate = new object();

        [System.Text.Json.Serialization.JsonIgnore]
        private Dictionary<int, string>? _parallelDispatchByIndex;

        /// <summary>Chỉ dùng khi không parse được chỉ số dispatch (hiếm).</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        private List<string>? _parallelDispatchCompletionOrder;

        public OutputNode()
        {
            Type = NodeType.Output;
            Title = "Output";

            // Initialize with one empty input variable
            _inputVariables.Add(new InputVariable
            {
                VariableKey = "input1",
                SourceNodeId = string.Empty,
                SourceOutputKey = string.Empty
            });

            // Rebuild DynamicOutputs
            RebuildDynamicOutputs();
        }

        /// <summary>
        /// Key của output (chỉ có 1 output với value là text).
        /// </summary>
        public string OutputKey
        {
            get => _outputKey;
            set
            {
                if (_outputKey != value)
                {
                    _outputKey = value;
                    OnPropertyChanged();
                    RebuildDynamicOutputs();
                }
            }
        }

        /// <summary>
        /// List of input variables - each defines a variable mapping from source node.
        /// </summary>
        public List<InputVariable> InputVariables
        {
            get => _inputVariables;
            set
            {
                if (_inputVariables != value)
                {
                    _inputVariables = value ?? new List<InputVariable>();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Format string để tạo output text.
        /// Ví dụ: "{input1} abc {input2}" hoặc "{input1[0...n]}" cho mảng.
        /// </summary>
        public string FormatString
        {
            get => _formatString;
            set
            {
                if (_formatString != value)
                {
                    _formatString = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Output text được tạo từ FormatString và các biến input (runtime value).
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string OutputText
        {
            get => _outputText;
            set
            {
                if (_outputText != value)
                {
                    _outputText = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Rebuild DynamicOutputs từ OutputKey.
        /// </summary>
        public void RebuildDynamicOutputs()
        {
            DynamicOutputs.Clear();

            if (!string.IsNullOrWhiteSpace(_outputKey))
            {
                DynamicOutputs.Add(new WorkflowDynamicDataPort
                {
                    Key = _outputKey,
                    DisplayName = _outputKey,
                    OutputType = WorkflowDataType.String,
                    ConvertType = WorkflowDataType.String,
                    IsUserAdded = true
                });
            }
        }

        /// <summary>
        /// Add a new input variable.
        /// </summary>
        public void AddInputVariable(string variableKey, string sourceNodeId, string sourceOutputKey)
        {
            _inputVariables.Add(new InputVariable
            {
                VariableKey = variableKey,
                SourceNodeId = sourceNodeId,
                SourceOutputKey = sourceOutputKey
            });
            OnPropertyChanged(nameof(InputVariables));
        }

        /// <summary>
        /// Remove an input variable at specified index.
        /// </summary>
        public void RemoveInputVariable(int index)
        {
            if (index >= 0 && index < _inputVariables.Count)
            {
                _inputVariables.RemoveAt(index);
                OnPropertyChanged(nameof(InputVariables));
            }
        }

        /// <summary>
        /// Clear all input variables.
        /// </summary>
        public void ClearInputVariables()
        {
            _inputVariables.Clear();
            OnPropertyChanged(nameof(InputVariables));
        }

        /// <summary>Gọi khi bắt đầu đợt AsyncTask loop-dispatch để node Output tích lũy nhiều dòng (song song/tuần tự).</summary>
        public void ResetParallelDispatchOutputAccumulation()
        {
            lock (_parallelDispatchAccumulateGate)
            {
                _parallelDispatchByIndex = null;
                _parallelDispatchCompletionOrder = null;
            }
        }

        /// <summary>
        /// Ghi kết quả một vòng dispatch. Nếu có <paramref name="dispatchIndex"/> thì hiển thị theo thứ tự 0..n
        /// (không phụ thuộc HTTP nhanh/chậm hoàn thành trước).
        /// </summary>
        internal void AppendParallelDispatchOutputLine(int? dispatchIndex, string line)
        {
            lock (_parallelDispatchAccumulateGate)
            {
                var text = line ?? string.Empty;
                if (dispatchIndex.HasValue && dispatchIndex.Value >= 0)
                {
                    _parallelDispatchByIndex ??= new Dictionary<int, string>();
                    _parallelDispatchByIndex[dispatchIndex.Value] = text;
                }
                else
                {
                    _parallelDispatchCompletionOrder ??= new List<string>();
                    _parallelDispatchCompletionOrder.Add(text);
                }

                RebuildParallelDispatchOutputText();
            }
        }

        private void RebuildParallelDispatchOutputText()
        {
            var parts = new List<string>();
            if (_parallelDispatchByIndex != null && _parallelDispatchByIndex.Count > 0)
            {
                foreach (var kv in _parallelDispatchByIndex.OrderBy(k => k.Key))
                    parts.Add($"{kv.Key}: {kv.Value}");
            }

            if (_parallelDispatchCompletionOrder != null && _parallelDispatchCompletionOrder.Count > 0)
                parts.AddRange(_parallelDispatchCompletionOrder);

            OutputText = string.Join(Environment.NewLine, parts);
        }
    }
}
