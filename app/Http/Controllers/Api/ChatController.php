<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\{Chat, Message, RequestLimit};
use App\Services\AiService;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Storage;

class ChatController extends Controller
{
    public function __construct(private AiService $ai) {}

    /** Список чатов пользователя */
    public function index(Request $request)
    {
        $chats = $request->user()
            ->chats()
            ->select('id', 'title', 'message_count', 'last_message_at', 'created_at')
            ->latest('last_message_at')
            ->get();

        return response()->json($chats);
    }

    /** Создать новый чат */
    public function store(Request $request)
    {
        $chat = Chat::create([
            'user_id' => $request->user()->id,
            'title'   => 'Новый чат',
        ]);

        return response()->json($chat, 201);
    }

    /** Сообщения конкретного чата */
    public function messages(Request $request, Chat $chat)
    {
        $this->authorize('view', $chat);

        $messages = $chat->messages()
            ->select('id', 'role', 'content', 'attachment_name', 'attachment_path', 'created_at')
            ->get()
            ->map(fn($m) => [
                'id'              => $m->id,
                'role'            => $m->role,
                'content'         => $m->content,
                'attachment_name' => $m->attachment_name,
                'has_attachment'  => $m->hasAttachment(),
                'created_at'      => $m->created_at->format('H:i'),
            ]);

        return response()->json($messages);
    }

    /** Отправить сообщение и получить ответ AI */
    public function sendMessage(Request $request, Chat $chat)
    {
        $this->authorize('view', $chat);

        $user = $request->user();

        // Проверяем лимит промтов
        if (!$user->canSendMessage()) {
            return response()->json([
                'message'       => 'Лимит бесплатных запросов исчерпан. Оформите подписку для продолжения.',
                'limit_reached' => true,
            ], 403);
        }

        $request->validate([
            'content'    => 'required_without:attachment|string|max:10000',
            'attachment' => 'nullable|file|max:10240|mimes:txt,pdf,doc,docx,png,jpg,jpeg,gif',
        ]);

        try {
            // Сохраняем вложение
            $attachmentPath = null;
            $attachmentName = null;
            if ($request->hasFile('attachment')) {
                $file           = $request->file('attachment');
                $attachmentPath = $file->store('attachments', 'public');
                $attachmentName = $file->getClientOriginalName();
            }

            $content = $request->input('content', '');
            if ($attachmentName) {
                $content = $content ?: "Прикреплён файл: {$attachmentName}";
            }

            // Сохраняем сообщение пользователя
            $userMessage = Message::create([
                'chat_id'         => $chat->id,
                'user_id'         => $user->id,
                'role'            => 'user',
                'content'         => $content,
                'attachment_path' => $attachmentPath,
                'attachment_name' => $attachmentName,
            ]);

            // Обновляем заголовок чата из первого сообщения
            if ($chat->message_count === 0) {
                $chat->update(['title' => Chat::generateTitle($content)]);
            }
            
            // Строим историю для AI (последние 20 сообщений)
            $history = $chat->messages()
                ->latest()
                ->take(20)
                ->get()
                ->reverse()
                ->map(fn($m) => ['role' => $m->role, 'content' => $m->content])
                ->values()
                ->toArray();

            // Получаем ответ AI
            $aiContent = $this->ai->chat($history);

            // Сохраняем ответ AI
            $aiMessage = Message::create([
                'chat_id'    => $chat->id,
                'user_id'    => $user->id,
                'role'       => 'assistant',
                'content'    => $aiContent,
                'model_used' => config('services.openrouter.model'),
            ]);

            // Обновляем счётчики чата
            $chat->increment('message_count', 2);
            $chat->update(['last_message_at' => now()]);

            // Увеличиваем счётчик использованных промтов (только для бесплатных)
            if (!$user->hasActiveSubscription()) {
                $limit = RequestLimit::firstOrCreate(
                    ['user_id' => $user->id],
                    ['daily_limit' => config('app.free_prompt_limit', 12), 'reset_date' => today()]
                );

                if ($limit->reset_date !== today()->toDateString()) {
                    $limit->update(['used_today' => 1, 'reset_date' => today()]);
                } else {
                    $limit->increment('used_today');
                }
            }

            return response()->json([
                'user_message' => [
                    'id'              => $userMessage->id,
                    'role'            => 'user',
                    'content'         => $userMessage->content,
                    'attachment_name' => $attachmentName,
                    'has_attachment'  => !empty($attachmentPath),
                    'created_at'      => $userMessage->created_at->format('H:i'),
                ],
                'ai_message' => [
                    'id'         => $aiMessage->id,
                    'role'       => 'assistant',
                    'content'    => $aiContent,
                    'created_at' => $aiMessage->created_at->format('H:i'),
                ],
                'remaining_prompts' => $user->getRemainingPrompts(),
            ]);

        } catch (\Illuminate\Database\QueryException $e) {
            // Ловим конкретно ошибки БД (например, SQLSTATE[22003] Numeric value out of range)
            \Illuminate\Support\Facades\Log::error('DB Error in Chat: ' . $e->getMessage(), [
                'sql_state' => $e->getCode(),
                'chat_id'   => $chat->id,
                'user_id'   => $user->id,
            ]);

            return response()->json([
                'message' => 'Ошибка базы данных при обновлении лимитов. Попробуйте позже.',
                'debug'   => env('APP_DEBUG') ? $e->getMessage() : null,
            ], 503); // 503 Service Unavailable
        } catch (\Exception $e) {
            // Все остальные ошибки (сеть, AI, таймаут)
            \Illuminate\Support\Facades\Log::error('AI Service Error in Chat: ' . $e->getMessage(), [
                'chat_id' => $chat->id,
                'user_id' => $user->id,
            ]);

            return response()->json([
                'message' => 'Внутренняя ошибка AI сервиса. Попробуйте позже.',
            ], 503);
        }
    }

    /** Удалить чат */
    public function destroy(Request $request, Chat $chat)
    {
        $this->authorize('delete', $chat);
        $chat->delete();
        return response()->json(['message' => 'Чат удалён.']);
    }
}
