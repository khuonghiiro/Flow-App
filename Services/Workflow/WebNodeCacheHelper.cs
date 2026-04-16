using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Utils;

namespace FlowMy.Services.Workflow;

/// <summary>
/// Cache WebView2 theo từng WebNode: CSS, JS, cookies, cấu hình (UserDataFolder).
/// Cung cấp: runtime path, copy khi duplicate, lưu/khôi phục khi Save/Load workflow.
/// </summary>
public static class WebNodeCacheHelper
{
    private static readonly string BaseCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlowMy", "WebNodeCache");

    /// <summary>Thư mục con trong <c>{workflow}_webcache</c>: snapshot profile WebView2 dùng chung (cookie, storage).</summary>
    public const string SharedWebViewProfileFolderName = "_webview2_shared";

    /// <summary>Thư mục con: bản sao file JS/CSS offline được HtmlUiNode tham chiếu (portable).</summary>
    public const string HtmlOfflineAssetsBundleFolderName = "html_offline_assets";

    /// <summary>File JSON cookie snapshot (format 2) trong .webpkg.zip — nhẹ, không gồm cả profile WebView2.</summary>
    public const string PortableCookieBundleFileName = "cookies.json";

    /// <summary>
    /// Thư mục cache runtime cho WebView2 của node (theo node.Id).
    /// Chứa CSS, JS, cookies, storage — dùng làm UserDataFolder khi khởi tạo CoreWebView2.
    /// </summary>
    public static string GetRuntimeCachePath(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            nodeId = Guid.NewGuid().ToString("N");
        return Path.Combine(BaseCacheDir, nodeId);
    }

    /// <summary>
    /// Thư mục cache runtime chung cho tất cả WebView2 nodes.
    /// Tất cả node web sẽ dùng chung một cache để tránh tạo cache riêng cho từng node.
    /// Chứa CSS, JS, cookies, storage — dùng làm UserDataFolder khi khởi tạo CoreWebView2.
    /// </summary>
    public static string GetSharedRuntimeCachePath()
    {
        return Path.Combine(BaseCacheDir, "Shared");
    }

    /// <summary>
    /// Thư mục cache WebNode khi lưu workflow (workflowsDir + workflowName + "_webcache").
    /// </summary>
    public static string GetWorkflowWebCacheDir(string workflowsDir, string workflowName)
    {
        if (string.IsNullOrWhiteSpace(workflowName)) return Path.Combine(workflowsDir ?? "", "_webcache");
        return Path.Combine(workflowsDir ?? "", workflowName + "_webcache");
    }

    /// <summary>
    /// Copy toàn bộ cache từ node nguồn sang node đích (dùng khi duplicate WebNode).
    /// </summary>
    public static void CopyWebNodeCache(string sourceNodeId, string destNodeId)
    {
        if (string.IsNullOrWhiteSpace(sourceNodeId) || string.IsNullOrWhiteSpace(destNodeId))
            return;
        var src = GetRuntimeCachePath(sourceNodeId);
        var dest = GetRuntimeCachePath(destNodeId);
        if (!Directory.Exists(src))
            return;
        CopyDirectory(src, dest);
    }

