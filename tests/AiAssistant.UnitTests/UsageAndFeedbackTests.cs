using AiAssistant.Application.Billing;
using AiAssistant.Application.Chats;
using AiAssistant.Application.Common;
using AiAssistant.Application.Feedback;
using AiAssistant.Application.Personalization;
using AiAssistant.Domain;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AiAssistant.UnitTests;

public sealed class UsageAndFeedbackTests
{
    private static readonly DateTimeOffset Reference = new(2026, 8, 1, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public async Task MidnightUsesNewCounterAndReadDoesNotWrite()
    {
        var data = new MemoryData();
        var now = new FixedClock(Reference);
        var user = Guid.NewGuid();
        data.Add(new SubscriptionPlan { Code = "Free", Name = "Free", AllowedModels = ["test"], DailyTokenLimit = 2200 });
        var limits = new UsageLimitService(data, new PlanAccess(data, now), now);
        var ct = CancellationToken.None;
        Assert.Equal(2200, (await limits.ReadAsync(user, ct)).RemainingTokens);
        Assert.Empty(data.Query<UsageCounter>());
        Assert.True(await limits.EnqueueAsync(user, 2200, now.Now, ct));
        var chat = new Chat { UserId = user };
        data.Add(chat);
        var pending = new Message { ChatId = chat.Id, Role = MessageRole.Assistant, Content = "",
            CreatedAt = now.Now, ReservedTokens = 1600 };
        data.Add(pending);
        Assert.False(await limits.EnqueueAsync(user, 2200, now.Now, ct));
        Assert.Equal(1600, (await limits.ReadAsync(user, ct)).ReservedTokens);
        pending.ReservedTokens = 0;
        await limits.FinishAsync(user, now.Now, false, 1450, ct);
        Assert.False(await limits.EnqueueAsync(user, 2200, now.Now, ct));
        Assert.Equal(750, (await limits.ReadAsync(user, ct)).RemainingTokens);
        now.Now = Reference.AddSeconds(1);
        Assert.Equal(2200, (await limits.ReadAsync(user, ct)).RemainingTokens);
        Assert.True(await limits.EnqueueAsync(user, 2200, now.Now, ct));
        Assert.Equal([1450L, 0L], data.Query<UsageCounter>().OrderBy(item => item.Date).Select(item => item.TokensUsed).ToArray());
        Assert.Equal(0, data.Saves);
    }

    [Fact]
    public async Task ActiveUnlimitedPlanPreservesDailyConsumption()
    {
        var data = new MemoryData();
        var clock = new FixedClock(Reference);
        var user = Guid.NewGuid();
        var paid = new SubscriptionPlan { Code = "Pro", Name = "Unlimited", DailyTokenLimit = null };
        data.Add(paid);
        data.Add(new SubscriptionPlan { Code = "Free", Name = "Free", DailyTokenLimit = 1 });
        data.Add(new Subscription { PlanId = paid.Id, UserId = user, StartsAt = Reference.AddDays(-1), EndsAt = Reference.AddDays(1) });
        var limits = new UsageLimitService(data, new PlanAccess(data, clock), clock);
        var ct = CancellationToken.None;
        Assert.True(await limits.EnqueueAsync(user, null, Reference, ct));
        Assert.True(await limits.EnqueueAsync(user, null, Reference, ct));
        await limits.FinishAsync(user, Reference, false, 125, ct);
        var active = await limits.ReadAsync(user, ct);
        Assert.Equal("Pro", active.PlanCode);
        Assert.Null(active.RemainingTokens);
        Assert.Equal(125, active.UsedTokensToday);
        Assert.Equal(2, active.RequestsToday);
        clock.Now = Reference.AddDays(1);
        Assert.Equal("Free", (await limits.ReadAsync(user, ct)).PlanCode);
    }

    [Fact]
    public async Task AdaptationNeedsConsentAndDismissalSuppressesTheSameSuggestion()
    {
        var data = new MemoryData();
        var clock = new FixedClock(Reference);
        var user = Guid.NewGuid();
        var profile = new PersonalizationProfile { UserId = user, ResponseLength = "long" };
        data.Add(profile);
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var handler = new FeedbackHandlers(data, new ChatAccess(data), new PreferenceReadCache(memory), clock);
        var ct = CancellationToken.None;
        Assert.Null((await handler.Handle(new GetAdaptation(user), ct)).Value);
        for (var index = 0; index < 3; index++)
            data.Add(new MessageFeedback { UserId = user, MessageId = Guid.NewGuid(), Rating = -1, Reason = "too_long",
                CreatedAt = Reference.AddSeconds(index) });
        var suggestion = (await handler.Handle(new GetAdaptation(user), ct)).Value;
        Assert.NotNull(suggestion);
        Assert.Equal("medium", suggestion.ProposedLength);
        Assert.Equal("long", profile.ResponseLength);
        Assert.Equal(ErrorKind.Conflict, (await handler.Handle(new DecideAdaptation(user, Guid.NewGuid(), true), ct)).Error?.Kind);
        Assert.True((await handler.Handle(new DecideAdaptation(user, suggestion.FeedbackId, false), ct)).IsSuccess);
        Assert.Null((await handler.Handle(new GetAdaptation(user), ct)).Value);
        Assert.Equal("long", profile.ResponseLength);
        data.Add(new MessageFeedback { UserId = user, MessageId = Guid.NewGuid(), Rating = -1, Reason = "too_long",
            CreatedAt = Reference.AddSeconds(4) });
        var next = (await handler.Handle(new GetAdaptation(user), ct)).Value!;
        Assert.True((await handler.Handle(new DecideAdaptation(user, next.FeedbackId, true), ct)).IsSuccess);
        Assert.Equal("medium", profile.ResponseLength);
    }

    [Fact]
    public async Task ProfileValidationAndCacheInvalidation()
    {
        var data = new MemoryData();
        var user = Guid.NewGuid();
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var handler = new ProfileHandlers(data, new PreferenceReadCache(memory), new FixedClock(Reference));
        var ct = CancellationToken.None;
        var initial = (await handler.Handle(new GetProfile(user), ct)).Value!;
        Assert.Equal("auto", initial.Language);
        var rejected = await handler.Handle(new SaveProfile(user, initial with { Tone = "unrecognised" }), ct);
        Assert.Equal(ErrorKind.Validation, rejected.Error?.Kind);
        var revised = initial with { Language = "en", About = " a note ", Interests = [" C#", "c#"], OnboardingCompleted = true };
        Assert.True((await handler.Handle(new SaveProfile(user, revised), ct)).IsSuccess);
        var saved = (await handler.Handle(new GetProfile(user), ct)).Value!;
        Assert.Equal("en", saved.Language);
        Assert.Equal("a note", saved.About);
        Assert.Equal(["C#"], saved.Interests);
        Assert.True(saved.OnboardingCompleted);
    }
}
