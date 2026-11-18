using AiAssistant.Api.Ai;
using AiAssistant.Api.Realtime;
using AiAssistant.Api.Security;
using AiAssistant.Application.Ai;
using AiAssistant.Application.Auth;
using AiAssistant.Application.Chats;
using AiAssistant.Application.Persistence;
using AiAssistant.Application.Personalization;
using AiAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<RegisterAccount>());
builder.Services.AddAssistantAuth(builder.Configuration, builder.Environment);
builder.Services.AddAssistantAi(builder.Configuration, builder.Environment);
builder.Services.AddScoped<ChatAccess>();
builder.Services.AddScoped<ChatSubmission>();
builder.Services.AddScoped<PersonaAccess>();
builder.Services.AddScoped<IPromptContextBuilder, PromptContextBuilder>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<GenerationStops>();
builder.Services.AddSingleton<IGenerationEvents, HubGenerationEvents>();
builder.Services.AddScoped<GenerationProcessor>();
builder.Services.AddHostedService<GenerationWorker>();
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
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
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat", options => options.CloseOnAuthenticationExpiration = true);
app.Run();

public partial class Program;
