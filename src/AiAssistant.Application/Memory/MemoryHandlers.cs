using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Memory;

public sealed record MemoryView(Guid Id, string Content, string Source, bool IsEnabled, DateTimeOffset CreatedAt);
public sealed record ListMemories(Guid UserId) : IRequest<Result<IReadOnlyList<MemoryView>>>;
public sealed record SaveMemory(Guid UserId, Guid? MemoryId, string Content, bool IsEnabled) : IRequest<Result<Guid>>;
public sealed record DeleteMemory(Guid UserId, Guid MemoryId) : IRequest<Result<Unit>>;

public sealed class MemoryHandlers(IAssistantData data, TimeProvider clock) :
    IRequestHandler<ListMemories, Result<IReadOnlyList<MemoryView>>>, IRequestHandler<SaveMemory, Result<Guid>>,
    IRequestHandler<DeleteMemory, Result<Unit>>
{
    public async Task<Result<IReadOnlyList<MemoryView>>> Handle(ListMemories request, CancellationToken cancellationToken)
    {
        var records = await data.ListAsync(data.Query<UserMemory>().Where(memory => memory.UserId == request.UserId)
            .OrderByDescending(memory => memory.CreatedAt).ThenByDescending(memory => memory.Id)
            .Select(memory => new MemoryView(memory.Id, memory.Content, memory.Source.ToString().ToLower(),
                memory.IsEnabled, memory.CreatedAt)), cancellationToken);
        return Result<IReadOnlyList<MemoryView>>.Ok(records);
    }

    public Task<Result<Guid>> Handle(SaveMemory request, CancellationToken cancellationToken)
    {
        var content = request.Content.Trim();
        if (content.Length is 0 or > 500)
            return Task.FromResult(Result<Guid>.Fail(ErrorKind.Validation, "invalid_memory", "Факт должен содержать от 1 до 500 символов."));
        var normalized = MemoryText.Normalize(content);

        return data.ForUserAsync(request.UserId, async ct =>
        {
            var records = data.Query<UserMemory>(true).Where(memory => memory.UserId == request.UserId);
            var memory = request.MemoryId is { } id
                ? await data.FirstAsync(records.Where(memory => memory.Id == id), ct)
                : null;
            if (request.MemoryId.HasValue && memory is null) return NotFound<Guid>();
            if (await data.AnyAsync(records.Where(item => item.NormalizedContent == normalized && item.Id != request.MemoryId), ct))
                return Result<Guid>.Fail(ErrorKind.Conflict, "memory_duplicate", "Такой факт уже сохранён.");

            if (memory is null)
            {
                memory = new UserMemory { UserId = request.UserId, Content = content, NormalizedContent = normalized,
                    Source = MemorySource.Manual, CreatedAt = clock.GetUtcNow() };
                data.Add(memory);
            }
            memory.Content = content;
            memory.NormalizedContent = normalized;
            memory.IsEnabled = request.IsEnabled;
            await data.SaveAsync(ct);
            return Result<Guid>.Ok(memory.Id);
        }, cancellationToken);
    }

    public Task<Result<Unit>> Handle(DeleteMemory request, CancellationToken cancellationToken) =>
        data.ForUserAsync(request.UserId, async ct =>
        {
            var memory = await data.FirstAsync(data.Query<UserMemory>(true)
                .Where(memory => memory.UserId == request.UserId && memory.Id == request.MemoryId), ct);
            if (memory is null) return NotFound<Unit>();
            data.Remove(memory);
            await data.SaveAsync(ct);
            return Result<Unit>.Ok(Unit.Value);
        }, cancellationToken);

    private static Result<T> NotFound<T>() =>
        Result<T>.Fail(ErrorKind.NotFound, "memory_not_found", "Факт не найден.");
}
