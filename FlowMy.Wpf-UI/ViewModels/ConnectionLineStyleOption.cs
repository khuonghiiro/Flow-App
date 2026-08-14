namespace FlowMy.ViewModels
{
    /// <summary>
    /// Option cho combobox chọn kiểu vẽ đường kết nối (Bezier/Orthogonal/Straight hoặc theo workflow).
    /// </summary>
    public sealed class ConnectionLineStyleOption
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public ConnectionLineStyleOption() { }

        public ConnectionLineStyleOption(string key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName ?? Key;
    }
}


