<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration {
    public function up(): void
    {
        Schema::create('subscription_plans', function (Blueprint $table) {
            $table->id();
            $table->string('name');           // "1 месяц", "3 месяца" ...
            $table->integer('duration_days'); // 30, 90, 180, 365
            $table->decimal('price', 8, 2);
            $table->decimal('discount_percent', 5, 2)->default(0);
            $table->integer('prompt_limit')->default(0); // 0 = безлимит
            $table->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('subscription_plans');
    }
};
