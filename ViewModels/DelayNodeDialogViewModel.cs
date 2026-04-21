using FlowMy.Models;
using FlowMy.Services.Interaction;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace FlowMy.ViewModels
{
    public sealed class DelayUnitOption
    {
        public DelayTimeUnit Value { get; }
        public string DisplayName { get; }

        public DelayUnitOption(DelayTimeUnit value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }
    }

    public sealed class DelayTimingModeOption
    {
        public DelayTimingMode Value { get; }
        public string DisplayName { get; }

        public DelayTimingModeOption(DelayTimingMode value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }
    }

    public partial class DelayNodeDialogViewModel : BaseNodeDialogViewModel
    {
        private readonly DelayNode _delayNode;

        [ObservableProperty]
        private double _delayValue;

        [ObservableProperty]
        private DelayTimeUnit _delayUnit;

        [ObservableProperty]
        private DelayTimingMode _timingMode;

        [ObservableProperty]
        private double _randomMinValue;

        [ObservableProperty]
        private double _randomMaxValue;

        [ObservableProperty]
        private string? _delaySourceNodeId;

        [ObservableProperty]
        private string? _delaySourceOutputKey;

        [ObservableProperty]
        private bool _isNoneMode;

        [ObservableProperty]
        private bool _isRandomMode;

        [ObservableProperty]
        private bool _isNodeKeyMode;

        public ObservableCollection<DelayUnitOption> DelayUnitOptions { get; } = new()
        {
            new DelayUnitOption(DelayTimeUnit.Milliseconds, "ms"),
            new DelayUnitOption(DelayTimeUnit.Seconds, "Giây"),
            new DelayUnitOption(DelayTimeUnit.Minutes, "Phút"),
            new DelayUnitOption(DelayTimeUnit.Hours, "Giờ"),
        };

        public ObservableCollection<DelayTimingModeOption> TimingModeOptions { get; } = new()
        {
            new DelayTimingModeOption(DelayTimingMode.None, "Không có"),
            new DelayTimingModeOption(DelayTimingMode.Random, "Số ngẫu nhiên (min–max)"),
            new DelayTimingModeOption(DelayTimingMode.NodeKey, "Lấy từ node / output key"),
        };

        public ObservableCollection<WorkflowDataSourceOption> AvailableNodeOptions { get; } = new();
        public ObservableCollection<WorkflowOutputKeyOption> AvailableOutputKeyOptions { get; } = new();

        public DelayNodeDialogViewModel(DelayNode node, IWorkflowEditorHost host)
            : base(node, host)
        {
            _delayNode = node ?? throw new ArgumentNullException(nameof(node));

            DelayValue = node.DelayValue;
            DelayUnit = node.DelayUnit;
            TimingMode = node.TimingMode;
            RandomMinValue = node.RandomMinValue;
            RandomMaxValue = node.RandomMaxValue;
            DelaySourceNodeId = string.IsNullOrWhiteSpace(node.DelaySourceNodeId) ? null : node.DelaySourceNodeId;
            DelaySourceOutputKey = string.IsNullOrWhiteSpace(node.DelaySourceOutputKey) ? null : node.DelaySourceOutputKey;

            UpdateModeFlags();
            RefreshAvailableNodes();
            RefreshOutputKeyOptions();

            if (node is INotifyPropertyChanged npc)
            {
                npc.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(DelayNode.DelayValue))
                        DelayValue = node.DelayValue;
                    else if (e.PropertyName == nameof(DelayNode.DelayUnit))
                        DelayUnit = node.DelayUnit;
                    else if (e.PropertyName == nameof(DelayNode.TimingMode))
                        TimingMode = node.TimingMode;
                    else if (e.PropertyName == nameof(DelayNode.RandomMinValue))
                        RandomMinValue = node.RandomMinValue;
                    else if (e.PropertyName == nameof(DelayNode.RandomMaxValue))
                        RandomMaxValue = node.RandomMaxValue;
                    else if (e.PropertyName == nameof(DelayNode.DelaySourceNodeId))
                        DelaySourceNodeId = string.IsNullOrWhiteSpace(node.DelaySourceNodeId) ? null : node.DelaySourceNodeId;
                    else if (e.PropertyName == nameof(DelayNode.DelaySourceOutputKey))
                        DelaySourceOutputKey = string.IsNullOrWhiteSpace(node.DelaySourceOutputKey) ? null : node.DelaySourceOutputKey;
                };
            }
        }

        partial void OnTimingModeChanged(DelayTimingMode value)
        {
            UpdateModeFlags();
            if (_delayNode.TimingMode != value)
                _delayNode.TimingMode = value;
        }

        partial void OnDelaySourceNodeIdChanged(string? value)
        {
            RefreshOutputKeyOptions();
        }

        private void UpdateModeFlags()
        {
            IsNoneMode = TimingMode == DelayTimingMode.None;
            IsRandomMode = TimingMode == DelayTimingMode.Random;
            IsNodeKeyMode = TimingMode == DelayTimingMode.NodeKey;
        }

        private void RefreshAvailableNodes()
        {
            AvailableNodeOptions.Clear();
            if (_host.ViewModel?.Nodes == null) return;

            foreach (var n in _host.ViewModel.Nodes)
            {
                if (ReferenceEquals(n, _delayNode)) continue;
                if (n.DynamicOutputs == null || n.DynamicOutputs.Count == 0) continue;
                AvailableNodeOptions.Add(CreateDataSourceOption(n));
            }
        }

        private void RefreshOutputKeyOptions()
        {
            AvailableOutputKeyOptions.Clear();
            if (string.IsNullOrWhiteSpace(DelaySourceNodeId) || _host.ViewModel?.Nodes == null)
                return;

            var src = _host.ViewModel.Nodes.FirstOrDefault(n =>
                string.Equals(n.Id, DelaySourceNodeId, StringComparison.OrdinalIgnoreCase));
            if (src?.DynamicOutputs == null) return;

            foreach (var o in src.DynamicOutputs)
            {
                var key = o.Key ?? string.Empty;
                AvailableOutputKeyOptions.Add(new WorkflowOutputKeyOption
                {
                    Key = key,
                    DisplayName = o.DisplayName ?? key,
                    Type = o.OutputType ?? o.ConvertType
                });
            }
        }

        protected override string GetDefaultTitle() => "Delay";

        protected override void OnSaveTitle()
        {
            bool needSync = false;

            if (Math.Abs(_delayNode.DelayValue - DelayValue) > 0.0000001d)
            {
                _delayNode.DelayValue = DelayValue;
                needSync = true;
            }

            if (_delayNode.DelayUnit != DelayUnit)
            {
                _delayNode.DelayUnit = DelayUnit;
                needSync = true;
            }

            if (_delayNode.TimingMode != TimingMode)
            {
                _delayNode.TimingMode = TimingMode;
                needSync = true;
            }

            if (Math.Abs(_delayNode.RandomMinValue - RandomMinValue) > 0.0000001d)
            {
                _delayNode.RandomMinValue = RandomMinValue;
                needSync = true;
            }

            if (Math.Abs(_delayNode.RandomMaxValue - RandomMaxValue) > 0.0000001d)
            {
                _delayNode.RandomMaxValue = RandomMaxValue;
                needSync = true;
            }

            var newNodeId = DelaySourceNodeId ?? string.Empty;
            if (_delayNode.DelaySourceNodeId != newNodeId)
            {
                _delayNode.DelaySourceNodeId = newNodeId;
                needSync = true;
            }

            var newKey = DelaySourceOutputKey ?? string.Empty;
            if (_delayNode.DelaySourceOutputKey != newKey)
            {
                _delayNode.DelaySourceOutputKey = newKey;
                needSync = true;
            }

            if (needSync)
                _host.RequestSyncDataPanels(immediate: true);

            _delayNode.NotifyTitleChanged();
        }
    }
}
