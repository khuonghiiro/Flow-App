using FlowMy.Services.Workflow;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;

namespace FlowMy.Views.Overlays
{
    public partial class WorkflowManagementDialog : Window
    {
        public ObservableCollection<WorkflowItem> Workflows { get; } = new();

        public WorkflowManagementDialog()
        {
            InitializeComponent();
            DataContext = this;
            LoadWorkflows();
        }

        private void LoadWorkflows()
        {
            Workflows.Clear();
            
            var workflowsDir = FileWorkflowPersistenceService.GetDefaultWorkflowsDirectory();
            if (!Directory.Exists(workflowsDir))
                return;

            var files = Directory.GetFiles(workflowsDir, "*.json")
                .OrderBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                Workflows.Add(new WorkflowItem
                {
                    Name = name,
                    FilePath = file
                });
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is WorkflowItem item)
            {
                var oldName = item.Name;
                var newName = Interaction.InputBox(
                    "Nhập tên mới cho workflow:",
                    "Sửa tên workflow",
                    oldName).Trim();

                if (string.IsNullOrWhiteSpace(newName) || newName == oldName)
                    return;

                // Sanitize file name
                foreach (var c in Path.GetInvalidFileNameChars())
                {
                    newName = newName.Replace(c, '_');
                }

                if (string.IsNullOrWhiteSpace(newName))
                    return;

                try
                {
                    var oldPath = item.FilePath;
                    var newPath = Path.Combine(Path.GetDirectoryName(oldPath)!, $"{newName}.json");

                    // Check if new name already exists
                    if (File.Exists(newPath) && !string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(
                            $"Workflow với tên '{newName}' đã tồn tại!",
                            "Lỗi",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    // Rename file
                    File.Move(oldPath, newPath);
                    
                    // Update item
                    item.Name = newName;
                    item.FilePath = newPath;

                    // Refresh list (sort lại)
                    var sorted = Workflows.OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    Workflows.Clear();
                    foreach (var w in sorted)
                    {
                        Workflows.Add(w);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Lỗi khi đổi tên workflow: {ex.Message}",
                        "Lỗi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is WorkflowItem item)
            {
                var result = MessageBox.Show(
                    $"Bạn có chắc chắn muốn xóa workflow '{item.Name}'?\n\nHành động này không thể hoàn tác!",
                    "Xác nhận xóa",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (File.Exists(item.FilePath))
                        {
                            File.Delete(item.FilePath);
                        }

                        Workflows.Remove(item);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Lỗi khi xóa workflow: {ex.Message}",
                            "Lỗi",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }

    public class WorkflowItem
    {
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }
}
