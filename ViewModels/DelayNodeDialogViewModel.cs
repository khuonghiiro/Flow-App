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

        [ObservableProperty]
        private bool _isTimeMode;

        [ObservableProperty]
        private bool _isTimeOnlyMode;

        [ObservableProperty]
        private bool _isNotTimeOrTimeOnlyMode;

        [ObservableProperty]
        private DateTime? _targetTime;

        [ObservableProperty]
        private DateTime? _targetTimeOnly;

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
            new DelayTimingModeOption(DelayTimingMode.Time, "Ngày và giờ"),
            new DelayTimingModeOption(DelayTimingMode.TimeOnly, "Thời gian"),
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
            TargetTime = node.TargetTime ?? DateTime.Now;
            // TargetTimeOnly: convert TimeSpan? -> DateTime? for DateTimePicker (Time mode)
            if (node.TargetTimeOnly.HasValue)
            {
                var ts = node.TargetTimeOnly.Value;
                TargetTimeOnly = DateTime.Today.Add(ts);
            }
            else
            {
                TargetTimeOnly = DateTime.Today.Add(DateTime.Now.TimeOfDay);
            }

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
                    else if (e.PropertyName == nameof(DelayNode.TargetTime))
                        TargetTime = node.TargetTime ?? DateTime.Now;
                    else if (e.PropertyName == nameof(DelayNode.TargetTimeOnly))
                    {
                        if (node.TargetTimeOnly.HasValue)
                            TargetTimeOnly = DateTime.Today.Add(node.TargetTimeOnly.Value);
                    }
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
            var newVal = value ?? string.Empty;
            if (_delayNode.DelaySourceNodeId != newVal)
                _delayNode.DelaySourceNodeId = newVal;
        }

        partial void OnDelayValueChanged(double value)
        {
            if (Math.Abs(_delayNode.DelayValue - value) > 0.0000001d)
                _delayNode.DelayValue = value;
        }

        partial void OnDelayUnitChanged(DelayTimeUnit value)
        {
            if (_delayNode.DelayUnit != value)
                _delayNode.DelayUnit = value;
        }

        partial void OnRandomMinValueChanged(double value)
        {
            if (Math.Abs(_delayNode.RandomMinValue - value) > 0.0000001d)
                _delayNode.RandomMinValue = value;
        }

        partial void OnRandomMaxValueChanged(double value)
        {
            if (Math.Abs(_delayNode.RandomMaxValue - value) > 0.0000001d)
                _delayNode.RandomMaxValue = value;
        }

        partial void OnDelaySourceOutputKeyChanged(string? value)
        {
            var newVal = value ?? string.Empty;
            if (_delayNode.DelaySourceOutputKey != newVal)
                _delayNode.DelaySourceOutputKey = newVal;
        }

        partial void OnTargetTimeChanged(DateTime? value)
        {
            if (_delayNode.TargetTime != value)
                _delayNode.TargetTime = value;
        }

        partial void OnTargetTimeOnlyChanged(DateTime? value)
        {
            var newTimeOnly = value?.TimeOfDay;
            if (_delayNode.TargetTimeOnly != newTimeOnly)
                _delayNode.TargetTimeOnly = newTimeOnly;
        }

        private void UpdateModeFlags()
        {
            IsNoneMode = TimingMode == DelayTimingMode.None;
            IsRandomMode = TimingMode == DelayTimingMode.Random;
            IsNodeKeyMode = TimingMode == DelayTimingMode.NodeKey;
            IsTimeMode = TimingMode == DelayTimingMode.Time;
            IsTimeOnlyMode = TimingMode == DelayTimingMode.TimeOnly;
            IsNotTimeOrTimeOnlyMode = !IsTimeMode && !IsTimeOnlyMode;
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

            if (_delayNode.TargetTime != TargetTime)
            {
                _delayNode.TargetTime = TargetTime;
                needSync = true;
            }

            // Convert DateTime? -> TimeSpan? for TargetTimeOnly
            var newTimeOnly = TargetTimeOnly?.TimeOfDay;
            if (_delayNode.TargetTimeOnly != newTimeOnly)
            {
                _delayNode.TargetTimeOnly = newTimeOnly;
                needSync = true;
            }

            if (needSync)
                _host.RequestSyncDataPanels(immediate: true);

            _delayNode.NotifyTitleChanged();
        }
    }
}
