using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> entity)
    {
        entity.ToTable("subscriptions");
        entity.HasKey(subscription => subscription.Id);
        entity.Property(subscription => subscription.Status).HasConversion<string>().HasMaxLength(16);
        entity.HasIndex(subscription => subscription.UserId).HasFilter("status = 'Active'").IsUnique();
        entity.HasOne<User>().WithMany().HasForeignKey(subscription => subscription.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(subscription => subscription.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
