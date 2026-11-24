using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class UserMemoryConfiguration : IEntityTypeConfiguration<UserMemory>
{
    public void Configure(EntityTypeBuilder<UserMemory> entity)
    {
        entity.ToTable("user_memories");
        entity.HasKey(memory => memory.Id);
        entity.Property(memory => memory.Content).HasMaxLength(500);
        entity.Property(memory => memory.NormalizedContent).HasMaxLength(1000);
        entity.Property(memory => memory.Source).HasConversion<string>().HasMaxLength(16);
        entity.HasIndex(memory => new { memory.UserId, memory.NormalizedContent }).IsUnique();
        entity.HasOne<User>().WithMany().HasForeignKey(memory => memory.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
