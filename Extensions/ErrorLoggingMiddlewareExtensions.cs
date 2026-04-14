using LISD.Middleware;

namespace LISD.Extensions
{
    public static class ErrorLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseErrorLogging(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ErrorLoggingMiddleware>();
        }
    }
}