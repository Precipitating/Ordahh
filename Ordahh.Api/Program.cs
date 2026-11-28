using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Ordahh.Api.Data;
using Ordahh.Api.Hubs;
using Ordahh.Api.Models;
using Ordahh.Api.Services;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=menu.db";
var loginLimit = builder.Configuration.GetValue<int>("RateLimit:LoginPermitLimit", 5);
// ---- services ----
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 5 login attempts per minute, counted separately for each client IP
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});



// JWT
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be set and at least 32 characters.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // browsers can't send an Authorization header on a WebSocket,
        // so SignalR sends the token as ?access_token=... on hub URLs
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs/orders"))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSingleton<MenuNotifier>();

builder.Services.AddAuthorization();

var app = builder.Build();

// ---- startup tasks ----
// Seed a default staff user if no users exist
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
    await db.Database.MigrateAsync();

    if (!await db.Users.AnyAsync())
    {
        var username = builder.Configuration["Seed:StaffUsername"];
        var password = builder.Configuration["Seed:StaffPassword"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "No users exist and Seed:StaffUsername / Seed:StaffPassword are not set.");

        var user = new User { Username = username, Role = "Staff" };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}

// ---- pipeline (order matters) ----
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OrdersHub>("/hubs/orders");
app.MapHub<TrackingHub>("/hubs/tracking");
app.MapHub<MenuHub>("/hubs/menu");

app.Run();

public partial class  Program 
{
    
}