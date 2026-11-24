using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiAssistant.Domain;

namespace AiAssistant.Application.Appearance;

public static partial class AppearanceSettingsValidator
{
    private static readonly HashSet<string> Presets = [AppearanceSettings.RegistrationPreset, "classic", "ultramarine", "graphite", "midnight", "mint", "sunset", "terminal", "contrast"];
    private static readonly HashSet<string> Modes = ["system", "light", "dark"];
    private static readonly Dictionary<string, string[]> Options = new(StringComparer.Ordinal)
    {
        ["glow"] = ["soft", "off", "bright"],
        ["radius"] = ["standard", "sharp", "soft", "pill"],
        ["density"] = ["normal", "compact", "spacious"],
        ["font"] = ["system", "inter", "manrope", "plex"],
        ["codeFont"] = ["system", "jetbrains", "fira"],
        ["messageStyle"] = ["cards", "bubbles", "flat"],
        ["messageOutline"] = ["rectangle", "wave", "rounded", "oval", "none", "side"],
        ["chatWidth"] = ["medium", "narrow", "wide"],
        ["userSide"] = ["left", "right"],
        ["assistantAvatar"] = ["brand", "persona", "custom"],
        ["codeTheme"] = ["classic", "github", "atom-one", "tokyo-night"],
        ["messageActions"] = ["always", "hover"],
        ["pattern"] = ["none", "dots", "grid", "noise", "mesh"],
        ["motion"] = ["full", "reduced", "off"],
    };

    [GeneratedRegex(@"\A#[0-9a-fA-F]{6}\z", RegexOptions.CultureInvariant)]
    private static partial Regex HexColor();

    public static Dictionary<string, string[]> Validate(AppearanceInput input)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (input.SchemaVersion != 1) errors["schemaVersion"] = ["Версия настроек не поддерживается."];
        if (input.Preset is null || !Presets.Contains(input.Preset)) errors["preset"] = ["Неизвестный пресет."];
        if (input.ColorMode is null || !Modes.Contains(input.ColorMode)) errors["colorMode"] = ["Неизвестный режим."];
        if (input.Settings.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(input.Settings.GetRawText()) > 8192)
        {
            errors["settings"] = ["Ожидается объект JSON размером до 8 КБ."];
            return errors;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var setting in input.Settings.EnumerateObject())
        {
            if (!seen.Add(setting.Name) || !IsAllowed(setting.Name, setting.Value))
                errors[$"settings.{setting.Name}"] = ["Недопустимый параметр или значение."];
        }
        return errors;
    }

    internal static string SerializeDifferences(AppearanceInput input)
    {
        var changes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var setting in input.Settings.EnumerateObject())
        {
            var value = setting.Value;
            if (Options.TryGetValue(setting.Name, out var options))
            {
                var defaultValue = (input.Preset, setting.Name) switch
                {
                    ("midnight", "glow") => "bright",
                    ("contrast", "glow") => "off",
                    ("terminal", "codeFont") => "jetbrains",
                    (AppearanceSettings.RegistrationPreset, "font") => "plex",
                    (AppearanceSettings.RegistrationPreset, "messageStyle") => "flat",
                    (AppearanceSettings.RegistrationPreset, "messageOutline") => "wave",
                    (AppearanceSettings.RegistrationPreset, "userSide") => "right",
                    _ => options[0],
                };
                if (value.GetString() == defaultValue) continue;
            }
            if (value.ValueKind == JsonValueKind.Null ||
                (setting.Name == "fontScale" && value.GetInt32() == 100) ||
                (setting.Name == "backgroundTint" && value.GetInt32() == 11)) continue;
            changes.Add(setting.Name, value);
        }
        return JsonSerializer.Serialize(changes);
    }

    private static bool IsAllowed(string key, JsonElement value)
    {
        if (Options.TryGetValue(key, out var options))
            return value.ValueKind == JsonValueKind.String && options.Contains(value.GetString(), StringComparer.Ordinal);
        return key switch
        {
            "accent" or "assistantAvatarColor" or "headingColor" => IsColor(value),
            "accent2" => IsColor(value) || value.ValueKind == JsonValueKind.String && value.GetString() == "auto",
            "backgroundTint" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var tint) && tint is >= 0 and <= 30,
            "fontScale" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var scale) && scale is 90 or 100 or 110 or 120,
            _ => false,
        };
    }

    private static bool IsColor(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.String && HexColor().IsMatch(value.GetString()!);
}
