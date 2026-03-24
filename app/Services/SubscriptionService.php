<?php

namespace App\Services;

use App\Models\{User, SubscriptionPlan, Subscription, Transaction, PaymentMethod, RequestLimit};
use Carbon\Carbon;

/**
 * Сервис управления подписками.
 * SRP: только логика оформления, проверки и отображения подписок.
 */
class SubscriptionService
{
    /**
     * Создать подписку после успешной оплаты.
     */
    public function create(User $user, SubscriptionPlan $plan): Subscription
    {
        // Деактивируем предыдущую подписку
        $user->subscriptions()->where('is_active', true)->update(['is_active' => false]);

        $starts = Carbon::today();
        $ends   = $starts->copy()->addDays($plan->duration_days);

        $subscription = Subscription::create([
            'user_id'   => $user->id,
            'plan_id'   => $plan->id,
            'starts_at' => $starts,
            'ends_at'   => $ends,
            'is_active' => true,
        ]);

        // Записываем транзакцию
        Transaction::create([
            'user_id'     => $user->id,
            'plan_id'     => $plan->id,
            'amount'      => $plan->final_price,
            'status'      => 'completed',
            'description' => "Подписка: {$plan->name}",
        ]);

        // Обнуляем лимиты запросов — теперь безлимит
        RequestLimit::updateOrCreate(
            ['user_id' => $user->id],
            ['used_today' => 0, 'daily_limit' => PHP_INT_MAX, 'reset_date' => today()]
        );

        return $subscription->load('plan');
    }

    /**
     * Сохранить платёжные данные (зашифровано).
     */
    public function savePaymentMethod(User $user, array $data): PaymentMethod
    {
        // Делаем все остальные карты не дефолтными
        $user->paymentMethods()->update(['is_default' => false]);

        return PaymentMethod::create([
            'user_id'                => $user->id,
            'card_number_encrypted'  => $data['card_number'], // setCardNumberEncryptedAttribute зашифрует
            'card_last4'             => substr($data['card_number'], -4),
            'card_holder'            => strtoupper($data['card_holder']),
            'expire_month'           => $data['expire_month'],
            'expire_year'            => $data['expire_year'],
            'is_default'             => true,
        ]);
    }

    /**
     * Получить информацию о подписке для профиля.
     */
    public function getStatus(User $user): array
    {
        $sub = $user->getActiveSubscription();

        if (!$sub) {
            return [
                'has_subscription' => false,
                'status'           => 'Нет активной подписки',
                'plan_name'        => null,
                'ends_at'          => null,
                'days_remaining'   => 0,
                'prompt_limit'     => config('app.free_prompt_limit', 12),
            ];
        }

        return [
            'has_subscription' => true,
            'status'           => $sub->status_text,
            'plan_name'        => $sub->plan->name,
            'ends_at'          => $sub->ends_at->format('d.m.Y'),
            'days_remaining'   => $sub->days_remaining,
            'prompt_limit'     => 'Безлимит',
        ];
    }
}
