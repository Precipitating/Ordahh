using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
public sealed class TestFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"ordahh-tests-{Guid.NewGuid():N}.db"
    );

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Do not load Development user secrets.
        builder.UseEnvironment("Testing");

        // Supply configuration through host settings.
        builder.UseSetting("Jwt:Key",
            "test-only-secret-key-that-is-at-least-32-characters-long");

        builder.UseSetting("Jwt:Issuer", "Ordahh.Tests");
        builder.UseSetting("Jwt:Audience", "Ordahh.Tests");
        builder.UseSetting("RateLimit:LoginPermitLimit", "1000");
        // Required by the database seeding process.
        builder.UseSetting("Seed:StaffUsername", "testadmin");
        builder.UseSetting("Seed:StaffPassword", "TestPassword123!");

        // Give this factory its own SQLite database.
        builder.UseSetting(
            "ConnectionStrings:Default",
            $"Data Source={_databasePath}"
        );
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        // Release SQLite connections before deleting the file.
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }
}
