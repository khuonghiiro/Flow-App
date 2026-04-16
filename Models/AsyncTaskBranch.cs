using System;

namespace FlowMy.Models
{
    /// <summary>
    /// Class quản lý các task branch cho AsyncTaskNode.
    /// Mỗi branch đại diện cho một task chạy song song.
    /// </summary>
    public class AsyncTaskBranch
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Label { get; set; } = "Task"; // "Task", "Task 1", "Task 2", ...
        public NodePort? Port { get; set; }
        public bool CanRemove { get; set; } = true;
    }
}

