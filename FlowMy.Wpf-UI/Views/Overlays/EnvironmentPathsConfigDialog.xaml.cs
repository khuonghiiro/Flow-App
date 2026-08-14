// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.ViewModels;
using Microsoft.Win32;
using System.Windows;

namespace FlowMy.Views.Overlays
{
    public partial class EnvironmentPathsConfigDialog : Window
    {
        private readonly EnvironmentPathsConfigDialogViewModel _viewModel;

        public EnvironmentPathsConfigDialog(Window? owner = null)
        {
            InitializeComponent();
            if (owner != null) Owner = owner;

            _viewModel = new EnvironmentPathsConfigDialogViewModel();
            DataContext = _viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SavePreferences();
            MessageBox.Show("✅ Đã lưu cấu hình đường dẫn hệ thống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void BrowseFfmpegFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file ffmpeg.exe",
                Filter = "FFmpeg Executable (ffmpeg.exe)|ffmpeg.exe|Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.FfmpegPath = dialog.FileName;
            }
        }

        private void BrowseFfmpegFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Chọn thư mục chứa ffmpeg.exe & ffprobe.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.FfmpegPath = dialog.FolderName;
            }
        }

        private void BrowseFfprobeFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file ffprobe.exe",
                Filter = "FFprobe Executable (ffprobe.exe)|ffprobe.exe|Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.FfprobePath = dialog.FileName;
            }
        }

        private void BrowseFfplayFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file ffplay.exe",
                Filter = "FFplay Executable (ffplay.exe)|ffplay.exe|Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.FfplayPath = dialog.FileName;
            }
        }

        private void BrowseGitFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file git.exe",
                Filter = "Git Executable (git.exe)|git.exe|Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.GitPath = dialog.FileName;
            }
        }

        private void BrowsePythonFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn file python.exe",
                Filter = "Python Executable (python.exe)|python.exe|Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.PythonPath = dialog.FileName;
            }
        }

        private void BrowseCustomFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Chọn thư mục chứa các công cụ/binaries bổ trợ"
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.CustomBinariesPath = dialog.FolderName;
            }
        }
    }
}
