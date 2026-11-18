using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> entity)
    {
        entity.ToTable("attachments");
        entity.HasKey(attachment => attachment.Id);
        entity.Property(attachment => attachment.FileName).HasMaxLength(255);
        entity.Property(attachment => attachment.StorageKey).HasMaxLength(64);
        entity.Property(attachment => attachment.ContentType).HasMaxLength(160);
        entity.HasIndex(attachment => attachment.StorageKey).IsUnique();
        entity.HasOne<Message>().WithMany().HasForeignKey(attachment => attachment.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}
