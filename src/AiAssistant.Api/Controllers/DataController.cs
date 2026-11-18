using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AiAssistant.Application.Common;
using AiAssistant.Application.Transfer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AiAssistant.Api.Controllers;

[Authorize]
[Route("api/v1/data")]
[EnableRateLimiting("chat")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DataController(ISender sender) : ApplicationController
{
    private const int FileLimit = 12 * 1024 * 1024;
    private static readonly JsonSerializerOptions ArchiveJson = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true
    };

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(FileLimit + 4096)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileLimit)]
    public async Task<IActionResult> Import(IFormFile file, [FromForm] Guid? workspaceId, CancellationToken ct)
    {
        if (file.Length is <= 0 or > FileLimit)
            return Failure(new(ErrorKind.Validation, "invalid_archive", "Выберите JSON-файл размером до 12 МБ."));
        using var reader = new StreamReader(file.OpenReadStream(), new UTF8Encoding(false, true), true);
        try { return From(await sender.Send(new ImportConversations(CurrentUserId, workspaceId, await reader.ReadToEndAsync(ct)), ct)); }
        catch (DecoderFallbackException)
        {
            return Failure(new(ErrorKind.Validation, "invalid_encoding", "Сохраните файл в Unicode и повторите импорт."));
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct, string format = "json", Guid? workspaceId = null, Guid? chatId = null,
        bool includeAccount = false)
    {
        if (format is not ("json" or "markdown"))
            return Failure(new(ErrorKind.Validation, "invalid_export_format", "Выберите JSON или Markdown."));
        var result = await sender.Send(new ExportConversations(CurrentUserId, workspaceId, chatId, includeAccount && format == "json"), ct);
        if (!result.IsSuccess) return Failure(result.Error!);
        var filename = chatId.HasValue ? "budz-dialogue" : workspaceId.HasValue ? "budz-workspace" : "budz-data";
        if (format == "markdown") return File(Encoding.UTF8.GetBytes(Markdown(result.Value!)), "text/markdown; charset=utf-8", filename + ".md");
        var json = JsonSerializer.SerializeToUtf8Bytes(result.Value, ArchiveJson);
        return json.Length <= FileLimit ? File(json, "application/json; charset=utf-8", filename + ".json") :
            Failure(new(ErrorKind.Validation, "export_too_large", "Выберите отдельный диалог или пространство для экспорта JSON."));
    }

    private static string Markdown(TransferArchive archive)
    {
        var text = new StringBuilder("# Диалоги Budz\n");
        foreach (var chat in archive.Conversations)
        {
            text.Append("\n## ").AppendLine(chat.Title.Replace('\n', ' ').Replace('\r', ' '));
            foreach (var message in chat.Messages)
                text.Append(message.Role == "user" ? "\n### Пользователь\n\n" : "\n### Ассистент\n\n").AppendLine(message.Content);
        }
        return text.ToString();
    }
}
