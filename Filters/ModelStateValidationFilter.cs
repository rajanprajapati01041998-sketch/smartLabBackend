using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LISD.Filters;

public sealed class ModelStateValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;

        var http = context.HttpContext;
        var correlationId = http.Items.TryGetValue("CorrelationId", out var v) && v is string s && !string.IsNullOrWhiteSpace(s)
            ? s
            : http.TraceIdentifier;

        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        context.Result = new BadRequestObjectResult(new
        {
            status = false,
            message = "Validation error",
            correlationId,
            data = errors
        });
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}

