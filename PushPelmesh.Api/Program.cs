using Microsoft.EntityFrameworkCore;
using PushPelmesh.Api.Data;
using PushPelmesh.Api.Endpoints;
using PushPelmesh.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using PushPelmesh.Api.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<JwtTokenGenerator>();

var jwtKey = builder.Configuration["Jwt:Key"]!;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,

                ValidateAudience = true,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                ValidateIssuerSigningKey = true,

                ValidIssuer = builder.Configuration["Jwt:Issuer"],

                ValidAudience = builder.Configuration["Jwt:Audience"],

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey))
            };
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("xoxi-room-password", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("credential-login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddScoped<BirthdayCalendarService>();
builder.Services.AddScoped<CalendarEventNotificationService>();

builder.Services.AddHostedService<BirthdayCalendarBackgroundService>();
builder.Services.AddHostedService<CalendarEventNotificationBackgroundService>();

builder.Services.AddScoped<WebPushSenderService>();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    db.Database.Migrate();
}

app.UseAuthentication();

app.UseAuthorization();
app.UseRateLimiter();
app.Use((context, next) =>
{
    // Android installations from the Unity version still request this manifest URL.
    if (context.Request.Path == "/manifest.json")
        context.Request.Path = "/manifest.webmanifest";
    return next(context);
});
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = file =>
    {
        var response = file.Context.Response;
        switch (file.Context.Request.Path.Value)
        {
            case "/index.html":
            case "/manifest.webmanifest":
            case "/service-worker.js":
                response.Headers.CacheControl = "no-cache";
                break;
            case "/Build/Push-Uslugi.loader.js":
                response.Headers.CacheControl = "no-store";
                break;
        }
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPingEndpoints();
app.MapAdminKeyEndpoints();
app.MapCandidateEndpoints();
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapIdentityPhotoEndpoints();
app.MapAdminCleanupEndpoints();
app.MapAdminUserEndpoints();
app.MapAdminRolesEndpoints();
app.MapRolesEndpoints();
app.MapXoxiSaveEndpoints();
app.MapXoxiRoomEndpoints();
app.MapCalendarEndpoints();
app.MapModuleNotificationEndpoints();
app.MapRewardEndpoints();
app.MapCinemaEndpoints();
app.MapMessengerEndpoints();
app.MapVoteEndpoints();
app.MapZongEndpoints();
app.MapDurakEndpoints();
app.MapPushEndpoints();
app.MapAdminPushEndpoints();
// app.GenerateKeys();

app.Run();




