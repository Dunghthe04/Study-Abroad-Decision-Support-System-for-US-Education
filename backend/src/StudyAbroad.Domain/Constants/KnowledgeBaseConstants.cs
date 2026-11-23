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

public static class KnowledgeDocTypes
{
    public const string Faq = "faq";
    public const string Glossary = "glossary";
    public const string ProcOverview = "proc_overview";
    public const string ProcPrepare = "proc_prepare";
    public const string ProcSteps = "proc_steps";
    public const string ProcChecklist = "proc_checklist";
    public const string ProcMistakes = "proc_mistakes";
    public const string Timeline = "timeline";
    public const string Compare = "compare";
    public const string Situation = "situation";
    public const string DataTable = "data_table";
    public const string Resource = "resource";
    public const string Notice = "notice";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Faq, Glossary, ProcOverview, ProcPrepare, ProcSteps, ProcChecklist, ProcMistakes,
        Timeline, Compare, Situation, DataTable, Resource, Notice,
    };

    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        [Faq] = "Hỏi đáp",
        [Glossary] = "Thuật ngữ",
        [ProcOverview] = "Thủ tục: tổng quan",
        [ProcPrepare] = "Thủ tục: chuẩn bị",
        [ProcSteps] = "Thủ tục: các bước",
        [ProcChecklist] = "Thủ tục: danh sách kiểm tra",
        [ProcMistakes] = "Thủ tục: lỗi hay gặp",
        [Timeline] = "Mốc thời gian",
        [Compare] = "So sánh",
        [Situation] = "Tình huống",
        [DataTable] = "Bảng số liệu",
        [Resource] = "Công cụ và link",
        [Notice] = "Quy định và cảnh báo",
    };

    public static bool IsProcedure(string docType) => docType.StartsWith("proc_", StringComparison.Ordinal);
}
