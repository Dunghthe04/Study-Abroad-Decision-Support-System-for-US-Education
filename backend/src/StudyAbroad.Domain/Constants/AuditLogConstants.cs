namespace StudyAbroad.Domain.Constants;

/// <summary>
/// Hằng số định danh các thao tác nhật ký truy cập (UC-97) và danh mục dữ liệu nhạy cảm.
/// </summary>
public static class AuditLogConstants
{
    // 1. Tài liệu xác minh trung tâm (UC-59 / BR-C5)
    public const string CenterDocumentViewed = "center.document_viewed";

    // 2. Tin nhắn bị báo cáo (UC-58)
    public const string LeadMessageReportQueueView = "lead_message.report_queue_view";
    public const string LeadMessageReportView = "lead_message.report_view";

    // 3. Xuất mẫu huấn luyện SLM (UC-102)
    public const string SlmSampleExport = "slm_sample.export";

    /// <summary>Danh mục lọc: Tất cả dữ liệu nhạy cảm (mặc định của UC-97).</summary>
    public const string CategoryAllSensitive = "all_sensitive";

    /// <summary>Danh mục lọc: Tài liệu xác minh trung tâm.</summary>
    public const string CategoryCenterDocument = "center_document";

    /// <summary>Danh mục lọc: Tin nhắn bị báo cáo.</summary>
    public const string CategoryReportedMessage = "reported_message";

    /// <summary>Danh mục lọc: Xuất mẫu huấn luyện SLM.</summary>
    public const string CategorySlmExport = "slm_export";

    /// <summary>Danh mục lọc: Tất cả thao tác hệ thống.</summary>
    public const string CategoryAll = "all";

    /// <summary>Danh sách mọi thao tác truy cập hoặc xuất dữ liệu nhạy cảm.</summary>
    public static readonly IReadOnlyList<string> SensitiveActions = new[]
    {
        CenterDocumentViewed,
        LeadMessageReportQueueView,
        LeadMessageReportView,
        SlmSampleExport
    };

    public static readonly IReadOnlyList<string> ReportedMessageActions = new[]
    {
        LeadMessageReportQueueView,
        LeadMessageReportView
    };
}

