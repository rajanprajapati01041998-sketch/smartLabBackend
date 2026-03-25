using Microsoft.AspNetCore.Http;
using System.Linq;

public class IPAddressService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IPAddressService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetIpAddress()
    {
        var context = _httpContextAccessor.HttpContext;

        if (context == null)
            return "Unknown";

        // ✅ Check proxy header first
        var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (!string.IsNullOrEmpty(ip))
        {
            return ip.Split(',').First();
        }

        // ✅ Fallback to direct IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }
}