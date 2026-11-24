using AiAssistant.Application.Billing;
using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Administration;

public sealed record GetPersonalStatistics(Guid UserId, int Days) : IRequest<Result<StatisticsView>>;
public sealed record GetAdminStatistics(Guid AdminId, int Days) : IRequest<Result<StatisticsView>>;
public sealed record StatGroup(string Label, int Count);
public sealed record StatisticsView(int Days, long Requests, int ActiveUsers, long PromptTokens, long CompletionTokens,
    double? PositiveShare, double? MeanRating, IReadOnlyList<StatGroup> ByModel, IReadOnlyList<StatGroup> ByPersona,
    UsageView? Usage);

public sealed class StatisticsHandlers(IAssistantData data, IUsageLimitService usage, AdminAccess access, TimeProvider clock) :
    IRequestHandler<GetPersonalStatistics, Result<StatisticsView>>, IRequestHandler<GetAdminStatistics, Result<StatisticsView>>
{
    public Task<Result<StatisticsView>> Handle(GetPersonalStatistics request, CancellationToken cancellationToken) =>
        ReadAsync(request.UserId, request.Days, cancellationToken);

    public async Task<Result<StatisticsView>> Handle(GetAdminStatistics request, CancellationToken cancellationToken)
    {
        if (!await access.AllowedAsync(request.AdminId, cancellationToken)) return AdminAccess.Denied<StatisticsView>();
        return await ReadAsync(null, request.Days, cancellationToken);
    }

    private async Task<Result<StatisticsView>> ReadAsync(Guid? userId, int days, CancellationToken cancellationToken)
    {
        if (days is < 1 or > 365)
            return Result<StatisticsView>.Fail(ErrorKind.Validation, "invalid_period", "Период должен составлять от 1 до 365 дней.");

        var since = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(1 - days);
        var start = new DateTimeOffset(since.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var counters = data.Query<UsageCounter>().Where(counter => counter.Date >= since && (userId == null || counter.UserId == userId));
        var requests = await data.FirstAsync(counters.GroupBy(_ => 1)
            .Select(group => group.Sum(counter => (long)counter.RequestsCount)), cancellationToken);
        var users = await data.CountAsync(counters.Where(counter => counter.RequestsCount > 0).Select(counter => counter.UserId).Distinct(),
            cancellationToken);
        var rows = from message in data.Query<Message>()
                   join chat in data.Query<Chat>() on message.ChatId equals chat.Id
                   where message.Role == MessageRole.Assistant && message.Model != null && message.CreatedAt >= start &&
                         (userId == null || chat.UserId == userId) &&
                         (message.Status == GenerationStatus.Completed || message.PromptTokens != null ||
                          message.CompletionTokens != null || message.Status == GenerationStatus.Superseded)
                   select message;

        var models = await data.ListAsync(rows.GroupBy(message => message.Model!)
            .Select(group => new StatGroup(group.Key, group.Count())), cancellationToken);
        var personaRows = from message in rows
                          join persona in data.Query<Persona>() on message.PersonaId equals persona.Id into personas
                          from persona in personas.DefaultIfEmpty()
                          group message by persona == null ? "Без персоны" : persona.Name into grouped
                          select new StatGroup(grouped.Key, grouped.Count());
        var personasUsed = await data.ListAsync(personaRows, cancellationToken);
        var tokens = await data.FirstAsync(rows.GroupBy(_ => 1).Select(group => new TokenTotals(
            group.Sum(message => (long)(message.PromptTokens ?? 0)), group.Sum(message => (long)(message.CompletionTokens ?? 0)))),
            cancellationToken);
        var ratings = data.Query<MessageFeedback>().Where(feedback => feedback.CreatedAt >= start &&
            (userId == null || feedback.UserId == userId));
        var score = await data.FirstAsync(ratings.GroupBy(_ => 1).Select(group => new RatingTotals(group.Count(),
            group.Count(feedback => feedback.Rating > 0), group.Average(feedback => feedback.Rating))), cancellationToken);
        var limits = userId is { } id ? await usage.ReadAsync(id, cancellationToken) : null;
        return Result<StatisticsView>.Ok(new(days, requests, users, tokens?.Prompt ?? 0, tokens?.Completion ?? 0,
            score is null ? null : (double)score.Positive / score.Count, score?.Average,
            Order(models), Order(personasUsed), limits));
    }

    private static StatGroup[] Order(List<StatGroup> items) => items
        .OrderByDescending(item => item.Count).ThenBy(item => item.Label, StringComparer.Ordinal).ToArray();

    private sealed record TokenTotals(long Prompt, long Completion);
    private sealed record RatingTotals(int Count, int Positive, double Average);
}
