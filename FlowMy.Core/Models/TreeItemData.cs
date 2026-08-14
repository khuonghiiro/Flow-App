// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
namespace FlowMy.Models
{
    public class TreeItemData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<TreeItemData> Children { get; set; } = new();
    }
}
