using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class SupportMessageConfiguration : IEntityTypeConfiguration<SupportMessage>
{
    public void Configure(EntityTypeBuilder<SupportMessage> entity)
    {
        entity.ToTable("support_messages");
        entity.HasKey(item => item.Id);
        entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.Property(item => item.Content).HasMaxLength(4000).IsRequired();
        entity.HasIndex(item => new { item.UserId, item.Id });
        entity.HasIndex(item => new { item.UserId, item.RequestId }).IsUnique();
    }
}
