using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("users");
        entity.HasKey(user => user.Id);
        entity.Property(user => user.Username).HasMaxLength(30);
        entity.Property(user => user.NormalizedUsername).HasMaxLength(60);
        entity.Property(user => user.Email).HasMaxLength(254);
        entity.Property(user => user.NormalizedEmail).HasMaxLength(254);
        entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(16);
        entity.HasIndex(user => user.NormalizedUsername).IsUnique();
        entity.HasIndex(user => user.NormalizedEmail).IsUnique();
    }
}
