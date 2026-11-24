using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class UsageCounterConfiguration : IEntityTypeConfiguration<UsageCounter>
{
    public void Configure(EntityTypeBuilder<UsageCounter> entity)
    {
        entity.ToTable("usage_counters");
        entity.HasKey(counter => new { counter.UserId, counter.Date });
        entity.HasOne<User>().WithMany().HasForeignKey(counter => counter.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.ToTable(table => table.HasCheckConstraint("ck_usage_counters_values", "requests_count >= 0 AND tokens_used >= 0"));
    }
}
