using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Views.NodeControls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.ViewModels
{
    public partial class EmbedApplicationNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly EmbedApplicationNode _embedNode;

        [ObservableProperty] private int _selectedProcessId;
        
        partial void OnSelectedProcessIdChanged(int value)
        {
            // Update node ngay khi chọn app để hiển thị preview
            var app = AvailableApplications.FirstOrDefault(a => a.ProcessId == value);
            if (app != null && _embedNode != null)
            {
                _embedNode.ProcessId = value;
                _embedNode.ProcessName = app.ProcessName;
                _embedNode.WindowHandle = app.WindowHandle;
                _embedNode.WindowTitle = app.WindowTitle;
                _embedNode.NotifyTitleChanged();
            }
        }
        [ObservableProperty] private double _embeddedWidth = 800;
        [ObservableProperty] private double _embeddedHeight = 600;
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private bool _showBorder = true;
        [ObservableProperty] private bool _allowInteraction = true;
        [ObservableProperty] private bool _autoRefresh = true;
        [ObservableProperty] private int _refreshRate = 30;
        [ObservableProperty] private EmbedCaptureMode _captureMode = EmbedCaptureMode.Interactive;

        public ObservableCollection<ApplicationOption> AvailableApplications { get; } = new();
        public ObservableCollection<EmbedCaptureModeOption> CaptureModeOptions { get; } = new();

        public EmbedApplicationNodeDialogViewModel(EmbedApplicationNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _embedNode = node ?? throw new ArgumentNullException(nameof(node));

            // Sync từ node → VM
            SelectedProcessId = _embedNode.ProcessId;
            EmbeddedWidth = _embedNode.EmbeddedWidth;
            EmbeddedHeight = _embedNode.EmbeddedHeight;
            IsActive = _embedNode.IsActive;
            ShowBorder = _embedNode.ShowBorder;
            AllowInteraction = _embedNode.AllowInteraction;
            AutoRefresh = _embedNode.AutoRefresh;
            RefreshRate = _embedNode.RefreshRate;
            CaptureMode = _embedNode.CaptureMode;

            // Load capture mode options
            LoadCaptureModeOptions();

            // Load available applications
            RefreshApplicationsList();

            // Subscribe PropertyChanged
            node.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(EmbedApplicationNode.ProcessId):
                        SelectedProcessId = _embedNode.ProcessId;
                        break;
                    case nameof(EmbedApplicationNode.EmbeddedWidth):
                        EmbeddedWidth = _embedNode.EmbeddedWidth;
                        break;
                    case nameof(EmbedApplicationNode.EmbeddedHeight):
                        EmbeddedHeight = _embedNode.EmbeddedHeight;
                        break;
                }
                OnNodePropertyChanged(e.PropertyName ?? string.Empty);
            };
        }

        protected override string GetDefaultTitle() => "Embed Application";

        protected override void OnSaveTitle()
        {
            // Tìm app đang được chọn trong VM
            var app = AvailableApplications.FirstOrDefault(a => a.ProcessId == SelectedProcessId);

            if (app != null && app.WindowHandle != IntPtr.Zero)
            {
                // Đồng bộ thông tin app vào node
                _embedNode.ProcessId   = app.ProcessId;
                _embedNode.ProcessName = app.ProcessName;
                _embedNode.WindowHandle = app.WindowHandle;
                _embedNode.WindowTitle = app.WindowTitle;

                // Chỉ dùng screenshot — KHÔNG SetParent/EmbedWindow
                // HasEmbeddedWindow = true sẽ trigger screenshot capture timer trong Control
                if (!_embedNode.HasEmbeddedWindow)
                    _embedNode.HasEmbeddedWindow = true;
            }
            else if (SelectedProcessId <= 0)
            {
                // User bỏ chọn app
                _embedNode.ProcessId     = 0;
                _embedNode.ProcessName   = string.Empty;
                _embedNode.WindowHandle  = IntPtr.Zero;
                _embedNode.WindowTitle   = string.Empty;
                _embedNode.HasEmbeddedWindow = false;
            }

            // Đồng bộ các tuỳ chọn còn lại
            _embedNode.EmbeddedWidth    = EmbeddedWidth;
            _embedNode.EmbeddedHeight   = EmbeddedHeight;
            _embedNode.IsActive         = IsActive;
            _embedNode.ShowBorder       = ShowBorder;
            _embedNode.AllowInteraction = AllowInteraction;
            _embedNode.AutoRefresh      = AutoRefresh;
            _embedNode.RefreshRate      = RefreshRate;
            _embedNode.CaptureMode      = CaptureMode;

            _embedNode.NotifyTitleChanged();
            _host.RequestSyncDataPanels(immediate: true);
        }

        [RelayCommand]
        private void RefreshApplications()
        {
            RefreshApplicationsList();
        }

        private void RefreshApplicationsList()
        {
            AvailableApplications.Clear();

            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                    .OrderBy(p => p.ProcessName)
                    .ToList();

                foreach (var proc in processes)
                {
                    try
                    {
                        AvailableApplications.Add(new ApplicationOption
                        {
                            ProcessId = proc.Id,
                            ProcessName = proc.ProcessName,
                            WindowTitle = proc.MainWindowTitle,
                            WindowHandle = proc.MainWindowHandle,
                            DisplayName = $"{proc.ProcessName} - {proc.MainWindowTitle}"
                        });
                    }
                    catch
                    {
                        // Skip processes we can't access
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading applications: {ex.Message}");
            }
        }

        private void LoadCaptureModeOptions()
        {
            CaptureModeOptions.Clear();
            CaptureModeOptions.Add(new EmbedCaptureModeOption 
            { 
                Value = EmbedCaptureMode.DisplayOnly, 
                DisplayName = "Chỉ hiển thị" 
            });
            CaptureModeOptions.Add(new EmbedCaptureModeOption 
            { 
                Value = EmbedCaptureMode.Interactive, 
                DisplayName = "Tương tác đầy đủ" 
            });
            CaptureModeOptions.Add(new EmbedCaptureModeOption 
            { 
                Value = EmbedCaptureMode.Snapshot, 
                DisplayName = "Snapshot tĩnh" 
            });
        }
    }

    public class ApplicationOption
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public IntPtr WindowHandle { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public class EmbedCaptureModeOption
    {
        public EmbedCaptureMode Value { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
