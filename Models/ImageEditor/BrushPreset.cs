namespace FlowMy.Models.ImageEditor
{
    /// <summary>
    /// Các loại brush preset giống Adobe Photoshop.
    /// Dùng cho cả Brush (vẽ) và Eraser (tẩy).
    /// </summary>
    public enum BrushPreset
    {
        /// <summary>Cọ tròn cứng — cạnh sắc, circle mask (mặc định).</summary>
        RoundHard,

        /// <summary>Cọ tròn mềm — viền mờ dần, gaussian-like falloff.</summary>
        RoundSoft,

        /// <summary>Cọ dẹp (chữ nhật ngang) — rectangular mask, ratio ~3:1.</summary>
        Flat,

        /// <summary>Phấn — texture gritty, random noise modulation.</summary>
        Chalk,

        /// <summary>Bình xịt — scatter random dots trong bán kính.</summary>
        Spray,

        /// <summary>Điểm rải rác — random positions + size variation.</summary>
        Scatter,

        /// <summary>Bút chì — nét nhỏ cứng, slight jitter.</summary>
        Pencil,

        /// <summary>Bút phun khí — phun sương mịn màng siêu rộng.</summary>
        Airbrush,

        /// <summary>Vết mực bắn — giọt mực to trung tâm và hạt bắn xung quanh.</summary>
        Splatter,

        /// <summary>Than củi — nét cọ rỗ ráp mô phỏng vẽ trên giấy nhám.</summary>
        Charcoal,

        /// <summary>Sơn dầu — vệt cọ xước sọc xếp hàng song song.</summary>
        OilBrush
    }
}
