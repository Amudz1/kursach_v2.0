<?php

namespace App\Services;

use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Log;

/**
 * Сервис работы с AI через OpenRouter API.
 * SRP: только запросы к нейросети.
 */
class AiService
{
    private string $apiKey;
    private string $baseUrl;
    private string $model;

    public function __construct()
    {
        $this->apiKey  = config('services.openrouter.api_key');
        $this->baseUrl = config('services.openrouter.base_url', 'https://openrouter.ai/api/v1');
        $this->model   = config('services.openrouter.model', 'deepseek/deepseek-chat');
    }

    /**
     * Получить ответ от AI с историей переписки.
     *
     * @param array $messages [['role' => 'user'|'assistant', 'content' => '...']]
     */
    public function chat(array $messages): string
    {
        // Ключ не задан в .env — не идём на сервер, возвращаем понятное сообщение
        if (empty($this->apiKey)) {
            Log::error('AiService: OPENROUTER_API_KEY не задан в .env');
            return 'AI недоступен: не настроен API-ключ OpenRouter. Добавьте OPENROUTER_API_KEY в файл .env.';
        }

        try {
            $response = Http::withHeaders([
                'Authorization' => "Bearer {$this->apiKey}",
                'HTTP-Referer'  => config('app.url'),
                'X-Title'       => config('app.name'),
            ])->timeout(60)->post("{$this->baseUrl}/chat/completions", [
                'model'    => $this->model,
                'messages' => $messages,
            ]);

            if ($response->failed()) {
                Log::error('OpenRouter API error', [
                    'status' => $response->status(),
                    'body'   => $response->body(),
                ]);

                // Конкретные коды — понятные сообщения пользователю
                return match ($response->status()) {
                    401 => 'Ошибка авторизации AI: неверный API-ключ OpenRouter.',
                    429 => 'AI временно недоступен: превышен лимит запросов. Попробуйте позже.',
                    503 => 'AI сервис временно недоступен. Попробуйте позже.',
                    default => 'Произошла ошибка при обращении к AI (код ' . $response->status() . '). Попробуйте ещё раз.',
                };
            }

            $content = $response->json('choices.0.message.content');

            if (empty($content)) {
                Log::warning('AiService: пустой ответ от OpenRouter', ['body' => $response->body()]);
                return 'AI вернул пустой ответ. Попробуйте переформулировать вопрос.';
            }

            return $content;

        } catch (\Illuminate\Http\Client\ConnectionException $e) {
            Log::error('AiService: нет соединения с OpenRouter', ['message' => $e->getMessage()]);
            return 'Нет соединения с AI сервисом. Проверьте подключение к интернету.';
        } catch (\Exception $e) {
            Log::error('AiService exception', ['message' => $e->getMessage()]);
            return 'Внутренняя ошибка AI сервиса. Попробуйте позже.';
        }
    }

    /**
     * Простой одиночный запрос без истории.
     */
    public function ask(string $question): string
    {
        return $this->chat([['role' => 'user', 'content' => $question]]);
    }
}
