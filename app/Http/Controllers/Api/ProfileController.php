<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Services\SubscriptionService;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Hash;

class ProfileController extends Controller
{
    public function __construct(private SubscriptionService $subService) {}

    public function show(Request $request)
    {
        $user  = $request->user()->load('paymentMethods');
        $status = $this->subService->getStatus($user);

        return response()->json([
            'user' => [
                'id'       => $user->id,
                'username' => $user->username,
                'email'    => $user->email,
            ],
            'subscription'   => $status,
            'payment_methods'=> $user->paymentMethods->map(fn($p) => [
                'id'            => $p->id,
                'masked_number' => $p->masked_number,
                'card_holder'   => $p->card_holder,
                'expire'        => "{$p->expire_month}/{$p->expire_year}",
                'is_default'    => $p->is_default,
            ]),
            'remaining_prompts' => $user->hasActiveSubscription()
                ? null : $user->getRemainingPrompts(),
        ]);
    }

    public function updateEmail(Request $request)
    {
        $request->validate([
            'email'    => 'required|email|unique:users,email,' . $request->user()->id,
            'password' => 'required|string',
        ]);

        if (!Hash::check($request->password, $request->user()->password)) {
            return response()->json(['message' => 'Неверный пароль.'], 403);
        }

        $request->user()->update(['email' => $request->email]);
        return response()->json(['message' => 'Email обновлён.']);
    }

    public function updatePassword(Request $request)
    {
        $request->validate([
            'current_password' => 'required|string',
            'password'         => 'required|string|min:6|confirmed',
        ]);

        if (!Hash::check($request->current_password, $request->user()->password)) {
            return response()->json(['message' => 'Неверный текущий пароль.'], 403);
        }

        $request->user()->update(['password' => $request->password]);
        return response()->json(['message' => 'Пароль изменён.']);
    }
}
