<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;

class SubscriptionSeeder extends Seeder
{
    public function run(): void
    {
        DB::table('subscriptions')->truncate();
        DB::table('payment_methods')->truncate();
        DB::table('transactions')->truncate();

        // Берём пользователей и планы, которые уже есть в БД
        $userIds = DB::table('users')->orderBy('id')->pluck('id');
        $planIds = DB::table('subscription_plans')->orderBy('id')->pluck('id');

        // 10 подписок: каждая для своего пользователя (2-11 id)
        $subscriptions = [
            // user_id index => plan_id index, starts_at offset (days ago)
            [1, 0, 25, true],
            [2, 1, 80, true],
            [3, 2, 150, true],
            [4, 3, 350, true],
            [5, 4, 20, true],
            [6, 5, 60, true],
            [7, 0, 5,  true],
            [8, 1, 45, true],
            [9, 0, 35, false],   // истекшая
            [9, 2, 0,  true],    // текущая (тот же пользователь переоформил)
        ];

        $now = now();
        foreach ($subscriptions as [$userIdx, $planIdx, $daysAgo, $isActive]) {
            $userId = $userIds[$userIdx] ?? $userIds->last();
            $plan   = DB::table('subscription_plans')->find($planIds[$planIdx] ?? $planIds->first());

            $startsAt = now()->subDays($daysAgo)->toDateString();
            $endsAt   = now()->subDays($daysAgo)->addDays($plan->duration_days)->toDateString();

            $subId = DB::table('subscriptions')->insertGetId([
                'user_id'    => $userId,
                'plan_id'    => $plan->id,
                'starts_at'  => $startsAt,
                'ends_at'    => $endsAt,
                'is_active'  => $isActive,
                'created_at' => $now,
                'updated_at' => $now,
            ]);

            // Payment method для каждой подписки (если ещё нет у этого юзера)
            $hasCard = DB::table('payment_methods')->where('user_id', $userId)->exists();
            if (! $hasCard) {
                DB::table('payment_methods')->insert([
                    'user_id'              => $userId,
                    'card_number_encrypted'=> encrypt('4111111111111111'),
                    'card_last4'           => '1111',
                    'card_holder'          => 'USER ' . strtoupper((string) $userId),
                    'expire_month'         => str_pad(rand(1, 12), 2, '0', STR_PAD_LEFT),
                    'expire_year'          => (string) (date('Y') + rand(1, 5)),
                    'is_default'           => true,
                    'created_at'           => $now,
                    'updated_at'           => $now,
                ]);
            }

            // Транзакция на каждую подписку
            DB::table('transactions')->insert([
                'user_id'     => $userId,
                'plan_id'     => $plan->id,
                'amount'      => $plan->price,
                'status'      => $isActive ? 'completed' : 'completed',
                'description' => 'Оплата плана «' . $plan->name . '»',
                'created_at'  => $now,
                'updated_at'  => $now,
            ]);
        }
    }
}
