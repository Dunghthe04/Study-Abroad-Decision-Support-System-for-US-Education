using System.Text;
using System.Text.RegularExpressions;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

public partial class KnowledgeBaseService(
    IKnowledgeBaseRepository repository, IKnowledgeWorkbookReader workbookReader) : IKnowledgeBaseService
{
    public const int MaxPageSize = 100;
    public const int MaxQuestionLength = 500;
    public const int MaxAnswerLength = 8000;
    public const int MaxSourceLength = 2000;
    public const int MaxSourceUrlLength = 4000;
    public const int MaxTopicCodeLength = 64;
    public const int MaxNoteLength = 4000;
    public const int MaxEntryNo = 1_000_000;
    private static readonly string[] Sorts = ["newest", "oldest", "entryNo", "updated"];

    public async Task<PagedResult<KnowledgeEntryDto>> GetPageAsync(
        KnowledgeEntryFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var cleanFilter = NormalizeFilter(filter);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (rows, total) = await repository.PageAsync(cleanFilter, (page - 1) * pageSize, pageSize, ct);
        return new PagedResult<KnowledgeEntryDto>(rows.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<KnowledgeEntryDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        await repository.FindAsync(id, ct) is { } document ? ToDto(document) : null;

    public async Task<KnowledgeEntryDto> CreateAsync(SaveKnowledgeEntryRequest request, CancellationToken ct = default)
    {
        var valid = Validate(request);
        var entryNo = request.EntryNo is { } given ? CheckEntryNo(given) : await repository.MaxEntryNoAsync(ct) + 1;
        if (await repository.FindIdByEntryNoAsync(entryNo, ct) is { } existing)
            throw new KnowledgeDuplicateEntryException(existing);

        var document = new KnowledgeDocument { EntryNo = entryNo };
        Apply(document, valid);
        await repository.AddAsync(document, ct);
        return ToDto(document);
    }

    public async Task<KnowledgeEntryDto?> UpdateAsync(Guid id, SaveKnowledgeEntryRequest request, CancellationToken ct = default)
    {
        var valid = Validate(request);
        var questionChanged = false;
        var found = await repository.UpdateAsync(id, document =>
        {
            questionChanged = document.Question != valid.Question;
            Apply(document, valid);
            document.UpdatedAt = DateTime.UtcNow;
        }, ct);
        if (!found)
            return null;
        if (questionChanged)
            await repository.ClearEmbeddingsAsync([id], ct);
        return await GetAsync(id, ct);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => repository.DeleteAsync(id, ct);

    private record ValidDocument(
        string DocType, string Label, string Question, string Answer, string? Source, string? SourceUrl,
        DateOnly RetrievedAt, string? TopicCode, string? ImageUrl, string? Note)
    {
        public static ValidDocument From(KnowledgeDocument document) => new(
            document.DocType, document.Label, document.Question, document.Answer, document.Source, document.SourceUrl,
            document.RetrievedAt, document.TopicCode, document.ImageUrl, document.Note);
    }

    private static ValidDocument Validate(SaveKnowledgeEntryRequest? request)
    {
        if (request is null)
            throw new ArgumentException("Thiếu nội dung tri thức.");

        var docType = Clean(request.DocType)?.ToLowerInvariant()
            ?? throw new ArgumentException("Cần chọn loại nội dung (doc_type).");
        if (!KnowledgeDocTypes.All.Contains(docType))
            throw new ArgumentException($"doc_type phải là một trong: {string.Join(", ", KnowledgeDocTypes.All)}.");

        var label = Clean(request.Label)?.ToUpperInvariant()
            ?? throw new ArgumentException("Cần chọn nhãn (label).");
        if (!KnowledgeLabels.All.Contains(label))
            throw new ArgumentException($"Nhãn \"{label}\" không có trong danh sách nhãn của kho tri thức.");

        var question = CleanLine(request.Question)
            ?? throw new ArgumentException("Câu hỏi (question) không được để trống.");
        if (question.Length > MaxQuestionLength)
            throw new ArgumentException($"Câu hỏi tối đa {MaxQuestionLength} ký tự.");

        var answer = CleanText(request.Answer)
            ?? throw new ArgumentException("Câu trả lời (answer) không được để trống.");
        if (answer.Length > MaxAnswerLength)
            throw new ArgumentException($"Câu trả lời tối đa {MaxAnswerLength} ký tự.");

        var source = CleanText(request.Source);
        var sourceUrl = CleanText(request.SourceUrl);
        if (source is null && sourceUrl is null)
            throw new ArgumentException("Cần ít nhất một trong hai: source (tên nguồn) hoặc source_url (link).");
        if (source is { Length: > MaxSourceLength })
            throw new ArgumentException($"source tối đa {MaxSourceLength} ký tự.");
        if (sourceUrl is { Length: > MaxSourceUrlLength })
            throw new ArgumentException($"source_url tối đa {MaxSourceUrlLength} ký tự.");

        var retrievedAt = request.RetrievedAt
            ?? throw new ArgumentException("Cần nhập ngày thu thập (retrieved_at).");

        var topicCode = Clean(request.TopicCode);
        if (topicCode is { Length: > MaxTopicCodeLength })
            throw new ArgumentException($"topic_code tối đa {MaxTopicCodeLength} ký tự.");
        if (topicCode is null && KnowledgeDocTypes.IsProcedure(docType))
            throw new ArgumentException("Dòng thủ tục (proc_*) bắt buộc có topic_code.");

        var imageUrl = Clean(request.ImageUrl);
        if (imageUrl is not null && !AdminEditHelpers.IsHttpUrl(imageUrl))
            throw new ArgumentException("image_url phải là đường dẫn http(s).");

        var note = CleanText(request.Note);
        if (note is { Length: > MaxNoteLength })
            throw new ArgumentException($"note tối đa {MaxNoteLength} ký tự.");

        return new ValidDocument(docType, label, question, answer, source, sourceUrl, retrievedAt, topicCode, imageUrl, note);
    }

    private static int CheckEntryNo(int entryNo) =>
        entryNo is >= 1 and <= MaxEntryNo
            ? entryNo
            : throw new ArgumentException($"Mã số (id) phải là số nguyên từ 1 đến {MaxEntryNo}.");

    private static void Apply(KnowledgeDocument document, ValidDocument valid)
    {
        if (document.Question != valid.Question)
        {
            document.EmbeddingHash = null;
            document.IngestedAt = null;
        }
        document.DocType = valid.DocType;
        document.Label = valid.Label;
        document.Question = valid.Question;
        document.Answer = valid.Answer;
        document.Source = valid.Source;
        document.SourceUrl = valid.SourceUrl;
        document.RetrievedAt = valid.RetrievedAt;
        document.TopicCode = valid.TopicCode;
        document.ImageUrl = valid.ImageUrl;
        document.Note = valid.Note;
    }

    private static KnowledgeEntryFilter NormalizeFilter(KnowledgeEntryFilter? filter)
    {
        var docType = Clean(filter?.DocType)?.ToLowerInvariant();
        var label = Clean(filter?.Label)?.ToUpperInvariant();
        var sort = Clean(filter?.Sort);

        if (docType is not null && !KnowledgeDocTypes.All.Contains(docType))
            throw new ArgumentException($"doc_type phải là một trong: {string.Join(", ", KnowledgeDocTypes.All)}.");
        if (label is not null && !KnowledgeLabels.All.Contains(label))
            throw new ArgumentException("Nhãn không có trong danh sách nhãn của kho tri thức.");
        if (sort is not null && !Sorts.Contains(sort))
            throw new ArgumentException($"Sắp xếp phải là một trong: {string.Join(", ", Sorts)}.");

        return new KnowledgeEntryFilter(Clean(filter?.Search), docType, label, Clean(filter?.TopicCode), sort);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? CleanLine(string? value) =>
        Clean(value) is { } text ? Regex.Replace(text.Normalize(NormalizationForm.FormC), @"\s+", " ") : null;

    private static string? CleanText(string? value) =>
        Clean(value)?.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n").Replace('\r', '\n');

    private static KnowledgeEntryDto ToDto(KnowledgeDocument document) => new(
        document.Id, document.EntryNo, document.DocType, document.Label, document.Question, document.Answer,
        document.Source, document.SourceUrl, document.RetrievedAt, document.TopicCode, document.ImageUrl,
        document.Note, document.CreatedAt, document.UpdatedAt);
}
