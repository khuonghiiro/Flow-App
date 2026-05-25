using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class MacroRecorderNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly MacroRecorderNode _macroRecorderNode;

        [ObservableProperty] private string _outputKey;
        [ObservableProperty] private string _macroDataJson;
        [ObservableProperty] private string _selectedPlaybackMode;
        [ObservableProperty] private int    _repeatIntervalMs;
        [ObservableProperty] private int    _repeatCount;
        [ObservableProperty] private string _selectedVisualPlaybackMode;
        [ObservableProperty] private bool   _showMouseTrail;
        [ObservableProperty] private int    _countdownSeconds;

        // Target App properties
        [ObservableProperty] private string _selectedExecutionMode;
        [ObservableProperty] private WindowInfo? _selectedTargetWindow;

        public bool IsRepeatVisible       => SelectedPlaybackMode == "Lặp lại";
        public bool IsTargetAppVisible    => SelectedExecutionMode == "Chỉ định Ứng dụng";
        public bool CanExportJson         => !string.IsNullOrWhiteSpace(MacroDataJson);

        public ObservableCollection<string> PlaybackModeOptions { get; } = new()
        {
            "Chạy 1 lần",
            "Lặp lại"
        };

        /// <summary>
        /// 3 chế độ hiển thị overlay khi phát lại.
        /// </summary>
        public ObservableCollection<string> VisualPlaybackModeOptions { get; } = new()
        {
            "Không hiển thị",
            "Hiển thị trực tiếp",
            "Hiển thị luồng sẵn"
        };

        public ObservableCollection<string> ExecutionModeOptions { get; } = new()
        {
            "Tự do (Toàn màn hình)",
            "Chỉ định Ứng dụng"
        };

        public ObservableCollection<WindowInfo> ActiveWindows { get; } = new();

        public MacroRecorderNodeDialogViewModel(MacroRecorderNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _macroRecorderNode = node;

            _outputKey               = node.OutputKey ?? "macroData";
            _macroDataJson           = node.MacroDataJson ?? "";
            _selectedPlaybackMode    = node.PlaybackMode == MacroPlaybackMode.Repeat ? "Lặp lại" : "Chạy 1 lần";
            _repeatIntervalMs        = node.RepeatIntervalMs;
            _repeatCount             = node.RepeatCount;
            _selectedVisualPlaybackMode = VisualModeToString(node.VisualPlaybackMode);
            _showMouseTrail          = node.ShowMouseTrail;
            _countdownSeconds        = node.CountdownSeconds;

            _selectedExecutionMode   = node.ExecutionMode == MacroExecutionMode.TargetApp ? "Chỉ định Ứng dụng" : "Tự do (Toàn màn hình)";

            LoadWindowsCommand = new RelayCommand(ExecuteLoadWindows);
            if (node.ExecutionMode == MacroExecutionMode.TargetApp)
            {
                ExecuteLoadWindows();
                _selectedTargetWindow = ActiveWindows.FirstOrDefault(w => w.Title == node.TargetWindowTitle && w.ProcessName == node.TargetProcessName);
            }

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MacroRecorderNode.OutputKey))
                        OutputKey = node.OutputKey ?? "macroData";
                    else if (e.PropertyName == nameof(MacroRecorderNode.MacroDataJson))
                    {
                        MacroDataJson = node.MacroDataJson ?? "";
                        OnPropertyChanged(nameof(CanExportJson));
                    }
                    else if (e.PropertyName == nameof(MacroRecorderNode.PlaybackMode))
                        SelectedPlaybackMode = node.PlaybackMode == MacroPlaybackMode.Repeat ? "Lặp lại" : "Chạy 1 lần";
                    else if (e.PropertyName == nameof(MacroRecorderNode.RepeatIntervalMs))
                        RepeatIntervalMs = node.RepeatIntervalMs;
                    else if (e.PropertyName == nameof(MacroRecorderNode.RepeatCount))
                        RepeatCount = node.RepeatCount;
                    else if (e.PropertyName == nameof(MacroRecorderNode.VisualPlaybackMode))
                        SelectedVisualPlaybackMode = VisualModeToString(node.VisualPlaybackMode);
                    else if (e.PropertyName == nameof(MacroRecorderNode.ShowMouseTrail))
                        ShowMouseTrail = node.ShowMouseTrail;
                    else if (e.PropertyName == nameof(MacroRecorderNode.ExecutionMode))
                        SelectedExecutionMode = node.ExecutionMode == MacroExecutionMode.TargetApp ? "Chỉ định Ứng dụng" : "Tự do (Toàn màn hình)";
                    else if (e.PropertyName == nameof(MacroRecorderNode.TargetProcessName) || e.PropertyName == nameof(MacroRecorderNode.TargetWindowTitle))
                    {
                        SelectedTargetWindow = ActiveWindows.FirstOrDefault(w => w.Title == node.TargetWindowTitle && w.ProcessName == node.TargetProcessName);
                    }

                    OnNodePropertyChanged(e.PropertyName);
                };
            }
        }

        protected override string GetDefaultTitle() => "Macro Recorder";

        partial void OnSelectedPlaybackModeChanged(string value)
            => OnPropertyChanged(nameof(IsRepeatVisible));

        partial void OnSelectedExecutionModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsTargetAppVisible));
            if (IsTargetAppVisible && ActiveWindows.Count == 0)
                ExecuteLoadWindows();
        }

        partial void OnMacroDataJsonChanged(string value)
            => OnPropertyChanged(nameof(CanExportJson));

        /// <summary>
        /// Flush ShowMouseTrail vào node ngay lập tức (không chờ Save button).
        /// Gọi trước khi mở MacroRecorderOverlay.
        /// </summary>
        public void SaveShowMouseTrailToNode()
        {
            if (_macroRecorderNode.ShowMouseTrail != ShowMouseTrail)
            {
                _macroRecorderNode.ShowMouseTrail = ShowMouseTrail;
                _host.RequestSyncDataPanels(immediate: true);
            }
        }

        protected override void OnSaveTitle()
        {
            bool needSync = false;

            if (_macroRecorderNode.Title != NodeTitle)
            { _macroRecorderNode.Title = NodeTitle; needSync = true; }

            if (_macroRecorderNode.OutputKey != OutputKey)
            { _macroRecorderNode.OutputKey = OutputKey; needSync = true; }

            if (_macroRecorderNode.MacroDataJson != MacroDataJson)
            { _macroRecorderNode.MacroDataJson = MacroDataJson; needSync = true; }

            var newPlaybackMode = SelectedPlaybackMode == "Lặp lại"
                ? MacroPlaybackMode.Repeat : MacroPlaybackMode.Once;
            if (_macroRecorderNode.PlaybackMode != newPlaybackMode)
            { _macroRecorderNode.PlaybackMode = newPlaybackMode; needSync = true; }

            if (_macroRecorderNode.RepeatIntervalMs != RepeatIntervalMs)
            { _macroRecorderNode.RepeatIntervalMs = RepeatIntervalMs; needSync = true; }

            if (_macroRecorderNode.RepeatCount != RepeatCount)
            { _macroRecorderNode.RepeatCount = RepeatCount; needSync = true; }

            var newVisual = StringToVisualMode(SelectedVisualPlaybackMode);
            if (_macroRecorderNode.VisualPlaybackMode != newVisual)
            { _macroRecorderNode.VisualPlaybackMode = newVisual; needSync = true; }

            if (_macroRecorderNode.ShowMouseTrail != ShowMouseTrail)
            { _macroRecorderNode.ShowMouseTrail = ShowMouseTrail; needSync = true; }

            if (_macroRecorderNode.CountdownSeconds != CountdownSeconds)
            { _macroRecorderNode.CountdownSeconds = CountdownSeconds; needSync = true; }

            var newExecMode = SelectedExecutionMode == "Chỉ định Ứng dụng" ? MacroExecutionMode.TargetApp : MacroExecutionMode.Free;
            if (_macroRecorderNode.ExecutionMode != newExecMode)
            { _macroRecorderNode.ExecutionMode = newExecMode; needSync = true; }

            string newTargetProc = SelectedTargetWindow?.ProcessName ?? "";
            string newTargetTitle = SelectedTargetWindow?.Title ?? "";
            
            if (_macroRecorderNode.TargetProcessName != newTargetProc)
            { _macroRecorderNode.TargetProcessName = newTargetProc; needSync = true; }

            if (_macroRecorderNode.TargetWindowTitle != newTargetTitle)
            { _macroRecorderNode.TargetWindowTitle = newTargetTitle; needSync = true; }

            if (needSync)
                _host.RequestSyncDataPanels(immediate: true);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private static string VisualModeToString(VisualPlaybackMode m) => m switch
        {
            VisualPlaybackMode.Silent => "Không hiển thị",
            VisualPlaybackMode.Ghost  => "Hiển thị luồng sẵn",
            _                         => "Hiển thị trực tiếp"
        };

        private static VisualPlaybackMode StringToVisualMode(string s) => s switch
        {
            "Không hiển thị"    => VisualPlaybackMode.Silent,
            "Hiển thị luồng sẵn" => VisualPlaybackMode.Ghost,
            _                    => VisualPlaybackMode.Live
        };

        public IRelayCommand LoadWindowsCommand { get; }

        private void ExecuteLoadWindows()
        {
            var windows = WindowHelper.GetActiveWindows();
            ActiveWindows.Clear();
            foreach (var w in windows)
            {
                ActiveWindows.Add(w);
            }
            if (SelectedTargetWindow != null)
            {
                var match = ActiveWindows.FirstOrDefault(x => x.ProcessName == SelectedTargetWindow.ProcessName && x.Title == SelectedTargetWindow.Title);
                SelectedTargetWindow = match;
            }
        }
    }
}
