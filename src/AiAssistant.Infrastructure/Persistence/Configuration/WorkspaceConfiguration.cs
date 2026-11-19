using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> entity)
    {
        entity.ToTable("workspaces");
        entity.HasKey(item => item.Id);
        entity.HasAlternateKey(item => new { item.Id, item.UserId });
        entity.HasIndex(item => item.UserId);
        entity.Property(item => item.Name).HasMaxLength(80);
        entity.Property(item => item.Instructions).HasMaxLength(2000);
        entity.Property(item => item.Materials).HasMaxLength(10000);
        entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
