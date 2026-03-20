using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.OpenApi;
using App.Data;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;



public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        // Allow binding to a LAN IP / all interfaces for testing on the same Wi‑Fi.
        // Prefer environment/launchSettings ("ASPNETCORE_URLS") when set, otherwise fall back to appsettings.
        var configuredUrls = builder.Configuration["Host:Urls"];
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) &&
            !string.IsNullOrWhiteSpace(configuredUrls))
        {
            builder.WebHost.UseUrls(configuredUrls);
        }

        builder.Services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition =
                    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;

                options.JsonSerializerOptions.PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase;
            });

        builder.Services.AddEndpointsApiExplorer();
        // SWAGGER WITH JWT
        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer",
                new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter JWT Token"
                });

            options.AddSecurityRequirement(
                document => new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecuritySchemeReference("Bearer", document, null),
                        new List<string>()
                    }
                });
        });



        // =============================
        // DATABASE
        // =============================
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection")));
        builder.Services.AddHttpContextAccessor();



        // =============================
        // JWT SETTINGS
        // =============================
        // var jwtSettings =
        //     builder.Configuration.GetSection("Jwt")
        //         .Get<JwtSettings>()
        //     ?? throw new InvalidOperationException("JwtSettings configuration is missing.");

        // Console.WriteLine("ISSUER: " + jwtSettings.Issuer);
        // Console.WriteLine("AUDIENCE: " + jwtSettings.Audience);
        // Console.WriteLine("KEY: " + jwtSettings.Key);



        // =============================
        // DEPENDENCY INJECTION
        // =============================
        builder.Services.AddHttpContextAccessor();
        // builder.Services.AddScoped<IAuthService, AuthService>();







        // =============================
        // JWT AUTHENTICATION 🔥🔥🔥
        // =============================
        //     builder.Services
        // .AddAuthentication(options =>
        // {
        //     options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        //     options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        // })
        // .AddJwtBearer(options =>
        // {
        //     options.MapInboundClaims = false;

        //     options.Events = new JwtBearerEvents
        //     {
        //         OnAuthenticationFailed = context =>
        //         {
        //             Console.WriteLine("❌ AUTH FAILED:");
        //             Console.WriteLine(context.Exception.ToString());
        //             return Task.CompletedTask;
        //         },
        //         OnTokenValidated = context =>
        //         {
        //             Console.WriteLine("✅ TOKEN VALIDATED");
        //             return Task.CompletedTask;
        //         }
        //     };

        //     options.TokenValidationParameters = new TokenValidationParameters
        //     {
        //         ValidateIssuer = true,
        //         ValidateAudience = true,
        //         ValidateLifetime = true,
        //         ValidateIssuerSigningKey = true,

        //         ValidIssuer = jwtSettings.Issuer,
        //         ValidAudience = jwtSettings.Audience,
        //         IssuerSigningKey =
        //             new SymmetricSecurityKey(
        //                 Encoding.UTF8.GetBytes(jwtSettings.Key)),

        //         ClockSkew = TimeSpan.FromMinutes(2)
        //     };
        // });



        // =============================
        // BUILD APP
        // =============================
        var app = builder.Build();

        // Print reachable URLs on startup (useful when calling from another device on the same Wi‑Fi).
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                var urls = app.Urls.ToArray();
                Console.WriteLine("Listening on:");
                foreach (var url in urls) Console.WriteLine($"  {url}");

                var ports = urls
                    .Select(u => new Uri(u).Port)
                    .Distinct()
                    .OrderBy(p => p)
                    .ToArray();

                var lanIps = GetLanIPv4Addresses().ToArray();
                if (lanIps.Length > 0 && ports.Length > 0)
                {
                    Console.WriteLine("LAN access:");
                    foreach (var ip in lanIps)
                    foreach (var port in ports)
                        Console.WriteLine($"  http://{ip}:{port}/swagger");
                }
            }
            catch
            {
                // ignore logging failures
            }
        });



        // =============================
        // SWAGGER
        // =============================
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }



        // =============================
        // HTTPS (only outside development)
        // =============================
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }



        // =============================
        // AUTH MIDDLEWARE ORDER 🔥
        // =============================
        app.UseAuthentication();
        app.UseAuthorization();

        // Quick connectivity check: http://<PC-IP>:<PORT>/ping
        app.MapGet("/ping", () => Results.Ok(new { status = "ok", atUtc = DateTimeOffset.UtcNow }));

        app.MapControllers();



        // =============================
        // DATABASE CONNECTION CHECK
        // =============================
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                if (db.Database.CanConnect())
                    Console.WriteLine("✅ Database connected successfully!");
                else
                    Console.WriteLine("❌ Database connection failed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Database connection error!");
                Console.WriteLine(ex.Message);
            }
        }

        app.Run();
    }

    private static IEnumerable<IPAddress> GetLanIPv4Addresses()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up) continue;
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(unicast.Address)) continue;
                yield return unicast.Address;
            }
        }
    }
}
