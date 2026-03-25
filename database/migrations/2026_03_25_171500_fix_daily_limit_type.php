<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Run the migrations.
     */
    public function up(): void
    {
        // Проверяем, существует ли таблица, чтобы не было ошибок на пустых БД
        if (Schema::hasTable('request_limits')) {
            Schema::table('request_limits', function (Blueprint $table) {
                // Меняем тип column daily_limit с integer на bigInteger
                // Это исправит ошибку SQLSTATE[22003]: Numeric value out of range
                $table->bigInteger('daily_limit')->change();
            });
        }
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        if (Schema::hasTable('request_limits')) {
            Schema::table('request_limits', function (Blueprint $table) {
                $table->integer('daily_limit')->change();
            });
        }
    }
};
