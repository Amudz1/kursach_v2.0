using AiAssistant.Application.Common;
using AiAssistant.Application.Persistence;
using AiAssistant.Domain;
using MediatR;

namespace AiAssistant.Application.Files;

public sealed record GetAttachment(Guid UserId, Guid Id) : IRequest<Result<AttachmentDownload>>;

public sealed class AttachmentHandlers(IAssistantData data, IAttachmentStorage storage) :
    IRequestHandler<GetAttachment, Result<AttachmentDownload>>
{
    public async Task<Result<AttachmentDownload>> Handle(GetAttachment request, CancellationToken cancellationToken)
    {
        var item = await data.FirstAsync(
            from attachment in data.Query<Attachment>()
            join message in data.Query<Message>() on attachment.MessageId equals message.Id
            join chat in data.Query<Chat>() on message.ChatId equals chat.Id
            where attachment.Id == request.Id && chat.UserId == request.UserId
            select attachment, cancellationToken);
        var stream = item is null ? null : storage.OpenRead(item.StorageKey);
        return item is null || stream is null
            ? Result<AttachmentDownload>.Fail(ErrorKind.NotFound, "attachment_not_found", "Вложение не найдено.")
            : Result<AttachmentDownload>.Ok(new(item.FileName, item.ContentType, stream));
    }
}
