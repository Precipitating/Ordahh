using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

public class TrackingTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;

    public TrackingTests(TestFactory factory)
    {
        _factory = factory;
    }

    private static async Task<string> PlaceOrderAndGetCodeAsync(HttpClient client)
    {
        var response = await TestHelpers.PostOrderAsync(client, (1, 1));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("trackingCode").GetGuid().ToString();
    }

    [Fact]
    public async Task PlacingAnOrder_ReturnsATrackingCode()
    {
        var client = _factory.CreateClient();

        var response = await TestHelpers.PostOrderAsync(client, (1, 1));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.NotEqual(Guid.Empty, json.GetProperty("trackingCode").GetGuid());
    }

    [Fact]
    public async Task Track_ValidCode_WorksWithoutLogin()
    {
        var client = _factory.CreateClient();
        var code = await PlaceOrderAndGetCodeAsync(client);

        var response = await client.GetAsync($"/api/orders/track/{code}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Received", json.GetProperty("status").GetString());
        Assert.Equal(1, json.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Track_Response_DoesNotExposeTheDatabaseId()
    {
        var client = _factory.CreateClient();
        var code = await PlaceOrderAndGetCodeAsync(client);

        var response = await client.GetAsync($"/api/orders/track/{code}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(json.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task Track_UnknownCode_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/orders/track/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Track_NotAGuid_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/orders/track/abc");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Track_ReflectsStatusChangesMadeByStaff()
    {
        var customer = _factory.CreateClient();
        var code = await PlaceOrderAndGetCodeAsync(customer);

        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var orders = await staff.GetFromJsonAsync<JsonElement>("/api/orders");
        var orderId = orders.EnumerateArray()
            .Single(o => o.GetProperty("trackingCode").GetGuid().ToString() == code)
            .GetProperty("id").GetInt32();
        await TestHelpers.PatchStatusAsync(staff, orderId, "Preparing");

        var response = await customer.GetAsync($"/api/orders/track/{code}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Preparing", json.GetProperty("status").GetString());
    }
}