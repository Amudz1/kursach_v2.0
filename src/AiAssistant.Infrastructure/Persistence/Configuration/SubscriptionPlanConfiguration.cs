using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> entity)
    {
        entity.ToTable("subscription_plans");
        entity.HasKey(plan => plan.Id);
        entity.Property(plan => plan.Code).HasMaxLength(20);
        entity.Property(plan => plan.Name).HasMaxLength(100);
        entity.Property(plan => plan.Description).HasMaxLength(500);
        entity.Property(plan => plan.Price).HasPrecision(10, 2);
        entity.Property(plan => plan.AllowedModels).HasColumnType("jsonb");
        entity.HasIndex(plan => plan.Code).IsUnique();
        entity.ToTable(table => table.HasCheckConstraint("ck_subscription_plans_limits",
            "price >= 0 AND duration_days >= 0 AND (daily_request_limit IS NULL OR daily_request_limit >= 0) " +
            "AND max_personas >= 0 AND max_memories >= 0 AND max_attachment_bytes >= 0"));
        entity.HasData(ReferenceData.Plans);
    }
}
