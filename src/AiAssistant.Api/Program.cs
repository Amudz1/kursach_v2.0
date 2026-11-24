using AiAssistant.Api.Ai;
using AiAssistant.Api.Documentation;
using AiAssistant.Api.Realtime;
using AiAssistant.Api.Security;
using AiAssistant.Api.Voice;
using AiAssistant.Application.Administration;
using AiAssistant.Application.Ai;
using AiAssistant.Application.Auth;
using AiAssistant.Application.Billing;
using AiAssistant.Application.Chats;
using AiAssistant.Application.Files;
using AiAssistant.Application.Memory;
using AiAssistant.Application.Persistence;
using AiAssistant.Application.Personalization;
using AiAssistant.Infrastructure.Persistence;
using AiAssistant.Infrastructure.Files;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddAssistantDocumentation();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PreferenceReadCache>();
builder.Services.AddSerilog((_, configuration) => configuration.Enrich.FromLogContext()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console());
builder.Services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<RegisterAccount>());
builder.Services.AddAssistantAuth(builder.Configuration, builder.Environment);
builder.Services.AddAssistantAi(builder.Configuration, builder.Environment);
builder.Services.AddAssistantVoice(builder.Configuration, builder.Environment);
builder.Services.AddScoped<ChatAccess>();
builder.Services.AddScoped<ChatSubmission>();
builder.Services.AddSingleton(new AttachmentRoot(Path.GetFullPath(
    builder.Configuration["Files:Directory"] ?? ".local/attachments", builder.Environment.ContentRootPath)));
builder.Services.AddSingleton<IAttachmentStorage, LocalAttachmentStorage>();
builder.Services.AddScoped<AttachmentLifecycle>();
builder.Services.AddHostedService<AttachmentCleanupWorker>();
builder.Services.AddHostedService<TemporaryChatCleanupWorker>();
builder.Services.AddScoped<PlanAccess>();
builder.Services.AddScoped<SubscriptionWriter>();
builder.Services.AddScoped<AdminAccess>();
builder.Services.AddSingleton(new ContextDiagnostics(
    builder.Configuration.GetValue("Diagnostics:AllowContext", builder.Environment.IsDevelopment())));
builder.Services.AddScoped<IUsageLimitService, UsageLimitService>();
builder.Services.AddScoped<PersonaAccess>();
builder.Services.AddScoped<IPromptContextBuilder, PromptContextBuilder>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<GenerationStops>();
builder.Services.AddSingleton<IGenerationEvents, HubGenerationEvents>();
builder.Services.AddScoped<GenerationProcessor>();
builder.Services.AddHostedService<GenerationWorker>();
builder.Services.AddScoped<FollowUpProcessor>();
builder.Services.AddHostedService<FollowUpWorker>();
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
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
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Diagnostics:Swagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Helper Budz v1"));
}
app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat", options => options.CloseOnAuthenticationExpiration = true);
app.Run();

public partial class Program;
