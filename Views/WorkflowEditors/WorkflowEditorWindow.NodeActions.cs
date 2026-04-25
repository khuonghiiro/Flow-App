using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Workflow;
using Microsoft.VisualBasic;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        private void RequestEditNodeTitle(WorkflowNode node)
        {
            if (node == null) return;

            var current = node.Title ?? string.Empty;
            var input = Interaction.InputBox("Nhập tiêu đề node", "Sửa tiêu đề", current).Trim();
            if (string.IsNullOrWhiteSpace(input)) return;

            var old = node.Title;
            node.Title = input;

            // Trigger PropertyChanged cho InputNode và MouseEventNode để cập nhật UI
            if (node is InputNode inputNode)
            {
                inputNode.NotifyTitleChanged();
            }
            else if (node is StringSplitNode stringSplitNode)
            {
                stringSplitNode.NotifyTitleChanged();
            }
            else if (node is MouseEventNode mouseNode)
            {
                mouseNode.NotifyTitleChanged();
            }
            else if (node is HttpRequestNode httpRequestNode)
            {
                httpRequestNode.NotifyTitleChanged();
            }
            else if (node is FolderNode folderNode)
            {
                folderNode.NotifyTitleChanged();
            }
            else if (node is FileDownloadNode fileDlRename)
            {
                fileDlRename.NotifyTitleChanged();
            }
            else if (node is FolderFilePathsNode ffpRename)
            {
                ffpRename.NotifyTitleChanged();
            }
            else if (node is HtmlUiNode htmlUiNode)
            {
                htmlUiNode.NotifyTitleChanged();
            }
            else if (node is FlowOverwriteNode flowOverwriteNode)
            {
                flowOverwriteNode.NotifyTitleChanged();
            }

            // Update UI nếu node có chrome simple
            if (node.TitleTextBlockUI != null)
            {
                node.TitleTextBlockUI.Text = input;
            }

            // Best-effort: tìm TextBlock đang hiển thị title cũ để update
            if (node.Border != null && !string.IsNullOrWhiteSpace(old))
            {
                TryUpdateTextBlock(node.Border, old, input);
            }

            // Refresh dynamic data UI so ComboBox titles + resolved text update after rename
            _eventService.RefreshDynamicDataSourceSelectors();
        }

        private static bool TryUpdateTextBlock(DependencyObject root, string oldText, string newText)
        {
            if (root is System.Windows.Controls.TextBlock tb && tb.Text == oldText)
            {
                tb.Text = newText;
                return true;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (TryUpdateTextBlock(child, oldText, newText)) return true;
            }

            return false;
        }

        private void DuplicateNode(WorkflowNode source)
        {
            if (ViewModel == null) return;
            if (source is LoopBodyNode) return; // loop body là node ảo

            var clone = CreateDuplicateNodeInstance(source, offsetX: 30, offsetY: 30);
            if (clone == null) return;

            ViewModel.Nodes.Add(clone);
            ViewModel.SelectedNode = clone;
        }

        private void DuplicateNodeAtPosition(WorkflowNode source, double x, double y)
        {
            if (ViewModel == null) return;
            if (source is LoopBodyNode) return; // loop body là node ảo

            // Tính offset từ vị trí source đến vị trí mới
            var offsetX = x - source.X;
            var offsetY = y - source.Y;

            var clone = CreateDuplicateNodeInstance(source, offsetX, offsetY);
            if (clone == null) return;

            ViewModel.Nodes.Add(clone);
            ViewModel.SelectedNode = clone;
        }

        private WorkflowNode? CreateDuplicateNodeInstance(WorkflowNode source, double offsetX, double offsetY)
        {
            if (ViewModel == null) return null;

            // 1) Create a fresh instance via TemplateFactory (preferred)
            WorkflowNode? node = null;
            try
            {
                // TemplateFactory expects center-ish coordinates, but we override X/Y after create anyway.
                node = _templateFactory.Create(source.Type.ToString(), 0, 0);
            }
            catch
            {
                // Fallback: create by runtime type
                try
                {
                    node = (WorkflowNode?)Activator.CreateInstance(source.GetType());
                }
                catch
                {
                    node = null;
                }
            }

            if (node == null) return null;

            // 2) Core props (keep new unique Id from factory/ctor when possible)
            node.X = source.X + offsetX;
            node.Y = source.Y + offsetY;
            node.NodeBrush = source.NodeBrush;
            node.ColorKey = source.ColorKey;
            node.Type = source.Type;
            node.ConditionalVisualMode = source.ConditionalVisualMode;

            node.Condition = source.Condition;
            node.Key = source.Key;
            node.MouseEvent = source.MouseEvent;
            node.TargetElement = source.TargetElement;

            node.DynamicInputs = CloneDynamicDataPorts(source.DynamicInputs);
            node.DynamicOutputs = CloneDynamicDataPorts(source.DynamicOutputs);

            // 3) Generate unique title with "- copy {x}" suffix if needed
            var baseTitle = source.Title ?? string.Empty;
            var newTitle = GenerateUniqueTitle(baseTitle);
            node.Title = newTitle;

            // Trigger PropertyChanged / copy extra properties cho từng loại node
            if (source is StringSplitNode srcStringSplit && node is StringSplitNode dstStringSplit)
            {
                // Copy StringSplitNode properties
                dstStringSplit.RegexPattern = srcStringSplit.RegexPattern;
                dstStringSplit.OutputKey = srcStringSplit.OutputKey;
                dstStringSplit.TitleDisplayMode = srcStringSplit.TitleDisplayMode;
                dstStringSplit.TitleColorMode = srcStringSplit.TitleColorMode;
                dstStringSplit.TitleColorKey = srcStringSplit.TitleColorKey;

                // Notify title changed để renderer update UI
                dstStringSplit.NotifyTitleChanged();
            }
            else if (source is ListOutNode srcListOut && node is ListOutNode dstListOut)
            {
                // Copy ListOutNode properties
                dstListOut.TitleDisplayMode = srcListOut.TitleDisplayMode;
                dstListOut.TitleColorMode = srcListOut.TitleColorMode;
                dstListOut.TitleColorKey = srcListOut.TitleColorKey;
                
                // Deep copy OutputMappings
                dstListOut.OutputMappings = srcListOut.OutputMappings
                    .Select(m => new OutputMapping
                    {
                        NewKey = m.NewKey,
                        SourceNodeId = m.SourceNodeId,
                        SourceOutputKey = m.SourceOutputKey
                    })
                    .ToList();
                
                // Rebuild DynamicOutputs based on copied mappings
                dstListOut.RebuildDynamicOutputs();
                
                // Notify title changed để renderer update UI
                dstListOut.NotifyTitleChanged();
            }
            else if (source is AssignDataNode srcAssign && node is AssignDataNode dstAssign)
            {
                dstAssign.Assignments.Clear();
                foreach (var a in srcAssign.Assignments)
                {
                    dstAssign.Assignments.Add(new AssignDataAssignment
                    {
                        SourceNodeId = a.SourceNodeId,
                        SourceOutputKey = a.SourceOutputKey,
                        TargetNodeId = a.TargetNodeId,
                        TargetKey = a.TargetKey,
                        RefreshSourceBeforeUse = a.RefreshSourceBeforeUse
                    });
                }
                dstAssign.TitleColorMode = srcAssign.TitleColorMode;
                dstAssign.TitleColorKey = srcAssign.TitleColorKey;
                dstAssign.NotifyTitleChanged();
            }
            else if (source is BodyContainerNode srcBody && node is BodyContainerNode dstBody)
            {
                dstBody.BodyWidth = srcBody.BodyWidth;
                dstBody.BodyHeight = srcBody.BodyHeight;
                dstBody.BodyBackgroundColorHex = srcBody.BodyBackgroundColorHex;
                dstBody.BodyBorderColorHex = srcBody.BodyBorderColorHex;
                dstBody.UseUnifiedColors = srcBody.UseUnifiedColors;
                dstBody.BackgroundOpacityPercent = srcBody.BackgroundOpacityPercent;
                dstBody.LockInnerNodes = srcBody.LockInnerNodes;
                dstBody.TitleDisplayMode = srcBody.TitleDisplayMode;
                dstBody.TitleColorMode = srcBody.TitleColorMode;
                dstBody.TitleColorKey = srcBody.TitleColorKey;
                dstBody.NotifyTitleChanged();
            }
            else if (source is MediaGalleryNode srcGallery && node is MediaGalleryNode dstGallery)
            {
                dstGallery.Width = srcGallery.Width;
                dstGallery.Height = srcGallery.Height;
                dstGallery.FrameDisplayWidth = srcGallery.FrameDisplayWidth;
                dstGallery.FrameDisplayHeight = srcGallery.FrameDisplayHeight;
                dstGallery.TitleKeyTemplate = srcGallery.TitleKeyTemplate ?? "";
                dstGallery.ImageUrlKeyTemplate = srcGallery.ImageUrlKeyTemplate ?? "";
                dstGallery.VideoUrlKeyTemplate = srcGallery.VideoUrlKeyTemplate ?? "";
                dstGallery.GroupArrayKey = srcGallery.GroupArrayKey ?? "";
                dstGallery.GroupTitleKey = srcGallery.GroupTitleKey ?? "";
                dstGallery.GroupItemsKey = srcGallery.GroupItemsKey ?? "";
                dstGallery.FolderSaveImages = srcGallery.FolderSaveImages ?? "";
                dstGallery.FolderSourceNodeId = srcGallery.FolderSourceNodeId;
                dstGallery.FolderSourceOutputKey = srcGallery.FolderSourceOutputKey;
                dstGallery.FolderSaveVideos = srcGallery.FolderSaveVideos ?? "";
                dstGallery.FolderSourceNodeIdVideo = srcGallery.FolderSourceNodeIdVideo;
                dstGallery.FolderSourceOutputKeyVideo = srcGallery.FolderSourceOutputKeyVideo;
                dstGallery.JsonSourceNodeId = srcGallery.JsonSourceNodeId;
                dstGallery.JsonSourceOutputKey = srcGallery.JsonSourceOutputKey;
                dstGallery.ItemClickPreviewMode = srcGallery.ItemClickPreviewMode;
                dstGallery.DisplayMode = srcGallery.DisplayMode;
                dstGallery.TitleColorMode = srcGallery.TitleColorMode;
                dstGallery.TitleColorKey = srcGallery.TitleColorKey;
                dstGallery.NotifyTitleChanged();
            }
            else if (source is ImageProcessingNode srcImage && node is ImageProcessingNode dstImage)
            {
                dstImage.Width = srcImage.Width;
                dstImage.Height = srcImage.Height;
                dstImage.InputMode = srcImage.InputMode;

                dstImage.ImageUrl = srcImage.ImageUrl ?? string.Empty;
                dstImage.ImageUrlSourceNodeId = srcImage.ImageUrlSourceNodeId;
                dstImage.ImageUrlSourceOutputKey = srcImage.ImageUrlSourceOutputKey;

                dstImage.ImageBase64 = srcImage.ImageBase64 ?? string.Empty;
                dstImage.ImageBase64SourceNodeId = srcImage.ImageBase64SourceNodeId;
                dstImage.ImageBase64SourceOutputKey = srcImage.ImageBase64SourceOutputKey;

                dstImage.PreferGpu = srcImage.PreferGpu;
                dstImage.FfmpegFilter = srcImage.FfmpegFilter ?? string.Empty;

                dstImage.TitleDisplayMode = srcImage.TitleDisplayMode;
                dstImage.TitleColorMode = srcImage.TitleColorMode;
                dstImage.TitleColorKey = srcImage.TitleColorKey;

                dstImage.NotifyTitleChanged();
            }
            else if (source is VideoProcessingNode srcVideo && node is VideoProcessingNode dstVideo)
            {
                dstVideo.Width = srcVideo.Width;
                dstVideo.Height = srcVideo.Height;
                dstVideo.VideoSourceNodeId = srcVideo.VideoSourceNodeId;
                dstVideo.VideoSourceOutputKey = srcVideo.VideoSourceOutputKey;
                dstVideo.VideoPath = srcVideo.VideoPath;
                dstVideo.OutputFolderSourceNodeId = srcVideo.OutputFolderSourceNodeId;
                dstVideo.OutputFolderSourceOutputKey = srcVideo.OutputFolderSourceOutputKey;
                dstVideo.OutputBase64 = srcVideo.OutputBase64;
                dstVideo.PreferGpu = srcVideo.PreferGpu;
                dstVideo.PreferredHwAccel = srcVideo.PreferredHwAccel;
                dstVideo.SourceFps = srcVideo.SourceFps;
                dstVideo.ExtractFps = srcVideo.ExtractFps;
                dstVideo.Brightness = srcVideo.Brightness;
                dstVideo.Contrast = srcVideo.Contrast;
                dstVideo.Saturation = srcVideo.Saturation;
                dstVideo.Hue = srcVideo.Hue;
                dstVideo.AudioTracks.Clear();
                foreach (var track in srcVideo.AudioTracks)
                {
                    dstVideo.AudioTracks.Add(new VideoAudioTrackConfig
                    {
                        SourceNodeId = track.SourceNodeId,
                        SourceOutputKey = track.SourceOutputKey,
                        VolumePercent = track.VolumePercent,
                        ShorterMode = track.ShorterMode,
                        LongerMode = track.LongerMode
                    });
                }
                dstVideo.TitleDisplayMode = srcVideo.TitleDisplayMode;
                dstVideo.TitleColorMode = srcVideo.TitleColorMode;
                dstVideo.TitleColorKey = srcVideo.TitleColorKey;
                dstVideo.NotifyTitleChanged();
            }
            else if (source is WebNode srcWeb && node is WebNode dstWeb)
            {
                dstWeb.Width = srcWeb.Width;
                dstWeb.Height = srcWeb.Height;
                dstWeb.ExtractUrl = srcWeb.ExtractUrl ?? "";
                dstWeb.ExtractRequestMethod = srcWeb.ExtractRequestMethod ?? "GET";
                dstWeb.ExtractStatusCode = srcWeb.ExtractStatusCode ?? "200";
                dstWeb.BlockingRules.Clear();
                foreach (var rule in srcWeb.BlockingRules)
                {
                    dstWeb.BlockingRules.Add(new WebBlockingRule { UrlPattern = rule.UrlPattern });
                }
                dstWeb.TitleDisplayMode = srcWeb.TitleDisplayMode;
                dstWeb.TitleColorMode = srcWeb.TitleColorMode;
                dstWeb.TitleColorKey = srcWeb.TitleColorKey;
                dstWeb.EnableSleepMode = srcWeb.EnableSleepMode;
                dstWeb.SleepIdleTimeoutValue = srcWeb.SleepIdleTimeoutValue;
                dstWeb.SleepIdleTimeoutUnit = srcWeb.SleepIdleTimeoutUnit;
                dstWeb.RequestInterceptRules.Clear();
                foreach (var r in srcWeb.RequestInterceptRules)
                {
                    dstWeb.RequestInterceptRules.Add(new WebRequestInterceptRule
                    {
                        MatchUrlPattern = r.MatchUrlPattern,
                        ReplaceUrlValue = r.ReplaceUrlValue,
                        ReplaceUrlSourceNodeId = r.ReplaceUrlSourceNodeId,
                        ReplaceUrlSourceOutputKey = r.ReplaceUrlSourceOutputKey,
                        ReplaceParamsValue = r.ReplaceParamsValue,
                        ReplaceParamsSourceNodeId = r.ReplaceParamsSourceNodeId,
                        ReplaceParamsSourceOutputKey = r.ReplaceParamsSourceOutputKey,
                        ReplaceBodyValue = r.ReplaceBodyValue,
                        ReplaceBodySourceNodeId = r.ReplaceBodySourceNodeId,
                        ReplaceBodySourceOutputKey = r.ReplaceBodySourceOutputKey
                    });
                }
                if (dstWeb.DynamicInputs != null && dstWeb.DynamicInputs.Count > 0 && srcWeb.DynamicInputs != null && srcWeb.DynamicInputs.Count > 0)
                {
                    var di = dstWeb.DynamicInputs[0];
                    var si = srcWeb.DynamicInputs[0];
                    di.SelectedSourceNodeId = si.SelectedSourceNodeId;
                    di.SelectedSourceOutputKey = si.SelectedSourceOutputKey;
                }

                // Copy JS injection config
                dstWeb.JsSources = srcWeb.JsSources != null
                    ? srcWeb.JsSources.Select(m => new WebJsSourceMapping
                    {
                        SourceNodeId = m.SourceNodeId,
                        SourceOutputKey = m.SourceOutputKey
                    }).ToList()
                    : new List<WebJsSourceMapping>();

                WebNodeCacheHelper.CopyWebNodeCache(srcWeb.Id, dstWeb.Id);
                dstWeb.NotifyTitleChanged();
            }
            else if (source is CodeNode srcCode && node is CodeNode dstCode)
            {
                dstCode.InputMappings = srcCode.InputMappings != null
                    ? srcCode.InputMappings.Select(m => new CodeInputMapping
                    {
                        SourceNodeId = m.SourceNodeId,
                        SourceOutputKey = m.SourceOutputKey,
                        InputKeyOverride = m.InputKeyOverride
                    }).ToList()
                    : new List<CodeInputMapping> { new CodeInputMapping() };
                dstCode.ScriptCode = srcCode.ScriptCode ?? string.Empty;
                dstCode.OutputKeys = srcCode.OutputKeys != null ? new List<string>(srcCode.OutputKeys) : new List<string> { "result" };
                dstCode.RebuildDynamicOutputs();
                dstCode.TitleDisplayMode = srcCode.TitleDisplayMode;
                dstCode.TitleColorMode = srcCode.TitleColorMode;
                dstCode.TitleColorKey = srcCode.TitleColorKey;
                dstCode.NotifyTitleChanged();
            }
            else if (source is HtmlUiNode srcHtmlUi && node is HtmlUiNode dstHtmlUi)
            {
                dstHtmlUi.HtmlCode = srcHtmlUi.HtmlCode ?? string.Empty;
                dstHtmlUi.JsCode = srcHtmlUi.JsCode ?? string.Empty;
                dstHtmlUi.CssCode = srcHtmlUi.CssCode ?? string.Empty;
                dstHtmlUi.ParamsCode = srcHtmlUi.ParamsCode ?? string.Empty;
                dstHtmlUi.InputMappings = srcHtmlUi.InputMappings != null
                    ? srcHtmlUi.InputMappings.Select(m => new CodeInputMapping
                    {
                        SourceNodeId = m.SourceNodeId,
                        SourceOutputKey = m.SourceOutputKey,
                        InputKeyOverride = m.InputKeyOverride,
                        ShouldReExecute = m.ShouldReExecute,
                        AutoRefreshEnabled = m.AutoRefreshEnabled,
                        AutoRefreshInterval = m.AutoRefreshInterval,
                        AutoRefreshUnit = m.AutoRefreshUnit
                    }).ToList()
                    : new List<CodeInputMapping> { new CodeInputMapping() };
                dstHtmlUi.OutputKeys = srcHtmlUi.OutputKeys != null ? new List<string>(srcHtmlUi.OutputKeys) : new List<string> { "result" };
                dstHtmlUi.RebuildDynamicOutputs();
                dstHtmlUi.Width = srcHtmlUi.Width;
                dstHtmlUi.Height = srcHtmlUi.Height;
                dstHtmlUi.CssZoom = srcHtmlUi.CssZoom;
                dstHtmlUi.AutoReloadOnDialogClose = srcHtmlUi.AutoReloadOnDialogClose;
                dstHtmlUi.EnableSleepMode = srcHtmlUi.EnableSleepMode;
                dstHtmlUi.SleepIdleTimeoutValue = srcHtmlUi.SleepIdleTimeoutValue;
                dstHtmlUi.SleepIdleTimeoutUnit = srcHtmlUi.SleepIdleTimeoutUnit;
                dstHtmlUi.UseWebTab = srcHtmlUi.UseWebTab;
                dstHtmlUi.WebTabUrl = srcHtmlUi.WebTabUrl;
                dstHtmlUi.WebTabCookieSourceNodeId = srcHtmlUi.WebTabCookieSourceNodeId;
                dstHtmlUi.WebTabCookieSourceOutputKey = srcHtmlUi.WebTabCookieSourceOutputKey;
                dstHtmlUi.WebTabAutoRefreshEnabled = srcHtmlUi.WebTabAutoRefreshEnabled;
                dstHtmlUi.WebTabAutoRefreshInterval = srcHtmlUi.WebTabAutoRefreshInterval;
                dstHtmlUi.WebTabAutoRefreshUnit = srcHtmlUi.WebTabAutoRefreshUnit ?? "ms";
                dstHtmlUi.OfflineAssets = srcHtmlUi.OfflineAssets != null
                    ? srcHtmlUi.OfflineAssets.Select(a => new HtmlOfflineAsset
                    {
                        Id = a.Id,
                        Title = a.Title,
                        Description = a.Description,
                        SourceUrl = a.SourceUrl,
                        LocalFileName = a.LocalFileName,
                        AssetType = a.AssetType,
                        IsEnabled = a.IsEnabled
                    }).ToList()
                    : new List<HtmlOfflineAsset>();
                dstHtmlUi.AsyncDataSources = srcHtmlUi.AsyncDataSources != null
                    ? srcHtmlUi.AsyncDataSources.Select(a => new AsyncDataSource
                    {
                        SourceNodeId = a.SourceNodeId,
                        SourceOutputKey = a.SourceOutputKey,
                        ReceiverKey = a.ReceiverKey
                    }).ToList()
                    : new List<AsyncDataSource>();
                dstHtmlUi.TitleDisplayMode = srcHtmlUi.TitleDisplayMode;
                dstHtmlUi.TitleColorMode = srcHtmlUi.TitleColorMode;
                dstHtmlUi.TitleColorKey = srcHtmlUi.TitleColorKey;
                dstHtmlUi.NotifyTitleChanged();
            }
            else if (source is FolderNode srcFolder && node is FolderNode dstFolder)
            {
                dstFolder.RootFolderPath = srcFolder.RootFolderPath ?? string.Empty;
                dstFolder.SubPathTemplate = srcFolder.SubPathTemplate ?? string.Empty;
                dstFolder.KeyValueInputs.Clear();
                if (srcFolder.KeyValueInputs != null)
                {
                    foreach (var kv in srcFolder.KeyValueInputs)
                    {
                        dstFolder.KeyValueInputs.Add(new FolderKeyValueInput
                        {
                            SourceNodeId = kv.SourceNodeId,
                            SourceOutputKey = kv.SourceOutputKey,
                            ValueConfirm = kv.ValueConfirm
                        });
                    }
                }
                dstFolder.TitleDisplayMode = srcFolder.TitleDisplayMode;
                dstFolder.TitleColorMode = srcFolder.TitleColorMode;
                dstFolder.TitleColorKey = srcFolder.TitleColorKey;
                dstFolder.NotifyTitleChanged();
            }
            else if (source is FolderFilePathsNode srcFfp && node is FolderFilePathsNode dstFfp)
            {
                dstFfp.FolderPath = srcFfp.FolderPath ?? string.Empty;
                dstFfp.FolderSourceNodeId = srcFfp.FolderSourceNodeId;
                dstFfp.FolderSourceOutputKey = srcFfp.FolderSourceOutputKey;
                dstFfp.RefreshFolderSourceNodeBeforeUse = srcFfp.RefreshFolderSourceNodeBeforeUse;
                dstFfp.IncludeSubfolders = srcFfp.IncludeSubfolders;
                dstFfp.ExtensionFilterText = srcFfp.ExtensionFilterText ?? string.Empty;
                dstFfp.ExtensionTags.Clear();
                foreach (var t in srcFfp.ExtensionTags)
                    dstFfp.ExtensionTags.Add(t);
                dstFfp.ReadFileContents = srcFfp.ReadFileContents;
                dstFfp.ReadContentExtensionsText = srcFfp.ReadContentExtensionsText ?? ".txt";
                dstFfp.TitleDisplayMode = srcFfp.TitleDisplayMode;
                dstFfp.TitleColorMode = srcFfp.TitleColorMode;
                dstFfp.TitleColorKey = srcFfp.TitleColorKey;
                dstFfp.NotifyTitleChanged();
            }
            else if (source is FileDownloadNode srcFd && node is FileDownloadNode dstFd)
            {
                dstFd.FileNameTemplate = srcFd.FileNameTemplate;
                dstFd.MaxFileNameLength = srcFd.MaxFileNameLength;
                dstFd.AutoIncrementIfExists = srcFd.AutoIncrementIfExists;
                dstFd.RemoveDiacriticsFromFileName = srcFd.RemoveDiacriticsFromFileName;
                dstFd.DownloadUrl = srcFd.DownloadUrl;
                dstFd.UrlSourceNodeId = srcFd.UrlSourceNodeId;
                dstFd.UrlSourceOutputKey = srcFd.UrlSourceOutputKey;
                dstFd.CurlCommand = srcFd.CurlCommand;
                dstFd.CurlSourceNodeId = srcFd.CurlSourceNodeId;
                dstFd.CurlSourceOutputKey = srcFd.CurlSourceOutputKey;
                dstFd.DownloadFolderPath = srcFd.DownloadFolderPath;
                dstFd.FolderSourceNodeId = srcFd.FolderSourceNodeId;
                dstFd.FolderSourceOutputKey = srcFd.FolderSourceOutputKey;
                dstFd.FileNameSourceNodeId = srcFd.FileNameSourceNodeId;
                dstFd.FileNameSourceOutputKey = srcFd.FileNameSourceOutputKey;
                dstFd.SaveAdditionalOutputFiles = srcFd.SaveAdditionalOutputFiles;
                dstFd.AdditionalOutputDefaultNameTemplate = srcFd.AdditionalOutputDefaultNameTemplate;
                dstFd.AdditionalOutputSaves = (srcFd.AdditionalOutputSaves ?? new List<FileDownloadAdditionalOutputSaveEntry>())
                    .Select(e => new FileDownloadAdditionalOutputSaveEntry
                    {
                        SourceNodeId = e.SourceNodeId,
                        SourceOutputKey = e.SourceOutputKey,
                        NameTemplate = e.NameTemplate,
                        SaveFormat = e.SaveFormat
                    })
                    .ToList();
                dstFd.TitleDisplayMode = srcFd.TitleDisplayMode;
                dstFd.TitleColorMode = srcFd.TitleColorMode;
                dstFd.TitleColorKey = srcFd.TitleColorKey;
                dstFd.NotifyTitleChanged();
            }
            else if (node is InputNode inputNode)
            {
                inputNode.NotifyTitleChanged();
            }
            else if (node is MouseEventNode mouseNode)
            {
                mouseNode.NotifyTitleChanged();
            }

            // 4) Ports + conditional branches
            if (source is LoopNode loopSrc && node is LoopNode loopDst)
            {
                // Copy loop specific values
                loopDst.LoopType = loopSrc.LoopType;
                loopDst.RepeatCount = loopSrc.RepeatCount;
                loopDst.StartIndex = loopSrc.StartIndex;
                loopDst.EndIndex = loopSrc.EndIndex;
                loopDst.ArrayInputKey = loopSrc.ArrayInputKey;
                loopDst.InputType = loopSrc.InputType;
                
                // Copy title color
                loopDst.TitleColorMode = loopSrc.TitleColorMode;
                loopDst.TitleColorKey = loopSrc.TitleColorKey;

                // Copy loop body sizing/position (independent node)
                loopDst.LoopBodyNode.Width = loopSrc.LoopBodyNode.Width;
                loopDst.LoopBodyNode.Height = loopSrc.LoopBodyNode.Height;
                loopDst.LoopBodyNode.X = loopSrc.LoopBodyNode.X + offsetX;
                loopDst.LoopBodyNode.Y = loopSrc.LoopBodyNode.Y + offsetY;

                // Ports: preserve semantic ports (LoopNode*)
                CopyPortSettingsById(loopSrc.Ports, loopDst.Ports);

                // Copy any extra custom ports user added
                var existingIds = new HashSet<string>(loopDst.Ports.Select(p => p.Id));
                foreach (var extra in loopSrc.Ports.Where(p => !existingIds.Contains(p.Id)))
                {
                    loopDst.Ports.Add(ClonePort(extra));
                }
                
                // Notify title changed để renderer update UI
                loopDst.NotifyTitleChanged();
            }
            else
            {
                // General ports clone (new IDs except reserved)
                var portMap = new Dictionary<NodePort, NodePort>();
                node.Ports = ClonePorts(source.Ports, portMap);

                node.ConditionalBranches = CloneConditionalBranches(source.ConditionalBranches, portMap);
            }

            // Copy AsyncTaskBranches và RunInParallel nếu là AsyncTaskNode
            if (source is AsyncTaskNode srcAsyncTask && node is AsyncTaskNode dstAsyncTask)
            {
                // Copy RunInParallel property
                dstAsyncTask.RunInParallel = srcAsyncTask.RunInParallel;
                dstAsyncTask.UiPresentationMode = srcAsyncTask.UiPresentationMode;
                dstAsyncTask.DispatchLoopType = srcAsyncTask.DispatchLoopType;
                dstAsyncTask.RepeatCount = srcAsyncTask.RepeatCount;
                dstAsyncTask.StartIndex = srcAsyncTask.StartIndex;
                dstAsyncTask.EndIndex = srcAsyncTask.EndIndex;
                dstAsyncTask.ReadResultsInBody = srcAsyncTask.ReadResultsInBody;

                if (srcAsyncTask.UiPresentationMode == AsyncTaskUiPresentationMode.LoopLikeDispatch)
                {
                    // Source đang ở loop-like: đảm bảo clone cũng có loop-like ports + body trước khi copy layout.
                    _templateFactory.ConfigureAsyncTaskLoopLikePorts(dstAsyncTask);

                    if (srcAsyncTask.AsyncTaskBodyNode != null && dstAsyncTask.AsyncTaskBodyNode != null)
                    {
                        dstAsyncTask.AsyncTaskBodyNode.X = srcAsyncTask.AsyncTaskBodyNode.X + offsetX;
                        dstAsyncTask.AsyncTaskBodyNode.Y = srcAsyncTask.AsyncTaskBodyNode.Y + offsetY;
                        dstAsyncTask.AsyncTaskBodyNode.Width = srcAsyncTask.AsyncTaskBodyNode.Width;
                        dstAsyncTask.AsyncTaskBodyNode.Height = srcAsyncTask.AsyncTaskBodyNode.Height;
                    }
                }
                
                // Copy AsyncTaskBranches
                if (srcAsyncTask.AsyncTaskBranches != null && srcAsyncTask.AsyncTaskBranches.Count > 0)
                {
                    // Tạo map từ port cũ sang port mới để clone branches
                    var portMap = new Dictionary<NodePort, NodePort>();
                    foreach (var oldPort in source.Ports)
                    {
                        var newPort = node.Ports.FirstOrDefault(p => p.IsInput == oldPort.IsInput && p.Position == oldPort.Position);
                        if (newPort != null)
                        {
                            portMap[oldPort] = newPort;
                        }
                    }
                    dstAsyncTask.AsyncTaskBranches = CloneAsyncTaskBranches(srcAsyncTask.AsyncTaskBranches, portMap);
                }
            }

            // 5) Type-specific clone - copy ALL properties exactly
            if (source is ScreenPositionPickerNode srcPos && node is ScreenPositionPickerNode dstPos)
            {
                dstPos.HasPosition = srcPos.HasPosition;
                dstPos.SelectedPosition = srcPos.SelectedPosition;
            }

            if (source is ScreenCaptureNode srcCap && node is ScreenCaptureNode dstCap)
            {
                dstCap.CaptureX = srcCap.CaptureX;
                dstCap.CaptureY = srcCap.CaptureY;
                dstCap.CaptureWidth = srcCap.CaptureWidth;
                dstCap.CaptureHeight = srcCap.CaptureHeight;
                dstCap.CapturedImage = srcCap.CapturedImage; // Copy image reference
            }

            if (source is KeyPressEventNode srcKey && node is KeyPressEventNode dstKey)
            {
                dstKey.RepeatCount = srcKey.RepeatCount;
                dstKey.Key = srcKey.Key;
                dstKey.TitleDisplayMode = srcKey.TitleDisplayMode;
                dstKey.TitleColorMode = srcKey.TitleColorMode;
                dstKey.TitleColorKey = srcKey.TitleColorKey;
                dstKey.PressDelayMs = srcKey.PressDelayMs;
            }

            if (source is HotkeyPressEventNode srcHotkey && node is HotkeyPressEventNode dstHotkey)
            {
                dstHotkey.RepeatCount = srcHotkey.RepeatCount;
                dstHotkey.Key = srcHotkey.Key;
                dstHotkey.TitleDisplayMode = srcHotkey.TitleDisplayMode;
                dstHotkey.TitleColorMode = srcHotkey.TitleColorMode;
                dstHotkey.TitleColorKey = srcHotkey.TitleColorKey;
                dstHotkey.PressDelayMs = srcHotkey.PressDelayMs;
            }

            if (source is MouseEventNode srcMouse && node is MouseEventNode dstMouse)
            {
                dstMouse.MouseButton = srcMouse.MouseButton;
                dstMouse.RepeatCount = srcMouse.RepeatCount;
                dstMouse.HoldDuration = srcMouse.HoldDuration;
                dstMouse.ScrollSpeed = srcMouse.ScrollSpeed;
                dstMouse.TitleDisplayMode = srcMouse.TitleDisplayMode;
                dstMouse.TitleColorMode = srcMouse.TitleColorMode;
                dstMouse.TitleColorKey = srcMouse.TitleColorKey;
            }

            if (source is InputNode srcInput && node is InputNode dstInput)
            {
                dstInput.Key = srcInput.Key;
                dstInput.Value = srcInput.Value;
                dstInput.DataType = srcInput.DataType;
                dstInput.TitleDisplayMode = srcInput.TitleDisplayMode;
                dstInput.TitleColorMode = srcInput.TitleColorMode;
                dstInput.TitleColorKey = srcInput.TitleColorKey;
                // Copy ArrayValues nếu có
                if (srcInput.ArrayValues != null && srcInput.ArrayValues.Count > 0)
                {
                    dstInput.ArrayValues = new List<string>(srcInput.ArrayValues);
                }
                else
                {
                    dstInput.ArrayValues = new List<string>();
                }
            }

            if (source is DelayNode srcDelay && node is DelayNode dstDelay)
            {
                dstDelay.DelayMilliseconds = srcDelay.DelayMilliseconds;
                dstDelay.DelayUnit = srcDelay.DelayUnit;
                dstDelay.DelayValue = srcDelay.DelayValue;
                dstDelay.TimingMode = srcDelay.TimingMode;
                dstDelay.RandomMinValue = srcDelay.RandomMinValue;
                dstDelay.RandomMaxValue = srcDelay.RandomMaxValue;
                dstDelay.DelaySourceNodeId = srcDelay.DelaySourceNodeId;
                dstDelay.DelaySourceOutputKey = srcDelay.DelaySourceOutputKey;
                dstDelay.TitleDisplayMode = srcDelay.TitleDisplayMode;
                dstDelay.TitleColorMode = srcDelay.TitleColorMode;
                dstDelay.TitleColorKey = srcDelay.TitleColorKey;
            }


            if (source is OutputNode srcOutput && node is OutputNode dstOutput)
            {
                // Copy OutputKey
                dstOutput.OutputKey = srcOutput.OutputKey;

                // Copy FormatString
                dstOutput.FormatString = srcOutput.FormatString;

                // Copy InputVariables (clone list để tránh reference sharing)
                if (srcOutput.InputVariables != null && srcOutput.InputVariables.Count > 0)
                {
                    dstOutput.InputVariables = srcOutput.InputVariables.Select(v => new InputVariable
                    {
                        VariableKey = v.VariableKey,
                        SourceNodeId = v.SourceNodeId,
                        SourceOutputKey = v.SourceOutputKey
                    }).ToList();
                }
                else
                {
                    dstOutput.InputVariables = new List<InputVariable>();
                }
                dstOutput.TitleDisplayMode = srcOutput.TitleDisplayMode;
                dstOutput.TitleColorMode = srcOutput.TitleColorMode;
                dstOutput.TitleColorKey = srcOutput.TitleColorKey;
            }
            else if (source is NotificationNode srcNotification && node is NotificationNode dstNotification)
            {
                dstNotification.TitleDisplayMode = srcNotification.TitleDisplayMode;
                dstNotification.TitleColorMode = srcNotification.TitleColorMode;
                dstNotification.TitleColorKey = srcNotification.TitleColorKey;

                dstNotification.DefaultDurationSeconds = srcNotification.DefaultDurationSeconds;

                if (srcNotification.TitleInput != null)
                {
                    dstNotification.TitleInput = new InputVariable
                    {
                        VariableKey = srcNotification.TitleInput.VariableKey,
                        SourceNodeId = srcNotification.TitleInput.SourceNodeId,
                        SourceOutputKey = srcNotification.TitleInput.SourceOutputKey
                    };
                }

                if (srcNotification.ContentInput != null)
                {
                    dstNotification.ContentInput = new InputVariable
                    {
                        VariableKey = srcNotification.ContentInput.VariableKey,
                        SourceNodeId = srcNotification.ContentInput.SourceNodeId,
                        SourceOutputKey = srcNotification.ContentInput.SourceOutputKey
                    };
                }

                if (srcNotification.DurationInput != null)
                {
                    dstNotification.DurationInput = new InputVariable
                    {
                        VariableKey = srcNotification.DurationInput.VariableKey,
                        SourceNodeId = srcNotification.DurationInput.SourceNodeId,
                        SourceOutputKey = srcNotification.DurationInput.SourceOutputKey
                    };
                }

            dstNotification.StaticTitle = srcNotification.StaticTitle;
            dstNotification.StaticContent = srcNotification.StaticContent;

            dstNotification.ToastTitleColorKey = srcNotification.ToastTitleColorKey;
            dstNotification.ToastContentColorKey = srcNotification.ToastContentColorKey;
            dstNotification.ToastBackgroundColorKey = srcNotification.ToastBackgroundColorKey;
            dstNotification.ToastBackgroundOpacity = srcNotification.ToastBackgroundOpacity;
            }

            if (source is HttpRequestNode srcHttp && node is HttpRequestNode dstHttp)
            {
                // Copy HttpRequestNode properties
                dstHttp.Url = srcHttp.Url;
                dstHttp.HttpMethod = srcHttp.HttpMethod;
                dstHttp.AuthType = srcHttp.AuthType;
                dstHttp.BodyType = srcHttp.BodyType;
                dstHttp.RawBody = srcHttp.RawBody;
                dstHttp.AuthUsername = srcHttp.AuthUsername;
                dstHttp.AuthPassword = srcHttp.AuthPassword;
                dstHttp.AuthToken = srcHttp.AuthToken;
                dstHttp.TokenSourceNodeId = srcHttp.TokenSourceNodeId;
                dstHttp.TokenSourceOutputKey = srcHttp.TokenSourceOutputKey;
                dstHttp.ApiKeyName = srcHttp.ApiKeyName;
                dstHttp.ApiKeyValue = srcHttp.ApiKeyValue;
                dstHttp.ApiKeyValueSourceNodeId = srcHttp.ApiKeyValueSourceNodeId;
                dstHttp.ApiKeyValueSourceOutputKey = srcHttp.ApiKeyValueSourceOutputKey;
                dstHttp.ApiKeyInHeader = srcHttp.ApiKeyInHeader;
                dstHttp.TimeoutSeconds = srcHttp.TimeoutSeconds;
                dstHttp.TitleDisplayMode = srcHttp.TitleDisplayMode;
                dstHttp.TitleColorMode = srcHttp.TitleColorMode;
                dstHttp.TitleColorKey = srcHttp.TitleColorKey;
                dstHttp.UrlSourceNodeId = srcHttp.UrlSourceNodeId;
                dstHttp.UrlSourceOutputKey = srcHttp.UrlSourceOutputKey;
                dstHttp.BodySourceNodeId = srcHttp.BodySourceNodeId;
                dstHttp.BodySourceOutputKey = srcHttp.BodySourceOutputKey;
                dstHttp.CurlSourceNodeId = srcHttp.CurlSourceNodeId;
                dstHttp.CurlSourceOutputKey = srcHttp.CurlSourceOutputKey;
                dstHttp.UseCurl = srcHttp.UseCurl;
                dstHttp.CurlPath = srcHttp.CurlPath ?? string.Empty;
                dstHttp.ImpersonateBrowser = srcHttp.ImpersonateBrowser ?? string.Empty;
                dstHttp.AutoAppendCurlWriteOut = srcHttp.AutoAppendCurlWriteOut;

                // Deep copy Headers
                dstHttp.Headers.Clear();
                foreach (var h in srcHttp.Headers)
                {
                    dstHttp.Headers.Add(new HttpKeyValuePair
                    {
                        Key = h.Key,
                        Value = h.Value,
                        IsEnabled = h.IsEnabled,
                        SourceNodeId = h.SourceNodeId,
                        SourceOutputKey = h.SourceOutputKey
                    });
                }

                // Deep copy QueryParams
                dstHttp.QueryParams.Clear();
                foreach (var p in srcHttp.QueryParams)
                {
                    dstHttp.QueryParams.Add(new HttpKeyValuePair
                    {
                        Key = p.Key,
                        Value = p.Value,
                        IsEnabled = p.IsEnabled,
                        SourceNodeId = p.SourceNodeId,
                        SourceOutputKey = p.SourceOutputKey
                    });
                }

                // Deep copy FormData
                dstHttp.FormData.Clear();
                foreach (var f in srcHttp.FormData)
                {
                    dstHttp.FormData.Add(new HttpKeyValuePair
                    {
                        Key = f.Key,
                        Value = f.Value,
                        IsEnabled = f.IsEnabled,
                        SourceNodeId = f.SourceNodeId,
                        SourceOutputKey = f.SourceOutputKey
                    });
                }
            }

            if (source is KeyValueBridgeNode srcKvb && node is KeyValueBridgeNode dstKvb)
            {
                dstKvb.IsPassKeyMode = srcKvb.IsPassKeyMode;
                dstKvb.KvChannelKey = srcKvb.KvChannelKey ?? string.Empty;
                dstKvb.SelectedSourceBridgeNodeId = srcKvb.SelectedSourceBridgeNodeId;
                dstKvb.PollIntervalValue = srcKvb.PollIntervalValue;
                dstKvb.PollIntervalUnit = srcKvb.PollIntervalUnit;
                dstKvb.TitleDisplayMode = srcKvb.TitleDisplayMode;
                dstKvb.TitleColorMode = srcKvb.TitleColorMode;
                dstKvb.TitleColorKey = srcKvb.TitleColorKey;
                dstKvb.EnableDataCleanup = srcKvb.EnableDataCleanup;
                dstKvb.CleanupTargetBridgeNodeId = srcKvb.CleanupTargetBridgeNodeId;
                dstKvb.CleanupTargetKey = srcKvb.CleanupTargetKey ?? string.Empty;
                dstKvb.CleanupClearAllNodeData = srcKvb.CleanupClearAllNodeData;
                dstKvb.CleanupArrayFilterField = srcKvb.CleanupArrayFilterField ?? string.Empty;
                dstKvb.CleanupArrayFilterValue = srcKvb.CleanupArrayFilterValue ?? string.Empty;
                dstKvb.CleanupRemoveAllMatchedArrayItems = srcKvb.CleanupRemoveAllMatchedArrayItems;
                dstKvb.CleanupTriggerSourceNodeId = srcKvb.CleanupTriggerSourceNodeId;
                dstKvb.CleanupTriggerSourceOutputKey = srcKvb.CleanupTriggerSourceOutputKey;
                dstKvb.CleanupTriggerExpectedValue = srcKvb.CleanupTriggerExpectedValue;
                dstKvb.CleanupKeySourceNodeId = srcKvb.CleanupKeySourceNodeId;
                dstKvb.CleanupKeySourceOutputKey = srcKvb.CleanupKeySourceOutputKey;
                dstKvb.CleanupFilterFieldSourceNodeId = srcKvb.CleanupFilterFieldSourceNodeId;
                dstKvb.CleanupFilterFieldSourceOutputKey = srcKvb.CleanupFilterFieldSourceOutputKey;
                dstKvb.CleanupFilterValueSourceNodeId = srcKvb.CleanupFilterValueSourceNodeId;
                dstKvb.CleanupFilterValueSourceOutputKey = srcKvb.CleanupFilterValueSourceOutputKey;
                dstKvb.AdditionalAppendSources = srcKvb.AdditionalAppendSources?
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SourceNodeId))
                    .Select(x => new KeyValueBridgeAppendSource
                    {
                        SourceNodeId = x.SourceNodeId.Trim(),
                        SourceOutputKey = string.IsNullOrWhiteSpace(x.SourceOutputKey) ? null : x.SourceOutputKey.Trim()
                    })
                    .ToList() ?? new List<KeyValueBridgeAppendSource>();
                dstKvb.RebuildDataPorts();
                dstKvb.RefreshFlowPortsVisibility();
            }

            if (source is FlowOverwriteNode srcOverwrite && node is FlowOverwriteNode dstOverwrite)
            {
                dstOverwrite.OutputKey = srcOverwrite.OutputKey;
                dstOverwrite.AppendMode = srcOverwrite.AppendMode;
                dstOverwrite.TitleDisplayMode = srcOverwrite.TitleDisplayMode;
                dstOverwrite.TitleColorMode = srcOverwrite.TitleColorMode;
                dstOverwrite.TitleColorKey = srcOverwrite.TitleColorKey;
                dstOverwrite.Mappings = srcOverwrite.Mappings?
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.SourceNodeId))
                    .Select(x => new FlowOverwriteMapping
                    {
                        SourceNodeId = x.SourceNodeId.Trim(),
                        SourceOutputKey = string.IsNullOrWhiteSpace(x.SourceOutputKey) ? null : x.SourceOutputKey.Trim()
                    })
                    .ToList() ?? new List<FlowOverwriteMapping>();
                dstOverwrite.RebuildDynamicOutputs();
            }

            // ⚠️ CRITICAL: Trigger PropertyChanged cho các node có INotifyPropertyChanged
            if (node is OutputNode outputNode)
            {
                outputNode.NotifyTitleChanged();
            }
            else if (node is NotificationNode notificationNode)
            {
                notificationNode.NotifyTitleChanged();
            }
            else if (node is ListOutNode listOutNode)
            {
                listOutNode.NotifyTitleChanged();
            }
            else if (node is InputNode inputNode)
            {
                inputNode.NotifyTitleChanged();
            }
            else if (node is MouseEventNode mouseNode)
            {
                mouseNode.NotifyTitleChanged();
            }
            else if (node is LoopNode loopNode)
            {
                loopNode.NotifyTitleChanged();
            }
            else if (node is HttpRequestNode httpRequestNode)
            {
                httpRequestNode.NotifyTitleChanged();
            }
            else if (node is AssignDataNode assignDataNode)
            {
                assignDataNode.NotifyTitleChanged();
            }
            else if (node is MediaGalleryNode mediaGalleryNode)
            {
                mediaGalleryNode.NotifyTitleChanged();
            }
            else if (node is WebNode webNode)
            {
                webNode.NotifyTitleChanged();
            }
            else if (node is CodeNode codeNode)
            {
                codeNode.NotifyTitleChanged();
            }
            else if (node is FolderNode folderNode)
            {
                folderNode.NotifyTitleChanged();
            }
            else if (node is FileDownloadNode fileDlDup)
            {
                fileDlDup.NotifyTitleChanged();
            }
            else if (node is FolderFilePathsNode ffpDup)
            {
                ffpDup.NotifyTitleChanged();
            }
            else if (node is HtmlUiNode htmlUiNode)
            {
                htmlUiNode.NotifyTitleChanged();
            }
            else if (node is KeyValueBridgeNode kvbDupNotify)
            {
                kvbDupNotify.NotifyTitleChanged();
            }
            else if (node is FlowOverwriteNode flowOverwriteNode)
            {
                flowOverwriteNode.NotifyTitleChanged();
            }
            else if (node is BodyContainerNode bodyContainerNode)
            {
                bodyContainerNode.NotifyTitleChanged();
            }
            else if (node is VideoProcessingNode videoProcessingNode)
            {
                videoProcessingNode.NotifyTitleChanged();
            }

            // Fallback: if we created by Activator and Id is empty, ensure unique
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                node.Id = $"Node_{source.Type}_{Guid.NewGuid()}";
            }

            return node;
        }

        private string GenerateUniqueTitle(string baseTitle)
        {
            if (ViewModel == null) return baseTitle;

            // Lấy danh sách tất cả các title hiện có
            var existingTitles = ViewModel.Nodes
                .Where(n => !string.IsNullOrWhiteSpace(n.Title))
                .Select(n => n.Title!)
                .ToList();

            // Nếu không có title nào trùng, trả về title gốc với "- copy"
            if (!existingTitles.Contains(baseTitle))
            {
                return string.IsNullOrWhiteSpace(baseTitle) ? "Copy" : $"{baseTitle} - copy";
            }

            // Tìm tất cả các title có pattern "- copy" hoặc "- copy {x}"
            var copyPattern = new System.Text.RegularExpressions.Regex(@"^(.+?)\s*-\s*copy(\s+(\d+))?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Tìm base title (loại bỏ "- copy" và số)
            string? actualBaseTitle = null;
            int maxCopyNumber = 0;

            // Kiểm tra baseTitle có phải là copy không
            var baseMatch = copyPattern.Match(baseTitle);
            if (baseMatch.Success)
            {
                actualBaseTitle = baseMatch.Groups[1].Value.Trim();
                if (baseMatch.Groups[3].Success && int.TryParse(baseMatch.Groups[3].Value, out var baseNum))
                {
                    maxCopyNumber = baseNum;
                }
                else
                {
                    maxCopyNumber = 1; // Có "- copy" nhưng không có số = copy 1
                }
            }
            else
            {
                actualBaseTitle = baseTitle;
            }

            // Tìm số copy lớn nhất trong các title hiện có
            foreach (var title in existingTitles)
            {
                var match = copyPattern.Match(title);
                if (match.Success)
                {
                    var titleBase = match.Groups[1].Value.Trim();
                    // Nếu cùng base title (so sánh không phân biệt hoa thường)
                    if (string.Equals(titleBase, actualBaseTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var num))
                        {
                            // Có số copy
                            if (num > maxCopyNumber)
                            {
                                maxCopyNumber = num;
                            }
                        }
                        else
                        {
                            // Có "- copy" nhưng không có số = copy 1
                            if (maxCopyNumber < 1)
                            {
                                maxCopyNumber = 1;
                            }
                        }
                    }
                }
                else
                {
                    // Nếu title không có pattern "- copy" nhưng trùng với actualBaseTitle
                    // thì đó là bản gốc, không tính vào số copy
                    if (string.Equals(title, actualBaseTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        // Đây là bản gốc, không cần làm gì
                    }
                }
            }

            // Tạo title mới với số copy tiếp theo
            int nextCopyNumber = maxCopyNumber + 1;
            return string.IsNullOrWhiteSpace(actualBaseTitle)
                ? $"Copy {nextCopyNumber}"
                : $"{actualBaseTitle} - copy {nextCopyNumber}";
        }

        private static List<WorkflowDynamicDataPort> CloneDynamicDataPorts(List<WorkflowDynamicDataPort>? ports)
        {
            if (ports == null) return new List<WorkflowDynamicDataPort>();

            return ports.Select(p => new WorkflowDynamicDataPort
            {
                Key = p.Key,
                DisplayName = p.DisplayName,
                IsMultiple = p.IsMultiple,
                SelectedSourceNodeId = p.SelectedSourceNodeId,
                SelectedSourceOutputKey = p.SelectedSourceOutputKey,
                UserKeyOverride = p.UserKeyOverride,
                UserValueOverride = p.UserValueOverride,
                ConvertType = p.ConvertType,
                AvailableSources = new List<WorkflowDataSourceOption>() // recompute after connect
            }).ToList();
        }

        private static List<NodePort> ClonePorts(List<NodePort> ports, Dictionary<NodePort, NodePort> map)
        {
            var cloned = new List<NodePort>(ports.Count);
            foreach (var p in ports)
            {
                var cp = ClonePort(p);
                cloned.Add(cp);
                map[p] = cp;
            }
            return cloned;
        }

        private static NodePort ClonePort(NodePort p)
        {
            return new NodePort
            {
                Id = IsReservedPortId(p.Id) ? p.Id : Guid.NewGuid().ToString(),
                IsInput = p.IsInput,
                Position = p.Position,
                IsVisible = p.IsVisible,
                CanDeleteConnection = p.CanDeleteConnection,
                ColorKey = p.ColorKey
            };
        }

        private static List<AsyncTaskBranch> CloneAsyncTaskBranches(
            List<AsyncTaskBranch> sourceBranches,
            Dictionary<NodePort, NodePort> portMap)
        {
            var cloned = new List<AsyncTaskBranch>();
            foreach (var branch in sourceBranches)
            {
                var clonedBranch = new AsyncTaskBranch
                {
                    Id = Guid.NewGuid().ToString(), // Tạo ID mới
                    Label = branch.Label,
                    CanRemove = branch.CanRemove
                };

                // Map port nếu có
                if (branch.Port != null && portMap.TryGetValue(branch.Port, out var newPort))
                {
                    clonedBranch.Port = newPort;
                }

                cloned.Add(clonedBranch);
            }
            return cloned;
        }

        private static List<ConditionalBranch> CloneConditionalBranches(
            List<ConditionalBranch> branches,
            Dictionary<NodePort, NodePort> portMap)
        {
            var cloned = new List<ConditionalBranch>(branches.Count);
            foreach (var b in branches)
            {
                var cb = new ConditionalBranch
                {
                    Id = b.Id,
                    Label = b.Label,
                    DisplayTitle = b.DisplayTitle,
                    Condition = b.Condition,
                    CanRemove = b.CanRemove,
                    SatelliteOffsetX = b.SatelliteOffsetX,
                    SatelliteOffsetY = b.SatelliteOffsetY,
                    SatelliteInputPosition = b.SatelliteInputPosition,
                    LeftSourceNodeId = b.LeftSourceNodeId,
                    SubConditions = b.SubConditions?.Select(x => new ConditionExpression
                    {
                        LeftSourceNodeId = x.LeftSourceNodeId,
                        LeftKey = x.LeftKey,
                        Operator = x.Operator,
                        RightUseLiteralValue = x.RightUseLiteralValue,
                        RightLiteralValue = x.RightLiteralValue,
                        RightSourceNodeId = x.RightSourceNodeId,
                        RightKey = x.RightKey
                    }).ToList(),
                    OperatorsBetween = b.OperatorsBetween?.ToList(),
                    LeftKey = b.LeftKey,
                    Operator = b.Operator,
                    RightUseLiteralValue = b.RightUseLiteralValue,
                    RightLiteralValue = b.RightLiteralValue,
                    RightSourceNodeId = b.RightSourceNodeId,
                    RightKey = b.RightKey
                };

                if (b.Port != null && portMap.TryGetValue(b.Port, out var mapped))
                {
                    // Preserve original branch port ID so pasted connections
                    // can map exactly to if/else branch ports (no side-based ambiguity).
                    mapped.Id = b.Port.Id;
                    cb.Port = mapped;
                }

                cloned.Add(cb);
            }
            return cloned;
        }

        private static void CopyPortSettingsById(List<NodePort> from, List<NodePort> to)
        {
            foreach (var dst in to)
            {
                var src = from.FirstOrDefault(p => p.Id == dst.Id);
                if (src == null) continue;

                dst.IsInput = src.IsInput;
                dst.Position = src.Position;
                dst.IsVisible = src.IsVisible;
                dst.CanDeleteConnection = src.CanDeleteConnection;
                dst.ColorKey = src.ColorKey;
            }
        }

        private static bool IsReservedPortId(string id)
        {
            return id is "LoopNodeIn"
                or "LoopNodeOut"
                or "LoopNodeBottom"
                or "LoopIndexOut"
                or "LoopBodyTop"
                or "LoopBodyLeft"
                or "LoopBodyRight";
        }
    }
}

