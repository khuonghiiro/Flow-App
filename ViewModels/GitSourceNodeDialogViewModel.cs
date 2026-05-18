using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Models;
using FlowMy.Services.Interaction;
using System.Diagnostics;
using System.IO;

namespace FlowMy.ViewModels
{
    /// <summary>
    /// ViewModel cho dialog node Git Source trên canvas.
    /// Chỉ có: Cài đặt (command line) + Hiển thị (icon/màu/tooltip) + VS Code button.
    /// </summary>
    public partial class GitSourceNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly GitSourceNode _gitNode;

        [ObservableProperty] private string _iconKey = "git-alt brands";
        [ObservableProperty] private string _iconColorKey = "White";
        [ObservableProperty] private string _tooltipText = string.Empty;
        [ObservableProperty] private string _commandText = string.Empty;
        [ObservableProperty] private string _commandOutput = string.Empty;
        [ObservableProperty] private bool _isRunningCommand;

        public GitSourceNodeDialogViewModel(GitSourceNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _gitNode = node ?? throw new ArgumentNullException(nameof(node));

            IconKey = node.IconKey;
            IconColorKey = node.IconColorKey ?? "White";
            TooltipText = node.TooltipText;
            CommandText = node.CommandText ?? string.Empty;
        }

        protected override string GetDefaultTitle() => "Git Source";

        protected override void OnSaveTitle()
        {
            _gitNode.IconKey = IconKey;
            _gitNode.IconColorKey = IconColorKey;
            _gitNode.TooltipText = TooltipText;
            _gitNode.CommandText = CommandText;
            _gitNode.NotifyTitleChanged();
        }

        [RelayCommand]
        private void OpenInVsCode()
        {
            var localPath = _gitNode.LocalPath;
            if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath))
            {
                CommandOutput = "❌ Thư mục source không tồn tại: " + (localPath ?? "(trống)");
                return;
            }

            try
            {
                // Dùng cmd /c "cd /d <path> && code ." để mở VS Code
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c cd /d \"{localPath}\" && code .",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };

                var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(5000);
                    if (proc.ExitCode != 0)
                    {
                        var err = proc.StandardError.ReadToEnd();
                        CommandOutput = $"❌ Không thể mở VS Code. Hãy cài đặt VS Code và đảm bảo lệnh 'code' có trong PATH.\n\nTải tại: https://code.visualstudio.com/\n\n{err}";
                    }
                    else
                    {
                        CommandOutput = $"✅ Đã mở VS Code: {localPath}";
                    }
                }
            }
            catch (Exception ex)
            {
                CommandOutput = $"❌ Không thể mở VS Code: {ex.Message}\n\nHãy cài đặt VS Code: https://code.visualstudio.com/";
            }
        }

        [RelayCommand]
        private async Task RunCommandAsync()
        {
            if (string.IsNullOrWhiteSpace(CommandText))
            {
                CommandOutput = "❌ Chưa nhập lệnh.";
                return;
            }

            var workDir = _gitNode.LocalPath;
            if (string.IsNullOrWhiteSpace(workDir) || !Directory.Exists(workDir))
            {
                CommandOutput = "❌ Thư mục source không tồn tại.";
                return;
            }

            IsRunningCommand = true;
            CommandOutput = $"⏳ Đang chạy: {CommandText}\n";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {CommandText}",
                    WorkingDirectory = workDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    CommandOutput += "❌ Không thể khởi tạo process.";
                    return;
                }

                var stdout = await process.StandardOutput.ReadToEndAsync();
                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var output = string.Empty;
                if (!string.IsNullOrWhiteSpace(stdout))
                    output += stdout;
                if (!string.IsNullOrWhiteSpace(stderr))
                    output += "\n[STDERR]\n" + stderr;

                output += $"\n\n[Exit Code: {process.ExitCode}]";
                CommandOutput = output;
            }
            catch (Exception ex)
            {
                CommandOutput += $"\n❌ Lỗi: {ex.Message}";
            }
            finally
            {
                IsRunningCommand = false;
            }
        }
    }
}
