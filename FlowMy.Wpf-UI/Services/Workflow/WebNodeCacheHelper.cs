// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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

    public static string GetBaseCacheDir() => BaseCacheDir;

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

    private static readonly string CefRootDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlowMy", "CefRoot");

    private static readonly string UserProfilesDir = Path.Combine(CefRootDir, "UserProfiles");

    public static string GetCefRootDir() => CefRootDir;
    public static string GetUserProfilesDir() => UserProfilesDir;

    private static bool _initialized = false;
    private static void EnsureUserProfilesInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            if (!Directory.Exists(CefRootDir))
                Directory.CreateDirectory(CefRootDir);
            if (!Directory.Exists(UserProfilesDir))
                Directory.CreateDirectory(UserProfilesDir);

            // Cleanup old legacy UserProfiles directory if it exists directly under FlowMy
            var legacyUserProfilesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlowMy", "UserProfiles");

            if (Directory.Exists(legacyUserProfilesDir) && !string.Equals(legacyUserProfilesDir, UserProfilesDir, StringComparison.OrdinalIgnoreCase))
            {
                var knownSystemDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Shared", "Profiles", "UserProfiles", "GPUCache", "BlobStorage", "Network", "Session Storage",
                    "Cache", "Code Cache", "Local Storage", "Crashpad", "databases", "IndexedDB",
                    "Extensions", "GrShaderCache", "GraphiteDawnCache", "DawnCache", "Storage", "Default",
                    "AutofillStates", "CertificateRevocation", "Crowd Deny", "Dictionaries", "FileTypePolicies",
                    "FirstPartySetsPreloaded", "hyphen-data", "MEIPreload", "OnDeviceHeadSuggestModel",
                    "OptimizationHints", "OriginTrials", "PKIMetadata", "PrivacySandboxAttestationsPreloaded",
                    "Safe Browsing", "SafetyTips", "segmentation_platform", "ShaderCache", "SSLErrorAssistant",
                    "Subresource Filter", "TpcdMetadata", "TrustTokenKeyCommitments", "WidevineCdm", "ZxcvbnData"
                };

                foreach (var sub in Directory.GetDirectories(legacyUserProfilesDir))
                {
                    var name = Path.GetFileName(sub);
                    if (string.IsNullOrWhiteSpace(name) || name.StartsWith("_", StringComparison.Ordinal) || knownSystemDirs.Contains(name))
                    {
                        continue;
                    }
                    var dest = Path.Combine(UserProfilesDir, name);
                    if (!Directory.Exists(dest))
                    {
                        try { Directory.Move(sub, dest); } catch { }
                    }
                }

                try { Directory.Delete(legacyUserProfilesDir, recursive: true); } catch { }
            }
        }
        catch { }
    }

    /// <summary>
    /// Đảm bảo một thư mục profile người dùng tồn tại và có file marker profile.json
    /// </summary>
    public static string EnsureProfileExists(string profileName)
    {
        EnsureUserProfilesInitialized();

        var pName = string.IsNullOrWhiteSpace(profileName) || profileName.Equals("Shared", StringComparison.OrdinalIgnoreCase)
            ? "Shared"
            : profileName.Trim();

        var path = Path.Combine(UserProfilesDir, pName);
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var metaPath = Path.Combine(path, "profile.json");
            if (!File.Exists(metaPath))
            {
                File.WriteAllText(metaPath, $"{{\"name\":\"{pName}\",\"created\":\"{DateTime.UtcNow:o}\"}}");
            }
        }
        catch { }
        return path;
    }

    /// <summary>
    /// Thư mục cache runtime chung cho tất cả WebView2 nodes.
    /// </summary>
    public static string GetSharedRuntimeCachePath()
    {
        return EnsureProfileExists("Shared");
    }

    /// <summary>
    /// Lấy đường dẫn thư mục cache của profile (chỉ tự động tạo khi createIfNotExists = true).
    /// </summary>
    public static string GetProfileCachePath(string profileName, bool createIfNotExists = true)
    {
        EnsureUserProfilesInitialized();

        var pName = string.IsNullOrWhiteSpace(profileName) || profileName.Equals("Shared", StringComparison.OrdinalIgnoreCase)
            ? "Shared"
            : profileName.Trim();

        if (createIfNotExists)
        {
            return EnsureProfileExists(pName);
        }

        return Path.Combine(UserProfilesDir, pName);
    }

    /// <summary>
    /// Quét và trả về danh sách các Profile Cache do người dùng tạo (chỉ lọc thư mục chứa profile.json hoặc cookie hợp lệ)
    /// </summary>
    public static List<string> GetAvailableCacheProfiles()
    {
        EnsureProfileExists("Shared");

        var profiles = new List<string> { "Shared" };
        try
        {
            if (Directory.Exists(UserProfilesDir))
            {
                var subDirs = Directory.GetDirectories(UserProfilesDir);
                foreach (var dir in subDirs)
                {
                    var name = Path.GetFileName(dir);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (name.Equals("Shared", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Equals("Default", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.StartsWith("_", StringComparison.Ordinal)) continue;
                    
                    var knownSystemDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "Crashpad", "BrowserMetrics", "GrShaderCache", "GraphiteDawnCache", "DawnCache",
                        "AutofillStates", "CertificateRevocation", "Crowd Deny", "Dictionaries", "FileTypePolicies",
                        "FirstPartySetsPreloaded", "hyphen-data", "MEIPreload", "OnDeviceHeadSuggestModel",
                        "OptimizationHints", "OriginTrials", "PKIMetadata", "PrivacySandboxAttestationsPreloaded",
                        "Safe Browsing", "SafetyTips", "segmentation_platform", "ShaderCache", "SSLErrorAssistant",
                        "Subresource Filter", "TpcdMetadata", "TrustTokenKeyCommitments", "WidevineCdm", "ZxcvbnData",
                        "component_crx_cache"
                    };
                    if (knownSystemDirs.Contains(name)) continue;

                    var metaPath = Path.Combine(dir, "profile.json");
                    
                    if (File.Exists(metaPath))
                    {
                        try
                        {
                            var jsonText = File.ReadAllText(metaPath);
                            if (jsonText.Contains("\"deleted\":true") || jsonText.Contains("\"deleted\": true"))
                                continue;
                        }
                        catch { }
                    }

                    var hasCookies = File.Exists(Path.Combine(dir, "Cookies")) ||
                                     File.Exists(Path.Combine(dir, "Network", "Cookies")) ||
                                     File.Exists(Path.Combine(dir, "Preferences")) ||
                                     File.Exists(Path.Combine(dir, "Web Data"));

                    // Chỉ chấp nhận thư mục có file marker profile.json hoặc có file cookie/preferences trình duyệt
                    if (File.Exists(metaPath) || hasCookies)
                    {
                        if (!File.Exists(metaPath))
                        {
                            try { File.WriteAllText(metaPath, $"{{\"name\":\"{name}\"}}"); } catch { }
                        }
                        profiles.Add(name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi quét danh sách cache: {ex.Message}");
        }
        return profiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Sự kiện được phát khi danh sách Profile thay đổi (thêm/xóa profile).</summary>
    public static event EventHandler? ProfilesChanged;

    /// <summary>Phát thông báo cập nhật danh sách Profile cho tất cả control/dialog đang mở.</summary>
    public static void NotifyProfilesChanged()
    {
        try
        {
            ProfilesChanged?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NotifyProfilesChanged error: {ex.Message}");
        }
    }

    /// <summary>Xóa hoàn toàn thư mục profile cache của profile được chỉ định (trừ 'Shared').</summary>
    public static bool DeleteProfileCache(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) || profileName.Equals("Shared", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            var pName = profileName.Trim();
            CefSharpEnvironmentManager.DisposeProfileRequestContext(pName);

            var path = GetProfileCachePath(pName, createIfNotExists: false);
            if (Directory.Exists(path))
            {
                // MARK AS DELETED IMMEDIATELY so GetAvailableCacheProfiles will ignore it on next scan
                try
                {
                    var metaPath = Path.Combine(path, "profile.json");
                    File.WriteAllText(metaPath, $"{{\"name\":\"{pName}\",\"deleted\":true}}");
                }
                catch { }

                // Rename thư mục sang _deleted_xxx để GetAvailableCacheProfiles loại bỏ lập tức (bất kể CEF có đang nhả file hay chưa)
                var tempDeletePath = Path.Combine(GetUserProfilesDir(), $"_deleted_{Guid.NewGuid():N}");
                try
                {
                    Directory.Move(path, tempDeletePath);
                }
                catch
                {
                    tempDeletePath = path;
                }

                // CefSharp subprocesses might take a moment to release file handles after Dispose. Retry up to 10 times (2 seconds).
                System.Threading.Tasks.Task.Run(async () =>
                {
                    for (int i = 0; i < 10; i++)
                    {
                        try
                        {
                            if (!Directory.Exists(tempDeletePath)) break;
                            Directory.Delete(tempDeletePath, recursive: true);
                            break;
                        }
                        catch
                        {
                            await System.Threading.Tasks.Task.Delay(200);
                        }
                    }
                });
            }

            // Xóa triệt để các thư mục lưu cũ tại BaseCacheDir nếu có
            try
            {
                var legacyPath1 = Path.Combine(BaseCacheDir, "Profiles", pName);
                if (Directory.Exists(legacyPath1))
                {
                    var temp1 = Path.Combine(BaseCacheDir, "Profiles", $"_deleted_{Guid.NewGuid():N}");
                    try { Directory.Move(legacyPath1, temp1); Directory.Delete(temp1, true); } catch { }
                }
            }
            catch { }
            try
            {
                var legacyPath2 = Path.Combine(BaseCacheDir, pName);
                if (Directory.Exists(legacyPath2))
                {
                    var temp2 = Path.Combine(BaseCacheDir, $"_deleted_{Guid.NewGuid():N}");
                    try { Directory.Move(legacyPath2, temp2); Directory.Delete(temp2, true); } catch { }
                }
            }
            catch { }

            NotifyProfilesChanged();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi xóa profile {profileName}: {ex.Message}");
        }
        return false;
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
    /// <summary>
    /// Lưu cache WebView2 (CSS, JS, cookies, cấu hình) của tất cả WebNode vào thư mục workflow.
    /// Gọi sau khi Save workflow (Ctrl+S). Hỗ trợ cả profile độc lập (CustomCacheName).
    /// </summary>
    public static void SaveWorkflowWebNodeCaches(string workflowsDir, string workflowName, IEnumerable<WorkflowNode> nodes)
    {
        if (nodes == null) return;
        
        // Đảm bảo dữ liệu từ RAM đã được ghi xuống đĩa trước khi copy
        CefSharpEnvironmentManager.FlushAllCookiesSync();
        
        var cacheBase = GetWorkflowWebCacheDir(workflowsDir, workflowName);
        foreach (var n in nodes.OfType<WebNode>())
        {
            // Nếu node dùng Isolated profile với CustomCacheName, lưu profile đó
            if (string.Equals(n.CacheMode, "Isolated", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(n.CustomCacheName) &&
                !string.Equals(n.CustomCacheName, "Shared", StringComparison.OrdinalIgnoreCase))
            {
                var profileSrc = GetProfileCachePath(n.CustomCacheName);
                if (Directory.Exists(profileSrc))
                {
                    var profileDest = Path.Combine(cacheBase, "profiles", n.CustomCacheName);
                    try { CopyDirectory(profileSrc, profileDest); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.SaveWorkflowWebNodeCaches profile {n.CustomCacheName}: {ex.Message}");
                    }
                }
            }

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
            // Restore isolated profile if specified
            if (string.Equals(n.CacheMode, "Isolated", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(n.CustomCacheName) &&
                !string.Equals(n.CustomCacheName, "Shared", StringComparison.OrdinalIgnoreCase))
            {
                var profileSrc = Path.Combine(cacheBase, "profiles", n.CustomCacheName);
                if (Directory.Exists(profileSrc))
                {
                    var profileDest = GetProfileCachePath(n.CustomCacheName);
                    try { CopyDirectory(profileSrc, profileDest); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.RestoreWorkflowWebNodeCaches profile {n.CustomCacheName}: {ex.Message}");
                    }
                }
            }

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
            // Không return sớm — tiếp tục phía dưới để restore profile Isolated nếu có trong bundle
        }

        foreach (var n in nodes.OfType<WebNode>())
        {
            if (string.Equals(n.CacheMode, "Isolated", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(n.CustomCacheName) &&
                !string.Equals(n.CustomCacheName, "Shared", StringComparison.OrdinalIgnoreCase))
            {
                var profileSrc = Path.Combine(portableCacheRoot, "profiles", n.CustomCacheName);
                if (Directory.Exists(profileSrc))
                {
                    try { CopyDirectory(profileSrc, GetProfileCachePath(n.CustomCacheName)); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"WebNodeCacheHelper.RestorePortableWebCaches profile {n.CustomCacheName}: {ex.Message}");
                    }
                }
            }

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
