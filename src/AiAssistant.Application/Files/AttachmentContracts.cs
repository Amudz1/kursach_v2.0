namespace AiAssistant.Application.Files;

public sealed record IncomingAttachment(string FileName, long Length, Stream Content);
public sealed record PreparedAttachment(string FileName, string ContentType, byte[] Bytes, string? ExtractedText)
{
    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);
}
public sealed record AttachmentView(Guid Id, string FileName, string ContentType, long Size);
public sealed record AttachmentDownload(string FileName, string ContentType, Stream Content);

public sealed class FileRejectedException(string message) : Exception(message);

/// <summary>Manages attachment bytes outside the web root and validates supported formats.</summary>
public interface IAttachmentStorage
{
    /// <summary>Validates and reads a single bounded upload, extracting textual content where possible.</summary>
    Task<PreparedAttachment> PrepareAsync(IncomingAttachment incoming, CancellationToken cancellationToken);

    /// <summary>Atomically writes an object under an application-generated key.</summary>
    Task WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken);

    /// <summary>Opens a private object for an already authorized caller.</summary>
    Stream? OpenRead(string key);

    /// <summary>Deletes a stored object if present.</summary>
    void Delete(string key);

    /// <summary>Enumerates only valid storage keys older than the requested cutoff.</summary>
    IEnumerable<string> OldKeys(DateTimeOffset cutoff);
}
