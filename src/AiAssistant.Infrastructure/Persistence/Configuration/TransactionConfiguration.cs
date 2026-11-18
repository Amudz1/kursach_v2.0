using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> entity)
    {
        entity.ToTable("transactions");
        entity.HasKey(transaction => transaction.Id);
        entity.Property(transaction => transaction.Amount).HasPrecision(10, 2);
        entity.Property(transaction => transaction.CardMask).HasMaxLength(9);
        entity.Property(transaction => transaction.Status).HasConversion<string>().HasMaxLength(16);
        entity.HasIndex(transaction => new { transaction.UserId, transaction.RequestId }).IsUnique();
        entity.HasOne<User>().WithMany().HasForeignKey(transaction => transaction.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(transaction => transaction.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
