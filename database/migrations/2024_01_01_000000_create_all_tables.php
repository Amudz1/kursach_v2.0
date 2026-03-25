<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * Единая миграция — создаёт все таблицы проекта AI Helper Budz.
 *
 * Порядок создания важен из-за внешних ключей:
 *   1. users                   — базовая таблица пользователей
 *   2. personal_access_tokens  — токены Sanctum (зависит от users через morphs)
 *   3. sessions                — сессии Laravel (зависит от users)
 *   4. cache / cache_locks     — кэш Laravel
 *   5. subscription_plans      — тарифы (независима)
 *   6. subscriptions           — подписки (users + subscription_plans)
 *   7. payment_methods         — карты (users)
 *   8. transactions            — транзакции (users + subscription_plans)
 *   9. chats                   — чаты (users)
 *  10. messages                — сообщения (chats + users)
 *  11. request_limits          — лимиты (users)
 *
 * Для запуска: php artisan migrate:fresh --seed
 */
return new class extends Migration
{
    public function up(): void
    {
        // ── 1. users ─────────────────────────────────────────────
        Schema::create('users', function (Blueprint $table) {
            $table->id();
            $table->string('username')->unique();
            $table->string('email')->unique()->nullable();
            $table->string('password');
            $table->boolean('is_admin')->default(false);
            $table->timestamp('email_verified_at')->nullable();
            $table->rememberToken();
            $table->timestamps();
        });

        // ── 2. personal_access_tokens (Laravel Sanctum) ──────────
        Schema::create('personal_access_tokens', function (Blueprint $table) {
            $table->id();
            $table->morphs('tokenable');          // tokenable_type + tokenable_id
            $table->string('name');
            $table->string('token', 64)->unique();
            $table->text('abilities')->nullable();
            $table->timestamp('last_used_at')->nullable();
            $table->timestamp('expires_at')->nullable();
            $table->timestamps();
        });

        // ── 3. sessions ──────────────────────────────────────────
        Schema::create('sessions', function (Blueprint $table) {
            $table->string('id')->primary();
            $table->foreignId('user_id')->nullable()->index();
            $table->string('ip_address', 45)->nullable();
            $table->text('user_agent')->nullable();
            $table->longText('payload');
            $table->integer('last_activity')->index();
        });

        // ── 4. cache / cache_locks ────────────────────────────────
        Schema::create('cache', function (Blueprint $table) {
            $table->string('key')->primary();
            $table->mediumText('value');
            $table->integer('expiration');
        });

        Schema::create('cache_locks', function (Blueprint $table) {
            $table->string('key')->primary();
            $table->string('owner');
            $table->integer('expiration');
        });

        // ── 5. subscription_plans ─────────────────────────────────
        Schema::create('subscription_plans', function (Blueprint $table) {
            $table->id();
            $table->string('name');                              // «1 месяц», «3 месяца» …
            $table->integer('duration_days');                    // 30, 90, 180, 365
            $table->decimal('price', 8, 2);
            $table->decimal('discount_percent', 5, 2)->default(0);
            $table->integer('prompt_limit')->default(0);         // 0 = безлимит
            $table->timestamps();
        });

        // ── 6. subscriptions ──────────────────────────────────────
        Schema::create('subscriptions', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->foreignId('plan_id')->constrained('subscription_plans');
            $table->date('starts_at');
            $table->date('ends_at');
            $table->boolean('is_active')->default(true);
            $table->timestamps();
        });

        // ── 7. payment_methods ────────────────────────────────────
        Schema::create('payment_methods', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->text('card_number_encrypted');               // AES-256
            $table->string('card_last4', 4);                     // последние 4 цифры
            $table->string('card_holder');
            $table->string('expire_month', 2);
            $table->string('expire_year', 4);
            $table->boolean('is_default')->default(false);
            $table->timestamps();
        });

        // ── 8. transactions ───────────────────────────────────────
        Schema::create('transactions', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->foreignId('plan_id')->constrained('subscription_plans');
            $table->decimal('amount', 8, 2);
            $table->string('status')->default('completed');      // pending|completed|failed
            $table->string('description')->nullable();
            $table->timestamps();
        });

        // ── 9. chats ──────────────────────────────────────────────
        Schema::create('chats', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->string('title')->nullable();                  // первые слова первого промта
            $table->integer('message_count')->default(0);
            $table->timestamp('last_message_at')->nullable();
            $table->timestamps();
        });

        // ── 10. messages ──────────────────────────────────────────
        Schema::create('messages', function (Blueprint $table) {
            $table->id();
            $table->foreignId('chat_id')->constrained()->cascadeOnDelete();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->enum('role', ['user', 'assistant']);
            $table->longText('content');
            $table->string('model_used')->nullable();
            $table->string('attachment_path')->nullable();
            $table->string('attachment_name')->nullable();
            $table->timestamps();
        });

        // ── 11. request_limits ────────────────────────────────────
        Schema::create('request_limits', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->unique()->constrained()->cascadeOnDelete();
            $table->integer('used_today')->default(0);
            $table->integer('daily_limit')->default(12);
            $table->date('reset_date')->nullable();
            $table->timestamps();
        });
    }

    public function down(): void
    {
        // Удаляем в обратном порядке (из-за внешних ключей)
        Schema::dropIfExists('request_limits');
        Schema::dropIfExists('messages');
        Schema::dropIfExists('chats');
        Schema::dropIfExists('transactions');
        Schema::dropIfExists('payment_methods');
        Schema::dropIfExists('subscriptions');
        Schema::dropIfExists('subscription_plans');
        Schema::dropIfExists('cache_locks');
        Schema::dropIfExists('cache');
        Schema::dropIfExists('sessions');
        Schema::dropIfExists('personal_access_tokens');
        Schema::dropIfExists('users');
    }
};
