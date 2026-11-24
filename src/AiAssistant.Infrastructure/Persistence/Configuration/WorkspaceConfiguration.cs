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
        entity.Property(item => item.Color).HasMaxLength(16);
        entity.Property(item => item.Icon).HasMaxLength(32);
        entity.ToTable(table => table.HasCheckConstraint("ck_workspaces_color", "color IN ('cat-1','cat-2','cat-3','cat-4','cat-5','cat-6','cat-7','cat-8')"));
        entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
