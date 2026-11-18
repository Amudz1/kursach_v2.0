using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace AiAssistant.IntegrationTests;

public sealed class ApiFixture : IAsyncLifetime
{
    internal const string Password = "Fixture-local-budz-2026";
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16").Build();
    private readonly string _files = Path.Combine(Path.GetTempPath(), "budz-fixture-" + Guid.NewGuid().ToString("N"));
    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Postgres", _database.GetConnectionString());
            builder.UseSetting("Files:Directory", _files);
            builder.UseSetting("Database:MigrateOnStart", "true");
            builder.UseSetting("Database:SeedDemo", "true");
            builder.UseSetting("Database:DemoPassword", Password);
            builder.UseSetting("Ai:Mode", "Demo");
        });
    }

    internal HttpClient Client(bool cookies = true) => _factory!.CreateClient(new()
    {
        AllowAutoRedirect = false, HandleCookies = cookies
    });

    internal async Task ExpireAsync(Guid chat)
    {
        await using var connection = new NpgsqlConnection(_database.GetConnectionString());
        await connection.OpenAsync();
        await using var update = new NpgsqlCommand("UPDATE chats SET expires_at = now() - interval '1 minute' WHERE id = $1", connection);
        update.Parameters.AddWithValue(chat);
        await update.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        await _database.DisposeAsync();
        if (!Directory.Exists(_files)) return;
        foreach (var entry in Directory.EnumerateFiles(_files)) File.Delete(entry);
        Directory.Delete(_files);
    }
}
