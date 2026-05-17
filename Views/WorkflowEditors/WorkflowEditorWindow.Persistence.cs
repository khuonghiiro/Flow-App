using FlowMy.Models;
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
        private const string WebBundleExtension = ".webpkg.zip";

        private sealed class ExportWorkflowSelection
        {
            public string Format { get; init; } = "json";
            public bool IncludeRuntimeOutput { get; init; }
            public bool IncludeWebCookies { get; init; }
            public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal;
            public string CompressionMode { get; init; } = "medium";

            public bool IsCompressed => string.Equals(Format, "flowz", StringComparison.OrdinalIgnoreCase);
            public bool IsWebBundle => string.Equals(Format, "webpkg", StringComparison.OrdinalIgnoreCase);
        }

        private async void ExportWorkflow_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var selection = ShowExportWorkflowOptionsDialog();
            if (selection == null) return;

            if (selection.IsWebBundle)
            {
                _ = ExportWorkflowWithWebBundleAsync(selection);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = selection.IsCompressed
                    ? "Compressed Workflow (*.flowz)|*.flowz"
                    : "Workflow JSON (*.json)|*.json",
                FileName = selection.IsCompressed
                    ? (ViewModel.CurrentWorkflowName ?? "workflow") + CompressedWorkflowExtension
                    : ViewModel.CurrentWorkflowName ?? "workflow"
            };

            if (saveFileDialog.ShowDialog(this) == true)
            {
                string? embeddedPortableWebBundleBase64 = null;
                var includeEmbeddedWeb = selection.IsCompressed && selection.IncludeWebCookies;
                if (includeEmbeddedWeb)
                {
                    try
                    {
                        var cookiesJson = await WebCookieSnapshotService.ExportSnapshotJsonAsync(ViewModel.Nodes.ToList(), CancellationToken.None);
                        var tempZip = Path.Combine(Path.GetTempPath(), "FlowMyExportEmbedded_" + Guid.NewGuid().ToString("N") + ".zip");
                        try
                        {
                            await PortableWebBundleZipService.CreateBundleZipAsync(
                                tempZip,
                                ViewModel.Nodes,
                                cookiesJson,
                                progress: null,
                                cancellationToken: CancellationToken.None,
                                compressionLevel: selection.CompressionLevel);
                            embeddedPortableWebBundleBase64 = Convert.ToBase64String(File.ReadAllBytes(tempZip));
                        }
                        finally
                        {
                            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* ignore */ }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            this,
                            $"Không thu thập được dữ liệu Web để nhúng vào flowz: {ex.Message}",
                            "Export flowz",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }

                var exportOptions = new WorkflowExportOptionsDto
                {
                    IncludeRuntimeData = selection.IncludeRuntimeOutput,
                    Compressed = selection.IsCompressed,
                    IncludeWebBundle = includeEmbeddedWeb,
                    IncludeWebCookies = includeEmbeddedWeb,
                    IncludeOfflineHtmlAssets = includeEmbeddedWeb,
                    PackageKind = selection.IsCompressed ? "flowz" : "json",
                    CompressionMode = selection.IsCompressed ? selection.CompressionMode : null
                };
                var json = ViewModel.ExportToJson(
                    includeRuntimeOutput: selection.IncludeRuntimeOutput,
                    exportOptions: exportOptions,
                    embeddedPortableWebBundleBase64: embeddedPortableWebBundleBase64);

                if (selection.IsCompressed)
                {
                    var utf8 = System.Text.Encoding.UTF8.GetBytes(json);
                    using var output = File.Create(saveFileDialog.FileName);
                    using var brotli = new BrotliStream(output, selection.CompressionLevel, leaveOpen: false);
                    brotli.Write(utf8, 0, utf8.Length);
                }
                else
                {
                    File.WriteAllText(saveFileDialog.FileName, json);
                }

                Activate();
            }
        }

        private ExportWorkflowSelection? ShowExportWorkflowOptionsDialog()
        {
            var dialog = new ExportWorkflowOptionsDialog
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return null;
            }

            return new ExportWorkflowSelection
            {
                Format = dialog.SelectedFormat,
                IncludeRuntimeOutput = dialog.IncludeRuntimeOutput,
                IncludeWebCookies = dialog.IncludeWebCookies,
                CompressionLevel = dialog.SelectedCompressionLevel,
                CompressionMode = dialog.SelectedCompressionMode
            };
        }

        private async Task ExportWorkflowWithWebBundleAsync(ExportWorkflowSelection selection)
        {
            if (ViewModel == null) return;

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Workflow Package (*.webpkg.zip)|*.webpkg.zip|Zip (*.zip)|*.zip",
                FileName = (ViewModel.CurrentWorkflowName ?? "workflow") + WebBundleExtension
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
                string cookiesJson = "{}";
                if (selection.IncludeWebCookies)
                {
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
                }

                var exportOptions = new WorkflowExportOptionsDto
                {
                    IncludeRuntimeData = selection.IncludeRuntimeOutput,
                    Compressed = true,
                    IncludeWebBundle = true,
                    IncludeWebCookies = selection.IncludeWebCookies,
                    IncludeOfflineHtmlAssets = true,
                    PackageKind = "webpkg",
                    CompressionMode = selection.CompressionMode
                };
                var json = ViewModel.ExportToJson(
                    portableWebBundleFileName: null,
                    includeRuntimeOutput: selection.IncludeRuntimeOutput,
                    exportOptions: exportOptions);
                await PortableWebBundleZipService.CreateWorkflowPackageZipAsync(
                    packagePath,
                    json,
                    ViewModel.Nodes,
                    cookiesJson,
                    progress,
                    cts.Token,
                    selection.CompressionLevel);
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

                if (dtoProbe.ExportOptions?.IncludeWebBundle == true && extractedPackageRoot == null)
                {
                    if (!string.IsNullOrWhiteSpace(dtoProbe.EmbeddedPortableWebBundleBase64))
                    {
                        var temp = Path.Combine(Path.GetTempPath(), "FlowMyImportPkg_" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(temp);
                        var zipPath = Path.Combine(temp, "embedded.webpkg.zip");
                        File.WriteAllBytes(zipPath, Convert.FromBase64String(dtoProbe.EmbeddedPortableWebBundleBase64));
                        ZipFile.ExtractToDirectory(zipPath, temp, overwriteFiles: true);
                        extractedPackageRoot = temp;
                    }

                    var packageName = dtoProbe.PortableWebBundleFileName;
                    if (string.IsNullOrWhiteSpace(dtoProbe.EmbeddedPortableWebBundleBase64) &&
                        !string.IsNullOrWhiteSpace(packageName))
                    {
                        var adjacentZip = Path.Combine(Path.GetDirectoryName(importPath) ?? string.Empty, packageName);
                        if (File.Exists(adjacentZip))
                        {
                            var temp = Path.Combine(Path.GetTempPath(), "FlowMyImportPkg_" + Guid.NewGuid().ToString("N"));
                            Directory.CreateDirectory(temp);
                            ZipFile.ExtractToDirectory(adjacentZip, temp, overwriteFiles: true);
                            extractedPackageRoot = temp;
                        }
                    }
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

        private void GitManagerButton_Click(object sender, RoutedEventArgs e)
        {
            // Mở dialog Git Source cho node đang chọn hoặc tạo mới
            if (ViewModel == null) return;

            // Nếu đang chọn 1 GitSourceNode → mở dialog của nó
            if (ViewModel.SelectedNode is GitSourceNode gitNode && gitNode.Border != null)
            {
                // Trigger right-click dialog (giống double-click mở dialog)
                var dialog = new Views.Overlays.GitSourceNodeDialog(gitNode, this, this);
                dialog.Show();
                return;
            }

            // Nếu không → tạo node GitSource mới ở giữa canvas rồi mở dialog
            var newNode = _templateFactory?.Create("GitSource",
                WorkflowCanvas.ActualWidth / 2,
                WorkflowCanvas.ActualHeight / 2);

            if (newNode is GitSourceNode newGitNode)
            {
                ViewModel.Nodes.Add(newGitNode);
                _nodeRenderer?.RenderNode(newGitNode, WorkflowCanvas);
                ViewModel.SelectedNode = newGitNode;

                // Mở dialog ngay
                var dialog = new Views.Overlays.GitSourceNodeDialog(newGitNode, this, this);
                dialog.Show();
            }
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
