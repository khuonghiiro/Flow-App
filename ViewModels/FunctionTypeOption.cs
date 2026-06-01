namespace FlowMy.ViewModels
{
    /// <summary>
    /// Option cho combobox chọn chức năng trong tab "Tái sử dụng flow".
    /// </summary>
    public sealed class FunctionTypeOption
    {
        public string? Value { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        public FunctionTypeOption() { }

        public FunctionTypeOption(string? value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName ?? Value ?? string.Empty;
    }
}
