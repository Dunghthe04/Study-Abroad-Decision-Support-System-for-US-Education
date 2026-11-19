using StudyAbroad.Application.Admin.KnowledgeBase;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Import;

/// <summary>File mẫu: sheet knowledge_base (tiêu đề + 2 dòng ví dụ) và sheet huong_dan (nhãn, bậc học, giới hạn độ dài).</summary>
public class KnowledgeTemplateWriter : IKnowledgeTemplateWriter
{
    private static readonly string[] GuideHeaders = ["muc", "gia_tri", "ghi_chu"];

    public byte[] Build()
    {
        List<string?[]> examples =
        [
            [
                null,
                "LABEL_VISA_F1",
                "Đại học; Thạc sĩ",
                "Phỏng vấn visa F-1 thường hỏi những gì?",
                "Viên chức lãnh sự thường hỏi về trường và ngành đã chọn, kế hoạch học tập, khả năng tài chính và ràng buộc với Việt Nam. Hãy trả lời ngắn, đúng sự thật và khớp với hồ sơ.",
                "Hội đồng Chuyên gia DSS\nMã: EXP-KB-001\nNgày thu: 01/11/2026",
            ],
            [
                null,
                "LABEL_TIENG_ANH",
                "Chung",
                "IELTS bao nhiêu thì nộp được đại học Mỹ?",
                "Nhiều trường yêu cầu IELTS từ 6.0 đến 6.5; trường top thường cần 7.0 trở lên. Luôn xem yêu cầu trên trang tuyển sinh của từng trường.",
                "Tổng hợp kiến thức chung (chưa gắn nguồn cụ thể)\nNgày thu: 01/11/2026",
            ],
        ];

        var guide = new List<string?[]>();
        foreach (var label in KnowledgeLabels.All)
            guide.Add(["label", label, null]);
        foreach (var name in KnowledgeLevels.Names.Values)
            guide.Add(["level", name, "Nhiều bậc cách nhau bởi dấu ; (ví dụ: Đại học; Thạc sĩ). \"Chung\" phải đứng một mình."]);
        guide.Add(["id", "số nguyên ≥ 1", "Bỏ trống thì hệ thống tự cấp số."]);
        guide.Add(["question", "tối đa 500 ký tự", "Bắt buộc, không trùng câu đã có."]);
        guide.Add(["answer", "tối đa 4000 ký tự", "Bắt buộc. Không ghi số điện thoại, e-mail."]);
        guide.Add(["source", "tối đa 2000 ký tự", "Bắt buộc, được xuống dòng."]);

        return XlsxPackage.Write(
        [
            new XlsxSheetData(KnowledgeWorkbookReader.SheetName, KnowledgeWorkbookReader.RequiredHeaders, examples),
            new XlsxSheetData("huong_dan", GuideHeaders, guide),
        ]);
    }
}