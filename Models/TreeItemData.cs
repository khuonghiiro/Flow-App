namespace FlowMy.Models
{
    public class TreeItemData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<TreeItemData> Children { get; set; } = new();
    }
}