    /// <summary>
    /// Lưu cache WebView2 (CSS, JS, cookies, cấu hình) của tất cả WebNode vào thư mục workflow.
    /// Gọi sau khi Save workflow (Ctrl+S).
    /// </summary>
    public static void SaveWorkflowWebNodeCaches(string workflowsDir, string workflowName, IEnumerable<WorkflowNode> nodes)
    {
        if (nodes == null) return;
        var cacheBase = GetWorkflowWebCacheDir(workflowsDir, workflowName);
        foreach (var n in nodes.OfType<WebNode>())
        {
            var src = GetRuntimeCachePath(n.Id);
            if (!Directory.Exists(src)) continue;
            var dest = Path.Combine(cacheBase, n.Id);
            try
            {
                CopyDirectory(src, dest);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.SaveWorkflowWebNodeCaches {n.Id}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Snapshot thư mục UserData WebView2 dùng chung (toàn app) vào bundle workflow — nơi thực tế chứa cookie/session
    /// khi các node dùng environment WebView2 dùng chung.
    /// </summary>
    public static void SaveWorkflowSharedWebProfile(string workflowsDir, string workflowName)
    {
        var cacheBase = GetWorkflowWebCacheDir(workflowsDir, workflowName);
        var dest = Path.Combine(cacheBase, SharedWebViewProfileFolderName);
        var src = GetSharedRuntimeCachePath();
        try
        {
            if (!Directory.Exists(src)) return;
            CopyDirectory(src, dest);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.SaveWorkflowSharedWebProfile: {ex.Message}");
        }
    }

    /// <summary>
    /// Sao chép các file HtmlUi offline (theo <see cref="HtmlUiNode.OfflineAssets"/>) vào bundle để mang sang máy khác.
    /// </summary>
    public static void SaveHtmlOfflineAssetsBundle(string workflowsDir, string workflowName, IEnumerable<WorkflowNode> nodes)
    {
        if (nodes == null) return;
        var cacheBase = GetWorkflowWebCacheDir(workflowsDir, workflowName);
        var destRoot = Path.Combine(cacheBase, HtmlOfflineAssetsBundleFolderName);
        try
        {
            if (Directory.Exists(destRoot))
            {
                try { Directory.Delete(destRoot, recursive: true); }
                catch { /* best effort */ }
            }
        }
        catch { /* ignore */ }

        foreach (var html in nodes.OfType<HtmlUiNode>())
        {
            var assets = html.OfflineAssets;
            if (assets == null) continue;
            foreach (var asset in assets)
            {
                var fn = asset.LocalFileName?.Trim();
                if (string.IsNullOrWhiteSpace(fn)) continue;
                var safe = Path.GetFileName(fn);
                if (string.IsNullOrWhiteSpace(safe)) continue;
                try
                {
                    var srcPath = HtmlOfflineAssetService.GetLocalFilePath(safe);
                    if (!File.Exists(srcPath)) continue;
                    var destDir = destRoot;
                    Directory.CreateDirectory(destDir);
                    var destFile = Path.Combine(destDir, safe);
                    File.Copy(srcPath, destFile, overwrite: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.SaveHtmlOfflineAssetsBundle {safe}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Khôi phục cache WebView2 của tất cả WebNode từ thư mục workflow (khi Load workflow).
    /// </summary>
    public static void RestoreWorkflowWebNodeCaches(string workflowsDir, string workflowName, IEnumerable<WorkflowNode> nodes)
    {
        if (nodes == null) return;
        var cacheBase = GetWorkflowWebCacheDir(workflowsDir, workflowName);
        foreach (var n in nodes.OfType<WebNode>())
        {
            var src = Path.Combine(cacheBase, n.Id);
            if (!Directory.Exists(src)) continue;
            var dest = GetRuntimeCachePath(n.Id);
            try
            {
                CopyDirectory(src, dest);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.RestoreWorkflowWebNodeCaches {n.Id}: {ex.Message}");
            }
        }
    }

    /// <summary>Khôi phục profile WebView2 dùng chung từ bundle workflow (nếu có).</summary>
    public static void RestoreWorkflowSharedWebProfile(string workflowsDir, string workflowName)
    {
        var cacheBase = GetWorkflowWebCacheDir(workflowsDir, workflowName);
        var src = Path.Combine(cacheBase, SharedWebViewProfileFolderName);
        if (!Directory.Exists(src)) return;
        var dest = GetSharedRuntimeCachePath();
        try
        {
            CopyDirectory(src, dest);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.RestoreWorkflowSharedWebProfile: {ex.Message}");
        }
    }

    /// <summary>Ghi đè/ghi thêm file HtmlUiAssets từ bundle portable (nếu có).</summary>
    public static void RestoreHtmlOfflineAssetsBundle(string workflowsDir, string workflowName)
    {
        var cacheBase = GetWorkflowWebCacheDir(workflowsDir, workflowName);
        var src = Path.Combine(cacheBase, HtmlOfflineAssetsBundleFolderName);
        if (!Directory.Exists(src)) return;
        var destFolder = HtmlOfflineAssetService.GetAssetsFolder();
        try
        {
            Directory.CreateDirectory(destFolder);
            foreach (var file in Directory.GetFiles(src))
            {
                var name = Path.GetFileName(file);
                if (string.IsNullOrWhiteSpace(name)) continue;
                try
                {
                    File.Copy(file, Path.Combine(destFolder, name), overwrite: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.RestoreHtmlOfflineAssetsBundle {name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.RestoreHtmlOfflineAssetsBundle: {ex.Message}");
        }
    }

    /// <summary>
    /// Gói portable nhẹ: <see cref="PortableCookieBundleFileName"/> + <see cref="HtmlOfflineAssetsBundleFolderName"/> (không copy profile WebView2 / cache từng node).
    /// </summary>
    public static void ExportPortableWebBundleLightweight(string portableCacheRoot, IEnumerable<WorkflowNode> nodes, string cookiesJson)
    {
        if (string.IsNullOrWhiteSpace(portableCacheRoot) || nodes == null) return;

        try
        {
            if (Directory.Exists(portableCacheRoot))
            {
                try { Directory.Delete(portableCacheRoot, recursive: true); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ExportPortableWebBundleLightweight: không xóa được {portableCacheRoot}: {ex.Message}");
                }
            }
            Directory.CreateDirectory(portableCacheRoot);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ExportPortableWebBundleLightweight: {ex.Message}");
            return;
        }

        try
        {
            File.WriteAllText(Path.Combine(portableCacheRoot, PortableCookieBundleFileName), cookiesJson ?? "{\"format\":2,\"entries\":[]}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ExportPortableWebBundleLightweight cookies.json: {ex.Message}");
        }

        CopyHtmlOfflineAssetsIntoBundleRoot(portableCacheRoot, nodes);
    }

    private static void CopyHtmlOfflineAssetsIntoBundleRoot(string portableCacheRoot, IEnumerable<WorkflowNode> nodes)
    {
        var destRoot = Path.Combine(portableCacheRoot, HtmlOfflineAssetsBundleFolderName);
        foreach (var html in nodes.OfType<HtmlUiNode>())
        {
            var assets = html.OfflineAssets;
            if (assets == null) continue;
            foreach (var asset in assets)
            {
                var fn = asset.LocalFileName?.Trim();
                if (string.IsNullOrWhiteSpace(fn)) continue;
                var safe = Path.GetFileName(fn);
                if (string.IsNullOrWhiteSpace(safe)) continue;
                try
                {
                    var srcPath = HtmlOfflineAssetService.GetLocalFilePath(safe);
                    if (!File.Exists(srcPath)) continue;
                    Directory.CreateDirectory(destRoot);
                    File.Copy(srcPath, Path.Combine(destRoot, safe), overwrite: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CopyHtmlOfflineAssetsIntoBundleRoot {safe}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Khôi phục từ thư mục portable. Nếu có <see cref="PortableCookieBundleFileName"/> format v2: chỉ copy Html offline + enqueue cookie cho WebView2;
    /// không copy profile nặng. Ngược lại: hành vi legacy (cache từng node + shared + html).
    /// </summary>
    public static void RestorePortableWebCaches(string portableCacheRoot, IEnumerable<WorkflowNode> nodes)
    {
        if (string.IsNullOrWhiteSpace(portableCacheRoot) || !Directory.Exists(portableCacheRoot) || nodes == null)
            return;

        string? cookieFileText = null;
        var cookiePath = Path.Combine(portableCacheRoot, PortableCookieBundleFileName);
        if (File.Exists(cookiePath))
        {
            try { cookieFileText = File.ReadAllText(cookiePath); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestorePortableWebCaches read cookies: {ex.Message}");
            }
        }

        if (!string.IsNullOrEmpty(cookieFileText) && WebCookieSnapshotService.IsV2PortableCookieBundleJson(cookieFileText))
        {
            RestorePortableHtmlAssetsOnly(portableCacheRoot);
            WebCookiePortableBridge.Enqueue(cookieFileText);
            return;
        }

        foreach (var n in nodes.OfType<WebNode>())
        {
            var src = Path.Combine(portableCacheRoot, n.Id);
            if (!Directory.Exists(src)) continue;
            var dest = GetRuntimeCachePath(n.Id);
            try { CopyDirectory(src, dest); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.RestorePortableWebCaches WebNode {n.Id}: {ex.Message}");
            }
        }

        var sharedSrc = Path.Combine(portableCacheRoot, SharedWebViewProfileFolderName);
        if (Directory.Exists(sharedSrc))
        {
            try { CopyDirectory(sharedSrc, GetSharedRuntimeCachePath()); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.RestorePortableWebCaches shared: {ex.Message}");
            }
        }

        RestorePortableHtmlAssetsOnly(portableCacheRoot);
    }

    private static void RestorePortableHtmlAssetsOnly(string portableCacheRoot)
    {
        var htmlAssetsSrc = Path.Combine(portableCacheRoot, HtmlOfflineAssetsBundleFolderName);
        if (!Directory.Exists(htmlAssetsSrc)) return;

        var destFolder = HtmlOfflineAssetService.GetAssetsFolder();
        try
        {
            Directory.CreateDirectory(destFolder);
            foreach (var file in Directory.GetFiles(htmlAssetsSrc))
            {
                var name = Path.GetFileName(file);
                if (string.IsNullOrWhiteSpace(name)) continue;
                try { File.Copy(file, Path.Combine(destFolder, name), overwrite: true); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.RestorePortableHtmlAssetsOnly {name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.RestorePortableHtmlAssetsOnly folder: {ex.Message}");
        }
    }

    /// <summary>
    /// Copy đệ quy thư mục source -> dest (tạo dest nếu chưa có).
    /// </summary>
    public static void CopyDirectory(string sourceDir, string destDir)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
            return;
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            try
            {
                File.Copy(file, destFile, overwrite: true);
            }
            catch
            {
                // Ignore locked / permission errors
            }
        }
        foreach (var sub in Directory.GetDirectories(sourceDir))
        {
            var destSub = Path.Combine(destDir, Path.GetFileName(sub));
            CopyDirectory(sub, destSub);
        }
    }
}
