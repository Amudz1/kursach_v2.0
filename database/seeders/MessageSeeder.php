<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;

class MessageSeeder extends Seeder
{
    public function run(): void
    {
        DB::table('messages')->truncate();

        $chats   = DB::table('chats')->orderBy('id')->get();
        $userIds = DB::table('users')->orderBy('id')->pluck('id');

        // По 2 сообщения на каждый чат (user + assistant) — итого 20 >= 10 записей
        // Первые 5 чатов дадут 10 пар = 10 записей в таблице messages (по условию «10 записей»).
        // Добавим ровно 10 сообщений: 5 user + 5 assistant по первым 5 чатам.
        $pairs = [
            [
                'user_msg' => 'Привет! Помоги мне написать профессиональное резюме разработчика.',
                'ai_msg'   => "Конечно! Давайте начнём с основных разделов резюме:\n\n1. **Контактная информация** — имя, email, LinkedIn, GitHub\n2. **Профессиональный профиль** — 2-3 предложения о вашем опыте\n3. **Навыки** — PHP, Laravel, Vue.js, PostgreSQL, Docker\n4. **Опыт работы** — компании, роли, достижения\n5. **Образование**\n\nРасскажите о своём стеке и опыте — я сформирую текст.",
            ],
            [
                'user_msg' => 'Что такое JWT и как он работает?',
                'ai_msg'   => "**JWT (JSON Web Token)** — стандарт для безопасной передачи данных между сторонами.\n\nСтруктура: `Header.Payload.Signature`\n\n- **Header** — алгоритм подписи (HS256, RS256)\n- **Payload** — claims: `sub`, `iat`, `exp`, произвольные данные\n- **Signature** — HMAC/RSA подпись для верификации\n\nСервер не хранит токен — проверяет подпись при каждом запросе. Это делает JWT stateless и масштабируемым.",
            ],
            [
                'user_msg' => 'Предложи темы для дипломной работы по веб-разработке.',
                'ai_msg'   => "Актуальные темы для диплома:\n\n1. Разработка AI-ассистента с RAG-архитектурой\n2. Микросервисная платформа на Laravel + Kubernetes\n3. Real-time система аналитики на WebSockets\n4. Progressive Web App для e-commerce\n5. Система управления контентом с headless CMS\n\nКакое направление вам ближе — backend, frontend или full-stack?",
            ],
            [
                'user_msg' => 'Переведи на английский: «Разработка и тестирование REST API»',
                'ai_msg'   => "**Перевод:** *Development and Testing of REST API*\n\nВарианты в зависимости от контекста:\n- *REST API Development and Testing* — для заголовков\n- *Building and Testing RESTful APIs* — более разговорный\n- *REST API Design, Development, and Testing* — для научных работ",
            ],
            [
                'user_msg' => 'Напиши SQL-запрос: топ-5 пользователей по количеству сообщений за последние 30 дней.',
                'ai_msg'   => "```sql\nSELECT\n    u.id,\n    u.username,\n    COUNT(m.id) AS message_count\nFROM users u\nJOIN chats c ON c.user_id = u.id\nJOIN messages m ON m.chat_id = c.id\nWHERE m.created_at >= NOW() - INTERVAL '30 days'\nGROUP BY u.id, u.username\nORDER BY message_count DESC\nLIMIT 5;\n```\nЗапрос использует JOIN по двум таблицам, фильтр по дате и агрегацию с сортировкой.",
            ],
        ];

        $now = now();
        $chatList = $chats->take(5);

        foreach ($chatList as $i => $chat) {
            $userId = $userIds[$i % count($userIds)];
            $pair   = $pairs[$i];
            $base   = now()->subDays((int) (abs(strtotime($chat->last_message_at) - time()) / 86400));

            DB::table('messages')->insert([
                'chat_id'         => $chat->id,
                'user_id'         => $userId,
                'role'            => 'user',
                'content'         => $pair['user_msg'],
                'model_used'      => null,
                'attachment_path' => null,
                'attachment_name' => null,
                'created_at'      => $base->copy()->subMinutes(5),
                'updated_at'      => $base->copy()->subMinutes(5),
            ]);

            DB::table('messages')->insert([
                'chat_id'         => $chat->id,
                'user_id'         => $userId,
                'role'            => 'assistant',
                'content'         => $pair['ai_msg'],
                'model_used'      => 'anthropic/claude-3.5-sonnet',
                'attachment_path' => null,
                'attachment_name' => null,
                'created_at'      => $base->copy()->subMinutes(4),
                'updated_at'      => $base->copy()->subMinutes(4),
            ]);
        }
    }
}
