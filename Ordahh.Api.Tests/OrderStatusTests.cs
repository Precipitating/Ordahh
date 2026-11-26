using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ordahh.Api.Models;

public class OrderStatusTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;

    public OrderStatusTests(TestFactory factory)
    {
        _factory = factory;
    }

    // creates an order as a customer, returns a staff client and the order id
    private async Task<(HttpClient staff, int orderId)> NewOrderAsync()
    {
        var customer = _factory.CreateClient();
        var orderId = await TestHelpers.CreateOrderAsync(customer);
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);
        return (staff, orderId);
    }

    [Fact]
    public async Task ReceivedToPreparing_IsAllowed()
    {
        var (staff, id) = await NewOrderAsync();

        var response = await TestHelpers.PatchStatusAsync(staff, id, "Preparing");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Preparing", json.GetProperty("status").GetString());
        Assert.Equal(OrderStatus.Preparing, await TestHelpers.GetStatusAsync(_factory, id));
    }

    [Fact]
    public async Task FullFlow_ReceivedPreparingCompleted()
    {
        var (staff, id) = await NewOrderAsync();

        var first = await TestHelpers.PatchStatusAsync(staff, id, "Preparing");
        var second = await TestHelpers.PatchStatusAsync(staff, id, "Completed");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(OrderStatus.Completed, await TestHelpers.GetStatusAsync(_factory, id));
    }

    [Fact]
    public async Task ReceivedToCancelled_IsAllowed()
    {
        var (staff, id) = await NewOrderAsync();

        var response = await TestHelpers.PatchStatusAsync(staff, id, "Cancelled");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReceivedToCompleted_SkippingAStep_IsRefused()
    {
        var (staff, id) = await NewOrderAsync();

        var response = await TestHelpers.PatchStatusAsync(staff, id, "Completed");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(OrderStatus.Received, await TestHelpers.GetStatusAsync(_factory, id));
    }

    [Fact]
    public async Task PreparingToCancelled_IsRefused()
    {
        var (staff, id) = await NewOrderAsync();
        await TestHelpers.PatchStatusAsync(staff, id, "Preparing");

        var response = await TestHelpers.PatchStatusAsync(staff, id, "Cancelled");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(OrderStatus.Preparing, await TestHelpers.GetStatusAsync(_factory, id));
    }

    [Theory]
    [InlineData("Preparing")]
    [InlineData("Cancelled")]
    [InlineData("Completed")]
    public async Task CompletedOrder_CannotMoveAnywhere(string target)
    {
        var (staff, id) = await NewOrderAsync();
        await TestHelpers.PatchStatusAsync(staff, id, "Preparing");
        await TestHelpers.PatchStatusAsync(staff, id, "Completed");

        var response = await TestHelpers.PatchStatusAsync(staff, id, target);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(OrderStatus.Completed, await TestHelpers.GetStatusAsync(_factory, id));
    }

    [Theory]
    [InlineData("Received")]
    [InlineData("Preparing")]
    [InlineData("Completed")]
    public async Task CancelledOrder_CannotMoveAnywhere(string target)
    {
        var (staff, id) = await NewOrderAsync();
        await TestHelpers.PatchStatusAsync(staff, id, "Cancelled");

        var response = await TestHelpers.PatchStatusAsync(staff, id, target);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(OrderStatus.Cancelled, await TestHelpers.GetStatusAsync(_factory, id));
    }

    [Fact]
    public async Task UnknownOrder_ReturnsNotFound()
    {
        var staff = await TestHelpers.CreateStaffClientAsync(_factory);

        var response = await TestHelpers.PatchStatusAsync(staff, 99999, "Preparing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NumberOutsideTheEnum_IsRejected()
    {
        var (staff, id) = await NewOrderAsync();

        var response = await TestHelpers.PatchStatusAsync(staff, id, 99);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(OrderStatus.Received, await TestHelpers.GetStatusAsync(_factory, id));
    }

    [Fact]
    public async Task UnknownStatusWord_IsRejected()
    {
        var (staff, id) = await NewOrderAsync();

        var response = await TestHelpers.PatchStatusAsync(staff, id, "Banana");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}