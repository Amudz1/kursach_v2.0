using System.Text.Json;
using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Appearance;

public sealed record AppearanceInput(short SchemaVersion, string Preset, string ColorMode, JsonElement Settings, bool OnlyIfMissing = false);
public sealed record AppearanceView(short SchemaVersion, string Preset, string ColorMode, JsonElement Settings, DateTimeOffset? UpdatedAt)
{
    public static AppearanceView From(AppearanceSettings? item) => item is null
        ? new(1, "classic", "system", JsonSerializer.SerializeToElement(new Dictionary<string, object>()), null)
        : new(item.SchemaVersion, item.Preset, item.ColorMode, JsonSerializer.Deserialize<JsonElement>(item.Settings), item.UpdatedAt);
}

public sealed record GetAppearanceQuery(Guid UserId) : IRequest<Result<AppearanceView>>;
public sealed record UpdateAppearanceCommand(Guid UserId, AppearanceInput Input) : IRequest<Result<AppearanceView>>;
public sealed record ResetAppearanceCommand(Guid UserId) : IRequest<Result<Unit>>;

public sealed class AppearanceHandlers(IAssistantData data, TimeProvider clock) :
    IRequestHandler<GetAppearanceQuery, Result<AppearanceView>>,
    IRequestHandler<UpdateAppearanceCommand, Result<AppearanceView>>,
    IRequestHandler<ResetAppearanceCommand, Result<Unit>>
{
    public async Task<Result<AppearanceView>> Handle(GetAppearanceQuery request, CancellationToken ct) =>
        Result<AppearanceView>.Ok(AppearanceView.From(await Find(request.UserId, false, ct)));

    public Task<Result<AppearanceView>> Handle(UpdateAppearanceCommand request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        var errors = AppearanceSettingsValidator.Validate(input);
        if (errors.Count != 0) return Task.FromResult(Result<AppearanceView>.Invalid(errors));

        return data.ForUserAsync(request.UserId, async ct =>
        {
            var item = await Find(request.UserId, true, ct);
            if (item is not null && input.OnlyIfMissing) return Result<AppearanceView>.Ok(AppearanceView.From(item));
            if (item is null)
            {
                item = new AppearanceSettings { UserId = request.UserId };
                data.Add(item);
            }
            item.SchemaVersion = input.SchemaVersion;
            item.Preset = input.Preset;
            item.ColorMode = input.ColorMode;
            item.Settings = AppearanceSettingsValidator.SerializeDifferences(input);
            item.UpdatedAt = clock.GetUtcNow();
            await data.SaveAsync(ct);
            return Result<AppearanceView>.Ok(AppearanceView.From(item));
        }, cancellationToken);
    }

    public Task<Result<Unit>> Handle(ResetAppearanceCommand request, CancellationToken cancellationToken) =>
        data.ForUserAsync(request.UserId, async ct =>
        {
            if (await Find(request.UserId, true, ct) is { } item)
            {
                data.Remove(item);
                await data.SaveAsync(ct);
            }
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);

    private Task<AppearanceSettings?> Find(Guid userId, bool forUpdate, CancellationToken ct) =>
        data.FirstAsync(data.Query<AppearanceSettings>(forUpdate).Where(item => item.UserId == userId), ct);
}
