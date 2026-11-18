using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class PersonalizationProfileConfiguration : IEntityTypeConfiguration<PersonalizationProfile>
{
    public void Configure(EntityTypeBuilder<PersonalizationProfile> entity)
    {
        entity.ToTable("personalization_profiles");
        entity.HasKey(profile => profile.UserId);
        entity.Property(profile => profile.About).HasMaxLength(1000);
        entity.Property(profile => profile.Language).HasMaxLength(8);
        entity.Property(profile => profile.Tone).HasMaxLength(20);
        entity.Property(profile => profile.ExpertiseLevel).HasMaxLength(20);
        entity.Property(profile => profile.ResponseFormat).HasMaxLength(20);
        entity.Property(profile => profile.ResponseLength).HasMaxLength(20);
        entity.Property(profile => profile.AdaptToTask).HasDefaultValue(true).HasSentinel(true);
        entity.Property(profile => profile.UseMemoryInReplies).HasDefaultValue(true).HasSentinel(true);
        entity.Property(profile => profile.Goal).HasMaxLength(300).HasDefaultValue("");
        entity.Property(profile => profile.TaskRulesJson).HasColumnType("jsonb").HasDefaultValue("[]");
        entity.HasOne<User>().WithOne().HasForeignKey<PersonalizationProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
