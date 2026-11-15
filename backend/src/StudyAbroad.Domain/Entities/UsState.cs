using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>51 bang của Mỹ (50 bang và DC) cho ô chọn bang trên form và chat, chia theo 4 vùng của Cục Thống kê Mỹ.</summary>
public class UsState : BaseEntity
{
    /// <summary>mã 2 chữ, ví dụ CA; cùng mã với cột state của universities</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>vùng: northeast, midwest, south, west</summary>
    public string Region { get; set; } = string.Empty;
    /// <summary>bang hay chọn, hiện thành nút bấm nhanh trên form</summary>
    public bool IsPopular { get; set; }
}
