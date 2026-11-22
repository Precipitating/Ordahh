using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

public class OrderCreationTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;

    public OrderCreationTests(TestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ValidOrder_TotalComesFromDatabasePrices()
    {
        var client = _factory.CreateClient();

        var response = await TestHelpers.PostOrderAsync(client, (1, 2), (2, 1));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(27.97m, json.GetProperty("total").GetDecimal()); // 2 x 8.99 + 9.99
        Assert.Equal("Received", json.GetProperty("status").GetString());
        Assert.Equal(2, json.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task DuplicateLines_AreMergedIntoOne()
    {
        var client = _factory.CreateClient();

        var response = await TestHelpers.PostOrderAsync(client, (1, 2), (1, 3));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(5, items[0].GetProperty("quantity").GetInt32());
        Assert.Equal(44.95m, json.GetProperty("total").GetDecimal()); // 5 x 8.99
    }

    [Fact]
    public async Task EmptyItemList_IsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new { items = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task QuantityOutsideOneToTen_IsRejected(int quantity)
    {
        var client = _factory.CreateClient();

        var response = await TestHelpers.PostOrderAsync(client, (1, quantity));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MergedQuantityOverTen_IsRejectedAndNothingIsSaved()
    {
        var client = _factory.CreateClient();
        var before = await TestHelpers.CountOrdersAsync(_factory);

        var response = await TestHelpers.PostOrderAsync(client, (1, 6), (1, 6)); // merges to 12

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await TestHelpers.CountOrdersAsync(_factory));
    }

    [Fact]
    public async Task UnknownMenuItem_IsRejectedAndNothingIsSaved()
    {
        var client = _factory.CreateClient();
        var before = await TestHelpers.CountOrdersAsync(_factory);

        var response = await TestHelpers.PostOrderAsync(client, (999, 1));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("999", body);
        Assert.Equal(before, await TestHelpers.CountOrdersAsync(_factory));
    }

    [Fact]
    public async Task UnavailableItem_IsRejectedAndNothingIsSaved()
    {
        var client = _factory.CreateClient();
        var soldOutId = await TestHelpers.AddMenuItemAsync(_factory, "Sold Out Special", 5.00m, available: false);
        var before = await TestHelpers.CountOrdersAsync(_factory);

        var response = await TestHelpers.PostOrderAsync(client, (soldOutId, 1));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Sold Out Special", body);
        Assert.Equal(before, await TestHelpers.CountOrdersAsync(_factory));
    }

    [Fact]
    public async Task StoredPrice_DoesNotChangeWhenMenuPriceChanges()
    {
        var customer = _factory.CreateClient();
        var itemId = await TestHelpers.AddMenuItemAsync(_factory, "Price Test Item", 5.00m);
        var orderId = await TestHelpers.CreateOrderAsync(customer, itemId, 1);

        await TestHelpers.SetPriceAsync(_factory, itemId, 7.00m);

        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var orders = await staff.GetFromJsonAsync<JsonElement>("/api/orders");
        var order = orders.EnumerateArray().Single(o => o.GetProperty("id").GetInt32() == orderId);
        var unitPrice = order.GetProperty("items")[0].GetProperty("unitPrice").GetDecimal();

        Assert.Equal(5.00m, unitPrice);
    }
}