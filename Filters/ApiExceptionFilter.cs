using log4net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LISD.Filters;

public sealed class ApiExceptionFilter : IAsyncExceptionFilter
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(ApiExceptionFilter));

    public Task OnExceptionAsync(ExceptionContext context)
    {
        var http = context.HttpContext;
        var req = http.Request;

        var correlationId = http.Items.TryGetValue("CorrelationId", out var v) && v is string s && !string.IsNullOrWhiteSpace(s)
            ? s
            : http.TraceIdentifier;

        Log.Error(
            "Unhandled controller exception" + Environment.NewLine +
            $"Correlation: {correlationId}" + Environment.NewLine +
            $"Method     : {req.Method}" + Environment.NewLine +
            $"Path       : {req.Path}" + Environment.NewLine +
            $"Query      : {req.QueryString}",
            context.Exception);

        context.Result = new ObjectResult(new
        {
            status = false,
            message = "Internal server error",
            correlationId,
            data = (object?)null
        })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };

        context.ExceptionHandled = true;
        return Task.CompletedTask;
    }
}

