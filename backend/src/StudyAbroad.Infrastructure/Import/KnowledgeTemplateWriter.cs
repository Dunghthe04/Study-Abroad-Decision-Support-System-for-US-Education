using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Import;

/// <summary>File mẫu: sheet knowledge_base (tiêu đề + 2 dòng ví dụ) và sheet huong_dan (loại nội dung, nhãn, quy tắc).</summary>
public class KnowledgeTemplateWriter : IKnowledgeTemplateWriter
{
    private static readonly string[] GuideHeaders = ["muc", "gia_tri", "ghi_chu"];

    public byte[] Build()
    {
        List<string?[]> examples =
        [
            [
                "1",
                "faq",
                "LABEL_VISA_F1",
                "Phỏng vấn visa F-1 thường hỏi những gì?",
                "Viên chức lãnh sự thường hỏi về trường và ngành đã chọn, kế hoạch học tập, khả năng tài chính và ràng buộc với Việt Nam. Hãy trả lời ngắn, đúng sự thật và khớp với hồ sơ.",
                "Study in the States - DHS",
                "https://studyinthestates.dhs.gov",
                "2026-11-01",
                null,
                null,
                null,
            ],
            [
                "2",
                "proc_overview",
                "LABEL_I20_SEVIS",
                "Form I-20 là gì?",
                "Form I-20 là giấy chứng nhận đủ điều kiện nhập học do trường cấp, dùng để xin visa F-1.",
                null,
                "https://studyinthestates.dhs.gov/students/prepare/students-and-the-form-i-20",
                "2026-11-01",
                "I-20",
                null,
                null,
            ],
        ];

        var guide = new List<string?[]>();
        foreach (var (code, name) in KnowledgeDocTypes.Names)
            guide.Add(["doc_type", code, name]);
        foreach (var label in KnowledgeLabels.All)
            guide.Add(["label", label, null]);
        guide.Add(["id", "số nguyên ≥ 1", "Bắt buộc, không trùng, không đổi sau khi nạp. Trùng id đã có thì cập nhật dòng đó."]);
        guide.Add(["question", "tối đa 500 ký tự", "Bắt buộc."]);
        guide.Add(["answer", "tối đa 8000 ký tự", "Bắt buộc."]);
        guide.Add(["source, source_url", "tên nguồn, link", "Cần ít nhất một trong hai. Nhiều link cách nhau bởi dấu ;."]);
        guide.Add(["retrieved_at", "yyyy-mm-dd", "Bắt buộc."]);
        guide.Add(["topic_code", "tối đa 64 ký tự", "Bắt buộc với doc_type bắt đầu bằng proc_."]);
        guide.Add(["image_url", "link http(s)", "Không bắt buộc."]);

        return XlsxPackage.Write(
        [
            new XlsxSheetData(KnowledgeWorkbookReader.SheetName, KnowledgeWorkbookReader.RequiredHeaders, examples),
            new XlsxSheetData("huong_dan", GuideHeaders, guide),
        ]);
    }
}
