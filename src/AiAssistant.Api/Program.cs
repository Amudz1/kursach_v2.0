using AiAssistant.Application.Persistence;
using AiAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("postgres");
builder.Services.AddSingleton(TimeProvider.System);
var postgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Задайте ConnectionStrings:Postgres.");
builder.Services.AddDbContext<AssistantDbContext>(options => options.UseNpgsql(postgres));
builder.Services.AddScoped<IAssistantData, AssistantData>();
builder.Services.AddScoped<DatabaseBootstrap>();

var app = builder.Build();
if (builder.Configuration.GetValue<bool>("Database:MigrateOnStart") ||
    (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Database:SeedDemo")))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseBootstrap>().InitializeAsync(
        builder.Configuration.GetValue<bool>("Database:MigrateOnStart"),
        app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Database:SeedDemo"),
        builder.Configuration["Database:DemoPassword"], app.Lifetime.ApplicationStopping);
}
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapHealthChecks("/health");
app.MapControllers();
app.Run();

public partial class Program;
