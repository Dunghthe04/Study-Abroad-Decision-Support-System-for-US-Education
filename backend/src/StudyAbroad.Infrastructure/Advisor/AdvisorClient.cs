using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using StudyAbroad.Application.Advisor;

namespace StudyAbroad.Infrastructure.Advisor;

public class AdvisorClient(HttpClient http) : IAdvisorClient
{
    private static readonly JsonSerializerOptions AdvisorJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/v1/chat", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdvisorChatResponse>(ct)
               ?? throw new InvalidOperationException("Advisor service returned an empty response.");
    }

    // ChatAsync gửi và nhận bằng cấu hình mặc định này (camelCase, không phân biệt hoa thường); stream dùng giống hệt
    private static readonly JsonSerializerOptions StreamJson = new(JsonSerializerDefaults.Web);

    public async Task<AdvisorChatResponse> ChatStreamAsync(
        AdvisorChatRequest request, Func<AdvisorStreamEvent, Task> onEvent, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/chat/stream")
        {
            Content = JsonContent.Create(request),
        };
        // ResponseHeadersRead: có dòng nào là đọc dòng đó ngay, không chờ advisor trả xong cả thân
        using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(body);
        AdvisorChatResponse? result = null;
        // Mỗi dòng một JSON: progress, partial, final (đúng JSON của ChatAsync), hoặc error
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "progress":
                    await onEvent(new AdvisorStreamEvent("progress", Answers: root.GetProperty("answers").GetInt32()));
                    break;
                case "partial":
                    await onEvent(new AdvisorStreamEvent("partial", Text: root.GetProperty("text").GetString()));
                    break;
                case "final":
                    result = root.GetProperty("response").Deserialize<AdvisorChatResponse>(StreamJson);
                    break;
                case "error":
                    throw new HttpRequestException("Advisor báo lỗi giữa chừng khi trả lời.");
            }
        }
        return result ?? throw new InvalidOperationException("Advisor stream kết thúc mà không có kết quả cuối.");
    }

    public Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default) =>
        PostAsync<ExplainRequestDto, ExplainResponseDto>("api/v1/recommend/explain", request, ct);

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(url, request, AdvisorJson, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(AdvisorJson, ct)
               ?? throw new InvalidOperationException("Advisor trả về rỗng.");
    }
}