using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyAbroad.Application;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Infrastructure;
using StudyAbroad.Infrastructure.Payments;

namespace StudyAbroad.UnitTests;

// Cấu hình PayOs / Billing: ghi đè bằng biến môi trường, chọn cổng thật hay giả, giá trị trong appsettings
public class PaymentConfigTests
{
    private static ServiceProvider Provider(Dictionary<string, string?> values)
    {
        values.TryAdd("ConnectionStrings:Default", "Host=localhost;Database=unused");
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddInfrastructure(config);
        services.AddApplication();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Defaults_RealGateway_WithEmptyKeys_ResolvesWithoutThrowing()
    {
        using var sp = Provider([]);

        var options = sp.GetRequiredService<PayOsOptions>();
        var gateway = sp.GetRequiredService<IPaymentGateway>();

        Assert.Equal(("", "", "", "http://localhost:3000", false), (options.ClientId, options.ApiKey, options.ChecksumKey, options.ReturnUrlBase, options.UseFake));
        Assert.IsType<PayOsGateway>(gateway);   // khóa trống chỉ lỗi khi dùng, không lỗi lúc khởi động
    }

    [Fact]
    public void EnvOverrides_WinOverTheSection_AndEmptyEnvFallsBack()
    {
        using var sp = Provider(new()
        {
            ["PayOs:ClientId"] = "section-id", ["PayOs:ApiKey"] = "section-key", ["PayOs:ChecksumKey"] = "section-checksum", ["PayOs:ReturnUrlBase"] = "https://section.test",
            ["PAYOS_CLIENT_ID"] = "env-id", ["PAYOS_API_KEY"] = "", ["PAYOS_CHECKSUM_KEY"] = "env-checksum", ["PAYOS_RETURN_URL_BASE"] = "https://env.test",
        });

        var options = sp.GetRequiredService<PayOsOptions>();

        Assert.Equal(("env-id", "section-key", "env-checksum", "https://env.test"), (options.ClientId, options.ApiKey, options.ChecksumKey, options.ReturnUrlBase));
    }

    [Theory]
    [InlineData("true", null, true)]
    [InlineData("TRUE", "false", true)]
    [InlineData("false", "true", false)]   // biến môi trường thắng appsettings
    [InlineData(null, "true", true)]
    [InlineData(null, null, false)]
    [InlineData("", "true", true)]         // rỗng coi như chưa đặt
    [InlineData("không phải bool", "true", true)]
    public void UseFake_PicksTheFakeGateway(string? env, string? section, bool expectFake)
    {
        var values = new Dictionary<string, string?> { ["PAYOS_CHECKSUM_KEY"] = "k" };
        if (env is not null) values["PAYOS_USE_FAKE"] = env;
        if (section is not null) values["PayOs:UseFake"] = section;
        using var sp = Provider(values);

        var gateway = sp.GetRequiredService<IPaymentGateway>();

        Assert.Equal(expectFake, gateway is FakePaymentGateway);
        Assert.Equal(!expectFake, gateway is PayOsGateway);
    }

    [Fact]
    public void FakeGatewayState_IsASingleton_SharedByTheGatewayAndTheDevController()
    {
        using var sp = Provider(new() { ["PAYOS_USE_FAKE"] = "true" });

        Assert.Same(sp.GetRequiredService<FakeGatewayState>(), sp.GetRequiredService<FakeGatewayState>());
        Assert.Same(sp.GetRequiredService<IPaymentGateway>(), sp.GetRequiredService<IPaymentGateway>());
    }

    [Fact]
    public void BillingOptions_DefaultsAndOverrides()
    {
        using var defaults = Provider([]);
        using var custom = Provider(new() { ["Billing:OrderExpiryMinutes"] = "1", ["Billing:JobIntervalSeconds"] = "2", ["Billing:ReminderDays"] = "3", ["Billing:PendingGraceMinutes"] = "4" });

        var d = defaults.GetRequiredService<BillingOptions>();
        var c = custom.GetRequiredService<BillingOptions>();

        Assert.Equal((30, 60, 7, 2), (d.OrderExpiryMinutes, d.JobIntervalSeconds, d.ReminderDays, d.PendingGraceMinutes));
        Assert.Equal((1, 2, 3, 4), (c.OrderExpiryMinutes, c.JobIntervalSeconds, c.ReminderDays, c.PendingGraceMinutes));
    }

    [Fact]
    public void OrderServiceAndRepository_AreRegistered()
    {
        using var sp = Provider([]);

        Assert.NotNull(sp.GetService<IOrderRepository>());
        Assert.NotNull(sp.GetService<IOrderService>());
    }

    // ---- appsettings thật của dự án Api

    private static IConfigurationRoot LoadSettings(params string[] files)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "StudyAbroad.Api", "appsettings.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var builder = new ConfigurationBuilder();
        foreach (var file in files)
            builder.AddJsonFile(Path.Combine(dir.FullName, "src", "StudyAbroad.Api", file), optional: false);
        return builder.Build();
    }

    [Fact]
    public void Appsettings_HasBothSections_WithEmptyKeysAndNoFakeGateway()
    {
        var config = LoadSettings("appsettings.json");

        var payOs = config.GetSection("PayOs").Get<PayOsOptions>()!;
        var billing = config.GetSection("Billing").Get<BillingOptions>()!;

        Assert.Equal(("", "", "", false), (payOs.ClientId, payOs.ApiKey, payOs.ChecksumKey, payOs.UseFake));
        Assert.Equal("http://localhost:3000", payOs.ReturnUrlBase);
        Assert.Equal((30, 60, 7, 2), (billing.OrderExpiryMinutes, billing.JobIntervalSeconds, billing.ReminderDays, billing.PendingGraceMinutes));
    }

    [Fact]
    public void DevelopmentSettings_TurnOnTheFakeGatewayWithTheDevChecksumKey()
    {
        var payOs = LoadSettings("appsettings.json", "appsettings.Development.example.json").GetSection("PayOs").Get<PayOsOptions>()!;

        Assert.Equal((true, "dev-checksum-key"), (payOs.UseFake, payOs.ChecksumKey));
    }
}
