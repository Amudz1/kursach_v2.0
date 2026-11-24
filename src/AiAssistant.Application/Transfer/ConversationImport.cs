using System.Globalization;
using System.Text.Json;

namespace AiAssistant.Application.Transfer;

internal static class ConversationImport
{
    private const int ChatLimit = 500;
    private const int MessageLimit = 10000;
    private const int TurnLimit = 100000;

    public static List<ImportedConversation> Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var kind = String(root, "format");
        if (kind == "budz-conversations" && (!Property(root, "version").TryGetInt32(out var version) || version != 1))
            throw new InvalidDataException("Эта версия файла Budz не поддерживается.");
        var list = Property(root, "conversations");
        var conversations = root.ValueKind == JsonValueKind.Array ? Items(root) :
            list.ValueKind == JsonValueKind.Array ? Items(list) : [root];
        var result = new List<ImportedConversation>();
        var count = 0;
        foreach (var item in conversations)
        {
            var mapping = Property(item, "mapping");
            var claude = Property(item, "chat_messages");
            var source = kind == "budz-conversations" ? "Budz" : mapping.ValueKind == JsonValueKind.Object ? "ChatGPT" :
                claude.ValueKind == JsonValueKind.Array ? "Claude" : "JSON";
            var turns = source switch
            {
                "ChatGPT" => Branch(item, mapping).Select(ReadChatGpt),
                "Claude" => Items(claude).Select(ReadClaude),
                _ => Items(Property(item, "messages")).Select(ReadPortable),
            };
            var messages = new List<TransferMessage>();
            foreach (var turn in turns)
            {
                if (turn is null) continue;
                if (++count > MessageLimit || turn.Content.Length > TurnLimit)
                    throw new InvalidDataException("Разделите выгрузку на файлы до 10 000 сообщений. Длина одного сообщения ограничена 100 000 символов.");
                messages.Add(turn);
            }
            if (messages.Count == 0) continue;
            if (result.Count >= ChatLimit) throw new InvalidDataException("Один файл может содержать до 500 диалогов.");
            var title = String(item, "title") ?? String(item, "name");
            var id = String(item, "id") ?? String(item, "uuid") ?? String(item, "conversation_id");
            var date = ParseDate(Property(item, "createdAt")) ?? ParseDate(Property(item, "create_time")) ?? ParseDate(Property(item, "created_at"));
            result.Add(new(source, id, title, date, messages));
        }
        if (result.Count == 0) throw new InvalidDataException("В файле нет текстовых диалогов Budz, ChatGPT, Claude или JSON с полями role и content.");
        return result;
    }

    private static IEnumerable<JsonElement> Branch(JsonElement chat, JsonElement mapping)
    {
        var nodeId = String(chat, "current_node");
        if (nodeId is null || !mapping.TryGetProperty(nodeId, out _))
        {
            // Some exports omit the selected node. Pick the newest leaf and follow only its ancestors.
            nodeId = mapping.EnumerateObject().Where(node => !Items(Property(node.Value, "children")).Any())
                .Select(node => new { node.Name, Date = ParseDate(Property(Property(node.Value, "message"), "create_time")) })
                .MaxBy(node => node.Date)?.Name;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var messages = new Stack<JsonElement>();
        while (nodeId is not null && seen.Add(nodeId) && mapping.TryGetProperty(nodeId, out var node))
        {
            messages.Push(Property(node, "message"));
            nodeId = String(node, "parent");
        }
        return messages;
    }

    private static TransferMessage? ReadChatGpt(JsonElement item)
    {
        if (Property(Property(item, "metadata"), "is_visually_hidden_from_conversation").ValueKind == JsonValueKind.True) return null;
        var author = Property(item, "author");
        var content = Property(item, "content");
        return Message(String(author, "role"), Text(Property(content, "parts")), Property(item, "create_time"));
    }

    private static TransferMessage? ReadClaude(JsonElement item)
    {
        var text = Text(Property(item, "text"));
        if (string.IsNullOrWhiteSpace(text)) text = Text(Property(item, "content"));
        return Message(String(item, "sender"), text, Property(item, "created_at"));
    }

    private static TransferMessage? ReadPortable(JsonElement item) =>
        Message(String(item, "role"), Text(Property(item, "content")), Property(item, "createdAt"));

    private static TransferMessage? Message(string? role, string? content, JsonElement date)
    {
        var author = role?.ToLowerInvariant() switch { "user" or "human" => "user", "assistant" or "model" => "assistant", _ => null };
        // PostgreSQL text does not support NUL. Keep visible text and line breaks intact.
        return author is null || string.IsNullOrWhiteSpace(content) ? null : new(author, content.Replace("\0", ""), ParseDate(date));
    }

    private static string? Text(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString();
        if (value.ValueKind != JsonValueKind.Array) return null;
        return string.Join('\n', value.EnumerateArray().Select(part => part.ValueKind == JsonValueKind.String ? part.GetString() :
            (String(part, "type") is null or "text" or "output_text" ? String(part, "text") : null))
            .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static DateTimeOffset? ParseDate(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var date) && date.Year is >= 1900 and < 9900) return date.ToUniversalTime();
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var seconds) && seconds is >= 0 and < 253000000000)
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(seconds * 1000));
        return null;
    }

    private static IEnumerable<JsonElement> Items(JsonElement value) => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];
    private static JsonElement Property(JsonElement item, string name) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) ? value : default;
    private static string? String(JsonElement item, string name) => Property(item, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
}
