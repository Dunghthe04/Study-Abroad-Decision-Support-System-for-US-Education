using StudyAbroad.Application.TargetSchools;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>Khu "Con của tôi": phụ huynh xem dữ liệu của con đã liên kết, chỉ đọc.
/// Không có liên kết còn hiệu lực với con đó thì trả null (controller trả 404), giống UC-86/87.</summary>
public class ParentChildrenService(IParentLinkRepository links, ITargetSchoolRepository targetSchools)
{
    // Danh sách trường mục tiêu của con. Chỉ gọi repository của Đức, không sửa.
    public async Task<IReadOnlyList<TargetSchoolDto>?> GetTargetSchoolsAsync(Guid parentId, Guid studentId, CancellationToken ct)
    {
        if (!await IsLinkedAsync(parentId, studentId, ct))
            return null;

        return await targetSchools.GetListByUserAsync(studentId, null, null, ct);
    }

    // Cùng cách kiểm tra với RecommendationHistoryService.CanViewAsync, nhưng không cho "xem của chính mình":
    // đây là khu của phụ huynh, chỉ xem con.
    private async Task<bool> IsLinkedAsync(Guid parentId, Guid studentId, CancellationToken ct) =>
        (await links.GetActiveByParentAsync(parentId, ct)).Any(l => l.StudentUserId == studentId);
}
