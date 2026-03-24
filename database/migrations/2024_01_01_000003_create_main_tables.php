<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration {
    public function up(): void
    {
        // Подписки пользователей
        Schema::create('subscriptions', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->foreignId('plan_id')->constrained('subscription_plans');
            $table->date('starts_at');
            $table->date('ends_at');
            $table->boolean('is_active')->default(true);
            $table->timestamps();
        });

        // Зашифрованные платёжные данные
        Schema::create('payment_methods', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->text('card_number_encrypted'); // AES-256
            $table->string('card_last4', 4);       // последние 4 для отображения
            $table->string('card_holder');
            $table->string('expire_month', 2);
            $table->string('expire_year', 4);
            $table->boolean('is_default')->default(false);
            $table->timestamps();
        });

        // Транзакции
        Schema::create('transactions', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->foreignId('plan_id')->constrained('subscription_plans');
            $table->decimal('amount', 8, 2);
            $table->string('status')->default('completed'); // pending|completed|failed
            $table->string('description')->nullable();
            $table->timestamps();
        });

        // Чаты (сессии)
        Schema::create('chats', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->string('title')->nullable(); // первые слова первого промта
            $table->integer('message_count')->default(0);
            $table->timestamp('last_message_at')->nullable();
            $table->timestamps();
        });

        // Сообщения в чате
        Schema::create('messages', function (Blueprint $table) {
            $table->id();
            $table->foreignId('chat_id')->constrained()->cascadeOnDelete();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->enum('role', ['user', 'assistant']);
            $table->longText('content');
            $table->string('model_used')->nullable();
            $table->string('attachment_path')->nullable(); // загруженный файл
            $table->string('attachment_name')->nullable();
            $table->timestamps();
        });

        // Лимиты запросов (для бесплатных)
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
        Schema::dropIfExists('request_limits');
        Schema::dropIfExists('messages');
        Schema::dropIfExists('chats');
        Schema::dropIfExists('transactions');
        Schema::dropIfExists('payment_methods');
        Schema::dropIfExists('subscriptions');
    }
};
