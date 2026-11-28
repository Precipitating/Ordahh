using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public class AuthTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;

    public AuthTests(TestFactory factory)
    {
        _factory = factory;
    }

    // ---- login ----

    [Fact]
    public async Task Login_GoodCredentials_ReturnsTokenAndExpiry()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = TestHelpers.StaffUsername, password = TestHelpers.StaffPassword });
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("token").GetString()));
        Assert.True(json.GetProperty("expiryTime").GetDateTime() > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = TestHelpers.StaffUsername, password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_GivesTheSameAnswerAsWrongPassword()
    {
        var client = _factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login",
            new { username = TestHelpers.StaffUsername, password = "wrong" });
        var unknownUser = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "nobody", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal(
            await wrongPassword.Content.ReadAsStringAsync(),
            await unknownUser.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_MissingPassword_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = TestHelpers.StaffUsername });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- protected endpoints ----

    [Fact]
    public async Task GetOrders_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_WithGarbageToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "abc");

        var response = await client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_WithStaffToken_Returns200()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);

        var response = await staff.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await TestHelpers.PatchStatusAsync(client, 1, "Preparing");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_LoggedInWithoutStaffRole_Returns403()
    {
        await TestHelpers.AddUserAsync(_factory, "guest", "GuestPassword123!", "Customer");
        var client = _factory.CreateClient();
        var token = await TestHelpers.LoginAsync(client, "guest", "GuestPassword123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlacingAnOrder_DoesNotRequireLogin()
    {
        var client = _factory.CreateClient();

        var response = await TestHelpers.PostOrderAsync(client, (1, 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- the SignalR hub ----

    [Fact]
    public async Task Hub_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/hubs/orders/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hub_WithStaffToken_AllowsTheHandshake()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);

        var response = await staff.PostAsync("/hubs/orders/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MenuHub_AllowsTheHandshakeWithoutLogin()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/hubs/menu/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}