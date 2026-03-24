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
                return 'Произошла ошибка при обращении к AI. Попробуйте ещё раз.';
            }

            return $response->json('choices.0.message.content')
                ?? 'Получен пустой ответ от AI.';

        } catch (\Exception $e) {
            Log::error('AiService exception', ['message' => $e->getMessage()]);
            return "Ошибка подключения к AI: {$e->getMessage()}";
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
