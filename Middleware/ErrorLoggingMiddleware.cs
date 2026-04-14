using System.Net;
using System.Text.Json;
using log4net;

namespace LISD.Middleware
{
    public class ErrorLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly ILog _log = LogManager.GetLogger(typeof(ErrorLoggingMiddleware));

        public ErrorLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);

                // Optional: log non-success server responses too
                if (context.Response.StatusCode >= 500)
                {
                    _log.Error(
                        $"Server error response. StatusCode={context.Response.StatusCode}, " +
                        $"Method={context.Request.Method}, Path={context.Request.Path}, Query={context.Request.QueryString}");
                }
            }
            catch (Exception ex)
            {
                var req = context.Request;

                var message =
                    "Unhandled API exception" + Environment.NewLine +
                    $"Method     : {req.Method}" + Environment.NewLine +
                    $"Path       : {req.Path}" + Environment.NewLine +
                    $"Query      : {req.QueryString}" + Environment.NewLine +
                    $"Host       : {req.Host}" + Environment.NewLine +
                    $"Scheme     : {req.Scheme}" + Environment.NewLine +
                    $"Remote IP  : {context.Connection.RemoteIpAddress}" + Environment.NewLine +
                    $"User-Agent : {req.Headers["User-Agent"]}";

                _log.Error(message, ex);

                if (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.ContentType = "application/json";

                    var json = JsonSerializer.Serialize(new
                    {
                        status = false,
                        message = "Internal server error",
                        data = (object?)null
                    });

                    await context.Response.WriteAsync(json);
                }
            }
        }
    }
}