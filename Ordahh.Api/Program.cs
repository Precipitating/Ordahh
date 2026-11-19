using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ordahh.Api.Data;
using Ordahh.Api.Hubs;
using Ordahh.Api.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDBContext>(options =>
    options.UseSqlite("Data Source=menu.db"));

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

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});


var app = builder.Build();
app.UseCors("Frontend");

// Seed the database with a default staff user if no users exist
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();

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


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<OrdersHub>("/hubs/orders");

app.Run();
