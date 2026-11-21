using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StudyAbroad.Application.Billing;

namespace StudyAbroad.Api.Filters;

/// <summary>
/// Đổi <see cref="BillingRuleException"/> thành phản hồi <c>{ code, message, ...extra }</c> với mã HTTP của lỗi.
/// Chỉ xử lý lỗi thanh toán; mọi exception khác để nguyên cho middleware lỗi chung.
/// </summary>
public sealed class BillingRuleExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not BillingRuleException ex) return;

        var body = new Dictionary<string, object?> { ["code"] = ex.Code, ["message"] = ex.Message };
        // TryAdd: trường kèm theo không được đè code / message
        foreach (var (key, value) in ex.Extra) body.TryAdd(key, value);

        context.Result = new ObjectResult(body) { StatusCode = ex.StatusCode };
        context.ExceptionHandled = true;
    }
}
