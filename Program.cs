using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.OpenApi.Models;
using App.Data;
using App.Settings;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using log4net.Config;
using log4net;
using System.Reflection;
using LISD.Extensions;
using LISD.Filters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;



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

        builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ApiExceptionFilter>();
                options.Filters.Add<ModelStateValidationFilter>();
            })
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


        // ✅ create date-wise folder path
        var todayFolder = Path.Combine(AppContext.BaseDirectory, "Logs", DateTime.Now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(todayFolder);

        // ✅ pass folder path to log4net
        GlobalContext.Properties["LogPath"] = Path.Combine(todayFolder, "application.log");

        // ✅ load log4net config
        var repository = LogManager.GetRepository(Assembly.GetEntryAssembly()!);
        XmlConfigurator.Configure(repository, new FileInfo(Path.Combine(AppContext.BaseDirectory, "log4net.config")));



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
        // JWT AUTHENTICATION / AUTHORIZATION
        // =============================
        builder.Services.AddAuthorization();

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        // Prevent the default 401 response (HTML / plain text) so we can return JSON
                        context.HandleResponse();

                        if (context.Response.HasStarted) return;

                        var correlationId = context.HttpContext.Items.TryGetValue("CorrelationId", out var v) && v is string s && !string.IsNullOrWhiteSpace(s)
                            ? s
                            : context.HttpContext.TraceIdentifier;

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";

                        var json = JsonSerializer.Serialize(new
                        {
                            status = false,
                            message = "Unauthorized",
                            correlationId,
                            data = (object?)null
                        });

                        await context.Response.WriteAsync(json);
                    },
                    OnForbidden = async context =>
                    {
                        if (context.Response.HasStarted) return;

                        var correlationId = context.HttpContext.Items.TryGetValue("CorrelationId", out var v) && v is string s && !string.IsNullOrWhiteSpace(s)
                            ? s
                            : context.HttpContext.TraceIdentifier;

                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";

                        var json = JsonSerializer.Serialize(new
                        {
                            status = false,
                            message = "Forbidden",
                            correlationId,
                            data = (object?)null
                        });

                        await context.Response.WriteAsync(json);
                    }
                };

                var jwt = builder.Configuration.GetSection("Jwt");
                var issuer = jwt["Issuer"];
                var audience = jwt["Audience"];
                var key = jwt["Key"];

                if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(key))
                {
                    throw new InvalidOperationException("Jwt configuration is missing. Please set Jwt:Issuer, Jwt:Audience, Jwt:Key in appsettings.");
                }

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    // Make token expiry behave exactly as configured in Jwt:DurationInMinutes
                    ClockSkew = TimeSpan.Zero
                };
            });



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
                    options.RoutePrefix = "swagger";
                    options.SwaggerEndpoint("v1/swagger.json", "LISD API v1");
                });
        }

        // Serve `wwwroot/index.html` at `/`
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseErrorLogging();



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
