using System.IO;
using System.IO.Compression;
using FlowMy.Models;

namespace FlowMy.Services.Workflow;

/// <summary>
/// Gói nén (.webpkg.zip) chứa cache WebNode, profile WebView2 chung, file offline HtmlUi — đồng bộ với JSON qua <see cref="Models.Persistence.WorkflowDto.PortableWebBundleFileName"/>.
/// </summary>
public static class PortableWebBundleZipService
{
    /// <summary>Hậu tố file bundle (đặt cạnh file JSON, tên trong JSON chỉ là tên file).</summary>
    public const string BundleFileSuffix = ".webpkg.zip";
    public const string WorkflowJsonEntryName = "workflow.json";

    public static Task CreateBundleZipAsync(
        string zipPath,
        IEnumerable<WorkflowNode> nodes,
        string portableCookiesJson,
        IProgress<WorkflowTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new WorkflowTransferProgress("Đang chuẩn bị...", 5));

            var temp = Path.Combine(Path.GetTempPath(), "FlowMyExport_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temp);
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new WorkflowTransferProgress("Đang ghi cookies.json + tài nguyên Html offline (không copy profile WebView2)...", 25));
                WebNodeCacheHelper.ExportPortableWebBundleLightweight(temp, nodes, portableCookiesJson);

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new WorkflowTransferProgress("Đang nén...", 65));
                var dir = Path.GetDirectoryName(zipPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(zipPath))
                {
                    try { File.Delete(zipPath); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"PortableWebBundleZipService: không ghi đè zip: {ex.Message}");
                        throw;
                    }
                }

                ZipFile.CreateFromDirectory(temp, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
                progress?.Report(new WorkflowTransferProgress("Hoàn tất", 100));
            }
            finally
            {
                try
                {
                    if (Directory.Exists(temp))
                        Directory.Delete(temp, recursive: true);
                }
                catch
                {
                    /* ignore */
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Tạo 1 file nén duy nhất chứa <see cref="WorkflowJsonEntryName"/> + gói web portable (cookies.json + Html offline).
    /// </summary>
    public static Task CreateWorkflowPackageZipAsync(
        string zipPath,
        string workflowJson,
        IEnumerable<WorkflowNode> nodes,
        string portableCookiesJson,
        IProgress<WorkflowTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new WorkflowTransferProgress("Đang chuẩn bị...", 5));

            var temp = Path.Combine(Path.GetTempPath(), "FlowMyExportPkg_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temp);
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new WorkflowTransferProgress("Đang ghi web bundle (cookies + Html offline)...", 25));
                WebNodeCacheHelper.ExportPortableWebBundleLightweight(temp, nodes, portableCookiesJson);

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new WorkflowTransferProgress("Đang ghi workflow.json...", 45));
                File.WriteAllText(Path.Combine(temp, WorkflowJsonEntryName), workflowJson ?? "{}");

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new WorkflowTransferProgress("Đang nén...", 70));
                var dir = Path.GetDirectoryName(zipPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(zipPath))
                {
                    try { File.Delete(zipPath); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"PortableWebBundleZipService: không ghi đè zip: {ex.Message}");
                        throw;
                    }
                }

                ZipFile.CreateFromDirectory(temp, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
                progress?.Report(new WorkflowTransferProgress("Hoàn tất", 100));
            }
            finally
            {
                try
                {
                    if (Directory.Exists(temp))
                        Directory.Delete(temp, recursive: true);
                }
                catch
                {
                    /* ignore */
                }
            }
        }, cancellationToken);
    }

    public static Task ExtractAndRestoreAsync(
        string zipPath,
        IEnumerable<WorkflowNode> nodes,
        IProgress<WorkflowTransferProgress>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new WorkflowTransferProgress("Đang giải nén gói web...", 15));

            var temp = Path.Combine(Path.GetTempPath(), "FlowMyImport_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temp);
                ZipFile.ExtractToDirectory(zipPath, temp, overwriteFiles: true);
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new WorkflowTransferProgress("Đang áp dụng gói web (cookies.json / hoặc cache legacy)...", 60));
                WebNodeCacheHelper.RestorePortableWebCaches(temp, nodes);
                progress?.Report(new WorkflowTransferProgress("Hoàn tất", 100));
            }
            finally
            {
                try
                {
                    if (Directory.Exists(temp))
                        Directory.Delete(temp, recursive: true);
                }
                catch
                {
                    /* ignore */
                }
            }
        }, cancellationToken);
    }

    /// <summary>Dùng khi <see cref="FileWorkflowPersistenceService.Load"/> (không có UI progress).</summary>
    public static void ExtractAndRestore(string zipPath, IEnumerable<WorkflowNode> nodes)
    {
        var temp = Path.Combine(Path.GetTempPath(), "FlowMyLoad_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            ZipFile.ExtractToDirectory(zipPath, temp, overwriteFiles: true);
            WebNodeCacheHelper.RestorePortableWebCaches(temp, nodes);
        }
        finally
        {
            try
            {
                if (Directory.Exists(temp))
                    Directory.Delete(temp, recursive: true);
            }
            catch
            {
                /* ignore */
            }
        }
    }
}
