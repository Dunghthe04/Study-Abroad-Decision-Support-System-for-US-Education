using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// [USAS-2067] Chỉ số giá khu vực theo bang (BEA Regional Price Parities, Mỹ = 100) cho UC-28.
/// Mỗi bang mỗi năm một dòng. Là chỉ số, không phải USD, và không thay chi phí sinh hoạt của từng trường.
/// </summary>
public class StateLivingCost : BaseEntity
{
    /// <summary>mã 2 chữ, trùng us_states.code</summary>
    public string StateCode { get; set; } = string.Empty;
    /// <summary>năm của số liệu (BEA công bố trễ khoảng 1 năm)</summary>
    public short Year { get; set; }
    /// <summary>chỉ số chung mọi khoản (RPP all items)</summary>
    public decimal IndexAllItems { get; set; }
    /// <summary>chỉ số tiền thuê nhà (RPP housing rents)</summary>
    public decimal? IndexHousingRents { get; set; }
    /// <summary>chỉ số hàng hóa (RPP goods)</summary>
    public decimal? IndexGoods { get; set; }
    /// <summary>chỉ số dịch vụ khác (RPP other services)</summary>
    public decimal? IndexOtherServices { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public DateOnly RetrievedAt { get; set; }
}
