using System.IO.Compression;
using System.Text;
using System.Xml;
using AiAssistant.Application.Files;
using UglyToad.PdfPig;

namespace AiAssistant.Infrastructure.Files;

public sealed record AttachmentRoot(string Directory);

public sealed class LocalAttachmentStorage(AttachmentRoot root) : IAttachmentStorage
{
    private const int MaxText = 40000;
    private const int MaxPdfPages = 75;
    private const int MaxArchiveExpansion = 48 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];

    public async Task<PreparedAttachment> PrepareAsync(IncomingAttachment incoming, CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(incoming.FileName.Replace('\\', '/')).Trim();
        if (name.Length is 0 or > 255 || name.Any(char.IsControl) || incoming.Length is <= 0 or > 16 * 1024 * 1024)
            throw new FileRejectedException("Проверьте имя и размер файла.");

        var bytes = new byte[(int)incoming.Length];
        try
        {
            await incoming.Content.ReadExactlyAsync(bytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(name).ToLowerInvariant();
            return extension switch
            {
                ".txt" => new(name, "text/plain", bytes, ExtractText(bytes)),
                ".md" => new(name, "text/markdown", bytes, ExtractText(bytes)),
                ".pdf" when bytes.AsSpan().StartsWith("%PDF-"u8) =>
                    new(name, "application/pdf", bytes, ExtractPdf(bytes, cancellationToken)),
                ".docx" when bytes.AsSpan().StartsWith("PK\u0003\u0004"u8) =>
                    new(name, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", bytes,
                        ExtractDocx(bytes, cancellationToken)),
                ".png" when bytes.Length >= 24 && bytes.AsSpan().StartsWith(PngSignature) &&
                    bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) => new(name, "image/png", bytes, null),
                ".jpg" or ".jpeg" when bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 &&
                    bytes[^2] == 255 && bytes[^1] == 217 => new(name, "image/jpeg", bytes, null),
                _ => throw new FileRejectedException("Формат файла не поддерживается или содержимое не соответствует расширению.")
            };
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or FileRejectedException or OutOfMemoryException))
        {
            throw new FileRejectedException("Не удалось прочитать вложение. Проверьте формат и целостность файла.");
        }
    }

    public async Task WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root.Directory);
        var path = PathFor(key);
        var temporary = path + ".upload";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            File.Move(temporary, path);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public Stream? OpenRead(string key)
    {
        try
        {
            return File.Open(PathFor(key), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public void Delete(string key) => File.Delete(PathFor(key, allowTemporary: true));

    public IEnumerable<string> OldKeys(DateTimeOffset cutoff)
    {
        if (!Directory.Exists(root.Directory)) yield break;
        foreach (var path in Directory.EnumerateFiles(root.Directory))
        {
            var key = Path.GetFileName(path);
            if (IsStorageKey(key, allowTemporary: true) && File.GetLastWriteTimeUtc(path) < cutoff.UtcDateTime)
                yield return key;
        }
    }

    private string PathFor(string key, bool allowTemporary = false)
    {
        if (!IsStorageKey(key, allowTemporary)) throw new ArgumentException("Invalid private storage key.", nameof(key));
        return Path.Combine(root.Directory, key);
    }

    private static bool IsStorageKey(string key, bool allowTemporary)
    {
        const string temporarySuffix = ".upload";
        var id = allowTemporary && key.EndsWith(temporarySuffix, StringComparison.Ordinal)
            ? key[..^temporarySuffix.Length] : key;
        return Guid.TryParseExact(id, "N", out _);
    }

    private static string ExtractText(byte[] bytes)
    {
        var text = Utf8.GetString(bytes).TrimStart('\uFEFF');
        if (text.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
            throw new FileRejectedException("Текстовое вложение должно быть в UTF-8.");
        return text.Length <= MaxText ? text : text[..MaxText];
    }

    private static string ExtractPdf(byte[] bytes, CancellationToken cancellationToken)
    {
        using var pdf = PdfDocument.Open(bytes);
        if (pdf.NumberOfPages > MaxPdfPages)
            throw new FileRejectedException("PDF должен содержать не больше 75 страниц.");
        var text = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendBounded(text, string.Join(" ", page.GetWords().Select(word => word.Text)));
            if (text.Length >= MaxText) break;
        }
        if (text.Length == 0) throw new FileRejectedException("В PDF нет текстового слоя.");
        return text.ToString();
    }

    private static string ExtractDocx(byte[] bytes, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes, false), ZipArchiveMode.Read);
        if (archive.Entries.Count > 800 || archive.Entries.Sum(entry => entry.Length) > MaxArchiveExpansion)
            throw new FileRejectedException("Распакованный DOCX слишком велик.");
        var document = archive.GetEntry("word/document.xml") ??
            throw new FileRejectedException("В DOCX отсутствует текстовый документ.");
        using var stream = document.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxArchiveExpansion
        });
        const string wordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var text = new StringBuilder();
        while (reader.Read() && text.Length < MaxText)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "t" && reader.NamespaceURI == wordNamespace)
                AppendBounded(text, reader.ReadString());
        }
        return text.ToString();
    }

    private static void AppendBounded(StringBuilder text, string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment) || text.Length >= MaxText) return;
        if (text.Length > 0) text.Append('\n');
        text.Append(fragment, 0, Math.Min(fragment.Length, MaxText - text.Length));
    }
}
