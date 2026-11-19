using AiAssistant.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AiAssistant.Infrastructure.Persistence;

public sealed class DatabaseBootstrap(AssistantDbContext context, TimeProvider clock)
{
    public async Task InitializeAsync(bool migrate, bool demo, string? demoPassword, CancellationToken cancellationToken)
    {
        if (migrate) await context.Database.MigrateAsync(cancellationToken);
        if (!demo) return;
        ArgumentException.ThrowIfNullOrWhiteSpace(demoPassword);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6142759)", cancellationToken);
        var now = clock.GetUtcNow();
        var passwordHasher = new PasswordHasher<User>();
        var samplePersona = ReferenceData.Personas[0];
        for (var index = 1; index <= 10; index++)
        {
            var username = index == 1 ? "demo-admin" : $"demo-{index:D2}";
            var normalized = username.ToUpperInvariant();
            if (await context.Set<User>().AnyAsync(user => user.NormalizedUsername == normalized, cancellationToken))
            {
                continue;
            }

            var email = $"{username}@demo.invalid";
            var user = new User
            {
                Username = username, NormalizedUsername = normalized, Email = email,
                NormalizedEmail = email.ToUpperInvariant(), PasswordHash = "",
                Role = index == 1 ? Role.Admin : Role.User, CreatedAt = now
            };
            user.PasswordHash = passwordHasher.HashPassword(user, demoPassword);
            AddSample(user, samplePersona.Id, now);
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private void AddSample(User user, Guid personaId, DateTimeOffset now)
    {
        var chat = new Chat
        {
            UserId = user.Id, PersonaId = personaId, Title = "Знакомство с C#",
            CreatedAt = now, LastMessageAt = now
        };
        var question = new Message
        {
            ChatId = chat.Id, Role = MessageRole.User, Content = "Как устроены коллекции в C#?", CreatedAt = now
        };
        var answer = new Message
        {
            ChatId = chat.Id, PersonaId = personaId, Role = MessageRole.Assistant, TriggerMessageId = question.Id,
            Content = "Для последовательности элементов подойдёт `List<T>`, для поиска по ключу — `Dictionary<TKey, TValue>`.",
            CreatedAt = now, Model = ReferenceData.TextModel
        };
        context.AddRange(user, chat, question, answer,
            new PersonalizationProfile { UserId = user.Id, Language = "ru", Interests = ["C#"], UpdatedAt = now },
            new UserMemory
            {
                UserId = user.Id, Content = "Изучает C#", NormalizedContent = "ИЗУЧАЕТ C#", CreatedAt = now
            },
            new MessageFeedback { MessageId = answer.Id, UserId = user.Id, Rating = 1, CreatedAt = now });
    }
}
