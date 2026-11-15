using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Infrastructure.Import;

public class SlmTemplateWriter : ISlmTemplateWriter
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace DocRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    private const string WorkbookContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
    private const string WorksheetContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
    private const string RelationshipsContentType = "application/vnd.openxmlformats-package.relationships+xml";
    private const string WorkbookRelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
    private const string WorksheetRelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";

    private static readonly string[] Headers =
        ["question", "profile", "pending_question", "intents", "split", "context", "review"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private record ExampleRow(
        string SampleType, string Question, string? Profile, string? PendingQuestion, string Intents);

    public byte[] Build()
    {
        var examples = BuildExamples();

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypes(examples.Count));
            WriteEntry(archive, "_rels/.rels", BuildPackageRelationships());
            WriteEntry(archive, "xl/workbook.xml", BuildWorkbook(examples));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(examples.Count));

            for (var index = 0; index < examples.Count; index++)
                WriteEntry(archive, $"xl/worksheets/sheet{index + 1}.xml", BuildSheet(examples[index]));
        }
        return output.ToArray();
    }

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

    private static XDocument BuildContentTypes(int sheetCount)
    {
        var types = new XElement(ContentTypes + "Types",
            new XElement(ContentTypes + "Default",
                new XAttribute("Extension", "rels"), new XAttribute("ContentType", RelationshipsContentType)),
            new XElement(ContentTypes + "Default",
                new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
            new XElement(ContentTypes + "Override",
                new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", WorkbookContentType)));

        for (var index = 1; index <= sheetCount; index++)
        {
            types.Add(new XElement(ContentTypes + "Override",
                new XAttribute("PartName", $"/xl/worksheets/sheet{index}.xml"),
                new XAttribute("ContentType", WorksheetContentType)));
        }
        return new XDocument(types);
    }

    private static XDocument BuildPackageRelationships() =>
        new(new XElement(PkgRel + "Relationships",
            new XElement(PkgRel + "Relationship",
                new XAttribute("Id", "rId1"),
                new XAttribute("Type", WorkbookRelationshipType),
                new XAttribute("Target", "xl/workbook.xml"))));

    private static XDocument BuildWorkbook(List<ExampleRow> examples)
    {
        var sheets = new XElement(Main + "sheets");
        for (var index = 0; index < examples.Count; index++)
        {
            sheets.Add(new XElement(Main + "sheet",
                new XAttribute("name", examples[index].SampleType),
                new XAttribute("sheetId", index + 1),
                new XAttribute(DocRel + "id", $"rId{index + 1}")));
        }

        var workbook = new XElement(Main + "workbook", new XAttribute(XNamespace.Xmlns + "r", DocRel.NamespaceName), sheets);
        return new XDocument(workbook);
    }

    private static XDocument BuildWorkbookRelationships(int sheetCount)
    {
        var relationships = new XElement(PkgRel + "Relationships");
        for (var index = 1; index <= sheetCount; index++)
        {
            relationships.Add(new XElement(PkgRel + "Relationship",
                new XAttribute("Id", $"rId{index}"),
                new XAttribute("Type", WorksheetRelationshipType),
                new XAttribute("Target", $"worksheets/sheet{index}.xml")));
        }
        return new XDocument(relationships);
    }

    private static XDocument BuildSheet(ExampleRow example)
    {
        var values = new string?[]
        {
            example.Question,
            example.Profile,
            example.PendingQuestion,
            example.Intents,
            SlmSampleSplits.Train,
            null,
            null,
        };

        var sheetData = new XElement(Main + "sheetData");
        sheetData.Add(BuildRow(1, Headers));
        sheetData.Add(BuildRow(2, values));
        return new XDocument(new XElement(Main + "worksheet", sheetData));
    }

    private static XElement BuildRow(int rowNumber, IReadOnlyList<string?> values)
    {
        var row = new XElement(Main + "row", new XAttribute("r", rowNumber));
        for (var column = 0; column < values.Count; column++)
        {
            var text = values[column];
            if (string.IsNullOrEmpty(text))
                continue;

            var reference = $"{(char)('A' + column)}{rowNumber}";
            var textElement = new XElement(Main + "t", text);
            textElement.SetAttributeValue(XNamespace.Xml + "space", "preserve");
            row.Add(new XElement(Main + "c",
                new XAttribute("r", reference),
                new XAttribute("t", "inlineStr"),
                new XElement(Main + "is", textElement)));
        }
        return row;
    }

    private static void WriteEntry(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
        };
        using var writer = XmlWriter.Create(entryStream, settings);
        document.Save(writer);
    }
}
