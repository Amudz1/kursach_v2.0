<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\SubscriptionPlan;
use App\Services\SubscriptionService;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Validator;

class SubscriptionController extends Controller
{
    public function __construct(private SubscriptionService $service) {}

    /** Список тарифных планов */
    public function plans()
    {
        $plans = SubscriptionPlan::orderBy('duration_days')->get()
            ->map(fn($p) => [
                'id'               => $p->id,
                'name'             => $p->name,
                'duration_days'    => $p->duration_days,
                'price'            => (float) $p->price,
                'discount_percent' => (float) $p->discount_percent,
                'final_price'      => (float) $p->final_price,
                'savings'          => (float) $p->savings,
            ]);

        return response()->json($plans);
    }

    /** Оформить подписку */
    public function purchase(Request $request)
    {
        $v = Validator::make($request->all(), [
            'plan_id'      => 'required|exists:subscription_plans,id',
            'card_number'  => ['required', 'string', 'regex:/^\d{16}$/'],
            'card_holder'  => 'required|string|min:3|max:100|regex:/^[A-Za-z\s]+$/',
            'expire_month' => ['required', 'string', 'regex:/^(0[1-9]|1[0-2])$/'],
            'expire_year'  => ['required', 'string', 'regex:/^\d{4}$/',
                function ($attr, $val, $fail) use ($request) {
                    $month = $request->expire_month;
                    $year  = (int) $val;
                    $mon   = (int) $month;
                    $now   = now();
                    if ($year < $now->year || ($year == $now->year && $mon < $now->month)) {
                        $fail('Срок действия карты истёк.');
                    }
                }
            ],
            'cvv'          => ['required', 'string', 'regex:/^\d{3,4}$/'],
            'save_card'    => 'boolean',
        ], [
            'card_number.regex'  => 'Номер карты должен содержать 16 цифр.',
            'card_holder.regex'  => 'Имя держателя карты только латинскими буквами.',
            'expire_month.regex' => 'Формат месяца: ММ (01-12).',
            'expire_year.regex'  => 'Формат года: ГГГГ.',
            'cvv.regex'          => 'CVV должен содержать 3-4 цифры.',
        ]);

        if ($v->fails()) {
            return response()->json(['errors' => $v->errors()], 422);
        }

        $plan = SubscriptionPlan::findOrFail($request->plan_id);
        $user = $request->user();

        // Имитация обработки платежа (5 секунд — на фронте)
        $subscription = $this->service->create($user, $plan);

        // Сохраняем карту если пользователь согласился
        $savedCard = null;
        if ($request->boolean('save_card')) {
            $savedCard = $this->service->savePaymentMethod($user, [
                'card_number'  => $request->card_number,
                'card_holder'  => $request->card_holder,
                'expire_month' => $request->expire_month,
                'expire_year'  => $request->expire_year,
            ]);
        }

        return response()->json([
            'message'      => 'Подписка успешно оформлена!',
            'subscription' => [
                'plan_name'     => $plan->name,
                'ends_at'       => $subscription->ends_at->format('d.m.Y'),
                'days_remaining'=> $subscription->days_remaining,
            ],
            'saved_card' => $savedCard ? $savedCard->masked_number : null,
        ]);
    }

    /** Статус подписки текущего пользователя */
    public function status(Request $request)
    {
        return response()->json(
            $this->service->getStatus($request->user())
        );
    }
}
