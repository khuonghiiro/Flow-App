namespace FlowMy.Models
{
    /// <summary>
    /// Mẫu lệnh CMD dùng cho popup gợi ý trong Git Manager.
    /// </summary>
    public sealed class CmdTemplateItem
    {
        public string Command { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public CmdTemplateItem() { }

        public CmdTemplateItem(string command, string description)
        {
            Command = command;
            Description = description;
        }
    }
}
