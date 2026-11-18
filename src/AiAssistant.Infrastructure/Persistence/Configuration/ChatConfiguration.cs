using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class ChatConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> entity)
    {
        entity.ToTable("chats");
        entity.HasKey(chat => chat.Id);
        entity.Property(chat => chat.Title).HasMaxLength(120);
        entity.HasIndex(chat => new { chat.UserId, chat.LastMessageAt, chat.Id }).IsDescending(false, true, true);
        entity.HasOne<User>().WithMany().HasForeignKey(chat => chat.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Persona>().WithMany().HasForeignKey(chat => chat.PersonaId).OnDelete(DeleteBehavior.SetNull);
    }
}
