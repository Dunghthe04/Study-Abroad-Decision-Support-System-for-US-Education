using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence;

/// <summary>
/// Điều kiện "trường khớp chữ tìm" dùng chung cho mọi ô tìm trường (danh sách trường, chi phí, admin, học bổng):
/// tên, mã hoặc tên viết tắt (cột aliases: MIT, UCLA, ASU). Mỗi màn hình vẫn tự thêm bộ lọc riêng của nó.
/// </summary>
public static class UniversitySearch
{
    /// <param name="includeCity">Trang tìm trường công khai cho gõ cả thành phố ("Boston").</param>
    public static Expression<Func<University, bool>> Matches(string term, bool includeCity = false)
    {
        var pattern = $"%{term.Trim()}%";
        return u => EF.Functions.ILike(u.Name, pattern)
                    || EF.Functions.ILike(u.Code, pattern)
                    || u.Aliases.Any(a => EF.Functions.ILike(a, pattern))
                    || (includeCity && u.City != null && EF.Functions.ILike(u.City, pattern));
    }

    /// <summary>Khớp đúng nguyên chữ (tên viết tắt, mã hoặc tên): xếp lên đầu để "MIT" không bị "Smith College" đẩy khỏi trang đầu.</summary>
    public static Expression<Func<University, bool>> ExactHit(string term)
    {
        var t = term.Trim();
        return u => EF.Functions.ILike(u.Code, t)
                    || EF.Functions.ILike(u.Name, t)
                    || u.Aliases.Any(a => EF.Functions.ILike(a, t));
    }
}
