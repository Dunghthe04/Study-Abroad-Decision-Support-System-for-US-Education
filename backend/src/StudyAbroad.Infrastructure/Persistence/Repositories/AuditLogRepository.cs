using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class AuditLogRepository(AppDbContext db) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
    {
        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var idStr = userId.ToString();
        var idUpper = idStr.ToUpperInvariant();
        var detailJson = await db.AuditLogs
            .AsNoTracking()
            .Where(x => x.Action == "user.lock" && (x.EntityId == idStr || x.EntityId == idUpper))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.DetailJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(detailJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(detailJson);
            if (doc.RootElement.TryGetProperty("reason", out var reasonElem) ||
                doc.RootElement.TryGetProperty("Reason", out reasonElem))
            {
                var r = reasonElem.GetString();
                if (!string.IsNullOrWhiteSpace(r)) return r;
            }
        }
        catch
        {
            // Bỏ qua lỗi parse nếu định dạng json cũ
        }
        return null;
    }

    public async Task<PagedResult<AccessLogDto>> GetAccessLogsAsync(AccessLogFilter filter, CancellationToken cancellationToken = default)
    {
        var baseQuery = from log in db.AuditLogs.AsNoTracking()
                        join u in db.Users.AsNoTracking() on log.ActorUserId equals (Guid?)u.Id into userGroup
                        from actor in userGroup.DefaultIfEmpty()
                        select new { log, actor };

        // 1. Lọc theo danh mục thao tác
        var category = filter.Category?.Trim().ToLowerInvariant() ?? AuditLogConstants.CategoryAllSensitive;
        if (category == AuditLogConstants.CategoryCenterDocument)
        {
            baseQuery = baseQuery.Where(x => x.log.Action == AuditLogConstants.CenterDocumentViewed || x.log.Action.StartsWith("center."));
        }
        else if (category == AuditLogConstants.CategoryReportedMessage)
        {
            baseQuery = baseQuery.Where(x => x.log.Action == AuditLogConstants.LeadMessageReportQueueView ||
                                             x.log.Action == AuditLogConstants.LeadMessageReportView ||
                                             x.log.Action.StartsWith("lead_message."));
        }
        else if (category == AuditLogConstants.CategorySlmExport)
        {
            baseQuery = baseQuery.Where(x => x.log.Action == AuditLogConstants.SlmSampleExport || x.log.Action.StartsWith("slm"));
        }
        else if (category == AuditLogConstants.CategoryParent)
        {
            baseQuery = baseQuery.Where(x => x.log.Action.StartsWith("parent."));
        }
        else if (category == AuditLogConstants.CategoryAccount)
        {
            baseQuery = baseQuery.Where(x => x.log.Action.StartsWith("user.") || x.log.Action.StartsWith("auth."));
        }
        else if (category == AuditLogConstants.CategoryAllSensitive)
        {
            baseQuery = baseQuery.Where(x => AuditLogConstants.SensitiveActions.Contains(x.log.Action));
        }
        // nếu category == "all": không áp dụng lọc action mặc định để xem toàn bộ log nếu cần

        // 2. Lọc theo mã action cụ thể (nếu có)
        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var actionStr = filter.Action.Trim();
            baseQuery = baseQuery.Where(x => x.log.Action == actionStr);
        }

        // 3. Lọc theo khoảng thời gian
        if (filter.FromUtc.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.log.CreatedAt >= filter.FromUtc.Value);
        }
        if (filter.ToUtc.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.log.CreatedAt < filter.ToUtc.Value);
        }

        // 4. Lọc theo ActorUserId
        if (filter.ActorUserId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.log.ActorUserId == filter.ActorUserId.Value);
        }

        // 5. Lọc theo ActorRole
        if (!string.IsNullOrWhiteSpace(filter.ActorRole))
        {
            var roleStr = filter.ActorRole.Trim();
            baseQuery = baseQuery.Where(x => x.actor != null && x.actor.Role == roleStr);
        }

        // 6. Tìm kiếm theo tên người thực hiện, email, IP hoặc mã thực thể
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            baseQuery = baseQuery.Where(x =>
                (x.actor != null && (x.actor.FullName.Contains(term) || x.actor.Email.Contains(term))) ||
                (x.log.IpAddress != null && x.log.IpAddress.Contains(term)) ||
                (x.log.EntityId != null && x.log.EntityId.Contains(term)));
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var pagedItems = await baseQuery
            .OrderByDescending(x => x.log.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => new
            {
                x.log.Id,
                x.log.CreatedAt,
                x.log.ActorUserId,
                ActorFullName = x.actor != null ? x.actor.FullName : null,
                ActorEmail = x.actor != null ? x.actor.Email : null,
                ActorRole = x.actor != null ? x.actor.Role : null,
                x.log.Action,
                x.log.EntityType,
                x.log.EntityId,
                x.log.DetailJson,
                x.log.IpAddress,
                x.log.UserAgent
            })
            .ToListAsync(cancellationToken);

        var dtos = pagedItems.Select(x => new AccessLogDto(
            Id: x.Id,
            CreatedAt: x.CreatedAt,
            ActorUserId: x.ActorUserId,
            ActorFullName: x.ActorFullName,
            ActorEmail: x.ActorEmail,
            ActorRole: x.ActorRole,
            Action: x.Action,
            ActionCategory: MapCategory(x.Action),
            ActionDescription: MapDescription(x.Action),
            EntityType: x.EntityType,
            EntityId: x.EntityId,
            DetailJson: x.DetailJson,
            IpAddress: x.IpAddress,
            UserAgent: x.UserAgent
        )).ToList();

        return new PagedResult<AccessLogDto>(dtos, filter.Page, filter.PageSize, totalCount);
    }

    public async Task<AccessLogSummaryDto> GetAccessLogSummaryAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (fromUtc.HasValue) query = query.Where(x => x.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(x => x.CreatedAt < toUtc.Value);

        var totalAll = await query.CountAsync(cancellationToken);

        var counts = await query
            .Where(x => AuditLogConstants.SensitiveActions.Contains(x.Action))
            .GroupBy(x => x.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var centerDocs = counts.FirstOrDefault(c => c.Action == AuditLogConstants.CenterDocumentViewed)?.Count ?? 0;
        var reportedQueue = counts.FirstOrDefault(c => c.Action == AuditLogConstants.LeadMessageReportQueueView)?.Count ?? 0;
        var reportedView = counts.FirstOrDefault(c => c.Action == AuditLogConstants.LeadMessageReportView)?.Count ?? 0;
        var slmExports = counts.FirstOrDefault(c => c.Action == AuditLogConstants.SlmSampleExport)?.Count ?? 0;
        var chatViews = counts.FirstOrDefault(c => c.Action == AuditLogConstants.ReviewBatchViewItem)?.Count ?? 0;

        return new AccessLogSummaryDto(
            TotalSensitiveCount: centerDocs + reportedQueue + reportedView + slmExports + chatViews,
            CenterDocumentViews: centerDocs,
            ReportedMessageViews: reportedQueue + reportedView,
            SlmSampleExports: slmExports,
            TotalAllCount: totalAll,
            ChatMessageViews: chatViews
        );
    }

    private static string MapCategory(string action) => action switch
    {
        AuditLogConstants.CenterDocumentViewed => AuditLogConstants.CategoryCenterDocument,
        var a when a.StartsWith("center.") => AuditLogConstants.CategoryCenterDocument,
        AuditLogConstants.LeadMessageReportQueueView or AuditLogConstants.LeadMessageReportView => AuditLogConstants.CategoryReportedMessage,
        var a when a.StartsWith("lead_message.") => AuditLogConstants.CategoryReportedMessage,
        AuditLogConstants.SlmSampleExport => AuditLogConstants.CategorySlmExport,
        var a when a.StartsWith("slm") => AuditLogConstants.CategorySlmExport,
        var a when a.StartsWith("parent.") => AuditLogConstants.CategoryParent,
        var a when a.StartsWith("user.") || a.StartsWith("auth.") => AuditLogConstants.CategoryAccount,
        _ => "other"
    };

    private static string MapDescription(string action) => action switch
    {
        AuditLogConstants.CenterDocumentViewed => "Xem tài liệu xác minh trung tâm",
        AuditLogConstants.LeadMessageReportQueueView => "Xem danh sách tin nhắn bị báo cáo",
        AuditLogConstants.LeadMessageReportView => "Đọc tin nhắn bị báo cáo và ngữ cảnh",
        AuditLogConstants.SlmSampleExport => "Xuất dữ liệu mẫu huấn luyện SLM (UC-102)",
        AuditLogConstants.ReviewBatchViewItem => "Xem nội dung tin nhắn chat kiểm thử (đợt duyệt)",
        "parent.acting.start" => "Bắt đầu quyền đại diện con",
        "parent.acting.stop" => "Kết thúc quyền đại diện con",
        "parent.link.create" => "Gửi yêu cầu liên kết phụ huynh",
        "parent.link.confirm" => "Xác nhận liên kết phụ huynh",
        "parent.share.consent" => "Đồng ý chia sẻ hồ sơ học sinh",
        "parent.managed_child.create" => "Tạo hồ sơ học sinh được quản lý",
        "user.lock" => "Khóa tài khoản người dùng",
        "user.unlock" => "Mở khóa tài khoản người dùng",
        "user.role_change" => "Thay đổi phân quyền vai trò",
        "user.birth_date_edit" => "Cập nhật ngày sinh và tuổi",
        "user.anonymized" => "Ẩn danh tài khoản",
        "user.deleted" => "Xóa vĩnh viễn tài khoản",
        "center.verify" => "Phê duyệt/Từ chối xác minh trung tâm",
        "center.review_remove" => "Gỡ đánh giá trung tâm vi phạm",
        "center.change_review" => "Duyệt thay đổi thông tin trung tâm",
        "center.delete_request" => "Yêu cầu xóa tài khoản trung tâm",
        "lead_message.remove" => "Gỡ bỏ tin nhắn vi phạm",
        "target_school.add" => "Thêm trường mục tiêu",
        "target_school.remove" => "Xóa trường mục tiêu",
        "target_school.update" => "Cập nhật trường mục tiêu",
        "lead.status_change" => "Cập nhật trạng thái yêu cầu tư vấn",
        "university.update" => "Cập nhật thông tin trường ĐH",
        "university.hide" => "Ẩn thông tin trường ĐH",
        "scholarship.update" => "Cập nhật thông tin học bổng",
        "grade_scale.update" => "Cập nhật bảng quy đổi điểm",
        _ => HumanizeAction(action)
    };

    private static string HumanizeAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action)) return "Thao tác hệ thống";
        var parts = action.Split(new[] { '.', '_' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(p => p.Length > 0 ? char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant() : p));
    }
}
