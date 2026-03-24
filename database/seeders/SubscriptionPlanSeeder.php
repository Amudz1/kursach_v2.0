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
                'name'             => '1 месяц',
                'duration_days'    => 30,
                'price'            => 9.99,
                'discount_percent' => 0,
                'prompt_limit'     => 0,
            ],
            [
                'name'             => '3 месяца',
                'duration_days'    => 90,
                'price'            => 28.77, // 9.99*3 - 4%
                'discount_percent' => 4,
                'prompt_limit'     => 0,
            ],
            [
                'name'             => '6 месяцев',
                'duration_days'    => 180,
                'price'            => 53.15, // 9.99*6 - 6%
                'discount_percent' => 6,
                'prompt_limit'     => 0,
            ],
            [
                'name'             => '1 год',
                'duration_days'    => 365,
                'price'            => 95.90, // 9.99*12 - 8%
                'discount_percent' => 8,
                'prompt_limit'     => 0,
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
