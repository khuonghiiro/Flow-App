using FlowMy.Models.Persistence;
using FlowMy.Services.Workflow;
using FlowMy.Views.Overlays;
using Microsoft.Win32;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private const string CompressedWorkflowExtension = ".flowz";

        private void ExportWorkflow_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Workflow JSON (*.json)|*.json",
                FileName = ViewModel.CurrentWorkflowName ?? "workflow"
            };

            if (saveFileDialog.ShowDialog(this) == true)
            {
                string json = ViewModel.ExportToJson();
                File.WriteAllText(saveFileDialog.FileName, json);
                Activate();
            }
        }

        private void ExportCompressedWorkflow_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Compressed Workflow (*.flowz)|*.flowz",
                FileName = (ViewModel.CurrentWorkflowName ?? "workflow") + CompressedWorkflowExtension
            };

            if (saveFileDialog.ShowDialog(this) == true)
            {
                var json = ViewModel.ExportToJson();
                var utf8 = System.Text.Encoding.UTF8.GetBytes(json);
                using var output = File.Create(saveFileDialog.FileName);
                using var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: false);
                brotli.Write(utf8, 0, utf8.Length);
                Activate();
            }
        }

        private async void ExportWorkflowWithWebBundle_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Workflow Package (*.webpkg.zip)|*.webpkg.zip|Zip (*.zip)|*.zip",
                FileName = (ViewModel.CurrentWorkflowName ?? "workflow") + PortableWebBundleZipService.BundleFileSuffix
            };

            if (saveFileDialog.ShowDialog(this) != true) return;

            var packagePath = saveFileDialog.FileName;
            var packageDir = Path.GetDirectoryName(packagePath);
            if (string.IsNullOrEmpty(packageDir))
            {
                MessageBox.Show(this, "Đường dẫn export không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var progressDlg = new WorkflowTransferProgressDialog
            {
                Owner = this,
                Title = "Export workflow + WebView2"
            };
            var cts = new CancellationTokenSource();
            progressDlg.CancellationRequested += (_, _) => cts.Cancel();
            var progress = new Progress<WorkflowTransferProgress>(p => progressDlg.Report(p));
            progressDlg.Show();

            SetExportImportBusy(true);
            try
            {
                string cookiesJson;
                try
                {
                    cookiesJson = await WebCookieSnapshotService.ExportSnapshotJsonAsync(ViewModel.Nodes.ToList(), cts.Token);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        $"Không thu thập được cookie từ WebView2 (cần UI thread / WebView2): {ex.Message}",
                        "Export + Web",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var json = ViewModel.ExportToJson(portableWebBundleFileName: null);
                await PortableWebBundleZipService.CreateWorkflowPackageZipAsync(packagePath, json, ViewModel.Nodes, cookiesJson, progress, cts.Token);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show(this, "Đã hủy export.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Lỗi export: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetExportImportBusy(false);
                progressDlg.Close();
            }
        }

        private async void ImportWorkflow_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var openFileDialog = new OpenFileDialog
            {
                Filter = "Workflow (*.json;*.flowz;*.webpkg.zip;*.zip)|*.json;*.flowz;*.webpkg.zip;*.zip|Workflow JSON (*.json)|*.json|Compressed Workflow (*.flowz)|*.flowz|Workflow Package (*.webpkg.zip;*.zip)|*.webpkg.zip;*.zip"
            };

            if (openFileDialog.ShowDialog(this) != true) return;

            var importPath = openFileDialog.FileName;
            string? extractedPackageRoot = null;
            string json;
            try
            {
                var ext = Path.GetExtension(importPath)?.ToLowerInvariant();
                if (ext == ".json")
                {
                    json = File.ReadAllText(importPath);
                }
                else if (ext == CompressedWorkflowExtension)
                {
                    using var input = File.OpenRead(importPath);
                    using var brotli = new BrotliStream(input, CompressionMode.Decompress, leaveOpen: false);
                    using var reader = new StreamReader(brotli, System.Text.Encoding.UTF8);
                    json = reader.ReadToEnd();
                }
                else
                {
                    // Package zip: extract -> read workflow.json inside.
                    var temp = Path.Combine(Path.GetTempPath(), "FlowMyImportPkg_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(temp);
                    extractedPackageRoot = temp;
                    try
                    {
                        ZipFile.ExtractToDirectory(importPath, temp, overwriteFiles: true);
                        var wfJsonPath = Path.Combine(temp, PortableWebBundleZipService.WorkflowJsonEntryName);
                        if (!File.Exists(wfJsonPath))
                            throw new FileNotFoundException($"Không tìm thấy {PortableWebBundleZipService.WorkflowJsonEntryName} trong file nén.");

                        json = File.ReadAllText(wfJsonPath);

                        // Replace importPath with extracted workflow.json so legacy logic (dir-based) still works when needed.
                        importPath = wfJsonPath;
                    }
                    finally
                    {
                        // cleanup handled later (success/failure) via extractedPackageRoot
                    }
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(extractedPackageRoot))
                {
                    try { Directory.Delete(extractedPackageRoot, recursive: true); } catch { /* ignore */ }
                }
                MessageBox.Show(this, $"Không đọc được file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var dtoProbe = JsonSerializer.Deserialize<WorkflowDto>(json);
                if (dtoProbe == null || dtoProbe.Nodes == null)
                {
                    if (!string.IsNullOrWhiteSpace(extractedPackageRoot))
                    {
                        try { Directory.Delete(extractedPackageRoot, recursive: true); } catch { /* ignore */ }
                    }
                    MessageBox.Show(this, "File JSON workflow không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            catch (JsonException)
            {
                if (!string.IsNullOrWhiteSpace(extractedPackageRoot))
                {
                    try { Directory.Delete(extractedPackageRoot, recursive: true); } catch { /* ignore */ }
                }
                MessageBox.Show(this, "File JSON không đúng định dạng workflow.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var progressDlg = new WorkflowTransferProgressDialog
            {
                Owner = this,
                Title = "Import workflow"
            };
            var cts = new CancellationTokenSource();
            progressDlg.CancellationRequested += (_, _) => cts.Cancel();
            var progress = new Progress<WorkflowTransferProgress>(p => progressDlg.Report(p));
            progressDlg.Show();

            ClearVisualsForReload();
            SetExportImportBusy(true);
            try
            {
                await ViewModel.ImportFromJsonAsync(json, importPath, progress, cts.Token);

                // If user selected a package zip, importPath currently points to extracted temp\workflow.json
                // and the extracted folder contains cookies/html bundle at root.
                var importDir = extractedPackageRoot ?? Path.GetDirectoryName(importPath);
                if (!string.IsNullOrWhiteSpace(importDir) && extractedPackageRoot != null)
                {
                    try
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        ((IProgress<WorkflowTransferProgress>)progress)?.Report(new WorkflowTransferProgress("Đang khôi phục dữ liệu web từ package...", 80));
                        var nodeList = ViewModel.Nodes.ToList();
                        await Task.Run(() => WebNodeCacheHelper.RestorePortableWebCaches(importDir, nodeList), cts.Token);
                    }
                    catch
                    {
                        // ignore restore errors; workflow itself is already loaded
                    }
                    finally
                    {
                        try { Directory.Delete(importDir, recursive: true); } catch { /* ignore */ }
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    ViewModel.SaveWorkflowSilently();
                    FitToViewAfterRender();
                    Activate();
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                    MessageBox.Show(this, "Đã hủy import.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    MessageBox.Show(this, $"Lỗi khi import: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(extractedPackageRoot))
                {
                    try { if (Directory.Exists(extractedPackageRoot)) Directory.Delete(extractedPackageRoot, recursive: true); } catch { /* ignore */ }
                }
                SetExportImportBusy(false);
                progressDlg.Close();
            }
        }

        private void SetExportImportBusy(bool busy)
        {
            if (ExportButton != null) ExportButton.IsEnabled = !busy;
            if (ExportCompressedButton != null) ExportCompressedButton.IsEnabled = !busy;
            if (ExportWebBundleButton != null) ExportWebBundleButton.IsEnabled = !busy;
            if (ImportButton != null) ImportButton.IsEnabled = !busy;
        }

        private void NewWorkflowButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            // Đếm số lượng nodes
            int nodeCount = ViewModel.Nodes.Count;

            // Kiểm tra xem có đúng 2 nodes và cả 2 đều là Start và End không
            bool hasOnlyStartAndEnd = nodeCount == 2 &&
                                      ViewModel.Nodes.Count(n => n.Type == Models.NodeType.Start) == 1 &&
                                      ViewModel.Nodes.Count(n => n.Type == Models.NodeType.End) == 1;

            // Nếu chỉ có Start và End, tạo mới luôn không hỏi
            if (hasOnlyStartAndEnd)
            {
                CreateNewWorkflow();
                return;
            }

            // Nếu có nhiều hơn 2 nodes hoặc có nodes khác Start/End, hỏi có muốn lưu workflow cũ không
            var result = MessageBox.Show(
                "Bạn có muốn lưu workflow hiện tại trước khi tạo workflow mới không?",
                "Tạo workflow mới",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                return; // User hủy
            }

            if (result == MessageBoxResult.Yes)
            {
                // Lưu workflow hiện tại
                ViewModel.SaveWorkflow();
            }

            // Tạo workflow mới
            CreateNewWorkflow();
        }

        private void CreateNewWorkflow()
        {
            if (ViewModel == null) return;

            // Dọn UI cũ trước khi reset
            ClearVisualsForReload();

            // Reset workflow về trạng thái ban đầu (chỉ có Start và End)
            if (ViewModel.ResetWorkflowCommand.CanExecute(null))
            {
                ViewModel.ResetWorkflowCommand.Execute(null);
            }

            // Fit to view sau khi tạo mới
            FitToViewAfterRender();
        }

        private void ManageWorkflowsButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var currentWorkflowName = ViewModel.CurrentWorkflowName;

            var dialog = new WorkflowManagementDialog
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                // Refresh combobox sau khi đóng dialog
                ViewModel.RefreshSavedWorkflows();

                // Đảm bảo CurrentWorkflowName vẫn được chọn nếu còn tồn tại
                // Nếu workflow hiện tại đã bị xóa hoặc đổi tên, chọn workflow đầu tiên hoặc clear
                if (!string.IsNullOrWhiteSpace(currentWorkflowName))
                {
                    // Kiểm tra xem workflow cũ còn tồn tại không (có thể đã bị xóa hoặc đổi tên)
                    if (ViewModel.SavedWorkflows.Contains(currentWorkflowName))
                    {
                        ViewModel.CurrentWorkflowName = currentWorkflowName;
                    }
                    else
                    {
                        // Try case-insensitive match
                        var match = ViewModel.SavedWorkflows.FirstOrDefault(n =>
                            string.Equals(n, currentWorkflowName, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            ViewModel.CurrentWorkflowName = match;
                        }
                        else if (ViewModel.SavedWorkflows.Count > 0)
                        {
                            // Nếu workflow hiện tại đã bị xóa, chọn workflow đầu tiên
                            ViewModel.CurrentWorkflowName = ViewModel.SavedWorkflows[0];
                        }
                        else
                        {
                            // Không còn workflow nào, clear selection
                            ViewModel.CurrentWorkflowName = string.Empty;
                        }
                    }
                }
                else if (ViewModel.SavedWorkflows.Count > 0)
                {
                    // Nếu không có workflow nào được chọn, chọn workflow đầu tiên
                    ViewModel.CurrentWorkflowName = ViewModel.SavedWorkflows[0];
                }
            }
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_colorThemeService == null) return;

            // Toggle theme
            _colorThemeService.ToggleTheme();

            // Update button icon & sync ComboBox
            UpdateThemeToggleIcon();
            InitializeThemeSelector();
            UpdateGridPattern();
        }

        /// <summary>
        /// Update theme toggle button icon based on current theme
        /// </summary>
        private void UpdateThemeToggleIcon()
        {
            //if (ThemeToggleIcon == null || _colorThemeService == null) return;

            //try
            //{
            //    // Dark themes (Dark/Dracula/Monokai/Night) → sun icon (click → go Light)
            //    // Light theme → moon icon (click → go Dark)
            //    bool isDarkTheme = _colorThemeService.CurrentTheme is "Dark" or "Dracula" or "Monokai" or "Night";
            //    var iconKey = isDarkTheme ? "sun-bright duotone-regular" : "moon-stars sharp-duotone-thin";

            //    string iconPath = IconResources.GetIconPath(iconKey);

            //    if (!string.IsNullOrEmpty(iconPath))
            //    {
            //        if (!iconPath.StartsWith("/") && !iconPath.StartsWith("http"))
            //            iconPath = "/" + iconPath;
            //        ThemeToggleIcon.Source = new Uri(iconPath, UriKind.RelativeOrAbsolute);
            //    }
            //    else
            //    {
            //        ThemeToggleIcon.Source = new Uri("/Assets/Icons/circle2.svg", UriKind.RelativeOrAbsolute);
            //    }

            //    // Sync ComboBox selection
            //    InitializeThemeSelector();
            //}
            //catch (Exception ex)
            //{
            //    System.Diagnostics.Debug.WriteLine($"Error updating theme icon: {ex.Message}");
            //    ThemeToggleIcon.Source = new Uri("/Assets/Icons/circle2.svg", UriKind.RelativeOrAbsolute);
            //}
        }
    }
}
