using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using log4net;

namespace LISD.Middleware
{
    public class ErrorLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly ILog _log = LogManager.GetLogger(typeof(ErrorLoggingMiddleware));
        private const int MaxLoggedBodyChars = 8_000;

        public ErrorLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = EnsureCorrelationId(context);
            var originalResponseBody = context.Response.Body;

            await using var responseBuffer = new MemoryStream();
            context.Response.Body = responseBuffer;

            try
            {
                context.Request.EnableBuffering();

                await _next(context);

                await LogNonSuccessResponseIfAny(context, correlationId);
            }
            catch (Exception ex)
            {
                var req = context.Request;

                var message =
                    "Unhandled API exception" + Environment.NewLine +
                    $"Correlation: {correlationId}" + Environment.NewLine +
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
                        correlationId,
                        data = (object?)null
                    });

                    // restore original body stream so the error response isn't swallowed by the buffer
                    context.Response.Body = originalResponseBody;
                    await context.Response.WriteAsync(json);
                }
            }
            finally
            {
                try
                {
                    if (context.Response.Body == responseBuffer)
                    {
                        responseBuffer.Position = 0;
                        await responseBuffer.CopyToAsync(originalResponseBody);
                    }
                }
                finally
                {
                    context.Response.Body = originalResponseBody;
                }
            }
        }

        private static string EnsureCorrelationId(HttpContext context)
        {
            var existing = context.Request.Headers["X-Correlation-Id"].ToString();
            var correlationId = string.IsNullOrWhiteSpace(existing)
                ? (Activity.Current?.Id ?? context.TraceIdentifier ?? Guid.NewGuid().ToString("N"))
                : existing;

            context.Items["CorrelationId"] = correlationId;
            if (!context.Response.HasStarted)
            {
                context.Response.Headers["X-Correlation-Id"] = correlationId;
            }

            return correlationId;
        }

        private async Task LogNonSuccessResponseIfAny(HttpContext context, string correlationId)
        {
            var statusCode = context.Response.StatusCode;
            if (statusCode < 400) return;

            var req = context.Request;
            var responseBody = await ReadResponseBody(context);
            var requestBody = await ReadRequestBody(req);

            var message =
                "API error response" + Environment.NewLine +
                $"Correlation: {correlationId}" + Environment.NewLine +
                $"Status     : {statusCode}" + Environment.NewLine +
                $"Method     : {req.Method}" + Environment.NewLine +
                $"Path       : {req.Path}" + Environment.NewLine +
                $"Query      : {req.QueryString}" + Environment.NewLine +
                $"Host       : {req.Host}" + Environment.NewLine +
                $"Scheme     : {req.Scheme}" + Environment.NewLine +
                $"Remote IP  : {context.Connection.RemoteIpAddress}" + Environment.NewLine +
                $"User-Agent : {req.Headers["User-Agent"]}" + Environment.NewLine +
                (string.IsNullOrWhiteSpace(requestBody) ? "" : $"RequestBody: {requestBody}" + Environment.NewLine) +
                (string.IsNullOrWhiteSpace(responseBody) ? "" : $"ResponseBody: {responseBody}");

            if (statusCode >= 500) _log.Error(message);
            else _log.Warn(message);
        }

        private static async Task<string?> ReadRequestBody(HttpRequest request)
        {
            if (request.Body is null || !request.Body.CanSeek) return null;
            if (request.ContentLength is 0) return null;

            var originalPosition = request.Body.Position;
            try
            {
                request.Body.Position = 0;
                using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var content = await reader.ReadToEndAsync();
                return Truncate(content);
            }
            catch
            {
                return null;
            }
            finally
            {
                request.Body.Position = originalPosition;
            }
        }

        private static async Task<string?> ReadResponseBody(HttpContext context)
        {
            if (context.Response.Body is not MemoryStream ms) return null;

            try
            {
                ms.Position = 0;
                using var reader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var content = await reader.ReadToEndAsync();
                return Truncate(content);
            }
            catch
            {
                return null;
            }
            finally
            {
                ms.Position = 0;
            }
        }

        private static string? Truncate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            if (value.Length <= MaxLoggedBodyChars) return value;
            return value.Substring(0, MaxLoggedBodyChars) + "…(truncated)";
        }
    }
}
