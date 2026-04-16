using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public partial class MouseEventNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly MouseEventNode _mouseEventNode;

        [ObservableProperty]
        private string _mouseButton;

        [ObservableProperty]
        private int _repeatCount;

        [ObservableProperty]
        private double _holdDuration;

        [ObservableProperty]
        private int _scrollSpeed;

        [ObservableProperty]
        private bool _isRepeatCountConnected;

        [ObservableProperty]
        private bool _isScrollMode;

        public bool IsRepeatCountEnabled => !IsRepeatCountConnected && !IsScrollMode;

        // Options cho ComboBox MouseButton
        public System.Collections.ObjectModel.ObservableCollection<string> MouseButtonOptions { get; } = new()
        {
            "Left",
            "Right",
            "Middle",
            "ScrollUp",
            "ScrollDown"
        };

        public MouseEventNodeDialogViewModel(MouseEventNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _mouseEventNode = node;
            _mouseButton = node.MouseButton ?? "Left";
            _repeatCount = node.RepeatCount;
            _holdDuration = node.HoldDuration;
            _scrollSpeed = node.ScrollSpeed;

            UpdateRepeatCountConnectionStatus();
            UpdateScrollMode();

            // Sync từ node
            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MouseEventNode.MouseButton))
                    {
                        MouseButton = node.MouseButton ?? "Left";
                        UpdateScrollMode();
                    }
                    else if (e.PropertyName == nameof(MouseEventNode.RepeatCount))
                    {
                        RepeatCount = node.RepeatCount;
                    }
                    else if (e.PropertyName == nameof(MouseEventNode.HoldDuration))
                    {
                        HoldDuration = node.HoldDuration;
                    }
                    else if (e.PropertyName == nameof(MouseEventNode.ScrollSpeed))
                    {
                        ScrollSpeed = node.ScrollSpeed;
                    }
                    OnNodePropertyChanged(e.PropertyName);
                };
            }
        }

        protected override string GetDefaultTitle() => "Mouse Event";

        internal void UpdateRepeatCountConnectionStatus()
        {
            if (_mouseEventNode == null)
                return;

            var repeatCountInput = _mouseEventNode.DynamicInputs?.FirstOrDefault(i => i.Key == "repeatCount");
            var isConnected = !string.IsNullOrWhiteSpace(repeatCountInput?.SelectedSourceNodeId);
            if (IsRepeatCountConnected != isConnected)
            {
                IsRepeatCountConnected = isConnected;
                OnPropertyChanged(nameof(IsRepeatCountEnabled));
            }
        }

        private void UpdateScrollMode()
        {
            var wasScrollMode = IsScrollMode;
            IsScrollMode = MouseButton == "ScrollUp" || MouseButton == "ScrollDown";
            if (wasScrollMode != IsScrollMode)
            {
                OnPropertyChanged(nameof(IsRepeatCountEnabled));
            }
        }

        partial void OnMouseButtonChanged(string value)
        {
            UpdateScrollMode();
        }

        // Override LoadInputs để listen to connection changes cho repeatCount input
        protected override void LoadInputs()
        {
            base.LoadInputs();

            foreach (var inputVm in Inputs)
            {
                // Listen to connection changes cho repeatCount input
                if (inputVm.Key == "repeatCount")
                {
                    inputVm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(InputItemViewModel.SelectedSourceNodeId))
                        {
                            UpdateRepeatCountConnectionStatus();
                        }
                    };
                }
            }
            
            // Update connection status sau khi load
            UpdateRepeatCountConnectionStatus();
        }

        // Override OnSaveTitle để thêm logic riêng thay vì override SaveTitle
        protected override void OnSaveTitle()
        {
            if (_mouseEventNode.MouseButton != MouseButton)
            {
                _mouseEventNode.MouseButton = MouseButton;
                _host.RequestSyncDataPanels(immediate: true);
            }

            // Chỉ save RepeatCount nếu không phải scroll mode và không có connection
            if (!IsScrollMode && !IsRepeatCountConnected && _mouseEventNode.RepeatCount != RepeatCount)
            {
                _mouseEventNode.RepeatCount = RepeatCount;
                _host.RequestSyncDataPanels(immediate: true);
            }

            if (_mouseEventNode.HoldDuration != HoldDuration)
            {
                _mouseEventNode.HoldDuration = HoldDuration;
                _host.RequestSyncDataPanels(immediate: true);
            }

            if (_mouseEventNode.ScrollSpeed != ScrollSpeed)
            {
                _mouseEventNode.ScrollSpeed = ScrollSpeed;
                _host.RequestSyncDataPanels(immediate: true);
            }
        }
    }
}
