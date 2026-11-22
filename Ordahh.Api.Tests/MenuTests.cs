using System.Net;

public class MenuTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;

    public MenuTests(TestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMenu_ReturnsSeededCategories()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/menu");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Burgers", body);
    }
}