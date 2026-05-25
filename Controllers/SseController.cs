using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LISD.Services;
using Microsoft.AspNetCore.Mvc;

namespace LISD.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SseController : ControllerBase
    {
        private readonly SseNotificationService _sseService;

        public SseController(SseNotificationService sseService)
        {
            _sseService = sseService;
        }

        [HttpGet("admin-listen")]
        public async Task AdminListen(CancellationToken cancellationToken)
        {
            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";
            Response.Headers["X-Accel-Buffering"] = "no";

            var clientId = Guid.NewGuid().ToString("N");

            await using var writer = new StreamWriter(Response.Body, leaveOpen: true);
            _sseService.AddClient(clientId, writer);

            try
            {
                await writer.WriteAsync("data: {\"message\":\"Admin connected successfully\"}\n\n");
                await writer.FlushAsync();

                while (!cancellationToken.IsCancellationRequested)
                {
                    await writer.WriteAsync(": keep-alive\n\n");
                    await writer.FlushAsync();

                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // client disconnected
            }
            finally
            {
                _sseService.RemoveClient(clientId);
            }
        }
    }
}

