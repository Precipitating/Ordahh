using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public class AdminMenuTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;

    public AdminMenuTests(TestFactory factory)
    {
        _factory = factory;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static object Body(string name, decimal price = 5.00m, int categoryId = 1, string? description = "desc")
        => new { name, description, price, categoryId };

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, object body)
        => TestHelpers.SendJsonAsync(client, method, url, body);

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    // ---- authorization ----

    [Fact]
    public async Task AdminMenu_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/menu");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminMenu_WithoutStaffRole_Returns403()
    {
        var username = Unique("guest");
        await TestHelpers.AddUserAsync(_factory, username, "GuestPassword123!", "Customer");
        var client = _factory.CreateClient();
        var token = await TestHelpers.LoginAsync(client, username, "GuestPassword123!");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/menu");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("PATCH", "/api/admin/menu-items/1/archive")]
    [InlineData("PATCH", "/api/admin/menu-items/1/availability")]
    [InlineData("POST", "/api/admin/menu-items")]
    [InlineData("PUT", "/api/admin/menu-items/1")]
    public async Task EveryAdminWriteEndpoint_WithoutToken_Returns401(string method, string url)
    {
        var client = _factory.CreateClient();

        var response = await Send(client, new HttpMethod(method), url, Body("Anything"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- the admin list ----

    [Fact]
    public async Task AdminMenu_IncludesArchivedItems_WithTheFlagSet()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var name = Unique("Archived");
        var id = await TestHelpers.AddMenuItemAsync(_factory, name, 4.00m, archived: true);

        var items = await staff.GetFromJsonAsync<JsonElement>("/api/admin/menu");
        var item = items.EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == id);

        Assert.Equal(name, item.GetProperty("name").GetString());
        Assert.True(item.GetProperty("isArchived").GetBoolean());
    }

    // ---- what customers see ----

    [Fact]
    public async Task CustomerMenu_HidesArchivedItems()
    {
        var client = _factory.CreateClient();
        var name = Unique("Hidden");
        await TestHelpers.AddMenuItemAsync(_factory, name, 4.00m, archived: true);

        var body = await client.GetStringAsync("/api/menu");

        Assert.DoesNotContain(name, body);
    }

    [Fact]
    public async Task CustomerMenu_HidesACategoryWhoseItemsAreAllArchived()
    {
        var client = _factory.CreateClient();
        var categoryName = Unique("EmptyCat");
        var categoryId = await TestHelpers.AddCategoryAsync(_factory, categoryName);
        await TestHelpers.AddMenuItemAsync(_factory, Unique("Gone"), 4.00m, archived: true, categoryId: categoryId);

        var body = await client.GetStringAsync("/api/menu");

        Assert.DoesNotContain(categoryName, body);
    }

    [Fact]
    public async Task CustomerMenu_StillShowsSoldOutItems_AsUnavailable()
    {
        var client = _factory.CreateClient();
        var name = Unique("SoldOut");
        var id = await TestHelpers.AddMenuItemAsync(_factory, name, 4.00m, available: false);

        var menu = await client.GetFromJsonAsync<JsonElement>("/api/menu");
        var item = menu.EnumerateArray()
            .SelectMany(c => c.GetProperty("items").EnumerateArray())
            .Single(i => i.GetProperty("id").GetInt32() == id);

        Assert.False(item.GetProperty("isAvailable").GetBoolean());
    }

    // ---- archive ----

    [Fact]
    public async Task Archive_RemovesFromCustomerMenu_AndRestoreBringsItBack()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var customer = _factory.CreateClient();
        var name = Unique("Toggle");
        var id = await TestHelpers.AddMenuItemAsync(_factory, name, 4.00m);

        var archive = await Send(staff, HttpMethod.Patch, $"/api/admin/menu-items/{id}/archive", new { isArchived = true });
        var whileArchived = await customer.GetStringAsync("/api/menu");

        var restore = await Send(staff, HttpMethod.Patch, $"/api/admin/menu-items/{id}/archive", new { isArchived = false });
        var afterRestore = await customer.GetStringAsync("/api/menu");

        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        Assert.True((await Json(archive)).GetProperty("isArchived").GetBoolean());
        Assert.DoesNotContain(name, whileArchived);

        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.Contains(name, afterRestore);
    }

    [Fact]
    public async Task Archive_UnknownItem_Returns404()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);

        var response = await Send(staff, HttpMethod.Patch, "/api/admin/menu-items/99999/archive", new { isArchived = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Order_ContainingAnArchivedItem_IsRejectedAndNothingIsSaved()
    {
        var customer = _factory.CreateClient();
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Old"), 4.00m, archived: true);
        var before = await TestHelpers.CountOrdersAsync(_factory);

        var response = await TestHelpers.PostOrderAsync(customer, (id, 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await TestHelpers.CountOrdersAsync(_factory));
    }

    [Fact]
    public async Task ArchivingAnItem_DoesNotChangeAnOrderPlacedBeforehand()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var customer = _factory.CreateClient();
        var name = Unique("History");
        var itemId = await TestHelpers.AddMenuItemAsync(_factory, name, 6.00m);
        var orderId = await TestHelpers.CreateOrderAsync(customer, itemId, 2);

        await Send(staff, HttpMethod.Patch, $"/api/admin/menu-items/{itemId}/archive", new { isArchived = true });

        var orders = await staff.GetFromJsonAsync<JsonElement>("/api/orders");
        var order = orders.EnumerateArray().Single(o => o.GetProperty("id").GetInt32() == orderId);
        var line = order.GetProperty("items")[0];

        Assert.Equal(name, line.GetProperty("name").GetString());
        Assert.Equal(2, line.GetProperty("quantity").GetInt32());
    }

    // ---- availability ----

    [Fact]
    public async Task Availability_CanBeToggledBothWays()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Stock"), 4.00m);

        var off = await Send(staff, HttpMethod.Patch, $"/api/admin/menu-items/{id}/availability", new { isAvailable = false });
        var on = await Send(staff, HttpMethod.Patch, $"/api/admin/menu-items/{id}/availability", new { isAvailable = true });

        Assert.False((await Json(off)).GetProperty("isAvailable").GetBoolean());
        Assert.True((await Json(on)).GetProperty("isAvailable").GetBoolean());
    }

    [Fact]
    public async Task Availability_UnknownItem_Returns404()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);

        var response = await Send(staff, HttpMethod.Patch, "/api/admin/menu-items/99999/availability", new { isAvailable = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Order_ContainingASoldOutItem_IsRejected()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var customer = _factory.CreateClient();
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Sellout"), 4.00m);
        await Send(staff, HttpMethod.Patch, $"/api/admin/menu-items/{id}/availability", new { isAvailable = false });

        var response = await TestHelpers.PostOrderAsync(customer, (id, 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- create ----

    [Fact]
    public async Task Create_ValidItem_ReturnsItAndShowsOnTheCustomerMenu()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var customer = _factory.CreateClient();
        var name = Unique("NewItem");

        var response = await Send(staff, HttpMethod.Post, "/api/admin/menu-items", Body("  " + name + "  ", 7.50m, 2));
        var json = await Json(response);
        var menu = await customer.GetStringAsync("/api/menu");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.GetProperty("id").GetInt32() > 0);
        Assert.Equal(name, json.GetProperty("name").GetString()); // trimmed
        Assert.Equal(7.50m, json.GetProperty("price").GetDecimal());
        Assert.Equal("Sides", json.GetProperty("categoryName").GetString());
        Assert.True(json.GetProperty("isAvailable").GetBoolean());
        Assert.False(json.GetProperty("isArchived").GetBoolean());
        Assert.Contains(name, menu);
    }

    [Theory]
    [InlineData("", 5.0, 1)]       // empty name
    [InlineData("   ", 5.0, 1)]    // blank name
    [InlineData("Valid", 0.0, 1)]  // free
    [InlineData("Valid", -1.0, 1)] // negative price
    [InlineData("Valid", 5.0, 0)]  // no category chosen
    public async Task Create_InvalidInput_Returns400(string name, double price, int categoryId)
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);

        var response = await Send(staff, HttpMethod.Post, "/api/admin/menu-items",
            new { name, description = "x", price, categoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownCategory_Returns400()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);

        var response = await Send(staff, HttpMethod.Post, "/api/admin/menu-items", Body(Unique("X"), 5.00m, 9999));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateNameInSameCategory_Returns409_EvenWithDifferentCase()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var name = Unique("Dup");
        await Send(staff, HttpMethod.Post, "/api/admin/menu-items", Body(name));

        var response = await Send(staff, HttpMethod.Post, "/api/admin/menu-items", Body(name.ToUpper()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_SameNameInADifferentCategory_Succeeds()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var name = Unique("Shared");
        await Send(staff, HttpMethod.Post, "/api/admin/menu-items", Body(name, 5.00m, 1));

        var response = await Send(staff, HttpMethod.Post, "/api/admin/menu-items", Body(name, 5.00m, 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_SameNameAsAnArchivedItem_Succeeds()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var name = Unique("Revived");
        await TestHelpers.AddMenuItemAsync(_factory, name, 5.00m, archived: true);

        var response = await Send(staff, HttpMethod.Post, "/api/admin/menu-items", Body(name));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- update ----

    [Fact]
    public async Task Update_ChangesTheFields()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Before"), 5.00m);
        var newName = Unique("After");

        var response = await Send(staff, HttpMethod.Put, $"/api/admin/menu-items/{id}", Body(newName, 9.25m, 3, "new text"));
        var json = await Json(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(newName, json.GetProperty("name").GetString());
        Assert.Equal(9.25m, json.GetProperty("price").GetDecimal());
        Assert.Equal("Drinks", json.GetProperty("categoryName").GetString());
        Assert.Equal("new text", json.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Update_SavingUnchangedValues_DoesNotClashWithItself()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var name = Unique("Same");
        var id = await TestHelpers.AddMenuItemAsync(_factory, name, 5.00m);

        var response = await Send(staff, HttpMethod.Put, $"/api/admin/menu-items/{id}", Body(name, 5.00m, 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_RenamingToAnExistingNameInTheCategory_Returns409()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var taken = Unique("Taken");
        await TestHelpers.AddMenuItemAsync(_factory, taken, 5.00m);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Other"), 5.00m);

        var response = await Send(staff, HttpMethod.Put, $"/api/admin/menu-items/{id}", Body(taken));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownItem_Returns404()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);

        var response = await Send(staff, HttpMethod.Put, "/api/admin/menu-items/99999", Body(Unique("X")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownCategory_Returns400()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Cat"), 5.00m);

        var response = await Send(staff, HttpMethod.Put, $"/api/admin/menu-items/{id}", Body(Unique("X"), 5.00m, 9999));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_InvalidInput_Returns400()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Valid"), 5.00m);

        var response = await Send(staff, HttpMethod.Put, $"/api/admin/menu-items/{id}", Body("", -5.00m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_AnArchivedItem_Works_AndLeavesItArchived()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Old"), 5.00m, archived: true);

        var response = await Send(staff, HttpMethod.Put, $"/api/admin/menu-items/{id}", Body(Unique("Fixed")));
        var json = await Json(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Update_DoesNotChangeTheFlags()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var id = await TestHelpers.AddMenuItemAsync(_factory, Unique("Flags"), 5.00m, available: false);

        var response = await Send(staff, HttpMethod.Put, $"/api/admin/menu-items/{id}", Body(Unique("Flags2")));
        var json = await Json(response);

        Assert.False(json.GetProperty("isAvailable").GetBoolean());
    }

    [Fact]
    public async Task Update_Price_DoesNotChangeAnExistingOrdersStoredPrice()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var customer = _factory.CreateClient();
        var name = Unique("Priced");
        var itemId = await TestHelpers.AddMenuItemAsync(_factory, name, 5.00m);
        var orderId = await TestHelpers.CreateOrderAsync(customer, itemId, 1);

        await Send(staff, HttpMethod.Put, $"/api/admin/menu-items/{itemId}", Body(name, 12.00m));

        var orders = await staff.GetFromJsonAsync<JsonElement>("/api/orders");
        var order = orders.EnumerateArray().Single(o => o.GetProperty("id").GetInt32() == orderId);

        Assert.Equal(5.00m, order.GetProperty("items")[0].GetProperty("unitPrice").GetDecimal());
    }

    [Fact]
    public async Task Update_Price_AppliesToNewOrders()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        var customer = _factory.CreateClient();
        var name = Unique("Repriced");
        var itemId = await TestHelpers.AddMenuItemAsync(_factory, name, 5.00m);
        await Send(staff, HttpMethod.Put, $"/api/admin/menu-items/{itemId}", Body(name, 12.00m));

        var response = await TestHelpers.PostOrderAsync(customer, (itemId, 2));
        var json = await Json(response);

        Assert.Equal(24.00m, json.GetProperty("total").GetDecimal());
    }
}