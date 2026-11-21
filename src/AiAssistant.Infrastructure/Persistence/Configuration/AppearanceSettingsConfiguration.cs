using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class AppearanceSettingsConfiguration : IEntityTypeConfiguration<AppearanceSettings>
{
    public void Configure(EntityTypeBuilder<AppearanceSettings> entity)
    {
        entity.ToTable("appearance_settings", table =>
        {
            table.HasCheckConstraint("ck_appearance_settings_color_mode", "color_mode IN ('system', 'light', 'dark')");
            table.HasCheckConstraint("ck_appearance_settings_size", "pg_column_size(settings) <= 8192");
        });
        entity.HasKey(item => item.UserId);
        entity.HasOne<User>().WithOne().HasForeignKey<AppearanceSettings>(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Property(item => item.SchemaVersion).HasDefaultValue((short)1);
        entity.Property(item => item.Preset).HasMaxLength(32).HasDefaultValue("classic");
        entity.Property(item => item.ColorMode).HasMaxLength(8).HasDefaultValue("system");
        entity.Property(item => item.Settings).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
    }
}
