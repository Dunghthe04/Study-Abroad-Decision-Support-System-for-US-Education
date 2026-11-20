namespace StudyAbroad.Domain.Constants;

/// <summary>27 nhãn chủ đề của kho tri thức. Thêm nhãn: thêm một dòng ở đây và một dòng ở frontend (knowledge-base-format.ts), không cần migration.</summary>
public static class KnowledgeLabels
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "LABEL_VISA_F1", "LABEL_DUY_TRI_F1", "LABEL_TUYEN_SINH_SAU_DAI_HOC", "LABEL_LO_TRINH_BAC_HOC",
        "LABEL_TIENG_ANH", "LABEL_THI_CHUAN_HOA", "LABEL_HO_SO_GIAY_TO", "LABEL_CHI_PHI", "LABEL_CHON_TRUONG",
        "LABEL_HOC_BONG_HO_TRO", "LABEL_THUAT_NGU", "LABEL_GPA_HOC_BA", "LABEL_I20_SEVIS",
        "LABEL_CHUNG_MINH_TAI_CHINH", "LABEL_TRUNG_TAM_TU_VAN", "LABEL_HOC_TAP_TAI_MY", "LABEL_XUAT_NHAP_CANH",
        "LABEL_DOI_SONG_AN_TOAN", "LABEL_CHON_NGANH", "LABEL_VONG_NOP_HAN_CHOT", "LABEL_BAI_LUAN",
        "LABEL_PHU_HUYNH", "LABEL_KET_QUA_TRUNG_TUYEN", "LABEL_NGOAI_KHOA_GIAI_THUONG", "LABEL_THU_GIOI_THIEU",
        "LABEL_NGOAI_PHAM_VI", "LABEL_PHONG_VAN_TUYEN_SINH",
    };
}

/// <summary>Bậc học: lưu mã (chung, thcs...) trong cột levels; file Excel và giao diện dùng tên tiếng Việt.</summary>
public static class KnowledgeLevels
{
    public const string Chung = "chung";
    public const string Thcs = "thcs";
    public const string Thpt = "thpt";
    public const string DaiHoc = "dai_hoc";
    public const string ThacSi = "thac_si";
    public const string TienSi = "tien_si";

    public static readonly IReadOnlyList<string> All = new[] { Chung, Thcs, Thpt, DaiHoc, ThacSi, TienSi };

    /// <summary>Mã → tên hiển thị, đúng như cột level của file usas_knowledge_base.xlsx.</summary>
    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        [Chung] = "Chung",
        [Thcs] = "THCS",
        [Thpt] = "THPT",
        [DaiHoc] = "Đại học",
        [ThacSi] = "Thạc sĩ",
        [TienSi] = "Tiến sĩ",
    };
}

/// <summary>Cùng 5 giá trị với SlmReviewStatuses; tách riêng để hai tính năng không buộc vào nhau.</summary>
public static class KnowledgeReviewStatuses
{
    public const string Draft = "draft";
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string NeedsFix = "needs_fix";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Pending, Approved, Rejected, NeedsFix };
}

/// <summary>Dòng tri thức đến từ đâu. Khác cột Source (trích dẫn nguồn kiến thức).</summary>
public static class KnowledgeOrigins
{
    public const string Manual = "manual";
    public const string Excel = "excel";
    public const string ChatUnanswered = "chat-unanswered";

    public static readonly IReadOnlyList<string> All = new[] { Manual, Excel, ChatUnanswered };
}

public static class KnowledgeHistoryActions
{
    public const string Create = "create";
    public const string Edit = "edit";
    public const string Submit = "submit";
    public const string Review = "review";
    public const string Delete = "delete";
    public const string Restore = "restore";
    public const string Capture = "capture";
    public const string Migrate = "migrate";
}