namespace StudyAbroad.Domain.Constants;

/// <summary>
/// Hằng số định danh các thao tác nhật ký truy cập và nhật ký thao tác (UC-91) cùng danh mục dữ liệu nhạy cảm.
/// </summary>
public static class AuditLogConstants
{
    // 1. Tài liệu xác minh trung tâm (UC-55 / BR-C5)
    public const string CenterDocumentViewed = "center.document_viewed";

    // 2. Tin nhắn bị báo cáo (UC-54)
    public const string LeadMessageReportQueueView = "lead_message.report_queue_view";
    public const string LeadMessageReportView = "lead_message.report_view";

    // 3. Xuất mẫu huấn luyện SLM (UC-96)
    public const string SlmSampleExport = "slm_sample.export";

    // 4. Xem chi tiết nội dung chat người dùng trong đợt duyệt mẫu (UC-148)
    public const string ReviewBatchViewItem = "review_batch.view_item";

    // 5. Xem thông tin người mua của tài khoản đã xóa (UC-124, UC-75, UC-91)
    public const string OrderDeletedBuyerViewed = "order.deleted_buyer_viewed";

    // 6. Thao tác quản lý Kho tri thức (UC-113..117 -> UC-91)
    public const string KnowledgeBaseCreate = "knowledge_base.create";
    public const string KnowledgeBaseUpdate = "knowledge_base.update";
    public const string KnowledgeBaseDelete = "knowledge_base.delete";
    public const string KnowledgeBaseRestore = "knowledge_base.restore";
    public const string KnowledgeBaseRestoreVersion = "knowledge_base.restore_version";
    public const string KnowledgeBaseImport = "knowledge_base.import";

    // 7. Thao tác quản lý Mẫu huấn luyện SLM (UC-90, UC-92..95 -> UC-91)
    public const string SlmSampleCreate = "slm_sample.create";
    public const string SlmSampleUpdate = "slm_sample.update";
    public const string SlmSampleDelete = "slm_sample.delete";
    public const string SlmSampleRestore = "slm_sample.restore";
    public const string SlmSampleRestoreVersion = "slm_sample.restore_version";
    public const string SlmSampleImport = "slm_sample.import";

    /// <summary>Danh mục lọc: Tất cả dữ liệu nhạy cảm (mặc định của UC-91).</summary>
    public const string CategoryAllSensitive = "all_sensitive";

    /// <summary>Danh mục lọc: Tài liệu xác minh trung tâm.</summary>
    public const string CategoryCenterDocument = "center_document";

    /// <summary>Danh mục lọc: Tin nhắn bị báo cáo.</summary>
    public const string CategoryReportedMessage = "reported_message";

    /// <summary>Danh mục lọc: Xuất mẫu huấn luyện SLM (UC-96).</summary>
    public const string CategorySlmExport = "slm_export";

    /// <summary>Danh mục lọc: Thao tác Kho tri thức (UC-113..117).</summary>
    public const string CategoryKnowledgeBase = "knowledge_base";

    /// <summary>Danh mục lọc: Thao tác Mẫu huấn luyện SLM (UC-90, UC-92..95).</summary>
    public const string CategorySlmSample = "slm_sample";

    /// <summary>Danh mục lọc: Tất cả thao tác hệ thống (Nhật ký thao tác - UC-91 2a).</summary>
    public const string CategoryAll = "all";

    /// <summary>Danh mục lọc: Đại diện và liên kết phụ huynh.</summary>
    public const string CategoryParent = "parent";

    /// <summary>Danh mục lọc: Tài khoản và phân quyền.</summary>
    public const string CategoryAccount = "account";

    /// <summary>Danh sách mọi thao tác truy cập hoặc xuất dữ liệu nhạy cảm.</summary>
    public static readonly IReadOnlyList<string> SensitiveActions = new[]
    {
        CenterDocumentViewed,
        LeadMessageReportQueueView,
        LeadMessageReportView,
        SlmSampleExport,
        ReviewBatchViewItem,
        OrderDeletedBuyerViewed
    };

    public static readonly IReadOnlyList<string> ReportedMessageActions = new[]
    {
        LeadMessageReportQueueView,
        LeadMessageReportView
    };
}
