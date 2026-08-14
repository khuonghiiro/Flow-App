# =========================================================================================
# Script: reorganize_views.ps1
# Reorganizes Views/Overlays and Views/NodeControls into clean, maintainable subfolders.
# =========================================================================================

$ErrorActionPreference = "Stop"

$rootDir = "d:\_DuAn\App_Desktop\workflows\Flow-My"
$viewsDir = "$rootDir\FlowMy.Wpf-UI\Views"
$overlaysDir = "$viewsDir\Overlays"
$nodeDialogsDir = "$viewsDir\NodeDialogs"
$dialogsDir = "$viewsDir\Dialogs"
$nodeControlsDir = "$viewsDir\NodeControls"
$contentPanelsDir = "$nodeControlsDir\ContentPanels"

# 1. Ensure target directories exist
New-Item -ItemType Directory -Force -Path $nodeDialogsDir | Out-Null
New-Item -ItemType Directory -Force -Path $dialogsDir | Out-Null
New-Item -ItemType Directory -Force -Path $contentPanelsDir | Out-Null

# List of true Overlays to KEEP in Views/Overlays/
$keepInOverlays = @(
    "BorderHighlightOverlay.xaml",
    "BorderHighlightOverlay.xaml.cs",
    "FloatingWidgetWindow.xaml",
    "FloatingWidgetWindow.xaml.cs",
    "MacroPlaybackOverlay.xaml",
    "MacroPlaybackOverlay.xaml.cs",
    "MacroRecorderOverlay.xaml",
    "MacroRecorderOverlay.xaml.cs",
    "ScreenCaptureOverlay.xaml",
    "ScreenCaptureOverlay.xaml.cs",
    "ScreenPositionPickerOverlay.xaml",
    "ScreenPositionPickerOverlay.xaml.cs",
    "ToastWindow.xaml",
    "ToastWindow.xaml.cs"
)

# List of Tool/System Dialogs to move to Views/Dialogs/
$moveToDialogs = @(
    "CanvasDisplaySettingsDialog.xaml",
    "CanvasDisplaySettingsDialog.xaml.cs",
    "CodeEditorPopupWindow.xaml",
    "CodeEditorPopupWindow.xaml.cs",
    "CustomPresetManagerDialog.xaml",
    "CustomPresetManagerDialog.xaml.cs",
    "DynamicUiPopupWindow.cs",
    "EnvironmentPathsConfigDialog.xaml",
    "EnvironmentPathsConfigDialog.xaml.cs",
    "ExecutionTraceDetachedWindow.xaml",
    "ExecutionTraceDetachedWindow.xaml.cs",
    "ExecutionTraceExportDialog.xaml",
    "ExecutionTraceExportDialog.xaml.cs",
    "ExportWorkflowOptionsDialog.xaml",
    "ExportWorkflowOptionsDialog.xaml.cs",
    "FloatingWidgetConfigDialog.xaml",
    "FloatingWidgetConfigDialog.xaml.cs",
    "GitManagerDialog.xaml",
    "GitManagerDialog.xaml.cs",
    "HotkeyCaptureDialog.xaml",
    "HotkeyCaptureDialog.xaml.cs",
    "HtmlUiEditorPopupWindow.xaml",
    "HtmlUiEditorPopupWindow.xaml.cs",
    "IconEditorDialog.xaml",
    "IconEditorDialog.xaml.cs",
    "KeyCaptureDialog.xaml",
    "KeyCaptureDialog.xaml.cs",
    "LayerAiDialog.DragDrop.cs",
    "LayerAiDialog.Helpers.cs",
    "LayerAiDialog.SecondaryImages.cs",
    "LayerAiDialog.SendAi.cs",
    "LayerAiDialog.WebBrowser.cs",
    "LayerAiDialog.xaml",
    "LayerAiDialog.xaml.cs",
    "MediaGalleryJsonExamples.cs",
    "NodeGeneratorWindow.xaml",
    "NodeGeneratorWindow.xaml.cs",
    "WorkflowManagementDialog.xaml",
    "WorkflowManagementDialog.xaml.cs",
    "WorkflowTransferProgressDialog.xaml",
    "WorkflowTransferProgressDialog.xaml.cs"
)

# 2. Process Overlays folder
$allOverlayFiles = Get-ChildItem -Path $overlaysDir -File

foreach ($file in $allOverlayFiles) {
    $fileName = $file.Name
    $srcPath = $file.FullName

    if ($keepInOverlays -contains $fileName) {
        # Keep in Overlays
        continue
    }
    elseif ($moveToDialogs -contains $fileName) {
        $destPath = "$dialogsDir\$fileName"
        Move-Item -Path $srcPath -Destination $destPath -Force
        Write-Host " [DIALOGS] Moved $fileName to Views/Dialogs/" -ForegroundColor Cyan
    }
    else {
        # All other files are Node Dialogs (*NodeDialog.xaml, .cs, BaseNodeDialog.xaml.cs, etc.)
        $destPath = "$nodeDialogsDir\$fileName"
        Move-Item -Path $srcPath -Destination $destPath -Force
        Write-Host " [NODE_DIALOGS] Moved $fileName to Views/NodeDialogs/" -ForegroundColor Green
    }
}

# 3. Move Heavy Content Panels from NodeControls to NodeControls/ContentPanels/
$contentPanelPatterns = @(
    "ImageEditorPanel.*",
    "ImageProcessingNodeContentControl.*",
    "VideoProcessingNodeContentControl.*",
    "VideoEditorNodeContentControl.*"
)

foreach ($pattern in $contentPanelPatterns) {
    $matchedFiles = Get-ChildItem -Path $nodeControlsDir -Filter $pattern -File
    foreach ($file in $matchedFiles) {
        $destPath = "$contentPanelsDir\$($file.Name)"
        Move-Item -Path $file.FullName -Destination $destPath -Force
        Write-Host " [CONTENT_PANELS] Moved $($file.Name) to Views/NodeControls/ContentPanels/" -ForegroundColor Yellow
    }
}

Write-Host "`nReorganization complete!" -ForegroundColor Magenta
