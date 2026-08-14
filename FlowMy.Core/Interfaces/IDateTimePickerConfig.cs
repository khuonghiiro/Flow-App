// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models.Enums;

namespace FlowMy.Interfaces
{
    /// <summary>
    /// Interface cho cấu hình DateTimePicker
    /// </summary>
    public interface IDateTimePickerConfig
    {
        DateTime? SelectedDateTime { get; set; }
        DateTimePickerModeEnum PickerMode { get; set; }
        string DisplayFormat { get; set; }
    }
}
