using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> entity)
    {
        entity.ToTable("messages");
        entity.HasKey(message => message.Id);
        entity.Property(message => message.Role).HasConversion<string>().HasMaxLength(16);
        entity.Property(message => message.Status).HasConversion<string>().HasMaxLength(16);
        entity.Property(message => message.Model).HasMaxLength(160);
        entity.Property(message => message.ErrorCode).HasMaxLength(80);
        entity.Property(message => message.ContextSnapshot).HasColumnType("jsonb");
        entity.HasIndex(message => new { message.ChatId, message.CreatedAt, message.Id }).IsDescending(false, true, true);
        entity.HasIndex(message => new { message.ChatId, message.RequestId }).IsUnique();
        entity.HasIndex(message => message.ChatId, "IX_messages_active_generation").IsUnique()
            .HasFilter("role = 'Assistant' AND status IN ('Pending', 'Streaming')");
        entity.HasOne<Chat>().WithMany().HasForeignKey(message => message.ChatId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Persona>().WithMany().HasForeignKey(message => message.PersonaId).OnDelete(DeleteBehavior.SetNull);
    }
}
