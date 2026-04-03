<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;

class SubscriptionPlanSeeder extends Seeder
{
    public function run(): void
    {
        DB::table('subscription_plans')->truncate();

        $plans = [
            [
                'name'             => '1 месяц',
                'duration_days'    => 30,
                'price'            => 9.99,
                'discount_percent' => 0.00,
                'prompt_limit'     => 0,
            ],
            [
                'name'             => '3 месяца',
                'duration_days'    => 90,
                'price'            => 28.77,
                'discount_percent' => 4.00,
                'prompt_limit'     => 0,
            ],
            [
                'name'             => '6 месяцев',
                'duration_days'    => 180,
                'price'            => 53.95,
                'discount_percent' => 10.00,
                'prompt_limit'     => 0,
            ],
            [
                'name'             => '1 год',
                'duration_days'    => 365,
                'price'            => 95.90,
                'discount_percent' => 20.00,
                'prompt_limit'     => 0,
            ],
            [
                'name'             => 'Студент — 1 месяц',
                'duration_days'    => 30,
                'price'            => 4.99,
                'discount_percent' => 50.00,
                'prompt_limit'     => 300,
            ],
            [
                'name'             => 'Студент — 3 месяца',
                'duration_days'    => 90,
                'price'            => 13.99,
                'discount_percent' => 53.00,
                'prompt_limit'     => 300,
            ],
            [
                'name'             => 'Команда — 1 месяц',
                'duration_days'    => 30,
                'price'            => 49.99,
                'discount_percent' => 0.00,
                'prompt_limit'     => 0,
            ],
            [
                'name'             => 'Команда — 1 год',
                'duration_days'    => 365,
                'price'            => 479.90,
                'discount_percent' => 20.00,
                'prompt_limit'     => 0,
            ],
            [
                'name'             => 'Разовый доступ',
                'duration_days'    => 1,
                'price'            => 1.99,
                'discount_percent' => 0.00,
                'prompt_limit'     => 20,
            ],
            [
                'name'             => 'Пробный — 7 дней',
                'duration_days'    => 7,
                'price'            => 0.00,
                'discount_percent' => 100.00,
                'prompt_limit'     => 50,
            ],
        ];

        $now = now();
        foreach ($plans as $plan) {
            DB::table('subscription_plans')->insert(
                array_merge($plan, ['created_at' => $now, 'updated_at' => $now])
            );
        }
    }
}
