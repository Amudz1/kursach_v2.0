<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;

class SubscriptionPlanSeeder extends Seeder
{
    public function run(): void
    {
        $plans = [
            [
                'name' => 'Пробный — 7 дней',
                'description' => 'Для знакомства с сервисом. Краткий доступ ко всем базовым возможностям без долгого обязательства.',
                'duration_days' => 7,
                'price' => 0.00,
                'discount_percent' => 100.00,
                'prompt_limit' => 50,
                'features' => json_encode([
                    'До 50 запросов',
                    'Базовый AI-чат',
                    'История диалогов',
                    '7 дней доступа',
                ], JSON_UNESCAPED_UNICODE),
                'sort_order' => 1,
            ],
            [
                'name' => 'Разовый доступ',
                'description' => 'Подходит для быстрых задач на один день: перевод, резюме, код, тексты и небольшие запросы.',
                'duration_days' => 1,
                'price' => 1.99,
                'discount_percent' => 0.00,
                'prompt_limit' => 20,
                'features' => json_encode([
                    'До 20 запросов',
                    'Все базовые инструменты',
                    'Подходит для разовых задач',
                    '1 день доступа',
                ], JSON_UNESCAPED_UNICODE),
                'sort_order' => 2,
            ],
            [
                'name' => '1 месяц',
                'description' => 'Полноценный старт для личного использования. Безлимитные запросы и все основные функции сервиса.',
                'duration_days' => 30,
                'price' => 9.99,
                'discount_percent' => 0.00,
                'prompt_limit' => 0,
                'features' => json_encode([
                    'Безлимитные запросы',
                    'Полная история чатов',
                    'Поддержка markdown',
                    'Загрузка файлов',
                ], JSON_UNESCAPED_UNICODE),
                'sort_order' => 3,
            ],
            [
                'name' => '3 месяца',
                'description' => 'Оптимальный выбор для регулярной работы. Больше выгоды по цене и удобный среднесрочный период.',
                'duration_days' => 90,
                'price' => 28.77,
                'discount_percent' => 4.00,
                'prompt_limit' => 0,
                'features' => json_encode([
                    'Все функции плана 1 месяц',
                    'Скидка на длительный период',
                    'Более выгодная цена',
                    '90 дней доступа',
                ], JSON_UNESCAPED_UNICODE),
                'sort_order' => 4,
            ],
            [
                'name' => '6 месяцев',
                'description' => 'Для активных пользователей, которым важна стабильная работа с сервисом и более выгодная цена на долгий срок.',
                'duration_days' => 180,
                'price' => 53.15,
                'discount_percent' => 6.00,
                'prompt_limit' => 0,
                'features' => json_encode([
                    'Все функции плана 3 месяца',
                    'Приоритетная обработка запросов',
                    'Увеличенный период доступа',
                    'Максимальная выгода для активных пользователей',
                ], JSON_UNESCAPED_UNICODE),
                'sort_order' => 5,
            ],
            [
                'name' => '1 год',
                'description' => 'Максимальный тариф для постоянного использования. Самая выгодная стоимость и полный набор возможностей.',
                'duration_days' => 365,
                'price' => 95.90,
                'discount_percent' => 8.00,
                'prompt_limit' => 0,
                'features' => json_encode([
                    'Все функции плана 6 месяцев',
                    'Лучшее соотношение цены и срока',
                    'Приоритетная поддержка',
                    '365 дней полного доступа',
                ], JSON_UNESCAPED_UNICODE),
                'sort_order' => 6,
            ],
        ];

        foreach ($plans as $plan) {
            DB::table('subscription_plans')->updateOrInsert(
                ['name' => $plan['name']],
                array_merge($plan, [
                    'created_at' => now(),
                    'updated_at' => now(),
                ])
            );
        }
    }
}