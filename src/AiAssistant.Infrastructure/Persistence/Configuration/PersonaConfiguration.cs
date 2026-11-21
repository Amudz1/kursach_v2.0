using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class PersonaConfiguration : IEntityTypeConfiguration<Persona>
{
    public void Configure(EntityTypeBuilder<Persona> entity)
    {
        entity.ToTable("personas");
        entity.HasKey(persona => persona.Id);
        entity.Property(persona => persona.Name).HasMaxLength(80);
        entity.Property(persona => persona.Description).HasMaxLength(500);
        entity.Property(persona => persona.SystemPrompt).HasMaxLength(4000);
        entity.Property(persona => persona.DefaultModel).HasMaxLength(160);
        entity.Property(persona => persona.Color).HasMaxLength(16);
        entity.Property(persona => persona.Icon).HasMaxLength(32);
        entity.HasOne<User>().WithMany().HasForeignKey(persona => persona.OwnerId).OnDelete(DeleteBehavior.Cascade);
        entity.ToTable(table => table.HasCheckConstraint("ck_personas_temperature", "temperature >= 0 AND temperature <= 2"));
        entity.ToTable(table => table.HasCheckConstraint("ck_personas_color", "color IN ('cat-1','cat-2','cat-3','cat-4','cat-5','cat-6','cat-7','cat-8')"));
        entity.HasData(ReferenceData.Personas);
    }
}
