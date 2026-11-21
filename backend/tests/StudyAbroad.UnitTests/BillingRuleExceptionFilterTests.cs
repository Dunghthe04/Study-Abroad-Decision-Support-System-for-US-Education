using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using StudyAbroad.Api.Filters;
using StudyAbroad.Application.Billing;
using Xunit;

namespace StudyAbroad.UnitTests;

public class BillingRuleExceptionFilterTests
{
    private static ExceptionContext ContextFor(Exception ex) =>
        new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>())
        {
            Exception = ex,
        };

    [Fact]
    public void BillingRule_WithoutExtra_ReturnsCodeAndMessageOnly()
    {
        var ctx = ContextFor(new BillingRuleException("plan_not_found", 404, "Không tìm thấy gói."));

        new BillingRuleExceptionFilter().OnException(ctx);

        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(404, result.StatusCode);
        var body = Assert.IsType<Dictionary<string, object?>>(result.Value);
        Assert.Equal(new[] { "code", "message" }, body.Keys);
        Assert.Equal("plan_not_found", body["code"]);
        Assert.Equal("Không tìm thấy gói.", body["message"]);
        Assert.True(ctx.ExceptionHandled);
    }

    [Fact]
    public void BillingRule_WithExtra_AddsEveryExtraKeyAsIs()
    {
        var extra = new Dictionary<string, object?> { ["orderCode"] = 123456L, ["retryAfterSeconds"] = null };
        var ctx = ContextFor(new BillingRuleException("payment_pending", 409, "Đang chờ thanh toán.", extra));

        new BillingRuleExceptionFilter().OnException(ctx);

        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(409, result.StatusCode);
        var body = Assert.IsType<Dictionary<string, object?>>(result.Value);
        Assert.Equal("payment_pending", body["code"]);
        Assert.Equal("Đang chờ thanh toán.", body["message"]);
        Assert.Equal(123456L, body["orderCode"]);
        Assert.True(body.ContainsKey("retryAfterSeconds"));
        Assert.Null(body["retryAfterSeconds"]);
        Assert.Equal(4, body.Count);
        Assert.True(ctx.ExceptionHandled);
    }

    [Fact]
    public void BillingRule_ExtraCannotOverrideCodeOrMessage()
    {
        var extra = new Dictionary<string, object?> { ["code"] = "hacked", ["message"] = "hacked" };
        var ctx = ContextFor(new BillingRuleException("real", 400, "thật", extra));

        new BillingRuleExceptionFilter().OnException(ctx);

        var body = Assert.IsType<Dictionary<string, object?>>(Assert.IsType<ObjectResult>(ctx.Result).Value);
        Assert.Equal("real", body["code"]);
        Assert.Equal("thật", body["message"]);
    }

    [Fact]
    public void BillingRule_ExtraDefaultsToEmpty()
    {
        var ex = new BillingRuleException("x", 400, "m");

        Assert.NotNull(ex.Extra);
        Assert.Empty(ex.Extra);
    }

    [Fact]
    public void OtherException_IsLeftUnhandled()
    {
        var ctx = ContextFor(new InvalidOperationException("boom"));

        new BillingRuleExceptionFilter().OnException(ctx);

        Assert.Null(ctx.Result);
        Assert.False(ctx.ExceptionHandled);
    }
}
