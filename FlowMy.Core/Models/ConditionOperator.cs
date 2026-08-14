// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
namespace FlowMy.Models
{
    /// <summary>
    /// Toán tử so sánh cho điều kiện if/else if.
    /// </summary>
    public enum ConditionOperator
    {
        Equal,               // ==
        NotEqual,            // !=
        GreaterThan,         // >
        GreaterThanOrEqual,  // >=
        LessThan,            // <
        LessThanOrEqual,     // <=
        Contains,            // Chuỗi chứa
        NotContains,
        TextEquals,          // So sánh text (không phân biệt hoa thường)
        TextNotEquals,
        Empty,               // Chuỗi rỗng (chỉ cần Left)
        NotEmpty,            // Không rỗng (chỉ cần Left)
        True,                // Giá trị Left là true (chỉ cần Left)
        False,               // Giá trị Left là false (chỉ cần Left)
        ImageSimilarityGte,  // Độ khớp ảnh >= ngưỡng% (Left, Right = base64 ảnh, SimilarityThreshold = %)
        ImageSimilarityLte,  // Độ khớp ảnh <= ngưỡng%
        ImageSimilarityGt,   // Độ khớp ảnh > ngưỡng%
        ImageSimilarityLt    // Độ khớp ảnh < ngưỡng%
    }

    /// <summary>Toán tử logic kết hợp điều kiện: OR (||) hoặc AND (&&).</summary>
    public enum LogicalOperator
    {
        Or,
        And
    }
}
