<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\{User, RequestLimit};
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Hash;
use Illuminate\Validation\Rules\Password;

class AuthController extends Controller
{
    public function register(Request $request)
    {
        $data = $request->validate([
            'username' => 'required|string|min:3|max:30|unique:users|alpha_dash',
            'email'    => 'nullable|email|unique:users',
            'password' => ['required', 'confirmed', Password::min(6)],
        ], [
            'username.unique'    => 'Этот логин уже занят.',
            'username.alpha_dash'=> 'Логин может содержать только буквы, цифры, дефис и подчёркивание.',
            'email.unique'       => 'Этот email уже зарегистрирован.',
            'password.confirmed' => 'Пароли не совпадают.',
        ]);

        $user = User::create([
            'username' => $data['username'],
            'email'    => $data['email'] ?? null,
            'password' => $data['password'],
        ]);

        // Создаём лимит запросов
        RequestLimit::create([
            'user_id'    => $user->id,
            'used_today' => 0,
            'daily_limit'=> config('app.free_prompt_limit', 12),
            'reset_date' => today(),
        ]);

        $token = $user->createToken('auth_token')->plainTextToken;

        return response()->json([
            'token' => $token,
            'user'  => $this->userResource($user),
        ], 201);
    }

    public function login(Request $request)
    {
        $data = $request->validate([
            'username' => 'required|string',
            'password' => 'required|string',
        ]);

        $user = User::where('username', $data['username'])->first();

        if (!$user || !Hash::check($data['password'], $user->password)) {
            return response()->json(['message' => 'Неверный логин или пароль.'], 401);
        }

        $token = $user->createToken('auth_token')->plainTextToken;

        return response()->json([
            'token' => $token,
            'user'  => $this->userResource($user),
        ]);
    }

    public function logout(Request $request)
    {
        $request->user()->currentAccessToken()->delete();
        return response()->json(['message' => 'Выход выполнен успешно.']);
    }

    public function me(Request $request)
    {
        return response()->json($this->userResource($request->user()));
    }

    public function deleteAccount(Request $request)
    {
        $request->validate([
            'password' => 'required|string',
        ]);

        $user = $request->user();

        if (!Hash::check($request->password, $user->password)) {
            return response()->json(['message' => 'Неверный пароль.'], 403);
        }

        // Удаляем все токены
        $user->tokens()->delete();
        $user->delete();

        return response()->json(['message' => 'Аккаунт удалён.']);
    }

    private function userResource(User $user): array
    {
        $sub = $user->getActiveSubscription();

        return [
            'id'                => $user->id,
            'username'          => $user->username,
            'email'             => $user->email,
            'is_admin'          => $user->is_admin,
            'has_subscription'  => $user->hasActiveSubscription(),
            'subscription'      => $sub ? [
                'plan_name'     => $sub->plan->name,
                'ends_at'       => $sub->ends_at->format('d.m.Y'),
                'days_remaining'=> $sub->days_remaining,
            ] : null,
            'remaining_prompts' => $user->hasActiveSubscription()
                ? null
                : $user->getRemainingPrompts(),
        ];
    }
}
