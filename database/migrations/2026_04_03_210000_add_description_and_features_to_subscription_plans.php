<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::table('subscription_plans', function (Blueprint $table) {
            $table->text('description')->nullable()->after('name');
            $table->json('features')->nullable()->after('description');
            $table->integer('sort_order')->default(0)->after('prompt_limit');
        });
    }

    public function down(): void
    {
        Schema::table('subscription_plans', function (Blueprint $table) {
            $table->dropColumn(['description', 'features', 'sort_order']);
        });
    }
};