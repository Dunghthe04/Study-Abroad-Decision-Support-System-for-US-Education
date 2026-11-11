using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using StudyAbroad.Application;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Infrastructure;

namespace StudyAbroad.UnitTests;

// Kiểm tra việc NỐI DI thật: IAdvisorClient là lớp bọc gom số đo, mỗi yêu cầu có một giỏ riêng, và JSON advisor gửi (có aiUsage)
// đi qua HTTP thật của AdvisorClient vào đúng giỏ. Test đơn vị không bắt được lỗi nối dây ở đây.
public class AiUsageWiringTests
{
    private const string AdvisorReply =
        "{\"answer\":\"x\",\"citations\":[],\"aiUsage\":[{\"source\":\"ollama\",\"model\":\"usas-slm-v4\",\"purpose\":\"slm_intents\"," +
        "\"skillVersion\":\"3.0\",\"inputTokens\":1700,\"outputTokens\":180,\"cachedTokens\":0,\"ok\":true,\"error\":null,\"latencyMs\":21000}]}";

    private static ServiceProvider Build()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=localhost;Database=unused;Username=u;Password=p",
            ["Advisor:BaseUrl"] = "http://advisor.test",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddInfrastructure(config);
        services.AddApplication();
        // Mọi cuộc gọi HTTP tới advisor trả JSON mẫu có aiUsage, không cần chạy advisor thật
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = new CannedHandler()));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void TheAdvisorClientTheAppUses_IsTheOneThatCollectsUsage()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        Assert.IsType<UsageCollectingAdvisorClient>(scope.ServiceProvider.GetRequiredService<IAdvisorClient>());
    }

    [Fact]
    public async Task ARealAdvisorReply_PutsItsAiUsageIntoTheBasketOfTheRequest()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        var response = await scope.ServiceProvider.GetRequiredService<IAdvisorClient>().ChatAsync(new AdvisorChatRequest([]));

        var basket = scope.ServiceProvider.GetRequiredService<IAiUsageCollector>().Snapshot();
        var call = Assert.Single(basket);
        Assert.Equal(("ollama", "slm_intents", "3.0", 1700, 180, 21000),
            (call.Source, call.Purpose, call.SkillVersion, call.InputTokens, call.OutputTokens, call.LatencyMs));
        Assert.Equal("x", response.Answer);   // phản hồi vẫn nguyên vẹn cho người gọi
    }

    [Fact]
    public async Task EachRequestHasItsOwnBasket()
    {
        using var provider = Build();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        await first.ServiceProvider.GetRequiredService<IAdvisorClient>().ChatAsync(new AdvisorChatRequest([]));

        Assert.Single(first.ServiceProvider.GetRequiredService<IAiUsageCollector>().Snapshot());
        Assert.Empty(second.ServiceProvider.GetRequiredService<IAiUsageCollector>().Snapshot());   // lượt khác không lẫn số đo
    }

    [Fact]
    public void WithinOneRequest_EveryoneSharesTheSameBasket()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        Assert.Same(scope.ServiceProvider.GetRequiredService<IAiUsageCollector>(), scope.ServiceProvider.GetRequiredService<IAiUsageCollector>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<IAdvisorClient>(), scope.ServiceProvider.GetRequiredService<IAdvisorClient>());
    }

    [Fact]
    public void TheChatServiceCanBeBuiltWithTheRealWiring()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ChatService>());
    }

    private sealed class CannedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AdvisorReply, Encoding.UTF8, "application/json"),
            });
    }
}
