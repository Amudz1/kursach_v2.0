using AiAssistant.Domain;

namespace AiAssistant.Infrastructure.Persistence;

internal static class ReferenceData
{
    public const string TextModel = "deepseek/deepseek-chat";
    public const string VisionModel = "openai/gpt-4o-mini";

    public static Persona[] Personas =>
    [
        Persona(1, "Универсальный помощник", "Работа с вопросами и повседневными задачами",
            "Помогай решать задачи. Когда данных недостаточно, уточняй вводные. Отделяй факты от предположений.", 0.5),
        Persona(2, "Преподаватель", "Объяснения и упражнения",
            "Объясняй понятия на уровне пользователя. Давай небольшой пример и проверочный вопрос.", 0.45),
        Persona(3, "Ревьюер кода", "Проверка корректности и читаемости кода",
            "Проверяй корректность, безопасность и обработку ошибок. Приводи конкретные замечания и способы исправления.", 0.2),
        Persona(4, "Переводчик", "Перевод с учётом контекста и регистра",
            "Сохраняй смысл, стиль и терминологию исходника. Указывай существенные неоднозначности.", 0.25),
        Persona(5, "Редактор текста", "Структура, ясность и редактура",
            "Исправляй текст, сохраняя авторский смысл. Кратко объясняй значимые редакторские правки.", 0.35)
    ];

    public static SubscriptionPlan[] Plans =>
    [
        Plan(1, "Free", "Свободный доступ", 0, 0, 12, 1, 20, 2, [TextModel]),
        Plan(2, "Student", "Для учёбы", 199, 30, 60, 5, 100, 6, [TextModel, VisionModel]),
        Plan(3, "Pro", "Для регулярной работы", 399, 30, 250, 15, 300, 10, [TextModel, VisionModel]),
        Plan(4, "Premium", "Без дневного лимита", 699, 30, null, 30, 1000, 16, [TextModel, VisionModel])
    ];

    private static Persona Persona(int number, string name, string description, string prompt, double temperature) => new()
    {
        Id = Guid.Parse($"f1000000-0000-4000-8000-{number:D12}"),
        Name = name,
        Description = description,
        SystemPrompt = prompt,
        Temperature = temperature,
        DefaultModel = TextModel
    };

    private static SubscriptionPlan Plan(int number, string code, string name, decimal price, int duration,
        int? dailyLimit, int personas, int memories, int attachmentMiB, string[] models) => new()
    {
        Id = Guid.Parse($"f2000000-0000-4000-8000-{number:D12}"),
        Code = code,
        Name = name,
        Description = name,
        Price = price,
        DurationDays = duration,
        DailyRequestLimit = dailyLimit,
        MaxPersonas = personas,
        MaxMemories = memories,
        MaxAttachmentBytes = attachmentMiB * 1024 * 1024,
        AllowedModels = models,
        SortOrder = number
    };
}
