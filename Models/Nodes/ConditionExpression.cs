namespace FlowMy.Models
{
    /// <summary>
    /// Một biểu thức điều kiện: Left [Operator] Right.
    /// Dùng trong SubConditions của ConditionalBranch để kết hợp nhiều điều kiện với OR/AND.
    /// </summary>
    public class ConditionExpression
    {
        public string? LeftSourceNodeId { get; set; }
        public string? LeftKey { get; set; }
        public ConditionOperator Operator { get; set; } = ConditionOperator.Equal;
        public bool RightUseLiteralValue { get; set; }
        public string? RightLiteralValue { get; set; }
        public string? RightSourceNodeId { get; set; }
        public string? RightKey { get; set; }

        /// <summary>Ngưỡng % cho so sánh ảnh (0-100). Chỉ dùng với ImageSimilarity* operators.</summary>
        public double SimilarityThreshold { get; set; } = 90;
    }
}
