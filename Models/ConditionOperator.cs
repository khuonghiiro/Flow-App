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
        False                // Giá trị Left là false (chỉ cần Left)
    }

    /// <summary>Toán tử logic kết hợp điều kiện: OR (||) hoặc AND (&&).</summary>
    public enum LogicalOperator
    {
        Or,
        And
    }
}
