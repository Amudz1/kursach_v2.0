using AiAssistant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiAssistant.Infrastructure.Persistence.Configuration;

internal sealed class MessageBookmarkConfiguration : IEntityTypeConfiguration<MessageBookmark>
{
    public void Configure(EntityTypeBuilder<MessageBookmark> entity)
    {
        entity.ToTable("message_bookmarks");
        entity.HasKey(bookmark => new { bookmark.UserId, bookmark.MessageId });
        entity.HasOne<User>().WithMany().HasForeignKey(bookmark => bookmark.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Message>().WithMany().HasForeignKey(bookmark => bookmark.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}
