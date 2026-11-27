using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

public class StockTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;

    public StockTests(TestFactory factory)
    {
        _factory = factory;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task SetStock_StoresTheCount_AndNullStopsTracking()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Count"), 4.00m);

        var set = await TestHelpers.SendJsonAsync(staff, HttpMethod.Patch, $"/api/admin/menu-items/{id}/stock", new { stock = 7 });
        var cleared = await TestHelpers.SendJsonAsync(staff, HttpMethod.Patch, $"/api/admin/menu-items/{id}/stock", new { stock = (int?)null });

        Assert.Equal(7, (await set.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("stock").GetInt32());
        Assert.Equal(JsonValueKind.Null, (await cleared.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("stock").ValueKind);
    }

    [Fact]
    public async Task SetStock_Negative_Returns400()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Neg"), 4.00m);

        var response = await TestHelpers.SendJsonAsync(staff, HttpMethod.Patch, $"/api/admin/menu-items/{id}/stock", new { stock = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetStock_UnknownItem_Returns404()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);

        var response = await TestHelpers.SendJsonAsync(staff, HttpMethod.Patch, "/api/admin/menu-items/99999/stock", new { stock = 5 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetStock_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await TestHelpers.SendJsonAsync(client, HttpMethod.Patch, "/api/admin/menu-items/1/stock", new { stock = 5 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Order_ForMoreThanTheStock_IsRejectedAndNothingIsSaved()
    {
        var customer = _factory.CreateClient();
        var name = Unique("Limited");
        var id = await TestHelpers.AddMenuItemAsync(_factory, name, 4.00m, stock: 2);
        var before = await TestHelpers.CountOrdersAsync(_factory);

        var response = await TestHelpers.PostOrderAsync(customer, (id, 3));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(name, body);
        Assert.Contains("2", body);
        Assert.Equal(before, await TestHelpers.CountOrdersAsync(_factory));
    }

    [Fact]
    public async Task Order_ForAnItemWithZeroStock_IsRejected()
    {
        var customer = _factory.CreateClient();
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Empty"), 4.00m, stock: 0);

        var response = await TestHelpers.PostOrderAsync(customer, (id, 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Order_WithinTheStock_IsAccepted()
    {
        var customer = _factory.CreateClient();
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Plenty"), 4.00m, stock: 5);

        var response = await TestHelpers.PostOrderAsync(customer, (id, 5));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Order_ForAnUntrackedItem_IsNeverBlockedByStock()
    {
        var customer = _factory.CreateClient();
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Untracked"), 4.00m, stock: null);

        var response = await TestHelpers.PostOrderAsync(customer, (id, 10));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CustomerMenu_ShowsAZeroStockItemAsUnavailable()
    {
        var customer = _factory.CreateClient();
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Gone"), 4.00m, stock: 0);

        var menu = await customer.GetFromJsonAsync<JsonElement>("/api/menu");
        var item = menu.EnumerateArray()
            .SelectMany(c => c.GetProperty("items").EnumerateArray())
            .Single(i => i.GetProperty("id").GetInt32() == id);

        Assert.False(item.GetProperty("isAvailable").GetBoolean());
    }

    [Fact]
    public async Task Create_WithStock_ReturnsIt()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);

        var response = await TestHelpers.SendJsonAsync(staff, HttpMethod.Post, "/api/admin/menu-items",
            new { name = Unique("Stocked"), description = "x", price = 5.00m, categoryId = 1, stock = 12 });

        Assert.Equal(12, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("stock").GetInt32());
    }

    [Fact]
    public async Task SuccessfulOrder_ReducesStockByTheQuantity()
    {
        var customer = _factory.CreateClient();
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Drop"), 4.00m, stock: 5);

        var response = await TestHelpers.PostOrderAsync(customer, (id, 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, await TestHelpers.GetStockAsync(_factory, id));
    }

    [Fact]
    public async Task OrderWithOneShortLine_SavesNothingAndKeepsAllStock()
    {
        var customer = _factory.CreateClient();
        var plenty = await TestHelpers.AddMenuItemAsync(_factory, Unique("Plenty"), 4.00m, stock: 10);
        var scarce = await TestHelpers.AddMenuItemAsync(_factory, Unique("Scarce"), 4.00m, stock: 1);
        var before = await TestHelpers.CountOrdersAsync(_factory);

        var response = await TestHelpers.PostOrderAsync(customer, (plenty, 2), (scarce, 2));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await TestHelpers.CountOrdersAsync(_factory));
        Assert.Equal(10, await TestHelpers.GetStockAsync(_factory, plenty));
        Assert.Equal(1, await TestHelpers.GetStockAsync(_factory, scarce));
    }

    [Fact]
    public async Task UntrackedItem_StaysUntrackedAfterAnOrder()
    {
        var customer = _factory.CreateClient();
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Free"), 4.00m, stock: null);

        await TestHelpers.PostOrderAsync(customer, (id, 3));

        Assert.Null(await TestHelpers.GetStockAsync(_factory, id));
    }

    [Fact]
    public async Task ParallelOrders_NeverOversell()
    {
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Last"), 4.00m, stock: 5);

        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            var client = _factory.CreateClient();
            return await TestHelpers.PostOrderAsync(client, (id, 1));
        });
        var responses = await Task.WhenAll(tasks);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(5, succeeded);
        Assert.Equal(0, await TestHelpers.GetStockAsync(_factory, id));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.True(r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task CancellingAnOrder_ReturnsItsQuantitiesToStock()
    {
        var customer = _factory.CreateClient();
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Return"), 4.00m, stock: 5);
        var orderId = await TestHelpers.CreateOrderAsync(customer, id, 3);
        Assert.Equal(2, await TestHelpers.GetStockAsync(_factory, id));

        var response = await TestHelpers.PatchStatusAsync(staff, orderId, "Cancelled");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, await TestHelpers.GetStockAsync(_factory, id));
    }

    [Fact]
    public async Task CancellingTwice_OnlyReturnsStockOnce()
    {
        var customer = _factory.CreateClient();
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Once"), 4.00m, stock: 5);
        var orderId = await TestHelpers.CreateOrderAsync(customer, id, 3);

        var first = await TestHelpers.PatchStatusAsync(staff, orderId, "Cancelled");
        var second = await TestHelpers.PatchStatusAsync(staff, orderId, "Cancelled");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(5, await TestHelpers.GetStockAsync(_factory, id));
    }

    [Fact]
    public async Task ParallelCancels_ReturnStockExactlyOnce()
    {
        var customer = _factory.CreateClient();
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Race"), 4.00m, stock: 5);
        var orderId = await TestHelpers.CreateOrderAsync(customer, id, 3);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => TestHelpers.PatchStatusAsync(staff, orderId, "Cancelled")));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, await TestHelpers.GetStockAsync(_factory, id));
    }

    [Fact]
    public async Task CancellingAnUntrackedItemsOrder_DoesNotGiveItAStockCount()
    {
        var customer = _factory.CreateClient();
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Free"), 4.00m, stock: null);
        var orderId = await TestHelpers.CreateOrderAsync(customer, id, 3);

        await TestHelpers.PatchStatusAsync(staff, orderId, "Cancelled");

        Assert.Null(await TestHelpers.GetStockAsync(_factory, id));
    }

    [Fact]
    public async Task MovingToPreparing_DoesNotTouchStock()
    {
        var customer = _factory.CreateClient();
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Keep"), 4.00m, stock: 5);
        var orderId = await TestHelpers.CreateOrderAsync(customer, id, 3);

        await TestHelpers.PatchStatusAsync(staff, orderId, "Preparing");

        Assert.Equal(2, await TestHelpers.GetStockAsync(_factory, id));
    }

    [Fact]
    public async Task CustomerMenu_CapsMaxPerOrderAtStock_AndFlagsLowStock()
    {
        var customer = _factory.CreateClient();
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Few"), 4.00m, stock: 3);

        var menu = await customer.GetFromJsonAsync<JsonElement>("/api/menu");
        var item = menu.EnumerateArray()
            .SelectMany(c => c.GetProperty("items").EnumerateArray())
            .Single(i => i.GetProperty("id").GetInt32() == id);

        Assert.Equal(3, item.GetProperty("maxPerOrder").GetInt32());
        Assert.True(item.GetProperty("lowStock").GetBoolean());
    }
}