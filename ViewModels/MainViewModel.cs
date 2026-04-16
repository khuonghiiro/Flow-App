using FlowMy.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace FlowMy.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        /// <summary>
        /// Command để mở WorkflowEditorWindow
        /// </summary>
        [RelayCommand]
        private void OpenWorkflowEditor()
        {
            if (App.Services == null) return;

            var mainWindow = Application.Current.MainWindow;
            
            var scope = App.Services.CreateScope();
            var workflowWindow = scope.ServiceProvider.GetService(typeof(WorkflowEditorWindow)) as WorkflowEditorWindow;
            if (workflowWindow != null)
            {
                workflowWindow.Owner = mainWindow;
                
                // Ẩn MainWindow khi mở WorkflowEditorWindow
                if (mainWindow != null)
                {
                    mainWindow.Hide();
                }
                
                // Hiện lại MainWindow khi WorkflowEditorWindow đóng
                workflowWindow.Closed += (_, __) =>
                {
                    scope.Dispose();
                    if (mainWindow != null)
                    {
                        mainWindow.Show();
                        mainWindow.Activate();
                    }
                };
                
                workflowWindow.Show();
            }
            else
            {
                scope.Dispose();
            }
        }
    }
}
