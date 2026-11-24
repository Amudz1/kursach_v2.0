using AiAssistant.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AiAssistant.Api.Documentation;

internal static class ApiDocumentation
{
    public static IServiceCollection AddAssistantDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "AI Helper Budz API", Version = "v1" });
            options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
                Description = "Access-токен из ответа регистрации или входа."
            });
            options.OperationFilter<AssistantOperationFilter>();
        });
        return services;
    }
}

internal sealed class AssistantOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.MethodInfo;
        var controller = method.DeclaringType!;
        if (!method.IsDefined(typeof(AllowAnonymousAttribute), true) &&
            !controller.IsDefined(typeof(AllowAnonymousAttribute), true) &&
            (method.IsDefined(typeof(AuthorizeAttribute), true) || controller.IsDefined(typeof(AuthorizeAttribute), true)))
        {
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("bearer", context.Document)] = []
            }];
            operation.Responses ??= [];
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Сессия недействительна." });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Операция недоступна для роли или тарифа." });
        }
        if (controller == typeof(ChatsController) && method.Name == nameof(ChatsController.Send))
            AddMessageForm(operation, context);
    }

    private static void AddMessageForm(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new()
                {
                    Schema = context.SchemaGenerator.GenerateSchema(typeof(ChatsController.SendBody), context.SchemaRepository)
                },
                ["multipart/form-data"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["content"] = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 10000 },
                            ["requestId"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                            ["model"] = new OpenApiSchema { Type = JsonSchemaType.String },
                            ["attachment"] = new OpenApiSchema
                            {
                                Type = JsonSchemaType.Array, MaxItems = 4,
                                Items = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }
                            }
                        }
                    }
                }
            }
        };
    }
}
