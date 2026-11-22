using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordahh.Api.Data;
using Ordahh.Api.Models;

public static class TestHelpers
{
    public const string StaffUsername = "testadmin";
    public const string StaffPassword = "TestPassword123!";

    public static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

    public static async Task<HttpClient> CreateStaffClientAsync(TestFactory factory)
    {
        var client = factory.CreateClient();
        var token = await LoginAsync(client, StaffUsername, StaffPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // each test creates its own menu items, so tests never depend on or change the seed data
    public static async Task<int> AddMenuItemAsync(TestFactory factory, string name, decimal price, bool available = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
        var item = new MenuItem { Name = name, Price = price, IsAvailable = available, CategoryId = 1 };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    public static async Task SetPriceAsync(TestFactory factory, int menuItemId, decimal price)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
        var item = await db.MenuItems.SingleAsync(m => m.Id == menuItemId);
        item.Price = price;
        await db.SaveChangesAsync();
    }

    public static async Task AddUserAsync(TestFactory factory, string username, string password, string role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
        var user = new User { Username = username, Role = role };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public static async Task<int> CountOrdersAsync(TestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
        return await db.Orders.CountAsync();
    }

    public static async Task<OrderStatus> GetStatusAsync(TestFactory factory, int orderId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
        return (await db.Orders.SingleAsync(o => o.Id == orderId)).Status;
    }

    public static Task<HttpResponseMessage> PostOrderAsync(HttpClient client, params (int menuItemId, int quantity)[] lines)
    {
        var body = new { items = lines.Select(l => new { menuItemId = l.menuItemId, quantity = l.quantity }).ToArray() };
        return client.PostAsJsonAsync("/api/orders", body);
    }

    public static async Task<int> CreateOrderAsync(HttpClient client, int menuItemId = 1, int quantity = 1)
    {
        var response = await PostOrderAsync(client, (menuItemId, quantity));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetInt32();
    }

    public static Task<HttpResponseMessage> PatchStatusAsync(HttpClient client, int orderId, object status)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{orderId}/status")
        {
            Content = JsonContent.Create(new { status })
        };
        return client.SendAsync(request);
    }
}