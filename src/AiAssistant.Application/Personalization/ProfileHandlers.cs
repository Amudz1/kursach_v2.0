using System.Text.Json;
using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using FluentValidation;
using MediatR;

namespace AiAssistant.Application.Personalization;

public sealed record ProfileView(string Language, string Tone, string ExpertiseLevel, string ResponseFormat,
    string ResponseLength, string[] Interests, string About, bool AutoMemoryEnabled, bool OnboardingCompleted,
    bool? AdaptToTask = null, bool? UseMemoryInReplies = null, string? Goal = null, TaskPreference[]? TaskRules = null)
{
    public static ProfileView From(PersonalizationProfile profile) => new(profile.Language, profile.Tone, profile.ExpertiseLevel,
        profile.ResponseFormat, profile.ResponseLength, profile.Interests, profile.About, profile.AutoMemoryEnabled,
        profile.OnboardingCompleted, profile.AdaptToTask, profile.UseMemoryInReplies, profile.Goal,
        JsonSerializer.Deserialize<TaskPreference[]>(profile.TaskRulesJson) ?? []);
}

public sealed record GetProfile(Guid UserId) : IRequest<Result<ProfileView>>;
public sealed record SaveProfile(Guid UserId, ProfileView Profile) : IRequest<Result<Unit>>;
public sealed record PreviewProfile(ProfileView Profile, string Sample, string? Previous = null) : IRequest<Result<ResponseDecision>>;

public sealed class ProfileHandlers(IAssistantData data, PreferenceReadCache cache, TimeProvider clock) :
    IRequestHandler<GetProfile, Result<ProfileView>>, IRequestHandler<SaveProfile, Result<Unit>>,
    IRequestHandler<PreviewProfile, Result<ResponseDecision>>
{
    internal static readonly IValidator<ProfileView> Validator = new InlineValidator<ProfileView>
    {
        value => value.RuleFor(profile => profile.Language).Must(language => language is "ru" or "en" or "auto"),
        value => value.RuleFor(profile => profile.Tone).Must(tone => tone is "formal" or "friendly" or "concise" or "detailed"),
        value => value.RuleFor(profile => profile.ExpertiseLevel).Must(level => level is "beginner" or "intermediate" or "expert"),
        value => value.RuleFor(profile => profile.ResponseFormat).Must(format => format is "text" or "lists" or "code" or "steps"),
        value => value.RuleFor(profile => profile.ResponseLength).Must(length => length is "short" or "medium" or "long"),
        value => value.RuleFor(profile => profile.About).NotNull().MaximumLength(1000),
        value => value.RuleFor(profile => profile.Interests).NotNull().Must(interests => interests is { Length: <= 12 }),
        value => value.RuleForEach(profile => profile.Interests).NotEmpty().MaximumLength(40),
        value => value.RuleFor(profile => profile.Goal).MaximumLength(300),
        value => value.RuleFor(profile => profile.TaskRules).Must(ValidRules)
            .WithMessage("Для каждого типа задачи можно задать одно правило длиной до 300 символов.")
    };

    private static bool ValidRules(TaskPreference[]? rules) => rules is null || (rules.Length <= 5 &&
        rules.All(rule => rule is not null && ResponsePolicy.IsTask(rule.Task) &&
            (rule.Length is null || ResponsePolicy.IsLength(rule.Length)) &&
            (rule.Format is null || ResponsePolicy.IsFormat(rule.Format)) && rule.Instructions?.Length is null or <= 300) &&
        rules.Select(rule => rule.Task).Distinct(StringComparer.Ordinal).Count() == rules.Length);

    public async Task<Result<ProfileView>> Handle(GetProfile request, CancellationToken cancellationToken)
    {
        var profile = await cache.ProfileAsync(request.UserId, async () =>
        {
            var record = await data.FirstAsync(data.Query<PersonalizationProfile>()
                .Where(profile => profile.UserId == request.UserId), cancellationToken);
            return ProfileView.From(record ?? new PersonalizationProfile { UserId = request.UserId });
        });
        return Result<ProfileView>.Ok(profile);
    }

    public async Task<Result<ResponseDecision>> Handle(PreviewProfile request, CancellationToken cancellationToken)
    {
        if (request.Sample.Trim().Length is 0 or > 400)
            return Result<ResponseDecision>.Fail(ErrorKind.Validation, "invalid_sample", "Введите пример длиной до 400 символов.");
        if (request.Previous?.Length > 400)
            return Result<ResponseDecision>.Fail(ErrorKind.Validation, "invalid_previous_sample", "Предыдущий запрос должен быть не длиннее 400 символов.");
        var validation = await Validator.ValidateAsync(request.Profile, cancellationToken);
        return validation.IsValid
            ? Result<ResponseDecision>.Ok(ResponsePolicy.Resolve(request.Profile, request.Sample.Trim(), request.Previous?.Trim()))
            : Result<ResponseDecision>.Invalid(validation.ToDictionary());
    }

    public async Task<Result<Unit>> Handle(SaveProfile request, CancellationToken cancellationToken)
    {
        var validation = await Validator.ValidateAsync(request.Profile, cancellationToken);
        if (!validation.IsValid) return Result<Unit>.Invalid(validation.ToDictionary());

        var result = await data.ForUserAsync(request.UserId, async ct =>
        {
            var profile = await data.FirstAsync(data.Query<PersonalizationProfile>(true)
                .Where(profile => profile.UserId == request.UserId), ct);
            if (profile is null)
            {
                profile = new PersonalizationProfile { UserId = request.UserId };
                data.Add(profile);
            }
            Apply(profile, request.Profile, clock.GetUtcNow());
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);
        if (result.IsSuccess) cache.InvalidateProfiles();
        return result;
    }

    private static void Apply(PersonalizationProfile profile, ProfileView input, DateTimeOffset now)
    {
        profile.Language = input.Language;
        profile.Tone = input.Tone;
        profile.ExpertiseLevel = input.ExpertiseLevel;
        profile.ResponseFormat = input.ResponseFormat;
        profile.ResponseLength = input.ResponseLength;
        profile.About = input.About.Trim();
        profile.Interests = input.Interests.Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        profile.AutoMemoryEnabled = input.AutoMemoryEnabled;
        profile.AdaptToTask = input.AdaptToTask ?? profile.AdaptToTask;
        profile.UseMemoryInReplies = input.UseMemoryInReplies ?? profile.UseMemoryInReplies;
        if (input.Goal is not null) profile.Goal = input.Goal.Trim();
        if (input.TaskRules is not null)
            profile.TaskRulesJson = JsonSerializer.Serialize(input.TaskRules.Select(rule =>
                rule with { Instructions = rule.Instructions?.Trim() }));
        profile.OnboardingCompleted |= input.OnboardingCompleted;
        profile.UpdatedAt = now;
    }
}
