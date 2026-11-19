using AiAssistant.Application.Chats;
using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Application.Personalization;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Feedback;

public sealed record RateMessage(Guid UserId, Guid MessageId, int Rating, string? Reason, string? Comment)
    : IRequest<Result<Unit>>;
public sealed record GetAdaptation(Guid UserId) : IRequest<Result<AdaptationView?>>;
public sealed record DecideAdaptation(Guid UserId, Guid FeedbackId, bool Apply) : IRequest<Result<Unit>>;
public sealed record AdaptationView(Guid FeedbackId, string CurrentLength, string ProposedLength);

public sealed class FeedbackHandlers(IAssistantData data, ChatAccess chats, PreferenceReadCache cache, TimeProvider clock) :
    IRequestHandler<RateMessage, Result<Unit>>, IRequestHandler<GetAdaptation, Result<AdaptationView?>>,
    IRequestHandler<DecideAdaptation, Result<Unit>>
{
    private const int RecentFeedbackCount = 5;
    private const int MinimumEvidence = 3;

    public Task<Result<Unit>> Handle(RateMessage request, CancellationToken cancellationToken)
    {
        if (request.Rating is not (-1 or 1) || request.Comment?.Length > 500 ||
            (request.Rating == 1 && request.Reason is not null) ||
            request.Reason is not (null or "too_long" or "too_complex" or "inaccurate" or "wrong_language"))
        {
            return Task.FromResult(Result<Unit>.Fail(ErrorKind.Validation, "invalid_rating", "Проверьте оценку и её причину."));
        }

        return data.ForUserAsync(request.UserId, async ct =>
        {
            var answer = await data.FirstAsync(data.Query<Message>().Where(message => message.Id == request.MessageId &&
                message.Role == MessageRole.Assistant && message.Status == GenerationStatus.Completed), ct);
            var chat = answer is null ? null : await chats.FindAsync(request.UserId, answer.ChatId, false, ct);
            if (answer is null || chat is null || chat.ExpiresAt.HasValue)
                return Result<Unit>.Fail(ErrorKind.NotFound, "message_not_found", "Ответ для оценки не найден.");

            var feedback = await data.FirstAsync(data.Query<MessageFeedback>(true).Where(item =>
                item.UserId == request.UserId && item.MessageId == request.MessageId), ct);
            if (feedback is null)
            {
                feedback = new MessageFeedback { UserId = request.UserId, MessageId = request.MessageId };
                data.Add(feedback);
            }
            feedback.Rating = request.Rating;
            feedback.Reason = request.Reason;
            feedback.Comment = request.Comment?.Trim();
            feedback.CreatedAt = clock.GetUtcNow();
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);
    }

    public async Task<Result<AdaptationView?>> Handle(GetAdaptation request, CancellationToken cancellationToken)
    {
        var profile = await ProfileAsync(request.UserId, false, cancellationToken);
        return Result<AdaptationView?>.Ok(profile is null ? null : await ProposeAsync(profile, cancellationToken));
    }

    public async Task<Result<Unit>> Handle(DecideAdaptation request, CancellationToken cancellationToken)
    {
        var result = await data.ForUserAsync(request.UserId, async ct =>
        {
            var profile = await ProfileAsync(request.UserId, true, ct);
            var proposal = profile is null ? null : await ProposeAsync(profile, ct);
            if (profile is null || proposal is null || proposal.FeedbackId != request.FeedbackId)
                return Result<Unit>.Fail(ErrorKind.Conflict, "adaptation_changed", "Предложение устарело. Обновите страницу.");
            if (request.Apply) profile.ResponseLength = proposal.ProposedLength;
            profile.AdaptationAcknowledgedFor = proposal.FeedbackId;
            profile.UpdatedAt = clock.GetUtcNow();
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);
        if (result.IsSuccess && request.Apply) cache.InvalidateProfiles();
        return result;
    }

    private Task<PersonalizationProfile?> ProfileAsync(Guid userId, bool tracked, CancellationToken cancellationToken) =>
        data.FirstAsync(data.Query<PersonalizationProfile>(tracked).Where(profile => profile.UserId == userId), cancellationToken);

    private async Task<AdaptationView?> ProposeAsync(PersonalizationProfile profile, CancellationToken cancellationToken)
    {
        if (profile.ResponseLength == "short") return null;
        var recent = await data.ListAsync(data.Query<MessageFeedback>().Where(item => item.UserId == profile.UserId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Take(RecentFeedbackCount), cancellationToken);
        if (recent.Count < MinimumEvidence || recent[0].Id == profile.AdaptationAcknowledgedFor ||
            recent.Count(item => item.Rating == -1 && item.Reason == "too_long") * 2 <= recent.Count)
            return null;
        return new(recent[0].Id, profile.ResponseLength, profile.ResponseLength == "long" ? "medium" : "short");
    }
}
