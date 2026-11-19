using System.Text.Encodings.Web;
using System.Text.Json;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Import;

public class SlmTemplateWriter : ISlmTemplateWriter
{
    private static readonly string[] Headers =
        ["question", "profile", "pending_question", "intents", "split", "context", "review"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private record ExampleRow(
        string SampleType, string Question, string? Profile, string? PendingQuestion, string Intents);

    private static readonly string[] ExportHeaders =
        ["question", "answer", "topic", "source", "profile", "pending_question", "intents", "split", "context", "review"];

    public byte[] Build() =>
        XlsxPackage.Write(BuildExamples()
            .Select(e => new XlsxSheetData(e.SampleType, Headers, [ExampleValues(e)]))
            .ToList());

    public byte[] BuildExport(IReadOnlyList<SlmTrainingSample> samples) =>
        XlsxPackage.Write(SlmSampleTypes.All
            .Select(sampleType => new XlsxSheetData(sampleType, ExportHeaders, samples
                .Where(s => s.SampleType == sampleType)
                .Select(s => new[] { s.Question, s.Answer, s.Topic, s.Source, s.Profile, s.PendingQuestion, s.Intents, s.Split, s.Context, s.SourceReview })
                .ToList()))
            .ToList());

    private static List<ExampleRow> BuildExamples()
    {
        const string profileReviewQuestion = "Giải thích giúp em vì sao trường Iowa thuộc nhóm đó và em có nên nộp không?";
        const string scenarioQuestion = "Nếu em thi lại SAT được 1500 thì danh sách trường đổi thế nào?";
        const string profileJson =
            "{\"target_level\":\"đại học\",\"major\":\"Tài chính\",\"intake\":\"Fall 2027\",\"gpa\":\"8 thang 10\",\"english\":\"IELTS 6\"}";

        var examplesByType = new Dictionary<string, ExampleRow>
        {
            [SlmSampleTypes.CollectProfile] = new(
                SlmSampleTypes.CollectProfile,
                "em học lớp 10 ạ",
                null,
                "Hiện bạn đang học lớp mấy, hoặc đang làm gì?",
                BuildIntents(SlmSampleTypes.CollectProfile, "em học lớp 10 ạ", new { current_grade = "em học lớp 10 ạ" })),
            [SlmSampleTypes.ProfileReview] = new(
                SlmSampleTypes.ProfileReview,
                profileReviewQuestion,
                profileJson,
                null,
                BuildIntents(SlmSampleTypes.ProfileReview, profileReviewQuestion,
                    new { tasks = new[] { "explain_school" }, schools = new[] { "Iowa" } })),
            [SlmSampleTypes.Scenario] = new(
                SlmSampleTypes.Scenario,
                scenarioQuestion,
                profileJson,
                null,
                BuildIntents(SlmSampleTypes.Scenario, scenarioQuestion,
                    new { mode = "what_if", changes = new[] { new { factor = "sat", value = "1500" } } })),
            [SlmSampleTypes.Knowledge] = new(
                SlmSampleTypes.Knowledge,
                "visa F-1 cần giấy tờ gì vậy",
                null,
                null,
                BuildIntents(SlmSampleTypes.Knowledge, "visa F-1 cần giấy tờ gì vậy", new { })),
            [SlmSampleTypes.CenterReferral] = new(
                SlmSampleTypes.CenterReferral,
                "ai sửa bài luận giúp em được không",
                null,
                null,
                BuildIntents(SlmSampleTypes.CenterReferral, "ai sửa bài luận giúp em được không", new { service = "ESSAY_EDITING" })),
            [SlmSampleTypes.OutOfScope] = new(
                SlmSampleTypes.OutOfScope,
                "bạn có người yêu chưa",
                null,
                null,
                BuildIntents(SlmSampleTypes.OutOfScope, "bạn có người yêu chưa", new { subtype = "exception" })),
        };

        var ordered = new List<ExampleRow>();
        foreach (var sampleType in SlmSampleTypes.All)
            ordered.Add(examplesByType[sampleType]);
        return ordered;
    }

    private static string BuildIntents(string type, string span, object entities)
    {
        var intent = new Dictionary<string, object>
        {
            ["type"] = type,
            ["span"] = span,
            ["entities"] = entities,
        };
        return JsonSerializer.Serialize(new[] { intent }, JsonOptions);
    }

    private static string?[] ExampleValues(ExampleRow example) =>
    [
        example.Question,
        example.Profile,
        example.PendingQuestion,
        example.Intents,
        SlmSampleSplits.Train,
        null,
        null,
    ];
}