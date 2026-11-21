using AiAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace AiAssistant.IntegrationTests;

public sealed class AppearanceMigrationTests
{
    [Fact]
    public async Task MigrationRollsBackWithoutRemovingExistingPersonas()
    {
        await using var database = new PostgreSqlBuilder("postgres:16").Build();
        await database.StartAsync();
        var connectionString = database.GetConnectionString();
        await using var db = new AssistantDbContext(new DbContextOptionsBuilder<AssistantDbContext>().UseNpgsql(connectionString).Options);
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261118194928_WorkspaceDataAndAppliedRules";
        await migrator.MigrateAsync(previous);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        const string personaName = "SELECT name FROM personas WHERE id = 'f1000000-0000-4000-8000-000000000001'";
        var name = await Scalar(personaName);

        await migrator.MigrateAsync();
        Assert.Equal("sparkles", await Scalar("SELECT icon FROM personas WHERE id = 'f1000000-0000-4000-8000-000000000001'"));
        Assert.Equal(true, await Scalar("SELECT to_regclass('appearance_settings') IS NOT NULL"));
        await migrator.MigrateAsync(previous);
        Assert.Equal(name, await Scalar(personaName));
        Assert.Equal(false, await Scalar("SELECT to_regclass('appearance_settings') IS NOT NULL"));

        async Task<object?> Scalar(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            return await command.ExecuteScalarAsync();
        }
    }
}
