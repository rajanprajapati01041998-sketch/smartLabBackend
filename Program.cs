using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.OpenApi.Models;
using App.Data;
using App.Settings;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;



public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        // SERVICES
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IPAddressService>();

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

        // CORS (needed for browser-based frontends; Postman is not subject to CORS).
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("DevCors", policy =>
            {
                var allowedOrigins = builder.Configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>();

                if (allowedOrigins is { Length: > 0 })
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
                else
                {
                    // Development-friendly default: allow any origin (no cookies/credentials).
                    policy.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            });
        });

        // Razorpay config

        builder.Services.Configure<RazorpaySettings>(
            builder.Configuration.GetSection("RazorpaySettings"));

        builder.Services.AddEndpointsApiExplorer();
        // SWAGGER WITH JWT
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "LISD API", Version = "v1" });

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

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
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
        // builder.Services.AddHttpContextAccessor();
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

        // Avoid stale Swagger responses when testing on LAN / behind proxies.
        app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/swagger"), swaggerApp =>
        {
            swaggerApp.Use(async (ctx, next) =>
            {
                ctx.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
                ctx.Response.Headers.Pragma = "no-cache";
                ctx.Response.Headers.Expires = "0";
                await next();
            });
        });

        // Print reachable URLs on startup (useful when calling from another device on the same Wi‑Fi).
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                var urls = app.Urls.ToArray();
                Console.WriteLine("Listening on:");
                foreach (var url in urls) Console.WriteLine($"  {url}");

                var parsedUrls = urls.Select(u => new Uri(u)).ToArray();

                Console.WriteLine("Swagger:");
                foreach (var uri in parsedUrls)
                    Console.WriteLine($"  {uri.Scheme}://{uri.Host}:{uri.Port}/swagger");

                var lanIps = GetLanIPv4Addresses().ToArray();
                if (lanIps.Length > 0)
                {
                    foreach (var uri in parsedUrls)
                    {
                        if (uri.Host is not ("0.0.0.0" or "::")) continue;

                        Console.WriteLine($"LAN access ({uri.Scheme.ToUpperInvariant()}):");
                        foreach (var ip in lanIps)
                            Console.WriteLine($"  {uri.Scheme}://{ip}:{uri.Port}/swagger");
                    }
                }
            }
            catch
            {
                // ignore logging failures
            }
        });

        // Non-blocking DB connectivity check (avoid delaying app startup / Swagger).
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = app.Services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    var canConnect = await db.Database.CanConnectAsync(cts.Token);
                    Console.WriteLine(canConnect
                        ? "✅ Database connected successfully!"
                        : "❌ Database connection failed!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Database connection error!");
                    Console.WriteLine(ex.Message);
                }
            });
        });



        // =============================
        // SWAGGER
        // =============================
        if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "LISD API v1");
            });
        }

        // Serve `wwwroot/index.html` at `/`
        app.UseDefaultFiles();
        app.UseStaticFiles();



        // =============================
        // HTTPS (only outside development)
        // =============================
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        // Must be before auth/endpoints so preflight OPTIONS requests succeed.
        app.UseCors("DevCors");



        // =============================
        // AUTH MIDDLEWARE ORDER 🔥
        // =============================
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

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
