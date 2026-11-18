using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using FluentValidation;
using MediatR;

namespace AiAssistant.Application.Personalization;

public sealed record ProfileView(string Language, string Tone, string ExpertiseLevel, string ResponseFormat,
    string ResponseLength, string[] Interests, string About, bool AutoMemoryEnabled, bool OnboardingCompleted)
{
    public static ProfileView From(PersonalizationProfile profile) => new(profile.Language, profile.Tone, profile.ExpertiseLevel,
        profile.ResponseFormat, profile.ResponseLength, profile.Interests, profile.About, profile.AutoMemoryEnabled, profile.OnboardingCompleted);
}

public sealed record GetProfile(Guid UserId) : IRequest<Result<ProfileView>>;
public sealed record SaveProfile(Guid UserId, ProfileView Profile) : IRequest<Result<Unit>>;

public sealed class ProfileHandlers(IAssistantData data, TimeProvider clock) :
    IRequestHandler<GetProfile, Result<ProfileView>>, IRequestHandler<SaveProfile, Result<Unit>>
{
    private static readonly IValidator<ProfileView> Validator = new InlineValidator<ProfileView>
    {
        value => value.RuleFor(profile => profile.Language).Must(language => language is "ru" or "en" or "auto"),
        value => value.RuleFor(profile => profile.Tone).Must(tone => tone is "formal" or "friendly" or "concise" or "detailed"),
        value => value.RuleFor(profile => profile.ExpertiseLevel).Must(level => level is "beginner" or "intermediate" or "expert"),
        value => value.RuleFor(profile => profile.ResponseFormat).Must(format => format is "text" or "lists" or "code" or "steps"),
        value => value.RuleFor(profile => profile.ResponseLength).Must(length => length is "short" or "medium" or "long"),
        value => value.RuleFor(profile => profile.About).NotNull().MaximumLength(1000),
        value => value.RuleFor(profile => profile.Interests).NotNull().Must(interests => interests is { Length: <= 12 }),
        value => value.RuleForEach(profile => profile.Interests).NotEmpty().MaximumLength(40)
    };

    public async Task<Result<ProfileView>> Handle(GetProfile request, CancellationToken cancellationToken)
    {
        var profile = await data.FirstAsync(data.Query<PersonalizationProfile>()
            .Where(profile => profile.UserId == request.UserId), cancellationToken);
        return Result<ProfileView>.Ok(ProfileView.From(profile ?? new PersonalizationProfile { UserId = request.UserId }));
    }

    public async Task<Result<Unit>> Handle(SaveProfile request, CancellationToken cancellationToken)
    {
        var validation = await Validator.ValidateAsync(request.Profile, cancellationToken);
        if (!validation.IsValid) return Result<Unit>.Invalid(validation.ToDictionary());

        return await data.ForUserAsync(request.UserId, async ct =>
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
        profile.OnboardingCompleted |= input.OnboardingCompleted;
        profile.UpdatedAt = now;
    }
}
