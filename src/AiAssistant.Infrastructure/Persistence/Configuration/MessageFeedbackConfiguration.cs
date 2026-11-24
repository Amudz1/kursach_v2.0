using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class MessageFeedbackConfiguration : IEntityTypeConfiguration<MessageFeedback>
{
    public void Configure(EntityTypeBuilder<MessageFeedback> entity)
    {
        entity.ToTable("message_feedback");
        entity.HasKey(feedback => feedback.Id);
        entity.Property(feedback => feedback.Reason).HasMaxLength(40);
        entity.Property(feedback => feedback.Comment).HasMaxLength(500);
        entity.HasIndex(feedback => new { feedback.MessageId, feedback.UserId }).IsUnique();
        entity.HasIndex(feedback => new { feedback.UserId, feedback.CreatedAt });
        entity.HasOne<User>().WithMany().HasForeignKey(feedback => feedback.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Message>().WithMany().HasForeignKey(feedback => feedback.MessageId).OnDelete(DeleteBehavior.Cascade);
        entity.ToTable(table => table.HasCheckConstraint("ck_message_feedback_rating", "rating IN (-1, 1)"));
    }
}
