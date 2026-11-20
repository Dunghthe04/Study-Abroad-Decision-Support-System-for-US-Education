using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Billing;
using StudyAbroad.Application.Billing.Orders;
using StudyAbroad.Application.Billing.Payments;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Infrastructure.Payments;

namespace StudyAbroad.UnitTests;

// Controller đơn hàng, webhook payOS và endpoint dev
public class PaymentControllersTests
{
    private static readonly DateTime Now = OrderHarness.Now;

    private static T Single<T>(object[] attributes) => attributes.OfType<T>().Single();

    // ---- OrdersController

    [Fact]
    public void OrdersController_IsForStudentsAndParentsOnly_AtTheDocumentedRoute()
    {
        var type = typeof(OrdersController);

        Assert.Equal("student,parent", Single<AuthorizeAttribute>(type.GetCustomAttributes(true)).Roles);
        Assert.Equal("api/v1/orders", Single<RouteAttribute>(type.GetCustomAttributes(true)).Template);
        Assert.Equal(["Cancel", "Create", "Get", "List"], type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Create_UsesTheUserAndRoleFromTheToken_AndReturns201WithTheOrder()
    {
        var service = new RecordingOrderService();
        var userId = Guid.NewGuid();
        var controller = new OrdersController(service) { ControllerContext = Context(userId, UserRoles.Parent) };

        var result = await controller.Create(new OrdersController.CreateOrderRequest("family-1m"), default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(("Get", 201), (created.ActionName, created.StatusCode ?? 201));
        Assert.Equal(55L, created.RouteValues!["orderCode"]);
        Assert.Equal((userId, UserRoles.Parent, "family-1m"), (service.LastUser, service.LastRole, service.LastPlan));
    }

    [Fact]
    public async Task F5_CreateWithoutAPlanCode_ReachesTheService_WhichAnswers400PlanNotForSale()
    {
        // Thân yêu cầu {} hoặc planCode null: không bị MVC chặn bằng lỗi kiểm tra model chung (cần PlanCode khai báo string?)
        var nullable = new System.Reflection.NullabilityInfoContext().Create(typeof(OrdersController.CreateOrderRequest).GetProperty(nameof(OrdersController.CreateOrderRequest.PlanCode))!);
        Assert.Equal(System.Reflection.NullabilityState.Nullable, nullable.WriteState);

        var request = JsonSerializer.Deserialize<OrdersController.CreateOrderRequest>("{}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Null(request.PlanCode);
        var service = new RecordingOrderService();
        var controller = new OrdersController(service) { ControllerContext = Context(Guid.NewGuid(), UserRoles.Student) };

        await controller.Create(request, default);

        Assert.Null(service.LastPlan);
    }

    [Fact]
    public async Task GetListCancel_PassTheTokenUserAsOwner_AndWithoutOneAreUnauthorized()
    {
        var service = new RecordingOrderService();
        var userId = Guid.NewGuid();
        var controller = new OrdersController(service) { ControllerContext = Context(userId, UserRoles.Student) };

        await controller.Get(77, default);
        Assert.Equal((userId, 77L), (service.LastUser, service.LastCode));
        await controller.Cancel(78, default);
        Assert.Equal((userId, 78L), (service.LastUser, service.LastCode));
        await controller.List(2, 10, default);
        Assert.Equal((userId, 2, 10), (service.LastUser, service.LastPage, service.LastPageSize));

        var anonymous = new OrdersController(service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<UnauthorizedResult>(await anonymous.Get(1, default));
        Assert.IsType<UnauthorizedResult>(await anonymous.Create(new OrdersController.CreateOrderRequest("x"), default));
    }

    // ---- webhook

    [Fact]
    public void WebhookController_IsAnonymous_AndHasNoRateLimitOrAuthAttributes()
    {
        var type = typeof(PayOsWebhookController);
        var attributes = type.GetCustomAttributes(true);
        var action = type.GetMethod(nameof(PayOsWebhookController.Receive))!;

        Assert.Contains(attributes, a => a is AllowAnonymousAttribute);
        Assert.DoesNotContain(attributes, a => a is AuthorizeAttribute);
        Assert.Equal("api/v1/payments/payos/webhook", Single<RouteAttribute>(attributes).Template);
        Assert.NotEmpty(action.GetCustomAttributes(typeof(HttpPostAttribute), true));
        Assert.DoesNotContain(attributes.Concat(action.GetCustomAttributes(true)), a => a.GetType().Name.Contains("RateLimit"));
    }

    [Fact]
    public async Task Webhook_PassesTheRawBodyUnchanged_Returns200WhenHandled()
    {
        var service = new RecordingOrderService { WebhookHandled = true };
        var raw = "{ \"desc\": \"Thành công\",\n  \"data\":{\"a\":1} }";
        var controller = new PayOsWebhookController(service) { ControllerContext = new ControllerContext { HttpContext = HttpWith(raw) } };

        var result = await controller.Receive(default);

        Assert.Equal(raw, service.LastWebhookBody);
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("{\"success\":true}", JsonSerializer.Serialize(ok.Value));
    }

    [Fact]
    public async Task Webhook_InvalidSignature_Returns400WithTheCode()
    {
        var service = new RecordingOrderService { WebhookHandled = false };
        var controller = new PayOsWebhookController(service) { ControllerContext = new ControllerContext { HttpContext = HttpWith("{}") } };

        var result = await controller.Receive(default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(bad.Value));
        Assert.Equal("invalid_signature", body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Webhook_EndToEndThroughTheRealService_ActivatesAndRepeatsAreHarmless()
    {
        var h = new OrderHarness();
        var buyer = h.AddUser();
        var order = await h.CreateOrder(buyer, "personal-1m");
        var raw = h.WebhookBody(h.Order(order.OrderCode));
        var controller = new PayOsWebhookController(h.Orders) { ControllerContext = new ControllerContext { HttpContext = HttpWith(raw) } };

        Assert.IsType<OkObjectResult>(await controller.Receive(default));
        controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        Assert.IsType<OkObjectResult>(await controller.Receive(default));

        Assert.Equal(OrderStatuses.Paid, h.Order(order.OrderCode).Status);
        Assert.Single(h.Store.Cycles);
    }

    // ---- endpoint dev

    [Theory]
    [InlineData("Development", true, true)]
    [InlineData("Development", false, false)]   // thiếu PayOs:UseFake
    [InlineData("Production", true, false)]     // không phải Development
    [InlineData("Staging", true, false)]
    public void DevFilter_404UnlessDevelopmentAndFakeGateway(string environment, bool useFake, bool allowed)
    {
        var filter = new DevPayOsOnlyFilter(new TestEnvironment(environment), new PayOsOptions { UseFake = useFake });
        var context = new ResourceExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>(), new List<IValueProviderFactory>());

        filter.OnResourceExecuting(context);

        if (allowed)
            Assert.Null(context.Result);
        else
            Assert.IsType<NotFoundResult>(context.Result);
    }

    [Fact]
    public void DevController_IsHiddenFromSwagger_AnonymousAndGuardedByTheFilter()
    {
        var attributes = typeof(DevPayOsController).GetCustomAttributes(true);

        Assert.Equal("api/v1/dev/payos", Single<RouteAttribute>(attributes).Template);
        Assert.True(Single<ApiExplorerSettingsAttribute>(attributes).IgnoreApi);
        Assert.Contains(attributes, a => a is AllowAnonymousAttribute);
        Assert.Equal(typeof(DevPayOsOnlyFilter), Single<TypeFilterAttribute>(attributes).ImplementationType);
    }

    [Fact]
    public async Task DevPay_BuildsASignedPayOsWebhook_AndMarksTheFakeLinkPaid()
    {
        var h = new OrderHarness();
        var order = await h.CreateOrder(h.AddUser(), "personal-1m");
        var state = new FakeGatewayState();
        var controller = new DevPayOsController(h.Orders, h.Store, h.PayOs, state);

        var result = await controller.Pay(order.OrderCode, null, default);

        Assert.Equal("{\"handled\":true}", JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value));
        var stored = h.Order(order.OrderCode);
        Assert.Equal(OrderStatuses.Paid, stored.Status);
        Assert.Equal(new GatewayStatus("PAID", 79_000), state.Get(order.OrderCode));
        // Dữ liệu tài khoản đối ứng của webhook giả bị lược khỏi payload lưu lại
        Assert.DoesNotContain("counterAccount", stored.PayloadJson);
        Assert.DoesNotContain("NGUYEN VAN A", stored.PayloadJson);
        Assert.Contains("\"reference\"", stored.PayloadJson);
    }

    [Fact]
    public async Task DevPay_WithAnotherAmount_IsAMismatch_AndAnUnknownOrderIsHandledQuietly()
    {
        var h = new OrderHarness();
        var order = await h.CreateOrder(h.AddUser(), "personal-1m");
        var controller = new DevPayOsController(h.Orders, h.Store, h.PayOs, new FakeGatewayState());

        await controller.Pay(order.OrderCode, new DevPayOsController.PayRequest(1_000), default);
        var unknown = await controller.Pay(424242, new DevPayOsController.PayRequest(5_000), default);

        Assert.Equal(OrderStatuses.Mismatch, h.Order(order.OrderCode).Status);
        Assert.Equal("{\"handled\":true}", JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(unknown).Value));
        Assert.Empty(h.Store.Cycles);
    }

    [Fact]
    public void DevSetStatus_ChangesWhatTheFakeGatewayReports()
    {
        var state = new FakeGatewayState();
        var controller = new DevPayOsController(new RecordingOrderService(), null!, new PayOsOptions(), state);

        controller.SetStatus(9, new DevPayOsController.SetStatusRequest("underpaid", 500));

        Assert.Equal(new GatewayStatus("UNDERPAID", 500), state.Get(9));
    }

    // ---- hỗ trợ

    private static ControllerContext Context(Guid userId, string role) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role)], "test")),
        },
    };

    private static DefaultHttpContext HttpWith(string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context;
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingOrderService : IOrderService
    {
        public Guid LastUser { get; private set; }
        public string? LastRole { get; private set; }
        public string? LastPlan { get; private set; }
        public long LastCode { get; private set; }
        public int LastPage { get; private set; }
        public int LastPageSize { get; private set; }
        public string? LastWebhookBody { get; private set; }
        public bool WebhookHandled { get; set; } = true;

        private static OrderDto Dto(long code) => new(code, "p", "P", "personal", 1, "USAS00001", "pending", Now, Now, null, "u", "q", null);

        public Task<OrderDto> CreateAsync(Guid userId, string role, string? planCode, DateTime nowUtc, CancellationToken ct = default)
        {
            (LastUser, LastRole, LastPlan) = (userId, role, planCode);
            return Task.FromResult(Dto(55));
        }

        public Task<OrderDto> GetMineAsync(Guid userId, long orderCode, CancellationToken ct = default)
        {
            (LastUser, LastCode) = (userId, orderCode);
            return Task.FromResult(Dto(orderCode));
        }

        public Task<PagedResult<OrderDto>> ListMineAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
        {
            (LastUser, LastPage, LastPageSize) = (userId, page, pageSize);
            return Task.FromResult(new PagedResult<OrderDto>([], page, pageSize, 0));
        }

        public Task<OrderDto> CancelAsync(Guid userId, long orderCode, DateTime nowUtc, CancellationToken ct = default)
        {
            (LastUser, LastCode) = (userId, orderCode);
            return Task.FromResult(Dto(orderCode));
        }

        public Task<PaymentApplyResult> ApplyPaymentAsync(long orderCode, long amountPaid, string? payloadJson, DateTime nowUtc, CancellationToken ct = default, PaymentApplyOptions? options = null) =>
            throw new NotSupportedException();

        public Task<bool> HandleWebhookAsync(string rawBody, DateTime nowUtc, CancellationToken ct = default)
        {
            LastWebhookBody = rawBody;
            return Task.FromResult(WebhookHandled);
        }
    }
}
