using AiAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace AiAssistant.IntegrationTests;

public sealed class AccountInterfaceMigrationTests
{
    [Fact]
    public async Task MigrationDefaultsExistingAccountsAndRollsBackOnlyTheNewPreference()
    {
        await using var database = new PostgreSqlBuilder("postgres:16").Build();
        await database.StartAsync();
        var connectionString = database.GetConnectionString();
        await using var db = new AssistantDbContext(new DbContextOptionsBuilder<AssistantDbContext>().UseNpgsql(connectionString).Options);
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261121203321_SupportMessages";
        const string accountId = "d1000000-0000-4000-8000-000000000001";
        await migrator.MigrateAsync(previous);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await Scalar($"""
            INSERT INTO users (id, username, normalized_username, email, normalized_email, password_hash, role, is_blocked, session_version, created_at)
            VALUES ('{accountId}', 'existing-user', 'EXISTING-USER', 'existing-user@example.invalid', 'EXISTING-USER@EXAMPLE.INVALID',
                    'fixture-hash', 'User', false, 0, now())
            """);

        await migrator.MigrateAsync("AccountInterfaceVariant");
        Assert.Equal("current", await Scalar($"SELECT interface_variant FROM users WHERE id = '{accountId}'"));
        var constraint = await Assert.ThrowsAsync<PostgresException>(async () =>
            await Scalar($"UPDATE users SET interface_variant = 'invalid' WHERE id = '{accountId}'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, constraint.SqlState);
        Assert.Equal("ck_users_interface_variant", constraint.ConstraintName);
        Assert.Equal("focus", await Scalar($"UPDATE users SET interface_variant = 'focus' WHERE id = '{accountId}' RETURNING interface_variant"));

        await migrator.MigrateAsync(previous);
        Assert.Equal(false, await Scalar("""
            SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public'
                AND table_name = 'users' AND column_name = 'interface_variant')
            """));
        Assert.Equal("existing-user", await Scalar($"SELECT username FROM users WHERE id = '{accountId}'"));
        Assert.Equal(true, await Scalar("SELECT to_regclass('support_messages') IS NOT NULL"));
        Assert.Equal(true, await Scalar("SELECT to_regclass('appearance_settings') IS NOT NULL"));

        async Task<object?> Scalar(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            return await command.ExecuteScalarAsync();
        }
    }
}
