namespace FlowMy.Interfaces
{
    public interface IRowStyleData
    {
        string Background { get; }
        string Foreground { get; }
        string HoverBackground { get; }
        string HoverForeground { get; }
        string SelectedBackground { get; }
        string SelectedForeground { get; }
        string BorderColor { get; }
        int BorderThickness { get; }
        string FontWeight { get; }
        string FontStyle { get; }
    }

    public enum StyleTypeEnum
    {
        Default,
        Success,
        Warning,
        Danger,
        Info,
        Custom
    }
}
