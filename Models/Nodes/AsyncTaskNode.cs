using System;
using System.Linq;
using FlowMy.Models.Nodes;

namespace FlowMy.Models
{
    /// <summary>
    /// Giao diện dialog / canvas: nhánh cố định vs giống Loop (body + dispatch theo N / mảng).
    /// </summary>
    public enum AsyncTaskUiPresentationMode
    {
        ManualBranches = 0,
        LoopLikeDispatch = 1
    }

    /// <summary>
    /// Node cho phép nhiều node logic chạy bất đồng bộ song song hoặc tuần tự.
    /// Mỗi task branch có một port out riêng (chế độ tay), hoặc một body chung (chế độ giống Loop).
    /// </summary>
    public class AsyncTaskNode : WorkflowNode
    {
        private bool _runInParallel = true;
        private AsyncTaskUiPresentationMode _uiPresentationMode = AsyncTaskUiPresentationMode.ManualBranches;
        private LoopType _dispatchLoopType = LoopType.RepeatN;
        private int _repeatCount = 5;
        private int _startIndex = 0;
        private int _endIndex = 10;

        public AsyncTaskNode()
        {
            Type = NodeType.AsyncTask;
            Title = "Async Task";
        }

        /// <summary>Property để kiểm tra nếu node là async task node (tương tự IsConditionalNode)</summary>
        public virtual bool IsAsyncTaskNode => true;

        /// <summary>
        /// Nếu true: chạy song song (parallel) — với ManualBranches: giữa các port; với LoopLike: giữa các vòng dispatch.
        /// </summary>
        public bool RunInParallel
        {
            get => _runInParallel;
            set => _runInParallel = value;
        }

        public AsyncTaskUiPresentationMode UiPresentationMode
        {
            get => _uiPresentationMode;
            set => _uiPresentationMode = value;
        }

        /// <summary>Kiểu đếm vòng khi <see cref="UiPresentationMode"/> = LoopLikeDispatch (giống Loop).</summary>
        public LoopType DispatchLoopType
        {
            get => _dispatchLoopType;
            set => _dispatchLoopType = value;
        }

        public int RepeatCount
        {
            get => _repeatCount;
            set => _repeatCount = value >= 1 ? value : 1;
        }

        public int StartIndex
        {
            get => _startIndex;
            set => _startIndex = value;
        }

        public int EndIndex
        {
            get => _endIndex;
            set => _endIndex = value;
        }

        /// <summary>Chỉ dùng khi <see cref="UiPresentationMode"/> = LoopLikeDispatch.</summary>
        public AsyncTaskBodyNode? AsyncTaskBodyNode { get; set; }

        /// <summary>
        /// Khi <see cref="UiPresentationMode"/> = LoopLikeDispatch:
        /// - true: chạy "kết nối sau loopOut" ngay trong từng iteration (đọc kết quả trong body).
        /// - false: chạy "kết nối sau loopOut" sau khi tất cả iterations xong.
        /// </summary>
        public bool ReadResultsInBody { get; set; } = true;

        public System.Windows.Controls.Border? ContainerBorder { get; set; }

        /// <summary>Wire mặc định AsyncTask bottom → body top (chế độ Loop-like).</summary>
        public WorkflowConnection? DefaultConnection { get; set; }

        /// <summary>Đảm bảo DynamicInputs/Outputs cho dispatch giống Loop (loopCount, loopArray, index, item).</summary>
        public void EnsureDispatchDynamicPorts()
        {
            if (DynamicInputs.All(i => !string.Equals(i.Key, "loopCount", StringComparison.OrdinalIgnoreCase)))
            {
                DynamicInputs.Add(new WorkflowDynamicDataPort
                {
                    Key = "loopCount",
                    DisplayName = "Số lần / N",
                    ConvertType = WorkflowDataType.Integer
                });
            }
            if (DynamicInputs.All(i => !string.Equals(i.Key, "loopArray", StringComparison.OrdinalIgnoreCase)))
            {
                DynamicInputs.Add(new WorkflowDynamicDataPort
                {
                    Key = "loopArray",
                    DisplayName = "Mảng dữ liệu",
                    ConvertType = WorkflowDataType.ArrayDynamic
                });
            }
            if (DynamicOutputs.All(o => !string.Equals(o.Key, "item", StringComparison.OrdinalIgnoreCase)))
            {
                DynamicOutputs.Add(new WorkflowDynamicDataPort
                {
                    Key = "item",
                    DisplayName = "Item",
                    IsMultiple = false,
                    OutputType = WorkflowDataType.String
                });
            }
            if (DynamicOutputs.All(o => !string.Equals(o.Key, "index", StringComparison.OrdinalIgnoreCase)))
            {
                DynamicOutputs.Add(new WorkflowDynamicDataPort
                {
                    Key = "index",
                    DisplayName = "Index",
                    IsMultiple = false,
                    OutputType = WorkflowDataType.Integer
                });
            }
        }
    }
}
