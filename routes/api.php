<?php

use App\Http\Controllers\Api\{AuthController, ChatController, ProfileController, SubscriptionController};
use Illuminate\Support\Facades\Route;

// ── Публичные маршруты ────────────────────────────────────────
Route::post('/register', [AuthController::class, 'register']);
Route::post('/login',    [AuthController::class, 'login']);
Route::get('/subscription/plans', [SubscriptionController::class, 'plans']);

// ── Защищённые маршруты (Sanctum) ─────────────────────────────
Route::middleware('auth:sanctum')->group(function () {

    // Auth
    Route::post('/logout',         [AuthController::class, 'logout']);
    Route::get('/me',              [AuthController::class, 'me']);
    Route::delete('/account',      [AuthController::class, 'deleteAccount']);

    // Profile
    Route::get('/profile',              [ProfileController::class, 'show']);
    Route::put('/profile/email',        [ProfileController::class, 'updateEmail']);
    Route::put('/profile/password',     [ProfileController::class, 'updatePassword']);

    // Subscription
    Route::post('/subscription/purchase', [SubscriptionController::class, 'purchase']);
    Route::get('/subscription/status',    [SubscriptionController::class, 'status']);

    // Chats
    Route::get('/chats',                     [ChatController::class, 'index']);
    Route::post('/chats',                    [ChatController::class, 'store']);
    Route::get('/chats/{chat}/messages',     [ChatController::class, 'messages']);
    Route::post('/chats/{chat}/messages',    [ChatController::class, 'sendMessage']);
    Route::delete('/chats/{chat}',           [ChatController::class, 'destroy']);
});
