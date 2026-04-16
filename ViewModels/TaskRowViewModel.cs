using FlowMy.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// Một dòng task trong dialog AsyncTask.
    /// Số thứ tự (TaskIndex) tương ứng với số hiển thị cạnh port out.
    /// </summary>
    public partial class TaskRowViewModel : ObservableObject
    {
        public AsyncTaskBranch Branch { get; }

        [ObservableProperty]
        private int _taskIndex;

        /// <summary>Tiêu đề: "Task 0", "Task 1", ... (số = port index).</summary>
        public string Title => $"Task {TaskIndex}";

        public bool CanRemove { get; }

        public TaskRowViewModel(AsyncTaskBranch branch, int taskIndex)
        {
            Branch = branch ?? throw new ArgumentNullException(nameof(branch));
            TaskIndex = taskIndex;
            CanRemove = branch.CanRemove;
        }
    }
}

