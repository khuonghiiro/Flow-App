using FlowMy.Models;

namespace FlowMy.Models.Nodes
{
    /// <summary>
    /// Một cặp gán: từ (SourceNodeId, SourceOutputKey) sang (TargetNodeId, TargetKey).
    /// </summary>
    public sealed class AssignDataAssignment
    {
        public string SourceNodeId { get; set; } = string.Empty;
        public string SourceOutputKey { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string TargetKey { get; set; } = string.Empty;

        /// <summary>
        /// Nếu true: trước khi lấy giá trị sẽ chạy lại logic của node nguồn để lấy giá trị mới nhất
        /// (dùng khi node nguồn có giá trị cũ / cần refresh).
        /// </summary>
        public bool RefreshSourceBeforeUse { get; set; }
    }

    /// <summary>
    /// Node xử lý gán dữ liệu: lấy giá trị từ output của node nguồn và gán vào key của node đích.
    /// Ví dụ: node Input có key và value, khi dùng node Gán dữ liệu thì value của Input đồng bộ với dữ liệu gán.
    /// </summary>
    public sealed class AssignDataNode : WorkflowNode
    {

        /// <summary>
        /// Danh sách gán: từ (SourceNodeId, SourceOutputKey) sang (TargetNodeId, TargetKey).
        /// </summary>
        public System.Collections.Generic.List<AssignDataAssignment> Assignments { get; } = new();

        public AssignDataNode()
        {
            Type = NodeType.AssignData;
            Title = "Gán dữ liệu";

            // Flow: input (trái), output (phải)
            Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"           // Port IN: dùng màu Info theo guideline
            });
            Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"   // Port OUT: dùng màu SunsetOrange theo guideline
            });
        }
    }
}
