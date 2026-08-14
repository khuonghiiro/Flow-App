using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace FlowMy.Models
{
    /// <summary>
    /// Class quản lý các nhánh điều kiện cho conditional nodes.
    /// Điều kiện: so sánh [Left: Node + Key] [Operator] [Right: Node + Key].
    /// </summary>
    public class ConditionalBranch
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Label { get; set; } = "if"; // "if", "else if", "else"
        /// <summary>Tiêu đề hiển thị có thể chỉnh sửa (vd: "Điều kiện 0"). Nếu null/empty thì dùng mặc định theo index.</summary>
        public string? DisplayTitle { get; set; }
        /// <summary>Legacy / đơn giản: key để lấy giá trị (null cho "else").</summary>
        public string? Condition { get; set; }
        public NodePort? Port { get; set; }
        public bool CanRemove { get; set; } = true;

        // So sánh Left [Operator] Right (dùng trong dialog và executor)
        public string? LeftSourceNodeId { get; set; }
        public string? LeftKey { get; set; }
        public ConditionOperator Operator { get; set; } = ConditionOperator.Equal;
        /// <summary>True = Đảo ngược (phủ định) kết quả điều kiện (!result).</summary>
        public bool IsInverted { get; set; } = false;
        /// <summary>True = bên phải dùng giá trị nhập (RightLiteralValue); False = lấy từ node (RightSourceNodeId + RightKey).</summary>
        public bool RightUseLiteralValue { get; set; }
        /// <summary>Giá trị so sánh bên phải khi RightUseLiteralValue = true.</summary>
        public string? RightLiteralValue { get; set; }
        public string? RightSourceNodeId { get; set; }
        public string? RightKey { get; set; }

        /// <summary>Ngưỡng % cho so sánh ảnh (0-100). Chỉ dùng với ImageSimilarity* operators.</summary>
        public double SimilarityThreshold { get; set; } = 90;

        /// <summary>Nhiều điều kiện con kết hợp OR/AND. Null/empty = dùng Left/Op/Right ở trên.</summary>
        public List<ConditionExpression>? SubConditions { get; set; }
        /// <summary>Toán tử giữa SubConditions[i] và SubConditions[i+1]: LogicalOperator.Or hoặc .And.</summary>
        public List<LogicalOperator>? OperatorsBetween { get; set; }

        // ===== Diamond visual mode: satellite circle position =====

        /// <summary>Vị trí X tương đối của satellite circle so với node (diamond). Serialize vào JSON.</summary>
        public double SatelliteOffsetX { get; set; } = double.NaN;
        /// <summary>Vị trí Y tương đối của satellite circle so với node (diamond). Serialize vào JSON.</summary>
        public double SatelliteOffsetY { get; set; } = double.NaN;
        /// <summary>Vị trí cổng IN của satellite (Left/Top/Right/Bottom). Serialize vào JSON.</summary>
        public PortPosition SatelliteInputPosition { get; set; } = PortPosition.Left;

        /// <summary>Runtime-only: Border UI của satellite circle trên canvas. Không serialize.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public Border? SatelliteBorder { get; set; }

        /// <summary>Runtime-only: Line (Path) nối từ diamond đến satellite. Không serialize.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public System.Windows.Shapes.Path? SatelliteLine { get; set; }

        /// <summary>Runtime-only: Button xoá nằm trên line satellite. Không serialize.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public Button? SatelliteDeleteButton { get; set; }

        /// <summary>Runtime-only: visual port IN của satellite (hình thoi). Không serialize.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public FrameworkElement? SatelliteInputVisual { get; set; }

        /// <summary>Runtime-only: visual port OUT tại hình thoi chính (overlay, không kéo line). Không serialize.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public FrameworkElement? DiamondOutputVisual { get; set; }

        /// <summary>Runtime-only: mũi tên ở cuối line vào satellite in. Không serialize.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public FrameworkElement? SatelliteArrowHead { get; set; }

        /// <summary>Runtime-only: UI badge hiển thị kết quả đánh giá (TRUE/FALSE) trên từng nhánh. Không serialize.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public TextBlock? ResultBadgeUI { get; set; }

        /// <summary>Runtime-only: Container Border của result badge. Không serialize.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public Border? ResultContainerUI { get; set; }

        /// <summary>Runtime-only: Chuỗi mô tả giá trị đánh giá gần nhất của nhánh. Không serialize.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string? LastEvaluatedValue { get; set; }

        /// <summary>Runtime-only: Tỷ lệ % tương đồng ảnh thực tế được tính toán. Không serialize.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public double? LastImageSimilarityScore { get; set; }

        /// <summary>Runtime-only: Mô tả chi tiết kết quả tính toán điều kiện (ví dụ: % khớp ảnh). Không serialize.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public string? LastEvaluationDetails { get; set; }
    }
}

