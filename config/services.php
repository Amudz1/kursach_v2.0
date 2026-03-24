<?php

return [
    'openrouter' => [
        'api_key'  => env('OPENROUTER_API_KEY'),
        'base_url' => env('OPENROUTER_BASE_URL', 'https://openrouter.ai/api/v1'),
        'model'    => env('DEFAULT_AI_MODEL', 'deepseek/deepseek-chat'),
    ],
];
