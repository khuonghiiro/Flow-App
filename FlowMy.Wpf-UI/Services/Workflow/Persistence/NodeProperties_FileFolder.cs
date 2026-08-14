using FlowMy.Models;
using FlowMy.Models.Nodes;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FlowMy.Services.Workflow;

public sealed partial class FileWorkflowPersistenceService
{
    // -- RESTORE (Deserialize) --

    private static void RestoreFileDownloadNodeProperties(FileDownloadNode fdNode, Dictionary<string, object> properties)
    {

            if (properties.TryGetValue("FileNameTemplate", out var fnt))
                fdNode.FileNameTemplate = fnt?.ToString() ?? fdNode.FileNameTemplate;
            if (properties.TryGetValue("MaxFileNameLength", out var mfnl) && mfnl != null && int.TryParse(mfnl.ToString(), out var mfn) && mfn >= 1)
                fdNode.MaxFileNameLength = mfn;
            if (properties.TryGetValue("AutoIncrementIfExists", out var aii) && aii != null && bool.TryParse(aii.ToString(), out var aib))
                fdNode.AutoIncrementIfExists = aib;
            if (properties.TryGetValue("RemoveDiacriticsFromFileName", out var rdfn) && rdfn != null && bool.TryParse(rdfn.ToString(), out var removeDia))
                fdNode.RemoveDiacriticsFromFileName = removeDia;
            if (properties.TryGetValue("DownloadUrl", out var du))
                fdNode.DownloadUrl = du?.ToString() ?? string.Empty;
            if (properties.TryGetValue("UrlSourceNodeId", out var usn))
                fdNode.UrlSourceNodeId = usn?.ToString();
            if (properties.TryGetValue("UrlSourceOutputKey", out var usk))
                fdNode.UrlSourceOutputKey = usk?.ToString();
            if (properties.TryGetValue("CurlCommand", out var cc))
                fdNode.CurlCommand = cc?.ToString() ?? string.Empty;
            if (properties.TryGetValue("CurlSourceNodeId", out var csn))
                fdNode.CurlSourceNodeId = csn?.ToString();
            if (properties.TryGetValue("CurlSourceOutputKey", out var csk))
                fdNode.CurlSourceOutputKey = csk?.ToString();
            if (properties.TryGetValue("DownloadFolderPath", out var dfp))
                fdNode.DownloadFolderPath = dfp?.ToString() ?? string.Empty;
            if (properties.TryGetValue("FolderSourceNodeId", out var fsn))
                fdNode.FolderSourceNodeId = fsn?.ToString();
            if (properties.TryGetValue("FolderSourceOutputKey", out var fsk))
                fdNode.FolderSourceOutputKey = fsk?.ToString();
            if (properties.TryGetValue("FileNameSourceNodeId", out var nfsn))
                fdNode.FileNameSourceNodeId = nfsn?.ToString();
            if (properties.TryGetValue("FileNameSourceOutputKey", out var nfsk))
                fdNode.FileNameSourceOutputKey = nfsk?.ToString();
            if (properties.TryGetValue("SaveAdditionalOutputFiles", out var saof) && saof != null && bool.TryParse(saof.ToString(), out var saofB))
                fdNode.SaveAdditionalOutputFiles = saofB;
            if (properties.TryGetValue("AdditionalOutputDefaultNameTemplate", out var aodnt))
                fdNode.AdditionalOutputDefaultNameTemplate = string.IsNullOrWhiteSpace(aodnt?.ToString()) ? null : aodnt.ToString();
            if (properties.TryGetValue("AdditionalOutputSaves", out var aosObj) && aosObj != null)
            {
                try
                {
                    var raw = aosObj is JsonElement je && je.ValueKind == JsonValueKind.String
                        ? je.GetString()
                        : aosObj.ToString();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        var list = JsonSerializer.Deserialize<List<FileDownloadAdditionalOutputSaveEntry>>(raw);
                        if (list != null)
                            fdNode.AdditionalOutputSaves = list;
                    }
                }
                catch { }
            }
    }

    private static void RestoreFolderFilePathsNodeProperties(FolderFilePathsNode ffpNode, Dictionary<string, object> properties)
    {

            if (properties.TryGetValue("FolderPath", out var fpObj))
                ffpNode.FolderPath = fpObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("FolderSourceNodeId", out var fsnFfp))
                ffpNode.FolderSourceNodeId = fsnFfp?.ToString();
            if (properties.TryGetValue("FolderSourceOutputKey", out var fskFfp))
                ffpNode.FolderSourceOutputKey = fskFfp?.ToString();
            if (properties.TryGetValue("RefreshFolderSourceNodeBeforeUse", out var rfsn) && rfsn != null && bool.TryParse(rfsn.ToString(), out var rfsnB))
                ffpNode.RefreshFolderSourceNodeBeforeUse = rfsnB;
            if (properties.TryGetValue("IncludeSubfolders", out var isub) && isub != null && bool.TryParse(isub.ToString(), out var isubB))
                ffpNode.IncludeSubfolders = isubB;
            if (properties.TryGetValue("ExtensionFilterText", out var eft))
                ffpNode.ExtensionFilterText = eft?.ToString() ?? string.Empty;
            if (properties.TryGetValue("ExtensionTags", out var etObj) && etObj != null)
            {
                try
                {
                    if (etObj is string etJson && !string.IsNullOrWhiteSpace(etJson))
                    {
                        var tags = JsonSerializer.Deserialize<List<string>>(etJson);
                        if (tags != null)
                        {
                            ffpNode.ExtensionTags.Clear();
                            foreach (var t in tags.Where(x => !string.IsNullOrWhiteSpace(x)))
                                ffpNode.ExtensionTags.Add(t.Trim());
                        }
                    }
                    else if (etObj is JsonElement etJe && etJe.ValueKind == JsonValueKind.Array)
                    {
                        var tags = JsonSerializer.Deserialize<List<string>>(etJe.GetRawText());
                        if (tags != null)
                        {
                            ffpNode.ExtensionTags.Clear();
                            foreach (var t in tags.Where(x => !string.IsNullOrWhiteSpace(x)))
                                ffpNode.ExtensionTags.Add(t.Trim());
                        }
                    }
                }
                catch { /* ignore */ }
            }
            if (properties.TryGetValue("ReadFileContents", out var rfc) && rfc != null && bool.TryParse(rfc.ToString(), out var rfcb))
                ffpNode.ReadFileContents = rfcb;
            if (properties.TryGetValue("ReadContentExtensionsText", out var rce))
                ffpNode.ReadContentExtensionsText = string.IsNullOrWhiteSpace(rce?.ToString()) ? ".txt" : rce.ToString()!;
    }

    private static void RestoreFolderNodeProperties(FolderNode folderNode, Dictionary<string, object> properties)
    {

            if (properties.TryGetValue("RootFolderPath", out var rfpObj))
                folderNode.RootFolderPath = rfpObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("RootFolderPresetKey", out var rfpkObj))
                folderNode.RootFolderPresetKey = rfpkObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("SubPathTemplate", out var sptObj))
                folderNode.SubPathTemplate = sptObj?.ToString() ?? string.Empty;
            if (properties.TryGetValue("KeyValueInputs", out var kviObj) && kviObj != null)
            {
                var list = new List<FolderKeyValueInput>();
                if (kviObj is JsonElement kviJe)
                {
                    if (kviJe.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in kviJe.EnumerateArray())
                        {
                            var kv = new FolderKeyValueInput();
                            if (e.TryGetProperty("SourceNodeId", out var sni)) kv.SourceNodeId = GetStringFromJsonValue(sni);
                            if (e.TryGetProperty("SourceOutputKey", out var sok)) kv.SourceOutputKey = GetStringFromJsonValue(sok);
                            if (e.TryGetProperty("ValueConfirm", out var vc)) kv.ValueConfirm = GetStringFromJsonValue(vc);
                            list.Add(kv);
                        }
                    }
                    else if (kviJe.ValueKind == JsonValueKind.String)
                    {
                        var str = kviJe.GetString();
                        if (!string.IsNullOrEmpty(str))
                        {
                            try
                            {
                                var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(str);
                                if (parsed != null)
                                    foreach (var d in parsed)
                                    {
                                        var kv = new FolderKeyValueInput();
                                        if (d.TryGetValue("SourceNodeId", out var sni)) kv.SourceNodeId = GetStringFromJsonValue(sni);
                                        if (d.TryGetValue("SourceOutputKey", out var sok)) kv.SourceOutputKey = GetStringFromJsonValue(sok);
                                        if (d.TryGetValue("ValueConfirm", out var vc)) kv.ValueConfirm = GetStringFromJsonValue(vc);
                                        list.Add(kv);
                                    }
                            }
                            catch { }
                        }
                    }
                }
                else if (kviObj is string kviStr && !string.IsNullOrEmpty(kviStr))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(kviStr);
                        if (parsed != null)
                            foreach (var d in parsed)
                            {
                                var kv = new FolderKeyValueInput();
                                if (d.TryGetValue("SourceNodeId", out var sni)) kv.SourceNodeId = GetStringFromJsonValue(sni);
                                if (d.TryGetValue("SourceOutputKey", out var sok)) kv.SourceOutputKey = GetStringFromJsonValue(sok);
                                if (d.TryGetValue("ValueConfirm", out var vc)) kv.ValueConfirm = GetStringFromJsonValue(vc);
                                list.Add(kv);
                            }
                    }
                    catch { }
                }
                if (list.Count > 0)
                {
                    folderNode.KeyValueInputs.Clear();
                    foreach (var kv in list)
                        folderNode.KeyValueInputs.Add(kv);
                }
            }
    }

    // -- GET (Serialize) --

    private static void GetFileDownloadNodeProperties(FileDownloadNode fdNode, Dictionary<string, object> dict)
    {

            dict["FileNameTemplate"] = fdNode.FileNameTemplate ?? string.Empty;
            dict["MaxFileNameLength"] = fdNode.MaxFileNameLength;
            dict["AutoIncrementIfExists"] = fdNode.AutoIncrementIfExists;
            dict["RemoveDiacriticsFromFileName"] = fdNode.RemoveDiacriticsFromFileName;
            dict["DownloadUrl"] = fdNode.DownloadUrl ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fdNode.UrlSourceNodeId))
                dict["UrlSourceNodeId"] = fdNode.UrlSourceNodeId;
            if (!string.IsNullOrWhiteSpace(fdNode.UrlSourceOutputKey))
                dict["UrlSourceOutputKey"] = fdNode.UrlSourceOutputKey;
            dict["CurlCommand"] = fdNode.CurlCommand ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fdNode.CurlSourceNodeId))
                dict["CurlSourceNodeId"] = fdNode.CurlSourceNodeId;
            if (!string.IsNullOrWhiteSpace(fdNode.CurlSourceOutputKey))
                dict["CurlSourceOutputKey"] = fdNode.CurlSourceOutputKey;
            dict["DownloadFolderPath"] = fdNode.DownloadFolderPath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fdNode.FolderSourceNodeId))
                dict["FolderSourceNodeId"] = fdNode.FolderSourceNodeId;
            if (!string.IsNullOrWhiteSpace(fdNode.FolderSourceOutputKey))
                dict["FolderSourceOutputKey"] = fdNode.FolderSourceOutputKey;
            if (!string.IsNullOrWhiteSpace(fdNode.FileNameSourceNodeId))
                dict["FileNameSourceNodeId"] = fdNode.FileNameSourceNodeId;
            if (!string.IsNullOrWhiteSpace(fdNode.FileNameSourceOutputKey))
                dict["FileNameSourceOutputKey"] = fdNode.FileNameSourceOutputKey;
            dict["SaveAdditionalOutputFiles"] = fdNode.SaveAdditionalOutputFiles;
            if (!string.IsNullOrWhiteSpace(fdNode.AdditionalOutputDefaultNameTemplate))
                dict["AdditionalOutputDefaultNameTemplate"] = fdNode.AdditionalOutputDefaultNameTemplate;
            dict["AdditionalOutputSaves"] = JsonSerializer.Serialize(
                fdNode.AdditionalOutputSaves ?? new List<FileDownloadAdditionalOutputSaveEntry>());
    }

    private static void GetFolderFilePathsNodeProperties(FolderFilePathsNode ffpNode, Dictionary<string, object> dict)
    {

            dict["FolderPath"] = ffpNode.FolderPath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(ffpNode.FolderSourceNodeId))
                dict["FolderSourceNodeId"] = ffpNode.FolderSourceNodeId;
            if (!string.IsNullOrWhiteSpace(ffpNode.FolderSourceOutputKey))
                dict["FolderSourceOutputKey"] = ffpNode.FolderSourceOutputKey;
            dict["RefreshFolderSourceNodeBeforeUse"] = ffpNode.RefreshFolderSourceNodeBeforeUse;
            dict["IncludeSubfolders"] = ffpNode.IncludeSubfolders;
            dict["ExtensionFilterText"] = ffpNode.ExtensionFilterText ?? string.Empty;
            dict["ExtensionTags"] = JsonSerializer.Serialize(
                (ffpNode.ExtensionTags ?? new List<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
            dict["ReadFileContents"] = ffpNode.ReadFileContents;
            dict["ReadContentExtensionsText"] = ffpNode.ReadContentExtensionsText ?? ".txt";
    }

    private static void GetFolderNodeProperties(FolderNode folderNode, Dictionary<string, object> dict)
    {

            if (!string.IsNullOrEmpty(folderNode.RootFolderPath))
                dict["RootFolderPath"] = folderNode.RootFolderPath;
            if (!string.IsNullOrEmpty(folderNode.RootFolderPresetKey))
                dict["RootFolderPresetKey"] = folderNode.RootFolderPresetKey;
            if (!string.IsNullOrEmpty(folderNode.SubPathTemplate))
                dict["SubPathTemplate"] = folderNode.SubPathTemplate;
            if (folderNode.KeyValueInputs != null && folderNode.KeyValueInputs.Count > 0)
            {
                var arr = folderNode.KeyValueInputs.Select(kv => new Dictionary<string, string?>
                {
                    ["SourceNodeId"] = kv.SourceNodeId,
                    ["SourceOutputKey"] = kv.SourceOutputKey,
                    ["ValueConfirm"] = kv.ValueConfirm
                }).ToList();
                dict["KeyValueInputs"] = JsonSerializer.Serialize(arr);
            }
    }

}
