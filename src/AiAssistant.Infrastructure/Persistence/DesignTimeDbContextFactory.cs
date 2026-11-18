using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiAssistant.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AssistantDbContext>
{
    public AssistantDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ??
            "Host=localhost;Database=aiassistant;Username=aiassistant";
        var options = new DbContextOptionsBuilder<AssistantDbContext>().UseNpgsql(connection).Options;
        return new AssistantDbContext(options);
    }
}
