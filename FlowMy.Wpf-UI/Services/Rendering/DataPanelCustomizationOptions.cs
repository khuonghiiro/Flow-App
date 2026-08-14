using System;
using System.Collections.Generic;
using FlowMy.Models;

namespace FlowMy.Services.Rendering
{
    /// <summary>
    /// Options for customizing data panel controls (button, textbox, combobox)
    /// </summary>
    public class DataPanelCustomizationOptions
    {
        /// <summary>
        /// Hide the "+" button (add new input button)
        /// </summary>
        public bool HideAddButton { get; set; } = false;

        /// <summary>
        /// Disable the key textbox
        /// </summary>
        public bool DisableKeyTextBox { get; set; } = false;

        /// <summary>
        /// Restrict type combobox to specific types (if null, show all types)
        /// </summary>
        public List<WorkflowDataType>? AllowedTypes { get; set; } = null;

        /// <summary>
        /// Default type to select (if null, use input.ConvertType)
        /// </summary>
        public WorkflowDataType? DefaultType { get; set; } = null;

        /// <summary>
        /// Disable the type combobox
        /// </summary>
        public bool DisableTypeComboBox { get; set; } = false;

        /// <summary>
        /// Default value for value textbox (if null, use input.UserValueOverride or resolved value)
        /// </summary>
        public string? DefaultValue { get; set; } = null;

        /// <summary>
        /// Only show the first input (hide others)
        /// </summary>
        public bool ShowOnlyFirstInput { get; set; } = false;

        /// <summary>
        /// Custom title for Inputs section (if null, uses default "Inputs")
        /// Example: "Số lần" for MouseEventNode
        /// </summary>
        public string? CustomInputsSectionTitle { get; set; } = null;

        /// <summary>
        /// Ẩn nút "+" thêm output (bên cạnh toggle Outputs).
        /// </summary>
        public bool HideAddOutputButton { get; set; } = false;

        /// <summary>
        /// Vô hiệu hóa nút "+" thêm output (vẫn hiển thị nhưng không click được).
        /// </summary>
        public bool DisableAddOutputButton { get; set; } = false;

        /// <summary>
        /// Số output tối đa cho phép thêm. Khi node.DynamicOutputs.Count >= giá trị này thì nút "+" sẽ bị disable.
        /// Null = không giới hạn.
        /// </summary>
        public int? MaxOutputsCount { get; set; } = null;
    }
}

